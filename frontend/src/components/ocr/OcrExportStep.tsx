import React from 'react';
import { Alert, Button, Card, Select, Space, Tag, Typography } from 'antd';
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
  ExportOutlined,
  ShopOutlined,
} from '@ant-design/icons';
import type { MasterDataFolder, OcrDetectedDocument } from '../../types';
import type { MergeShipmentPreviewResponse } from '../../features/shipment-dispatch/types/shipmentDispatch';

const { Text } = Typography;

interface Props {
  partners: MasterDataFolder[];
  selectedPartnerId?: number | null;
  onPartnerChange: (partnerId: number) => void;
  selectedDocuments: OcrDetectedDocument[];
  mergePreview: MergeShipmentPreviewResponse | null;
  isStale: boolean;
  isExporting: boolean;
  onExport: () => void;
  exportError: { message: string; traceId?: string } | null;
}

export const OcrExportStep: React.FC<Props> = ({
  partners,
  selectedPartnerId,
  onPartnerChange,
  selectedDocuments,
  mergePreview,
  isStale,
  isExporting,
  onExport,
  exportError,
}) => {
  const currentPartner = partners.find((p) => p.id === selectedPartnerId);
  const totalQuantity = mergePreview?.totalQuantity ?? selectedDocuments.reduce((s, d) => s + d.calculatedTotal, 0);

  // Check manual confirmations
  const manuallyConfirmedDocs = selectedDocuments.filter((d) => d.isManuallyConfirmed);

  // Preflight checks
  const checkPartner = Boolean(selectedPartnerId && currentPartner);
  const checkPreviewValid = Boolean(mergePreview && mergePreview.isExportable && !isStale);
  const checkAllItemsMatched = Boolean(
    selectedDocuments.every((d) => d.items.every((i) => i.isMatched))
  );

  const canExport = checkPartner && checkPreviewValid && !isExporting;

  return (
    <div className="p-6 space-y-5 max-w-3xl mx-auto">
      <div className="border-b border-slate-200 pb-3">
        <h3 className="text-base font-semibold text-slate-800 m-0">Bước 4: Kiểm tra trước khi xuất chứng từ</h3>
        <p className="text-xs text-slate-500 m-0 mt-1">
          Rà soát hồ sơ đối tác, tính toàn vẹn danh mục Master Data và xác nhận xuất hóa đơn.
        </p>
      </div>

      {exportError && (
        <Alert
          type="error"
          showIcon
          message="Không thể xuất hóa đơn"
          description={
            <div>
              <div>{exportError.message}</div>
              {exportError.traceId && (
                <div className="mt-1 text-xs text-slate-500">
                  Mã theo dõi lỗi (Trace ID): <code>{exportError.traceId}</code>
                </div>
              )}
            </div>
          }
        />
      )}

      {/* PARTNER NOT SELECTED INLINE RECOVERY */}
      {!checkPartner && (
        <Alert
          type="warning"
          showIcon
          icon={<ShopOutlined />}
          message="Chưa chọn đối tác / hợp đồng"
          description={
            <div className="space-y-2 mt-1">
              <div>
                Hệ thống cần thông tin hợp đồng của đối tác để cấp số hóa đơn và xuất theo biểu mẫu tương ứng.
                Bạn có thể chọn đối tác ngay tại đây mà <b>không cần đóng modal hay quét lại ảnh</b>:
              </div>
              <Select
                placeholder="Chọn đối tác ngay..."
                style={{ width: 280 }}
                onChange={onPartnerChange}
                options={partners.map((p) => ({
                  value: p.id,
                  label: `${p.name} ${p.customerName ? `(${p.customerName.slice(0, 20)}...)` : ''}`,
                }))}
              />
            </div>
          }
        />
      )}

      {/* SUMMARY DETAILS CARD */}
      <Card size="small" className="border-slate-200 bg-slate-50">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
          <div>
            <Text type="secondary" className="text-xs">Đối tác xuất khẩu:</Text>
            <div className="font-semibold text-slate-800">
              {currentPartner ? currentPartner.name : <span className="text-amber-600">Chưa chọn</span>}
            </div>
            {currentPartner?.customerName && (
              <div className="text-xs text-slate-500">{currentPartner.customerName}</div>
            )}
          </div>

          <div>
            <Text type="secondary" className="text-xs">Nguồn đợt hàng:</Text>
            <div className="font-semibold text-slate-800">
              {selectedDocuments.map((d) => d.title).join(' + ')}
            </div>
            <div className="text-xs text-slate-500">{selectedDocuments.length} đợt &bull; {totalQuantity.toLocaleString()} đôi</div>
          </div>

          <div>
            <Text type="secondary" className="text-xs">Quy cách hóa đơn dự kiến:</Text>
            <div className="font-semibold text-blue-600">
              {currentPartner?.invoiceNoPattern
                ? currentPartner.invoiceNoPattern.replace('{SEQ:4}', String(currentPartner.currentSequenceNumber).padStart(4, '0'))
                : 'Theo số thứ tự đối tác'}
            </div>
          </div>

          <div>
            <Text type="secondary" className="text-xs">Số lượng chứng từ tạo:</Text>
            <div className="font-semibold text-violet-600">
              {mergePreview?.generatedDocumentCount ?? 1} file hóa đơn Excel
            </div>
          </div>
        </div>
      </Card>

      {/* PREFLIGHT CHECKLIST */}
      <Card size="small" title="Danh sách kiểm tra hợp lệ (Preflight Check)" className="border-slate-200">
        <div className="space-y-2.5 text-sm">
          <div className="flex items-center justify-between">
            <Space>
              <CheckCircleOutlined className="text-emerald-500" />
              <span>Dữ liệu OCR đã được đối soát</span>
            </Space>
            <Tag color="green">Đạt</Tag>
          </div>

          <div className="flex items-center justify-between">
            <Space>
              {checkPartner ? (
                <CheckCircleOutlined className="text-emerald-500" />
              ) : (
                <CloseCircleOutlined className="text-red-500" />
              )}
              <span>Đối tác / Hợp đồng đã chọn</span>
            </Space>
            {checkPartner ? (
              <Tag color="green">Đạt ({currentPartner?.name})</Tag>
            ) : (
              <Tag color="red">Chưa chọn</Tag>
            )}
          </div>

          <div className="flex items-center justify-between">
            <Space>
              {checkAllItemsMatched ? (
                <CheckCircleOutlined className="text-emerald-500" />
              ) : (
                <ExclamationCircleOutlined className="text-amber-500" />
              )}
              <span>Danh mục Master Data</span>
            </Space>
            {checkAllItemsMatched ? (
              <Tag color="green">100% khớp hợp đồng</Tag>
            ) : (
              <Tag color="gold">Có mã mới / chưa map</Tag>
            )}
          </div>

          <div className="flex items-center justify-between">
            <Space>
              {checkPreviewValid ? (
                <CheckCircleOutlined className="text-emerald-500" />
              ) : (
                <CloseCircleOutlined className="text-red-500" />
              )}
              <span>Kết quả gom hợp lệ</span>
            </Space>
            {checkPreviewValid ? (
              <Tag color="green">Hợp lệ</Tag>
            ) : (
              <Tag color="red">{isStale ? 'Dữ liệu đã cũ' : 'Chưa hợp lệ'}</Tag>
            )}
          </div>
        </div>
      </Card>

      {/* MANUAL CONFIRMATION WARNING */}
      {manuallyConfirmedDocs.length > 0 && (
        <Alert
          type="warning"
          showIcon
          icon={<ExclamationCircleOutlined />}
          message="Lưu ý về đợt hàng xác nhận thủ công"
          description={
            <div className="text-xs">
              Các đợt sau được xuất dựa trên xác nhận thủ công của bạn dù tổng OCR đọc trên phiếu bị lệch:{' '}
              <b>{manuallyConfirmedDocs.map((d) => d.title).join(', ')}</b>.
              Thông tin xác nhận này đã được ghi vào nhật ký kiểm toán hệ thống.
            </div>
          }
        />
      )}

      {/* PRIMARY EXPORT ACTION BUTTON */}
      <div className="pt-2 text-center">
        <Button
          type="primary"
          size="large"
          icon={<ExportOutlined />}
          loading={isExporting}
          disabled={!canExport}
          onClick={onExport}
          className="min-w-[260px] h-12 text-base font-semibold shadow-md bg-blue-600 hover:bg-blue-500"
        >
          {isExporting ? 'Đang xuất chứng từ...' : `Xuất ${mergePreview?.generatedDocumentCount || 1} hóa đơn`}
        </Button>
        {!canExport && (
          <div className="text-xs text-red-500 mt-2">
            {!checkPartner
              ? 'Vui lòng chọn đối tác trước khi xuất.'
              : isStale
              ? 'Dữ liệu nguồn đã thay đổi, vui lòng quay lại Bước 3 để tính lại.'
              : 'Dữ liệu chưa đủ điều kiện xuất.'}
          </div>
        )}
      </div>
    </div>
  );
};
