import React from 'react';
import { Alert, Button, Card, Checkbox, Space, Tag, Typography } from 'antd';
import {
  EyeOutlined,
  FileZipOutlined,
  NodeIndexOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons';
import type { OcrDetectedDocument } from '../../types';
import { getReconciliationBadge, getReconciliationStatus } from '../../utils/ocrReconciliation';

const { Text } = Typography;

interface Props {
  documents: OcrDetectedDocument[];
  selectedDocumentIds: string[];
  processedDocumentIds: Set<string>;
  onToggleSelectDocument: (id: string, checked: boolean) => void;
  onSelectAll: () => void;
  onDeselectAll: () => void;
  onPreviewMerge: () => void;
  onExportSeparate: () => void;
  onOpenSplitSingle: (doc: OcrDetectedDocument) => void;
  onApplySingleInv: (doc: OcrDetectedDocument) => void;
  isPreviewingMerge: boolean;
  isExportingSeparate: boolean;
}

export const OcrDispatchStep: React.FC<Props> = ({
  documents,
  selectedDocumentIds,
  processedDocumentIds,
  onToggleSelectDocument,
  onSelectAll,
  onDeselectAll,
  onPreviewMerge,
  onExportSeparate,
  onOpenSplitSingle,
  onApplySingleInv,
  isPreviewingMerge,
  isExportingSeparate,
}) => {
  const selectedDocs = documents.filter(
    (d) => selectedDocumentIds.includes(d.documentId) && !processedDocumentIds.has(d.documentId)
  );

  const totalPairs = selectedDocs.reduce((sum, d) => sum + d.calculatedTotal, 0);

  // Check if any selected doc is in MISMATCH state
  const mismatchedDocs = selectedDocs.filter((d) => getReconciliationStatus(d) === 'MISMATCH');
  const hasMismatch = mismatchedDocs.length > 0;

  return (
    <div className="p-6 space-y-6 max-w-4xl mx-auto">
      <div className="border-b border-slate-200 pb-3">
        <h3 className="text-base font-semibold text-slate-800 m-0">Bước 2: Chọn phương thức điều phối</h3>
        <p className="text-xs text-slate-500 m-0 mt-1">
          Chọn các đợt hàng hợp lệ cần xử lý và chọn phương án gom hoặc tách xuất chứng từ.
        </p>
      </div>

      {hasMismatch && (
        <Alert
          type="error"
          showIcon
          icon={<ExclamationCircleOutlined />}
          message="Có đợt hàng chưa đối soát khớp tổng"
          description={
            <div>
              Các đợt sau đang bị lệch tổng và chưa được xác nhận: <b>{mismatchedDocs.map((d) => d.title).join(', ')}</b>.
              Vui lòng quay lại <b>Bước 1</b> để sửa số liệu hoặc xác nhận dữ liệu OCR đúng trước khi điều phối.
            </div>
          }
        />
      )}

      {/* DOCUMENT SELECTION LIST */}
      <Card
        size="small"
        title={
          <div className="flex justify-between items-center py-1">
            <span className="font-semibold text-slate-800">
              Danh sách đợt hàng ({selectedDocs.length}/{documents.length} đã chọn)
            </span>
            <Space>
              <Button size="small" onClick={onSelectAll}>
                Chọn tất cả
              </Button>
              <Button size="small" onClick={onDeselectAll}>
                Bỏ chọn
              </Button>
            </Space>
          </div>
        }
        className="border-slate-200"
      >
        <div className="divide-y divide-slate-100">
          {documents.map((doc) => {
            const isSelected = selectedDocumentIds.includes(doc.documentId);
            const isProcessed = processedDocumentIds.has(doc.documentId);
            const status = getReconciliationStatus(doc);
            const badge = getReconciliationBadge(status);
            const isBlocked = status === 'MISMATCH';

            return (
              <div
                key={doc.documentId}
                className={`py-2.5 px-2 flex items-center justify-between transition-colors ${
                  isBlocked ? 'bg-red-50/50' : isSelected ? 'bg-blue-50/30' : ''
                }`}
              >
                <Space>
                  <Checkbox
                    checked={isSelected}
                    disabled={isProcessed || isBlocked}
                    onChange={(e) => onToggleSelectDocument(doc.documentId, e.target.checked)}
                  />
                  <div>
                    <div className="font-medium text-slate-800 text-sm">{doc.title}</div>
                    <div className="text-xs text-slate-500">
                      {doc.items.length} mã hàng &bull; {doc.calculatedTotal.toLocaleString()} đôi
                    </div>
                  </div>
                </Space>

                <Space>
                  <Tag color={badge.color}>{badge.label}</Tag>
                  {isProcessed && <Tag color="green">Đã xuất</Tag>}
                </Space>
              </div>
            );
          })}
        </div>

        <div className="border-t border-slate-200 pt-3 mt-2 flex justify-between items-center text-sm">
          <Text strong>Tổng số lượng đã chọn:</Text>
          <Text strong className="text-blue-600 text-base">
            {totalPairs.toLocaleString()} đôi ({selectedDocs.length} đợt)
          </Text>
        </div>
      </Card>

      {/* DISPATCH ACTION TILES */}
      <div className="space-y-3">
        <Text strong className="text-sm text-slate-700 block">
          Chọn hình thức xử lý cho {selectedDocs.length} đợt hàng đã chọn:
        </Text>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {/* OPTION 1: GOM (MERGE) */}
          <Card
            hoverable={selectedDocs.length >= 2 && !hasMismatch}
            className={`border transition-all ${
              selectedDocs.length >= 2 && !hasMismatch
                ? 'border-blue-400 bg-blue-50/20'
                : 'opacity-60 bg-slate-50 border-slate-200'
            }`}
          >
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-blue-600 font-semibold">
                <EyeOutlined className="text-lg" />
                <span>Gom các đợt đã chọn</span>
              </div>
              <p className="text-xs text-slate-500 min-h-[36px]">
                Tổng hợp số lượng các mã trùng nhau từ nhiều đợt để xuất chung Invoice.
              </p>
              <Button
                type="primary"
                block
                loading={isPreviewingMerge}
                disabled={selectedDocs.length < 2 || hasMismatch}
                onClick={onPreviewMerge}
              >
                Xem kết quả gom ({selectedDocs.length} đợt)
              </Button>
            </div>
          </Card>

          {/* OPTION 2: XUẤT RIÊNG (SEPARATE) */}
          <Card
            hoverable={selectedDocs.length >= 1 && !hasMismatch}
            className={`border transition-all ${
              selectedDocs.length >= 1 && !hasMismatch
                ? 'border-emerald-400 bg-emerald-50/20'
                : 'opacity-60 bg-slate-50 border-slate-200'
            }`}
          >
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-emerald-600 font-semibold">
                <FileZipOutlined className="text-lg" />
                <span>Xuất riêng từng đợt</span>
              </div>
              <p className="text-xs text-slate-500 min-h-[36px]">
                Mỗi đợt xuất thành 1 hóa đơn riêng lẻ, đóng gói tải về trong 1 file ZIP.
              </p>
              <Button
                block
                className="border-emerald-500 text-emerald-700 hover:bg-emerald-50"
                loading={isExportingSeparate}
                disabled={selectedDocs.length === 0 || hasMismatch}
                onClick={onExportSeparate}
              >
                Xuất riêng ({selectedDocs.length} file)
              </Button>
            </div>
          </Card>

          {/* OPTION 3: TÁCH (SPLIT) */}
          <Card
            hoverable={selectedDocs.length === 1 && !hasMismatch}
            className={`border transition-all ${
              selectedDocs.length === 1 && !hasMismatch
                ? 'border-violet-400 bg-violet-50/20'
                : 'opacity-60 bg-slate-50 border-slate-200'
            }`}
          >
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-violet-600 font-semibold">
                <NodeIndexOutlined className="text-lg" />
                <span>Tách đợt đã chọn</span>
              </div>
              <p className="text-xs text-slate-500 min-h-[36px]">
                Phân bổ 1 đợt hàng thành nhiều hóa đơn con theo ma trận tỷ lệ.
              </p>
              <Button
                block
                disabled={selectedDocs.length !== 1 || hasMismatch}
                onClick={() => selectedDocs[0] && onOpenSplitSingle(selectedDocs[0])}
              >
                Mở ma trận tách
              </Button>
            </div>
          </Card>
        </div>

        {selectedDocs.length === 1 && !hasMismatch && (
          <div className="pt-2 text-center">
            <Button
              type="dashed"
              size="small"
              onClick={() => onApplySingleInv(selectedDocs[0])}
            >
              Hoặc áp dụng trực tiếp 1 đợt này vào giao diện hóa đơn chính
            </Button>
          </div>
        )}
      </div>
    </div>
  );
};
