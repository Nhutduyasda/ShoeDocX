import type { FC } from 'react';
import { Empty, Table, Tabs, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { MaterialRequirementItem, MaterialRequirementResult, SizeMaterialRequirement } from '../types/bom';

interface BomResultTableProps {
  result?: MaterialRequirementResult;
}

interface SizeResultRow extends SizeMaterialRequirement {
  key: string;
  materialCode: string;
  materialName: string;
  unit: string;
}

const numberFormatter = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 4 });
const numberCell = (value: number, strong = false) => (
  <span className={`font-mono tabular-nums ${strong ? 'font-semibold text-slate-900' : 'text-slate-700'}`}>
    {numberFormatter.format(value)}
  </span>
);

export const BomResultTable: FC<BomResultTableProps> = ({ result }) => {
  if (!result) {
    return (
      <section className="enterprise-panel flex min-h-56 items-center justify-center p-6">
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có kết quả bóc tách vật tư" />
      </section>
    );
  }

  const commonMaterials = result.materials.filter((item) => item.materialType === 'Common' || item.materialType === 0);
  const sizeMaterials = result.materials.filter((item) => item.materialType === 'SizeDependent' || item.materialType === 1);
  const sizeRows: SizeResultRow[] = sizeMaterials.flatMap((material) =>
    material.sizeBreakdown.map((size) => ({
      ...size,
      key: `${material.materialId}-${size.sizeName}`,
      materialCode: material.materialCode,
      materialName: material.materialName,
      unit: material.unit,
    })),
  );

  const commonColumns: ColumnsType<MaterialRequirementItem> = [
    { title: 'STT', width: 60, align: 'center', render: (_value, _record, index) => index + 1 },
    { title: 'Mã vật tư', dataIndex: 'materialCode', width: 140, render: (value) => <span className="font-mono font-medium text-blue-700">{value}</span> },
    { title: 'Tên NPL', dataIndex: 'materialName', minWidth: 220 },
    { title: 'ĐVT', dataIndex: 'unit', width: 100, align: 'center' },
    { title: 'Định mức (Net)', dataIndex: 'netConsumption', width: 140, align: 'right', render: (value) => numberCell(value) },
    { title: 'Hao hụt (%)', dataIndex: 'wastageRatePercent', width: 130, align: 'right', render: (value) => numberCell(value) },
    { title: 'Tổng nhu cầu (Gross)', dataIndex: 'totalRequiredQuantity', width: 190, align: 'right', render: (value) => numberCell(value, true) },
  ];

  const sizeColumns: ColumnsType<SizeResultRow> = [
    { title: 'Mã NPL', dataIndex: 'materialCode', width: 140, render: (value) => <span className="font-mono font-medium text-blue-700">{value}</span> },
    { title: 'Tên NPL', dataIndex: 'materialName', minWidth: 220 },
    { title: 'Size', dataIndex: 'sizeName', width: 100, align: 'center', render: (value) => <Tag className="font-mono">{value}</Tag> },
    { title: 'SL đơn hàng', dataIndex: 'orderQuantity', width: 130, align: 'right', render: (value) => numberCell(value) },
    { title: 'SL cần xuất', dataIndex: 'requiredQuantity', width: 160, align: 'right', render: (value) => numberCell(value, true) },
    { title: 'ĐVT', dataIndex: 'unit', width: 90, align: 'center' },
  ];

  return (
    <section className="enterprise-panel min-w-0" aria-labelledby="bom-result-heading">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-200 px-4 py-3 sm:px-5">
        <div>
          <h2 id="bom-result-heading" className="m-0 text-sm font-semibold text-slate-800">Kết quả nhu cầu vật tư</h2>
          <p className="m-0 mt-1 text-xs text-slate-500">
            Lệnh <span className="font-mono font-medium">{result.orderNo}</span> · BOM {result.bomVersion} · {numberFormatter.format(result.totalQuantity)} đôi
          </p>
        </div>
        <Tag color="green">Đã tính toán</Tag>
      </div>
      <div className="min-w-0 px-4 pb-4 sm:px-5 sm:pb-5">
        <Tabs
          items={[
            {
              key: 'common',
              label: `Vật tư dùng chung (${commonMaterials.length})`,
              children: (
                <div className="max-w-full overflow-x-auto">
                  <Table rowKey="materialId" columns={commonColumns} dataSource={commonMaterials} pagination={false} scroll={{ x: 980 }} size="small" />
                </div>
              ),
            },
            {
              key: 'size',
              label: `Vật tư theo size (${sizeMaterials.length})`,
              children: (
                <div className="max-w-full overflow-x-auto">
                  <Table rowKey="key" columns={sizeColumns} dataSource={sizeRows} pagination={false} scroll={{ x: 840 }} size="small" />
                </div>
              ),
            },
          ]}
        />
      </div>
    </section>
  );
};
