import React, { useEffect } from 'react';
import { Modal, Form, Input, Radio, TreeSelect, message } from 'antd';
import type { MasterDataFolder, CreateFolderRequest, UpdateFolderRequest } from '../types';
import { masterDataFolderApi } from '../api/masterDataFolderApi';

interface FolderConfigModalProps {
  visible: boolean;
  folder: MasterDataFolder | null; // null => Thêm mới, khác null => Sửa
  parentFolderId?: number | null; // thư mục cha gợi ý khi tạo con
  treeData: MasterDataFolder[];
  onCancel: () => void;
  onSuccess: () => void;
}

export const FolderConfigModal: React.FC<FolderConfigModalProps> = ({
  visible,
  folder,
  parentFolderId,
  treeData,
  onCancel,
  onSuccess,
}) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = React.useState(false);
  const isEdit = !!folder;

  // Chuyển treeData sang dạng dùng cho TreeSelect
  const formatTreeSelect = (nodes: MasterDataFolder[]): any[] => {
    return nodes
      .filter((n) => !isEdit || n.id !== folder?.id) // Không cho chọn chính nó làm cha
      .map((node) => ({
        title: `${node.name} (${node.defaultPairsPerCarton} đôi/thùng)`,
        value: node.id,
        key: node.id,
        children: node.children ? formatTreeSelect(node.children) : [],
      }));
  };

  useEffect(() => {
    if (visible) {
      if (folder) {
        form.setFieldsValue({
          name: folder.name,
          parentId: folder.parentId ?? undefined,
          customerName: folder.customerName,
          deliveryAddress: folder.deliveryAddress,
          contractNo: folder.contractNo,
          poSuffix: folder.poSuffix,
          defaultPairsPerCarton: folder.defaultPairsPerCarton || 12,
          defaultUnit: folder.defaultUnit || 'PRS',
        });
      } else {
        form.resetFields();
        form.setFieldsValue({
          parentId: parentFolderId ?? undefined,
          defaultPairsPerCarton: 12,
          defaultUnit: 'PRS',
        });
      }
    }
  }, [visible, folder, parentFolderId, form]);

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      if (isEdit && folder) {
        const updatePayload: UpdateFolderRequest = {
          name: values.name.trim(),
          customerName: values.customerName?.trim() || null,
          deliveryAddress: values.deliveryAddress?.trim() || null,
          contractNo: values.contractNo?.trim() || null,
          poSuffix: values.poSuffix?.trim() || null,
          defaultPairsPerCarton: values.defaultPairsPerCarton || 12,
          defaultUnit: values.defaultUnit?.trim() || 'PRS',
        };
        await masterDataFolderApi.update(folder.id, updatePayload);
        message.success(`Đã cập nhật thư mục: ${values.name}`);
      } else {
        const createPayload: CreateFolderRequest = {
          name: values.name.trim(),
          parentId: values.parentId ?? null,
          customerName: values.customerName?.trim() || null,
          deliveryAddress: values.deliveryAddress?.trim() || null,
          contractNo: values.contractNo?.trim() || null,
          poSuffix: values.poSuffix?.trim() || null,
          defaultPairsPerCarton: values.defaultPairsPerCarton || 12,
          defaultUnit: values.defaultUnit?.trim() || 'PRS',
        };
        await masterDataFolderApi.create(createPayload);
        message.success(`Đã tạo mới thư mục: ${values.name}`);
      }

      onSuccess();
    } catch (err: any) {
      if (err.errorFields) return; // Validation error
      const msg = err.response?.data?.message || err.message || 'Lỗi khi lưu thư mục';
      message.error(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={
        <div className="text-sm font-semibold text-slate-900">
          {isEdit ? `Cấu hình Thư mục: ${folder?.name}` : 'Tạo Thư mục Master Data Mới'}
        </div>
      }
      open={visible}
      onCancel={onCancel}
      onOk={handleSubmit}
      confirmLoading={loading}
      okText={isEdit ? 'Lưu thay đổi' : 'Tạo thư mục'}
      cancelText="Hủy"
      width={520}
      destroyOnClose
    >
      <Form form={form} layout="vertical" className="mt-4">
        <Form.Item
          name="name"
          label={<span className="text-xs font-medium text-slate-700">Tên Thư mục / Hợp đồng / Mùa</span>}
          rules={[{ required: true, message: 'Vui lòng nhập tên thư mục' }]}
        >
          <Input placeholder="Ví dụ: Kingmaker III, NewBalance, Mùa Hè 2026..." className="text-xs" />
        </Form.Item>

        {!isEdit && (
          <Form.Item
            name="parentId"
            label={<span className="text-xs font-medium text-slate-700">Thư mục cha (để trống nếu là cấp cao nhất)</span>}
          >
            <TreeSelect
              treeData={formatTreeSelect(treeData)}
              placeholder="Chọn thư mục cha (hoặc để trống)"
              allowClear
              treeDefaultExpandAll
              className="text-xs"
            />
          </Form.Item>
        )}

        <div className="grid grid-cols-2 gap-3">
          <Form.Item
            name="customerName"
            label={<span className="text-xs font-medium text-slate-700">Đối tác / Tên Khách hàng</span>}
          >
            <Input placeholder="Ví dụ: CÔNG TY TNHH KINGMAKER III..." className="text-xs" />
          </Form.Item>

          <Form.Item
            name="contractNo"
            label={<span className="text-xs font-medium text-slate-700">Số Hợp đồng mặc định</span>}
          >
            <Input placeholder="Ví dụ: KM-HANEW/01-2025..." className="text-xs" />
          </Form.Item>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <Form.Item
            name="deliveryAddress"
            label={<span className="text-xs font-medium text-slate-700">Địa chỉ giao hàng mặc định</span>}
          >
            <Input placeholder="Ví dụ: SỐ 1, ĐƯỜNG 4A, KCN VSIP..." className="text-xs" />
          </Form.Item>

          <Form.Item
            name="poSuffix"
            label={<span className="text-xs font-medium text-slate-700">Đuôi PO mặc định (PoSuffix)</span>}
          >
            <Input placeholder="Ví dụ: (KM3.PO5.26)..." className="text-xs" />
          </Form.Item>
        </div>

        <Form.Item
          name="defaultPairsPerCarton"
          label={<span className="text-xs font-medium text-slate-700">Quy cách đóng gói mặc định (Đôi / Thùng)</span>}
          rules={[{ required: true, message: 'Vui lòng chọn quy cách đóng gói' }]}
          extra="Các mã sản phẩm import vào thư mục này sẽ tự động áp dụng quy cách này."
        >
          <Radio.Group className="w-full">
            <div className="flex gap-4 items-center">
              <Radio value={12}>
                <span className="font-semibold text-xs text-blue-700">12 đôi / thùng</span> (Chuẩn Kingmaker III)
              </Radio>
              <Radio value={24}>
                <span className="font-semibold text-xs text-emerald-700">24 đôi / thùng</span> (Đối tác mới)
              </Radio>
            </div>
          </Radio.Group>
        </Form.Item>

        <Form.Item
          name="defaultUnit"
          label={<span className="text-xs font-medium text-slate-700">Đơn vị tính mặc định</span>}
        >
          <Radio.Group>
            <Radio value="PRS">PRS (Cặp/Đôi chuẩn QT)</Radio>
            <Radio value="PR">PR</Radio>
            <Radio value="đôi">đôi</Radio>
          </Radio.Group>
        </Form.Item>
      </Form>
    </Modal>
  );
};
