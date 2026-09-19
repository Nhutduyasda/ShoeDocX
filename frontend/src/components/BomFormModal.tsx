import { useEffect, useState } from 'react';
import { Button, Form, Input, InputNumber, message, Modal, Select, Table, Tag } from 'antd';
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import type { ProductMaster } from '../types';
import type { BomDefinition, Material, ProcessType, SaveBomDefinitionRequest } from '../types/bom';

interface BomFormModalProps {
  open: boolean;
  editing?: BomDefinition;
  materials: Material[];
  products: ProductMaster[];
  saving?: boolean;
  onCancel: () => void;
  onSave: (request: SaveBomDefinitionRequest) => Promise<void>;
}

interface EditableBomItem {
  key: string;
  materialId?: number;
  netConsumption: number;
  wastageRatePercent: number;
}

interface BomHeaderValues {
  styleCode: string;
  processType: ProcessType;
  version: string;
  description?: string;
}

const newRow = (): EditableBomItem => ({ key: crypto.randomUUID(), netConsumption: 0, wastageRatePercent: 0 });

export const BomFormModal = ({ open, editing, materials, products, saving, onCancel, onSave }: BomFormModalProps) => {
  const [form] = Form.useForm<BomHeaderValues>();
  const [items, setItems] = useState<EditableBomItem[]>([newRow()]);

  useEffect(() => {
    if (!open) return;
    form.setFieldsValue(editing ? {
      styleCode: editing.styleCode,
      processType: editing.processType,
      version: editing.version,
      description: editing.description,
    } : { processType: 'Standard', version: 'V1', styleCode: undefined, description: undefined });
    setItems(editing?.items.map((item) => ({
      key: String(item.id),
      materialId: item.materialId,
      netConsumption: item.netConsumption,
      wastageRatePercent: item.wastageRatePercent,
    })) ?? [newRow()]);
  }, [editing, form, open]);

  const updateRow = (key: string, patch: Partial<EditableBomItem>) => {
    setItems((current) => current.map((item) => item.key === key ? { ...item, ...patch } : item));
  };

  const handleSave = async () => {
    const header = await form.validateFields();
    const validItems = items.filter((item) => item.materialId);
    if (validItems.length === 0) {
      message.warning('BOM phải có ít nhất một dòng vật tư.');
      return;
    }
    if (new Set(validItems.map((item) => item.materialId)).size !== validItems.length) {
      message.warning('Một vật tư không thể xuất hiện nhiều lần trong cùng BOM.');
      return;
    }
    await onSave({
      ...header,
      items: validItems.map((item) => ({
        materialId: item.materialId!,
        netConsumption: item.netConsumption,
        wastageRatePercent: item.wastageRatePercent,
      })),
    });
  };

  const columns: ColumnsType<EditableBomItem> = [
    {
      title: 'Vật tư', dataIndex: 'materialId', minWidth: 260,
      render: (_value, record) => <Select
        showSearch value={record.materialId} className="w-full" placeholder="Chọn vật tư"
        optionFilterProp="searchText" onChange={(value) => updateRow(record.key, { materialId: value })}
        options={materials.map((m) => ({ value: m.id, searchText: `${m.materialCode} ${m.materialName}`, label: <span><span className="font-mono font-medium">{m.materialCode}</span> — {m.materialName}</span> }))}
      />,
    },
    {
      title: 'Loại', width: 120, render: (_value, record) => {
        const material = materials.find((m) => m.id === record.materialId);
        return material ? (material.materialType === 'SizeDependent' || material.materialType === 1 ? <Tag color="purple">Theo size</Tag> : <Tag>Dùng chung</Tag>) : '—';
      },
    },
    { title: 'ĐVT', width: 90, align: 'center', render: (_value, record) => materials.find((m) => m.id === record.materialId)?.unit ?? '—' },
    {
      title: 'Định mức Net', width: 150, render: (_value, record) => <InputNumber min={0} precision={4} value={record.netConsumption} onChange={(value) => updateRow(record.key, { netConsumption: Number(value ?? 0) })} className="w-full font-mono" />,
    },
    {
      title: 'Hao hụt (%)', width: 140, render: (_value, record) => <InputNumber min={0} max={100} precision={4} value={record.wastageRatePercent} onChange={(value) => updateRow(record.key, { wastageRatePercent: Number(value ?? 0) })} className="w-full font-mono" />,
    },
    { title: '', width: 48, align: 'center', render: (_value, record) => <Button danger type="text" icon={<DeleteOutlined />} onClick={() => setItems((current) => current.filter((item) => item.key !== record.key))} /> },
  ];

  return (
    <Modal title={editing ? 'Chỉnh sửa Định mức BOM' : 'Tạo BOM Mới'} open={open} onCancel={onCancel} onOk={handleSave} okText="Lưu BOM" confirmLoading={saving} width={1080} destroyOnHidden>
      <Form form={form} layout="vertical" className="pt-2">
        <div className="grid grid-cols-1 gap-x-4 md:grid-cols-3">
          <Form.Item name="styleCode" label="Mã hình thể" rules={[{ required: true, message: 'Chọn mã hình thể' }]}>
            <Select showSearch optionFilterProp="searchText" placeholder="Chọn từ Product Master" options={products.map((p) => ({ value: p.styleCode, searchText: `${p.styleCode} ${p.description}`, label: <span><span className="font-mono font-medium">{p.styleCode}</span> — {p.description}</span> }))} />
          </Form.Item>
          <Form.Item name="processType" label="Công đoạn" rules={[{ required: true }]}>
            <Select options={[{ value: 'Standard', label: 'Thành hình' }, { value: 'GoKhongMay', label: 'Gò không may' }]} />
          </Form.Item>
          <Form.Item name="version" label="Phiên bản" rules={[{ required: true, message: 'Nhập phiên bản' }]}><Input maxLength={30} className="font-mono" /></Form.Item>
        </div>
        <Form.Item name="description" label="Diễn giải"><Input.TextArea rows={2} maxLength={500} showCount /></Form.Item>
      </Form>

      <div className="mb-2 flex items-center justify-between">
        <div className="text-sm font-semibold text-slate-700">Chi tiết Vật tư <span className="text-rose-500">*</span></div>
        <Button size="small" icon={<PlusOutlined />} onClick={() => setItems((current) => [...current, newRow()])}>Thêm dòng vật tư</Button>
      </div>
      <div className="max-w-full overflow-x-auto">
        <Table rowKey="key" columns={columns} dataSource={items} pagination={false} scroll={{ x: 850 }} size="small" locale={{ emptyText: 'Chưa có dòng vật tư' }} />
      </div>
    </Modal>
  );
};
