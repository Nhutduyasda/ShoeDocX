using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Infrastructure.UniversalDocDrafts;

internal static class Gate6BNative
{
    public static async Task Run(string output) {
        var report=new Dictionary<string,object?> { ["status"]="BLOCKED",["production_tls_parity"]="UNVERIFIED",["docker_executed"]=false };
        var temporary=Path.Combine(Path.GetTempPath(),"gate6b-native-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporary);
        var raw=Environment.GetEnvironmentVariable("GATE5C_SQL")??throw new InvalidOperationException("SYNTHETIC_SQL_REQUIRED");
        var setup=new SqlConnectionStringBuilder(raw) { InitialCatalog="gate6b_"+Guid.NewGuid().ToString("N") };
        Process? process=null;
        await using var legacy=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(setup.ConnectionString).Options);
        try {
            await legacy.Database.MigrateAsync();
            await using(var drafts=new UniversalDocDraftDb(new DbContextOptionsBuilder<UniversalDocDraftDb>().UseSqlServer(setup.ConnectionString,s=>s.MigrationsHistoryTable("__DraftMigrations","ude")).Options)) await drafts.Database.MigrateAsync();
            using var rsa=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");request.CertificateExtensions.Add(san.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment|X509KeyUsageFlags.KeyCertSign,true));
            using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
            var pfx=Path.Combine(temporary,"protection.pfx");var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));File.WriteAllBytes(pfx,cert.Export(X509ContentType.Pfx,password));
            Directory.CreateDirectory(Path.Combine(temporary,"keys"));Directory.CreateDirectory(Path.Combine(temporary,"customs"));Directory.CreateDirectory(Path.Combine(temporary,"Templates"));File.WriteAllText(Path.Combine(temporary,"Templates/synthetic.xlsx"),"synthetic startup sentinel only; not an XLSX acceptance fixture");
            var listener=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
            var runtime=new SqlConnectionStringBuilder(setup.ConnectionString) { Encrypt=SqlConnectionEncryptOption.Mandatory,TrustServerCertificate=false };
            var config=new Dictionary<string,object?> {
                ["ConnectionStrings"]=new {DefaultConnection=runtime.ConnectionString},["Jwt"]=new {Key=Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))},
                ["Xnk"]=new {AllowedOrigins=new[]{"https://localhost:"+port},CustomsStoragePath=Path.Combine(temporary,"customs"),ShipmentTemplatePath=Path.Combine(temporary,"Templates/synthetic.xlsx")},
                ["DataProtection"]=new {KeyPath=Path.Combine(temporary,"keys"),CertificatePath=pfx,CertificatePassword=password},
                ["UniversalDocDrafts"]=new {Enabled=true,ActiveKeyId="native-test",Keys=new Dictionary<string,string>{{"native-test",Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}}},
                ["Kestrel"]=new {Certificates=new {Default=new {Path=pfx,Password=password}}},
                ["Logging"]=new {LogLevel=new {Default="Warning"}},["AllowedHosts"]="localhost"
            };
            var path=Path.Combine(temporary,"runtime.json");File.WriteAllText(path,JsonSerializer.Serialize(config));
            var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!File.Exists(Path.Combine(root.FullName,"backend/ShoeExportInvoice.Api/Program.cs")))root=root.Parent;
            if(root==null)throw new InvalidOperationException("REPOSITORY_REQUIRED");
            var start=new ProcessStartInfo("dotnet") {WorkingDirectory=temporary,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add(Path.Combine(root.FullName,"backend/ShoeExportInvoice.Api/bin/Debug/net10.0/ShoeExportInvoice.Api.dll"));
            start.Environment["ASPNETCORE_ENVIRONMENT"]="Production";start.Environment["ASPNETCORE_URLS"]="https://localhost:"+port;start.Environment["SHOEDOCX_CONFIGURATION_FILE"]=path;
            process=new Process {StartInfo=start};process.Start();process.OutputDataReceived+=(_,_)=>{};process.ErrorDataReceived+=(_,_)=>{};process.BeginOutputReadLine();process.BeginErrorReadLine();
            using var handler=new SocketsHttpHandler();var chain=new X509ChainPolicy {TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};chain.CustomTrustStore.Add(cert);handler.SslOptions=new SslClientAuthenticationOptions {CertificateChainPolicy=chain};
            using var client=new HttpClient(handler) {Timeout=TimeSpan.FromSeconds(5)};
            var ready=false;
            for(var i=0;i<90;i++) {
                if(process.HasExited){report["production_process_exit"]=process.ExitCode;break;}
                try {if((await client.GetAsync($"https://localhost:{port}/api/health")).StatusCode==HttpStatusCode.OK){ready=true;break;}}catch(HttpRequestException){}catch(TaskCanceledException){}
                await Task.Delay(500);
            }
            report["actual_native_production_started"]=ready;
            report["blocker"]="LocalDB is not a verified certificate-managed TCP SQL endpoint or separate SQL-login runtime; native proxy/least-privilege parity not established";
            if(ready){report["trusted_https_health"]=true;report["unauthenticated_authorization"]=(await client.GetAsync($"https://localhost:{port}/api/auth/me")).StatusCode==HttpStatusCode.Unauthorized;}
        } catch(Exception e) {report["exception_type"]=e.GetType().Name;}
        finally {
            if(process is {HasExited:false}){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}
            await legacy.Database.EnsureDeletedAsync();
            Directory.Delete(temporary,true);Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true}));
        }
    }
}
