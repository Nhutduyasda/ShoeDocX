import { DownloadOutlined, EyeOutlined, ReloadOutlined } from '@ant-design/icons';
import { Alert, Button, message, Modal, Space, Spin, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useEffect, useMemo, useState } from 'react';
import dayjs from 'dayjs';
import { warehouseApi } from '../../../api/warehouseApi';
import type { WarehouseBatchSummary } from '../../../types/warehouse';
import { shipmentDispatchApi, triggerDownload } from '../api/shipmentDispatchApi';
import type { MergeShipmentPreviewResponse, MergeShipmentRequest } from '../types/shipmentDispatch';

interface Props { open: boolean; contractFolderId?: number | null; templateId?: number | null; poSuffix?: string; invoiceDate: string; onClose: () => void; onExported: () => void }

export function MergeShipmentModal({ open, contractFolderId, templateId, poSuffix, invoiceDate, onClose, onExported }: Props) {
  const [batches, setBatches] = useState<WarehouseBatchSummary[]>([]);
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [preview, setPreview] = useState<MergeShipmentPreviewResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [previewing, setPreviewing] = useState(false);
  const [exporting, setExporting] = useState(false);
  const request = useMemo<MergeShipmentRequest>(() => ({ sourceBatchIds: selectedIds, contractFolderId: contractFolderId ?? undefined, templateId: templateId ?? undefined, poSuffix, invoiceDate }), [selectedIds, contractFolderId, templateId, poSuffix, invoiceDate]);
  const load = async () => { setLoading(true); try { setBatches(await warehouseApi.getBatches({ status: 'SubmittedToXnk' })); } catch { message.error('Không thể tải danh sách đợt đang chờ xử lý.'); } finally { setLoading(false); } };
  useEffect(() => { if (open) queueMicrotask(() => { setSelectedIds([]); setPreview(null); void load(); }); }, [open]);
  useEffect(() => { if (open) queueMicrotask(() => setPreview(null)); }, [open, contractFolderId, templateId, poSuffix, invoiceDate]);
  const select = (keys: React.Key[]) => { setSelectedIds(keys.map(Number)); setPreview(null); };
  const previewMerge = async () => { setPreviewing(true); try { setPreview(await shipmentDispatchApi.previewMerge(request)); } catch { setPreview(null); } finally { setPreviewing(false); } };
  const exportMerge = async () => { if (!preview) return; setExporting(true); try { const result = await shipmentDispatchApi.exportMerge(request); triggerDownload(result); message.success('Đã tải chứng từ gom đợt thành công.'); onExported(); onClose(); } catch { message.error('Không thể xuất chứng từ gom đợt. Vui lòng xem trước lại dữ liệu.'); setPreview(null); } finally { setExporting(false); } };
  const columns: ColumnsType<WarehouseBatchSummary> = [
    { title: 'Đợt hàng', dataIndex: 'batchName', render: (value, row) => <div><strong>{value}</strong><div className="text-xs text-slate-500">{row.contractNote}</div></div> },
    { title: 'Ngày', dataIndex: 'exportDate', width: 110, render: (value) => dayjs(value).format('DD/MM/YYYY') },
    { title: 'Công đoạn', width: 190, render: (_, row) => <Space size={4}>{row.thanhHinhCount > 0 && <Tag color="blue">Thành hình: {row.thanhHinhCount}</Tag>}{row.goCount > 0 && <Tag color="orange">Gò: {row.goCount}</Tag>}</Space> },
    { title: 'Số dòng', dataIndex: 'itemCount', width: 85, align: 'right' },
    { title: 'Số lượng', dataIndex: 'totalQuantity', width: 120, align: 'right', render: (value) => <strong className="font-mono">{value.toLocaleString()}</strong> },
  ];
  return <Modal title="Gom nhiều đợt hàng" open={open} onCancel={exporting ? undefined : onClose} footer={null} width="92vw" style={{ maxWidth: 1300, top: 28 }} destroyOnHidden maskClosable={!exporting}>
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3"><p className="m-0 text-xs text-slate-500">Chọn ít nhất hai đợt đã bàn giao XNK. Kết quả gộp và Packing List được tính chính thức tại Backend.</p><Button icon={<ReloadOutlined />} onClick={load} loading={loading}>Làm mới</Button></div>
      <Table rowKey="id" size="small" loading={loading} pagination={{ pageSize: 6 }} dataSource={batches} columns={columns} rowSelection={{ selectedRowKeys: selectedIds, onChange: select, getCheckboxProps: () => ({ disabled: exporting }) }} />
      <div className="flex justify-end gap-2"><Button icon={<EyeOutlined />} disabled={selectedIds.length < 2 || exporting} loading={previewing} onClick={previewMerge}>Xem trước số liệu gộp</Button><Button type="primary" icon={<DownloadOutlined />} disabled={!preview || exporting} loading={exporting} onClick={exportMerge}>Xuất Excel đã gom</Button></div>
      {previewing && <div className="text-center py-4"><Spin /> <span className="ml-2 text-xs text-slate-500">Đang đối soát dữ liệu nguồn...</span></div>}
      {preview && <>
        <Alert type="success" showIcon message={`Đã gộp ${preview.totalQuantity.toLocaleString()} đôi · ${preview.totalCartons.toLocaleString()} kiện`} description="Thay đổi lựa chọn đợt sẽ hủy kết quả xem trước này." />
        {preview.warnings.length > 0 && <Alert type="warning" showIcon message="Cảnh báo Packing List" description={preview.warnings.map((warning) => <div key={`${warning.code}-${warning.styleCode}`}>{warning.message}</div>)} />}
        <div><h3 className="text-sm font-semibold">Mặt hàng sau khi gom</h3><Table rowKey={(row) => `${row.styleCode}-${row.processType}`} size="small" pagination={false} dataSource={preview.mergedItems} columns={[
          { title: 'Mã', dataIndex: 'styleCode', render: (value) => <span className="font-mono font-semibold">{value}</span> },
          { title: 'Công đoạn', dataIndex: 'processType', render: (value) => <Tag color={value === 2 ? 'orange' : 'blue'}>{value === 2 ? 'Gò không may' : 'Thành hình'}</Tag> },
          { title: 'Mô tả', dataIndex: 'description' }, { title: 'Số lượng sau gom', dataIndex: 'quantity', align: 'right', render: (value) => value.toLocaleString() },
        ]} /></div>
        <div><h3 className="text-sm font-semibold">Packing List Preview</h3><Table rowKey={(row) => `${row.styleCode}-${row.fromCarton}-${row.toCarton}`} size="small" pagination={false} scroll={{ x: 850, y: 260 }} dataSource={preview.pklBreakdown} columns={[
          { title: 'Mã', dataIndex: 'styleCode' }, { title: 'Dải kiện', dataIndex: 'cartonRange' }, { title: 'Số kiện', dataIndex: 'cartonCount', align: 'right' },
          { title: 'Đôi/thùng', dataIndex: 'pairsPerCarton', align: 'right' }, { title: 'Số đôi', dataIndex: 'quantity', align: 'right' },
          { title: 'Net Weight', dataIndex: 'netWeight', align: 'right' }, { title: 'Gross Weight', dataIndex: 'grossWeight', align: 'right' },
          { title: 'Ghi chú', dataIndex: 'isOddCarton', render: (value) => value ? <Tag color="orange">Thùng lẻ</Tag> : <Tag color="green">Thùng nguyên</Tag> },
        ]} /></div>
      </>}
    </div>
  </Modal>;
}
