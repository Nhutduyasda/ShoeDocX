import React, { useState } from 'react';
import { Modal, Alert, Typography, Space, Button, Input } from 'antd';
import { ExclamationCircleOutlined, CheckOutlined } from '@ant-design/icons';
import type { OcrDetectedDocument } from '../../types';

const { Text } = Typography;

interface Props {
  open: boolean;
  document: OcrDetectedDocument | null;
  onCancel: () => void;
  onConfirm: (reason: string) => Promise<void> | void;
  loading?: boolean;
}

export const ManualOcrConfirmationModal: React.FC<Props> = ({
  open,
  document,
  onCancel,
  onConfirm,
  loading = false,
}) => {
  const [reason, setReason] = useState<string>('Đã đối chiếu ảnh gốc, OCR đọc nhầm ghi chú thành tổng phiếu.');

  if (!document) return null;

  const diff = document.reportedTotal !== null && document.reportedTotal !== undefined
    ? document.calculatedTotal - document.reportedTotal
    : 0;

  const handleConfirm = () => {
    onConfirm(reason);
  };

  return (
    <Modal
      title={
        <Space className="text-amber-600">
          <ExclamationCircleOutlined className="text-lg" />
          <span>Xác nhận dữ liệu OCR đúng (Đối soát thủ công)</span>
        </Space>
      }
      open={open}
      onCancel={onCancel}
      footer={[
        <Button key="cancel" onClick={onCancel} disabled={loading}>
          Hủy
        </Button>,
        <Button
          key="confirm"
          type="primary"
          icon={<CheckOutlined />}
          loading={loading}
          onClick={handleConfirm}
          className="bg-amber-600 hover:bg-amber-500 border-amber-600"
        >
          Xác nhận đã kiểm tra
        </Button>,
      ]}
      width={560}
      destroyOnClose
    >
      <div className="space-y-4 py-2">
        <Alert
          type="warning"
          showIcon
          message="Bạn đang thực hiện xác nhận thủ công"
          description={
            <ul className="list-disc pl-4 text-xs space-y-1 mt-1">
              <li>Bạn đã đối chiếu kỹ lưỡng với ảnh phiếu kho thực tế.</li>
              <li>Danh sách mã hàng và số lượng từng dòng đã được kiểm tra chính xác.</li>
              <li>Phần chênh lệch tổng có thể do OCR đọc nhầm ghi chú hoặc số phụ trên ảnh.</li>
            </ul>
          }
        />

        <div className="rounded-lg bg-slate-50 p-3.5 border border-slate-200 text-sm space-y-2">
          <div className="flex justify-between">
            <Text type="secondary">Đợt hàng / Phiếu:</Text>
            <Text strong>{document.title || 'Không tiêu đề'}</Text>
          </div>
          <div className="flex justify-between">
            <Text type="secondary">Tổng số lượng các dòng OCR:</Text>
            <Text strong className="text-blue-600">{document.calculatedTotal.toLocaleString()} đôi</Text>
          </div>
          <div className="flex justify-between">
            <Text type="secondary">Tổng OCR đọc từ phiếu:</Text>
            <Text strong className="text-slate-700">
              {document.reportedTotal !== null && document.reportedTotal !== undefined
                ? `${document.reportedTotal.toLocaleString()} đôi`
                : 'Không đọc được'}
            </Text>
          </div>
          <div className="flex justify-between border-t border-slate-200 pt-2 font-semibold">
            <span>Chênh lệch:</span>
            <span className={diff > 0 ? 'text-amber-600' : 'text-red-600'}>
              {diff > 0 ? `+${diff.toLocaleString()}` : diff.toLocaleString()} đôi
            </span>
          </div>
        </div>

        <div>
          <Text className="text-xs text-slate-600 mb-1 block">Ghi chú xác nhận (Lưu vào nhật ký hệ thống):</Text>
          <Input.TextArea
            rows={2}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Lý do xác nhận đợt hàng này..."
          />
        </div>
      </div>
    </Modal>
  );
};
