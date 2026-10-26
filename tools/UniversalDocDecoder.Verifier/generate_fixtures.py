"""Synthetic generation only. Pillow is not a worker/runtime dependency."""
import base64
import io
import json
from pathlib import Path
from PIL import Image

rows = []


def add(name, mime, data, pages=1, width=0, height=0, text=None, code=None):
    rows.append(dict(Name=name, MediaType=mime, Base64=base64.b64encode(data).decode(), Pages=pages,
                     Width=width, Height=height, Text=text, ExpectedCode=code))


image = Image.new('RGB', (16, 8), 'white')
for x in range(8):
    for y in range(8): image.putpixel((x, y), (200, 20, 30))
for name, fmt, options, width, height in [
    ('jpeg-baseline', 'JPEG', {}, 16, 8),
    ('jpeg-progressive', 'JPEG', {'progressive': True}, 16, 8),
    ('jpeg-exif-orientation', 'JPEG', {'exif': Image.Exif()}, 8, 16),
    ('png-rgb', 'PNG', {}, 16, 8), ('webp-lossy', 'WEBP', {}, 16, 8),
    ('webp-lossless', 'WEBP', {'lossless': True}, 16, 8)]:
    if name == 'jpeg-exif-orientation': options['exif'][274] = 6
    data = io.BytesIO(); image.save(data, fmt, **options)
    add(name, 'image/' + {'JPEG': 'jpeg', 'PNG': 'png', 'WEBP': 'webp'}[fmt], data.getvalue(), width=width, height=height)
for name, fmt, obj in [('png-rgba', 'PNG', image.convert('RGBA')), ('png-palette', 'PNG', image.convert('P')),
                       ('webp-alpha', 'WEBP', Image.new('RGBA', (16, 8), (200, 20, 30, 128)))]:
    data=io.BytesIO(); obj.save(data,fmt); add(name,'image/'+fmt.lower(),data.getvalue(),width=16,height=8)
data=io.BytesIO(); image.save(data,'WEBP',save_all=True,append_images=[Image.new('RGB',(16,8),'black')],duration=100)
add('webp-animation','image/webp',data.getvalue(),pages=0,code='ANIMATION_UNSUPPORTED')
add('image-truncated','image/png',b'\x89PNG\r\n\x1a\n',pages=0,code='IMAGE_INVALID')


def pdf(objects):
    data=b'%PDF-1.4\n'; offsets=[]
    for i,obj in enumerate(objects,1):
        offsets.append(len(data));data+=f'{i} 0 obj\n'.encode()+obj+b'\nendobj\n'
    xref=len(data);data+=f'xref\n0 {len(objects)+1}\n0000000000 65535 f \n'.encode()
    data+=b''.join(f'{x:010} 00000 n \n'.encode() for x in offsets)
    return data+f'trailer\n<< /Size {len(objects)+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n'.encode()


def stream(data, extra=b''):
    return b'<< /Length '+str(len(data)).encode()+b' '+extra+b' >>\nstream\n'+data+b'\nendstream'


catalog=b'<< /Type /Catalog /Pages 2 0 R >>'
tree=b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>'
page=b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 144 72]'
font=b'<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>'
text=b'BT /F1 12 Tf 6 36 Td (Synthetic 123) Tj ET'
objects=[catalog,tree,page+b' /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>',stream(text),font]
add('pdf-selectable-text','application/pdf',pdf(objects),width=300,height=150,text='Synthetic 123')
rotated=objects.copy();rotated[2]=rotated[2].replace(b'/Contents',b'/Rotate 90 /Contents')
add('pdf-rotated','application/pdf',pdf(rotated),width=150,height=300,text='Synthetic 123')
multiple=objects.copy();multiple[1]=b'<< /Type /Pages /Kids [3 0 R 6 0 R] /Count 2 >>';multiple.append(objects[2])
add('pdf-multiple-pages','application/pdf',pdf(multiple),pages=2,width=300,height=150,text='Synthetic 123')
data=io.BytesIO();image.save(data,'JPEG')
scanned=[catalog,tree,page+b' /Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >>',
         stream(b'q 120 0 0 60 0 0 cm /Im0 Do Q'),stream(data.getvalue(),b'/Type /XObject /Subtype /Image /Width 16 /Height 8 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode')]
add('pdf-scanned-raster','application/pdf',pdf(scanned),width=300,height=150)
layout=objects.copy();layout[3]=stream(b'BT /F1 8 Tf 1 0 0 1 8 48 Tm (Left) Tj 1 0 0 1 75 20 Tm (Right) Tj ET')
add('pdf-layout','application/pdf',pdf(layout),width=300,height=150,text='Left')
for name, token in [('pdf-encrypted',b'/Encrypt'),('pdf-action',b'/OpenAction /JavaScript'),('pdf-external',b'/URI')]:
    changed=objects.copy();changed[0]=catalog[:-2]+b' '+token+b' >>'
    add(name,'application/pdf',pdf(changed),pages=0,code='PDF_FEATURE_UNSUPPORTED')
add('pdf-malformed','application/pdf',b'%PDF-1.4\ninvalid',pages=0,code='PDF_INVALID_OR_ENCRYPTED')
add('pdf-compressed-quota','application/pdf',b'%PDF-1.4\n/FlateDecode',pages=0,code='PDF_EXPANSION_ACCOUNTING_UNAVAILABLE')
Path('tools/UniversalDocDecoder.Verifier/fixtures.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
