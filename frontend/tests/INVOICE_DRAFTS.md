# Kiểm thử INV đang soạn

Chạy `npm run test:drafts` trong frontend (môi trường kiểm tra: Node 24), sau đó `npm run build`.

Các kiểm thử tự động bao gồm bản chưa đủ dữ liệu, ba bản độc lập, tách tài khoản/backend, dữ liệu hỏng, bộ nhớ đầy, chuyển bản, xóa đúng bản, liên kết ID và số INV trả về mà giữ thay đổi mới, sao chép và kiểm tra phiên bản máy chủ trước khi sửa.

## Thử giao diện chính

1. Nhập thông tin đầu INV, dòng hàng và phân bổ size. Chuyển Master Data rồi quay lại, tải lại trang và đóng/mở tab. Kiểm tra đúng ngày, số INV, nội dung dòng và size.
2. Tạo ít nhất ba bản, chuyển qua lại bằng **INV đang soạn**. Mở hóa đơn trong Lịch sử và đổi đối tác; bản cũ phải còn trong danh sách.
3. Mở cùng bản ở hai tab. Tab sau chỉ xem. Sửa tab đầu rồi đóng; tab chờ phải đọc phiên bản mới nhất trước khi cho nhập.
4. Đăng xuất, đăng nhập cùng tài khoản để khôi phục. Tài khoản khác không thấy nháp đó.
5. Với hóa đơn đã lưu, sửa/khóa/thông quan trên máy chủ rồi mở lại nháp. Nháp phải được giữ, nhưng nút ghi đè bị chặn; sao chép phải bỏ ID hóa đơn cũ và yêu cầu số INV mới.
6. Lưu/xuất thất bại phải giữ nháp. Khi thành công, nhập thêm trong lúc chờ phản hồi phải được giữ; lưu lần sau cập nhật đúng ID đã tạo. Xuất hai hóa đơn giữ bản tổng ở chế độ xem để tránh tạo lại cả hai.

## Trang thử lỗi và phản hồi chậm

Trong chế độ phát triển, mở `/tests/invoiceDraftHarness.html`. Trang này dùng tài khoản nháp thử riêng, không gọi API tạo/cập nhật hóa đơn và không nằm trong bản build sản phẩm.

- Nhập số INV và 17 đôi, chọn **Bắt đầu lưu giả lập**, đổi thành 27 rồi chọn **Trả kết quả lưu thành công**. Tải lại phải có số `REAL-123`, ID 123 và 27 đôi.
- **Đổi tài khoản thử** phải cho bản riêng; chuyển lại phải khôi phục bản trước.
- **Tạo bản hỏng thử nghiệm** phải tăng số bản hỏng mà không che bản đọc được.
- **Giả lập bộ nhớ đầy**, sửa số đôi: trạng thái phải báo chưa tự lưu, không báo thành công. **Thử rời màn hình** rồi chọn **Ở lại** phải giữ dữ liệu.
- **Khôi phục bộ nhớ thử** phải lưu dữ liệu đang nhập và xóa cảnh báo lỗi; tải lại kiểm tra dữ liệu vừa khôi phục. Khôi phục bộ nhớ trước khi đóng trang thử.

Không xóa hoặc sửa hóa đơn thật để thử các tình huống trên. Bản đang soạn lưu tại trình duyệt hiện tại, không đồng bộ sang máy khác.
