import React from 'react';
import { Modal, Button, Tag } from 'antd';
import {
  CameraOutlined,
  CheckCircleFilled,
  SwapOutlined,
  DownloadOutlined,
  RocketOutlined,
  ThunderboltOutlined,
  SafetyCertificateOutlined,
} from '@ant-design/icons';

interface CheatsheetModalProps {
  open: boolean;
  onClose: () => void;
  onStartTour: () => void;
}

export const CheatsheetModal: React.FC<CheatsheetModalProps> = ({
  open,
  onClose,
  onStartTour,
}) => {
  const steps = [
    {
      step: 1,
      badge: 'Siêu tốc',
      badgeColor: 'blue',
      icon: <CameraOutlined className="text-xl text-blue-600" />,
      title: 'Nhập Số liệu Kho',
      subtitle: 'Không cần gõ tay thủ công',
      desc: 'Chụp ảnh phiếu kho từ Zalo rồi bấm Ctrl + V vào màn hình, hoặc bấm [Dán nhanh] để dán trực tiếp dữ liệu từ file Excel của phân xưởng.',
      highlight: 'Ctrl + V / Dán nhanh',
    },
    {
      step: 2,
      badge: 'Chính xác',
      badgeColor: 'emerald',
      icon: <CheckCircleFilled className="text-xl text-emerald-600" />,
      title: 'Đối soát & Tra giá',
      subtitle: 'Tự động ráp đơn giá',
      desc: 'Hệ thống tự đối chiếu mã hàng và ráp giá CMT/DAP từ Master Data. Nhìn nhãn [Khớp phiếu kho] để chắc chắn số lượng không bị lệch.',
      highlight: 'Nhãn [Khớp phiếu kho]',
    },
    {
      step: 3,
      badge: 'Linh hoạt',
      badgeColor: 'purple',
      icon: <SwapOutlined className="text-xl text-purple-600" />,
      title: 'Tách 2 File nếu có Gò',
      subtitle: 'Tùy chọn thứ tự ưu tiên',
      desc: 'Nếu đơn có cả hàng Thành hình và Gò không may (.G), hệ thống mở modal cho bạn chọn xuất Thành hình trước hay Gò trước theo đợt.',
      highlight: 'Thành hình vs Gò (.G)',
    },
    {
      step: 4,
      badge: 'Hoàn tất',
      badgeColor: 'amber',
      icon: <DownloadOutlined className="text-xl text-amber-600" />,
      title: 'Xuất Excel & Lưu đơn',
      subtitle: 'Chuẩn mẫu nhà máy',
      desc: 'Bấm [Xuất File Excel & Lưu đơn] để tải về file Excel (.xlsx) đa sheet hoặc file ZIP. Đơn hàng tự động được lưu vào Lịch sử chứng từ.',
      highlight: 'Tự động lưu vào Lịch sử',
    },
  ];

  return (
    <Modal
      title={
        <div className="flex items-center space-x-2 text-slate-900 pb-1">
          <RocketOutlined className="text-blue-600 text-lg" />
          <span className="font-semibold text-base">
            Quy trình 4 Bước Xuất Hóa đơn & Packing List (INV & PKL)
          </span>
        </div>
      }
      open={open}
      onCancel={onClose}
      width={940}
      footer={[
        <Button key="close" onClick={onClose} className="text-xs h-8 px-4">
          Đóng
        </Button>,
        <Button
          key="tour"
          type="primary"
          icon={<ThunderboltOutlined />}
          onClick={() => {
            onClose();
            onStartTour();
          }}
          className="bg-blue-600 hover:bg-blue-700 text-white text-xs h-8 px-4 font-medium"
        >
          Bắt đầu Tour Hướng dẫn Tương tác ➔
        </Button>,
      ]}
    >
      <div className="py-3 space-y-4">
        {/* Banner giới thiệu */}
        <div className="bg-blue-50/80 border border-blue-200 rounded-lg p-3 flex items-center justify-between">
          <div className="flex items-center space-x-2.5">
            <SafetyCertificateOutlined className="text-blue-600 text-lg shrink-0" />
            <div className="text-xs text-blue-900">
              <span className="font-semibold">Hệ thống Xuất Nhập Khẩu ShoeDocX</span> giúp loại bỏ thao tác thủ công, tự động hóa tính kiện PKL và bảo đảm số liệu chuẩn xác 100%.
            </div>
          </div>
          <span className="text-[11px] font-mono text-blue-600 font-medium px-2 py-0.5 rounded bg-blue-100/60 border border-blue-200 shrink-0">
            Chuẩn Kingmaker III
          </span>
        </div>

        {/* 4 thẻ quy trình dạng thẻ ngang */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
          {steps.map((s) => (
            <div
              key={s.step}
              className="bg-white border border-slate-200 rounded-xl p-3.5 flex flex-col justify-between hover:border-blue-400 hover:shadow-md transition-all relative overflow-hidden group"
            >
              {/* Step indicator strip */}
              <div className="flex items-center justify-between mb-2">
                <span className="w-6 h-6 rounded-full bg-slate-100 text-slate-700 flex items-center justify-center font-bold text-xs font-mono border border-slate-200">
                  {s.step}
                </span>
                <Tag
                  color={s.badgeColor}
                  className="text-[10px] py-0 px-1.5 m-0 font-medium rounded"
                >
                  {s.badge}
                </Tag>
              </div>

              {/* Icon & Title */}
              <div className="space-y-1 my-1">
                <div className="w-9 h-9 rounded-lg bg-slate-50 border border-slate-100 flex items-center justify-center mb-2 group-hover:scale-105 transition-transform">
                  {s.icon}
                </div>
                <div className="font-semibold text-slate-900 text-xs tracking-tight">
                  {s.title}
                </div>
                <div className="text-[11px] text-blue-600 font-medium">
                  {s.subtitle}
                </div>
              </div>

              {/* Description */}
              <p className="text-[11px] text-slate-500 leading-relaxed mt-2 mb-2 flex-grow">
                {s.desc}
              </p>

              {/* Highlight Tag */}
              <div className="pt-2 border-t border-slate-100">
                <span className="inline-block text-[10px] font-mono font-medium px-1.5 py-0.5 rounded bg-slate-50 text-slate-600 border border-slate-200">
                  💡 {s.highlight}
                </span>
              </div>
            </div>
          ))}
        </div>

        {/* Phím tắt hữu ích */}
        <div className="bg-slate-50 border border-slate-200 rounded-lg p-3 text-xs text-slate-600 flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center space-x-2">
            <span className="font-semibold text-slate-800">⌨️ Phím tắt nhanh:</span>
            <span>
              <kbd className="px-1.5 py-0.5 bg-white border border-slate-200 rounded font-mono text-[11px] shadow-sm">Ctrl + V</kbd> dán ảnh/bảng
            </span>
            <span className="text-slate-300">•</span>
            <span>
              <kbd className="px-1.5 py-0.5 bg-white border border-slate-200 rounded font-mono text-[11px] shadow-sm">Enter</kbd> tại ô số lượng cuối để thêm dòng
            </span>
          </div>
          <span className="text-[11px] text-slate-400">
            Bạn có thể bấm &quot;Hướng dẫn nhanh&quot; ở góc trên bất cứ khi nào cần trợ giúp.
          </span>
        </div>
      </div>
    </Modal>
  );
};
