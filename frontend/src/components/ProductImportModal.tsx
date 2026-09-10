import React, { useState } from 'react';
import {
  Modal,
  Upload,
  Button,
  Switch,
  Alert,
  Table,
  Typography,
  message,
} from 'antd';
import {
  InboxOutlined,
  DownloadOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
} from '@ant-design/icons';
import type { UploadFile } from 'antd/es/upload/interface';
import { productMasterApi } from '../api/productMasterApi';
import type { ImportResult } from '../types';

const { Text } = Typography;
const { Dragger } = Upload;

interface ProductImportModalProps {
  visible: boolean;
  onCancel: () => void;
  onSuccess: () => void;
}

export const ProductImportModal: React.FC<ProductImportModalProps> = ({
  visible,
  onCancel,
  onSuccess,
}) => {
  const [fileList, setFileList] = useState<UploadFile[]>([]);
  const [updateExisting, setUpdateExisting] = useState<boolean>(true);
  const [uploading, setUploading] = useState<boolean>(false);
  const [downloadingTemplate, setDownloadingTemplate] = useState<boolean>(false);
  const [result, setResult] = useState<ImportResult | null>(null);

  const handleDownloadTemplate = async () => {
    try {
      setDownloadingTemplate(true);
      await productMasterApi.downloadTemplate();
      message.success('Đã tải xuống file mẫu Excel');
    } catch (err) {
      console.error(err);
      message.error('Không thể tải file mẫu Excel');
    } finally {
      setDownloadingTemplate(false);
    }
  };

  const handleUpload = async () => {
    if (fileList.length === 0) {
      message.warning('Vui lòng chọn file Excel để tải lên');
      return;
    }

    const file = fileList[0].originFileObj as File;
    if (!file) {
      message.warning('File không hợp lệ');
      return;
    }

    try {
      setUploading(true);
      const res = await productMasterApi.importExcel(file, updateExisting);
      setResult(res);

      if (res.failedCount === 0) {
        message.success(
          `Import thành công: ${res.createdCount} tạo mới, ${res.updatedCount} cập nhật`
        );
        onSuccess();
      } else {
        message.warning(
          `Import hoàn tất: ${res.createdCount} tạo mới, ${res.updatedCount} cập nhật, ${res.failedCount} lỗi`
        );
        onSuccess();
      }
    } catch (err: any) {
      console.error(err);
      message.error('Lỗi xảy ra trong quá trình import dữ liệu');
    } finally {
      setUploading(false);
    }
  };

  const handleClose = () => {
    setFileList([]);
    setResult(null);
    onCancel();
  };

  const errorColumns = [
    {
      title: 'Dòng',
      dataIndex: 'rowNumber',
      key: 'rowNumber',
      width: 60,
      align: 'center' as const,
      render: (val: number) => <span className="font-mono text-xs text-slate-400">#{val}</span>,
    },
    {
      title: 'Mã hình thể',
      dataIndex: 'styleCode',
      key: 'styleCode',
      width: 130,
      render: (val: string) => (
        <span className="font-mono text-xs text-slate-700 bg-slate-100 px-1.5 py-0.5 rounded">
          {val || '---'}
        </span>
      ),
    },
    {
      title: 'Chi tiết lỗi',
      dataIndex: 'message',
      key: 'message',
      render: (val: string) => <span className="text-xs text-slate-600">{val}</span>,
    },
  ];

  return (
    <Modal
      title={
        <div className="text-sm font-semibold text-slate-900 tracking-tight">
          Import Danh mục Hàng hóa từ Excel (.xlsx)
        </div>
      }
      open={visible}
      onCancel={handleClose}
      footer={[
        <Button
          key="close"
          onClick={handleClose}
          className="border-slate-200 text-slate-600 hover:bg-slate-50 text-xs h-8"
        >
          {result ? 'Đóng' : 'Hủy'}
        </Button>,
        !result && (
          <Button
            key="upload"
            type="primary"
            loading={uploading}
            disabled={fileList.length === 0}
            onClick={handleUpload}
            className="bg-indigo-600 hover:bg-indigo-500 text-white border-none shadow-none text-xs h-8"
          >
            Bắt đầu Import
          </Button>
        ),
      ]}
      width={650}
      destroyOnClose
    >
      <div className="space-y-4 my-3">
        {/* Template download & options bar */}
        <div className="flex flex-wrap items-center justify-between bg-slate-50 border border-slate-200/80 rounded-lg px-3 py-2 text-xs">
          <div className="flex items-center space-x-2">
            <span className="text-slate-600">Ghi đè mã đã có:</span>
            <Switch
              size="small"
              checked={updateExisting}
              onChange={setUpdateExisting}
            />
          </div>

          <button
            type="button"
            onClick={handleDownloadTemplate}
            disabled={downloadingTemplate}
            className="text-indigo-600 hover:text-indigo-700 font-medium flex items-center space-x-1 bg-transparent border-none cursor-pointer p-0 text-xs"
          >
            <DownloadOutlined />
            <span>Tải file mẫu Excel</span>
          </button>
        </div>

        {/* Upload dragger */}
        {!result && (
          <Dragger
            fileList={fileList}
            beforeUpload={(file) => {
              const isXlsx =
                file.name.endsWith('.xlsx') ||
                file.type ===
                  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
              if (!isXlsx) {
                message.error('Vui lòng chỉ chọn file Excel (.xlsx)');
                return Upload.LIST_IGNORE;
              }
              setFileList([file]);
              return false;
            }}
            onRemove={() => {
              setFileList([]);
            }}
            maxCount={1}
            className="bg-slate-50/40 border-slate-200 hover:border-indigo-400 rounded-lg p-3 transition-colors"
          >
            <p className="ant-upload-drag-icon text-slate-400 my-2">
              <InboxOutlined style={{ fontSize: '36px' }} />
            </p>
            <p className="ant-upload-text text-xs font-medium text-slate-700 m-0">
              Kéo thả file Excel vào đây hoặc click để chọn
            </p>
            <p className="ant-upload-hint text-[11px] text-slate-400 mt-1 m-0">
              Định dạng file .xlsx theo cấu trúc bảng chuẩn
            </p>
          </Dragger>
        )}

        {/* Results */}
        {result && (
          <div className="space-y-3">
            <Alert
              message={
                <div className="text-xs font-semibold text-slate-800">
                  Tổng {result.totalRows} dòng dữ liệu
                </div>
              }
              description={
                <div className="flex items-center space-x-5 mt-1 text-xs text-slate-600">
                  <span className="flex items-center space-x-1">
                    <CheckCircleOutlined className="text-emerald-500" />
                    <span>Thêm mới: {result.createdCount}</span>
                  </span>
                  <span className="flex items-center space-x-1">
                    <CheckCircleOutlined className="text-indigo-500" />
                    <span>Cập nhật: {result.updatedCount}</span>
                  </span>
                  {result.failedCount > 0 && (
                    <span className="flex items-center space-x-1 text-rose-500">
                      <CloseCircleOutlined />
                      <span>Lỗi: {result.failedCount}</span>
                    </span>
                  )}
                </div>
              }
              type={result.failedCount === 0 ? 'success' : 'warning'}
              className="rounded-lg border-slate-200"
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

            <div className="text-center pt-1">
              <Button
                size="small"
                onClick={() => {
                  setFileList([]);
                  setResult(null);
                }}
                className="text-xs text-slate-600 border-slate-200"
              >
                Import file khác
              </Button>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
};
