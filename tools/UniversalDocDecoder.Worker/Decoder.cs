using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;
using UniversalDocDecoder.Protocol;

namespace UniversalDocDecoder.Worker;

internal static class Decoder
{
    public static async Task Decode(DecodeRequest request, byte[] data, Stream output)
    {
        if (request.MediaType == "application/pdf") { await Pdf(request, data, output); return; }
        using var source = SKData.CreateCopy(data);
        using var codec = SKCodec.Create(source) ?? throw new ProtocolFault("IMAGE_INVALID");
        var expected = request.MediaType switch { "image/jpeg" => SKEncodedImageFormat.Jpeg, "image/png" => SKEncodedImageFormat.Png, _ => SKEncodedImageFormat.Webp };
        Limits.Require(codec.EncodedFormat == expected, "FORMAT_MISMATCH");
        Limits.Require(codec.FrameCount <= 1, "ANIMATION_UNSUPPORTED");
        Wire.Dimensions(codec.Info.Width, codec.Info.Height);
        await Wire.Write(output, new Message(1, "page-start", Limits.Build, request.Sha256, Page: 1, Count: 1));
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        Limits.Require(codec.GetPixels(info, bitmap.GetPixels()) == SKCodecResult.Success, "IMAGE_INCOMPLETE");
        // EXIF transforms are explicit; SKCodec.GetPixels does not apply encoded origin itself.
        bool swaps = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int width = swaps ? info.Height : info.Width, height = swaps ? info.Width : info.Height;
        var sourceBytes = new byte[checked(info.Width * info.Height * 4)]; Marshal.Copy(bitmap.GetPixels(), sourceBytes, 0, sourceBytes.Length);
        var pixels = new byte[sourceBytes.Length];
        for (int y = 0; y < info.Height; y++) for (int x = 0; x < info.Width; x++)
        {
            (int dx, int dy) = codec.EncodedOrigin switch {
                SKEncodedOrigin.TopRight => (info.Width - 1 - x, y), SKEncodedOrigin.BottomRight => (info.Width - 1 - x, info.Height - 1 - y),
                SKEncodedOrigin.BottomLeft => (x, info.Height - 1 - y), SKEncodedOrigin.LeftTop => (y, x),
                SKEncodedOrigin.RightTop => (info.Height - 1 - y, x), SKEncodedOrigin.RightBottom => (info.Height - 1 - y, info.Width - 1 - x),
                SKEncodedOrigin.LeftBottom => (y, info.Width - 1 - x), _ => (x, y) };
            sourceBytes.AsSpan((y * info.Width + x) * 4, 4).CopyTo(pixels.AsSpan((dy * width + dx) * 4, 4));
        }
        await Page(output, request.Sha256, 1, 1, width, height, pixels, "");
        await Wire.Write(output, new Message(1, "complete", Limits.Build, request.Sha256, Count: 1));
    }

    private static async Task Page(Stream output, string hash, int page, int count, int width, int height, byte[] pixels, string text)
    {
        Wire.Dimensions(width, height); var utf8 = Encoding.UTF8.GetBytes(text);
        Limits.Require(utf8.Length <= Limits.TextBytes, "TEXT_LIMIT");
        await Wire.Write(output, new Message(1, "page", Limits.Build, hash, page, count, width, height, pixels.Length, utf8.Length));
        await output.WriteAsync(pixels); await output.WriteAsync(utf8); await output.FlushAsync();
    }

    private static async Task Pdf(DecodeRequest request, byte[] data, Stream output)
    {
        Limits.Require(data.AsSpan().StartsWith("%PDF-"u8), "FORMAT_MISMATCH");
        // Conservative experimental exclusions; these byte searches are NOT a complete PDF security parser.
        string syntax = Encoding.Latin1.GetString(data);
        foreach (var token in new[] { "/Encrypt", "/JavaScript", "/JS", "/OpenAction", "/AA", "/Launch", "/URI", "/GoToR", "/EmbeddedFile", "/XFA", "/Filespec" })
            Limits.Require(!syntax.Contains(token, StringComparison.Ordinal), "PDF_FEATURE_UNSUPPORTED");
        // PDFium does not expose an expanded-stream accounting hook. Do not pretend OS RSS is that quota.
        Limits.Require(!syntax.Contains("/FlateDecode", StringComparison.Ordinal) && !syntax.Contains("/ObjStm", StringComparison.Ordinal), "PDF_EXPANSION_ACCOUNTING_UNAVAILABLE");
        Native.FPDF_InitLibrary(); var pin = GCHandle.Alloc(data, GCHandleType.Pinned); nint document = 0;
        try
        {
            document = Native.FPDF_LoadMemDocument64(pin.AddrOfPinnedObject(), (nuint)data.Length, null);
            Limits.Require(document != 0, "PDF_INVALID_OR_ENCRYPTED");
            int count = Native.FPDF_GetPageCount(document); Limits.Require(count is > 0 and <= Limits.Pages, "PAGE_LIMIT");
            long pixelsUsed = 0, textUsed = 0;
            for (int i = 0; i < count; i++)
            {
                await Wire.Write(output, new Message(1, "page-start", Limits.Build, request.Sha256, i + 1, count));
                var page = Native.FPDF_LoadPage(document, i); Limits.Require(page != 0, "PDF_PAGE_INVALID");
                nint bitmap = 0, textPage = 0;
                try
                {
                    double w = Native.FPDF_GetPageWidth(page), h = Native.FPDF_GetPageHeight(page);
                    Limits.Require(double.IsFinite(w) && double.IsFinite(h) && w > 0 && h > 0 && w <= 1966 && h <= 1966, "PIXEL_LIMIT");
                    int width = checked((int)Math.Ceiling(w * 150 / 72)), height = checked((int)Math.Ceiling(h * 150 / 72)); Wire.Dimensions(width, height);
                    pixelsUsed += (long)width * height; Limits.Require(pixelsUsed <= Limits.JobPixels, "JOB_PIXEL_LIMIT");
                    bitmap = Native.FPDFBitmap_Create(width, height, 1); Limits.Require(bitmap != 0, "PDF_ALLOCATION_FAILED");
                    Native.FPDFBitmap_FillRect(bitmap, 0, 0, width, height, 0xffffffff);
                    // No form-fill environment or action API is ever invoked. Native package is the non-V8 variant.
                    Native.FPDF_RenderPageBitmap(bitmap, page, 0, 0, width, height, 0, 0);
                    int stride = Native.FPDFBitmap_GetStride(bitmap); Limits.Require(stride == checked(width * 4), "PIXEL_LAYOUT_INVALID");
                    var pixels = new byte[checked(stride * height)]; Marshal.Copy(Native.FPDFBitmap_GetBuffer(bitmap), pixels, 0, pixels.Length);
                    textPage = Native.FPDFText_LoadPage(page); string text = "";
                    if (textPage != 0)
                    {
                        int chars = Native.FPDFText_CountChars(textPage); Limits.Require(chars is >= 0 and <= Limits.TextBytes / 4, "TEXT_LIMIT");
                        var buffer = new ushort[checked(chars + 1)]; int copied = Native.FPDFText_GetText(textPage, 0, chars, buffer);
                        Limits.Require(copied >= 0 && copied <= buffer.Length, "TEXT_INVALID");
                        text = new string(buffer.Take(Math.Max(0, copied - 1)).Select(c => (char)c).ToArray());
                        textUsed += Encoding.UTF8.GetByteCount(text); Limits.Require(textUsed <= Limits.TextBytes, "TEXT_LIMIT");
                    }
                    await Page(output, request.Sha256, i + 1, count, width, height, pixels, text);
                }
                finally { if (textPage != 0) Native.FPDFText_ClosePage(textPage); if (bitmap != 0) Native.FPDFBitmap_Destroy(bitmap); Native.FPDF_ClosePage(page); }
            }
            await Wire.Write(output, new Message(1, "complete", Limits.Build, request.Sha256, Count: count));
        }
        finally { if (document != 0) Native.FPDF_CloseDocument(document); pin.Free(); Native.FPDF_DestroyLibrary(); }
    }
}

internal static class Native
{
    private const string Dll = "pdfium";
    [DllImport(Dll)] internal static extern void FPDF_InitLibrary();
    [DllImport(Dll)] internal static extern void FPDF_DestroyLibrary();
    [DllImport(Dll)] internal static extern nint FPDF_LoadMemDocument64(nint bytes, nuint length, [MarshalAs(UnmanagedType.LPStr)] string? password);
    [DllImport(Dll)] internal static extern int FPDF_GetPageCount(nint document);
    [DllImport(Dll)] internal static extern nint FPDF_LoadPage(nint document, int index);
    [DllImport(Dll)] internal static extern double FPDF_GetPageWidth(nint page);
    [DllImport(Dll)] internal static extern double FPDF_GetPageHeight(nint page);
    [DllImport(Dll)] internal static extern nint FPDFBitmap_Create(int width, int height, int alpha);
    [DllImport(Dll)] internal static extern void FPDFBitmap_FillRect(nint bitmap, int left, int top, int width, int height, uint color);
    [DllImport(Dll)] internal static extern void FPDF_RenderPageBitmap(nint bitmap, nint page, int x, int y, int width, int height, int rotate, int flags);
    [DllImport(Dll)] internal static extern nint FPDFBitmap_GetBuffer(nint bitmap);
    [DllImport(Dll)] internal static extern int FPDFBitmap_GetStride(nint bitmap);
    [DllImport(Dll)] internal static extern void FPDFBitmap_Destroy(nint bitmap);
    [DllImport(Dll)] internal static extern void FPDF_ClosePage(nint page);
    [DllImport(Dll)] internal static extern void FPDF_CloseDocument(nint document);
    [DllImport(Dll)] internal static extern nint FPDFText_LoadPage(nint page);
    [DllImport(Dll)] internal static extern int FPDFText_CountChars(nint text);
    [DllImport(Dll)] internal static extern int FPDFText_GetText(nint text, int start, int count, [Out] ushort[] buffer);
    [DllImport(Dll)] internal static extern void FPDFText_ClosePage(nint text);
}
