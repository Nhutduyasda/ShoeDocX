import React, { useEffect, useState } from 'react';
import { Modal, Form, Input, InputNumber, Row, Col, message, Alert, Typography } from 'antd';
import { PlusCircleOutlined, CheckCircleOutlined, DollarOutlined, InfoCircleOutlined } from '@ant-design/icons';
import type { ProductMaster, CreateProductMasterRequest } from '../types';
import { productMasterApi } from '../api/productMasterApi';

const { Text } = Typography;

interface QuickAddMasterDataModalProps {
  folderId?: number | null;
  defaultPairsPerCarton?: number;
  visible: boolean;
  styleCode: string;
  initialDescription?: string;
  isGoProcess?: boolean;
  onCancel: () => void;
  onSuccess: (created: ProductMaster) => void;
}

export const QuickAddMasterDataModal: React.FC<QuickAddMasterDataModalProps> = ({
  folderId,
  defaultPairsPerCarton = 12,
  visible,
  styleCode,
  initialDescription = '',
  isGoProcess = false,
  onCancel,
  onSuccess,
}) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState<boolean>(false);

  // Chuẩn hóa mã hình thể: nếu là hàng .G thì lấy mã gốc làm StyleCode cho Master Data
  const cleanStyleCode = styleCode.trim().toUpperCase().endsWith('.G')
    ? styleCode.trim().toUpperCase().slice(0, -2).trim()
    : styleCode.trim().toUpperCase();

  useEffect(() => {
    if (visible) {
      form.resetFields();
      form.setFieldsValue({
        styleCode: cleanStyleCode,
        description: initialDescription.trim() || `Giày ${cleanStyleCode} xuất khẩu`,
        unitPriceCMT: 0,
        unitPriceDAP: 0,
        unitPriceCMT_Go: 0,
        unitPriceDAP_Go: 0,
        pairPerCarton: defaultPairsPerCarton,
        hsCode: '64041990',
        unit: 'đôi',
      });
    }
  }, [visible, cleanStyleCode, initialDescription, form, defaultPairsPerCarton]);

  const handleSaveAndApply = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      const payload: CreateProductMasterRequest = {
        folderId,
        styleCode: values.styleCode.trim().toUpperCase(),
        description: values.description.trim(),
        unitPriceCMT: values.unitPriceCMT || 0,
        unitPriceDAP: values.unitPriceDAP || 0,
        unitPriceCMT_Go: values.unitPriceCMT_Go > 0 ? values.unitPriceCMT_Go : null,
        unitPriceDAP_Go: values.unitPriceDAP_Go > 0 ? values.unitPriceDAP_Go : null,
        hsCode: values.hsCode?.trim() || '64041990',
        unit: values.unit?.trim() || 'đôi',
        pairPerCarton: values.pairPerCarton || 12,
      };

      const created = await productMasterApi.create(payload);
      message.success(`Đã thêm mã [${created.styleCode}] vào Master Data và tự động cập nhật đơn giá!`);
      onSuccess(created);
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } }; message?: string; errorFields?: unknown };
      if (error.errorFields) return;
      message.error(error.response?.data?.message || error.message || 'Lỗi khi lưu mã vào Master Data.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={
        <div className="flex items-center gap-2 text-slate-800 font-semibold">
          <PlusCircleOutlined className="text-blue-600 text-lg" />
          <span>Thêm nhanh vào Master Data</span>
        </div>
      }
      open={visible}
      onCancel={onCancel}
      onOk={handleSaveAndApply}
      confirmLoading={loading}
      okText="Lưu & Áp dụng"
      cancelText="Hủy"
      okButtonProps={{
        icon: <CheckCircleOutlined />,
        className: 'bg-emerald-600 hover:bg-emerald-700 text-white font-medium',
      }}
      width={560}
      destroyOnClose
    >
      <div className="py-2 space-y-4">
        <Alert
          message={
            <div className="text-xs text-amber-900 leading-relaxed">
              Mã hình thể <strong>{cleanStyleCode}</strong> chưa được đăng ký trong Master Data. Vui lòng nhập nhanh
              đơn giá và quy cách đóng gói để hệ thống áp dụng ngay vào đơn hàng và mở khóa xuất file.
            </div>
          }
          type="warning"
          showIcon
          icon={<InfoCircleOutlined className="text-amber-600" />}
          className="border-amber-200 bg-amber-50/70"
        />

        <Form form={form} layout="vertical" size="middle">
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                label={<span className="font-semibold text-xs text-slate-700">Mã hình thể (Style Code)</span>}
                name="styleCode"
                rules={[{ required: true, message: 'Vui lòng nhập mã hình thể' }]}
              >
                <Input
                  className="font-mono font-bold text-blue-700 bg-slate-50 uppercase"
                  placeholder="VD: 42072-030"
                />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                label={<span className="font-semibold text-xs text-slate-700">Quy cách (Đôi / Thùng)</span>}
                name="pairPerCarton"
                rules={[{ required: true, message: 'Nhập quy cách đóng gói' }]}
              >
                <InputNumber
                  min={1}
                  max={1000}
                  className="w-full font-mono text-right"
                  placeholder="12"
                  addonAfter="đôi/thùng"
                />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            label={<span className="font-semibold text-xs text-slate-700">Mô tả hàng hóa hải quan</span>}
            name="description"
            rules={[{ required: true, message: 'Vui lòng nhập mô tả hàng hóa' }]}
          >
            <Input.TextArea
              rows={2}
              placeholder="VD: Giày thể thao nữ buộc dây đế cao su (Women's Athletic Shoes Rubber Sole)"
              className="text-xs"
            />
          </Form.Item>

          <div className="bg-slate-50 p-3 rounded-lg border border-slate-200 space-y-3">
            <div className="flex items-center gap-1.5 text-xs font-semibold text-slate-700">
              <DollarOutlined className="text-emerald-600" />
              <span>Đơn giá tiêu chuẩn (Standard Process)</span>
            </div>
            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  label={<span className="text-xs text-slate-600">Đơn giá CMT (USD)</span>}
                  name="unitPriceCMT"
                  rules={[{ required: true, message: 'Nhập đơn giá CMT' }]}
                  className="mb-1"
                >
                  <InputNumber
                    min={0}
                    step={0.01}
                    precision={4}
                    className="w-full font-mono text-right text-xs"
                    placeholder="0.0000"
                    addonBefore="$"
                  />
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item
                  label={<span className="text-xs text-slate-600">Đơn giá DAP (USD)</span>}
                  name="unitPriceDAP"
                  rules={[{ required: true, message: 'Nhập đơn giá DAP' }]}
                  className="mb-1"
                >
                  <InputNumber
                    min={0}
                    step={0.01}
                    precision={4}
                    className="w-full font-mono text-right text-xs"
                    placeholder="0.0000"
                    addonBefore="$"
                  />
                </Form.Item>
              </Col>
            </Row>
          </div>

          {(isGoProcess || styleCode.toUpperCase().endsWith('.G')) && (
            <div className="bg-purple-50/60 p-3 rounded-lg border border-purple-200 space-y-3 mt-3">
              <div className="flex items-center justify-between text-xs font-semibold text-purple-800">
                <div className="flex items-center gap-1.5">
                  <DollarOutlined className="text-purple-600" />
                  <span>Đơn giá Gò không may riêng (.G) (Tùy chọn)</span>
                </div>
                <Text type="secondary" className="text-[11px] font-normal">
                  Để 0 nếu dùng chung giá chuẩn
                </Text>
              </div>
              <Row gutter={16}>
                <Col span={12}>
                  <Form.Item
                    label={<span className="text-xs text-purple-700">Đơn giá CMT Gò (USD)</span>}
                    name="unitPriceCMT_Go"
                    className="mb-1"
                  >
                    <InputNumber
                      min={0}
                      step={0.01}
                      precision={4}
                      className="w-full font-mono text-right text-xs"
                      placeholder="0.0000"
                      addonBefore="$"
                    />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item
                    label={<span className="text-xs text-purple-700">Đơn giá DAP Gò (USD)</span>}
                    name="unitPriceDAP_Go"
                    className="mb-1"
                  >
                    <InputNumber
                      min={0}
                      step={0.01}
                      precision={4}
                      className="w-full font-mono text-right text-xs"
                      placeholder="0.0000"
                      addonBefore="$"
                    />
                  </Form.Item>
                </Col>
              </Row>
            </div>
          )}

          <Row gutter={16} className="mt-3">
            <Col span={12}>
              <Form.Item
                label={<span className="text-xs text-slate-500">Mã HS Code</span>}
                name="hsCode"
                className="mb-0"
              >
                <Input className="font-mono text-xs" placeholder="64041990" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                label={<span className="text-xs text-slate-500">Đơn vị tính</span>}
                name="unit"
                className="mb-0"
              >
                <Input className="text-xs" placeholder="đôi" />
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </div>
    </Modal>
  );
};
