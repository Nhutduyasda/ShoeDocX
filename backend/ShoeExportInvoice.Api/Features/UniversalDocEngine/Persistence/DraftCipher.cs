using System.Security.Cryptography;
using System.Text.Json;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Persistence;

public sealed class DraftCipher
{
    private readonly Dictionary<string, byte[]> keys = new(StringComparer.Ordinal);
    public string ActiveKeyId { get; }
    public DraftCipher(IConfiguration configuration)
    {
        try {
            if (!configuration.GetValue<bool>("UniversalDocDrafts:Enabled")) throw new FormatException();
            ActiveKeyId = configuration["UniversalDocDrafts:ActiveKeyId"] ?? throw new FormatException();
            if (ActiveKeyId.Length is < 1 or > 64) throw new FormatException();
            foreach (var entry in configuration.GetSection("UniversalDocDrafts:Keys").GetChildren()) {
                var key = Convert.FromBase64String(entry.Value ?? ""); if (key.Length != 32 || entry.Key.Length > 64) throw new FormatException(); keys.Add(entry.Key, key);
            }
            if (!keys.ContainsKey(ActiveKeyId)) throw new FormatException();
        } catch (Exception e) when (e is FormatException or ArgumentException) { throw new StudioFault(503, "DRAFT_CONFIGURATION_UNAVAILABLE"); }
    }
    private static byte[] Aad(DraftRecord r) => JsonSerializer.SerializeToUtf8Bytes(new object[] {
        r.Id, r.OwnerId, r.TenantId, r.Workflow, r.Revision, r.CreatedAt, r.ExpiresAt, r.Format, r.KeyId });
    public byte[] Encrypt(DraftRecord r, byte[] plaintext)
    {
        if (plaintext.Length is <= 0 or > DraftSnapshot.MaximumPayload) throw new StudioFault(400, "DRAFT_PAYLOAD_LIMIT");
        var result = new byte[plaintext.Length + 28]; RandomNumberGenerator.Fill(result.AsSpan(0, 12));
        using var aes = new AesGcm(keys[r.KeyId], 16); aes.Encrypt(result.AsSpan(0, 12), plaintext, result.AsSpan(12, plaintext.Length), result.AsSpan(result.Length - 16), Aad(r)); return result;
    }
    public byte[] Decrypt(DraftRecord r)
    {
        if (r.Format != 1 || r.Ciphertext.Length is <= 28 or > DraftSnapshot.MaximumPayload + 28 || !keys.TryGetValue(r.KeyId, out var key)) throw new StudioFault(409, "DRAFT_PAYLOAD_INVALID");
        var plain = new byte[r.Ciphertext.Length - 28];
        try { using var aes = new AesGcm(key, 16); aes.Decrypt(r.Ciphertext.AsSpan(0, 12), r.Ciphertext.AsSpan(12, plain.Length), r.Ciphertext.AsSpan(r.Ciphertext.Length - 16), plain, Aad(r)); return plain; }
        catch (CryptographicException) { CryptographicOperations.ZeroMemory(plain); throw new StudioFault(409, "DRAFT_PAYLOAD_INVALID"); }
    }
}
