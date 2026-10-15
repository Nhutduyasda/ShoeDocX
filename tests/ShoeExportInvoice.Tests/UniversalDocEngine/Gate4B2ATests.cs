using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

namespace ShoeExportInvoice.Tests.UniversalDocEngine;

public class Gate4B2ATests
{
    public static IEnumerable<object[]> Fixtures()
    {
        using var input = typeof(Gate4B2ATests).Assembly.GetManifestResourceStream("ShoeExportInvoice.Tests.UniversalDocEngine.Gate4B2AFixtures.json")!;
        using var json = JsonDocument.Parse(input);
        foreach (var item in json.RootElement.EnumerateArray())
            yield return new object[] { item.GetProperty("name").GetString()!, item.GetProperty("extension").GetString()!,
                item.GetProperty("base64").GetString()!, item.GetProperty("code").GetString()! };
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void SyntheticCompatibilityIsMeasuredAgainstActualAdmission(string name, string extension, string encoded, string code)
    {
        Assert.NotEmpty(name);
        var data = Convert.FromBase64String(encoded);
        var actor = new UniversalDocActor("synthetic", "synthetic", "Admin", DateTimeOffset.UtcNow.AddHours(1));
        string mime = extension == "pdf" ? "application/pdf" : extension == "jpg" ? "image/jpeg" : "image/" + extension;
        if (code == "ACCEPT")
        {
            var source = SourceIntake.Admit(data, "synthetic." + extension, mime, actor, DateTimeOffset.UtcNow);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(data)), source.Sha256);
            Assert.Equal(data, source.Bytes.ToArray());
            Assert.Equal("AdmittedNotRecognized", SourceIntake.View(source).Status);
            Assert.Null(source.PageCount);
        }
        else
        {
            var error = Assert.Throws<StudioFault>(() => SourceIntake.Admit(data, "synthetic." + extension, mime, actor, DateTimeOffset.UtcNow));
            Assert.Equal(code, error.Code);
        }
    }

    [Fact]
    public void OverOneMiBIsRejectedBeforeDecode()
    {
        var bytes = new byte[SourceIntake.MaximumFileBytes + 1];
        Gate4B1Tests.Image("jpg").CopyTo(bytes, 0);
        var actor = new UniversalDocActor("synthetic", "synthetic", "Admin", DateTimeOffset.UtcNow.AddHours(1));
        Assert.Equal("SOURCE_SIZE_LIMIT", Assert.Throws<StudioFault>(() =>
            SourceIntake.Admit(bytes, "synthetic.jpg", "image/jpeg", actor, DateTimeOffset.UtcNow)).Code);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("regex")]
    public void DotNetEightUsesSharedFrameworkImplementation(string kind)
    {
        var assembly = kind == "http" ? typeof(HttpClient).Assembly : typeof(Regex).Assembly;
        Assert.Equal(8, assembly.GetName().Version!.Major);
        var location = assembly.Location.Replace('\\', '/');
        Assert.Contains("/shared/Microsoft.NETCore.App/8.", location);
        Assert.DoesNotContain("/.nuget/", location);
    }
}
