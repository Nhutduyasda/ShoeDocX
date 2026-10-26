using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniversalDocDecoder.Protocol;

public static class Limits
{
    public const int Input = 1_048_576, Header = 4096, Pages = 10, Dimension = 4096;
    public const int PagePixels = 4_000_000, JobPixels = 20_000_000, TextBytes = 1_048_576;
    public const int Output = JobPixels * 4 + TextBytes + 65536, Memory = 256 * 1024 * 1024;
    public static readonly TimeSpan JobWall = TimeSpan.FromSeconds(30), PageWall = TimeSpan.FromSeconds(10);
    public const string Build = "shoedocx-poc1/pdfium-157.0.8100/skia-4.153.1";
    public static void Require(bool value, string code) { if (!value) throw new ProtocolFault(code); }
}

public sealed class ProtocolFault(string code) : Exception(code);
public sealed class DecoderRejected(string code) : Exception(code);
public sealed record DecodeRequest(int Version, string MediaType, string Sha256, int ByteLength);
public sealed record Message(int Version, string Kind, string Build, string Sha256 = "", int Page = 0,
    int Count = 0, int Width = 0, int Height = 0, int PixelBytes = 0, int TextBytes = 0, string Code = "");
public sealed record DecodedPage(int Number, int Width, int Height, byte[] Bgra, string Text);

public static class Wire
{
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 8,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, PropertyNameCaseInsensitive = false };
    public static async Task Write<T>(Stream output, T message, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        Limits.Require(bytes.Length is > 0 and <= Limits.Header, "IPC_HEADER_LIMIT");
        var length = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        await output.WriteAsync(length, ct); await output.WriteAsync(bytes, ct); await output.FlushAsync(ct);
    }
    public static async Task<T> Read<T>(Stream input, CancellationToken ct = default)
    {
        var length = new byte[4]; await input.ReadExactlyAsync(length, ct);
        int count = BinaryPrimitives.ReadInt32LittleEndian(length);
        Limits.Require(count is > 0 and <= Limits.Header, "IPC_HEADER_LIMIT");
        var bytes = new byte[count]; await input.ReadExactlyAsync(bytes, ct);
        return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new ProtocolFault("IPC_NULL");
    }
    public static void Validate(DecodeRequest r)
    {
        Limits.Require(r.Version == 1 && r.ByteLength is > 0 and <= Limits.Input && r.Sha256 is { Length: 64 } &&
            r.Sha256.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'), "IPC_REQUEST_INVALID");
        Limits.Require(r.MediaType is "application/pdf" or "image/jpeg" or "image/png" or "image/webp", "FORMAT_UNSUPPORTED");
    }
    public static void Hash(DecodeRequest r, byte[] bytes) => Limits.Require(
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), Convert.FromHexString(r.Sha256)), "SOURCE_HASH_MISMATCH");
    public static void Dimensions(int width, int height) => Limits.Require(width is > 0 and <= Limits.Dimension &&
        height is > 0 and <= Limits.Dimension && (long)width * height <= Limits.PagePixels, "PIXEL_LIMIT");
}
