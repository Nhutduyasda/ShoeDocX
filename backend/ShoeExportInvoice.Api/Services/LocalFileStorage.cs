namespace ShoeExportInvoice.Api.Services;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    public LocalFileStorage(IWebHostEnvironment environment) : this(environment.ContentRootPath) { }
    public LocalFileStorage(string root) => _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    public static string ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Path.IsPathRooted(key) || key.Contains('\\') || key.Contains(':') ||
            key.Split('/').Any(p => p is "" or "." or ".."))
            throw new ArgumentException("Invalid file storage key.");
        return key;
    }

    private string Resolve(string key)
    {
        ValidateKey(key);
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(Path.EndsInDirectorySeparator(_root) ? _root : _root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("File must be inside the storage root.");
        return path;
    }

    public async Task<string> SaveAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Unique keys are never overwritten; database references are updated after this completes.
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await stream.WriteAsync(bytes, cancellationToken);
        return key;
    }

    public async Task<byte[]?> ReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        var path = Resolve(reference);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    public Task DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        File.Delete(Resolve(reference));
        return Task.CompletedTask;
    }
}
