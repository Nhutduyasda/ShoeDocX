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
  Spin,
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
  ThunderboltOutlined,
  EyeOutlined,
  LoadingOutlined,
  FileTextOutlined,
  InboxOutlined,
  BulbOutlined,
} from '@ant-design/icons';
import type { UploadFile } from 'antd/es/upload/interface';
import type {
  CompanyTemplate,
  DocumentTemplateConfig,
  AiTemplateAnalysisResponse,
  DocumentPreviewResponse,
  InvoicePreviewItem,
  PklBreakdownItem,
} from '../types';
import { templateApi } from '../api/templateApi';

const { Text } = Typography;

const format2 = (val: number) =>
  (val || 0).toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });

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

  // AI Smart Template Parser & Interactive Live Preview state
  const [isAiAnalyzing, setIsAiAnalyzing] = useState<boolean>(false);
  const [aiLoadingStep, setAiLoadingStep] = useState<number>(0);
  const aiSteps = [
    'Đang đọc ma trận các ô văn bản Excel (INV & PKL)...',
    'AI phân tích ngữ nghĩa vị trí Số HĐ, Ngày, Bên mua & các cột...',
    'Đang khởi tạo dữ liệu mẫu và lập bản xem trước Live Preview...',
  ];
  const [livePreviewModalVisible, setLivePreviewModalVisible] = useState<boolean>(false);
  const [livePreviewData, setLivePreviewData] = useState<DocumentPreviewResponse | null>(null);
  const [detectedAiResult, setDetectedAiResult] = useState<AiTemplateAnalysisResponse | null>(null);
  const [uploadedFileForPreview, setUploadedFileForPreview] = useState<File | null>(null);
  const [loadingPreview, setLoadingPreview] = useState<boolean>(false);
  const [previewActiveTab, setPreviewActiveTab] = useState<'inv' | 'pkl'>('inv');

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

  const handleAiAnalyze = async () => {
    if (uploadFileList.length === 0) {
      message.warning('Vui lòng chọn file mẫu Excel (.xlsx) trước khi phân tích AI');
      return;
    }
    const file = uploadFileList[0].originFileObj as File;
    setIsAiAnalyzing(true);
    setAiLoadingStep(0);

    const stepTimer = setInterval(() => {
      setAiLoadingStep((prev) => (prev + 1) % aiSteps.length);
    }, 1200);

    try {
      const result = await templateApi.aiAnalyze(file);
      // Tự động tạo bản xem trước Live Preview với dữ liệu mẫu
      const preview = await templateApi.previewWithConfig(file, JSON.stringify(result.config));

      clearInterval(stepTimer);
      setDetectedAiResult(result);
      setUploadedFileForPreview(file);
      setLivePreviewData(preview);
      setUploadModalVisible(false);
      setLivePreviewModalVisible(true);
      message.success('AI phân tích biểu mẫu và tạo bản xem trước thành công!');
    } catch (err: any) {
      clearInterval(stepTimer);
      message.error('Phân tích AI thất bại: ' + (err?.response?.data?.message || err.message));
    } finally {
      clearInterval(stepTimer);
      setIsAiAnalyzing(false);
    }
  };

  const handleConfirmAndUseTemplate = async () => {
    if (!uploadedFileForPreview || !detectedAiResult) return;
    try {
      setUploading(true);
      const name = detectedAiResult.detectedName || uploadForm.getFieldValue('name') || 'Mẫu Mới';
      const configJson = JSON.stringify(detectedAiResult.config, null, 2);
      const newTpl = await templateApi.uploadTemplate(uploadedFileForPreview, name, configJson, true);
      message.success(`Đã lưu và áp dụng biểu mẫu "${newTpl.name}" làm mặc định thành công!`);

      setLivePreviewModalVisible(false);
      uploadForm.resetFields();
      setUploadFileList([]);
      setUploadedFileForPreview(null);
      setDetectedAiResult(null);

      const refreshed = await fetchTemplates();
      selectTemplateForMapping(newTpl.id, refreshed);
      onTemplateUpdated?.(refreshed, newTpl.id);
    } catch (err: any) {
      message.error('Lưu biểu mẫu thất bại: ' + (err?.response?.data?.message || err.message));
    } finally {
      setUploading(false);
    }
  };

  const handleEditCoordinatesFromAi = () => {
    if (!detectedAiResult) return;
    const cfg = detectedAiResult.config;
    mapperForm.setFieldsValue({
      templateName: cfg.templateName || detectedAiResult.detectedName,
      invSheetName: cfg.invSheet?.sheetName || 'INV',
      invInvoiceNoCell: cfg.invSheet?.header?.invoiceNoCell || 'J4',
      invDateCell: cfg.invSheet?.header?.dateCell || 'J5',
      invContractNoCell: cfg.invSheet?.header?.contractNoCell || 'J6',
      invBuyerNameCell: cfg.invSheet?.header?.buyerNameCell || 'D4',
      invBuyerAddressCell: cfg.invSheet?.header?.buyerAddressCell || 'D5',
      invDeliveryTermsCell: cfg.invSheet?.header?.deliveryTermsCell || 'J7',
      invPaymentTermsCell: cfg.invSheet?.header?.paymentTermsCell || 'J8',
      invDestinationCell: cfg.invSheet?.header?.destinationCell || 'D6',

      invStartRow: cfg.invSheet?.table?.startRow || 13,
      invItemCodeCol: cfg.invSheet?.table?.itemCodeCol || 'C',
      invDescriptionCol: cfg.invSheet?.table?.descriptionCol || 'D',
      invQuantityCol: cfg.invSheet?.table?.quantityCol || 'E',
      invUnitCol: cfg.invSheet?.table?.unitCol || 'F',
      invCmtUnitPriceCol: cfg.invSheet?.table?.cmtUnitPriceCol || 'G',
      invDapUnitPriceCol: cfg.invSheet?.table?.dapUnitPriceCol || 'H',
      invCmtAmountCol: cfg.invSheet?.table?.cmtAmountCol || 'I',
      invDapAmountCol: cfg.invSheet?.table?.dapAmountCol || 'J',

      invTotalAmountCell: cfg.invSheet?.totalAmountCell || '',
      invWordsAmountCell: cfg.invSheet?.wordsAmountCell || '',

      pklSheetName: cfg.pklSheet?.sheetName || 'PKL',
      pklStartRow: cfg.pklSheet?.startRow || 12,
      pklCartonRangeCol: cfg.pklSheet?.cartonRangeCol || 'A',
      pklItemCodeCol: cfg.pklSheet?.itemCodeCol || 'B',
      pklDescriptionCol: cfg.pklSheet?.descriptionCol || 'C',
      pklQuantityCol: cfg.pklSheet?.quantityCol || 'D',
      pklUnitCol: cfg.pklSheet?.unitCol || 'E',
      pklCartonsCol: cfg.pklSheet?.cartonsCol || 'F',
      pklNetWeightCol: cfg.pklSheet?.netWeightCol || 'G',
      pklGrossWeightCol: cfg.pklSheet?.grossWeightCol || 'H',
    });

    setLivePreviewModalVisible(false);
    setActiveTab('mapper');
    message.info('Đã nạp tọa độ từ AI vào Trình ánh xạ. Bạn có thể kiểm tra và lưu cấu hình.');
  };

  const handlePreviewFromMapper = async () => {
    try {
      const values = await mapperForm.validateFields();
      setLoadingPreview(true);

      const cfg: DocumentTemplateConfig = {
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

      const preview = await templateApi.previewWithConfig(null, JSON.stringify(cfg), editingTemplateId);
      setDetectedAiResult({
        detectedName: cfg.templateName,
        config: cfg,
        textGrid: '',
        isAiAnalyzed: false,
      });
      setUploadedFileForPreview(null);
      setLivePreviewData(preview);
      setLivePreviewModalVisible(true);
    } catch (err: any) {
      if (err?.errorFields) return;
      message.error('Không thể tạo bản xem trước: ' + (err?.response?.data?.message || err.message));
    } finally {
      setLoadingPreview(false);
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
              key="preview"
              icon={<EyeOutlined />}
              loading={loadingPreview}
              onClick={handlePreviewFromMapper}
            >
              Xem thử dữ liệu mẫu
            </Button>
          ),
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

      {/* Sub-modal: Tải lên mẫu phôi mới với AI Onboarding */}
      <Modal
        title={
          <Space>
            <UploadOutlined style={{ color: '#1890ff' }} />
            <span>Tải Lên Mẫu Phôi Excel Mới (BYOT - Bring Your Own Template)</span>
          </Space>
        }
        open={uploadModalVisible}
        onCancel={() => {
          if (isAiAnalyzing) return;
          setUploadModalVisible(false);
          uploadForm.resetFields();
          setUploadFileList([]);
        }}
        footer={null}
        width={680}
      >
        <Form form={uploadForm} layout="vertical">
          <Form.Item
            name="name"
            label="Tên Biểu Mẫu"
            rules={[{ required: true, message: 'Vui lòng nhập tên biểu mẫu' }]}
          >
            <Input placeholder="Vd: Mẫu Phôi Đối Tác Nike / Kingmaker" />
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
                // Tự động gợi ý tên biểu mẫu từ tên tệp nếu người dùng chưa nhập
                if (!uploadForm.getFieldValue('name')) {
                  const rawName = file.name.replace(/\.[^/.]+$/, '').replace(/[_\-\.]+/g, ' ');
                  uploadForm.setFieldsValue({ name: `Mẫu ${rawName}` });
                }
                return false;
              }}
              onRemove={() => {
                setUploadFileList([]);
              }}
              maxCount={1}
            >
              <Button icon={<UploadOutlined />}>Chọn tệp .xlsx từ máy tính</Button>
            </Upload>
          </Form.Item>

          {/* AI Onboarding Callout khi đã chọn file */}
          {uploadFileList.length > 0 && (
            <div className="mt-4 p-4 rounded-lg bg-gradient-to-br from-indigo-50 via-purple-50 to-pink-50 border border-purple-200">
              <div className="flex items-center gap-2 mb-2">
                <ThunderboltOutlined className="text-purple-600 text-lg" />
                <Text strong className="text-purple-900 text-base">
                  Cấu hình Biểu mẫu Thông minh với AI
                </Text>
              </div>
              <p className="text-xs text-slate-600 mb-4">
                Hệ thống sẽ tự động quét ma trận các ô văn bản của phôi Excel, định vị các ô Số HĐ, Ngày, Bên mua, bảng chi tiết INV & PKL, đồng thời tạo bản xem trước Live Preview với dữ liệu mẫu hoàn chỉnh.
              </p>

              {isAiAnalyzing ? (
                <div className="py-4 text-center bg-white/80 rounded-md border border-purple-200">
                  <Spin indicator={<LoadingOutlined style={{ fontSize: 28, color: '#722ed1' }} spin />} />
                  <div className="mt-3 font-medium text-purple-800 text-sm animate-pulse">
                    {aiSteps[aiLoadingStep]}
                  </div>
                  <div className="text-xs text-slate-500 mt-1">
                    Đang xử lý ma trận ClosedXML và ngữ nghĩa AI...
                  </div>
                </div>
              ) : (
                <div className="flex flex-col sm:flex-row items-center gap-3">
                  <Button
                    type="primary"
                    size="large"
                    icon={<ThunderboltOutlined />}
                    onClick={handleAiAnalyze}
                    className="w-full sm:w-auto flex-1 bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-700 hover:to-indigo-700 border-none text-white font-semibold shadow-md"
                  >
                    ✨ Phân tích tự động bằng AI
                  </Button>
                  <Button
                    size="large"
                    icon={<EditOutlined />}
                    onClick={handleUploadSubmit}
                    loading={uploading}
                    className="w-full sm:w-auto"
                  >
                    📝 Tự nhập tọa độ thủ công
                  </Button>
                </div>
              )}
            </div>
          )}
        </Form>
      </Modal>

      {/* Interactive Live Preview Modal */}
      <Modal
        title={
          <div className="flex items-center justify-between pr-8">
            <Space>
              <EyeOutlined style={{ color: '#722ed1' }} />
              <span className="font-bold text-slate-800">
                Bản Xem Trước Trực Quan & Kiểm Tra Tọa Độ (Interactive Live Preview)
              </span>
            </Space>
            {detectedAiResult && (
              detectedAiResult.isAiAnalyzed ? (
                <Tag color="purple" icon={<ThunderboltOutlined />}>
                  ✨ Phân tích bởi OpenAI GPT-4o-mini
                </Tag>
              ) : (
                <Tag color="blue" icon={<BulbOutlined />}>
                  ⚡ Nhận diện thông minh (Rule Heuristic Engine)
                </Tag>
              )
            )}
          </div>
        }
        open={livePreviewModalVisible}
        onCancel={() => setLivePreviewModalVisible(false)}
        width={1080}
        footer={
          <div className="flex items-center justify-between">
            <div className="text-xs text-slate-500">
              Kiểm tra dữ liệu mẫu được điền vào đúng vị trí trước khi áp dụng.
            </div>
            <Space>
              {uploadedFileForPreview ? (
                <>
                  <Button icon={<EditOutlined />} onClick={handleEditCoordinatesFromAi}>
                    🛠️ Chỉnh sửa lại tọa độ
                  </Button>
                  <Button onClick={() => setLivePreviewModalVisible(false)}>
                    Hủy
                  </Button>
                  <Button
                    type="primary"
                    icon={<CheckCircleOutlined />}
                    loading={uploading}
                    onClick={handleConfirmAndUseTemplate}
                    className="bg-green-600 hover:bg-green-700 text-white font-semibold"
                  >
                    ✅ Xác nhận & Dùng Mẫu Này
                  </Button>
                </>
              ) : (
                <Button type="primary" onClick={() => setLivePreviewModalVisible(false)}>
                  Đóng Bản Xem Trước
                </Button>
              )}
            </Space>
          </div>
        }
      >
        {/* AI Insights Card */}
        {detectedAiResult?.config && (
          <div className="bg-slate-50 border border-slate-200 rounded-lg p-3 mb-4">
            <div className="flex items-center justify-between mb-2">
              <span className="font-semibold text-xs text-slate-700 uppercase tracking-wide">
                📌 Tọa độ trích xuất tự động (AI Detected Coordinates):
              </span>
              <Tag color="green">Dữ liệu mẫu: 3 dòng kiểm thử (Tiêu chuẩn 1200, Tiêu chuẩn 38 lẻ thùng, Gò 780)</Tag>
            </div>
            <div className="flex flex-wrap gap-2 text-xs">
              <Tag color="blue">Số HĐ (Invoice No): <b>{detectedAiResult.config.invSheet.header.invoiceNoCell}</b></Tag>
              <Tag color="blue">Ngày (Date): <b>{detectedAiResult.config.invSheet.header.dateCell}</b></Tag>
              <Tag color="blue">Hợp đồng (Contract): <b>{detectedAiResult.config.invSheet.header.contractNoCell}</b></Tag>
              <Tag color="blue">Khách hàng (Buyer): <b>{detectedAiResult.config.invSheet.header.buyerNameCell}</b></Tag>
              <Tag color="geekblue">Dòng bắt đầu INV: <b>Dòng {detectedAiResult.config.invSheet.table.startRow}</b> (Mã: {detectedAiResult.config.invSheet.table.itemCodeCol}, SL: {detectedAiResult.config.invSheet.table.quantityCol}, CMT: {detectedAiResult.config.invSheet.table.cmtUnitPriceCol}, DAP: {detectedAiResult.config.invSheet.table.dapUnitPriceCol})</Tag>
              <Tag color="volcano">Dòng bắt đầu PKL: <b>Dòng {detectedAiResult.config.pklSheet.startRow}</b> (Dải kiện: {detectedAiResult.config.pklSheet.cartonRangeCol}, Thùng: {detectedAiResult.config.pklSheet.cartonsCol})</Tag>
            </div>
          </div>
        )}

        {/* Live Preview Tabs */}
        {livePreviewData ? (
          <Tabs
            activeKey={previewActiveTab}
            onChange={(k) => setPreviewActiveTab(k as 'inv' | 'pkl')}
            type="card"
            items={[
              {
                key: 'inv',
                label: (
                  <span className="font-semibold">
                    <FileTextOutlined /> Xem thử Hóa Đơn (INV)
                  </span>
                ),
                children: (
                  <div className="border border-slate-200 rounded p-4 bg-white">
                    {/* Header info */}
                    <div className="grid grid-cols-2 gap-4 text-xs border border-slate-300 p-3 mb-4 rounded bg-slate-50">
                      <div>
                        <div><span className="font-semibold text-slate-700">Người mua (Buyer):</span> <span className="font-bold text-slate-900">{livePreviewData.invoice.buyerName}</span></div>
                        <div><span className="font-semibold text-slate-700">Địa chỉ:</span> <span className="text-slate-700">{livePreviewData.invoice.buyerAddressLine1}</span></div>
                        <div><span className="font-semibold text-slate-700">Điều kiện giao:</span> <Tag color="blue">{livePreviewData.invoice.deliveryTerms}</Tag></div>
                      </div>
                      <div className="space-y-1">
                        <div><span className="font-semibold text-slate-700">Số hóa đơn:</span> <span className="font-bold text-slate-900 font-mono">{livePreviewData.invoice.invoiceNo}</span></div>
                        <div><span className="font-semibold text-slate-700">Ngày lập:</span> <span className="text-slate-800">{livePreviewData.invoice.invoiceDate}</span></div>
                        <div><span className="font-semibold text-slate-700">Số hợp đồng:</span> <span className="text-slate-800">{livePreviewData.invoice.contractNo}</span></div>
                        <div><span className="font-semibold text-slate-700">Thanh toán:</span> <span className="text-slate-800">{livePreviewData.invoice.paymentTerms}</span></div>
                      </div>
                    </div>

                    {/* Table items */}
                    <table className="w-full text-xs border-collapse border border-slate-300">
                      <thead>
                        <tr className="bg-slate-100 text-slate-800 text-center font-bold">
                          <th className="border border-slate-300 p-2 w-12">STT</th>
                          <th className="border border-slate-300 p-2 w-36">Mã hàng</th>
                          <th className="border border-slate-300 p-2">Mô tả hàng hóa</th>
                          <th className="border border-slate-300 p-2 w-20">Số lượng</th>
                          <th className="border border-slate-300 p-2 w-14">ĐVT</th>
                          <th className="border border-slate-300 p-2 w-24">Đơn giá CMT</th>
                          <th className="border border-slate-300 p-2 w-24">Đơn giá DAP</th>
                          <th className="border border-slate-300 p-2 w-28">Thành tiền CMT</th>
                          <th className="border border-slate-300 p-2 w-28">Thành tiền DAP</th>
                        </tr>
                      </thead>
                      <tbody>
                        {livePreviewData.invoice.items.map((it: InvoicePreviewItem, idx: number) => (
                          <tr key={idx} className="hover:bg-slate-50">
                            <td className="border border-slate-300 p-2 text-center font-mono">{it.lineNo}</td>
                            <td className="border border-slate-300 p-2 font-mono font-semibold text-slate-900">{it.fullItemCode}</td>
                            <td className="border border-slate-300 p-2 text-slate-700">{it.description}</td>
                            <td className="border border-slate-300 p-2 text-right font-mono font-medium">{format2(it.quantity)}</td>
                            <td className="border border-slate-300 p-2 text-center">{it.unit}</td>
                            <td className="border border-slate-300 p-2 text-right font-mono">${it.unitPriceCMT.toFixed(2)}</td>
                            <td className="border border-slate-300 p-2 text-right font-mono">${it.unitPriceDAP.toFixed(2)}</td>
                            <td className="border border-slate-300 p-2 text-right font-mono font-medium">${format2(it.amountCMT)}</td>
                            <td className="border border-slate-300 p-2 text-right font-mono font-medium text-blue-700">${format2(it.amountDAP)}</td>
                          </tr>
                        ))}
                        <tr className="bg-slate-100 font-bold text-slate-900">
                          <td colSpan={3} className="border border-slate-300 p-2 text-center uppercase">
                            TỔNG CỘNG (TOTAL)
                          </td>
                          <td className="border border-slate-300 p-2 text-right font-mono font-bold">
                            {format2(livePreviewData.invoice.totalQuantity)}
                          </td>
                          <td className="border border-slate-300 p-2 text-center">đôi</td>
                          <td colSpan={2} className="border border-slate-300 p-2"></td>
                          <td className="border border-slate-300 p-2 text-right font-mono font-bold">
                            ${format2(livePreviewData.invoice.totalAmountCMT)}
                          </td>
                          <td className="border border-slate-300 p-2 text-right font-mono font-bold text-blue-800">
                            ${format2(livePreviewData.invoice.totalAmountDAP)}
                          </td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                ),
              },
              {
                key: 'pkl',
                label: (
                  <span className="font-semibold">
                    <InboxOutlined /> Xem thử Bảng Kê (PKL)
                  </span>
                ),
                children: (
                  <div className="border border-slate-200 rounded p-4 bg-white">
                    <table className="w-full text-xs border-collapse border border-slate-300">
                      <thead>
                        <tr className="bg-slate-100 text-slate-800 text-center font-bold">
                          <th className="border border-slate-300 p-2 w-12">STT</th>
                          <th className="border border-slate-300 p-2 w-28">Dải kiện (C/No)</th>
                          <th className="border border-slate-300 p-2 w-36">Mã hàng</th>
                          <th className="border border-slate-300 p-2">Mô tả hàng hóa</th>
                          <th className="border border-slate-300 p-2 w-20">Số lượng</th>
                          <th className="border border-slate-300 p-2 w-14">ĐVT</th>
                          <th className="border border-slate-300 p-2 w-20">Số thùng</th>
                          <th className="border border-slate-300 p-2 w-24">TL Tịnh (KGM)</th>
                          <th className="border border-slate-300 p-2 w-24">TL Cả bì (KGM)</th>
                        </tr>
                      </thead>
                      <tbody>
                        {livePreviewData.packingList.breakdownItems.map((it: PklBreakdownItem, idx: number) => (
                          <tr key={idx} className="hover:bg-slate-50">
                            <td className="border border-slate-300 p-2 text-center font-mono">{idx + 1}</td>
                            <td className="border border-slate-300 p-2 text-center font-mono font-semibold text-slate-800">{it.cartonRange}</td>
                            <td className="border border-slate-300 p-2 font-mono font-semibold text-slate-900">{it.fullItemCode}</td>
                            <td className="border border-slate-300 p-2 text-slate-700">{it.description}</td>
                            <td className="border border-slate-300 p-2 text-right font-mono font-medium">{format2(it.quantity)}</td>
                            <td className="border border-slate-300 p-2 text-center">đôi</td>
                            <td className="border border-slate-300 p-2 text-right font-mono font-medium text-purple-700">{it.cartonCount}</td>
                            <td className="border border-slate-300 p-2 text-right font-mono">{it.netWeight.toFixed(2)}</td>
                            <td className="border border-slate-300 p-2 text-right font-mono">{it.grossWeight.toFixed(2)}</td>
                          </tr>
                        ))}
                        <tr className="bg-slate-100 font-bold text-slate-900">
                          <td colSpan={4} className="border border-slate-300 p-2 text-center uppercase">
                            TỔNG CỘNG (TOTAL)
                          </td>
                          <td className="border border-slate-300 p-2 text-right font-mono font-bold">
                            {format2(livePreviewData.packingList.totalQuantity)}
                          </td>
                          <td className="border border-slate-300 p-2 text-center">đôi</td>
                          <td className="border border-slate-300 p-2 text-right font-mono font-bold text-purple-800">
                            {livePreviewData.packingList.totalCartons}
                          </td>
                          <td className="border border-slate-300 p-2 text-right font-mono font-bold">
                            {livePreviewData.packingList.totalNetWeight.toFixed(2)}
                          </td>
                          <td className="border border-slate-300 p-2 text-right font-mono font-bold">
                            {livePreviewData.packingList.totalGrossWeight.toFixed(2)}
                          </td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                ),
              },
            ]}
          />
        ) : (
          <div className="py-12 text-center">
            <Spin size="large" />
            <div className="mt-3 text-slate-500 text-sm">Đang tải dữ liệu xem trước...</div>
          </div>
        )}
      </Modal>
    </>
  );
};
