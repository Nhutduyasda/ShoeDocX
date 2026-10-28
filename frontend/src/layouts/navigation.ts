import {
  getAllowedTabsForUser,
  type NavTabKey,
  type User,
} from "../types/auth";
export const destinations: {
  key: NavTabKey;
  label: string;
  group: string;
  description: string;
}[] = [
  {
    key: "overview",
    label: "Tổng quan",
    group: "Không gian làm việc",
    description: "Chứng từ gần đây và hoạt động xuất hàng",
  },
  {
    key: "shipment",
    label: "Invoice & Packing List",
    group: "Chứng từ & xuất hàng",
    description: "Lập, kiểm tra và xuất bộ chứng từ",
  },
  {
    key: "history",
    label: "Lịch sử chứng từ",
    group: "Chứng từ & xuất hàng",
    description: "Tìm, mở lại và tải chứng từ đã lưu",
  },
  {
    key: "ocr",
    label: "Quét phiếu kho",
    group: "Chứng từ & xuất hàng",
    description: "Nhập và rà soát dữ liệu từ phiếu kho",
  },
  {
    key: "warehouse",
    label: "Kho thành phẩm",
    group: "Kho & sản xuất",
    description: "Fast-Grid và giao nhận thành phẩm",
  },
  {
    key: "bom",
    label: "Tính BOM theo size",
    group: "Kho & sản xuất",
    description: "Bóc tách nhu cầu nguyên phụ liệu",
  },
  {
    key: "material-planning",
    label: "Kế hoạch vật tư",
    group: "Kho & sản xuất",
    description: "Lập kế hoạch, duyệt và xuất nguyên phụ liệu",
  },
  {
    key: "products",
    label: "Product Master",
    group: "Dữ liệu & kế toán",
    description: "Mã hàng, quy cách và hợp đồng",
  },
  {
    key: "materials",
    label: "Danh mục vật tư",
    group: "Dữ liệu & kế toán",
    description: "Quản lý nguyên phụ liệu sản xuất",
  },
  {
    key: "bom-master",
    label: "Định mức kỹ thuật",
    group: "Dữ liệu & kế toán",
    description: "Quản lý BOM và định mức",
  },
  {
    key: "settlement",
    label: "Quyết toán hải quan",
    group: "Dữ liệu & kế toán",
    description: "Đối chiếu nội bộ và báo cáo quyết toán",
  },
];
export function navigationFor(user: User | null) {
  const allowed = getAllowedTabsForUser(user);
  return destinations.filter((item) => allowed.includes(item.key));
}
