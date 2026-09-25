import React from 'react';
import { Alert, Button, Card, Table, Typography } from 'antd';
import {
  ExclamationCircleOutlined,
  ReloadOutlined,
  NodeIndexOutlined,
} from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import type { MergeShipmentPreviewResponse } from '../../features/shipment-dispatch/types/shipmentDispatch';
import type { CreateShipmentItem } from '../../types';
import { ProcessType } from '../../types';

const { Text } = Typography;

interface Props {
  mergePreview: MergeShipmentPreviewResponse | null;
  isStale: boolean;
  selectedDocCount: number;
  localSelectedTotal: number;
  onRecalculateMerge: () => void;
  onOpenConsolidatedSplit: () => void;
  isLoading: boolean;
}

export const OcrResultStep: React.FC<Props> = ({
  mergePreview,
  isStale,
  selectedDocCount,
  localSelectedTotal,
  onRecalculateMerge,
  onOpenConsolidatedSplit,
  isLoading,
}) => {
  if (!mergePreview) {
    return (
      <div className="p-8 text-center">
        <Alert
          message="Chưa có kết quả tính toán"
          description="Vui lòng quay lại Bước 2 (Điều phối) và chọn một phương án xử lý."
          type="info"
          showIcon
        />
      </div>
    );
  }

  const columns: ColumnsType<CreateShipmentItem> = [
    {
      title: 'Mã hình thể',
      dataIndex: 'styleCode',
      render: (text) => <Text strong>{text}</Text>,
    },
    {
      title: 'Công đoạn',
      dataIndex: 'processType',
      render: (val) => (val === ProcessType.GoKhongMay ? 'Gò không may' : 'Thành hình'),
    },
    {
      title: 'SL sau gom',
      dataIndex: 'quantity',
      align: 'right',
      render: (val: number) => <Text className="font-semibold text-blue-600">{val.toLocaleString()}</Text>,
    },
    {
      title: 'Đôi/thùng',
      dataIndex: 'pairPerCarton',
      align: 'right',
      render: (val: number) => val || 12,
    },
    {
      title: 'Đơn giá CMT',
      dataIndex: 'unitPriceCMT',
      align: 'right',
      render: (val: number) => (val ? `$${val.toFixed(2)}` : '-'),
    },
    {
      title: 'Đơn giá DAP',
      dataIndex: 'unitPriceDAP',
      align: 'right',
      render: (val: number) => (val ? `$${val.toFixed(2)}` : '-'),
    },
  ];

  const totalQtyMismatch = localSelectedTotal !== mergePreview.totalQuantity;

  return (
    <div className="p-6 space-y-4 max-w-5xl mx-auto">
      <div className="border-b border-slate-200 pb-3 flex justify-between items-center">
        <div>
          <h3 className="text-base font-semibold text-slate-800 m-0">Bước 3: Xem trước kết quả gom đợt</h3>
          <p className="text-xs text-slate-500 m-0 mt-1">
            Tổng hợp dữ liệu từ {selectedDocCount} đợt hàng đã chọn để chuẩn bị xuất chứng từ.
          </p>
        </div>

        {mergePreview.isExportable && !isStale && !totalQtyMismatch && (
          <Button
            size="small"
            icon={<NodeIndexOutlined />}
            onClick={onOpenConsolidatedSplit}
          >
            Tách kết quả gom thành nhiều INV
          </Button>
        )}
      </div>

      {/* STALE BANNER */}
      {isStale && (
        <Alert
          type="warning"
          showIcon
          icon={<ExclamationCircleOutlined />}
          message="Kết quả đã cũ vì dữ liệu nguồn vừa thay đổi"
          description="Bạn đã chỉnh sửa số lượng hoặc thông tin đợt hàng ở Bước 1. Vui lòng bấm 'Cập nhật lại kết quả' để tính toán lại trước khi tiếp tục."
          action={
            <Button
              type="primary"
              size="small"
              icon={<ReloadOutlined />}
              loading={isLoading}
              onClick={onRecalculateMerge}
              className="bg-amber-600 hover:bg-amber-500 border-amber-600"
            >
              Cập nhật lại kết quả
            </Button>
          }
        />
      )}

      {/* TOTAL MISMATCH ERROR */}
      {totalQtyMismatch && !isStale && (
        <Alert
          type="error"
          showIcon
          message="Không khớp tổng số lượng"
          description={`Tổng số lượng local (${localSelectedTotal.toLocaleString()} đôi) khác với kết quả backend (${mergePreview.totalQuantity.toLocaleString()} đôi). Vui lòng tính lại.`}
          action={
            <Button size="small" onClick={onRecalculateMerge}>
              Tính lại
            </Button>
          }
        />
      )}

      {/* BLOCKING ERRORS */}
      {mergePreview.blockingErrors.map((err, i) => (
        <Alert
          key={i}
          type="error"
          showIcon
          message={err.message}
        />
      ))}

      {/* WARNINGS */}
      {mergePreview.warnings.map((w, i) => (
        <Alert
          key={i}
          type="warning"
          showIcon
          message={w.message}
        />
      ))}

      {/* SUMMARY METRICS */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <Card size="small" className="bg-slate-50 border-slate-200">
          <div className="text-xs text-slate-500">Đợt nguồn</div>
          <div className="text-lg font-semibold text-slate-800">{selectedDocCount} đợt</div>
        </Card>
        <Card size="small" className="bg-slate-50 border-slate-200">
          <div className="text-xs text-slate-500">Mã hàng sau gom</div>
          <div className="text-lg font-semibold text-blue-600">
            {mergePreview.mergedItemCount} mã{' '}
            <span className="text-xs font-normal text-slate-500">
              (từ {mergePreview.sourceItemCount} dòng)
            </span>
          </div>
        </Card>
        <Card size="small" className="bg-slate-50 border-slate-200">
          <div className="text-xs text-slate-500">Tổng số lượng</div>
          <div className="text-lg font-semibold text-emerald-600">
            {mergePreview.totalQuantity.toLocaleString()} đôi
          </div>
        </Card>
        <Card size="small" className="bg-slate-50 border-slate-200">
          <div className="text-xs text-slate-500">Dự kiến tạo</div>
          <div className="text-lg font-semibold text-violet-600">
            {mergePreview.generatedDocumentCount} Invoice
          </div>
        </Card>
      </div>

      {/* PROCESS GROUPS */}
      {mergePreview.processGroups.length > 1 && (
        <Alert
          type="info"
          showIcon
          message={`Hệ thống sẽ tự động tách thành ${mergePreview.processGroups.length} hóa đơn theo nhóm công đoạn:`}
          description={
            <div className="flex gap-4 mt-1">
              {mergePreview.processGroups.map((pg) => (
                <div key={pg.processType} className="text-xs">
                  <b>{pg.processType === ProcessType.GoKhongMay ? 'Gò không may' : 'Thành hình'}:</b>{' '}
                  {pg.totalQuantity.toLocaleString()} đôi ({pg.itemCount} mã)
                </div>
              ))}
            </div>
          }
        />
      )}

      {/* MERGED ITEMS TABLE */}
      <Table
        size="small"
        pagination={false}
        rowKey={(row) => `${row.styleCode}-${row.processType}`}
        dataSource={mergePreview.mergedItems}
        columns={columns}
      />
    </div>
  );
};
