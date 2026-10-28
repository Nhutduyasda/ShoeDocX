using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

internal static class Gate6BNativeCertificates
{
    public static void Create(string folder)
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true" ||
            !Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Environment.GetEnvironmentVariable("RUNNER_TEMP")!)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(folder).StartsWith("gate6b-tcp-",StringComparison.Ordinal)) throw new InvalidOperationException("DISPOSABLE_CERTIFICATE_DIRECTORY_REQUIRED");
        using var caRsa=RSA.Create(2048);
        var caRequest=new CertificateRequest("CN=Gate6B Synthetic SQL Root",caRsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign|X509KeyUsageFlags.CrlSign,true));
        using var ca=caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddDays(2));
        var parameters=new CspParameters(12,"Microsoft RSA SChannel Cryptographic Provider","gate6b-"+Guid.NewGuid().ToString("N")) {
            Flags=CspProviderFlags.UseMachineKeyStore,KeyNumber=(int)KeyNumber.Exchange };
        using var sqlRsa=new RSACryptoServiceProvider(2048,parameters){PersistKeyInCsp=true};
        var request=new CertificateRequest("CN="+System.Net.Dns.GetHostName(),sqlRsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var names=new SubjectAlternativeNameBuilder();names.AddDnsName(System.Net.Dns.GetHostName());names.AddDnsName("localhost");request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection{new("1.3.6.1.5.5.7.3.1")},true));
        using var publicLeaf=request.Create(ca,DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1),RandomNumberGenerator.GetBytes(16));
        using var leaf=publicLeaf.CopyWithPrivateKey(sqlRsa);
        using var rootStore=new X509Store(StoreName.Root,StoreLocation.LocalMachine);rootStore.Open(OpenFlags.ReadWrite);
        using var personal=new X509Store(StoreName.My,StoreLocation.LocalMachine);personal.Open(OpenFlags.ReadWrite);
        try {
            using var publicRoot=X509CertificateLoader.LoadCertificate(ca.RawData);rootStore.Add(publicRoot);personal.Add(leaf);
            using var stored=personal.Certificates.Find(X509FindType.FindByThumbprint,leaf.Thumbprint,false)[0];
            using var key=stored.GetRSAPrivateKey();
            if(key is not RSACryptoServiceProvider csp || !csp.CspKeyContainerInfo.MachineKeyStore ||
                csp.CspKeyContainerInfo.KeyNumber!=KeyNumber.Exchange)throw new InvalidOperationException("MACHINE_EXCHANGE_KEY_NOT_PROVEN");
            File.WriteAllBytes(Path.Combine(folder,"ca.cer"),ca.RawData);
            File.WriteAllText(Path.Combine(folder,"sql-key.json"),JsonSerializer.Serialize(new {
                leafThumbprint=leaf.Thumbprint,rootThumbprint=ca.Thumbprint,hasPrivateKey=stored.HasPrivateKey,
                machinePrivateKey=csp.CspKeyContainerInfo.MachineKeyStore,privateKeyProvider=key.GetType().Name,
                keyFileName=csp.CspKeyContainerInfo.UniqueKeyContainerName }));
        } catch {personal.Remove(leaf);rootStore.Remove(ca);sqlRsa.PersistKeyInCsp=false;throw;}
    }
}
