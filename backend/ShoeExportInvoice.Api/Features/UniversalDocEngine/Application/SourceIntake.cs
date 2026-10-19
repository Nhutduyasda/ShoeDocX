using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

// Server-only immutable data. Never bind/serialize this record at an HTTP boundary.
public sealed record StudioSource(string Id, string Sha256, ImmutableArray<byte> Bytes, string MediaType,
    DateTimeOffset UploadedAt, string OwnerId, string TenantId, int? Width, int? Height, int? PageCount);
public sealed record StudioSourceView(string SourceId, string Sha256, long ByteLength, string MediaType,
    DateTimeOffset UploadedAt, int? Width, int? Height, int? PageCount, string Status);

public static class SourceIntake
{
    public const int MaximumFileBytes = 1024 * 1024, MaximumSources = 4;
    public const int MaximumSessionBytes = InMemoryUniversalDocSessionStore.MaximumTemplateBytes;
    private static StudioFault Invalid() => new(400, "SOURCE_MALFORMED_OR_UNSUPPORTED");
    private static void Require(bool condition) { if (!condition) throw Invalid(); }
    public static StudioSourceView View(StudioSource s) => new(s.Id, s.Sha256, s.Bytes.Length, s.MediaType,
        s.UploadedAt, s.Width, s.Height, s.PageCount, "AdmittedNotRecognized");
    public static void RequireBudget(UniversalDocSession s)
    {
        if (s.Sources.Length > MaximumSources) throw new StudioFault(400, "SOURCE_COUNT_LIMIT");
        if (s.Sources.Any(x => x.Bytes.Length is <= 0 or > MaximumFileBytes || x.OwnerId != s.OwnerId || x.TenantId != s.TenantId) ||
            (long)s.Template.Length + s.Example.Length + s.Sources.Sum(x => (long)x.Bytes.Length) > MaximumSessionBytes)
            throw new StudioFault(400, "SESSION_BYTE_LIMIT");
    }
    public static async Task<byte[]> ReadBoundedAsync(Stream input, long declaredLength, CancellationToken cancellationToken)
    {
        if (declaredLength is <= 0 or > MaximumFileBytes) throw new StudioFault(400, "SOURCE_SIZE_LIMIT");
        using var data = new MemoryStream((int)declaredLength); var buffer = new byte[8192]; int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (data.Length + read > MaximumFileBytes) throw new StudioFault(400, "SOURCE_SIZE_LIMIT");
            data.Write(buffer, 0, read);
        }
        if (data.Length != declaredLength) throw new StudioFault(400, "SOURCE_LENGTH_MISMATCH");
        return data.ToArray();
    }
    public static StudioSource Admit(byte[] data, string filename, string contentType, UniversalDocActor actor, DateTimeOffset now)
    {
        if (data.Length is <= 0 or > MaximumFileBytes) throw new StudioFault(400, "SOURCE_SIZE_LIMIT");
        // Filename is discarded, never normalized into a path, logged, stored or returned.
        if (filename.Length > 255 || filename.IndexOfAny(['/', '\\', ':', '\0']) >= 0 || filename.Any(char.IsControl))
            throw new StudioFault(400, "SOURCE_FILENAME_INVALID");
        var extension = filename.LastIndexOf('.') is var dot && dot >= 0 ? filename[dot..].ToLowerInvariant() : "";
        var media = extension switch { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp", _ => throw new StudioFault(415, "SOURCE_FORMAT_UNSUPPORTED") };
        if (!string.Equals(media, contentType, StringComparison.OrdinalIgnoreCase)) throw new StudioFault(415, "SOURCE_MIME_MISMATCH");
        int? width = null, height = null, pages = null;
        try
        {
            switch (media)
            {
                case "image/png": (width, height) = Png(data); break;
                case "image/jpeg": (width, height) = Jpeg(data); break;
                case "image/webp": (width, height) = WebP(data); break;
                case "application/pdf": pages = Pdf(data); break;
            }
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or OverflowException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        { throw Invalid(); }
        if (width is not null) Require(width > 0 && height > 0 && width <= 4096 && height <= 4096 && (long)width * height <= 262144);
        return new(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), Convert.ToHexString(SHA256.HashData(data)),
            data.ToImmutableArray(), media, now, actor.UserId, actor.TenantId, width, height, pages);
    }
    private static int Be(ReadOnlySpan<byte> d) => checked((int)BinaryPrimitives.ReadUInt32BigEndian(d));
    private static uint Crc(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (var b in data) { crc ^= b; for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0); }
        return ~crc;
    }
    private static (int, int) Png(byte[] data)
    {
        Require(data.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        int pos = 8, width = 0, height = 0, channels = 0, chunks = 0; bool ended = false, seenData = false;
        using var compressed = new MemoryStream();
        while (pos < data.Length)
        {
            Require(++chunks <= 128 && data.Length - pos >= 12); int length = Be(data.AsSpan(pos));
            Require(length <= data.Length - pos - 12);
            var kind = Encoding.ASCII.GetString(data, pos + 4, 4); var body = data.AsSpan(pos + 8, length);
            Require(Crc(data.AsSpan(pos + 4, length + 4)) == BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pos + 8 + length)));
            if (chunks == 1)
            {
                Require(kind == "IHDR" && length == 13); width = Be(body); height = Be(body[4..]);
                Require(width is > 0 and <= 4096 && height is > 0 and <= 4096 && (long)width * height <= 262144);
                Require(body[8] == 8 && body[9] is 2 or 6 && body[10] == 0 && body[11] == 0 && body[12] == 0);
                channels = body[9] == 2 ? 3 : 4;
            }
            else if (kind == "IDAT") { Require(length > 0); seenData = true; compressed.Write(body); }
            else if (kind == "IEND") { Require(seenData && length == 0 && pos + 12 == data.Length); ended = true; }
            else throw new StudioFault(415, "SOURCE_PNG_SUBSET_UNSUPPORTED"); // No animation, profiles, metadata or unknown chunks.
            pos += 12 + length;
        }
        Require(ended);
        int rowBytes = checked(width * channels), expanded = checked((rowBytes + 1) * height);
        Require(expanded <= MaximumFileBytes); compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        var row = new byte[rowBytes];
        for (int y = 0; y < height; y++)
        { int filter = zlib.ReadByte(); Require(filter is >= 0 and <= 4); zlib.ReadExactly(row); }
        Require(zlib.ReadByte() == -1); return (width, height);
    }
    private static (int, int) Jpeg(byte[] d)
    {
        Require(d.Length >= 4 && d[0] == 255 && d[1] == 216); int p = 2, w = 0, h = 0, components = 0, markers = 0;
        var quantization = new HashSet<int>(); var huffman = new HashSet<int>();
        var requiredQuantization = new HashSet<int>(); var componentIds = new HashSet<byte>();
        while (p < d.Length)
        {
            Require(++markers <= 128 && p + 4 <= d.Length && d[p++] == 255); int marker = d[p++];
            int length = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(p)); Require(length >= 2 && length <= d.Length - p);
            var body = d.AsSpan(p + 2, length - 2);
            if (marker == 0xC0)
            {
                Require(w == 0 && body.Length >= 6 && body[0] == 8); components = body[5];
                Require(components is 1 or 3 && body.Length == 6 + 3 * components);
                h = BinaryPrimitives.ReadUInt16BigEndian(body[1..]); w = BinaryPrimitives.ReadUInt16BigEndian(body[3..]);
                for (int i = 6; i < body.Length; i += 3)
                {
                    Require(componentIds.Add(body[i]) && body[i + 1] >> 4 is >= 1 and <= 4 && (body[i + 1] & 15) is >= 1 and <= 4 && body[i + 2] <= 3);
                    requiredQuantization.Add(body[i + 2]);
                }
            }
            else if (marker == 0xDB)
            {
                int q = 0; Require(body.Length >= 65);
                while (q < body.Length)
                {
                    byte info = body[q++]; Require(info <= 3 && body.Length - q >= 64); // Baseline 8-bit tables only.
                    quantization.Add(info);
                    for (int i = 0; i < 64; i++) Require(body[q++] != 0);
                }
            }
            else if (marker == 0xC4)
            {
                int t = 0; Require(body.Length >= 18);
                while (t < body.Length)
                {
                    Require(body.Length - t >= 17); int info = body[t++]; Require((info & 15) <= 3 && (info >> 4) <= 1);
                    huffman.Add(info);
                    int count = 0, space = 1;
                    for (int i = 0; i < 16; i++) { int n = body[t++]; count += n; space = space * 2 - n; Require(space >= 0); }
                    Require(count is > 0 and <= 256 && count <= body.Length - t); t += count;
                }
            }
            else if (marker == 0xDA)
            {
                Require(w > 0 && h > 0 && requiredQuantization.All(quantization.Contains) && body.Length == 4 + 2 * components && body[0] == components);
                var scanIds = new HashSet<byte>();
                for (int i = 1; i < 1 + 2 * components; i += 2)
                {
                    Require(componentIds.Contains(body[i]) && scanIds.Add(body[i]) && body[i + 1] >> 4 <= 3 && (body[i + 1] & 15) <= 3);
                    Require(huffman.Contains(body[i + 1] >> 4) && huffman.Contains(0x10 | (body[i + 1] & 15)));
                }
                Require(body[^3] == 0 && body[^2] == 63 && body[^1] == 0);
                p += length; int start = p;
                // One baseline scan, no progressive/multiscan/embedded metadata. Do not decode entropy or render.
                while (p < d.Length)
                {
                    if (d[p++] != 255) continue;
                    Require(p < d.Length); int next = d[p++];
                    if (next == 0 || next is >= 0xD0 and <= 0xD7) continue;
                    Require(next == 0xD9 && p == d.Length && p - start > 2); return (w, h);
                }
                throw Invalid();
            }
            else if (marker == 0xE0) Require(body.StartsWith("JFIF\0"u8) && body.Length >= 14 && body.Length == 14 + 3 * body[12] * body[13]);
            else throw new StudioFault(415, "SOURCE_JPEG_SUBSET_UNSUPPORTED");
            p += length;
        }
        throw Invalid();
    }
    private static (int, int) WebP(byte[] d)
    {
        Require(d.Length >= 30 && d.AsSpan(0, 4).SequenceEqual("RIFF"u8) && d.AsSpan(8, 4).SequenceEqual("WEBP"u8));
        Require(BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(4)) == d.Length - 8);
        if (!d.AsSpan(12, 4).SequenceEqual("VP8 "u8)) throw new StudioFault(415, "SOURCE_WEBP_SUBSET_UNSUPPORTED");
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(16));
        Require(size >= 11 && size <= d.Length - 20 && 20L + size + (size & 1) == d.Length);
        var frame = d.AsSpan(20); int tag = frame[0] | frame[1] << 8 | frame[2] << 16;
        Require((tag & 1) == 0 && ((tag >> 1) & 7) <= 3 && (tag & 16) != 0 && (tag >> 5) > 0 && (tag >> 5) <= size - 10);
        Require(frame.Slice(3, 3).SequenceEqual(new byte[] { 0x9D, 1, 0x2A }));
        int w = BinaryPrimitives.ReadUInt16LittleEndian(frame[6..]), h = BinaryPrimitives.ReadUInt16LittleEndian(frame[8..]);
        Require((w & 0xC000) == 0 && (h & 0xC000) == 0 && ((size & 1) == 0 || d[^1] == 0)); return (w, h);
    }
    private static int Pdf(byte[] d)
    {
        // Deliberately tiny grammar: a single blank page, three objects, classic xref.
        // All content streams/resources/actions/encryption/incremental/object-stream PDFs explicitly unsupported.
        if (d.Length > 16384 || d.Any(b => b is < 10 or > 126 || b is 11 or 12)) throw new StudioFault(415, "SOURCE_PDF_SUBSET_UNSUPPORTED");
        var text = Encoding.ASCII.GetString(d).Replace("\r\n", "\n", StringComparison.Ordinal);
        const string pattern = @"\A%PDF-1\.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids \[3 0 R\] /Count 1 >>\nendobj\n3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox \[0 0 612 792\] >>\nendobj\nxref\n0 4\n0000000000 65535 f \n(?<a>\d{10}) 00000 n \n(?<b>\d{10}) 00000 n \n(?<c>\d{10}) 00000 n \ntrailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n(?<x>\d{1,5})\n%%EOF\n\z";
        var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success) throw new StudioFault(415, "SOURCE_PDF_SUBSET_UNSUPPORTED");
        // Offsets refer to original bytes (including CRLF), not normalized text.
        var original = Encoding.ASCII.GetString(d);
        foreach (var (group, token) in new[] { ("a", "1 0 obj"), ("b", "2 0 obj"), ("c", "3 0 obj"), ("x", "xref\n") })
            Require(int.Parse(match.Groups[group].Value, System.Globalization.CultureInfo.InvariantCulture) ==
                original.IndexOf(token == "xref\n" && original.Contains("\r\n", StringComparison.Ordinal) ? "xref\r\n" : token, StringComparison.Ordinal));
        return 1; // Proven by the entire restricted page-tree grammar, never inferred from a token count.
    }
}

// Trusted server adapter input. No endpoint exposes this capability. Future adapters implement the
// existing IExtractionProvider and submit ExtractionBatch to ExtractionPipeline using this Context.
// Observations still undergo Gate 3B validation; bytes/hash/identity cannot come from that batch.
public sealed class StudioRecognitionInput
{
    private readonly ImmutableArray<StudioSource> sources;
    public ExtractionContext Context { get; }
    public StudioRecognitionInput(UniversalDocSession session)
    {
        sources = session.Sources;
        if (sources.Any(s => s.OwnerId != session.OwnerId || s.TenantId != session.TenantId ||
            Convert.ToHexString(SHA256.HashData(s.Bytes.AsSpan())) != s.Sha256)) throw new StudioFault(409, "SOURCE_IDENTITY_CHANGED");
        Context = new(session.Profile, sources.Select(s => new VerifiedExtractionSource(s.Id, s.Sha256, s.Bytes.Length, s.PageCount)).ToImmutableArray(),
            StudioDemoCatalog.ExtractionContext(session.Profile).Normalization);
    }
    public Stream OpenRead(string sourceId) => new MemoryStream(sources.SingleOrDefault(s => s.Id == sourceId)?.Bytes.ToArray()
        ?? throw new StudioFault(404, "SOURCE_NOT_FOUND"), false);
}
