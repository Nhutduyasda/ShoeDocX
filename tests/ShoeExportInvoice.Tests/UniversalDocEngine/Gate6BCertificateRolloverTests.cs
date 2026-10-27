using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Tests.UniversalDocEngine;

public sealed class Gate6BCertificateRolloverTests
{
    [Fact]
    public void RolloverDecryptsOldPayloadAndEncryptsNewKeyWithActiveCertificate()
    {
        var root = Path.Combine(Path.GetTempPath(), "gate6b-rollover-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            using var rsaA = RSA.Create(2048);
            using var rsaB = RSA.Create(2048);
            using var a = new CertificateRequest("CN=Synthetic A", rsaA, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3));
            using var b = new CertificateRequest("CN=Synthetic B", rsaB, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3));
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var pathA = Path.Combine(root, "a.pfx");
            var pathB = Path.Combine(root, "b.pfx");
            File.WriteAllBytes(pathA, a.Export(X509ContentType.Pfx, password));
            File.WriteAllBytes(pathB, b.Export(X509ContentType.Pfx, password));
            if (!OperatingSystem.IsWindows()) {
                File.SetUnixFileMode(pathA, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.SetUnixFileMode(pathB, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            var ring = Directory.CreateDirectory(Path.Combine(root, "ring"));
            ServiceProvider Provider(string active, bool retainA)
            {
                var values = new Dictionary<string, string?> {
                    ["DataProtection:CertificatePath"] = active,
                    ["DataProtection:CertificatePassword"] = password
                };
                if (retainA) {
                    values["DataProtection:DecryptionCertificates:0:Path"] = pathA;
                    values["DataProtection:DecryptionCertificates:0:Password"] = password;
                }
                var services = new ServiceCollection();
                services.AddDataProtection().SetApplicationName("ShoeDocX")
                    .PersistKeysToFileSystem(ring).ConfigureCertificates(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
                return services.BuildServiceProvider();
            }
            string oldPayload;
            using (var original = Provider(pathA, false)) {
                original.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddDays(2));
                oldPayload = original.GetRequiredService<IDataProtectionProvider>().CreateProtector("synthetic-purpose").Protect("synthetic-value");
            }
            var oldFiles = ring.GetFiles("key-*.xml").ToDictionary(f => f.Name, f => File.ReadAllBytes(f.FullName));
            string newPayload;
            using (var rotated = Provider(pathB, true)) {
                rotated.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(2));
                var protector = rotated.GetRequiredService<IDataProtectionProvider>().CreateProtector("synthetic-purpose");
                Assert.Equal("synthetic-value", protector.Unprotect(oldPayload));
                newPayload = protector.Protect("new-synthetic-value");
            }
            foreach (var file in oldFiles) Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(ring.FullName, file.Key)));
            var added = Assert.Single(ring.GetFiles("key-*.xml").Where(f => !oldFiles.ContainsKey(f.Name)));
            var xml = XDocument.Load(added.FullName);
            Assert.Contains(xml.Descendants(), e => e.Name.LocalName == "encryptedSecret");
            var encoded = Assert.Single(xml.Descendants().Where(e => e.Name.LocalName == "X509Certificate")).Value;
            Assert.Equal(b.RawData, Convert.FromBase64String(encoded));
            using (var restarted = Provider(pathB, true)) {
                var protector = restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("synthetic-purpose");
                Assert.Equal("synthetic-value", protector.Unprotect(oldPayload));
                Assert.Equal("new-synthetic-value", protector.Unprotect(newPayload));
            }
            using (var retiredTooSoon = Provider(pathB, false)) {
                var protector = retiredTooSoon.GetRequiredService<IDataProtectionProvider>().CreateProtector("synthetic-purpose");
                Assert.Throws<CryptographicException>(() => protector.Unprotect(oldPayload));
                Assert.Equal("new-synthetic-value", protector.Unprotect(newPayload));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void MissingPrivateCertificateFailsWithConstantError()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["DataProtection:CertificatePath"] = Path.GetFullPath("missing-synthetic.pfx")
        }).Build();
        Assert.Equal("DATA_PROTECTION_CERTIFICATE_UNAVAILABLE", Assert.Throws<InvalidOperationException>(() =>
            services.AddDataProtection().ConfigureCertificates(config)).Message);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(1, "relative.pfx")]
    [InlineData(5, "/synthetic.pfx")]
    public void InvalidOrUnboundedHistoricalCertificateListIsRejected(int count, string? active)
    {
        var values = new Dictionary<string, string?> { ["DataProtection:CertificatePath"] = active };
        for (var i = 0; i < count; i++) values[$"DataProtection:DecryptionCertificates:{i}:Path"] = "relative-old.pfx";
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.Equal("DATA_PROTECTION_CERTIFICATE_CONFIGURATION_INVALID", Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddDataProtection().ConfigureCertificates(config)).Message);
    }
}
