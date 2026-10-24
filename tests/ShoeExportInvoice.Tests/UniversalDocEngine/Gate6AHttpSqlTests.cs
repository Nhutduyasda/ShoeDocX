using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;

public sealed class Gate6AHttpSqlTests
{
    [Fact]
    public async Task RealApplicationJwtIdentityAndFootwearCrudUseEphemeralSql()
    {
        var raw=Environment.GetEnvironmentVariable("GATE5C_SQL") ?? throw new InvalidOperationException("Ephemeral SQL required");
        var builder=new SqlConnectionStringBuilder(raw) { InitialCatalog="gate5c_legacy_"+Guid.NewGuid().ToString("N") };
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(builder.ConnectionString).Options);
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null && !File.Exists(Path.Combine(root.FullName,"backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj")))root=root.Parent;
        Assert.NotNull(root);
        var api=Path.Combine(root!.FullName,"backend/ShoeExportInvoice.Api");
        var listener=new TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();var port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        var start=new ProcessStartInfo("dotnet") { WorkingDirectory=api,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true };
        start.ArgumentList.Add(Path.Combine(api,"bin/Debug/net10.0/ShoeExportInvoice.Api.dll"));
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";start.Environment["ASPNETCORE_URLS"]=$"http://127.0.0.1:{port}";
        start.Environment["ConnectionStrings__DefaultConnection"]=builder.ConnectionString;
        start.Environment["Jwt__Key"]="Gate6A_Synthetic_Jwt_Key_Only_2026_1234567890";
        start.Environment["Jwt__Issuer"]="gate6a-issuer";start.Environment["Jwt__Audience"]="gate6a-audience";
        start.Environment["DevelopmentUsers__Enabled"]="true";
        start.Environment["UniversalDocDrafts__Enabled"]="false";
        using var process=new Process { StartInfo=start };
        try
        {
            Assert.True(process.Start());
            // Drain private process output; do not attach raw logs, cookies or JWTs to test evidence.
            process.OutputDataReceived+=(_,_)=>{};process.ErrorDataReceived+=(_,_)=>{};process.BeginOutputReadLine();process.BeginErrorReadLine();
            using var client=new HttpClient(new HttpClientHandler { UseCookies=false }) { BaseAddress=new Uri($"http://127.0.0.1:{port}"),Timeout=TimeSpan.FromSeconds(10) };
            var ready=false;
            for(var i=0;i<120;i++)
            {
                Assert.False(process.HasExited,"Real API process exited during startup");
                try { if((await client.GetAsync("/api/auth/me")).StatusCode==HttpStatusCode.Unauthorized) {ready=true;break;} } catch(HttpRequestException){}
                await Task.Delay(500);
            }
            Assert.True(ready,"Real API readiness timed out");
            var login=await client.PostAsJsonAsync("/api/auth/login",new {username="admin",password="@Admin1234"});Assert.Equal(HttpStatusCode.OK,login.StatusCode);
            var cookie=login.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("shoedocx_access="));Assert.Contains("httponly",cookie.ToLowerInvariant());Assert.Contains("samesite=strict",cookie.ToLowerInvariant());
            var token=(await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            client.DefaultRequestHeaders.Authorization=new("Bearer",token);
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/auth/me")).StatusCode);
            foreach(var kind in new[]{"expired","audience","signature"})
            {
                var key=kind=="signature"?"Gate6A_Wrong_Signature_Key_Only_2026_1234567890":"Gate6A_Synthetic_Jwt_Key_Only_2026_1234567890";
                var jwt=new JwtSecurityToken("gate6a-issuer",kind=="audience"?"wrong-audience":"gate6a-audience",null,
                    DateTime.UtcNow.AddHours(-2),kind=="expired"?DateTime.UtcNow.AddHours(-1):DateTime.UtcNow.AddHours(1),
                    new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),SecurityAlgorithms.HmacSha256));
                client.DefaultRequestHeaders.Authorization=new("Bearer",new JwtSecurityTokenHandler().WriteToken(jwt));
                Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/auth/me")).StatusCode);
            }
            client.DefaultRequestHeaders.Authorization=new("Bearer",token);
            var data=new {styleCode="SYNTH-00123",description="Sản phẩm tổng hợp",unitPriceCMT=2.50m,unitPriceDAP=3m,hsCode="64041990",unit="đôi",pairPerCarton=12};
            var created=await client.PostAsJsonAsync("/api/product-masters",data);Assert.Equal(HttpStatusCode.Created,created.StatusCode);
            var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
            var item=await client.GetFromJsonAsync<JsonElement>($"/api/product-masters/{id}");Assert.Equal("SYNTH-00123",item.GetProperty("styleCode").GetString());
            Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync($"/api/product-masters/{id}",data)).StatusCode);
            Assert.True((await client.DeleteAsync($"/api/product-masters/{id}")).IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/product-masters/{id}")).StatusCode);
            Assert.True((await client.PostAsJsonAsync("/api/auth/logout",new {})).IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/auth/me")).StatusCode);
            client.DefaultRequestHeaders.Authorization=new("Bearer","invalid.jwt.value");
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/auth/me")).StatusCode);
        }
        finally
        {
            if(process.Id!=0&&!process.HasExited) {process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}
            await db.Database.EnsureDeletedAsync();
        }
    }
}
