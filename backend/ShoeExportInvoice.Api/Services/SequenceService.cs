using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Entities;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace ShoeExportInvoice.Api.Services;

public class SequenceService : ISequenceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SequenceService> _logger;

    // Process-level async lock for concurrency safety across threads
    private static readonly SemaphoreSlim _sequenceLock = new(1, 1);

    // Key dùng để lưu trong bảng SystemSettings
    private const string LastSequenceKey = "LastSequenceNumber";

    // Số bắt đầu mặc định nếu chưa có trong DB
    private int DefaultStartNumber => _options.FirstInvoiceNumber - 1;
    private readonly XnkOptions _options;

    public SequenceService(AppDbContext context, ILogger<SequenceService> logger, Microsoft.Extensions.Options.IOptions<XnkOptions>? options = null)
    {
        _context = context;
        _logger = logger;
        _options = options?.Value ?? new XnkOptions();
    }

    /// <inheritdoc/>
    public Task<int[]> GetNextSequenceNumbersAsync(int count = 1) =>
        _context.Database.CurrentTransaction != null
            ? GetNextSequenceNumbersCoreAsync(count)
            : _context.ExecuteWithRetryAsync(() => GetNextSequenceNumbersCoreAsync(count));

    private async Task<int[]> GetNextSequenceNumbersCoreAsync(int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Số lượng phải lớn hơn 0.");
        if (count > 100) throw new ArgumentOutOfRangeException(nameof(count), "Không thể cấp quá 100 số cùng lúc.");

        await _sequenceLock.WaitAsync();
        try
        {
            await using var transaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                : null;
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == LastSequenceKey);

            int lastUsed = DefaultStartNumber;
            if (setting != null && int.TryParse(setting.Value, out var saved))
            {
                lastUsed = saved;
            }

            // Cấp N số tiếp theo
            var numbers = Enumerable.Range(lastUsed + 1, count).ToArray();
            int newLast = lastUsed + count;

            if (setting == null)
            {
                _context.SystemSettings.Add(new SystemSetting
                {
                    Key = LastSequenceKey,
                    Value = newLast.ToString(),
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                setting.Value = newLast.ToString();
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();
            _logger.LogInformation("Đã cấp {Count} số thứ tự: {Numbers}", count, string.Join(", ", numbers));
            return numbers;
        }
        finally
        {
            _sequenceLock.Release();
        }
    }

    public Task<int[]> ReservePartnerSequenceNumbersAsync(int folderId, int count = 1, int? requestedStart = null) =>
        _context.Database.CurrentTransaction != null
            ? ReservePartnerSequenceNumbersCoreAsync(folderId, count, requestedStart)
            : _context.ExecuteWithRetryAsync(() => ReservePartnerSequenceNumbersCoreAsync(folderId, count, requestedStart));

    private async Task<int[]> ReservePartnerSequenceNumbersCoreAsync(int folderId, int count, int? requestedStart)
    {
        if (folderId <= 0) throw new ArgumentOutOfRangeException(nameof(folderId));
        if (count is <= 0 or > 100) throw new ArgumentOutOfRangeException(nameof(count));

        await _sequenceLock.WaitAsync();
        try
        {
            await using var ownedTransaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                : null;

            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var isSqlServer = _context.Database.IsSqlServer();
            var countParameter = isSqlServer ? "@count" : "$count";
            var nextParameter = isSqlServer ? "@nextSeq" : "$nextSeq";
            var updatedParameter = isSqlServer ? "@updatedAt" : "$updatedAt";
            var folderParameter = isSqlServer ? "@folderId" : "$folderId";
            int first;
            if (requestedStart.HasValue && requestedStart.Value > 0)
            {
                first = requestedStart.Value;
                var nextSeq = first + count;
                command.CommandText = isSqlServer ? """
                    UPDATE MasterDataFolders WITH (UPDLOCK, ROWLOCK)
                    SET CurrentSequenceNumber = CASE
                            WHEN CurrentSequenceNumber > @nextSeq THEN CurrentSequenceNumber
                            ELSE @nextSeq
                        END,
                        UpdatedAt = @updatedAt
                    WHERE Id = @folderId;
                    """ : """
                    UPDATE MasterDataFolders
                    SET CurrentSequenceNumber = CASE 
                            WHEN CurrentSequenceNumber > $nextSeq THEN CurrentSequenceNumber 
                            ELSE $nextSeq 
                        END,
                        UpdatedAt = $updatedAt
                    WHERE Id = $folderId;
                    """;
                command.Parameters.Add(CreateParameter(command, nextParameter, nextSeq));
                command.Parameters.Add(CreateParameter(command, updatedParameter, DateTime.UtcNow));
                command.Parameters.Add(CreateParameter(command, folderParameter, folderId));

                var rows = await command.ExecuteNonQueryAsync();
                if (rows == 0)
                    throw new KeyNotFoundException($"Không tìm thấy thư mục đối tác #{folderId}.");
            }
            else
            {
                command.CommandText = isSqlServer ? """
                    UPDATE MasterDataFolders WITH (UPDLOCK, ROWLOCK)
                    SET CurrentSequenceNumber = CurrentSequenceNumber + @count,
                        UpdatedAt = @updatedAt
                    OUTPUT deleted.CurrentSequenceNumber
                    WHERE Id = @folderId;
                    """ : """
                    UPDATE MasterDataFolders
                    SET CurrentSequenceNumber = CurrentSequenceNumber + $count,
                        UpdatedAt = $updatedAt
                    WHERE Id = $folderId
                    RETURNING CurrentSequenceNumber - $count;
                    """;
                command.Parameters.Add(CreateParameter(command, countParameter, count));
                command.Parameters.Add(CreateParameter(command, updatedParameter, DateTime.UtcNow));
                command.Parameters.Add(CreateParameter(command, folderParameter, folderId));

                var scalar = await command.ExecuteScalarAsync();
                if (scalar == null || scalar == DBNull.Value)
                    throw new KeyNotFoundException($"Không tìm thấy thư mục đối tác #{folderId}.");

                first = Convert.ToInt32(scalar);
            }

            // Heal sequence drift deterministically while the serializable transaction/row lock is held.
            // This can happen after data migration or a manual sequence correction.
            var pattern = await _context.MasterDataFolders.AsNoTracking()
                .Where(folder => folder.Id == folderId)
                .Select(folder => folder.InvoiceNoPattern)
                .SingleAsync();
            while (await HasInvoiceConflictAsync(pattern, first, count)) first++;
            await using (var alignCommand = connection.CreateCommand())
            {
                alignCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                alignCommand.CommandText = isSqlServer
                    ? "UPDATE MasterDataFolders SET CurrentSequenceNumber = CASE WHEN CurrentSequenceNumber > @nextSeq THEN CurrentSequenceNumber ELSE @nextSeq END, UpdatedAt = @updatedAt WHERE Id = @folderId;"
                    : "UPDATE MasterDataFolders SET CurrentSequenceNumber = CASE WHEN CurrentSequenceNumber > $nextSeq THEN CurrentSequenceNumber ELSE $nextSeq END, UpdatedAt = $updatedAt WHERE Id = $folderId;";
                alignCommand.Parameters.Add(CreateParameter(alignCommand, nextParameter, first + count));
                alignCommand.Parameters.Add(CreateParameter(alignCommand, updatedParameter, DateTime.UtcNow));
                alignCommand.Parameters.Add(CreateParameter(alignCommand, folderParameter, folderId));
                await alignCommand.ExecuteNonQueryAsync();
            }

            if (ownedTransaction != null) await ownedTransaction.CommitAsync();
            // Only the partner sequence was changed by raw SQL. Keep source
            // warehouse batches tracked so export can persist their new status.
            foreach (var entry in _context.ChangeTracker.Entries<MasterDataFolder>()
                         .Where(entry => entry.Entity.Id == folderId).ToList())
                entry.State = EntityState.Detached;
            return Enumerable.Range(first, count).ToArray();
        }
        finally
        {
            _sequenceLock.Release();
        }
    }

    private async Task<bool> HasInvoiceConflictAsync(string pattern, int first, int count)
    {
        var invoiceNumbers = Enumerable.Range(first, count)
            .Select(number => PartnerDocumentPatternFormatter.InvoiceNo(pattern, number)).ToList();
        return await _context.ShipmentOrders.AsNoTracking().AnyAsync(order => invoiceNumbers.Contains(order.InvoiceNo));
    }

    private static System.Data.Common.DbParameter CreateParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        return parameter;
    }

    /// <inheritdoc/>
    public async Task SetNextSequenceNumberAsync(int nextNumber)
    {
        if (_context.Database.CurrentTransaction != null)
            await SetNextSequenceNumberCoreAsync(nextNumber);
        else
            await _context.ExecuteWithRetryAsync(async () =>
            {
                await SetNextSequenceNumberCoreAsync(nextNumber);
                return true;
            });
    }

    private async Task SetNextSequenceNumberCoreAsync(int nextNumber)
    {
        if (nextNumber <= 0) throw new ArgumentOutOfRangeException(nameof(nextNumber), "Số thứ tự phải lớn hơn 0.");

        await _sequenceLock.WaitAsync();
        try
        {
            // Lưu (nextNumber - 1) vì lần tiếp theo sẽ cấp nextNumber
            int lastUsed = nextNumber - 1;

            await using var transaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                : null;
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == LastSequenceKey);

            if (setting == null)
            {
                _context.SystemSettings.Add(new SystemSetting
                {
                    Key = LastSequenceKey,
                    Value = lastUsed.ToString(),
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                if (int.TryParse(setting.Value, out var current) && current > lastUsed) lastUsed = current;
                setting.Value = lastUsed.ToString();
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();
            _logger.LogInformation("Đã ghi đè số thứ tự: lần tiếp theo sẽ bắt đầu từ {NextNumber}", nextNumber);
        }
        finally
        {
            _sequenceLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetCurrentNextNumberAsync()
    {
        var setting = await _context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == LastSequenceKey);

        if (setting != null && int.TryParse(setting.Value, out var saved))
        {
            return saved + 1;
        }

        return DefaultStartNumber + 1;
    }

    /// <inheritdoc/>
    public string ToInvoiceNo(int sequenceNumber)
    {
        return $"{_options.InvoicePrefix}{sequenceNumber}";
    }

    /// <inheritdoc/>
    public string ToFileName(int sequenceNumber)
    {
        return $"{_options.FilePrefix}{sequenceNumber}.xlsx";
    }

    /// <inheritdoc/>
    public int? ExtractSequenceNumber(string invoiceNo)
    {
        if (string.IsNullOrWhiteSpace(invoiceNo)) return null;

        var match = Regex.Match(invoiceNo.Trim(), @"\d+$");
        if (match.Success && int.TryParse(match.Value, out var num) && num > 0)
        {
            return num;
        }
        return null;
    }

    /// <summary>
    /// Trích xuất tên file chuẩn từ Invoice No.
    /// Ví dụ: "KMHD-NEW2026-0233" -> "KM3-26-DH233.xlsx"
    /// Fallback: "CUSTOM-INV-ABC" -> "CUSTOM-INV-ABC.xlsx"
    /// </summary>
    public string InvoiceNoToFileName(string invoiceNo)
    {
        var seqNum = ExtractSequenceNumber(invoiceNo);
        if (seqNum.HasValue)
        {
            return ToFileName(seqNum.Value);
        }

        // Fallback: dùng safeInvoiceNo
        var safe = invoiceNo.Trim()
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace("?", "")
            .Replace("*", "")
            .Replace(":", "")
            .Replace("|", "")
            .Replace("\"", "")
            .Replace("<", "")
            .Replace(">", "");
        return $"{safe}.xlsx";
    }
}
