import React from 'react';
import { Button, Result, Space, Typography } from 'antd';
import { CheckCircleFilled, DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import type { MasterDataFolder, OcrDetectedDocument } from '../../types';

const { Text } = Typography;

interface Props {
  selectedDocuments: OcrDetectedDocument[];
  partner: MasterDataFolder | null;
  exportedFileName?: string;
  documentCount?: number;
  totalQuantity: number;
  onClose: () => void;
  onScanNewImage: () => void;
}

export const OcrDoneStep: React.FC<Props> = ({
  selectedDocuments,
  partner,
  exportedFileName,
  documentCount = 1,
  totalQuantity,
  onClose,
  onScanNewImage,
}) => {
  return (
    <div className="py-8 px-4 max-w-2xl mx-auto text-center">
      <Result
        icon={<CheckCircleFilled className="text-emerald-500 text-6xl" />}
        status="success"
        title="Xuất chứng từ hóa đơn thành công!"
        subTitle="Toàn bộ chứng từ đã được khởi tạo, tính toán dải số kiện packing list và tải về máy của bạn."
        extra={[
          <Button key="close" type="primary" size="large" onClick={onClose}>
            Hoàn tất & Đóng
          </Button>,
          <Button
            key="new"
            size="large"
            icon={<ReloadOutlined />}
            onClick={onScanNewImage}
          >
            Quét ảnh mới
          </Button>,
        ]}
      />

      <div className="mt-4 bg-slate-50 border border-slate-200 rounded-lg p-4 text-left text-sm max-w-lg mx-auto space-y-2">
        <div className="flex justify-between">
          <Text type="secondary">Đối tác / Hợp đồng:</Text>
          <Text strong>{partner?.name || 'Không xác định'}</Text>
        </div>
        <div className="flex justify-between">
          <Text type="secondary">Đợt hàng nguồn:</Text>
          <Text strong>{selectedDocuments.map((d) => d.title).join(' + ')}</Text>
        </div>
        <div className="flex justify-between">
          <Text type="secondary">Tổng số lượng xuất:</Text>
          <Text strong className="text-blue-600">{totalQuantity.toLocaleString()} đôi</Text>
        </div>
        <div className="flex justify-between">
          <Text type="secondary">Số lượng file:</Text>
          <Text strong>{documentCount} file chứng từ</Text>
        </div>
        {exportedFileName && (
          <div className="flex justify-between border-t border-slate-200 pt-2 items-center">
            <Text type="secondary">Tên file đã tải về:</Text>
            <Space className="text-xs font-mono bg-white px-2 py-0.5 rounded border border-slate-200">
              <DownloadOutlined className="text-emerald-600" />
              <span>{exportedFileName}</span>
            </Space>
          </div>
        )}
      </div>
    </div>
  );
};
