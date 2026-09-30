namespace ShoeExportInvoice.Api.Services;

public interface IFileStorage
{
    Task<string> SaveAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken = default);
    Task<byte[]?> ReadAsync(string reference, CancellationToken cancellationToken = default);
    Task DeleteAsync(string reference, CancellationToken cancellationToken = default);
}

public sealed class FileStorageOptions
{
    public string Provider { get; set; } = "Local";
    public string Endpoint { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
}
