# Hệ Thống Tự Động Hóa Xuất Hóa Đơn (INV) & Đóng Gói (PKL) Giày Xuất Khẩu

Hệ thống web ứng dụng nội bộ chuẩn Enterprise B2B SaaS phục vụ tự động hóa toàn diện quy trình lập hóa đơn thương mại quốc tế (Commercial Invoice - INV) và bảng kê chi tiết đóng gói (Packing List - PKL) cho ngành sản xuất gia công giày dép xuất khẩu, thay thế hoàn toàn việc nhập liệu thủ công bằng Excel.

---

## 🌟 Tính Năng Nổi Bật

### 1. Quản lý Danh mục Hàng hóa (Master Data)
- Quản lý mã hình thể gốc (`StyleCode`), mô tả hải quan, đơn giá gia công CMT, đơn giá DAP, mã HS code (`64041990`), đơn vị tính và quy cách đóng gói (`PairPerCarton`).
- Hỗ trợ **nhập/xuất file Excel danh mục** hàng nghìn mã với cơ chế xác thực dữ liệu chặt chẽ và báo lỗi chi tiết theo từng dòng.
- Giao diện bảng phân trang, tìm kiếm thời gian thực, lọc và chỉnh sửa nhanh.

### 2. Lập Hóa Đơn & Phân Rã Đóng Gói Tự Động (Core Business Logic)
- **Tự động phân rã kiện hàng theo quy cách động**:
  - Lấy động số đôi/thùng (`PairPerCarton`) từ từng mã hàng (không hardcode 12 đôi/thùng).
  - Tự động chia **thùng chẵn** (`FullCartons = Quantity / pairsPerCarton`) và **thùng lẻ** (`Quantity % pairsPerCarton > 0`).
  - Đánh số dải kiện lũy kế liên tục trên toàn lô hàng (ví dụ: `1-3`, `4-339`, `340-340`...).
- **Tính toán trọng lượng thông minh**:
  - Trọng lượng Net: Tính theo tỷ lệ thùng thực tế `(Quantity / pairsPerCarton) * 3.2`.
  - Trọng lượng Gross: Quy tròn lên (`Math.Ceiling`) cộng thêm trọng lượng vỏ thùng carton (0.1 kg/thùng).
- **Hỗ trợ quy trình công nghệ**: Tự động nhận diện công đoạn **"Gò không may"** (`ProcessType = GoKhongMay`, gán hậu tố mã `.G`) và **"Thành hình"** (`Standard`).

### 3. Xuất File Excel 3 Sheet Hoàn Chỉnh từ Mẫu Thực Tế
- Mở trực tiếp file mẫu của công ty (`Templates/Shipment_Template.xlsx`), **bảo tồn 100% định dạng, độ rộng cột, font chữ, header thông tin doanh nghiệp và chân trang ký tên**.
- **Sheet "INV"**: Điền thông tin hóa đơn (J4, J5, J6), điều kiện giao nhận (E9, E10, I9, I10), bảng mặt hàng từ dòng 13, tính thành tiền và tổng cộng.
- **Sheet "PKL"**: Bảng phân rã đóng gói chi tiết từ dòng 12, giữ nguyên đường viền kẻ bảng và công thức tính.
- **Sheet danh mục ("Sheet2")**: Trích xuất bảng Master Data tương ứng cho đợt hàng xuất.

### 4. Nhập Liệu Siêu Tốc & Dán Nhanh từ Clipboard (Quick Paste)
- **AutoComplete thông minh**: Gợi ý mã có sẵn trong Master Data kèm tự động điền đơn giá và quy cách đóng thùng.
- **Phím tắt Enter**: Nhấn `Enter` ở ô số lượng để tự động thêm dòng mới và focus con trỏ tiếp tục gõ.
- **Dán nhanh từ Clipboard (Quick Paste)**: Hỗ trợ copy nguyên bảng từ Excel hoặc tin nhắn Zalo dán thẳng vào ứng dụng. Tự động bóc tách mã, số lượng và nhận diện công đoạn Gò không may.

### 5. Vision AI OCR Bóc Tách Ảnh Phiếu Kho (Image-to-Data)
- Tích hợp **Google Gemini Vision API (`gemini-1.5-flash`)** / **OpenAI GPT-4o-mini Vision**.
- **3 phương thức nạp ảnh**: Kéo thả ảnh, chọn file từ máy, hoặc **nhấn `Ctrl + V` dán trực tiếp ảnh chụp màn hình Zalo/Snipping Tool**.
- **Đối soát tổng số đôi tự động**:
  - So sánh tổng nhận diện (`CalculatedTotal`) với số ghi dưới đáy phiếu (`ReportedTotal`).
  - Badge xanh: `Khớp 100% số tổng phiếu kho (6,348 đôi)`.
  - Cảnh báo đỏ: Cảnh báo chi tiết số đôi chênh lệch để kiểm tra lại trước khi áp dụng.
- **Chế độ mô phỏng (Demo Fallback)**: Tự động chạy chế độ mô phỏng thông minh khi chưa có API Key, cho phép kiểm thử toàn diện quy trình mà không bị gián đoạn.

### 6. Quản Lý Lịch Sử Hóa Đơn & Tái Xuất
- Tự động lưu vết lô hàng vào cơ sở dữ liệu SQLite mỗi khi xuất file Excel.
- Tab **"Lịch sử Hóa đơn"**: Xem danh sách các lần xuất trước đây.
- Nút **"Tải lại Excel"**: Tải trực tiếp file Excel từ đơn hàng cũ.
- Nút **"Mở lại dữ liệu" (Load into Editor)**: Tải toàn bộ dữ liệu đơn hàng cũ lên lưới làm việc để chỉnh sửa hoặc tái xuất.

---

## 🏗️ Kiến Trúc Công Nghệ

| Thành phần | Công nghệ sử dụng |
| :--- | :--- |
| **Backend** | ASP.NET Core 8 Web API, Entity Framework Core 8, SQLite |
| **Xử lý Excel** | ClosedXML (OpenXML standard) |
| **Frontend** | React 18, TypeScript, Vite, Tailwind CSS, Ant Design 5 |
| **Thiết kế UI/UX** | Clean Slate Minimalist & Modern (Linear / Vercel design system) |
| **Thị giác AI / OCR** | Google Gemini Vision API / OpenAI GPT-4o-mini Vision |
| **Containerization** | Docker, Docker Compose, Nginx Alpine Reverse Proxy |

---

## 🚀 Hướng Dẫn Chạy Dự Án

### Cách 1: Chạy bằng Docker Compose (Khuyên dùng - 1 Lệnh Duy Nhất)

Yêu cầu: Máy tính đã cài đặt [Docker Desktop](https://www.docker.com/products/docker-desktop/).

1. Clone repository và mở thư mục dự án:
   ```bash
   git clone <repository-url>
   cd clever-faraday
   ```

2. Tạo file `.env` từ `.env.example` và cấu hình khóa JWT mạnh:
   - Khóa JWT là bắt buộc; API key OCR là tùy chọn:
     ```env
     JWT_KEY=replace-with-a-random-secret-at-least-32-bytes
     OPENAI_API_KEY=
     ```
   - Để tạo tài khoản quản trị lần đầu, bật `BOOTSTRAP_ADMIN_ENABLED=true`, đặt tên đăng nhập và mật khẩu mạnh trong `.env`, khởi động một lần rồi tắt lại.

3. Khởi chạy toàn bộ hệ thống:
   ```bash
   docker compose up -d --build
   ```

4. Truy cập hệ thống:
   - **Giao diện người dùng (Web App)**: [http://localhost:5173](http://localhost:5173)
   - **Backend API Swagger**: [http://localhost:5270/swagger](http://localhost:5270/swagger)

---

### Cách 2: Chạy Phát Triển Cục Bộ (Local Development)

#### Yêu cầu môi trường:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/) & npm

#### Bước 1: Chạy Backend API (.NET 8)
```bash
# Di chuyển vào thư mục backend
cd backend/ShoeExportInvoice.Api

# Khởi chạy server API (PowerShell, mặc định port 5270)
$env:Jwt__Key="replace-with-a-random-secret-at-least-32-bytes"
dotnet run --urls "http://localhost:5270"
```
*Cơ sở dữ liệu SQLite `shoe_export.db` sẽ được tự động migrate. Hệ thống không còn tạo tài khoản hoặc mật khẩu mặc định.*

#### Bước 2: Chạy Frontend (React + Vite)
Mở một terminal mới:
```bash
# Di chuyển vào thư mục frontend
cd frontend

# Cài đặt thư viện dependencies
npm install

# Khởi chạy máy chủ phát triển Vite
npm run dev
```
Mở trình duyệt tại [http://localhost:5173](http://localhost:5173).

---

## 🐳 Triển Khai Production Bằng Docker & Docker Compose

Hệ thống đã được đóng gói hoàn chỉnh bằng Docker với kiến trúc Multi-stage build siêu nhẹ, Nginx Reverse Proxy và Volume lưu trữ bền vững.

### 1. Chuẩn bị file biến môi trường (`.env`)
Sao chép từ file mẫu `.env.example` hoặc tạo file `.env` tại thư mục gốc:
```bash
cp .env.example .env
```
Nội dung file `.env`:
```env
# Port truy cập giao diện Web (Nginx Frontend)
WEB_PORT=80

# Port truy cập Backend API (Tùy chọn)
BACKEND_PORT=5270

# OpenAI API Key dùng cho tính năng Vision OCR
OPENAI_API_KEY=sk-proj-...

# Bắt buộc: bí mật ký JWT, tối thiểu 32 byte và không đưa vào Git
JWT_KEY=replace-with-a-random-secret-at-least-32-bytes

# Chỉ bật cho lần tạo quản trị viên đầu tiên, sau đó đổi thành false
BOOTSTRAP_ADMIN_ENABLED=true
BOOTSTRAP_ADMIN_USERNAME=admin
BOOTSTRAP_ADMIN_PASSWORD=replace-with-a-strong-unique-password
```

### 2. Khởi chạy toàn bộ hệ thống
Tại thư mục gốc dự án, chạy lệnh:
```bash
docker compose up -d --build
```

### 3. Kiểm tra trạng thái và logs
```bash
# Xem trạng thái containers
docker compose ps

# Xem logs thời gian thực
docker compose logs -f

# Xem riêng logs backend
docker compose logs -f backend
```

### 4. Dừng hoặc khởi động lại hệ thống
```bash
# Dừng các containers
docker compose down

# Khởi động lại
docker compose restart
```

### 5. Truy cập ứng dụng:
- **Giao diện Web**: [http://localhost](http://localhost) (hoặc `http://<IP_Server>:<WEB_PORT>`).
- **Dữ liệu SQLite**: Tự động lưu trữ bền vững tại thư mục `./data/shoe_export.db` trên máy host.
- **Mẫu Excel**: Có thể thay thế file template trực tiếp tại thư mục `./Templates/Shipment_Template.xlsx` mà không cần build lại container.

## 🔑 Cấu Hình OCR Vision API

Hệ thống hỗ trợ 2 cách cấu hình API Key:

### Cách 1: Cấu hình trong `appsettings.json`
Mở file `backend/ShoeExportInvoice.Api/appsettings.json`:
```json
{
  "OcrSettings": {
    "Provider": "Gemini",
    "GeminiApiKey": "AIzaSyYourGeminiApiKeyHere",
    "GeminiModel": "gemini-1.5-flash",
    "OpenAIApiKey": "",
    "OpenAIModel": "gpt-4o-mini"
  }
}
```

### Cách 2: Sử dụng biến môi trường (Environment Variable)
- Với Windows PowerShell:
  ```powershell
  $env:GEMINI_API_KEY="AIzaSyYourGeminiApiKeyHere"
  ```
- Với Docker Compose: đặt `GEMINI_API_KEY=...` trong file `.env`.

> **Mẹo lấy Google Gemini API Key miễn phí**:
> 1. Truy cập [Google AI Studio](https://aistudio.google.com/).
> 2. Đăng nhập tài khoản Google và bấm **"Get API key"** $\rightarrow$ **"Create API key"**.
> 3. Copy key và dán vào cấu hình trên.

---

## 📁 Cấu Trúc File Mẫu Excel (`Shipment_Template.xlsx`)

File mẫu được lưu trữ tại:
- `Templates/Shipment_Template.xlsx` (hoặc `Templates/KM3-26-DH233.xlsx`)
- `backend/ShoeExportInvoice.Api/Templates/Shipment_Template.xlsx`

### Quy định tọa độ ghi dữ liệu:
1. **Sheet "INV" (Commercial Invoice)**:
   - `J4`: Số hóa đơn (`InvoiceNo`)
   - `J5`: Ngày lập hóa đơn dạng text (vd: `SEP 09, 2026`)
   - `J6`: Số hợp đồng (`ContractNo`)
   - `E9`: Tên khách hàng (`CustomerName`)
   - `E10`: Địa chỉ giao hàng (`Address`)
   - `I9`: Điều kiện giao hàng (`DeliveryTerms`, vd: `DAP`)
   - `I10`: Điều kiện thanh toán (`PaymentTerms`, vd: `T/T`)
   - Dòng 13 trở đi: Chi tiết mặt hàng (STT, Mã đầy đủ, Đơn vị, Số lượng, Đơn giá CMT, Đơn giá DAP, Thành tiền).
2. **Sheet "PKL" (Packing List)**:
   - Dòng 12 trở đi: Phân rã kiện đóng gói (Dải số kiện, Phân loại, Mã hàng, Quy cách, Số kiện, Số đôi/kiện, Tổng số đôi, Net Weight, Gross Weight).
3. **Sheet "Sheet2"**: Danh mục Master Data tương ứng cho đợt xuất.

---

## 🧪 Chạy Kiểm Thử Tự Động (Unit Tests)

Dự án trang bị bộ test tự động kiểm tra toàn diện thuật toán chia kiện, công thức trọng lượng và bóc tách OCR:
```bash
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj
```
Kết quả kiểm thử: **4/4 Tests Passed 100%**.

---

## 📄 Bản Quyền & Giấy Phép
Phát triển bởi đội ngũ Kỹ thuật & Tự động hóa Doanh nghiệp. Dự án dành cho mục đích nội bộ quản lý xuất nhập khẩu.
