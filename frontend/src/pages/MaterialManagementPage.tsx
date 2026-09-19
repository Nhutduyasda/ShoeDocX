import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Form, Input, InputNumber, message, Modal, Popconfirm, Select, Table, Tag } from 'antd';
import { DeleteOutlined, EditOutlined, PlusOutlined, SearchOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { bomApi } from '../api/bomApi';
import type { Material, SaveMaterialRequest } from '../types/bom';

const numberFormatter = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 4 });

export const MaterialManagementPage = () => {
  const [materials, setMaterials] = useState<Material[]>([]);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editing, setEditing] = useState<Material>();
  const [modalOpen, setModalOpen] = useState(false);
  const [form] = Form.useForm<SaveMaterialRequest>();

  const loadMaterials = useCallback(async () => {
    setLoading(true);
    try { setMaterials(await bomApi.getMaterials()); } finally { setLoading(false); }
  }, []);

  useEffect(() => { void loadMaterials(); }, [loadMaterials]);

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    return term ? materials.filter((item) => `${item.materialCode} ${item.materialName}`.toLowerCase().includes(term)) : materials;
  }, [materials, search]);

  const openForm = (material?: Material) => {
    setEditing(material);
    form.setFieldsValue(material ?? { materialType: 'Common', currentStock: 0, unit: 'đôi' });
    setModalOpen(true);
  };

  const handleSave = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      if (editing) await bomApi.updateMaterial(editing.id, values);
      else await bomApi.createMaterial(values);
      message.success(editing ? 'Đã cập nhật vật tư.' : 'Đã thêm vật tư mới.');
      setModalOpen(false);
      form.resetFields();
      await loadMaterials();
    } finally { setSaving(false); }
  };

  const columns: ColumnsType<Material> = [
    { title: 'STT', width: 60, align: 'center', render: (_v, _r, index) => index + 1 },
    { title: 'Mã Vật tư', dataIndex: 'materialCode', width: 150, render: (value) => <span className="font-mono font-semibold text-blue-700">{value}</span> },
    { title: 'Tên NPL', dataIndex: 'materialName', minWidth: 230 },
    { title: 'Đơn vị tính', dataIndex: 'unit', width: 120, align: 'center' },
    { title: 'Phân loại', dataIndex: 'materialType', width: 150, render: (value) => value === 'SizeDependent' || value === 1 ? <Tag color="purple">Theo size</Tag> : <Tag>Dùng chung</Tag> },
    { title: 'Tồn kho', dataIndex: 'currentStock', width: 140, align: 'right', render: (value) => <span className="font-mono tabular-nums">{numberFormatter.format(value)}</span> },
    {
      title: 'Thao tác', width: 110, fixed: 'right', align: 'center', render: (_v, record) => (
        <div className="flex justify-center gap-1">
          <Button type="text" size="small" icon={<EditOutlined />} onClick={() => openForm(record)} aria-label={`Sửa ${record.materialCode}`} />
          <Popconfirm title="Xóa vật tư này?" description="Vật tư đang dùng trong BOM sẽ không thể xóa." onConfirm={async () => {
            await bomApi.deleteMaterial(record.id);
            message.success('Đã xóa vật tư.');
            await loadMaterials();
          }}>
            <Button danger type="text" size="small" icon={<DeleteOutlined />} aria-label={`Xóa ${record.materialCode}`} />
          </Popconfirm>
        </div>
      ),
    },
  ];

  return (
    <div className="enterprise-page">
      <div className="enterprise-page-header">
        <div><h1 className="enterprise-page-title">Danh mục Vật tư</h1><p className="enterprise-page-description">Quản lý nguyên phụ liệu dùng trong định mức kỹ thuật.</p></div>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => openForm()}>Thêm Vật tư</Button>
      </div>
      <section className="enterprise-panel min-w-0">
        <div className="border-b border-slate-200 p-3">
          <Input allowClear prefix={<SearchOutlined className="text-slate-400" />} placeholder="Tìm theo mã hoặc tên vật tư..." value={search} onChange={(e) => setSearch(e.target.value)} className="max-w-md" />
        </div>
        <div className="max-w-full overflow-x-auto p-3">
          <Table rowKey="id" loading={loading} columns={columns} dataSource={filtered} scroll={{ x: 1040 }} pagination={{ pageSize: 20, showSizeChanger: true }} />
        </div>
      </section>

      <Modal title={editing ? 'Sửa Vật tư' : 'Thêm Vật tư'} open={modalOpen} onCancel={() => setModalOpen(false)} onOk={handleSave} confirmLoading={saving} okText="Lưu vật tư" destroyOnHidden>
        <Form form={form} layout="vertical" className="pt-2">
          <div className="grid grid-cols-1 gap-x-4 sm:grid-cols-2">
            <Form.Item name="materialCode" label="Mã Vật tư" rules={[{ required: true, message: 'Nhập mã vật tư' }]}><Input className="font-mono" maxLength={50} /></Form.Item>
            <Form.Item name="unit" label="Đơn vị tính" rules={[{ required: true, message: 'Nhập đơn vị tính' }]}><Input maxLength={30} /></Form.Item>
          </div>
          <Form.Item name="materialName" label="Tên NPL" rules={[{ required: true, message: 'Nhập tên vật tư' }]}><Input maxLength={255} /></Form.Item>
          <div className="grid grid-cols-1 gap-x-4 sm:grid-cols-2">
            <Form.Item name="materialType" label="Phân loại" rules={[{ required: true }]}><Select options={[{ value: 'Common', label: 'Dùng chung' }, { value: 'SizeDependent', label: 'Theo size' }]} /></Form.Item>
            <Form.Item name="currentStock" label="Tồn kho" rules={[{ required: true }, { type: 'number', min: 0 }]}><InputNumber min={0} precision={4} className="w-full font-mono" /></Form.Item>
          </div>
        </Form>
      </Modal>
    </div>
  );
};
