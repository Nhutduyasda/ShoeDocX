# Hướng dẫn thử nghiệm Trợ lý Excel văn phòng

**INTERNAL PILOT NOT YET EXECUTED.** Chưa có URL hoặc tài khoản được cấp cho người thật.
Tài liệu này chuẩn bị cho 2–3 người được chủ dự án cho phép và một quản trị viên/phê duyệt.
Không gửi lời mời, tạo tài khoản người thật hoặc dùng tài liệu công ty theo tài liệu này.

## Đăng nhập lần đầu

Chỉ dùng địa chỉ HTTPS và tài khoản do người phụ trách cấp qua kênh riêng sau khi được
phê duyệt. Không bỏ qua cảnh báo certificate. Không gửi mật khẩu/token qua phản hồi.
Đăng xuất sau khi làm việc. Nếu bị khóa tài khoản, liên hệ người phụ trách; không thử
liên tục. Việc khôi phục backend có thể yêu cầu đăng nhập lại.

## Chuẩn bị mẫu

Dùng XLSX giả lập có cấu trúc rõ ràng: thông tin đầu phiếu, một bảng dòng và footer.
Giữ bản gốc riêng. Tối đa ba sheet dữ liệu được ánh xạ, bốn sheet Studio, 20 dòng xuất,
năm dòng ví dụ đã điền và tám quy tắc. Sheet tham chiếu cần giữ nguyên. Không dùng OCR,
PDF, macro, công thức/cấu trúc ngoài hợp đồng hỗ trợ. Sản phẩm không hiểu mọi Excel.

## Một lượt làm việc

1. Mở Trợ lý Excel văn phòng, tải mẫu trống; có thể thêm một ví dụ đã điền hợp lệ.
2. Xem từng mục dữ liệu. Nếu có nhiều vị trí, chọn đúng vị trí hoặc bỏ qua mục không dùng.
   Chọn kiểu số/ngày/văn bản và mục bắt buộc, rồi áp dụng các mục.
3. Ánh xạ vị trí: mục đầu phiếu tới ô, mục chi tiết tới cột; chọn đúng sheet và dòng bắt đầu.
   Với nhiều sheet, kiểm tra từng sheet theo cùng thứ tự trường rồi áp dụng.
4. Thêm quy tắc, ví dụ Số lượng × Đơn giá = Thành tiền. Áp dụng quy tắc, xác nhận vị trí.
5. Nhập dữ liệu giả lập, giữ mã như 00123 dưới dạng văn bản; dùng dấu chấm cho số thập phân.
   Gửi dữ liệu để kiểm tra. Thay đổi chưa áp dụng phải được lưu vào trạng thái trước khi đi tiếp.
6. Sửa lỗi bắt buộc, sai kiểu hoặc sai phép tính rồi kiểm tra lại. Không sửa mẫu để né lỗi.
7. Người có quyền rà soát hoàn tất rà soát; quản trị viên phê duyệt trạng thái đã kiểm tra.
   Phê duyệt không thay thế việc xác nhận vị trí hoặc kiểm tra dữ liệu.
8. Tải Excel đã phê duyệt. Mở lại, kiểm tra mã, số liệu, công thức footer, định dạng, merge
   và thứ tự sheet. Không gửi tệp thử nghiệm cho khách hàng.

## Lưu và mở lại bản nháp

Áp dụng mọi thay đổi rồi chọn Lưu bản nháp. Kiểm tra thông báo đã lưu thành công; reload
không có nghĩa thay đổi chưa lưu đã tồn tại. Bản nháp mã hóa thuộc đúng người và đơn vị,
hết hạn bảy ngày từ lúc tạo. Sau restart, mở lại bản nháp, kiểm tra ánh xạ, xác nhận,
rá soát và phê duyệt lại; phê duyệt cũ không được giữ. Xóa bản nháp khi không dùng.

## Lỗi thường gặp và FAQ

- Không có nút tải: kiểm tra bước xác nhận vị trí, lỗi dữ liệu và quyền phê duyệt.
- Không lưu được: có thay đổi chưa áp dụng, phiên hết hạn, mất kết nối hoặc xung đột
  phiên bản; không nhấn lặp liên tục. Tải lại trạng thái và kiểm tra trước khi sửa tiếp.
- Mẫu không hỗ trợ: dùng mẫu đơn giản nằm trong phạm vi, giữ thông báo lỗi và báo người
  phụ trách; không kỳ vọng OCR/AI tự sửa mẫu.
- Bản nháp không hiện: kiểm tra tài khoản, đơn vị và hạn bảy ngày. Không yêu cầu mở quyền
  xem bản nháp của người khác để xử lý.
- Di động: cuộn vùng bảng ngang, dùng bàn phím chuyển ô; kiểm tra toàn bộ 20 dòng.
- Có được dùng dữ liệu thật không? Chưa; pilot này chỉ cho dữ liệu giả lập.

## Checklist người tham gia — để trống đến khi thực hiện thật

Mỗi người thử hóa đơn và phiếu nhập/xuất kho, ít nhất một tài liệu nhiều sheet; cố ý tạo
lỗi phép tính rồi sửa, lưu/mở nháp, tải và kiểm tra XLSX, thử mẫu không hỗ trợ. Ghi có cần
lập trình viên trợ giúp không, thuật ngữ nào khó hiểu và bước nào không tự hoàn thành.
Người rà soát/quản trị kiểm tra phân quyền; người dùng không thử tài khoản thật của nhau.

Tiêu chí đạt: hoàn thành không cần developer, hiểu mapping/lỗi/hạn chế, khôi phục nháp,
kết quả đúng, không có truy cập trái phép và lỗi vận hành xử lý được. Chưa có số liệu,
phản hồi, người tham gia hoặc tỷ lệ thành công thực tế. Không điền thay người dùng.

## Riêng tư và xóa dữ liệu

Chỉ sử dụng danh tính/tài liệu giả lập, không tải dữ liệu cá nhân hoặc bí mật công ty.
Bản nháp được mã hóa trong SQL và hết hạn bảy ngày; backup có chính sách giữ riêng và
không tự mất ngay khi xóa một bản nháp. Người phụ trách phải chốt thời hạn backup, quyền
truy cập và quy trình xóa trước pilot thật. Không hứa xóa tức thì khỏi mọi backup.
