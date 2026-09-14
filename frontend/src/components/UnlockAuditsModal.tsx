import React, { useState, useEffect } from 'react';
import { Modal, Table, Spin, Empty, Button } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { UserOutlined, ClockCircleOutlined, AuditOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { shipmentApi } from '../api/shipmentApi';
import type { ShipmentUnlockAudit } from '../types';

interface UnlockAuditsModalProps {
  open: boolean;
  onClose: () => void;
  orderId?: number | null;
  invoiceNo?: string;
}

export const UnlockAuditsModal: React.FC<UnlockAuditsModalProps> = ({
  open,
  onClose,
  orderId,
  invoiceNo,
}) => {
  const [loading, setLoading] = useState<boolean>(false);
  const [audits, setAudits] = useState<ShipmentUnlockAudit[]>([]);

  useEffect(() => {
    if (!open || !orderId) {
      setAudits([]);
      return;
    }

    setLoading(true);
    shipmentApi
      .getUnlockAudits(orderId)
      .then((data) => setAudits(data))
      .catch(() => setAudits([]))
      .finally(() => setLoading(false));
  }, [open, orderId]);

  const columns: ColumnsType<ShipmentUnlockAudit> = [
    {
      title: 'Thời gian mở khóa',
      dataIndex: 'unlockedAt',
      key: 'unlockedAt',
      width: 170,
      render: (val: string) => (
        <div className="flex items-center space-x-1 font-mono text-xs text-slate-700">
          <ClockCircleOutlined className="text-slate-400" />
          <span>{dayjs(val).format('DD/MM/YYYY HH:mm:ss')}</span>
        </div>
      ),
    },
    {
      title: 'Người mở khóa',
      dataIndex: 'unlockedByUserName',
      key: 'unlockedByUserName',
      width: 150,
      render: (val: string) => (
        <div className="flex items-center space-x-1 text-xs">
          <UserOutlined className="text-blue-500" />
          <span className="font-medium text-slate-800">{val || 'Hệ thống'}</span>
        </div>
      ),
    },
    {
      title: 'Lý do mở khóa (AMA / AMC)',
      dataIndex: 'reason',
      key: 'reason',
      render: (val: string) => (
        <div className="p-2 rounded bg-amber-50/60 border border-amber-200/80 text-xs text-amber-900 leading-relaxed font-sans">
          <span className="font-semibold text-amber-800 mr-1">📝 Lý do:</span>
          {val}
        </div>
      ),
    },
  ];

  return (
    <Modal
      title={
        <div className="flex items-center space-x-2 text-slate-800">
          <AuditOutlined className="text-blue-600" />
          <span>
            Nhật Ký Mở Khóa Đơn Hàng (Audit Log): <strong className="text-blue-700">{invoiceNo}</strong>
          </span>
        </div>
      }
      open={open}
      onCancel={onClose}
      width={780}
      footer={[
        <Button key="close" type="primary" onClick={onClose}>
          Đóng
        </Button>,
      ]}
    >
      <div className="py-2">
        <div className="text-xs text-slate-500 mb-3">
          Toàn bộ lịch sử can thiệp mở khóa đơn hàng đã thông quan để khai bổ sung sau thông quan (AMA/AMC) được ghi nhận bảo toàn tính toàn vẹn dữ liệu.
        </div>

        {loading ? (
          <div className="py-12 flex justify-center">
            <Spin tip="Đang tải lịch sử mở khóa..." />
          </div>
        ) : audits.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description="Chưa có bản ghi mở khóa nào cho đơn hàng này."
          />
        ) : (
          <Table
            dataSource={audits}
            columns={columns}
            rowKey="id"
            pagination={false}
            size="small"
            bordered
          />
        )}
      </div>
    </Modal>
  );
};
