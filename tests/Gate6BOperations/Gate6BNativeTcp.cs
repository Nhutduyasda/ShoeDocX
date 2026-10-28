using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using ShoeExportInvoice.Tests.UniversalDocEngine;

internal static class Gate6BNativeTcp
{
    public static async Task Run(string output)
    {
        var report = new Dictionary<string, object?> { ["status"]="BLOCKED", ["productionParity"]="UNVERIFIED", ["dockerExecuted"]=false };
        var raw=Environment.GetEnvironmentVariable("GATE6B_WINDOWS_TCP")!;
        var target=new SqlConnectionStringBuilder(raw);
        if(!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true" ||
            target.DataSource!="tcp:localhost,50143" || target.InitialCatalog!="master" || !target.IntegratedSecurity ||
            target.TrustServerCertificate || target.Encrypt==SqlConnectionEncryptOption.Optional) throw new InvalidOperationException("DISPOSABLE_LOOPBACK_SQL_REQUIRED");
        var parent=Environment.GetEnvironmentVariable("GATE6B_WINDOWS_PRIVATE")??throw new InvalidOperationException("PRIVATE_DIRECTORY_REQUIRED");
        if(!Directory.Exists(parent))throw new InvalidOperationException("PRIVATE_DIRECTORY_REQUIRED");
        var folder=Path.Combine(parent,"native-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var database="gate6b_"+Guid.NewGuid().ToString("N");
        string Password()=>"G6b!"+Guid.NewGuid().ToString("N")+"aA1";
        var migration=Password();var runtime=Password();var user=Password();var pfxPassword=Password();
        Process? process=null;
        var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!File.Exists(Path.Combine(root.FullName,"backend/ShoeExportInvoice.Api/Program.cs")))root=root.Parent;
        if(root==null)throw new InvalidOperationException("REPOSITORY_REQUIRED");
        var helper=Path.Combine(AppContext.BaseDirectory,"Gate6BOperations.dll");
        var api=Path.Combine(root.FullName,"backend/ShoeExportInvoice.Api/bin/Debug/net10.0/ShoeExportInvoice.Api.dll");
        var operatorFile=Path.Combine(folder,"operator.json");var configFile=Path.Combine(folder,"runtime.json");
        File.WriteAllText(operatorFile,JsonSerializer.Serialize(new{adminConnection=raw,database,migrationPassword=migration,runtimePassword=runtime,userPassword=user}));
        Process Start(string dll,params string[] arguments) {
            var start=new ProcessStartInfo("dotnet") {WorkingDirectory=folder,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(dll);
            foreach(var value in arguments)start.ArgumentList.Add(value);
            start.Environment["GATE6B_OPERATOR"]=operatorFile;start.Environment["SHOEDOCX_CONFIGURATION_FILE"]=configFile;start.Environment["ASPNETCORE_ENVIRONMENT"]="Production";start.Environment["ASPNETCORE_URLS"]="https://localhost:5443";
            var p=new Process{StartInfo=start};p.OutputDataReceived+=(_,_)=>{};p.ErrorDataReceived+=(_,_)=>{};p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();return p;
        }
        async Task Execute(string dll,params string[] arguments) {report["phase"]=arguments[0];using var p=Start(dll,arguments);using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(45));try{await p.WaitForExitAsync(deadline.Token);}catch{p.Kill(true);throw;}if(p.ExitCode!=0)throw new InvalidOperationException("NATIVE_MAINTENANCE_FAILED");}
        try {
            report["phase"]="sql-tls";
            await using(var c=new SqlConnection(raw)) {
                for(var i=0;;i++){try{await c.OpenAsync();break;}catch(SqlException)when(i<15){await Task.Delay(1000);}}
                await using var q=new SqlCommand("SELECT net_transport,encrypt_option FROM sys.dm_exec_connections WHERE session_id=@@SPID",c);
                await using var reader=await q.ExecuteReaderAsync();await reader.ReadAsync();
                report["sqlTransport"]=reader.GetString(0);report["sqlEncrypted"]=reader.GetString(1);
                if(reader.GetString(0)!="TCP"||reader.GetString(1)!="TRUE")throw new InvalidOperationException("SQL_TCP_TLS_NOT_PROVEN");
            }
            var wrong=new SqlConnectionStringBuilder(raw){HostNameInCertificate="wrong.invalid"};
            try {await using var c=new SqlConnection(wrong.ConnectionString);await c.OpenAsync();throw new InvalidOperationException("WRONG_SQL_HOSTNAME_ACCEPTED");}
            catch(SqlException){report["wrongSqlHostnameRejected"]=true;}
            using var rsa=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");request.CertificateExtensions.Add(san.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
            using var web=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
            using var protectionRsa=RSA.Create(2048);using var protection=new CertificateRequest("CN=Synthetic Protection",protectionRsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
            var webPath=Path.Combine(folder,"web.pfx");var protectionPath=Path.Combine(folder,"protection.pfx");File.WriteAllBytes(webPath,web.Export(X509ContentType.Pfx,pfxPassword));File.WriteAllBytes(protectionPath,protection.Export(X509ContentType.Pfx,pfxPassword));
            foreach(var name in new[]{"keys","customs","Templates"})Directory.CreateDirectory(Path.Combine(folder,name));
            File.WriteAllBytes(Path.Combine(folder,"Templates/synthetic.xlsx"),Gate5DFixtures.Workbook("A"));
            var runtimeConnection=new SqlConnectionStringBuilder(raw){InitialCatalog=database,IntegratedSecurity=false,UserID="gate6b_runtime",Password=runtime};
            var settings=new Dictionary<string,object?> {
                ["ConnectionStrings"]=new{DefaultConnection=runtimeConnection.ConnectionString},["Jwt"]=new{Key=Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),Issuer="gate6b",Audience="gate6b"},
                ["Xnk"]=new{AllowedOrigins=new[]{"https://localhost:5443"},CustomsStoragePath=Path.Combine(folder,"customs"),ShipmentTemplatePath=Path.Combine(folder,"Templates/synthetic.xlsx")},
                ["DataProtection"]=new{KeyPath=Path.Combine(folder,"keys"),CertificatePath=protectionPath,CertificatePassword=pfxPassword},
                ["UniversalDocDrafts"]=new{Enabled=true,ActiveKeyId="synthetic",Keys=new Dictionary<string,string>{{"synthetic",Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}}},
                ["UniversalDocGate4A"]=new{Users=new Dictionary<string,string>{{"admin-a","synthetic-a"},{"other-a","synthetic-a"},{"admin-b","synthetic-b"},{"clerk-a","synthetic-a"}}},
                ["Kestrel"]=new{Certificates=new{Default=new{Path=webPath,Password=pfxPassword}}},["AllowedHosts"]="localhost",["Logging"]=new{LogLevel=new{Default="Warning"}}
            };
            void Write()=>File.WriteAllText(configFile,JsonSerializer.Serialize(settings));
            await Execute(helper,"provision");
            var migrationConnection=new SqlConnectionStringBuilder(runtimeConnection.ConnectionString){UserID="gate6b_migration",Password=migration};
            settings["ConnectionStrings"]=new{DefaultConnection=migrationConnection.ConnectionString};settings["Database"]=new{MaintenanceAuthorized=true};Write();
            await Execute(api,"--migrate-all");await Execute(api,"--initialize-staging");await Execute(helper,"users");await Execute(helper,"permissions");
            settings["ConnectionStrings"]=new{DefaultConnection=runtimeConnection.ConnectionString};settings["Database"]=new{MaintenanceAuthorized=false};Write();
            report["separateMaintenanceAndRuntimePermissions"]=true;
            report["phase"]="production-startup";process=Start(api);
            var policy=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};policy.CustomTrustStore.Add(web);
            using var handler=new SocketsHttpHandler{SslOptions=new SslClientAuthenticationOptions{CertificateChainPolicy=policy}};using var client=new HttpClient(handler){BaseAddress=new Uri("https://localhost:5443"),Timeout=TimeSpan.FromSeconds(3)};
            var ready=false;for(var i=0;i<60&&!process.HasExited;i++){try{if((await client.GetAsync("/api/health")).StatusCode==HttpStatusCode.OK){ready=true;break;}}catch(HttpRequestException){}catch(TaskCanceledException){}await Task.Delay(500);}
            if(!ready)throw new InvalidOperationException("NATIVE_HTTPS_READINESS_FAILED");
            report["actualProductionStarted"]=true;report["trustedHttpsHealth"]=true;
            if((await client.GetAsync("/api/auth/me")).StatusCode!=HttpStatusCode.Unauthorized)throw new InvalidOperationException("NATIVE_AUTHORIZATION_FAILED");
            report["phase"]="login-csrf";using var login=await client.PostAsJsonAsync("/api/auth/login",new{username="admin-a",password=user});login.EnsureSuccessStatusCode();
            using var bootstrap=await client.GetAsync("/api/universal-doc/bootstrap");bootstrap.EnsureSuccessStatusCode();
            using var body=JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync());var csrf=body.RootElement.GetProperty("csrfToken").GetString();
            using var create=new HttpRequestMessage(HttpMethod.Post,"/api/universal-doc/sessions"){Content=JsonContent.Create(new{workflow="example-template"})};create.Headers.Add("X-Studio-CSRF",csrf);
            using var created=await client.SendAsync(create);created.EnsureSuccessStatusCode();
            using var missing=await client.PostAsJsonAsync("/api/universal-doc/sessions",new{workflow="example-template"});if(missing.StatusCode!=HttpStatusCode.BadRequest)throw new InvalidOperationException("NATIVE_CSRF_FAILED");
            report["actualProductionStarted"]=true;report["trustedHttpsAuthCsrf"]=true;report["status"]="PARTIAL";
            report["phase"]="subset-complete";
            report["remaining"]="Trusted native reverse-proxy, browser, tenant/owner and actual draft recovery parity not yet executed; Windows acceptance is not waived";
        }catch(Exception e){report["errorType"]=e.GetType().Name;if(System.Text.RegularExpressions.Regex.IsMatch(e.Message,"^[A-Z_]+$"))report["errorCode"]=e.Message;}
        finally {
            if(process is {HasExited:false}){process.Kill(true);await process.WaitForExitAsync();}
            try {await using var c=new SqlConnection(raw);await c.OpenAsync();await using var q=new SqlCommand($"IF DB_ID('{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END; IF SUSER_ID('gate6b_runtime') IS NOT NULL DROP LOGIN [gate6b_runtime]; IF SUSER_ID('gate6b_migration') IS NOT NULL DROP LOGIN [gate6b_migration];",c);await q.ExecuteNonQueryAsync();}
            catch(Exception cleanup){report["cleanupErrorType"]=cleanup.GetType().Name;}
            Directory.Delete(folder,true);Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.WriteAllText(output,JsonSerializer.Serialize(report));
        }
    }
}
