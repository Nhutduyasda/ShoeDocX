using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;

namespace ShoeExportInvoice.Api.Services;

public static class CustomsStorageMaintenance
{
    public static async Task CheckIntegrityAsync(AppDbContext db, IWebHostEnvironment env, XnkOptions options, ILogger logger, IFileStorage? storage = null)
    {
        var rows = await db.ShipmentOrders.AsNoTracking()
            .Where(s => s.CustomsAttachmentFilePath != null || s.CustomsAttachmentFileName != null)
            .Select(s => new { s.Id, s.CustomsAttachmentFilePath, s.CustomsAttachmentFileName }).ToListAsync();
        foreach (var row in rows)
        {
            var relative = row.CustomsAttachmentFilePath ?? Path.Combine(options.CustomsStoragePath, row.CustomsAttachmentFileName!);
            if (relative.StartsWith("r2:", StringComparison.Ordinal))
            {
                if (storage == null || await storage.ReadAsync(relative) == null)
                    throw new IOException($"Missing durable customs attachment for shipment {row.Id}.");
                continue;
            }
            var path = Path.GetFullPath(Path.Combine(env.ContentRootPath, relative));
            if (!File.Exists(path)) logger.LogError("Missing customs attachment for shipment {ShipmentId}: {Path}", row.Id, relative);
        }
    }

    public static async Task MigrateLegacyAsync(AppDbContext db, IWebHostEnvironment env, XnkOptions options, ILogger logger)
    {
        var legacyRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, "Uploads/Customs"));
        var targetRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.CustomsStoragePath));
        Directory.CreateDirectory(targetRoot);
        var rows = await db.ShipmentOrders.Where(s => s.CustomsAttachmentFileName != null).ToListAsync();
        foreach (var row in rows)
        {
            var source = Path.Combine(legacyRoot, Path.GetFileName(row.CustomsAttachmentFileName!));
            if (!File.Exists(source)) continue;
            var target = Path.Combine(targetRoot, Path.GetFileName(row.CustomsAttachmentFileName!));
            var temp = target + ".migrating";
            await using (var input = File.OpenRead(source)) await using (var output = File.Create(temp)) await input.CopyToAsync(output);
            byte[] sourceHash, targetHash;
            await using (var sourceStream = File.OpenRead(source)) sourceHash = await SHA256.HashDataAsync(sourceStream);
            await using (var targetStream = File.OpenRead(temp)) targetHash = await SHA256.HashDataAsync(targetStream);
            if (!sourceHash.SequenceEqual(targetHash)) { File.Delete(temp); throw new IOException($"Hash mismatch for {source}"); }
            File.Move(temp, target, true);
            row.CustomsAttachmentFilePath = Path.GetRelativePath(env.ContentRootPath, target).Replace("\\", "/");
            logger.LogInformation("Migrated customs attachment for shipment {ShipmentId}", row.Id);
        }
        await db.SaveChangesAsync();
    }
}

