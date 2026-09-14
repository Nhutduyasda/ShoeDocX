import React, { useState, useRef, useCallback } from 'react';
import {
  Modal,
  Button,
  Table,
  Tag,
  Alert,
  Spin,
  message,
  notification,
  Tooltip,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  InboxOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  CloseCircleOutlined,
  ReloadOutlined,
  FileExcelOutlined,
  AuditOutlined,
} from '@ant-design/icons';
import { customsApi, type ConfirmSyncResponse } from '../api/customsApi';
import type {
  SavedShipmentSummary,
  CustomsReconciliationResult,
  CustomsComparisonRow,
} from '../types';
import { ProcessType, normalizeProcessType } from '../types';

interface CustomsSyncModalProps {
  open: boolean;
  onClose: () => void;
  targetOrder?: SavedShipmentSummary | null;
  onSyncSuccess?: (result: ConfirmSyncResponse) => void;
}

export const CustomsSyncModal: React.FC<CustomsSyncModalProps> = ({
  open,
  onClose,
  targetOrder,
  onSyncSuccess,
}) => {
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [confirming, setConfirming] = useState<boolean>(false);
  const [reconciliation, setReconciliation] = useState<CustomsReconciliationResult | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState<boolean>(false);

  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleClose = () => {
    setFile(null);
    setLoading(false);
    setConfirming(false);
    setReconciliation(null);
    setErrorMsg(null);
    setIsDragging(false);
    onClose();
  };

  const handleProcessFile = useCallback(
    async (selectedFile: File) => {
      const ext = selectedFile.name.substring(selectedFile.name.lastIndexOf('.')).toLowerCase();
      if (ext !== '.xls' && ext !== '.xlsx') {
        message.error('Vui lòng chọn file Excel kết xuất từ VNACCS (.xls hoặc .xlsx).');
        return;
      }

      setFile(selectedFile);
      setLoading(true);
      setErrorMsg(null);
      setReconciliation(null);

      try {
        const res = await customsApi.parseAndCompare(selectedFile, targetOrder?.id);
        setReconciliation(res);
      } catch (err: unknown) {
        const error = err as { response?: { data?: { message?: string; detail?: string } }; message?: string };
        const msg =
          error.response?.data?.message ||
          error.response?.data?.detail ||
          error.message ||
          'Không thể xử lý file tờ khai hải quan. Vui lòng kiểm tra định dạng file.';
        setErrorMsg(msg);
      } finally {
        setLoading(false);
      }
    },
    [targetOrder]
  );

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files && files.length > 0) {
      handleProcessFile(files[0]);
    }
  };

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
    const files = e.dataTransfer.files;
    if (files && files.length > 0) {
      handleProcessFile(files[0]);
    }
  };

  const isEffectivelyMatched = Boolean(reconciliation?.isFullyMatched);

  const handleConfirmSync = async () => {
    if (!reconciliation || !reconciliation.isOrderFound || !reconciliation.matchedOrder) {
      message.error('Không tìm thấy đơn hàng tương ứng để đồng bộ.');
      return;
    }

    const orderId = reconciliation.matchedOrder.id;
    const decl = reconciliation.declaration;

    setConfirming(true);
    try {
      const result = await customsApi.confirmSync(
        orderId,
        {
          declarationNo: decl.declarationNo,
          clearanceDate: decl.clearanceDate,
          customsDeclarationType: decl.customsDeclarationType,
          customsChannel: decl.customsChannel,
          customsOffice: decl.customsOffice,
          packageQty: decl.packageQty,
          grossWeight: decl.grossWeight,
          totalDap: decl.totalDap,
          totalCmt: decl.totalCmt,
          isFullyMatched: isEffectivelyMatched,
        },
        file || undefined
      );

      notification.success({
        message: 'Thông quan thành công',
        description: `Đồng bộ tờ khai ${decl.declarationNo} thành công! Đơn hàng đã chuyển sang trạng thái Đã thông quan.`,
        duration: 4.5,
      });

      if (onSyncSuccess) {
        onSyncSuccess(result);
      }
      handleClose();
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } }; message?: string };
      message.error(
        error.response?.data?.message || 'Có lỗi xảy ra khi xác nhận đồng bộ tờ khai hải quan.'
      );
    } finally {
      setConfirming(false);
    }
  };

  // Helper render luồng hải quan
  const renderChannelBadge = (channel: number) => {
    switch (channel) {
      case 1:
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
            Luồng 1 (Xanh) - Thông quan
          </span>
        );
      case 2:
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-amber-50 text-amber-700 border border-amber-200">
            <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
            Luồng 2 (Vàng) - Kiểm tra hồ sơ
          </span>
        );
      case 3:
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-rose-50 text-rose-700 border border-rose-200">
            <span className="w-1.5 h-1.5 rounded-full bg-rose-500" />
            Luồng 3 (Đỏ) - Kiểm tra thực tế
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-slate-50 text-slate-600 border border-slate-200">
            Chưa phân luồng
          </span>
        );
    }
  };

  const columns: ColumnsType<CustomsComparisonRow> = [
    {
      title: 'STT',
      dataIndex: 'index',
      key: 'index',
      width: 50,
      align: 'center',
      fixed: 'left',
      render: (val: number) => <span className="text-slate-400 font-mono text-xs">{val}</span>,
    },
    {
      title: 'Mã hình thể',
      dataIndex: 'styleCode',
      key: 'styleCode',
      width: 140,
      fixed: 'left',
      render: (code: string, record) => (
        <div className="flex items-center gap-1.5">
          <span className="font-mono text-xs font-semibold text-slate-800">{code}</span>
          {normalizeProcessType(record.processType) === ProcessType.GoKhongMay && (
            <Tag className="bg-purple-50 text-purple-700 border-purple-200 text-[10px] px-1 py-0 m-0">
              Gò
            </Tag>
          )}
        </div>
      ),
    },
    {
      title: 'SL Invoice (Đôi)',
      dataIndex: 'invoiceQuantity',
      key: 'invoiceQuantity',
      width: 110,
      align: 'right',
      render: (qty: number) => (
        <span className="font-mono text-xs font-medium text-slate-700">
          {qty > 0 ? qty.toLocaleString('en-US') : '—'}
        </span>
      ),
    },
    {
      title: 'SL Tờ khai (Đôi)',
      dataIndex: 'customsQuantity',
      key: 'customsQuantity',
      width: 110,
      align: 'right',
      render: (qty: number) => (
        <span className="font-mono text-xs font-medium text-slate-900">
          {qty > 0 ? qty.toLocaleString('en-US') : '—'}
        </span>
      ),
    },
    {
      title: 'Chênh lệch',
      dataIndex: 'differenceQuantity',
      key: 'differenceQuantity',
      width: 95,
      align: 'right',
      render: (diff: number) => {
        if (diff === 0) {
          return <span className="text-slate-400 font-mono text-xs">0</span>;
        }
        return (
          <span className={`font-mono text-xs font-bold ${diff > 0 ? 'text-amber-600' : 'text-rose-600'}`}>
            {diff > 0 ? `+${diff.toLocaleString('en-US')}` : diff.toLocaleString('en-US')}
          </span>
        );
      },
    },
    {
      title: 'Đơn giá DAP',
      key: 'priceDap',
      width: 130,
      align: 'right',
      render: (_, record) => {
        const isMatched = record.invoicePriceDap === record.customsPriceDap;
        return (
          <div className="font-mono text-xs">
            <span className={isMatched ? 'text-slate-700' : 'text-rose-600 font-bold'}>
              ${record.customsPriceDap.toFixed(2)}
            </span>
            {!isMatched && (
              <span className="text-slate-400 text-[11px] block line-through">
                ${record.invoicePriceDap.toFixed(2)}
              </span>
            )}
          </div>
        );
      },
    },
    {
      title: 'Đơn giá CMT',
      key: 'priceCmt',
      width: 110,
      align: 'right',
      render: (_, record) => (
        <div className="font-mono text-xs text-slate-600">
          ${record.customsPriceCmt.toFixed(2)}
        </div>
      ),
    },
    {
      title: 'Trạng thái đối soát',
      key: 'status',
      width: 140,
      fixed: 'right',
      render: (_, record) => {
        if (record.isMatched) {
          return (
            <Tag className="bg-emerald-50 text-emerald-700 border-emerald-200 text-xs py-0.5 px-2 m-0 flex items-center gap-1 w-fit">
              <CheckCircleOutlined /> Khớp 100%
            </Tag>
          );
        }
        if (record.invoiceQuantity === 0) {
          return (
            <Tag className="bg-amber-50 text-amber-700 border-amber-200 text-xs py-0.5 px-2 m-0">
              Thừa trên tờ khai
            </Tag>
          );
        }
        if (record.customsQuantity === 0) {
          return (
            <Tag className="bg-rose-50 text-rose-700 border-rose-200 text-xs py-0.5 px-2 m-0">
              Thiếu trên tờ khai
            </Tag>
          );
        }
        return (
          <Tooltip title={record.statusText}>
            <Tag className="bg-rose-50 text-rose-700 border-rose-200 text-xs py-0.5 px-2 m-0 flex items-center gap-1 w-fit cursor-pointer">
              <ExclamationCircleOutlined /> Sai lệch số liệu
            </Tag>
          </Tooltip>
        );
      },
    },
  ];

  return (
    <Modal
      open={open}
      onCancel={handleClose}
      width="min(1100px, 96vw)"
      style={{ top: 20 }}
      title={
        <div className="flex items-center gap-2.5 py-1">
          <div className="w-8 h-8 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center text-base">
            <AuditOutlined />
          </div>
          <div>
            <div className="text-base font-semibold text-slate-900 leading-tight">
              Hậu Kiểm & Đối Soát Tờ Khai Hải Quan
            </div>
            <div className="text-xs text-slate-500 font-normal mt-0.5">
              Tự động bóc tách dữ liệu từ file Excel VNACCS/VCIS và đối soát chéo 2 chiều với Hóa đơn nội bộ
            </div>
          </div>
        </div>
      }
      footer={
        <div className="flex items-center justify-between pt-2">
          <div>
            {reconciliation && (
              <span className="text-xs text-slate-500">
                File nguồn: <span className="font-mono text-slate-700 font-medium">{reconciliation.declaration.fileName}</span>
              </span>
            )}
          </div>
          <div className="flex items-center gap-2">
            <Button onClick={handleClose} disabled={loading || confirming}>
              Đóng
            </Button>
            {reconciliation?.isOrderFound && (
              <>
                {reconciliation.isInvoiceMismatch ? (
                  <Tooltip title="Không thể lưu hoặc đồng bộ vì số hóa đơn trên tờ khai khác với hóa đơn đang chọn">
                    <Button
                      disabled
                      danger
                      type="primary"
                      icon={<CloseCircleOutlined />}
                    >
                      Khóa đồng bộ (Lệch hóa đơn)
                    </Button>
                  </Tooltip>
                ) : isEffectivelyMatched ? (
                  <Button
                    type="primary"
                    icon={<CheckCircleOutlined />}
                    loading={confirming}
                    onClick={handleConfirmSync}
                    className="bg-emerald-600 hover:bg-emerald-700 text-white font-semibold shadow-md px-5 h-9 rounded-lg"
                  >
                    {confirming ? 'Đang lưu hồ sơ...' : 'Xác nhận Đồng bộ & Thông quan'}
                  </Button>
                ) : (
                  <Button disabled>Cần khắc phục sai lệch trước khi thông quan</Button>
                )}
              </>
            )}
          </div>
        </div>
      }
      destroyOnClose
    >
      <div className="space-y-4 py-2">
        {/* Khu vực Upload / Drag & Drop file */}
        {!reconciliation && !loading && (
          <div
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            onClick={() => fileInputRef.current?.click()}
            className={`border-2 border-dashed rounded-xl p-8 text-center cursor-pointer transition-all duration-200 ${
              isDragging
                ? 'border-blue-500 bg-blue-50/50 scale-[0.99]'
                : 'border-slate-300 hover:border-blue-400 bg-slate-50/50 hover:bg-slate-50'
            }`}
          >
            <input
              ref={fileInputRef}
              type="file"
              accept=".xls,.xlsx"
              className="hidden"
              onChange={handleFileSelect}
            />
            <div className="w-12 h-12 rounded-full bg-blue-50 text-blue-600 flex items-center justify-center mx-auto mb-3 text-2xl">
              <InboxOutlined />
            </div>
            <div className="text-sm font-semibold text-slate-800">
              Kéo thả file tờ khai hải quan vào đây, hoặc <span className="text-blue-600 underline">chọn từ máy tính</span>
            </div>
            <div className="text-xs text-slate-500 mt-1.5 max-w-lg mx-auto leading-relaxed">
              Hỗ trợ file Excel kết xuất trực tiếp từ phần mềm VNACCS/VCIS định dạng <span className="font-mono font-medium text-slate-700">.xls</span> (BIFF8) hoặc <span className="font-mono font-medium text-slate-700">.xlsx</span>.
            </div>
            {targetOrder && (
              <div className="mt-3 inline-flex items-center gap-1.5 px-3 py-1 bg-white rounded-md border border-slate-200 text-xs text-slate-600">
                <span>Hóa đơn mục tiêu:</span>
                <span className="font-mono font-semibold text-slate-900">{targetOrder.invoiceNo}</span>
              </div>
            )}
          </div>
        )}

        {/* Loading state */}
        {loading && (
          <div className="py-16 text-center">
            <Spin size="large" />
            <div className="mt-4 text-sm font-medium text-slate-700">
              Đang phân tích và bóc tách dữ liệu tờ khai hải quan...
            </div>
            <div className="text-xs text-slate-400 mt-1">
              Hệ thống đang đọc cấu trúc BIFF8/VNACCS và đối soát chéo với cơ sở dữ liệu hóa đơn.
            </div>
          </div>
        )}

        {/* Thông báo lỗi */}
        {errorMsg && (
          <Alert
            type="error"
            showIcon
            message="Lỗi đối soát tờ khai"
            description={errorMsg}
            className="rounded-lg"
            action={
              <Button size="small" icon={<ReloadOutlined />} onClick={() => fileInputRef.current?.click()}>
                Chọn file khác
              </Button>
            }
          />
        )}

        {/* Kết quả đối soát */}
        {reconciliation && (
          <div className="space-y-4">
            {/* Thanh công cụ file đã nạp */}
            <div className="flex items-center justify-between px-3.5 py-2.5 bg-slate-50 rounded-lg border border-slate-200">
              <div className="flex items-center gap-2">
                <FileExcelOutlined className="text-emerald-600 text-base" />
                <span className="text-xs font-mono font-semibold text-slate-800">
                  {file?.name || reconciliation.declaration.fileName}
                </span>
                <span className="text-[11px] text-slate-400">
                  ({file ? `${(file.size / 1024).toFixed(1)} KB` : ''})
                </span>
              </div>
              <Button
                size="small"
                icon={<ReloadOutlined />}
                onClick={() => {
                  setReconciliation(null);
                  setFile(null);
                  setErrorMsg(null);
                  setTimeout(() => fileInputRef.current?.click(), 100);
                }}
              >
                Tải file khác
              </Button>
            </div>

            {/* Header thông tin bóc tách từ Tờ khai */}
            <div className="bg-white rounded-lg border border-slate-200 p-3.5 space-y-3">
              <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-xs">
                <div className="space-y-1">
                  <div className="text-slate-400 text-[11px] uppercase tracking-wider">Số tờ khai</div>
                  <div className="font-mono text-sm font-bold text-slate-900">
                    {reconciliation.declaration.declarationNo || '—'}
                  </div>
                </div>
                <div className="space-y-1">
                  <div className="text-slate-400 text-[11px] uppercase tracking-wider">Ngày thông quan</div>
                  <div className="font-medium text-slate-800">
                    {reconciliation.declaration.clearanceDate || 'Chưa ghi nhận'}
                  </div>
                </div>
                <div className="space-y-1">
                  <div className="text-slate-400 text-[11px] uppercase tracking-wider">Mã loại hình / Chi cục</div>
                  <div className="font-medium text-slate-800">
                    <Tag className="bg-slate-100 text-slate-700 font-mono text-[11px] m-0 mr-1">
                      {reconciliation.declaration.customsDeclarationType || 'E52'}
                    </Tag>
                    <span>{reconciliation.declaration.customsOffice || '—'}</span>
                  </div>
                </div>
                <div className="space-y-1">
                  <div className="text-slate-400 text-[11px] uppercase tracking-wider">Phân luồng hải quan</div>
                  <div>{renderChannelBadge(reconciliation.declaration.customsChannel)}</div>
                </div>
              </div>

              <div className="h-[1px] bg-slate-100" />

              <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-xs">
                <div className="space-y-1">
                  <div className="text-slate-400 text-[11px] uppercase tracking-wider">Số hóa đơn trên tờ khai</div>
                  <div className="font-mono font-bold text-blue-700">
                    {reconciliation.declaration.invoiceNo || '—'}
                  </div>
                </div>
                <div className="space-y-1">
                  <div className="text-slate-400 text-[11px] uppercase tracking-wider">Số kiện / Trọng lượng Gross</div>
                  <div className="font-medium text-slate-800">
                    <span className="font-mono">{reconciliation.declaration.packageQty}</span> PK /{' '}
                    <span className="font-mono">{reconciliation.declaration.grossWeight}</span> kg
                  </div>
                </div>
                <div className="space-y-1">
                  <div className="text-slate-400 text-[11px] uppercase tracking-wider">Tổng trị giá DAP (TK)</div>
                  <div className="font-mono font-bold text-slate-900">
                    ${reconciliation.declaration.totalDap.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                  </div>
                </div>
                <div className="space-y-1">
                  <div className="text-slate-400 text-[11px] uppercase tracking-wider">Tổng trị giá CMT (TK)</div>
                  <div className="font-mono font-bold text-slate-900">
                    ${reconciliation.declaration.totalCmt.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                  </div>
                </div>
              </div>
            </div>

            {/* Banner cảnh báo sai lệch số hóa đơn (nếu có) */}
            {reconciliation.isInvoiceMismatch && (
              <Alert
                type="error"
                showIcon
                icon={<CloseCircleOutlined />}
                message="Cảnh báo: Sai lệch Số Hóa đơn (Invoice Mismatch)"
                description={
                  <div className="space-y-1.5 mt-1">
                    <div className="text-xs font-semibold text-rose-800">
                      {reconciliation.invoiceMismatchWarning}
                    </div>
                    <div className="text-xs text-slate-600">
                      Hóa đơn đang mở đối soát: <strong className="font-mono text-slate-900">{reconciliation.matchedOrder?.invoiceNo}</strong> | 
                      Số hóa đơn trên tờ khai: <strong className="font-mono text-rose-700">{reconciliation.declaration.invoiceNo}</strong>
                    </div>
                    <div className="text-[11px] text-rose-600 italic">
                      * Chức năng đồng bộ đã tự động khóa để bảo vệ dữ liệu, tránh ghi đè nhầm tờ khai của đơn khác.
                    </div>
                  </div>
                }
                className="rounded-lg border-rose-300 bg-rose-50/80"
              />
            )}

            {/* Banner trạng thái đối soát */}
            {!reconciliation.isOrderFound ? (
              <Alert
                type="error"
                showIcon
                icon={<CloseCircleOutlined />}
                message="Không tìm thấy hóa đơn tương ứng"
                description={reconciliation.message}
                className="rounded-lg"
              />
            ) : isEffectivelyMatched ? (
              <Alert
                type="success"
                showIcon
                icon={<CheckCircleOutlined className="text-emerald-600 text-lg" />}
                message={
                  <div className="flex items-center justify-between flex-wrap gap-2">
                    <span className="font-bold text-emerald-900 text-sm">
                      ✔ HỒ SƠ ĐÃ KHỚP 100% SỐ LIỆU HẢI QUAN
                    </span>
                    <Tag color="success" className="font-semibold text-xs px-2.5 py-0.5 rounded-full m-0">
                      Sẵn sàng thông quan
                    </Tag>
                  </div>
                }
                description={
                  <div className="text-xs text-emerald-800 mt-1 leading-relaxed">
                    Tất cả các dòng hàng, số lượng ({reconciliation.matchedOrder?.totalQuantity.toLocaleString('en-US')} đôi) và đơn giá DAP đều trùng khớp hoàn toàn giữa Tờ khai hải quan và Hóa đơn {reconciliation.matchedOrder?.invoiceNo}. Bạn hãy bấm nút <strong className="text-emerald-950 font-semibold">"Xác nhận Đồng bộ & Thông quan"</strong> bên dưới để hoàn tất cập nhật và khóa lưu trữ hồ sơ điện tử.
                  </div>
                }
                className="rounded-lg border-emerald-300 bg-emerald-50 shadow-sm"
              />
            ) : (
              <Alert
                type="warning"
                showIcon
                icon={<ExclamationCircleOutlined />}
                message={`Cảnh báo sai lệch (${reconciliation.discrepancies.length} điểm không trùng khớp)`}
                description={
                  <div className="space-y-1.5 mt-1">
                    <div className="text-xs text-amber-800">{reconciliation.message}</div>
                    <ul className="list-disc pl-4 text-xs text-amber-700 space-y-0.5">
                      {reconciliation.discrepancies.map((d, i) => (
                        <li key={i}>{d}</li>
                      ))}
                    </ul>
                  </div>
                }
                className="rounded-lg border-amber-200 bg-amber-50/50"
              />
            )}

            {/* Bảng so sánh chi tiết từng dòng hàng */}
            {reconciliation.comparisonRows.length > 0 && (
              <div className="border border-slate-200 rounded-lg overflow-hidden w-full overflow-x-auto min-w-0">
                <div className="px-3.5 py-2 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
                  <span className="text-xs font-semibold text-slate-700">
                    Bảng đối soát chi tiết dòng hàng ({reconciliation.comparisonRows.length} mặt hàng)
                  </span>
                  <span className="text-[11px] text-slate-500">
                    Hóa đơn: <strong className="font-mono text-slate-800">{reconciliation.matchedOrder?.invoiceNo}</strong>
                  </span>
                </div>
                <Table
                  dataSource={reconciliation.comparisonRows}
                  columns={columns}
                  rowKey="index"
                  size="small"
                  pagination={false}
                  scroll={{ x: 'max-content', y: 320 }}
                />
              </div>
            )}
          </div>
        )}
      </div>
    </Modal>
  );
};
