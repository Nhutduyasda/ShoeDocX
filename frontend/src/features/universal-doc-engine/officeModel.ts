import type { LearnedField, StudioRule, StudioSession } from './studioApi';
export const officeErrors: Readonly<Record<string, string>> = Object.freeze({
  SOURCE_PDF_SUBSET_UNSUPPORTED: 'PDF này nằm ngoài tập được hỗ trợ. Công cụ hiện chưa đọc nội dung PDF/OCR; hãy dùng dữ liệu nhập tay và mẫu Excel hợp lệ.',
  REQUEST_FAILED: 'Không thể hoàn tất thao tác. Hãy thử lại hoặc liên hệ người quản trị.',
  UNSAVED_CHANGES: 'Có thay đổi chưa áp dụng. Hãy áp dụng hoặc hoàn tác trước khi tiếp tục.',
  SESSION_EXPIRED: 'Phiên làm việc đã hết hạn. Hãy mở lại bản nháp hoặc tạo phiên mới.',
  SESSION_NOT_FOUND: 'Phiên riêng tư không còn khả dụng. Hãy mở bản nháp hoặc tạo phiên mới.',
  SESSION_VERSION_CONFLICT: 'Dữ liệu đã thay đổi. Hãy tải lại trạng thái mới nhất trước khi tiếp tục.',
  IDENTITY_EXPIRED: 'Đăng nhập đã hết hạn. Hãy đăng nhập lại.',
  CSRF_TOKEN_REQUIRED: 'Thiếu xác nhận phiên đăng nhập. Hãy tải lại trang và thử lại.',
  CSRF_TOKEN_INVALID: 'Xác nhận đăng nhập đã thay đổi. Hãy tải lại trang.',
  STUDIO_MEMBERSHIP_REQUIRED: 'Tài khoản chưa được cấp quyền sử dụng công cụ Excel.',
  STUDIO_BUSY: 'Hệ thống đang bận. Hãy thử lại sau ít phút.',
  SESSION_CAPACITY_REACHED: 'Đã đạt giới hạn phiên. Hãy đóng phiên không dùng hoặc đợi phiên hết hạn.',
  SUPPORTED_XLSX_REQUIRED: 'Chỉ nhận tệp .xlsx hợp lệ. Không hỗ trợ macro hoặc nội dung Excel chủ động.',
  UNSUPPORTED_OR_INVALID_TEMPLATE: 'Mẫu Excel có cấu trúc chưa hỗ trợ hoặc không hợp lệ. Dùng một bảng dữ liệu cho mỗi sheet; tránh macro, công thức liên sheet và liên kết ngoài.',
  STUDIO_TEMPLATE_LIMIT: 'Mẫu vượt giới hạn: tối đa 4 sheet, 2.000 ô đã phân tích, dòng 1.000 và cột AF.',
  SESSION_BYTE_LIMIT: 'Tổng mẫu, ví dụ và tài liệu nguồn không được vượt 2 MiB.',
  EXAMPLE_STRUCTURE_MISMATCH: 'Tệp ví dụ phải giữ tên sheet, tiêu đề, định dạng và cấu trúc mẫu. Chỉ hỗ trợ 1–5 dòng ví dụ liên tiếp với công thức và chân bảng phù hợp.',
  UNRESOLVED_SCHEMA_FIELD: 'Hãy chọn một vị trí Excel cho từng mục hoặc bỏ qua mục tùy chọn.',
  INVALID_SCHEMA_FIELDS: 'Kiểm tra tên, kiểu dữ liệu và các mục bắt buộc/bỏ qua; không để trùng mục.',
  INVALID_SCHEMA_MAPPING: 'Vị trí Excel bị trùng hoặc không đúng vùng. Ô thông tin phải nằm trước bảng dữ liệu.',
  INVALID_SCHEMA_DESTINATION: 'Chọn ô có sẵn, không gộp và không chứa công thức trong vùng được hỗ trợ.',
  INVALID_MULTISHEET_MAPPING: 'Ánh xạ Excel chưa hợp lệ. Kiểm tra sheet, cột và dòng mẫu đã chọn.',
  INVALID_MULTISHEET_DESTINATION: 'Vị trí Excel không hợp lệ hoặc nằm trên ô gộp/công thức.',
  INVALID_WORKFLOW_MAPPING: 'Mỗi mục đang dùng cần ít nhất một vị trí hợp lệ; không dùng trùng ô/cột hoặc sheet.',
  IDENTIFIER_REQUIRES_TEXT: 'Mã có số 0 ở đầu phải dùng kiểu Văn bản để giữ nguyên giá trị.',
  SINGLE_PROTOTYPE_PER_SHEET_REQUIRED: 'Mỗi sheet chỉ hỗ trợ một dòng mẫu để lặp dữ liệu.',
  EXAMPLE_SHARED_ORDERED_IDENTIFIER_REQUIRED: 'Các sheet dùng chung dòng cần cùng một mục mã Văn bản, không trùng và cùng thứ tự trong ví dụ.',
  EXAMPLE_SHARED_FIELD_VALUES_DIFFER: 'Giá trị ví dụ của mục dùng chung khác nhau giữa các sheet. Hãy kiểm tra lại ý nghĩa và thứ tự dòng.',
  EXAMPLE_SELECTED_SHEETS_MISMATCH: 'Các sheet được chọn không tương ứng với ví dụ. Hãy kiểm tra lại phạm vi dữ liệu.',
  SCHEMA_CORRECTION_REQUIRED: 'Hãy áp dụng lựa chọn các mục dữ liệu trước khi xác nhận vị trí Excel.',
  MAPPING_CORRECTION_REQUIRED: 'Hãy hoàn tất lựa chọn vị trí Excel trước khi xác nhận.',
  MAPPING_CONFIRMATION_REQUIRED: 'Hãy kiểm tra và xác nhận vị trí Excel trước khi tiếp tục.',
  MAPPING_CONFIRMATION_STALE: 'Vị trí Excel đã đổi. Hãy kiểm tra và xác nhận lại.',
  EXTRACTION_REQUIRED: 'Chưa có dữ liệu nhập tay. Hãy nhập và gửi bảng dữ liệu để kiểm tra.',
  DOCUMENT_VALIDATION_REQUIRED: 'Dữ liệu còn lỗi hoặc cảnh báo cần xử lý trước khi rà soát và xuất Excel.',
  CURRENT_HUMAN_REVIEW_REQUIRED: 'Hãy hoàn tất rà soát dữ liệu hiện tại trước khi phê duyệt.',
  CURRENT_APPROVAL_REQUIRED: 'Dữ liệu hiện tại cần được rà soát và phê duyệt lại.',
  REVIEW_PERMISSION_REQUIRED: 'Tài khoản này không có quyền hoàn tất rà soát.',
  APPROVAL_PERMISSION_REQUIRED: 'Tài khoản này không có quyền phê duyệt.',
  EXPORT_PERMISSION_REQUIRED: 'Tài khoản này không có quyền xuất tài liệu đã phê duyệt.',
  INVALID_EDITED_VALUE: 'Giá trị chưa đúng kiểu. Dùng số thập phân dấu chấm, ngày năm-tháng-ngày hoặc Có/Không.',
  SAMPLE_LIMIT: 'Chỉ nhập 1–20 dòng, tối đa 2.000 ký tự mỗi giá trị.',
  INVALID_VALIDATION_RULES: 'Kiểm tra phép tính, mục số, độ lệch từ 0 đến 1 và mức độ của quy tắc.',
  INVALID_RULE_FIELD: 'Quy tắc chỉ được dùng các mục Số phù hợp, đang sử dụng và đúng phạm vi.',
  DUPLICATE_OR_CONFLICTING_RULE: 'Quy tắc trùng hoặc cùng kiểm tra một kết quả theo cách xung đột.',
  DRAFT_REVISION_CONFLICT: 'Bản nháp đã thay đổi. Tải danh sách mới rồi chọn rõ phiên bản cần mở hoặc thay thế.',
  DRAFT_EXPIRED: 'Bản nháp đã hết hạn và không thể mở lại.',
  DRAFT_NOT_FOUND: 'Bản nháp riêng tư không còn khả dụng hoặc đã bị xóa.',
  DRAFT_CONFIGURATION_UNAVAILABLE: 'Lưu bản nháp chưa khả dụng. Nhờ quản trị viên kiểm tra cấu hình lưu trữ được bảo vệ.',
  DRAFT_STORAGE_UNAVAILABLE: 'Chưa xác nhận lưu thành công. Tải lại phiên và danh sách bản nháp trước khi thử lại.',
  DRAFT_CAPACITY_REACHED: 'Đã đủ hai bản nháp riêng tư. Xóa một bản không dùng trước khi tạo bản mới.',
  DRAFT_PAYLOAD_LIMIT: 'Bản nháp vượt giới hạn lưu trữ. Chưa xác nhận lưu thành công.',
  DRAFT_PAYLOAD_INVALID: 'Không xác minh được tính toàn vẹn của bản nháp. Hệ thống đã chặn khôi phục.',
  DRAFT_FINGERPRINT_MISMATCH: 'Cấu hình khôi phục không khớp bản đã lưu. Hệ thống đã chặn khôi phục.',
  DRAFT_RESUME_EXPIRED: 'Khôi phục đã hết thời gian. Hãy tải danh sách và mở lại bản nháp.',
  FORMAT_VALUE: 'Định dạng chưa rõ: số dùng dấu chấm và không phân nhóm hàng nghìn; ngày dùng năm-tháng-ngày.',
  PREVIEW_LIMIT: 'Dữ liệu vượt giới hạn xem thông tin. Chỉ hỗ trợ tối đa 20 dòng và 3 sheet dữ liệu.',
});
export function officeError(code: unknown): string { return typeof code === 'string' && Object.hasOwn(officeErrors, code) ? officeErrors[code] : officeErrors.REQUEST_FAILED; }
export class OfficeRequestError extends Error {
  readonly code: string; readonly status: number;
  constructor(code: string, status = 0) { super(officeError(code)); this.code=code; this.status=status; }
}
export const typeLabel: Readonly<Record<string,string>> = { Text:'Văn bản', Decimal:'Số', Date:'Ngày', Boolean:'Có/Không' };
export const scopeLabel: Readonly<Record<string,string>> = { Header:'Thông tin đầu phiếu', LineItem:'Cột trong bảng' };
export function colName(n: number): string { return Number.isInteger(n) && n>=1 && n<=32 ? n<=26 ? String.fromCharCode(64+n) : 'A'+String.fromCharCode(38+n) : '?'; }
export function ruleText(rule: StudioRule, session: StudioSession): string {
  const label=(key:string|null)=>session.schemaDraft?.fields.find(f=>f.key===key&&!f.omit)?.label ?? 'Mục chưa chọn';
  return rule.kind==='CrossFoot' ? `Tổng cột ${label(rule.fieldA)} = ${label(rule.target)}` : `${label(rule.fieldA)} ${{Add:'+',Subtract:'−',Multiply:'×',Divide:'÷'}[rule.operator||'Multiply'] || '?'} ${label(rule.fieldB)} = ${label(rule.target)}`;
}
export function validEntry(field: LearnedField, value: string): boolean {
  if(value==='') return true; // Missing required values are reported by the server, never marked PASS here.
  if(field.type==='Decimal') return /^[+-]?\d+(?:\.\d+)?$/.test(value);
  if(field.type==='Date') return /^\d{4}-\d{2}-\d{2}$/.test(value);
  if(field.type==='Boolean') return value==='true'||value==='false';
  return true;
}
export const issueText: Readonly<Record<string,string>> = {
  REQUIRED_FIELD_MISSING:'Thiếu giá trị bắt buộc.', MISSING_FIELD:'Thiếu giá trị.', REQUIRED_HEADER_MISSING:'Thiếu thông tin bắt buộc.',
  MISSING_RULE_INPUT:'Chưa đủ giá trị để kiểm tra quy tắc.', DIVISION_BY_ZERO:'Không thể chia cho 0.', NUMERIC_OVERFLOW:'Số vượt phạm vi được hỗ trợ.',
  ARITHMETIC_MISMATCH:'Kết quả đã nhập không khớp phép tính.', CROSS_FOOT_MISMATCH:'Tổng đã nhập không khớp tổng các dòng.',
};

export function checkLabel(id:string,session:StudioSession):string {
  if(id==='schema')return 'Cấu trúc dữ liệu';if(id==='identity')return 'Định danh chứng từ';
  const parts=id.split(':');const rule=session.validationRules.find(r=>r.id===parts[0]);
  const number=(text:string|undefined)=>text&&/^\d{1,2}$/.test(text)&&Number(text)<20?Number(text)+1:null;
  if(rule){const row=number(parts[1]);return (row?`Dòng ${row}: `:'')+ruleText(rule,session);}
  if(parts[0]==='field') {const field=session.schemaDraft?.fields.find(f=>f.scope===parts[1]&&f.key===parts[2]&&!f.omit);if(field){const row=field.scope==='LineItem'?number(parts[3]):null;return (row?`Dòng ${row}: `:'')+field.label;}}
  return 'Kiểm tra giá trị';
}
