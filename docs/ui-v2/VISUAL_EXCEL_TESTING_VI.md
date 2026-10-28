# Hướng dẫn thử Visual Excel Studio tại máy

1. Đăng nhập bằng tài khoản đã được cấp quyền Office Studio. Mở Trợ lý Excel văn phòng. Dùng tệp tổng hợp, không dùng dữ liệu khách hàng khi chụp ảnh kiểm thử.
2. Tải mẫu XLSX bằng “Tải và phân tích mẫu”. Chờ thông báo máy chủ đã phân tích. Bấm “Xem cấu trúc Excel”. Chọn tệp chưa tải phải tiếp tục chặn chuyển bước và đưa tiêu điểm về nút tải.
3. Trong bước 2, chọn một thẻ mục, rồi chọn ô có sẵn trong mẫu. Với cột chi tiết, chọn ô ở đúng dòng mẫu. Đổi kiểu hoặc đánh dấu bắt buộc trong khung bên phải. Xem bằng chứng để xử lý các mục có nhiều gợi ý. Có thể mở “Chỉnh sửa chi tiết các mục và gợi ý” để đổi tên/phạm vi hoặc bỏ mục tùy chọn.
4. Bấm “Áp dụng các mục dữ liệu” và chờ phản hồi. Sang bước 3; chọn từng mục và ô/cột trên sheet tương ứng. Mỗi sheet chỉ dùng một dòng mẫu, tối đa ba sheet dữ liệu. Mở phần quản lý sheet để xóa/chỉnh vị trí chi tiết. Màu vàng là bản sửa cục bộ; màu xanh là vị trí máy chủ đã áp dụng. Xác nhận vị trí là thao tác riêng sau Apply.
5. Chỉ dùng bàn phím: Tab đến thẻ mục, Enter để chọn; Tab đến ô, dùng mũi tên, Enter để gán. Dùng “Đi đến ô” để đến địa chỉ có thật trên sheet. Tệp dài chia thành từng cửa sổ 40 dòng.
6. Trong bước 4, nhập thông tin đầu phiếu và bảng chi tiết. Dán bảng dùng tab/xuống dòng. Mã như 00123 phải ở kiểu Văn bản. Số dùng dấu chấm, ngày theo năm-tháng-ngày; Có/Không dùng true/false khi dán. Tối đa 20 dòng. Tab chuyển ô, Enter hoặc lên/xuống chuyển dòng; Alt+trái/phải chuyển cột. “Hoàn tác thay đổi” bỏ bản sửa; không có tự lưu ngầm.
7. Bấm “Gửi dữ liệu để kiểm tra”. Đọc lỗi do máy chủ trả; bấm “Sửa…” để đến đúng ô nếu có thông tin vị trí. Sửa và gửi lại. Không thể đi tiếp khi còn bản sửa chưa áp dụng.
8. Lưu bản nháp chỉ khi đã áp dụng. “Đã lưu” phải có xác nhận máy chủ. Sau mở lại hoặc khởi động lại, chọn lại đúng tệp ở “Xem lại mẫu gốc” để xem ô; không cần tải lại mẫu lên máy chủ. Mẫu khác dấu vân tay bị từ chối. Bản nháp giữ tối đa bảy ngày, phiên hai mươi phút; phải xác nhận/rà soát/phê duyệt lại.
9. Bước 5 rà soát rồi phê duyệt với tài khoản đủ quyền. Bước 6 tải XLSX; mở lại bằng Excel hoặc openpyxl và kiểm tra sheet, mã 0 đầu, số, số dòng, ô gộp và công thức được giữ theo phạm vi máy chủ.
10. Thử ở 1440, 1366, 1024, 390 và 360 px. Trang không được tràn ngang; vùng bảng tự cuộn. Bản xem chỉ hiển thị giá trị gốc và tọa độ, không mô phỏng định dạng/in hoặc chạy công thức.

## Lệnh kiểm thử cho người phát triển

Trong `frontend`: `npm ci`, `npm run build`, `npm audit --audit-level=low`, `node --test tests/officeModel.test.mjs tests/uiNavigation.test.mjs tests/selectorBackport.test.mjs tests/invoiceDraftStore.test.mjs`.

Bản xem và kiểm tra ZIP không cần đăng nhập: `npx playwright test --config playwright.ui-v2.config.ts workbook-reader.spec.ts`.

Kiểm tra thực tế cần SQL thử riêng và Python có openpyxl: đặt `GATE5C_SQL` thành kết nối SQL được phép tạo/xóa database tổng hợp; chạy `python .github/scripts/verify_ui_v2_legacy.py`. Script tự tạo người dùng, đăng nhập Identity/CSRF thật và xóa database mới của chính lần chạy. Không trỏ script vào database dữ liệu đang sử dụng. Để chạy riêng luồng mới, đặt `UI_V2_TEST_FILE=visual-excel.spec.ts`.

Để kiểm tra khởi động lại: đặt `STUDIO_PLAYWRIGHT_MODULE` đến `frontend/node_modules/playwright`, chọn `STUDIO_BROWSER_CHANNEL=msedge` trên Windows nếu cần, rồi chạy `python .github/scripts/verify_ui_v2_office.py`. Hai script không chạy đồng thời vì dùng cổng cố định. CI thực hiện các bước tương đương trên Windows và Ubuntu; không tính các test bị skip vì thiếu tài khoản là PASS chức năng.
