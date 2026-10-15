"""Regenerate tiny synthetic compatibility fixtures; Pillow is generation-only, never an app dependency."""
import base64
import io
import json
from pathlib import Path
from PIL import Image

rows = []


def add(name, extension, data, code):
    rows.append({'name': name, 'extension': extension, 'base64': base64.b64encode(data).decode(), 'code': code})


def pdf(objects):
    data = b'%PDF-1.4\n'
    offsets = [0]
    for i, body in enumerate(objects, 1):
        offsets.append(len(data))
        data += f'{i} 0 obj\n'.encode() + body + b'\nendobj\n'
    offset = len(data)
    data += f'xref\n0 {len(offsets)}\n0000000000 65535 f \n'.encode()
    data += b''.join(f'{v:010} 00000 n \n'.encode() for v in offsets[1:])
    return data + f'trailer\n<< /Size {len(offsets)} /Root 1 0 R >>\nstartxref\n{offset}\n%%EOF\n'.encode()


def stream(data, attributes=b''):
    return b'<< /Length ' + str(len(data)).encode() + b' ' + attributes + b' >>\nstream\n' + data + b'\nendstream'


catalog = b'<< /Type /Catalog /Pages 2 0 R >>'
pages = b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>'
page = b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]'
text = b'BT /F1 12 Tf 72 720 Td (Synthetic invoice 123) Tj ET'
add('selectable-text', 'pdf', pdf([catalog, pages, page + b' /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>',
    stream(text), b'<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>']), 'SOURCE_PDF_SUBSET_UNSUPPORTED')
add('multiple-pages', 'pdf', pdf([catalog, b'<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>', page+b' >>', page+b' >>']), 'SOURCE_PDF_SUBSET_UNSUPPORTED')
image = Image.new('RGB', (8, 8), 'white')
buf = io.BytesIO(); image.save(buf, 'JPEG'); jpeg = buf.getvalue()
add('scanned-image', 'pdf', pdf([catalog, pages, page+b' /Contents 4 0 R /Resources << /XObject << /Im0 5 0 R >> >> >>',
    stream(b'q 72 0 0 72 72 600 cm /Im0 Do Q'), stream(jpeg, b'/Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode')]), 'SOURCE_PDF_SUBSET_UNSUPPORTED')
for name, fmt, options, code in [
    ('baseline-jpeg','JPEG',{},'ACCEPT'),
    ('exif-jpeg','JPEG',{'exif': b'Exif\x00\x00II\x2a\x00\x08\x00\x00\x00\x00\x00\x00\x00\x00\x00'},'SOURCE_JPEG_SUBSET_UNSUPPORTED'),
    ('progressive-jpeg','JPEG',{'progressive': True},'SOURCE_JPEG_SUBSET_UNSUPPORTED'),
    ('rgb-png','PNG',{},'ACCEPT'),
    ('lossy-webp','WEBP',{},'ACCEPT'),
    ('lossless-webp','WEBP',{'lossless':True},'SOURCE_WEBP_SUBSET_UNSUPPORTED')]:
    buf=io.BytesIO(); image.save(buf, fmt, **options)
    add(name, {'JPEG':'jpg','PNG':'png','WEBP':'webp'}[fmt], buf.getvalue(), code)
palette=Image.new('P',(8,8)); palette.putpalette([255,255,255,0,0,0]+[0]*762)
buf=io.BytesIO(); palette.save(buf,'PNG',transparency=0)
add('palette-transparency','png',buf.getvalue(),'SOURCE_MALFORMED_OR_UNSUPPORTED')
alpha=Image.new('RGBA',(8,8),(255,255,255,128)); buf=io.BytesIO(); alpha.save(buf,'WEBP')
add('extended-alpha-webp','webp',buf.getvalue(),'SOURCE_WEBP_SUBSET_UNSUPPORTED')
buf=io.BytesIO(); image.save(buf,'WEBP',save_all=True,append_images=[Image.new('RGB',(8,8),'black')],duration=100,loop=0)
add('animated-webp','webp',buf.getvalue(),'SOURCE_WEBP_SUBSET_UNSUPPORTED')
Path('tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate4B2AFixtures.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
