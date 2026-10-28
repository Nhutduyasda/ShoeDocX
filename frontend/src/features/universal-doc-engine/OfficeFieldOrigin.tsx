import { colName } from './officeModel';
import type { LearnedField, StudioSession } from './studioApi';
import { coordinate, type WorkbookEvidence } from './workbookPreview';

export function OfficeFieldOrigin({field,session}:{field:LearnedField;session:StudioSession}) {
  const destinations=session.mappings.flatMap(mapping=>{
    const cell=field.scope==='Header'?mapping.headers[field.key]:mapping.columns[field.key]?`${colName(mapping.columns[field.key])}${mapping.startRow}`:undefined;
    return cell?[`${mapping.sheet}!${cell}${field.scope==='LineItem'?` · cột ${colName(mapping.columns[field.key])}, dòng mẫu ${mapping.startRow}`:''}`]:[];
  });
  const candidates=field.candidate!==null&&field.candidates[field.candidate]?[field.candidates[field.candidate]]:field.candidates;
  const labels=Array.from(new Set(candidates.map(c=>`${c.worksheet}!${c.labelCell}: ${c.matchedLabel}`)));
  return <small className="office-field-origin"><span>Ghi vào: {destinations.join('; ')||'Chưa có vị trí áp dụng'}</span><span>{labels.length===1?'Nhãn trong mẫu: ':'Các nhãn gợi ý: '}{labels.join('; ')||'Chưa có bằng chứng nhãn'}</span></small>;
}

export function OfficeFormulaHelp({session,workbook}:{session:StudioSession;workbook?:WorkbookEvidence|null}) {
  const formulas=session.mappings.flatMap(mapping=>{
    const sheet=workbook?.sheets.find(s=>s.name===mapping.sheet);
    return Object.entries(sheet?.cells??{}).filter(([ref,cell])=>cell.formula&&coordinate(ref)?.row===mapping.startRow).map(([ref])=>{
      const position=coordinate(ref)!;
      // Use a title only when the analyzed prototype has one unambiguous table.
      const summary=session.sheetSummaries.find(s=>s.name===mapping.sheet);
      const heading=summary?.tables===1?sheet?.cells[`${colName(position.column)}${mapping.startRow-1}`]:undefined;
      return {sheet:mapping.sheet,cell:ref,label:heading&&!heading.formula?heading.value:''};
    });
  });
  return <details className="office-formula-help"><summary>Vì sao cột trong Excel không có ô nhập ở đây?</summary><p>Bảng nhập dùng các mục đã áp dụng ở bước 2. Tên hoặc thứ tự có thể khác Excel; xem “Nhãn trong mẫu” và “Ghi vào” dưới từng mục. Mục đã bỏ qua không xuất hiện.</p><p>Ô công thức không được dùng làm ô nhập. Bản xem trên trình duyệt không tính công thức. Sau khi xuất, mở tệp Excel để kiểm tra kết quả.</p>{workbook?formulas.length>0?<ul aria-label="Ô công thức trên dòng mẫu">{formulas.map(f=><li key={`${f.sheet}:${f.cell}`}><strong>{f.sheet}!{f.cell}</strong> · Không nhập trực tiếp{f.label?` · Ô phía trên: ${f.label}`:''}</li>)}</ul>:<p>Không tìm thấy ô công thức trên dòng mẫu đang ánh xạ trong bản xem này.</p>:<p>Chọn lại đúng tệp tại “Xem lại mẫu gốc” để chỉ ra các ô công thức của mẫu này.</p>}</details>;
}
