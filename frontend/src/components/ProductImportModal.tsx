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

    const rawItem = fileList[0] as any;
    const file = (rawItem?.originFileObj ?? rawItem) as File;
    if (!file || typeof file.slice !== 'function') {
      message.warning('File không hợp lệ hoặc chưa được chọn đúng định dạng.');
      return;
    }

    try {
      setUploading(true);
      const res = await productMasterApi.importExcel(file, updateExisting);
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
      console.error(err);
      const errorMsg = err.response?.data?.message || err.message || 'Lỗi xảy ra trong quá trình import dữ liệu';
      message.error(errorMsg);
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
        <div className="text-sm font-semibold text-slate-900">
          Nhập dữ liệu Master Data từ Excel (.xlsx)
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
        !result && (
          <Button
            key="upload"
            type="primary"
            loading={uploading}
            disabled={fileList.length === 0}
            onClick={handleUpload}
            className="bg-blue-600 hover:bg-blue-700 text-white text-xs h-9 px-4"
          >
            Bắt đầu Nhập dữ liệu
          </Button>
        ),
      ]}
      width={640}
      destroyOnClose
    >
      <div className="space-y-4 my-2">
        {/* Template download & options bar */}
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

        {/* Upload dragger */}
        {!result && (
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
              setFileList([file]);
              return false;
            }}
            onChange={(info) => {
              if (info.fileList.length > 0) {
                setFileList(info.fileList.slice(-1));
              }
            }}
            onRemove={() => {
              setFileList([]);
            }}
            maxCount={1}
            className="border-dashed border-slate-300 hover:border-blue-500 rounded-lg p-4 bg-white"
          >
            <p className="ant-upload-drag-icon text-slate-400 my-2">
              <InboxOutlined style={{ fontSize: '32px' }} />
            </p>
            <p className="ant-upload-text text-xs font-medium text-slate-800 m-0">
              Kéo thả file Excel vào đây hoặc bấm để duyệt file
            </p>
            <p className="ant-upload-hint text-[11px] text-slate-400 mt-1 m-0">
              Chỉ chấp nhận file định dạng .xlsx theo cấu trúc cột chuẩn
            </p>
          </Dragger>
        )}

        {/* Results */}
        {result && (
          <div className="space-y-3">
            <Alert
              message={
                <div className="text-xs font-semibold text-slate-900">
                  Tổng {result.totalRows} dòng dữ liệu
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

            <div className="text-center pt-1">
              <Button
                size="small"
                onClick={() => {
                  setFileList([]);
                  setResult(null);
                }}
                className="text-xs text-slate-700 border-slate-300"
              >
                Nhập file khác
              </Button>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
};
