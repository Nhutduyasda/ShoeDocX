import React, { useState, useEffect } from 'react';
import {
  Modal,
  Tabs,
  Table,
  Button,
  Tag,
  Form,
  Input,
  InputNumber,
  Card,
  Space,
  Row,
  Col,
  Upload,
  Select,
  Popconfirm,
  message,
  Typography,
} from 'antd';
import {
  DownloadOutlined,
  UploadOutlined,
  CheckCircleOutlined,
  EditOutlined,
  SettingOutlined,
  FileExcelOutlined,
  SaveOutlined,
  PlusOutlined,
} from '@ant-design/icons';
import type { UploadFile } from 'antd/es/upload/interface';
import type { CompanyTemplate, DocumentTemplateConfig } from '../types';
import { templateApi } from '../api/templateApi';

const { Text } = Typography;

interface TemplateConfigModalProps {
  visible: boolean;
  onClose: () => void;
  onTemplateUpdated?: (templates: CompanyTemplate[], currentSelectedId?: number) => void;
  selectedTemplateId?: number | null;
}

export const TemplateConfigModal: React.FC<TemplateConfigModalProps> = ({
  visible,
  onClose,
  onTemplateUpdated,
  selectedTemplateId,
}) => {
  const [activeTab, setActiveTab] = useState<string>('list');
  const [templates, setTemplates] = useState<CompanyTemplate[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [editingTemplateId, setEditingTemplateId] = useState<number | null>(null);

  // Form ánh xạ tọa độ
  const [mapperForm] = Form.useForm();
  const [savingConfig, setSavingConfig] = useState<boolean>(false);

  // Form tải lên template mới
  const [uploadModalVisible, setUploadModalVisible] = useState<boolean>(false);
  const [uploadForm] = Form.useForm();
  const [uploadFileList, setUploadFileList] = useState<UploadFile[]>([]);
  const [uploading, setUploading] = useState<boolean>(false);

  const fetchTemplates = async () => {
    setLoading(true);
    try {
      const data = await templateApi.getAll();
      setTemplates(data);
      return data;
    } catch (err: any) {
      message.error('Không thể tải danh sách biểu mẫu: ' + (err?.response?.data?.message || err.message));
      return [];
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (visible) {
      fetchTemplates().then((list) => {
        const targetId = selectedTemplateId || list.find((t) => t.isDefault)?.id || list[0]?.id;
        if (targetId) {
          selectTemplateForMapping(targetId, list);
        }
      });
    }
  }, [visible, selectedTemplateId]);

  const selectTemplateForMapping = (id: number, list: CompanyTemplate[] = templates) => {
    setEditingTemplateId(id);
    const tpl = list.find((t) => t.id === id);
    if (!tpl) return;

    let cfg: DocumentTemplateConfig | null = tpl.config || null;
    if (!cfg && tpl.configJson) {
      try {
        cfg = JSON.parse(tpl.configJson);
      } catch {
        cfg = null;
      }
    }

    mapperForm.setFieldsValue({
      templateName: cfg?.templateName || tpl.name,
      // Sheet INV
      invSheetName: cfg?.invSheet?.sheetName || 'INV',
      invInvoiceNoCell: cfg?.invSheet?.header?.invoiceNoCell || 'J4',
      invDateCell: cfg?.invSheet?.header?.dateCell || 'J5',
      invContractNoCell: cfg?.invSheet?.header?.contractNoCell || 'J6',
      invBuyerNameCell: cfg?.invSheet?.header?.buyerNameCell || 'D4',
      invBuyerAddressCell: cfg?.invSheet?.header?.buyerAddressCell || 'D5',
      invDeliveryTermsCell: cfg?.invSheet?.header?.deliveryTermsCell || 'J7',
      invPaymentTermsCell: cfg?.invSheet?.header?.paymentTermsCell || 'J8',
      invDestinationCell: cfg?.invSheet?.header?.destinationCell || 'D6',

      invStartRow: cfg?.invSheet?.table?.startRow || 13,
      invItemCodeCol: cfg?.invSheet?.table?.itemCodeCol || 'C',
      invDescriptionCol: cfg?.invSheet?.table?.descriptionCol || 'D',
      invQuantityCol: cfg?.invSheet?.table?.quantityCol || 'E',
      invUnitCol: cfg?.invSheet?.table?.unitCol || 'F',
      invCmtUnitPriceCol: cfg?.invSheet?.table?.cmtUnitPriceCol || 'G',
      invDapUnitPriceCol: cfg?.invSheet?.table?.dapUnitPriceCol || 'H',
      invCmtAmountCol: cfg?.invSheet?.table?.cmtAmountCol || 'I',
      invDapAmountCol: cfg?.invSheet?.table?.dapAmountCol || 'J',

      invTotalAmountCell: cfg?.invSheet?.totalAmountCell || '',
      invWordsAmountCell: cfg?.invSheet?.wordsAmountCell || '',

      // Sheet PKL
      pklSheetName: cfg?.pklSheet?.sheetName || 'PKL',
      pklStartRow: cfg?.pklSheet?.startRow || 12,
      pklCartonRangeCol: cfg?.pklSheet?.cartonRangeCol || 'A',
      pklItemCodeCol: cfg?.pklSheet?.itemCodeCol || 'B',
      pklDescriptionCol: cfg?.pklSheet?.descriptionCol || 'C',
      pklQuantityCol: cfg?.pklSheet?.quantityCol || 'D',
      pklUnitCol: cfg?.pklSheet?.unitCol || 'E',
      pklCartonsCol: cfg?.pklSheet?.cartonsCol || 'F',
      pklNetWeightCol: cfg?.pklSheet?.netWeightCol || 'G',
      pklGrossWeightCol: cfg?.pklSheet?.grossWeightCol || 'H',
    });
  };

  const handleSaveConfig = async () => {
    if (!editingTemplateId) {
      message.warning('Vui lòng chọn biểu mẫu cần lưu cấu hình');
      return;
    }

    try {
      const values = await mapperForm.validateFields();
      setSavingConfig(true);

      const updatedConfig: DocumentTemplateConfig = {
        templateName: values.templateName,
        invSheet: {
          sheetName: values.invSheetName,
          header: {
            invoiceNoCell: values.invInvoiceNoCell?.toUpperCase(),
            dateCell: values.invDateCell?.toUpperCase(),
            contractNoCell: values.invContractNoCell?.toUpperCase(),
            buyerNameCell: values.invBuyerNameCell?.toUpperCase(),
            buyerAddressCell: values.invBuyerAddressCell?.toUpperCase(),
            deliveryTermsCell: values.invDeliveryTermsCell?.toUpperCase(),
            paymentTermsCell: values.invPaymentTermsCell?.toUpperCase(),
            destinationCell: values.invDestinationCell?.toUpperCase(),
          },
          table: {
            startRow: Number(values.invStartRow),
            itemCodeCol: values.invItemCodeCol?.toUpperCase(),
            descriptionCol: values.invDescriptionCol?.toUpperCase(),
            quantityCol: values.invQuantityCol?.toUpperCase(),
            unitCol: values.invUnitCol?.toUpperCase(),
            cmtUnitPriceCol: values.invCmtUnitPriceCol?.toUpperCase(),
            dapUnitPriceCol: values.invDapUnitPriceCol?.toUpperCase(),
            cmtAmountCol: values.invCmtAmountCol?.toUpperCase(),
            dapAmountCol: values.invDapAmountCol?.toUpperCase(),
          },
          totalAmountCell: values.invTotalAmountCell ? values.invTotalAmountCell.toUpperCase() : undefined,
          wordsAmountCell: values.invWordsAmountCell ? values.invWordsAmountCell.toUpperCase() : undefined,
        },
        pklSheet: {
          sheetName: values.pklSheetName,
          startRow: Number(values.pklStartRow),
          cartonRangeCol: values.pklCartonRangeCol?.toUpperCase(),
          itemCodeCol: values.pklItemCodeCol?.toUpperCase(),
          descriptionCol: values.pklDescriptionCol?.toUpperCase(),
          quantityCol: values.pklQuantityCol?.toUpperCase(),
          unitCol: values.pklUnitCol?.toUpperCase(),
          cartonsCol: values.pklCartonsCol?.toUpperCase(),
          netWeightCol: values.pklNetWeightCol?.toUpperCase(),
          grossWeightCol: values.pklGrossWeightCol?.toUpperCase(),
        },
      };

      const json = JSON.stringify(updatedConfig, null, 2);
      await templateApi.updateConfig(editingTemplateId, json);
      message.success('Cập nhật cấu hình tọa độ mẫu thành công!');
      const refreshed = await fetchTemplates();
      onTemplateUpdated?.(refreshed, editingTemplateId);
    } catch (err: any) {
      if (err?.errorFields) return;
      message.error('Lỗi lưu cấu hình: ' + (err?.response?.data?.message || err.message));
    } finally {
      setSavingConfig(false);
    }
  };

  const handleSetDefault = async (id: number) => {
    try {
      await templateApi.setDefault(id);
      message.success('Đã đặt làm biểu mẫu mặc định!');
      const refreshed = await fetchTemplates();
      onTemplateUpdated?.(refreshed, id);
    } catch (err: any) {
      message.error('Không thể đặt làm mặc định: ' + (err?.response?.data?.message || err.message));
    }
  };

  const handleDownload = async (id: number, fileName: string) => {
    try {
      await templateApi.downloadTemplate(id, fileName);
      message.success('Tải file mẫu thành công!');
    } catch (err: any) {
      message.error('Tải file mẫu thất bại: ' + (err?.response?.data?.message || err.message));
    }
  };

  const handleUploadSubmit = async () => {
    try {
      const values = await uploadForm.validateFields();
      if (uploadFileList.length === 0) {
        message.warning('Vui lòng chọn file mẫu Excel (.xlsx)');
        return;
      }
      const file = uploadFileList[0].originFileObj as File;
      setUploading(true);

      const newTpl = await templateApi.uploadTemplate(file, values.name, undefined, values.isDefault);
      message.success('Tải lên biểu mẫu thành công!');
      setUploadModalVisible(false);
      uploadForm.resetFields();
      setUploadFileList([]);
      const refreshed = await fetchTemplates();
      selectTemplateForMapping(newTpl.id, refreshed);
      onTemplateUpdated?.(refreshed, newTpl.id);
    } catch (err: any) {
      if (err?.errorFields) return;
      message.error('Tải lên thất bại: ' + (err?.response?.data?.message || err.message));
    } finally {
      setUploading(false);
    }
  };

  const columns = [
    {
      title: 'Tên Biểu Mẫu',
      dataIndex: 'name',
      key: 'name',
      render: (text: string, record: CompanyTemplate) => (
        <Space>
          <FileExcelOutlined style={{ color: '#107c41', fontSize: '18px' }} />
          <Text strong>{text}</Text>
          {record.isDefault && <Tag color="green">Mặc định</Tag>}
        </Space>
      ),
    },
    {
      title: 'File Phôi Excel',
      dataIndex: 'templateFileName',
      key: 'templateFileName',
      render: (val: string) => <Text code>{val}</Text>,
    },
    {
      title: 'Ngày cập nhật',
      dataIndex: 'updatedAt',
      key: 'updatedAt',
      render: (val?: string, record?: CompanyTemplate) => {
        const d = val || record?.createdAt;
        return d ? new Date(d).toLocaleDateString('vi-VN') : '-';
      },
    },
    {
      title: 'Thao tác',
      key: 'action',
      width: 320,
      render: (_: any, record: CompanyTemplate) => (
        <Space size="small">
          <Button
            size="small"
            icon={<EditOutlined />}
            type="primary"
            ghost
            onClick={() => {
              selectTemplateForMapping(record.id);
              setActiveTab('mapper');
            }}
          >
            Sửa tọa độ
          </Button>
          <Button
            size="small"
            icon={<DownloadOutlined />}
            onClick={() => handleDownload(record.id, record.templateFileName)}
          >
            Tải mẫu
          </Button>
          {!record.isDefault && (
            <Popconfirm
              title="Đặt làm biểu mẫu mặc định?"
              description="Hệ thống sẽ tự động dùng biểu mẫu này cho toàn bộ hóa đơn xuất khẩu."
              onConfirm={() => handleSetDefault(record.id)}
              okText="Đồng ý"
              cancelText="Hủy"
            >
              <Button size="small" icon={<CheckCircleOutlined />}>
                Đặt mặc định
              </Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ];

  return (
    <>
      <Modal
        title={
          <Space>
            <SettingOutlined style={{ color: '#1890ff' }} />
            <span>Quản Lý Biểu Mẫu & Tùy Biến Tọa Độ Xuất Excel (BYOT)</span>
          </Space>
        }
        open={visible}
        onCancel={onClose}
        width={960}
        footer={[
          <Button key="close" onClick={onClose}>
            Đóng
          </Button>,
          activeTab === 'mapper' && (
            <Button
              key="save"
              type="primary"
              icon={<SaveOutlined />}
              loading={savingConfig}
              onClick={handleSaveConfig}
            >
              Lưu Cấu Hình Tọa Độ
            </Button>
          ),
        ]}
      >
        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          items={[
            {
              key: 'list',
              label: (
                <span>
                  <FileExcelOutlined /> Danh sách Biểu mẫu ({templates.length})
                </span>
              ),
              children: (
                <div>
                  <div className="flex justify-between items-center mb-4">
                    <Text type="secondary">
                      Hệ thống tự động nạp các biểu mẫu Excel riêng biệt theo đối tác hoặc quy cách doanh nghiệp.
                    </Text>
                    <Button
                      type="primary"
                      icon={<PlusOutlined />}
                      onClick={() => setUploadModalVisible(true)}
                    >
                      Thêm Mẫu Phôi Mới
                    </Button>
                  </div>
                  <Table
                    dataSource={templates}
                    columns={columns}
                    rowKey="id"
                    loading={loading}
                    pagination={false}
                    size="middle"
                  />
                </div>
              ),
            },
            {
              key: 'mapper',
              label: (
                <span>
                  <SettingOutlined /> Trình Ánh Xạ Tọa Độ (Manual Coordinate Mapper)
                </span>
              ),
              children: (
                <div>
                  <div className="bg-blue-50 border border-blue-200 rounded p-3 mb-4 flex items-center justify-between">
                    <div>
                      <Text strong className="text-blue-800">
                        Đang cấu hình biểu mẫu:{' '}
                      </Text>
                      <Select
                        style={{ width: 340, marginLeft: 8 }}
                        value={editingTemplateId ?? undefined}
                        onChange={(val) => selectTemplateForMapping(val)}
                        options={templates.map((t) => ({
                          label: `${t.name} ${t.isDefault ? '(Mặc định)' : ''}`,
                          value: t.id,
                        }))}
                      />
                    </div>
                    <Text type="secondary" className="text-xs">
                      Tọa độ dạng ô Excel (Vd: J4, D4) hoặc cột chữ cái (Vd: C, D, E)
                    </Text>
                  </div>

                  <Form form={mapperForm} layout="vertical">
                    <Form.Item
                      name="templateName"
                      label="Tên hiển thị biểu mẫu"
                      rules={[{ required: true, message: 'Vui lòng nhập tên biểu mẫu' }]}
                    >
                      <Input placeholder="Vd: Mẫu Tiêu Chuẩn Kingmaker / Hải An" />
                    </Form.Item>

                    <Tabs
                      type="card"
                      items={[
                        {
                          key: 'inv-tab',
                          label: 'Sheet Hóa Đơn (INV)',
                          children: (
                            <div className="space-y-4">
                              <Card size="small" title="1. Thông tin Tiêu Đề (INV Header Cells)" className="bg-gray-50">
                                <Row gutter={16}>
                                  <Col span={6}>
                                    <Form.Item name="invSheetName" label="Tên Sheet INV" rules={[{ required: true }]}>
                                      <Input placeholder="INV" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={6}>
                                    <Form.Item name="invInvoiceNoCell" label="Ô Số Hóa Đơn (Inv No)" rules={[{ required: true }]}>
                                      <Input placeholder="J4" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={6}>
                                    <Form.Item name="invDateCell" label="Ô Ngày Hóa Đơn (Date)" rules={[{ required: true }]}>
                                      <Input placeholder="J5" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={6}>
                                    <Form.Item name="invContractNoCell" label="Ô Số Hợp Đồng (Contract No)" rules={[{ required: true }]}>
                                      <Input placeholder="J6" />
                                    </Form.Item>
                                  </Col>
                                </Row>

                                <Row gutter={16}>
                                  <Col span={6}>
                                    <Form.Item name="invBuyerNameCell" label="Ô Tên Khách Hàng (Buyer)" rules={[{ required: true }]}>
                                      <Input placeholder="D4" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={6}>
                                    <Form.Item name="invBuyerAddressCell" label="Ô Địa Chỉ Khách Hàng" rules={[{ required: true }]}>
                                      <Input placeholder="D5" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={6}>
                                    <Form.Item name="invDeliveryTermsCell" label="Ô Điều Kiện Giao Hàng" rules={[{ required: true }]}>
                                      <Input placeholder="J7" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={6}>
                                    <Form.Item name="invPaymentTermsCell" label="Ô Điều Khoản Thanh Toán" rules={[{ required: true }]}>
                                      <Input placeholder="J8" />
                                    </Form.Item>
                                  </Col>
                                </Row>

                                <Row gutter={16}>
                                  <Col span={6}>
                                    <Form.Item name="invDestinationCell" label="Ô Nước Đến (Destination)">
                                      <Input placeholder="D6" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={9}>
                                    <Form.Item name="invTotalAmountCell" label="Ô Tổng Tiền Số (Tùy chọn)">
                                      <Input placeholder="Vd: J42 (Để trống = tự tính theo bảng)" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={9}>
                                    <Form.Item name="invWordsAmountCell" label="Ô Tổng Tiền Bằng Chữ (Tùy chọn)">
                                      <Input placeholder="Vd: A44 (Để trống = tự tìm 'Bằng chữ:')" />
                                    </Form.Item>
                                  </Col>
                                </Row>
                              </Card>

                              <Card size="small" title="2. Cột Dữ Liệu Bảng INV (Table Columns)" className="bg-gray-50">
                                <Row gutter={16}>
                                  <Col span={6}>
                                    <Form.Item name="invStartRow" label="Dòng bắt đầu điền hàng" rules={[{ required: true }]}>
                                      <InputNumber min={1} style={{ width: '100%' }} placeholder="13" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={6}>
                                    <Form.Item name="invItemCodeCol" label="Cột Mã Hàng" rules={[{ required: true }]}>
                                      <Input placeholder="C" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={6}>
                                    <Form.Item name="invDescriptionCol" label="Cột Tên / Mô Tả" rules={[{ required: true }]}>
                                      <Input placeholder="D" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={6}>
                                    <Form.Item name="invQuantityCol" label="Cột Số Lượng" rules={[{ required: true }]}>
                                      <Input placeholder="E" />
                                    </Form.Item>
                                  </Col>
                                </Row>

                                <Row gutter={16}>
                                  <Col span={6}>
                                    <Form.Item name="invUnitCol" label="Cột Đơn Vị Tính" rules={[{ required: true }]}>
                                      <Input placeholder="F" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={6}>
                                    <Form.Item name="invCmtUnitPriceCol" label="Cột Đơn Giá CMT" rules={[{ required: true }]}>
                                      <Input placeholder="G" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={6}>
                                    <Form.Item name="invDapUnitPriceCol" label="Cột Đơn Giá DAP" rules={[{ required: true }]}>
                                      <Input placeholder="H" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={3}>
                                    <Form.Item name="invCmtAmountCol" label="Cột Tiền CMT" rules={[{ required: true }]}>
                                      <Input placeholder="I" />
                                    </Form.Item>
                                  </Col>
                                  <Col span={3}>
                                    <Form.Item name="invDapAmountCol" label="Cột Tiền DAP" rules={[{ required: true }]}>
                                      <Input placeholder="J" />
                                    </Form.Item>
                                  </Col>
                                </Row>
                              </Card>
                            </div>
                          ),
                        },
                        {
                          key: 'pkl-tab',
                          label: 'Sheet Bảng Kê Đóng Gói (PKL)',
                          children: (
                            <Card size="small" title="Cấu hình Tọa Độ Sheet PKL" className="bg-gray-50">
                              <Row gutter={16}>
                                <Col span={8}>
                                  <Form.Item name="pklSheetName" label="Tên Sheet PKL" rules={[{ required: true }]}>
                                    <Input placeholder="PKL" />
                                  </Form.Item>
                                </Col>
                                <Col span={8}>
                                  <Form.Item name="pklStartRow" label="Dòng bắt đầu điền kiện" rules={[{ required: true }]}>
                                    <InputNumber min={1} style={{ width: '100%' }} placeholder="12" />
                                  </Form.Item>
                                </Col>
                                <Col span={8}>
                                  <Form.Item name="pklCartonRangeCol" label="Cột Dải Số Kiện (Carton Range)" rules={[{ required: true }]}>
                                    <Input placeholder="A" />
                                  </Form.Item>
                                </Col>
                              </Row>

                              <Row gutter={16}>
                                <Col span={6}>
                                  <Form.Item name="pklItemCodeCol" label="Cột Mã Hàng" rules={[{ required: true }]}>
                                    <Input placeholder="B" />
                                  </Form.Item>
                                </Col>
                                <Col span={6}>
                                  <Form.Item name="pklDescriptionCol" label="Cột Tên / Mô Tả" rules={[{ required: true }]}>
                                    <Input placeholder="C" />
                                  </Form.Item>
                                </Col>
                                <Col span={6}>
                                  <Form.Item name="pklQuantityCol" label="Cột Số Lượng" rules={[{ required: true }]}>
                                    <Input placeholder="D" />
                                  </Form.Item>
                                </Col>
                                <Col span={6}>
                                  <Form.Item name="pklUnitCol" label="Cột Đơn Vị Tính" rules={[{ required: true }]}>
                                    <Input placeholder="E" />
                                  </Form.Item>
                                </Col>
                              </Row>

                              <Row gutter={16}>
                                <Col span={8}>
                                  <Form.Item name="pklCartonsCol" label="Cột Số Thùng (Cartons)" rules={[{ required: true }]}>
                                    <Input placeholder="F" />
                                  </Form.Item>
                                </Col>
                                <Col span={8}>
                                  <Form.Item name="pklNetWeightCol" label="Cột Trọng Lượng Tịnh (Net Weight)" rules={[{ required: true }]}>
                                    <Input placeholder="G" />
                                  </Form.Item>
                                </Col>
                                <Col span={8}>
                                  <Form.Item name="pklGrossWeightCol" label="Cột Trọng Lượng Cả Bì (Gross Weight)" rules={[{ required: true }]}>
                                    <Input placeholder="H" />
                                  </Form.Item>
                                </Col>
                              </Row>
                            </Card>
                          ),
                        },
                      ]}
                    />
                  </Form>
                </div>
              ),
            },
          ]}
        />
      </Modal>

      {/* Sub-modal: Tải lên mẫu phôi mới */}
      <Modal
        title="Tải Lên Mẫu Phôi Excel Mới"
        open={uploadModalVisible}
        onCancel={() => {
          setUploadModalVisible(false);
          uploadForm.resetFields();
          setUploadFileList([]);
        }}
        onOk={handleUploadSubmit}
        confirmLoading={uploading}
        okText="Tải lên & Khởi tạo"
        cancelText="Hủy"
      >
        <Form form={uploadForm} layout="vertical">
          <Form.Item
            name="name"
            label="Tên Biểu Mẫu"
            rules={[{ required: true, message: 'Vui lòng nhập tên biểu mẫu' }]}
          >
            <Input placeholder="Vd: Mẫu Phôi Đối Tác Nike / Adidas" />
          </Form.Item>

          <Form.Item label="File Phôi Excel (.xlsx)" required>
            <Upload
              fileList={uploadFileList}
              beforeUpload={(file) => {
                const isExcel =
                  file.type === 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' ||
                  file.name.endsWith('.xlsx');
                if (!isExcel) {
                  message.error('Vui lòng chỉ chọn tệp tin Excel định dạng .xlsx');
                  return Upload.LIST_IGNORE;
                }
                setUploadFileList([file]);
                return false;
              }}
              onRemove={() => setUploadFileList([])}
              maxCount={1}
            >
              <Button icon={<UploadOutlined />}>Chọn tệp .xlsx từ máy tính</Button>
            </Upload>
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
};
