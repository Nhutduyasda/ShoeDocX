using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace ShoeExportInvoice.Api.Services;

public static class ProtectionCertificateConfiguration
{
    private sealed class CertificateLifetime(List<X509Certificate2> certificates) : IDisposable
    {
        public void Dispose() { foreach (var certificate in certificates) certificate.Dispose(); }
    }
    // Historical certificates decrypt existing key XML only. New key XML always
    // uses CertificatePath. Never rewrite or discard the existing key ring.
    public static IDataProtectionBuilder ConfigureCertificates(this IDataProtectionBuilder builder, IConfiguration configuration)
    {
        var activePath = configuration["DataProtection:CertificatePath"];
        var historical = configuration.GetSection("DataProtection:DecryptionCertificates").GetChildren().ToArray();
        if (historical.Length > 4 || (historical.Length > 0 && string.IsNullOrWhiteSpace(activePath)))
            throw new InvalidOperationException("DATA_PROTECTION_CERTIFICATE_CONFIGURATION_INVALID");
        if (string.IsNullOrWhiteSpace(activePath)) return builder;
        if (new[] { activePath }.Concat(historical.Select(item => item["Path"]))
            .Any(path => string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)))
            throw new InvalidOperationException("DATA_PROTECTION_CERTIFICATE_CONFIGURATION_INVALID");
        var certificates = new List<X509Certificate2>();
        try
        {
            X509Certificate2 Load(string? path, string? password)
            {
                var certificate = X509CertificateLoader.LoadPkcs12FromFile(path!, password, X509KeyStorageFlags.EphemeralKeySet);
                certificates.Add(certificate);
                if (!certificate.HasPrivateKey) throw new CryptographicException();
                return certificate;
            }
            var active = Load(activePath, configuration["DataProtection:CertificatePassword"]);
            foreach (var item in historical) Load(item["Path"], item["Password"]);
            builder.ProtectKeysWithCertificate(active).UnprotectKeysWithAnyCertificate(certificates.ToArray());
            builder.Services.AddSingleton<CertificateLifetime>(_ => new CertificateLifetime(certificates));
            builder.Services.AddOptions<KeyManagementOptions>().Configure<CertificateLifetime>((options, lifetime) => { });
            return builder;
        }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException)
        {
            foreach (var certificate in certificates) certificate.Dispose();
            throw new InvalidOperationException("DATA_PROTECTION_CERTIFICATE_UNAVAILABLE");
        }
    }
}
