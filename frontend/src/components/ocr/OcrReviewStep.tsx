import React, { useRef } from 'react';
import {
  Alert,
  Button,
  Card,
  Checkbox,
  Input,
  InputNumber,
  Select,
  Space,
  Spin,
  Table,
  Tag,
} from 'antd';
import {
  DeleteOutlined,
  ExclamationCircleOutlined,
  InboxOutlined,
  ReloadOutlined,
  WarningOutlined,
  CheckCircleOutlined,
} from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import type { OcrDetectedDocument, OcrItem, ProductMaster } from '../../types';
import { ProcessType, normalizeProcessType } from '../../types';
import { OriginalImagePreview } from '../OriginalImagePreview';
import { getReconciliationBadge, getReconciliationStatus } from '../../utils/ocrReconciliation';

interface Props {
  file: File | null;
  previewUrl: string | null;
  documents: OcrDetectedDocument[];
  activeDocumentId: string | null;
  selectedDocumentIds: string[];
  loading: boolean;
  products: ProductMaster[];
  selectedPartnerId?: number | null;
  onSelectFile: (file: File) => void;
  onRescanImage: () => void;
  onSetActiveDocumentId: (id: string | null) => void;
  onToggleSelectDocument: (id: string, checked: boolean) => void;
  onUpdateDocument: (id: string, updater: (doc: OcrDetectedDocument) => OcrDetectedDocument) => void;
  onRequestManualConfirm: (doc: OcrDetectedDocument) => void;
}

export const OcrReviewStep: React.FC<Props> = ({
  file,
  previewUrl,
  documents,
  activeDocumentId,
  selectedDocumentIds,
  loading,
  products,
  selectedPartnerId,
  onSelectFile,
  onRescanImage,
  onSetActiveDocumentId,
  onToggleSelectDocument,
  onUpdateDocument,
  onRequestManualConfirm,
}) => {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const activeDocument = documents.find((d) => d.documentId === activeDocumentId) ?? documents[0];

  const handleUpdateItem = (
    docId: string,
    itemIndex: number,
    field: keyof OcrItem,
    value: unknown
  ) => {
    onUpdateDocument(docId, (doc) => {
      const updatedItems = doc.items.map((item, idx) => {
        if (idx !== itemIndex) return item;
        const updated = { ...item, [field]: value };
        if (field === 'styleCode') {
          const upper = String(value || '').trim().toUpperCase();
          const clean = upper.replace(/\.G$/, '').trim();
          const pm = products.find(
            (p) => (!selectedPartnerId || p.folderId === selectedPartnerId) && p.styleCode.trim().toUpperCase() === clean
          );
          return {
            ...updated,
            styleCode: String(value || ''),
            isMatched: Boolean(pm),
            unitPriceCMT: pm?.unitPriceCMT ?? updated.unitPriceCMT,
            unitPriceDAP: pm?.unitPriceDAP ?? updated.unitPriceDAP,
            pairPerCarton: pm?.pairPerCarton || updated.pairPerCarton || 12,
            description: pm?.description ?? updated.description,
            unit: pm?.unit ?? updated.unit ?? 'đôi',
          };
        }
        return updated;
      });

      const newCalculatedTotal = updatedItems.reduce((sum, it) => sum + (it.quantity || 0), 0);
      const isMatch = doc.reportedTotal !== null && doc.reportedTotal !== undefined && doc.reportedTotal === newCalculatedTotal;

      return {
        ...doc,
        items: updatedItems,
        calculatedTotal: newCalculatedTotal,
        discrepancy: doc.reportedTotal !== null && doc.reportedTotal !== undefined ? newCalculatedTotal - doc.reportedTotal : null,
        isTotalMatched: isMatch,
        // Khi user sửa bất kỳ dòng nào, hủy bỏ xác nhận thủ công trước đó để bắt buộc đối soát lại
        isManuallyConfirmed: isMatch ? false : false,
      };
    });
  };

  const handleDeleteItem = (docId: string, itemIndex: number) => {
    onUpdateDocument(docId, (doc) => {
      const updatedItems = doc.items.filter((_, idx) => idx !== itemIndex);
      const newCalculatedTotal = updatedItems.reduce((sum, it) => sum + (it.quantity || 0), 0);
      const isMatch = doc.reportedTotal !== null && doc.reportedTotal !== undefined && doc.reportedTotal === newCalculatedTotal;
      return {
        ...doc,
        items: updatedItems,
        calculatedTotal: newCalculatedTotal,
        discrepancy: doc.reportedTotal !== null && doc.reportedTotal !== undefined ? newCalculatedTotal - doc.reportedTotal : null,
        isTotalMatched: isMatch,
        isManuallyConfirmed: false,
      };
    });
  };

  const itemColumns = (doc: OcrDetectedDocument): ColumnsType<OcrItem> => [
    {
      title: 'Mã hình thể',
      dataIndex: 'styleCode',
      render: (v, _r, i) => (
        <Input
          size="small"
          value={v}
          onChange={(e) => handleUpdateItem(doc.documentId, i, 'styleCode', e.target.value)}
        />
      ),
    },
    {
      title: 'Số lượng',
      dataIndex: 'quantity',
      width: 110,
      align: 'right',
      render: (v, _r, i) => (
        <InputNumber
          size="small"
          min={1}
          value={v}
          style={{ width: '100%' }}
          onChange={(q) => handleUpdateItem(doc.documentId, i, 'quantity', q || 0)}
        />
      ),
    },
    {
      title: 'Công đoạn',
      dataIndex: 'processType',
      width: 140,
      render: (v, _r, i) => (
        <Select
          size="small"
          value={normalizeProcessType(v)}
          style={{ width: '100%' }}
          options={[
            { value: ProcessType.Standard, label: 'Thành hình' },
            { value: ProcessType.GoKhongMay, label: 'Gò không may' },
          ]}
          onChange={(p) => handleUpdateItem(doc.documentId, i, 'processType', p)}
        />
      ),
    },
    {
      title: 'Master',
      width: 80,
      align: 'center',
      render: (_, r) => (
        <Tag color={r.isMatched ? 'green' : 'default'} className="m-0">
          {r.isMatched ? 'Khớp' : 'Mới'}
        </Tag>
      ),
    },
    {
      title: '',
      width: 40,
      align: 'center',
      render: (_, _r, i) => (
        <Button
          danger
          type="text"
          size="small"
          icon={<DeleteOutlined />}
          onClick={() => handleDeleteItem(doc.documentId, i)}
        />
      ),
    },
  ];

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-[43%_57%] p-4 min-h-[550px]">
      <input
        ref={fileInputRef}
        hidden
        type="file"
        accept="image/*"
        onChange={(e) => e.target.files?.[0] && onSelectFile(e.target.files[0])}
      />

      {/* LEFT COLUMN: Original Image Preview (Sticky) */}
      <div
        className="lg:sticky lg:top-0 lg:self-start bg-slate-50 p-3 rounded-lg border border-slate-200"
        onDrop={(e) => {
          e.preventDefault();
          if (e.dataTransfer.files[0]) onSelectFile(e.dataTransfer.files[0]);
        }}
        onDragOver={(e) => e.preventDefault()}
      >
        <div className="flex justify-between items-center mb-2">
          <span className="font-semibold text-xs tracking-wider text-slate-700 uppercase">
            ẢNH PHIẾU KHO GỐC
          </span>
          {file && <span className="text-xs text-slate-500 truncate max-w-[200px]">{file.name}</span>}
        </div>

        {previewUrl ? (
          <>
            <OriginalImagePreview
              src={previewUrl}
              alt="Ảnh phiếu kho gốc"
              sourceRegion={activeDocument?.sourceRegion}
              maxHeightClassName="max-h-[62vh]"
            />
            <Space className="mt-2.5 w-full justify-between" wrap>
              <Space>
                <Button size="small" onClick={() => fileInputRef.current?.click()}>
                  Chọn ảnh khác
                </Button>
                <Button
                  size="small"
                  type="link"
                  icon={<ReloadOutlined />}
                  onClick={onRescanImage}
                >
                  Quét lại ảnh gốc
                </Button>
              </Space>
              <Button size="small" onClick={() => onSetActiveDocumentId(null)}>
                Xem toàn ảnh
              </Button>
            </Space>
          </>
        ) : (
          <div
            onClick={() => fileInputRef.current?.click()}
            className="border-2 border-dashed border-slate-300 rounded-lg min-h-[300px] flex items-center justify-center cursor-pointer hover:border-blue-400 bg-white transition-colors"
          >
            <Space direction="vertical" align="center">
              <InboxOutlined className="text-4xl text-blue-500" />
              <span className="font-medium text-slate-700">Chọn, kéo thả hoặc dán ảnh phiếu kho</span>
              <span className="text-xs text-slate-400">Hỗ trợ PNG, JPG, WEBP (tối đa 20MB)</span>
            </Space>
          </div>
        )}
      </div>

      {/* RIGHT COLUMN: OCR Documents list */}
      <div className="max-h-[74vh] overflow-y-auto space-y-3 pr-1">
        {loading ? (
          <div className="py-32 text-center">
            <Spin tip="Đang phân tích và bóc tách các đợt hàng từ ảnh qua Vision AI..." size="large" />
          </div>
        ) : documents.length === 0 ? (
          <Alert
            message="Chưa có dữ liệu OCR"
            description="Vui lòng chọn hoặc dán ảnh phiếu kho ở cột bên trái để bắt đầu bóc tách."
            type="info"
            showIcon
          />
        ) : (
          <>
            <div className="flex justify-between items-center px-1">
              <div className="text-xs text-slate-500">
                Phát hiện <b>{documents.length} đợt hàng</b>. Vui lòng kiểm tra đối chiếu mã và số lượng trước khi tiếp tục.
              </div>
            </div>

            {documents.map((doc, index) => {
              const status = getReconciliationStatus(doc);
              const badge = getReconciliationBadge(status);
              const isSelected = selectedDocumentIds.includes(doc.documentId);
              const isActive = activeDocument?.documentId === doc.documentId;
              const diff = doc.reportedTotal !== null && doc.reportedTotal !== undefined
                ? doc.calculatedTotal - doc.reportedTotal
                : null;

              return (
                <Card
                  key={doc.documentId}
                  size="small"
                  className={`transition-all ${
                    isActive ? 'border-blue-500 ring-1 ring-blue-400 shadow-sm' : 'border-slate-200'
                  }`}
                  onClick={() => onSetActiveDocumentId(doc.documentId)}
                  title={
                    <div className="flex items-center justify-between gap-2 py-1">
                      <Space>
                        <Checkbox
                          checked={isSelected}
                          onClick={(e) => e.stopPropagation()}
                          onChange={(e) => onToggleSelectDocument(doc.documentId, e.target.checked)}
                        />
                        <Input
                          size="small"
                          value={doc.title}
                          className="font-semibold text-slate-800"
                          style={{ width: 220 }}
                          onClick={(e) => e.stopPropagation()}
                          onChange={(e) =>
                            onUpdateDocument(doc.documentId, (d) => ({ ...d, title: e.target.value }))
                          }
                        />
                      </Space>
                      <Space>
                        <Tag color={badge.color} className="m-0 font-medium">
                          {badge.label}
                        </Tag>
                      </Space>
                    </div>
                  }
                >
                  <div className="flex justify-between items-center text-xs text-slate-600 mb-2 px-1">
                    <span>
                      Đợt {index + 1} &bull; <b>{doc.items.length} mã</b>
                    </span>
                    <span>
                      Tổng dòng: <b className="text-blue-600 text-sm">{doc.calculatedTotal.toLocaleString()}</b> đôi
                    </span>
                  </div>

                  {/* RECONCILIATION PANEL */}
                  {status === 'MISMATCH' && (
                    <div className="bg-red-50 border border-red-200 rounded-lg p-3 my-2 text-xs space-y-2">
                      <div className="flex items-center text-red-700 font-semibold gap-1.5">
                        <WarningOutlined />
                        <span>Tổng chưa khớp với số ghi nhận trên phiếu</span>
                      </div>
                      <div className="grid grid-cols-3 gap-2 text-slate-700">
                        <div>Tổng OCR đọc trên phiếu: <b>{doc.reportedTotal?.toLocaleString()} đôi</b></div>
                        <div>Tổng các dòng cộng lại: <b>{doc.calculatedTotal.toLocaleString()} đôi</b></div>
                        <div className="text-red-600">
                          Chênh lệch: <b>{diff && diff > 0 ? `+${diff.toLocaleString()}` : diff?.toLocaleString()} đôi</b>
                        </div>
                      </div>
                      <div className="flex justify-end gap-2 pt-1 border-t border-red-100">
                        <Button
                          size="small"
                          onClick={(e) => {
                            e.stopPropagation();
                            // User intends to fix data in table
                          }}
                        >
                          Sửa dòng OCR
                        </Button>
                        <Button
                          size="small"
                          type="primary"
                          className="bg-amber-600 hover:bg-amber-500 border-amber-600"
                          icon={<ExclamationCircleOutlined />}
                          onClick={(e) => {
                            e.stopPropagation();
                            onRequestManualConfirm(doc);
                          }}
                        >
                          Xác nhận dữ liệu OCR đúng
                        </Button>
                      </div>
                    </div>
                  )}

                  {status === 'MANUALLY_CONFIRMED' && (
                    <Alert
                      type="warning"
                      showIcon
                      className="my-2 text-xs"
                      message="Đã xác nhận thủ công"
                      description={`Tổng đọc trên phiếu ${doc.reportedTotal?.toLocaleString()} lệch với tổng dòng hàng ${doc.calculatedTotal.toLocaleString()} đôi, nhưng bạn đã đối chiếu ảnh gốc và xác nhận dòng hàng là chính xác.`}
                    />
                  )}

                  {status === 'MATCHED' && (
                    <div className="flex items-center gap-1 text-xs text-emerald-700 bg-emerald-50 px-2.5 py-1 rounded mb-2">
                      <CheckCircleOutlined />
                      <span>Số liệu khớp tuyệt đối: {doc.calculatedTotal.toLocaleString()} đôi</span>
                    </div>
                  )}

                  {status === 'NO_REPORTED_TOTAL' && (
                    <Alert
                      type="info"
                      showIcon
                      className="my-2 text-xs"
                      message="Không đọc được dòng tổng cộng trên phiếu"
                      description={`Hệ thống sẽ lấy tổng thực tính các dòng: ${doc.calculatedTotal.toLocaleString()} đôi.`}
                    />
                  )}

                  <Table
                    size="small"
                    pagination={false}
                    rowKey={(_r, idx) => `${doc.documentId}-${idx}`}
                    dataSource={doc.items}
                    columns={itemColumns(doc)}
                  />
                </Card>
              );
            })}
          </>
        )}
      </div>
    </div>
  );
};
