# 🗺️ ROADMAP: ĐIỀU PHỐI HÓA ĐƠN XNK (GOM ĐỢT & TÁCH HÓA ĐƠN TỰ ĐỘNG)

**Dự án:** ShoeDocX / XNK Office  
**Mục tiêu:** Đáp ứng Business Rule mới từ Ban Giám đốc — Tự động hóa hoàn toàn việc Gom nhiều đợt lẻ thành 1 Invoice và Tách các đợt lớn ra N Invoice độc lập, loại bỏ 100% việc tính toán thủ công trên Excel.

---

## I. TỔNG QUAN & BÀI TOÁN NGHIỆP VỤ (BUSINESS CONTEXT)

### 1. Nỗi đau thực tế (Pain Points)

- **Đợt hàng ít:** Các đợt giao số lượng ít (như Lần 23, Lần 24) cần gom lại thành 1 Invoice duy nhất để tiết kiệm chi phí mở tờ khai và phí chứng từ hải quan.
- **Đợt hàng lớn:** Các đợt giao số lượng quá nhiều (như Lần 19, Lần 20) bắt buộc phải xé nhỏ làm nhiều hóa đơn (ví dụ: tách làm 4 INV) để điều tiết tải trọng xe tải, công-ten-nơ hoặc giới hạn hạn ngạch.
- **Rủi ro hiện tại:** Nhân viên/Sếp đang phải mở Excel tính nhẩm, chia tay từng con số rồi mới nạp vào hệ thống. Việc này rất tốn thời gian, dễ gây lệch số tổng, vỡ quy cách đóng gói 12 đôi/thùng và đứt gãy dải số kiện trên Packing List.

### 2. Mục tiêu kỹ thuật (Technical Objectives)

1. **Gom đợt (Consolidate/Merge):**  
   Nạp nhiều phiếu kho cùng lúc → Hệ thống tự gom nhóm (`Group By`) theo Mã hình thể + Công đoạn (Thành hình / Gò), cộng dồn số lượng và xuất ra 1 file duy nhất.

2. **Tách đợt (Split N-Invoices):**  
   Nạp 1 đợt lớn → Chọn số lượng INV muốn tách (ví dụ: 4 INV) → Hệ thống mở Ma trận phân bổ:
   - **Tự động (Auto-balance):** Chia đều và tự làm tròn theo bội số 12 đôi (thùng nguyên).
   - **Tự do (Custom Allocation):** Cho phép nhập số lượng mong muốn vào từng cột INV.
   - **Chốt chặn an toàn (Validation Guard):**

     \[
     \sum(\text{Các hóa đơn con}) = \text{Tổng gốc}
     \]

     Nếu lệch dù chỉ 1 đôi, hệ thống khóa nút xuất để chống sai sót.

3. **Cấp số tự động (Sequence Engine):**  
   Tự động nhảy liên tục các số hóa đơn tịnh tiến (`DH233`, `DH234`, `DH235`, `DH236`...) và đóng gói trong 1 file `.zip`.

---

## II. LỘ TRÌNH TRIỂN KHAI THEO 4 GIAI ĐOẠN (4-PHASE ROADMAP)

```text
[Phase 1: Backend Core] ──► [Phase 2: Frontend UX] ──► [Phase 3: OCR & Rules] ──► [Phase 4: E2E Testing]

- DTOs Gom/Tách              - Ma trận phân bổ           - Quét nhiều ảnh         - Test Lần 19-20 (Split)
- Validation chốt chặn       - Thanh đối soát real-time - Tự bóc tách số liệu   - Test Lần 23-24 (Merge)
- Cấp số nhảy & Zip          - Modal điều phối          - Lưu vết liên kết      - Đóng gói Docker
```

---

### 📌 GIAI ĐOẠN 1 (PHASE 1): BACKEND DISPATCH SERVICE & DATA STRUCTURES

**Mục tiêu:** Xây dựng cấu trúc dữ liệu và logic xử lý Gom / Tách đơn hàng ở tầng Backend (.NET 8).

#### 1.1. DTOs & Models

- `MergeShipmentRequestDto`: Nhận danh sách các đợt giao kho cần gộp:

```csharp
public class MergeShipmentRequestDto
{
    public List<int> SourceBatchIds { get; set; } = new();
    public List<CreateShipmentItemDto> RawItems { get; set; } = new();
    public string PoSuffix { get; set; }
    public int? ContractFolderId { get; set; }
}
```

- `SplitShipmentRequestDto`: Nhận ma trận phân bổ chia lô hàng thành N đơn con:

```csharp
public class SplitShipmentRequestDto
{
    public int OriginalTotalQuantity { get; set; }
    public int StartInvoiceNumber { get; set; }
    public List<SubInvoiceDto> SubInvoices { get; set; } = new(); // Danh sách 2-10 hóa đơn con
}

public class SubInvoiceDto
{
    public string InvoiceSuffixTitle { get; set; } // vd: "INV 1", "INV 2"
    public List<CreateShipmentItemDto> Items { get; set; } = new();
}
```

#### 1.2. Logic Nghiệp vụ Core

- **Logic Gom (Merge Logic):**
  - Tự động chuẩn hóa mã gốc (xóa bỏ khoảng trắng, dấu ngoặc).
  - Nhóm `GroupBy(x => new { x.StyleCode, x.ProcessType })`.
  - Tính tổng: `Quantity = Group.Sum(x => x.Quantity)`.

- **Logic Tách (Split Logic & Chốt chặn):**
  - Viết hàm kiểm tra chốt chặn (`ValidateSplitIntegrity`):

    \[
    \sum_{i=1}^{N} \text{SubInvoice}[i].\text{Quantity}
    =
    \text{OriginalTotalQuantity}
    \]

  - Nếu sai số `!= 0`, trả về `400 Bad Request` kèm chi tiết số lượng chênh lệch.

- **Cấp số thứ tự liên tục & Xuất file Zip:**
  - Kế thừa `SequenceService.cs` để cấp N số hóa đơn liên tiếp.
  - Dùng ClosedXML tạo N file Excel `.xlsx` và nén vào một file `.zip` duy nhất thông qua `System.IO.Compression`.

---

### 📌 GIAI ĐOẠN 2 (PHASE 2): FRONTEND UX — MA TRẬN ĐIỀU PHỐI & PHÂN BỔ (SPLIT/MERGE UI)

**Mục tiêu:** Xây dựng giao diện trực quan, thân thiện cho nhân viên và Sếp thao tác nhanh bằng chuột/bàn phím.

#### 2.1. Nâng cấp Giao diện Lập Hóa Đơn (`ShipmentPage.tsx`)

- Thêm cụm nút tác vụ điều phối trên thanh công cụ:
  - 🔀 **"Tách thành nhiều Hóa đơn (Split)"**
  - 🔗 **"Gom nhiều đợt thành 1 (Merge)"**

#### 2.2. Modal Ma trận Phân bổ Tách đơn (`SplitMatrixModal.tsx`)

- **Bước 1: Chọn số lượng hóa đơn cần tách:**  
  Dropdown chọn `2`, `3`, `4` hoặc `N` hóa đơn (ví dụ chọn 4 theo yêu cầu sếp).

- **Bước 2: Bảng Ma trận (Grid Matrix):**

| **Mã hình thể** | **Tổng kho giao** | **INV 1** | **INV 2** | **INV 3** | **INV 4** | **Trạng thái** |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| **42072-410** | 5,316 | 1,332 | 1,332 | 1,332 | 1,320 | ✔ Đủ 100% |
| **42073-030** | 2,880 | 720 | 720 | 720 | 720 | ✔ Đủ 100% |

- **Tiện ích phân bổ:**
  - Nút **"Chia đều tự động (Ưu tiên tròn 12 đôi)"**: Hệ thống tự chia số nguyên và cân đối phần dư vào hóa đơn cuối cùng.
  - Ô nhập liệu trực tiếp: Cho phép Sếp/người dùng gõ số mong muốn cho từng INV.

- **Thanh Kiểm soát Real-time (Safety Bar):**
  - Hiển thị ngay chân bảng: `Đã phân bổ: 5,316 / 5,316 đôi (Khớp 100%)`.
  - Nếu thiếu/thừa: Hiện tag đỏ cảnh báo `Chưa phân bổ hết: Còn thiếu 24 đôi` và **Vô hiệu hóa nút Xuất**.

#### 2.3. Trải nghiệm Gom đợt (`MergeModal.tsx`)

- Cho phép tích chọn các đợt hàng trong danh sách hàng đợi.
- Hiển thị bảng xem trước (Preview) số liệu sau khi đã gộp và cộng dồn các mã trùng nhau.

---

### 📌 GIAI ĐOẠN 3 (PHASE 3): TÍCH HỢP OCR & TỰ ĐỘNG NHẬN DIỆN Ý ĐỊNH

**Mục tiêu:** Tận dụng tối đa Vision OCR hiện có để đọc ảnh phiếu kho và tự động đề xuất phương án Gom/Tách.

#### 3.1. Nhận diện Thông minh trên Modal Quét ảnh (`BatchOcrModal.tsx`)

- Quét cùng lúc nhiều ảnh (ví dụ: ảnh Lần 19, 20 hoặc Lần 23, 24):
  - **Tự động gợi ý Gom:** Nếu phát hiện các đợt có số lượng nhỏ (`< 1.500 đôi/đợt`), hiển thị nút gợi ý:  
    *"Phát hiện đợt hàng ít. Bấm vào đây để Gom thành 1 Invoice".*
  - **Tự động gợi ý Tách:** Nếu phát hiện đợt hàng lớn (`> 5.000 đôi`), hiển thị nút gợi ý:  
    *"Đợt hàng lớn (11,816 đôi). Bạn có muốn Tách làm 2, 3 hoặc 4 Invoice không?"*

#### 3.2. Lưu vết Quản trị (Traceability & Audit)

- Bổ sung trường `ParentBatchNumber` hoặc `ConsolidatedFrom` trong Database để khi kiểm tra sau thông quan, XNK luôn biết hóa đơn này được tách từ đợt kho nào hoặc gom từ những lần nào.

---

### 📌 GIAI ĐOẠN 4 (PHASE 4): KIỂM THỬ THỰC CHIẾN (E2E TESTING & VERIFICATION)

#### Kịch bản 1: Gom đợt lẻ (Test case Lần 23 + Lần 24)

- **Đầu vào:**
  - Lần 23 (14/9): `45428-2LX` (4,038 đôi - Gò), `42072-267` (936 đôi).
  - Lần 24 (17/9): `42072-410` (1,440 đôi), `42073-030` (24 đôi), `42072-267` (1,062 đôi).

- **Kỳ vọng:**
  - Mã trùng `42072-267` được tự động cộng dồn: `936 + 1,062 = 1,998 đôi`.
  - Tách nhánh đúng theo công đoạn: Hàng Gò (`45428-2LX.G`) và Hàng Thành hình riêng biệt.
  - Xuất ra file chuẩn, số lượng khớp 100%.

#### Kịch bản 2: Tách đợt lớn (Test case Lần 19 - Tách làm 4 INV)

- **Đầu vào:** Phiếu Lần 19 gồm 5 mã, tổng cộng 11,816 đôi.
- **Thao tác:** Chọn tách làm 4 hóa đơn, nhập số mong muốn hoặc bấm auto-balance.
- **Kỳ vọng:**
  - Cấp 4 số thứ tự hóa đơn liên tiếp: `KM3-26-DH233`, `DH234`, `DH235`, `DH236`.
  - Cả 4 file đều tính đúng thùng chẵn/lẻ (12 đôi/thùng), đúng Net/Gross Weight.
  - Tổng số đôi của 4 file cộng lại chính xác tuyệt đối bằng 11,816 đôi.
  - Tải về gói file `.zip` trong 1 click.

---

## III. BẢNG TIẾN ĐỘ THỰC HIỆN DỰ KIẾN

| **Hạng mục** | **Thời gian** | **Trạng thái** | **Đầu ra** |
| --- | --- | --- | --- |
| **Phase 1: Backend DTOs & Export Logic** | 1 ngày | Sẵn sàng | API `/api/shipments/split-export`, `/merge-export` |
| **Phase 2: Frontend Split/Merge Matrix UI** | 1 ngày | Sẵn sàng | Modal phân bổ ma trận, thanh đối soát realtime |
| **Phase 3: Tích hợp OCR & Nâng cấp Batch** | 0.5 ngày | Chờ duyệt | Gợi ý gom/tách thông minh trên hàng đợi OCR |
| **Phase 4: Kiểm thử E2E & Triển khai** | 0.5 ngày | Chờ duyệt | Bộ test case với dữ liệu thực tế Lần 19-24 |
