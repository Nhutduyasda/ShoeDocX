# Disposable hosted runner only. Installer output and credentials never enter artifacts.
param([string]$Output)
$ErrorActionPreference='Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or !$IsWindows) { throw 'DISPOSABLE_WINDOWS_RUNNER_REQUIRED' }
$root=Join-Path $env:RUNNER_TEMP ('gate6b-tcp-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $root | Out-Null
icacls $root /inheritance:r /grant:r "$([System.Security.Principal.WindowsIdentity]::GetCurrent().Name):(OI)(CI)F" 'SYSTEM:(OI)(CI)F' | Out-Null
$report=[ordered]@{status='BLOCKED';productionParity='UNVERIFIED';phase='download';installerEvidence=@();publicSqlPort=$false}
$thumb=$null;$caThumb=$null;$rsa=$null;$instance='GATE6BTCP';$service='MSSQL$GATE6BTCP'
function Verified([string]$File) {
 $sig=Get-AuthenticodeSignature $File
 if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'CN=Microsoft Corporation,') { throw 'MICROSOFT_SIGNATURE_REQUIRED' }
 $report.installerEvidence+=@{sha256=(Get-FileHash $File -Algorithm SHA256).Hash;signature='Valid Microsoft Corporation';version=(Get-Item $File).VersionInfo.FileVersion}
}
function Run-Private([string]$File,[string[]]$Arguments,[int]$Seconds) {
 $p=Start-Process $File -ArgumentList $Arguments -PassThru -RedirectStandardOutput (Join-Path $root ('stdout-'+[guid]::NewGuid()+'.txt')) -RedirectStandardError (Join-Path $root ('stderr-'+[guid]::NewGuid()+'.txt'))
 if (!$p.WaitForExit($Seconds*1000)) { Stop-Process -Id $p.Id -Force; throw 'BOUNDED_PROCESS_TIMEOUT' }
 if ($p.ExitCode -notin @(0,3010)) { $report.processExit=$p.ExitCode;throw 'PROCESS_FAILED' }
}
try {
 if (Get-Service $service -ErrorAction SilentlyContinue) { throw 'REFUSING_EXISTING_SQL_INSTANCE' }
 $bootstrap=Join-Path $root 'express.exe'
 # Official current Express link from Microsoft's SQL Server download page.
 Invoke-WebRequest 'https://go.microsoft.com/fwlink/p/?clcid=0x409&country=us&culture=en-us&linkid=2216019' -OutFile $bootstrap -TimeoutSec 180
 Verified $bootstrap
 $media=Join-Path $root 'media';New-Item -ItemType Directory $media | Out-Null
 $report.phase='download-core';Run-Private $bootstrap @('/ACTION=Download','/MEDIATYPE=Core',"/MEDIAPATH=`"$media`"",'/QUIET') 300
 $core=Get-ChildItem $media -Filter 'SQLEXPR*x64*.exe' -Recurse | Select-Object -First 1
 if (!$core) { throw 'SIGNED_CORE_MEDIA_UNAVAILABLE' };Verified $core.FullName
 $unpacked=Join-Path $root 'unpacked';$report.phase='extract';Run-Private $core.FullName @('/Q',"/x:`"$unpacked`"") 120
 $setup=Join-Path $unpacked 'setup.exe';Verified $setup
 $report.phase='install'
 $sa='G6b!'+[guid]::NewGuid().ToString('N')+'aA1'
 Run-Private $setup @('/Q','/IACCEPTSQLSERVERLICENSETERMS','/SUPPRESSPRIVACYSTATEMENTNOTICE','/ACTION=Install','/FEATURES=SQLEngine',"/INSTANCENAME=$instance",'/SQLSVCACCOUNT="NT Service\MSSQL$GATE6BTCP"',"/SQLSYSADMINACCOUNTS=`"$([System.Security.Principal.WindowsIdentity]::GetCurrent().Name)`"",'/SECURITYMODE=SQL',"/SAPWD=$sa",'/TCPENABLED=0','/NPENABLED=0','/SQLSVCSTARTUPTYPE=Manual','/UPDATEENABLED=True') 900
 Stop-Service $service -ErrorAction SilentlyContinue
 $report.phase='certificate'
 $ca=New-SelfSignedCertificate -Type Custom -Subject 'CN=Gate6B Synthetic SQL Root' -KeyUsage CertSign,CRLSign,DigitalSignature -TextExtension '2.5.29.19={critical}{text}ca=1&pathlength=0' -CertStoreLocation 'Cert:\LocalMachine\My' -KeyAlgorithm RSA -KeyLength 2048 -KeySpec Signature -Provider 'Microsoft RSA SChannel Cryptographic Provider' -NotAfter (Get-Date).AddDays(2)
 $caThumb=$ca.Thumbprint
 $cert=New-SelfSignedCertificate -Type SSLServerAuthentication -Signer $ca -DnsName $env:COMPUTERNAME,'localhost' -CertStoreLocation 'Cert:\LocalMachine\My' -KeyAlgorithm RSA -KeyLength 2048 -KeySpec KeyExchange -Provider 'Microsoft RSA SChannel Cryptographic Provider' -NotAfter (Get-Date).AddDays(1)
 $thumb=$cert.Thumbprint
 $public=Join-Path $root 'ca.cer';Export-Certificate -Cert $ca -FilePath $public | Out-Null
 Import-Certificate -FilePath $public -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
 $cert=Get-Item "Cert:\LocalMachine\My\$thumb"
 $report.leafHasPrivateKey=$cert.HasPrivateKey
 $rsa=[System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
 if (!$rsa) {throw 'SQL_RSA_PRIVATE_KEY_REQUIRED'}
 $report.privateKeyProvider=$rsa.GetType().Name
 if ($rsa -isnot [System.Security.Cryptography.RSACryptoServiceProvider]) {throw 'LEGACY_SQL_PRIVATE_KEY_REQUIRED'}
 $account=(Get-CimInstance Win32_Service -Filter "Name='$service'").StartName
 if ($account -ne 'NT Service\MSSQL$GATE6BTCP') {throw 'SQL_VIRTUAL_SERVICE_REQUIRED'}
 $keyInfo=$rsa.CspKeyContainerInfo
 $report.machinePrivateKey=$keyInfo.MachineKeyStore
 if (!$keyInfo.MachineKeyStore) {throw 'MACHINE_PRIVATE_KEY_REQUIRED'}
 $keyFile=Join-Path "$env:ProgramData\Microsoft\Crypto\RSA\MachineKeys" $keyInfo.UniqueKeyContainerName
 if (!(Test-Path -LiteralPath $keyFile -PathType Leaf)) {throw 'MACHINE_KEY_FILE_REQUIRED'}
 $serviceSid=([System.Security.Principal.NTAccount]::new($account)).Translate([System.Security.Principal.SecurityIdentifier])
 $acl=Get-Acl -LiteralPath $keyFile
 $acl.SetAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new($serviceSid,[System.Security.AccessControl.FileSystemRights]::Read,[System.Security.AccessControl.AccessControlType]::Allow))
 Set-Acl -LiteralPath $keyFile -AclObject $acl
 $report.servicePrivateKeyAclVerified=@((Get-Acl -LiteralPath $keyFile).GetAccessRules($true,$false,[System.Security.Principal.SecurityIdentifier]) | Where-Object {$_.IdentityReference -eq $serviceSid -and $_.AccessControlType -eq 'Allow' -and ($_.FileSystemRights -band [System.Security.AccessControl.FileSystemRights]::ReadData)}).Count -gt 0
 if (!$report.servicePrivateKeyAclVerified) {throw 'SQL_PRIVATE_KEY_ACL_REQUIRED'}
 $id=(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL').$instance
 $network="HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$id\MSSQLServer\SuperSocketNetLib"
 Set-ItemProperty $network Certificate $thumb.ToLowerInvariant();Set-ItemProperty $network ForceEncryption 1
 Set-ItemProperty "$network\Tcp" Enabled 1;Set-ItemProperty "$network\Tcp" ListenOnAllIPs 0
 $loopback=$false
 foreach ($ip in Get-ChildItem "$network\Tcp" | Where-Object {$_.PSChildName -ne 'IPAll'}) {
   $v=Get-ItemProperty $ip.PSPath;$enabled=if ($v.IpAddress -eq '127.0.0.1') {1} else {0}
   Set-ItemProperty $ip.PSPath Enabled $enabled
   if ($enabled) {$loopback=$true;Set-ItemProperty $ip.PSPath TcpDynamicPorts '';Set-ItemProperty $ip.PSPath TcpPort '50143'}
 }
 if (!$loopback) { throw 'LOOPBACK_TCP_CONFIGURATION_UNAVAILABLE' }
 $report.phase='start-sql';Start-Service $service
 $listeners=@()
 for($i=0;$i -lt 20;$i++) {$listeners=@(Get-NetTCPConnection -State Listen -LocalPort 50143 -ErrorAction SilentlyContinue);if($listeners.Count){break};Start-Sleep -Milliseconds 500}
 if (!$listeners.Count -or @($listeners | Where-Object {$_.LocalAddress -ne '127.0.0.1'}).Count) {throw 'LOOPBACK_SQL_LISTENER_NOT_PROVEN'}
 $report.loopbackTcpListenerVerified=$true
 $env:GATE6B_WINDOWS_TCP='Server=tcp:localhost,50143;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;Connect Timeout=5'
 $env:GATE6B_WINDOWS_PRIVATE=$root
 $report.phase='native-production';$native=Join-Path $root 'native.json'
 Run-Private 'dotnet' @('run','--no-build','--project',"`"$env:GITHUB_WORKSPACE/feature/tests/Gate6BOperations/Gate6BOperations.csproj`"",'--','native-tcp',"`"$native`"") 300
 $report.native=Get-Content $native -Raw | ConvertFrom-Json
 $report.status=if($report.native.status -eq 'PARTIAL'){'PARTIAL'}else{'BLOCKED'};$report.phase='complete'
 $report.blocker='Inspect native subreport for executed stages. Trusted reverse-proxy, unprivileged application identity, full native browser/owner/tenant/draft recovery parity remain unverified. No Windows platform waiver.'
} catch {
 $report.errorType=$_.Exception.GetType().Name
 # Only our fixed identifiers may leave private diagnostics.
 if ($_.Exception.Message -match '^[A-Z_]+$') {$report.errorCode=$_.Exception.Message}
 if ($id) {
   $log=Join-Path "$env:ProgramFiles\Microsoft SQL Server" "$id\MSSQL\Log\ERRORLOG"
   if (Test-Path $log) {
     $text=Get-Content $log -Raw
     $report.sqlErrorNumbers=@([regex]::Matches($text,'Error: (\d+)') | ForEach-Object {$_.Groups[1].Value} | Sort-Object -Unique)
     $tlsLines=($text -split "`n" | Where-Object {$_ -match 'certificate|TDSSNI|SSL'}) -join "`n"
     $report.sqlTlsCodes=@([regex]::Matches($tlsLines,'0x[0-9A-Fa-f]{8}') | ForEach-Object {$_.Value} | Sort-Object -Unique)
     $report.sqlStartupClassifiers=@{
       certificateLoadFailure=($text -match 'Unable to load user-specified certificate|could not load the certificate|Unable to initialize SSL')
       privateKeyAccess=($text -match '0x8009030d|0x80090016|Access is denied')
       tcpInitializationFailure=($text -match 'TDSSNIClient initialization failed|TCP Provider')
       portAlreadyUsed=($text -match '10048|Only one usage of each socket')
     }
   }
 }
} finally {
 Stop-Service $service -ErrorAction SilentlyContinue
 if ($rsa) {$rsa.Dispose()}
 if ($thumb) {Remove-Item "Cert:\LocalMachine\My\$thumb" -DeleteKey -ErrorAction SilentlyContinue}
 if ($caThumb) {Remove-Item "Cert:\LocalMachine\Root\$caThumb" -ErrorAction SilentlyContinue;Remove-Item "Cert:\LocalMachine\My\$caThumb" -DeleteKey -ErrorAction SilentlyContinue}
 Remove-Item Env:\GATE6B_WINDOWS_TCP -ErrorAction SilentlyContinue
 Remove-Item Env:\GATE6B_WINDOWS_PRIVATE -ErrorAction SilentlyContinue
 New-Item -ItemType Directory (Split-Path $Output) -Force | Out-Null
 $report | ConvertTo-Json -Depth 10 | Set-Content $Output
 $report | ConvertTo-Json -Depth 10 | Write-Output
 Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue
}
