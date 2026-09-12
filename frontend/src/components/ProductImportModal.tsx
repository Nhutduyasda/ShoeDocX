import React, { useState } from 'react';
import {
  Modal,
  Upload,
  Button,
  Switch,
  Alert,
  Table,
  Typography,
  Select,
  Tag,
  Spin,
  message,
} from 'antd';
import {
  InboxOutlined,
  DownloadOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  FileExcelOutlined,
  SettingOutlined,
} from '@ant-design/icons';
import type { UploadFile } from 'antd/es/upload/interface';
import { productMasterApi } from '../api/productMasterApi';
import type {
  ImportResult,
  MasterDataFolder,
  ImportPreviewResponse,
  ColumnMappingOverride,
} from '../types';

const { Text } = Typography;
const { Dragger } = Upload;

interface ProductImportModalProps {
  visible: boolean;
  onCancel: () => void;
  onSuccess: () => void;
  targetFolder?: MasterDataFolder | null;
}

export const ProductImportModal: React.FC<ProductImportModalProps> = ({
  visible,
  onCancel,
  onSuccess,
  targetFolder,
}) => {
  const [fileList, setFileList] = useState<UploadFile[]>([]);
  const [rawFile, setRawFile] = useState<File | null>(null);
  const [previewing, setPreviewing] = useState<boolean>(false);
  const [previewData, setPreviewData] = useState<ImportPreviewResponse | null>(null);
  const [mapping, setMapping] = useState<ColumnMappingOverride>({});
  const [updateExisting, setUpdateExisting] = useState<boolean>(true);
  const [uploading, setUploading] = useState<boolean>(false);
  const [downloadingTemplate, setDownloadingTemplate] = useState<boolean>(false);
  const [result, setResult] = useState<ImportResult | null>(null);

  const handleDownloadTemplate = async () => {
    try {
      setDownloadingTemplate(true);
      await productMasterApi.downloadTemplate();
      message.success('Đã tải xuống file mẫu Excel');
    } catch {
      message.error('Không thể tải file mẫu Excel');
    } finally {
      setDownloadingTemplate(false);
    }
  };

  // Khi người dùng chọn file: Gọi API Preview để nhận diện ngữ nghĩa các cột
  const handleFileSelect = async (file: File) => {
    setRawFile(file);
    setFileList([file as any]);
    setResult(null);

    try {
      setPreviewing(true);
      const preview = await productMasterApi.previewImport(file, targetFolder?.id);
      setPreviewData(preview);

      // Điền mapping mặc định từ kết quả tự động phát hiện
      setMapping({
        styleCodeCol: preview.detectedMapping.styleCodeCol,
        cmtPriceCol: preview.detectedMapping.cmtPriceCol,
        dapPriceCol: preview.detectedMapping.dapPriceCol,
        descriptionCol: preview.detectedMapping.descriptionCol,
        hsCodeCol: preview.detectedMapping.hsCodeCol ?? undefined,
        unitCol: preview.detectedMapping.unitCol ?? undefined,
        pairsPerCartonCol: preview.detectedMapping.pairsPerCartonCol ?? undefined,
      });

      message.success(
        `Đã phân tích file: tìm thấy ${preview.totalRows} dòng dữ liệu, bắt đầu từ dòng ${preview.startRowIndex}`
      );
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể phân tích cấu trúc file Excel');
      setPreviewData(null);
    } finally {
      setPreviewing(false);
    }
  };

  // Xác nhận nạp dữ liệu vào hệ thống
  const handleConfirmUpload = async () => {
    if (!rawFile) {
      message.warning('Vui lòng chọn file Excel để tải lên');
      return;
    }

    try {
      setUploading(true);
      const res = await productMasterApi.importExcel(
        rawFile,
        updateExisting,
        targetFolder?.id,
        mapping
      );
      setResult(res);

      if (res.failedCount === 0) {
        message.success(
          res.message || `Import thành công: ${res.createdCount} tạo mới, ${res.updatedCount} cập nhật`
        );
        onSuccess();
      } else {
        message.warning(
          res.message || `Import hoàn tất: ${res.createdCount} tạo mới, ${res.updatedCount} cập nhật, ${res.failedCount} lỗi`
        );
        onSuccess();
      }
    } catch (err: any) {
      const errorMsg =
        err.response?.data?.message || err.message || 'Lỗi xảy ra trong quá trình import dữ liệu';
      message.error(errorMsg);
    } finally {
      setUploading(false);
    }
  };

  const handleClose = () => {
    setFileList([]);
    setRawFile(null);
    setPreviewData(null);
    setMapping({});
    setResult(null);
    onCancel();
  };

  const handleResetFile = () => {
    setFileList([]);
    setRawFile(null);
    setPreviewData(null);
    setMapping({});
    setResult(null);
  };

  // Tạo options cho các Select dropdown chọn cột
  const columnOptions = (previewData?.availableColumns || []).map((col) => {
    const sampleText = col.sampleValues.slice(0, 2).join(' | ');
    const headerLabel = col.headerName ? `[${col.headerName}] ` : '';
    return {
      value: col.index,
      label: `Cột ${col.columnLetter}: ${headerLabel}${sampleText || '(Trống)'}`,
    };
  });

  const previewColumns = [
    {
      title: 'Dòng',
      dataIndex: 'rowNumber',
      key: 'rowNumber',
      width: 50,
      render: (v: number) => <span className="font-mono text-xs text-slate-400">#{v}</span>,
    },
    {
      title: 'Mã hình thể',
      dataIndex: 'styleCode',
      key: 'styleCode',
      width: 130,
      render: (v: string, r: any) => (
        <span className="font-mono font-semibold text-xs text-slate-900 bg-slate-50 px-1.5 py-0.5 rounded border border-slate-200">
          {v} {r.isGo && <Tag color="purple" className="text-[10px] ml-1 px-1 py-0">.G</Tag>}
        </span>
      ),
    },
    {
      title: 'Đơn giá CMT',
      dataIndex: 'unitPriceCMT',
      key: 'unitPriceCMT',
      width: 90,
      align: 'right' as const,
      render: (v: number) => <span className="font-mono text-xs text-blue-700">${v.toFixed(2)}</span>,
    },
    {
      title: 'Đơn giá DAP/FOB',
      dataIndex: 'unitPriceDAP',
      key: 'unitPriceDAP',
      width: 100,
      align: 'right' as const,
      render: (v: number) => <span className="font-mono text-xs text-emerald-700">${v.toFixed(2)}</span>,
    },
    {
      title: 'Mô tả hải quan',
      dataIndex: 'description',
      key: 'description',
      ellipsis: true,
      render: (v: string) => <span className="text-xs text-slate-700">{v || '---'}</span>,
    },
    {
      title: 'ĐVT',
      dataIndex: 'unit',
      key: 'unit',
      width: 60,
      align: 'center' as const,
      render: (v: string) => <span className="text-xs text-slate-600">{v}</span>,
    },
    {
      title: 'Quy cách',
      dataIndex: 'pairsPerCarton',
      key: 'pairsPerCarton',
      width: 75,
      align: 'center' as const,
      render: (v: number) => <span className="text-xs font-mono">{v} đôi</span>,
    },
  ];

  const errorColumns = [
    {
      title: 'Dòng',
      dataIndex: 'rowNumber',
      key: 'rowNumber',
      width: 70,
      render: (v: number) => <span className="font-mono text-xs">{v}</span>,
    },
    {
      title: 'Mã hình thể',
      dataIndex: 'styleCode',
      key: 'styleCode',
      width: 140,
      render: (v: string) => <span className="font-medium text-xs text-slate-800">{v}</span>,
    },
    {
      title: 'Nguyên nhân lỗi',
      dataIndex: 'message',
      key: 'message',
      render: (v: string) => <span className="text-xs text-rose-600">{v}</span>,
    },
  ];

  return (
    <Modal
      title={
        <div className="text-sm font-semibold text-slate-900 flex items-center gap-2">
          <FileExcelOutlined className="text-emerald-600" />
          <span>Nhập dữ liệu Master Data (Bộ Phân Tích Thích Ứng Thông Minh)</span>
        </div>
      }
      open={visible}
      onCancel={handleClose}
      footer={[
        <Button
          key="close"
          onClick={handleClose}
          className="border-slate-300 text-slate-700 text-xs h-9 px-3.5"
        >
          {result ? 'Đóng' : 'Hủy'}
        </Button>,
        previewData && !result && (
          <Button
            key="upload"
            type="primary"
            loading={uploading}
            onClick={handleConfirmUpload}
            className="bg-blue-600 hover:bg-blue-700 text-white text-xs h-9 px-4 font-medium"
          >
            Xác nhận Nạp {previewData.totalRows} mã vào hệ thống
          </Button>
        ),
      ]}
      width={780}
      destroyOnClose
    >
      <div className="space-y-3.5 my-2">
        {/* Banner thư mục đích */}
        {targetFolder && (
          <Alert
            type="info"
            showIcon
            message={
              <div className="text-xs">
                <span>Nhập vào thư mục: </span>
                <strong className="text-blue-700">{targetFolder.name}</strong>
                {targetFolder.customerName && (
                  <span className="text-slate-600"> ({targetFolder.customerName})</span>
                )}
                <span className="ml-2 text-slate-500">
                  | Quy cách mặc định: <strong className="text-slate-800">{targetFolder.defaultPairsPerCarton} đôi/thùng</strong>
                </span>
              </div>
            }
          />
        )}

        {/* Thanh công cụ: Tùy chọn & Tải file mẫu */}
        <div className="flex flex-wrap items-center justify-between bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2 text-xs">
          <div className="flex items-center space-x-2">
            <span className="text-slate-600">Ghi đè mã đã tồn tại:</span>
            <Switch
              size="small"
              checked={updateExisting}
              onChange={setUpdateExisting}
            />
          </div>

          <Button
            type="link"
            onClick={handleDownloadTemplate}
            disabled={downloadingTemplate}
            icon={<DownloadOutlined />}
            className="text-blue-600 hover:text-blue-700 p-0 text-xs font-medium"
          >
            Tải file Excel mẫu
          </Button>
        </div>

        {/* 1. Khu vực Kéo Thả File khi chưa chọn file */}
        {!rawFile && !result && (
          <Dragger
            fileList={fileList}
            beforeUpload={(file) => {
              const fileName = file.name.toLowerCase();
              const isXlsx =
                fileName.endsWith('.xlsx') ||
                fileName.endsWith('.xls') ||
                file.type ===
                  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' ||
                file.type === 'application/vnd.ms-excel';
              if (!isXlsx) {
                message.error('Vui lòng chỉ chọn file Excel (.xlsx hoặc .xls)');
                return Upload.LIST_IGNORE;
              }
              handleFileSelect(file);
              return false;
            }}
            showUploadList={false}
            className="border-dashed border-slate-300 hover:border-blue-500 rounded-lg p-6 bg-white"
          >
            <p className="ant-upload-drag-icon text-slate-400 my-2">
              <InboxOutlined style={{ fontSize: '36px' }} />
            </p>
            <p className="ant-upload-text text-xs font-medium text-slate-800 m-0">
              Kéo thả file Excel vào đây hoặc bấm để duyệt file
            </p>
            <p className="ant-upload-hint text-[11px] text-slate-400 mt-1 m-0">
              Hệ thống tự động thích ứng với cấu trúc file cũ (Workbook1.xlsx) và file đối tác mới (Book1.xlsx)
            </p>
          </Dragger>
        )}

        {/* Đang phân tích file */}
        {previewing && (
          <div className="text-center py-8 bg-slate-50 border border-slate-200 rounded-lg">
            <Spin tip="Đang phân tích cấu trúc cột và dữ liệu mẫu..." />
          </div>
        )}

        {/* 2. Khu vực Xem Trước & Ánh Xạ Cột (Preview & Mapping UI) */}
        {previewData && !result && !previewing && (
          <div className="space-y-3.5">
            {/* File info card */}
            <div className="flex items-center justify-between bg-emerald-50 border border-emerald-200 rounded-lg px-3.5 py-2 text-xs">
              <div className="flex items-center space-x-2 text-emerald-900">
                <FileExcelOutlined className="text-emerald-600 text-sm" />
                <span className="font-semibold">{rawFile?.name}</span>
                <span className="text-emerald-700 font-mono">({previewData.totalRows} dòng dữ liệu)</span>
                <span className="text-slate-500">| Bắt đầu từ dòng #{previewData.startRowIndex}</span>
              </div>
              <Button
                type="link"
                size="small"
                onClick={handleResetFile}
                className="text-xs text-slate-500 hover:text-slate-800 p-0"
              >
                Chọn file khác
              </Button>
            </div>

            {/* Box cấu hình ánh xạ cột */}
            <div className="bg-slate-50 border border-slate-200 rounded-lg p-3 space-y-2.5">
              <div className="flex items-center justify-between">
                <span className="text-xs font-semibold text-slate-800 flex items-center gap-1.5">
                  <SettingOutlined className="text-blue-600" />
                  Ánh xạ cột theo ngữ nghĩa (Tự động nhận diện - có thể điều chỉnh):
                </span>
                <Tag color="blue" className="text-[10px] m-0">
                  Adaptive Auto-Detected
                </Tag>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label className="text-[11px] font-medium text-slate-600 block mb-1">
                    Cột Mã sản phẩm (Style Code) *
                  </label>
                  <Select
                    value={mapping.styleCodeCol}
                    onChange={(v) => setMapping((prev) => ({ ...prev, styleCodeCol: v }))}
                    options={columnOptions}
                    className="w-full text-xs"
                    size="small"
                  />
                </div>

                <div>
                  <label className="text-[11px] font-medium text-slate-600 block mb-1">
                    Cột Đơn giá CMT (USD) *
                  </label>
                  <Select
                    value={mapping.cmtPriceCol}
                    onChange={(v) => setMapping((prev) => ({ ...prev, cmtPriceCol: v }))}
                    options={columnOptions}
                    className="w-full text-xs"
                    size="small"
                  />
                </div>

                <div>
                  <label className="text-[11px] font-medium text-slate-600 block mb-1">
                    Cột Đơn giá DAP / FOB (USD) *
                  </label>
                  <Select
                    value={mapping.dapPriceCol}
                    onChange={(v) => setMapping((prev) => ({ ...prev, dapPriceCol: v }))}
                    options={columnOptions}
                    className="w-full text-xs"
                    size="small"
                  />
                </div>

                <div>
                  <label className="text-[11px] font-medium text-slate-600 block mb-1">
                    Cột Mô tả hàng hóa hải quan *
                  </label>
                  <Select
                    value={mapping.descriptionCol}
                    onChange={(v) => setMapping((prev) => ({ ...prev, descriptionCol: v }))}
                    options={columnOptions}
                    className="w-full text-xs"
                    size="small"
                  />
                </div>
              </div>
            </div>

            {/* Bảng xem trước 3-5 dòng mẫu */}
            <div>
              <div className="flex items-center justify-between mb-1.5">
                <span className="text-xs font-medium text-slate-700">
                  Xem trước dữ liệu ({previewData.previewRows.length} dòng mẫu đầu tiên):
                </span>
                <span className="text-[11px] text-slate-400 italic">
                  Các mã đuôi .G sẽ tự động phân tách đơn giá Gò
                </span>
              </div>
              <Table
                dataSource={previewData.previewRows}
                columns={previewColumns}
                rowKey="rowNumber"
                size="small"
                pagination={false}
                className="border border-slate-200 rounded-lg overflow-hidden text-xs"
              />
            </div>
          </div>
        )}

        {/* 3. Kết quả Import */}
        {result && (
          <div className="space-y-3">
            <Alert
              message={
                <div className="text-xs font-semibold text-slate-900">
                  Tổng {result.totalRowsRead} dòng dữ liệu đã xử lý
                </div>
              }
              description={
                <div className="flex items-center space-x-4 mt-1 text-xs text-slate-700">
                  <span className="flex items-center space-x-1">
                    <CheckCircleOutlined className="text-emerald-600" />
                    <span>Tạo mới: {result.createdCount}</span>
                  </span>
                  <span className="flex items-center space-x-1">
                    <CheckCircleOutlined className="text-blue-600" />
                    <span>Cập nhật: {result.updatedCount}</span>
                  </span>
                  {result.failedCount > 0 && (
                    <span className="flex items-center space-x-1 text-red-600">
                      <CloseCircleOutlined />
                      <span>Lỗi: {result.failedCount}</span>
                    </span>
                  )}
                </div>
              }
              type={result.failedCount === 0 ? 'success' : 'warning'}
              className="border-slate-200"
              showIcon
            />

            {result.errors.length > 0 && (
              <div>
                <Text className="text-xs font-medium text-slate-700 mb-1.5 block">
                  Chi tiết lỗi ({result.errors.length}):
                </Text>
                <Table
                  dataSource={result.errors}
                  columns={errorColumns}
                  rowKey={(r) => `${r.rowNumber}-${r.styleCode}`}
                  size="small"
                  pagination={{ pageSize: 5 }}
                />
              </div>
            )}
          </div>
        )}
      </div>
    </Modal>
  );
};
