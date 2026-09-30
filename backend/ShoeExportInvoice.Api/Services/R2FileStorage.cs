using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace ShoeExportInvoice.Api.Services;

public sealed class R2FileStorage : IFileStorage
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly LocalFileStorage _bundledFiles;

    public R2FileStorage(IAmazonS3 client, IOptions<FileStorageOptions> options, IWebHostEnvironment environment)
    {
        _client = client;
        _bucket = options.Value.Bucket;
        _bundledFiles = new LocalFileStorage(environment);
    }

    public async Task<string> SaveAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken = default)
    {
        LocalFileStorage.ValidateKey(key);
        using var stream = new MemoryStream(bytes, writable: false);
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket, Key = key, InputStream = stream, ContentType = contentType,
            DisablePayloadSigning = true, DisableDefaultChecksumValidation = true,
            AutoCloseStream = false
        }, cancellationToken);
        return "r2:" + key;
    }

    public async Task<byte[]?> ReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (!reference.StartsWith("r2:", StringComparison.Ordinal))
            return await _bundledFiles.ReadAsync(reference, cancellationToken);
        var key = LocalFileStorage.ValidateKey(reference[3..]);
        try
        {
            using var response = await _client.GetObjectAsync(_bucket, key, cancellationToken);
            using var output = new MemoryStream();
            await response.ResponseStream.CopyToAsync(output, cancellationToken);
            return output.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
    }

    public async Task DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        // Historical local files and bundled templates are retained for migration/backup.
        if (!reference.StartsWith("r2:", StringComparison.Ordinal)) return;
        await _client.DeleteObjectAsync(_bucket, LocalFileStorage.ValidateKey(reference[3..]), cancellationToken);
    }
}
