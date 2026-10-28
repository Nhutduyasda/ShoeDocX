"""Browser smoke against the unchanged full application and a NEW synthetic SQL DB.

Runtime credentials exist only in child-process environments. No existing secrets,
database or production configuration is read or modified.
"""
from pathlib import Path
from datetime import datetime
from openpyxl import Workbook
from openpyxl.styles import Font
import base64, json, os, secrets, subprocess, time, urllib.request, urllib.error, uuid

root=Path(__file__).resolve().parents[2]
private=root/'.audit-run/ui-v2/private-full-api'
private.mkdir(parents=True,exist_ok=True)
fixture=private/'synthetic-office.xlsx'
workbook=Workbook();sheet=workbook.active;sheet.title='HoaDon'
for row in [('Số phiếu','CT-00000'),('Ngày chứng từ',datetime(2026,10,24)),('Đã đối chiếu',False),('Số phiếu','KHÔNG DÙNG'),('Mã hàng','Số lượng','Đơn giá','Thành tiền','Đơn vị'),('00000',0,0,0,'cái')]:sheet.append(row)
sheet['B2'].number_format='yyyy-mm-dd'
for cell in sheet[5]:cell.font=Font(bold=True)
sheet['F6']='=B6*C6';sheet['A7']='Tổng cộng';sheet['F7']='=SUM(F6:F6)'
workbook.create_sheet('ThamChieu')['A1']='Synthetic reference 00123'
workbook.save(fixture)
env=os.environ.copy()
database='gate5c_ui_v2_full_'+uuid.uuid4().hex
password='UiV2!'+secrets.token_hex(20)
env.update(ASPNETCORE_ENVIRONMENT='Development',DOTNET_ENVIRONMENT='Development',
 APPDATA=str(private/'profile'),SHOEDOCX_CONFIGURATION_FILE='',OPENAI_API_KEY='',OpenAI__ApiKey='',
 ConnectionStrings__DefaultConnection=os.environ['GATE5C_SQL']+';Database='+database,
 UniversalDocDrafts__ConnectionString=os.environ['GATE5C_SQL']+';Database='+database,
 Jwt__Key=base64.b64encode(secrets.token_bytes(48)).decode(),
 UniversalDocDrafts__Enabled='true',UniversalDocDrafts__ActiveKeyId='ui-v2-synthetic',
 UniversalDocDrafts__Keys__ui_v2_synthetic=base64.b64encode(secrets.token_bytes(32)).decode(),
 DevelopmentUsers__Enabled='false',BootstrapAdmin__Enabled='false',Database__MaintenanceAuthorized='false',
 DataProtection__KeyPath=str(private/'protection'),Xnk__CustomsStoragePath=str(private/'customs'),
 Xnk__DefaultCompanyName='Synthetic UI QA',Xnk__DefaultTaxCode='TEST-ONLY',
 Xnk__DefaultAddress='Synthetic local workspace',Xnk__DefaultCustomerName='Synthetic Customer',
 Logging__LogLevel__Default='Warning',ASPNETCORE_URLS='http://127.0.0.1:5293')
env['UniversalDocDrafts__Keys__ui-v2-synthetic']=env.pop('UniversalDocDrafts__Keys__ui_v2_synthetic')
env['Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command']='Warning'
api=root/'backend/ShoeExportInvoice.Api'
dll=api/'bin/Debug/net10.0/ShoeExportInvoice.Api.dll'
process=None
logs=[]
def command(args):
 result=subprocess.run(['dotnet',str(dll),*args],cwd=api,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=120)
 assert result.returncode==0,'Full API maintenance failed (private diagnostics retained)'
def start():
 log=open(private/f'api-{len(logs)}.log','wb');logs.append(log)
 p=subprocess.Popen(['dotnet',str(dll)],cwd=api,env=env,stdout=log,stderr=subprocess.STDOUT)
 deadline=time.monotonic()+45
 while True:
  try:
   with urllib.request.urlopen('http://127.0.0.1:5293/api/health',timeout=2) as r:
    assert r.status==200
    break
  except (urllib.error.URLError,TimeoutError):
   assert p.poll() is None and time.monotonic()<deadline,'Full API startup failed'
   time.sleep(.2)
 return p
try:
 env['Database__MaintenanceAuthorized']='true';command(['--migrate-all'])
 env.update(BootstrapAdmin__Enabled='true',BootstrapAdmin__Username='ui-v2-admin',BootstrapAdmin__FullName='Synthetic UI QA',BootstrapAdmin__Password=password)
 command(['--initialize-staging'])
 env['Database__MaintenanceAuthorized']='false';env['BootstrapAdmin__Enabled']='false';env.pop('BootstrapAdmin__Password')
 process=start()
 request=urllib.request.Request('http://127.0.0.1:5293/api/auth/login',data=json.dumps({'username':'ui-v2-admin','password':password}).encode(),headers={'Content-Type':'application/json'})
 with urllib.request.urlopen(request) as r: user=json.load(r)['user']
 process.terminate();process.wait(timeout=15)
 env['UniversalDocGate4A__Users__'+user['id']]='11111111-1111-1111-1111-111111111111'
 process=start()
 test_env=env.copy();test_env.update(UI_V2_API_TARGET='http://127.0.0.1:5293',UI_V2_USERNAME='ui-v2-admin',UI_V2_PASSWORD=password,UI_V2_OFFICE_FIXTURE=str(fixture))
 test_args=['node','node_modules/@playwright/test/cli.js','test','--config','playwright.ui-v2.config.ts']
 if os.environ.get('UI_V2_TEST_FILE'):test_args.append(os.environ['UI_V2_TEST_FILE'])
 result=subprocess.run(test_args,cwd=root/'frontend',env=test_env,timeout=360)
 assert result.returncode==0,'Full application browser verification failed'
 (root/'.audit-run/ui-v2/full-api-summary.json').write_text(json.dumps({'status':'PASS','backend':'unchanged full application','database':database,'syntheticOnly':True,'legacyCoverage':'navigation/render/API smoke; CRUD/export parity not implied'}),encoding='utf-8')
finally:
 if process is not None and process.poll() is None:process.terminate();process.wait(timeout=15)
 for log in logs:log.close()
 # Only the fresh UUID database created by this invocation is disposable.
 # The existing host enforces its gate5c_ prefix before executing --drop.
 cleanup_env=env.copy();cleanup_env['GATE5C_DATABASE']=os.environ['GATE5C_SQL']+';Database='+database
 cleanup=subprocess.run(['dotnet',str(root/'tests/Gate5DDemoHost/bin/Debug/net10.0/Gate5DDemoHost.dll'),'--drop'],cwd=root,env=cleanup_env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=90)
 assert cleanup.returncode==0,'Synthetic full-API database cleanup failed'
