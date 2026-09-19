import { useCallback, useEffect, useState } from 'react';
import { Button, message, Popconfirm, Table, Tag } from 'antd';
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { bomApi } from '../api/bomApi';
import { productMasterApi } from '../api/productMasterApi';
import { BomFormModal } from '../components/BomFormModal';
import type { ProductMaster } from '../types';
import type { BomDefinition, Material, SaveBomDefinitionRequest } from '../types/bom';

export const BomManagementPage = () => {
  const [definitions, setDefinitions] = useState<BomDefinition[]>([]);
  const [materials, setMaterials] = useState<Material[]>([]);
  const [products, setProducts] = useState<ProductMaster[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<BomDefinition>();

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const [bomList, materialList, productList] = await Promise.all([
        bomApi.getDefinitions(), bomApi.getMaterials(), productMasterApi.getAll(),
      ]);
      setDefinitions(bomList);
      setMaterials(materialList);
      setProducts(productList);
    } finally { setLoading(false); }
  }, []);

  useEffect(() => { void loadData(); }, [loadData]);

  const handleSave = async (request: SaveBomDefinitionRequest) => {
    setSaving(true);
    try {
      if (editing) await bomApi.updateDefinition(editing.id, request);
      else await bomApi.createDefinition(request);
      message.success(editing ? 'Đã cập nhật BOM.' : 'Đã tạo BOM mới.');
      setModalOpen(false);
      setEditing(undefined);
      await loadData();
    } finally { setSaving(false); }
  };

  const columns: ColumnsType<BomDefinition> = [
    { title: 'Mã hình thể', dataIndex: 'styleCode', width: 160, render: (value) => <span className="font-mono font-semibold text-blue-700">{value}</span> },
    { title: 'Phiên bản', dataIndex: 'version', width: 110, render: (value) => <Tag className="font-mono">{value}</Tag> },
    { title: 'Công đoạn', dataIndex: 'processType', width: 150, render: (value) => value === 'GoKhongMay' || value === 2 ? <Tag color="orange">Gò không may</Tag> : <Tag color="blue">Thành hình</Tag> },
    { title: 'Diễn giải', dataIndex: 'description', minWidth: 250, render: (value) => value || <span className="text-slate-400">—</span> },
    { title: 'Số NPL', width: 90, align: 'right', render: (_v, record) => <span className="font-mono">{record.items.length}</span> },
    { title: 'Ngày tạo', dataIndex: 'createdAt', width: 140, render: (value) => new Date(value).toLocaleDateString('vi-VN') },
    {
      title: 'Thao tác', width: 110, fixed: 'right', align: 'center', render: (_v, record) => (
        <div className="flex justify-center gap-1">
          <Button type="text" size="small" icon={<EditOutlined />} onClick={() => { setEditing(record); setModalOpen(true); }} />
          <Popconfirm title="Xóa BOM này?" description="Toàn bộ chi tiết định mức sẽ bị xóa." onConfirm={async () => {
            await bomApi.deleteDefinition(record.id);
            message.success('Đã xóa BOM.');
            await loadData();
          }}>
            <Button danger type="text" size="small" icon={<DeleteOutlined />} />
          </Popconfirm>
        </div>
      ),
    },
  ];

  return (
    <div className="enterprise-page">
      <div className="enterprise-page-header">
        <div><h1 className="enterprise-page-title">Định mức Kỹ thuật</h1><p className="enterprise-page-description">Thiết lập cấu trúc BOM và tỷ lệ hao hụt theo mã hình thể, công đoạn.</p></div>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditing(undefined); setModalOpen(true); }}>Tạo BOM Mới</Button>
      </div>
      <section className="enterprise-panel min-w-0 p-3">
        <div className="max-w-full overflow-x-auto">
          <Table rowKey="id" loading={loading} columns={columns} dataSource={definitions} scroll={{ x: 1050 }} pagination={{ pageSize: 20, showSizeChanger: true }} />
        </div>
      </section>
      <BomFormModal open={modalOpen} editing={editing} materials={materials} products={products} saving={saving} onCancel={() => { setModalOpen(false); setEditing(undefined); }} onSave={handleSave} />
    </div>
  );
};
