import { InputNumber, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { WarehouseBatchItem } from '../../../types/warehouse';
import { itemKey, normalizeStyle } from '../utils/splitAllocation';

interface Props { items: WarehouseBatchItem[]; allocations: Record<string, number[]>; invoiceCount: number; disabled: boolean; onChange: (key: string, invoiceIndex: number, value: number) => void }

export function SplitMatrixTable({ items, allocations, invoiceCount, disabled, onChange }: Props) {
  const columns: ColumnsType<WarehouseBatchItem> = [
    { title: 'Mã hình thể', dataIndex: 'styleCode', fixed: 'left', width: 145, render: (value) => <span className="font-mono font-semibold">{normalizeStyle(value)}</span> },
    { title: 'Công đoạn', dataIndex: 'processType', width: 125, render: (value) => <Tag color={value === 2 ? 'orange' : 'blue'}>{value === 2 ? 'Gò không may' : 'Thành hình'}</Tag> },
    { title: 'Tổng gốc', dataIndex: 'quantity', width: 105, align: 'right', render: (value) => <strong className="font-mono">{value.toLocaleString()}</strong> },
    ...Array.from({ length: invoiceCount }, (_, invoiceIndex) => ({ title: `INV ${invoiceIndex + 1}`, width: 120, align: 'right' as const,
      render: (_: unknown, row: WarehouseBatchItem) => { const key = itemKey(row.styleCode, row.processType); return <InputNumber aria-label={`${normalizeStyle(row.styleCode)} INV ${invoiceIndex + 1}`} min={0} precision={0} disabled={disabled} value={allocations[key]?.[invoiceIndex] ?? 0} controls={false} className="w-full" formatter={(value) => `${value ?? 0}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')} parser={(value) => Number((value ?? '0').replace(/,/g, ''))} onChange={(value) => onChange(key, invoiceIndex, Number(value ?? 0))} />; } })),
    { title: 'Trạng thái', fixed: 'right', width: 130, render: (_: unknown, row) => { const allocated = (allocations[itemKey(row.styleCode, row.processType)] ?? []).reduce((a, b) => a + b, 0); const diff = allocated - row.quantity; return <Tag color={diff === 0 ? 'green' : 'red'}>{diff === 0 ? 'Đủ 100%' : diff < 0 ? `Thiếu ${(-diff).toLocaleString()}` : `Dư ${diff.toLocaleString()}`}</Tag>; } },
  ];
  return <Table rowKey={(row) => itemKey(row.styleCode, row.processType)} size="small" pagination={false} dataSource={items} columns={columns} scroll={{ x: 500 + invoiceCount * 120, y: 390 }} />;
}
