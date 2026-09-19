import type { FC } from 'react';
import { Form, Input, Select, Statistic, Tag } from 'antd';
import { BarcodeOutlined, BgColorsOutlined, NumberOutlined, ToolOutlined } from '@ant-design/icons';
import type { BomMasterOption } from '../types/bom';

interface ProductionOrderFormProps {
  orderNo: string;
  bomSelection?: string;
  totalQuantity: number;
  bomOptions: BomMasterOption[];
  loadingBomOptions?: boolean;
  onOrderNoChange: (value: string) => void;
  onBomSelectionChange: (value: string) => void;
}

export const ProductionOrderForm: FC<ProductionOrderFormProps> = ({
  orderNo,
  bomSelection,
  totalQuantity,
  bomOptions,
  loadingBomOptions,
  onOrderNoChange,
  onBomSelectionChange,
}) => {
  const selectedBom = bomOptions.find((item) => `${item.styleCode}::${item.processType}` === bomSelection);

  return (
    <section className="enterprise-panel p-4 sm:p-5" aria-labelledby="production-order-heading">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 pb-3">
        <div>
          <h2 id="production-order-heading" className="m-0 text-sm font-semibold text-slate-800">
            Thông tin lệnh sản xuất
          </h2>
          <p className="m-0 mt-1 text-xs text-slate-500">Chọn BOM và nhập dải size để tính nhu cầu nguyên phụ liệu.</p>
        </div>
        {selectedBom && <Tag color="blue" className="font-mono">BOM {selectedBom.version}</Tag>}
      </div>

      <div className="grid min-w-0 grid-cols-1 gap-x-4 gap-y-1 md:grid-cols-2 xl:grid-cols-[1fr_1.2fr_0.8fr_220px]">
        <Form.Item label={<span><BarcodeOutlined className="mr-1.5 text-slate-400" />Lệnh sản xuất</span>} required>
          <Input
            value={orderNo}
            onChange={(event) => onOrderNoChange(event.target.value)}
            placeholder="VD: LSX-2026-001"
            maxLength={50}
            className="font-mono"
          />
        </Form.Item>

        <Form.Item label={<span><BgColorsOutlined className="mr-1.5 text-slate-400" />Mã hình thể</span>} required>
          <Select
            value={bomSelection}
            onChange={onBomSelectionChange}
            loading={loadingBomOptions}
            showSearch
            allowClear
            placeholder="Tìm mã hình thể có BOM"
            optionFilterProp="searchText"
            options={bomOptions.map((item) => ({
              value: `${item.styleCode}::${item.processType}`,
              searchText: `${item.styleCode} ${item.version} ${item.description ?? ''}`,
              label: (
                <div className="flex min-w-0 items-center justify-between gap-3">
                  <span className="truncate font-mono font-medium">{item.styleCode}</span>
                  <span className="shrink-0 text-[11px] text-slate-400">v{item.version}</span>
                </div>
              ),
            }))}
          />
        </Form.Item>

        <Form.Item label={<span><ToolOutlined className="mr-1.5 text-slate-400" />Công đoạn</span>}>
          <Input value={selectedBom ? (selectedBom.processType === 'GoKhongMay' || selectedBom.processType === 2 ? 'Gò không may' : 'Thành hình') : '—'} readOnly className="bg-slate-50 text-slate-600" />
        </Form.Item>

        <div className="rounded-md border border-slate-200 bg-slate-50 px-4 py-3">
          <Statistic
            title={<span className="text-xs text-slate-500"><NumberOutlined className="mr-1.5" />Tổng số lượng</span>}
            value={totalQuantity}
            suffix={<span className="text-xs font-normal text-slate-500">đôi</span>}
            valueStyle={{ fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace', fontSize: 26, lineHeight: 1.2, color: '#0f172a' }}
          />
        </div>
      </div>
    </section>
  );
};
