import React, { useEffect } from 'react';
import { Modal, Form, Input, InputNumber, Row, Col, message, Divider, TreeSelect } from 'antd';
import type { ProductMaster, CreateProductMasterRequest, MasterDataFolder } from '../types';
import { productMasterApi } from '../api/productMasterApi';

interface ProductModalProps {
  visible: boolean;
  product: ProductMaster | null; // null => Thêm mới, khác null => Sửa
  folders?: MasterDataFolder[];
  defaultFolderId?: number | null;
  onCancel: () => void;
  onSuccess: () => void;
}

export const ProductModal: React.FC<ProductModalProps> = ({
  visible,
  product,
  folders = [],
  defaultFolderId,
  onCancel,
  onSuccess,
}) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = React.useState(false);
  const isEdit = !!product;

  const formatTreeSelect = (nodes: MasterDataFolder[]): any[] => {
    return nodes.map((node) => ({
      title: `${node.name} (${node.defaultPairsPerCarton} đôi/thùng)`,
      value: node.id,
      key: node.id,
      children: node.children ? formatTreeSelect(node.children) : [],
    }));
  };

  const findFolder = (nodes: MasterDataFolder[], id: number): MasterDataFolder | null => {
    for (const n of nodes) {
      if (n.id === id) return n;
      if (n.children) {
        const found = findFolder(n.children, id);
        if (found) return found;
      }
    }
    return null;
  };

  useEffect(() => {
    if (visible) {
      if (product) {
        form.setFieldsValue({
          styleCode: product.styleCode,
          description: product.description,
          unitPriceCMT: product.unitPriceCMT,
          unitPriceDAP: product.unitPriceDAP,
          unitPriceCMT_Go: product.unitPriceCMT_Go ?? undefined,
          unitPriceDAP_Go: product.unitPriceDAP_Go ?? undefined,
          hsCode: product.hsCode,
          unit: product.unit,
          pairPerCarton: product.pairPerCarton,
          folderId: product.folderId ?? undefined,
        });
      } else {
        const targetFld = defaultFolderId ? findFolder(folders, defaultFolderId) : null;
        form.resetFields();
        form.setFieldsValue({
          unitPriceCMT: 0,
          unitPriceDAP: 0,
          hsCode: '64041990',
          unit: targetFld?.defaultUnit || 'PRS',
          pairPerCarton: targetFld?.defaultPairsPerCarton || 12,
          folderId: defaultFolderId ?? undefined,
        });
      }
    }
  }, [visible, product, defaultFolderId, form]);

  const handleFolderChange = (val: number | undefined) => {
    if (val) {
      const f = findFolder(folders, val);
      if (f) {
        form.setFieldsValue({
          pairPerCarton: f.defaultPairsPerCarton || 12,
          unit: f.defaultUnit || form.getFieldValue('unit') || 'PRS',
        });
      }
    }
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      const payload: CreateProductMasterRequest = {
        styleCode: values.styleCode.trim().toUpperCase(),
        description: values.description.trim(),
        unitPriceCMT: values.unitPriceCMT || 0,
        unitPriceDAP: values.unitPriceDAP || 0,
        unitPriceCMT_Go: values.unitPriceCMT_Go || null,
        unitPriceDAP_Go: values.unitPriceDAP_Go || null,
        hsCode: values.hsCode?.trim() || '64041990',
        unit: values.unit?.trim() || 'PRS',
        pairPerCarton: values.pairPerCarton || 12,
        folderId: values.folderId ?? null,
      };

      if (isEdit && product) {
        await productMasterApi.update(product.id, payload);
        message.success(`Đã cập nhật mã hàng ${payload.styleCode}`);
      } else {
        await productMasterApi.create(payload);
        message.success(`Đã thêm mã hàng ${payload.styleCode}`);
      }

      onSuccess();
    } catch (err: any) {
      if (err.errorFields) return;
      console.error(err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        'Lỗi xảy ra khi lưu thông tin sản phẩm';
      message.error(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={
        <div className="text-sm font-semibold text-slate-900">
          {isEdit ? `Chỉnh sửa mã hàng: ${product?.styleCode}` : 'Thêm mới mã hàng Master Data'}
        </div>
      }
      open={visible}
      onCancel={onCancel}
      onOk={handleSubmit}
      confirmLoading={loading}
      okText={isEdit ? 'Lưu thay đổi' : 'Tạo mới'}
      cancelText="Hủy"
      width={640}
      destroyOnClose
    >
      <Form
        form={form}
        layout="vertical"
        className="mt-3 space-y-1"
        requiredMark="optional"
      >
        <Row gutter={16}>
          <Col span={12}>
            <Form.Item
              name="styleCode"
              label={<span className="text-xs font-medium text-slate-700">Mã hình thể (Style Code) *</span>}
              rules={[
                { required: true, message: 'Vui lòng nhập mã hình thể' },
                { max: 50, message: 'Tối đa 50 ký tự' },
              ]}
              extra={<span className="text-[11px] text-slate-400">Ví dụ: 42072-030, 45428-2LX</span>}
            >
              <Input
                placeholder="42072-030"
                className="uppercase font-mono text-xs"
              />
            </Form.Item>
          </Col>

          <Col span={12}>
            <Form.Item
              name="folderId"
              label={<span className="text-xs font-medium text-slate-700">Thư mục đối tác</span>}
              extra={<span className="text-[11px] text-slate-400">Tự động kế thừa quy cách đóng gói</span>}
            >
              <TreeSelect
                treeData={formatTreeSelect(folders)}
                placeholder="Chọn thư mục đối tác"
                allowClear
                treeDefaultExpandAll
                onChange={handleFolderChange}
                className="text-xs"
              />
            </Form.Item>
          </Col>
        </Row>

        <Row gutter={16}>
          <Col span={12}>
            <Form.Item
              name="hsCode"
              label={<span className="text-xs font-medium text-slate-700">Mã HS Code Hải quan *</span>}
              rules={[
                { required: true, message: 'Vui lòng nhập mã HS Code' },
                { max: 30, message: 'Tối đa 30 ký tự' },
              ]}
              extra={<span className="text-[11px] text-slate-400">Mặc định: 64041990</span>}
            >
              <Input placeholder="64041990" className="font-mono text-xs" />
            </Form.Item>
          </Col>

          <Col span={12}>
            <Form.Item
              name="pairPerCarton"
              label={<span className="text-xs font-medium text-slate-700">Số đôi / Thùng (Pair/CTN) *</span>}
              rules={[{ required: true, message: 'Vui lòng nhập quy cách đóng gói' }]}
              extra={<span className="text-[11px] text-slate-400">KM III: 12 đôi | Đối tác khác: 24 đôi</span>}
            >
              <InputNumber
                className="w-full font-mono text-xs"
                min={1}
                max={1000}
                placeholder="12"
              />
            </Form.Item>
          </Col>
        </Row>

        <Form.Item
          name="description"
          label={<span className="text-xs font-medium text-slate-700">Mô tả hàng hóa xuất khẩu *</span>}
          rules={[
            { required: true, message: 'Vui lòng nhập mô tả hàng hóa' },
            { max: 255, message: 'Tối đa 255 ký tự' },
          ]}
        >
          <Input.TextArea
            rows={2}
            className="text-xs"
            placeholder="Giày thể thao nữ buộc dây đế cao su (Women's Athletic Shoes Rubber Sole)..."
          />
        </Form.Item>

        {/* Đơn giá Thành hình (Standard) */}
        <Divider className="my-2 text-xs text-slate-500 font-medium">
          Đơn giá Thành hình (Standard)
        </Divider>

        <Row gutter={16}>
          <Col span={12}>
            <Form.Item
              name="unitPriceCMT"
              label={<span className="text-xs font-medium text-slate-700">Đơn giá CMT Thành hình (USD) *</span>}
              rules={[{ required: true, message: 'Vui lòng nhập đơn giá CMT' }]}
            >
              <InputNumber
                className="w-full font-mono text-xs"
                min={0}
                step={0.01}
                precision={4}
                prefix={<span className="text-slate-400">$</span>}
                placeholder="0.00"
              />
            </Form.Item>
          </Col>

          <Col span={12}>
            <Form.Item
              name="unitPriceDAP"
              label={<span className="text-xs font-medium text-slate-700">Đơn giá DAP Thành hình (USD) *</span>}
              rules={[{ required: true, message: 'Vui lòng nhập đơn giá DAP' }]}
            >
              <InputNumber
                className="w-full font-mono text-xs"
                min={0}
                step={0.01}
                precision={4}
                prefix={<span className="text-slate-400">$</span>}
                placeholder="0.00"
              />
            </Form.Item>
          </Col>
        </Row>

        {/* Đơn giá Gò không may (.G) */}
        <Divider className="my-2 text-xs text-amber-700 font-medium">
          Đơn giá Gò không may (Tùy chọn gia công .G)
        </Divider>

        <Row gutter={16}>
          <Col span={12}>
            <Form.Item
              name="unitPriceCMT_Go"
              label={<span className="text-xs font-medium text-slate-700">CMT Gò không may (USD)</span>}
            >
              <InputNumber
                className="w-full font-mono text-xs"
                min={0}
                step={0.01}
                precision={4}
                prefix={<span className="text-slate-400">$</span>}
                placeholder="Để trống = dùng chung"
              />
            </Form.Item>
          </Col>

          <Col span={12}>
            <Form.Item
              name="unitPriceDAP_Go"
              label={<span className="text-xs font-medium text-slate-700">DAP Gò không may (USD)</span>}
            >
              <InputNumber
                className="w-full font-mono text-xs"
                min={0}
                step={0.01}
                precision={4}
                prefix={<span className="text-slate-400">$</span>}
                placeholder="Để trống = dùng chung"
              />
            </Form.Item>
          </Col>
        </Row>

        <Row gutter={16}>
          <Col span={12}>
            <Form.Item
              name="unit"
              label={<span className="text-xs font-medium text-slate-700">Đơn vị tính *</span>}
              rules={[{ required: true, message: 'Vui lòng nhập đơn vị tính' }]}
            >
              <Input placeholder="PRS" className="text-xs" />
            </Form.Item>
          </Col>
        </Row>
      </Form>
    </Modal>
  );
};
