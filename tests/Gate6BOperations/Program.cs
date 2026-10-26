using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Tests.UniversalDocEngine;
using ClosedXML.Excel;

// Disposable CI maintenance only. Never shipped in the backend image.
var config = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("GATE6B_OPERATOR")!)).RootElement;
var raw = config.GetProperty("adminConnection").GetString()!;
var cs = new SqlConnectionStringBuilder(raw);
var database = config.GetProperty("database").GetString()!;
if (!System.Text.RegularExpressions.Regex.IsMatch(database,"^gate6b_[a-f0-9]{32}$")) throw new InvalidOperationException("SYNTHETIC_DATABASE_REQUIRED");
async Task Execute(string sql, params SqlParameter[] parameters) {
    await using var c = new SqlConnection(raw); await c.OpenAsync();
    await using var q = new SqlCommand(sql,c) {CommandTimeout=90}; q.Parameters.AddRange(parameters); await q.ExecuteNonQueryAsync();
}
var action=args[0];
if(action is "seal" or "unseal") {
    var key=File.ReadAllBytes(args[3]);using var aes=new AesGcm(key,16);var input=File.ReadAllBytes(args[1]);
    if(action=="seal") {var nonce=RandomNumberGenerator.GetBytes(12);var output=new byte[input.Length+28];nonce.CopyTo(output,0);aes.Encrypt(nonce,input,output.AsSpan(12,input.Length),output.AsSpan(12+input.Length,16));File.WriteAllBytes(args[2],output);}
    else {var output=new byte[input.Length-28];aes.Decrypt(input.AsSpan(0,12),input.AsSpan(12,input.Length-28),input.AsSpan(input.Length-16),output);File.WriteAllBytes(args[2],output);}
} else if(action=="provision") {
    await Execute($"CREATE DATABASE [{database}]");
    foreach(var name in new[]{"migration","runtime"}) {
        var login="gate6b_"+name;
        var password=config.GetProperty(name+"Password").GetString()!;
        // Server DDL does not accept password parameters. QUOTENAME is evaluated by SQL.
        await Execute("DECLARE @sql nvarchar(max) = N'CREATE LOGIN [' + @login + N'] WITH PASSWORD=' + QUOTENAME(@password,'''') + N', CHECK_POLICY=ON'; EXEC(@sql);",
            new SqlParameter("@login",login),new SqlParameter("@password",password));
        await Execute($"USE [{database}]; CREATE USER [{login}] FOR LOGIN [{login}]; ALTER ROLE [{(name=="migration"?"db_owner":"db_datareader")}] ADD MEMBER [{login}];");
        if(name=="runtime") await Execute($"USE [{database}]; ALTER ROLE db_datawriter ADD MEMBER [{login}];");
    }
} else if(action=="users") {
    cs.InitialCatalog=database;
    await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs.ConnectionString).Options);
    foreach(var (id,department) in new[]{("admin-a",Department.Admin),("other-a",Department.Xnk),("admin-b",Department.Admin),("clerk-a",Department.Kho)}) {
        var user=new ApplicationUser { Id=id,UserName=id,NormalizedUserName=id.ToUpperInvariant(),FullName="Synthetic pilot",Department=department,SecurityStamp=Guid.NewGuid().ToString(),ConcurrencyStamp=Guid.NewGuid().ToString() };
        user.PasswordHash=new PasswordHasher<ApplicationUser>().HashPassword(user,config.GetProperty("userPassword").GetString()!);
        db.Users.Add(user);
    }
    await db.SaveChangesAsync();
} else if(action=="fixtures") {
    Directory.CreateDirectory(args[1]);
    foreach(var scenario in new[]{"A","B","C","D","E","F","G","H"})
      foreach(var rows in new[]{1,3}) foreach(var filled in new[]{false,true})
        File.WriteAllBytes(Path.Combine(args[1],$"{scenario}-{rows}-{filled.ToString().ToLowerInvariant()}-none.xlsx"),Gate5DFixtures.Workbook(scenario,rows,filled));
    File.WriteAllBytes(Path.Combine(args[1],"H-1-false-cross-sheet.xlsx"),Gate5DFixtures.Workbook("H",unsupported:"cross-sheet"));
} else if(action=="backup") {
    await Execute($"BACKUP DATABASE [{database}] TO DISK='/var/opt/mssql/backup/staging.bak' WITH INIT, CHECKSUM;");
} else if(action=="restore") {
    await Execute($"RESTORE VERIFYONLY FROM DISK='/var/opt/mssql/backup/staging.bak' WITH CHECKSUM;");
    await Execute($"RESTORE DATABASE [{database}] FROM DISK='/var/opt/mssql/backup/staging.bak' WITH CHECKSUM, REPLACE;");
    await Execute($"USE [{database}]; ALTER USER [gate6b_migration] WITH LOGIN=[gate6b_migration]; ALTER USER [gate6b_runtime] WITH LOGIN=[gate6b_runtime];");
} else if(action=="fingerprint") {
    await using var c=new SqlConnection(raw);await c.OpenAsync();
    await using var q=new SqlCommand($"SELECT Id, KeyId, Ciphertext, RowVersion FROM [{database}].ude.Drafts ORDER BY Id",c);
    await using var r=await q.ExecuteReaderAsync();using var sha=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var rows=0;
    while(await r.ReadAsync()){rows++;sha.AppendData(System.Text.Encoding.UTF8.GetBytes(r.GetString(0)+r.GetString(1)));sha.AppendData((byte[])r[2]);sha.AppendData((byte[])r[3]);}
    File.WriteAllText(args[1],JsonSerializer.Serialize(new{rows,sha256=Convert.ToHexString(sha.GetHashAndReset())}));
} else if(action=="expire") {
    await Execute($"UPDATE [{database}].ude.Drafts SET ExpiresAt=DATEADD(day,-1,SYSDATETIMEOFFSET())");
} else if(action=="tls-negative") {
    cs.HostNameInCertificate="wrong-sql.invalid";
    try { await using var c=new SqlConnection(cs.ConnectionString); await c.OpenAsync(); }
    catch(SqlException) {Console.WriteLine("OPERATION_PASS");return;}
    throw new InvalidOperationException("WRONG_SQL_CERTIFICATE_ACCEPTED");
} else if(action=="permissions") {
    cs.InitialCatalog=database;cs.UserID="gate6b_runtime";cs.Password=config.GetProperty("runtimePassword").GetString();
    await using var c=new SqlConnection(cs.ConnectionString);await c.OpenAsync();
    await using var q=new SqlCommand("SELECT HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE TABLE'), IS_MEMBER('db_owner'), HAS_PERMS_BY_NAME('ude.Drafts','OBJECT','SELECT'), HAS_PERMS_BY_NAME('ude.Drafts','OBJECT','UPDATE')",c);
    await using var r=await q.ExecuteReaderAsync();await r.ReadAsync();
    if(r.GetInt32(0)!=0||r.GetInt32(1)!=0||r.GetInt32(2)!=1||r.GetInt32(3)!=1)throw new InvalidOperationException("RUNTIME_PERMISSIONS_INVALID");
} else if(action=="reopen") {
    foreach(var file in Directory.GetFiles(args[1],"office-*.xlsx")){using var workbook=new XLWorkbook(file);if(workbook.Worksheets.Count<2)throw new InvalidOperationException("WORKBOOK_INVALID");}
} else throw new InvalidOperationException("UNKNOWN_OPERATION");
Console.WriteLine("OPERATION_PASS");
