import { useCallback, useEffect, useState } from 'react';
import { Button, message, Popconfirm, Progress, Table, Tag } from 'antd';
import { CheckOutlined, CloseOutlined, DownloadOutlined, ExportOutlined, ReloadOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { bomApi } from '../api/bomApi';
import type { ProductionOrderPlan, ProductionOrderPlanItem, ProductionOrderStatus } from '../types/bom';

const fmt = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 4 });
const statusMeta = (status: ProductionOrderStatus) => {
  const key = String(status);
  if (key === '1' || key === 'Calculated') return { label: 'Đã tính BOM', color: 'blue' };
  if (key === '2' || key === 'Approved') return { label: 'Đã duyệt', color: 'gold' };
  if (key === '3' || key === 'Issued') return { label: 'Đã xuất kho', color: 'green' };
  if (key === '4' || key === 'Cancelled') return { label: 'Đã hủy', color: 'default' };
  return { label: 'Nháp', color: 'default' };
};

export const MaterialPlanningPage = () => {
  const [orders, setOrders] = useState<ProductionOrderPlan[]>([]);
  const [loading, setLoading] = useState(false);
  const [actingId, setActingId] = useState<number>();

  const load = useCallback(async () => {
    setLoading(true);
    try { setOrders(await bomApi.getProductionOrders()); } finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  const act = async (id: number, action: 'approve' | 'issue' | 'cancel') => {
    setActingId(id);
    try {
      if (action === 'approve') await bomApi.approveOrder(id);
      if (action === 'issue') await bomApi.issueOrder(id);
      if (action === 'cancel') await bomApi.cancelOrder(id);
      message.success(action === 'approve' ? 'Đã duyệt nhu cầu vật tư.' : action === 'issue' ? 'Đã xuất kho và cập nhật tồn.' : 'Đã hủy lệnh.');
      await load();
    } finally { setActingId(undefined); }
  };

  const materialColumns: ColumnsType<ProductionOrderPlanItem> = [
    { title: 'Mã NPL', dataIndex: 'materialCode', width: 140, render: (v) => <span className="font-mono font-semibold text-blue-700">{v}</span> },
    { title: 'Tên NPL', dataIndex: 'materialName', minWidth: 200 },
    { title: 'ĐVT', dataIndex: 'unit', width: 80, align: 'center' },
    { title: 'Nhu cầu', dataIndex: 'requiredQuantity', width: 130, align: 'right', render: (v) => <span className="font-mono">{fmt.format(v)}</span> },
    { title: 'Tồn hiện tại', dataIndex: 'currentStock', width: 130, align: 'right', render: (v) => <span className="font-mono">{fmt.format(v)}</span> },
    { title: 'Thiếu', dataIndex: 'shortageQuantity', width: 120, align: 'right', render: (v) => <span className={`font-mono font-semibold ${v > 0 ? 'text-rose-600' : 'text-emerald-600'}`}>{fmt.format(v)}</span> },
    { title: 'Đáp ứng', width: 150, render: (_v, row) => <Progress size="small" percent={row.requiredQuantity <= 0 ? 100 : Math.min(100, Math.round(row.currentStock / row.requiredQuantity * 100))} status={row.shortageQuantity > 0 ? 'exception' : 'success'} /> },
  ];

  const columns: ColumnsType<ProductionOrderPlan> = [
    { title: 'Lệnh SX', dataIndex: 'orderNo', width: 160, render: (v) => <span className="font-mono font-semibold">{v}</span> },
    { title: 'Mã hình thể', dataIndex: 'styleCode', width: 150, render: (v) => <span className="font-mono">{v}</span> },
    { title: 'Công đoạn', dataIndex: 'processType', width: 130, render: (v) => v === 'GoKhongMay' || v === 2 ? 'Gò không may' : 'Thành hình' },
    { title: 'Số lượng', dataIndex: 'totalQuantity', width: 110, align: 'right', render: (v) => <span className="font-mono">{fmt.format(v)}</span> },
    { title: 'BOM', dataIndex: 'bomVersion', width: 90, render: (v) => v ? <Tag className="font-mono">{v}</Tag> : '—' },
    { title: 'Trạng thái', dataIndex: 'status', width: 130, render: (v) => { const meta = statusMeta(v); return <Tag color={meta.color}>{meta.label}</Tag>; } },
    {
      title: 'Thao tác', width: 270, fixed: 'right', render: (_v, row) => {
        const status = String(row.status);
        const calculated = status === 'Calculated' || status === '1';
        const approved = status === 'Approved' || status === '2';
        const cancellable = status === 'Draft' || status === '0' || calculated;
        return <div className="flex gap-1">
          {row.materials.length > 0 && <Button size="small" icon={<DownloadOutlined />} onClick={() => bomApi.downloadMaterialPlan(row.id, row.orderNo)}>Excel</Button>}
          {calculated && <Button size="small" icon={<CheckOutlined />} loading={actingId === row.id} onClick={() => act(row.id, 'approve')}>Duyệt</Button>}
          {approved && <Popconfirm title="Xác nhận xuất kho?" description="Tồn kho sẽ được trừ theo snapshot đã duyệt." onConfirm={() => act(row.id, 'issue')}><Button type="primary" size="small" icon={<ExportOutlined />} loading={actingId === row.id}>Xuất kho</Button></Popconfirm>}
          {cancellable && <Popconfirm title="Hủy lệnh sản xuất này?" onConfirm={() => act(row.id, 'cancel')}><Button danger size="small" icon={<CloseOutlined />}>Hủy</Button></Popconfirm>}
        </div>;
      },
    },
  ];

  return (
    <div className="enterprise-page">
      <div className="enterprise-page-header">
        <div><h1 className="enterprise-page-title">Kế hoạch & Xuất kho NPL</h1><p className="enterprise-page-description">Duyệt nhu cầu, theo dõi thiếu hụt và xuất kho theo snapshot BOM.</p></div>
        <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>Tải lại</Button>
      </div>
      <section className="enterprise-panel min-w-0 p-3">
        <div className="max-w-full overflow-x-auto">
          <Table rowKey="id" loading={loading} columns={columns} dataSource={orders} scroll={{ x: 1150 }} pagination={{ pageSize: 20 }} expandable={{
            rowExpandable: (row) => row.materials.length > 0,
            expandedRowRender: (row) => <div className="overflow-x-auto py-2"><Table rowKey="materialId" columns={materialColumns} dataSource={row.materials} pagination={false} size="small" scroll={{ x: 900 }} /></div>,
          }} />
        </div>
      </section>
    </div>
  );
};
