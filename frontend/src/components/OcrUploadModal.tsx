import React, { useState, useEffect, useRef, useCallback } from 'react';
import {
  Modal,
  Button,
  Table,
  Radio,
  Space,
  Alert,
  Input,
  InputNumber,
  Select,
  Spin,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CloudUploadOutlined,
  CameraOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  DeleteOutlined,
  ThunderboltOutlined,
  ReloadOutlined,
  FileImageOutlined,
} from '@ant-design/icons';
import { ocrApi } from '../api/ocrApi';
import type { CreateShipmentItem, OcrItem, ProductMaster } from '../types';
import { ProcessType } from '../types';

interface OcrUploadModalProps {
  visible: boolean;
  onClose: () => void;
  products: ProductMaster[];
  onApply: (items: CreateShipmentItem[], mode: 'replace' | 'append') => void;
}

export const OcrUploadModal: React.FC<OcrUploadModalProps> = ({
  visible,
  onClose,
  products,
  onApply,
}) => {
  const [file, setFile] = useState<File | null>(null);
  const [imagePreviewUrl, setImagePreviewUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [batchTitle, setBatchTitle] = useState<string>('');
  const [reportedTotal, setReportedTotal] = useState<number>(0);
  const [extractedItems, setExtractedItems] = useState<OcrItem[]>([]);
  const [isSimulation, setIsSimulation] = useState<boolean>(false);
  const [simulationMessage, setSimulationMessage] = useState<string>('');
  const [applyMode, setApplyMode] = useState<'replace' | 'append'>('replace');

  const fileInputRef = useRef<HTMLInputElement>(null);

  // Tạo map tra cứu sản phẩm nhanh theo mã
  const productMap = React.useMemo(() => {
    const map = new Map<string, ProductMaster>();
    products.forEach((p) => {
      map.set(p.styleCode.trim().toUpperCase(), p);
    });
    return map;
  }, [products]);

  // Tính tổng số đôi của các dòng hiện tại
  const calculatedTotal = React.useMemo(() => {
    return extractedItems.reduce((acc, item) => acc + (item.quantity || 0), 0);
  }, [extractedItems]);

  const isTotalMatched = reportedTotal > 0 && calculatedTotal === reportedTotal;

  // Cleanup object URL khi unmount hoặc đổi ảnh
  useEffect(() => {
    return () => {
      if (imagePreviewUrl && imagePreviewUrl.startsWith('blob:')) {
        URL.revokeObjectURL(imagePreviewUrl);
      }
    };
  }, [imagePreviewUrl]);

  // Xử lý gửi ảnh lên backend OCR
  const handleProcessImage = useCallback(async (imageFile: File | Blob) => {
    setLoading(true);
    try {
      const res = await ocrApi.extractFromImage(imageFile);
      setBatchTitle(res.title || '');
      setReportedTotal(res.reportedTotal || 0);
      setIsSimulation(res.isSimulation);
      setSimulationMessage(res.message || '');

      // Đồng bộ lại với Master Data để đảm bảo thông tin mới nhất
      const enrichedItems: OcrItem[] = res.items.map((item) => {
        const cleanCode = item.styleCode.toUpperCase().endsWith('.G')
          ? item.styleCode.toUpperCase().slice(0, -2).trim()
          : item.styleCode.toUpperCase().trim();
        const pm = productMap.get(cleanCode);

        return {
          ...item,
          unitPriceCMT: pm?.unitPriceCMT ?? item.unitPriceCMT,
          unitPriceDAP: pm?.unitPriceDAP ?? item.unitPriceDAP,
          pairPerCarton: (pm?.pairPerCarton && pm.pairPerCarton > 0) ? pm.pairPerCarton : item.pairPerCarton || 12,
          description: pm?.description ?? item.description,
          unit: pm?.unit ?? item.unit ?? 'đôi',
          isMatched: Boolean(pm) || item.isMatched,
        };
      });

      setExtractedItems(enrichedItems);
      message.success(`Nhận diện thành công ${enrichedItems.length} dòng mặt hàng!`);
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
        || 'Không thể nhận diện ảnh phiếu kho. Vui lòng kiểm tra lại.';
      message.error(errorMsg);
    } finally {
      setLoading(false);
    }
  }, [productMap]);

  // Xử lý khi người dùng chọn file từ máy tính
  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFile = e.target.files?.[0];
    if (!selectedFile) return;

    if (!selectedFile.type.startsWith('image/')) {
      message.error('Vui lòng chọn đúng định dạng hình ảnh (.jpg, .png, .webp).');
      return;
    }

    if (imagePreviewUrl && imagePreviewUrl.startsWith('blob:')) {
      URL.revokeObjectURL(imagePreviewUrl);
    }

    setFile(selectedFile);
    const preview = URL.createObjectURL(selectedFile);
    setImagePreviewUrl(preview);
    handleProcessImage(selectedFile);
  };

  // Hỗ trợ sự kiện Ctrl + V (Paste image từ Clipboard - Zalo, Snipping Tool)
  useEffect(() => {
    if (!visible) return;

    const handlePaste = (e: ClipboardEvent) => {
      const items = e.clipboardData?.items;
      if (!items) return;

      for (let i = 0; i < items.length; i++) {
        if (items[i].type.startsWith('image/')) {
          const blob = items[i].getAsFile();
          if (blob) {
            e.preventDefault();
            message.info('Đã nhận diện ảnh từ Clipboard! Đang bóc tách OCR...');

            if (imagePreviewUrl && imagePreviewUrl.startsWith('blob:')) {
              URL.revokeObjectURL(imagePreviewUrl);
            }

            setFile(blob);
            const preview = URL.createObjectURL(blob);
            setImagePreviewUrl(preview);
            handleProcessImage(blob);
            break;
          }
        }
      }
    };

    window.addEventListener('paste', handlePaste);
    return () => {
      window.removeEventListener('paste', handlePaste);
    };
  }, [visible, imagePreviewUrl, handleProcessImage]);

  // Hỗ trợ kéo thả ảnh
  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    const droppedFile = e.dataTransfer.files?.[0];
    if (!droppedFile || !droppedFile.type.startsWith('image/')) {
      message.error('Vui lòng kéo thả file ảnh hợp lệ.');
      return;
    }

    if (imagePreviewUrl && imagePreviewUrl.startsWith('blob:')) {
      URL.revokeObjectURL(imagePreviewUrl);
    }

    setFile(droppedFile);
    const preview = URL.createObjectURL(droppedFile);
    setImagePreviewUrl(preview);
    handleProcessImage(droppedFile);
  };

  // Tạo ảnh mẫu mô phỏng phiếu xuất kho và bóc tách trực tiếp
  const handleSampleReceipt = () => {
    // Tạo canvas vẽ ảnh mẫu bảng kê giao hàng
    const canvas = document.createElement('canvas');
    canvas.width = 600;
    canvas.height = 420;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // Nền trắng
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Tiêu đề
    ctx.fillStyle = '#0f172a';
    ctx.font = 'bold 18px sans-serif';
    ctx.fillText('BẢNG KÊ GIAO HÀNG: LẦN 16 29/8 5BUY HD THÀNH HÌNH', 20, 35);

    // Bảng kẻ
    ctx.strokeStyle = '#94a3b8';
    ctx.lineWidth = 1;

    // Header table
    ctx.fillStyle = '#f1f5f9';
    ctx.fillRect(20, 50, 560, 30);
    ctx.strokeRect(20, 50, 560, 30);

    ctx.fillStyle = '#334155';
    ctx.font = 'bold 13px sans-serif';
    ctx.fillText('STT', 30, 70);
    ctx.fillText('MÃ HÌNH THỂ', 80, 70);
    ctx.fillText('SỐ LƯỢNG (ĐÔI)', 260, 70);
    ctx.fillText('GHI CHÚ / CÔNG ĐOẠN', 400, 70);

    const rows = [
      { stt: '1', code: '42072-410', qty: '36', note: '' },
      { stt: '2', code: '41898-2LI', qty: '228', note: '' },
      { stt: '3', code: '42072-030', qty: '4032', note: '' },
      { stt: '4', code: '42073-030', qty: '312', note: '' },
      { stt: '5', code: '45428-2LX', qty: '780', note: 'GÒ KHÔNG MAY' },
      { stt: '6', code: '43223-001', qty: '960', note: 'GÒ KHÔNG MAY' },
    ];

    let y = 80;
    rows.forEach((r) => {
      ctx.strokeRect(20, y, 560, 30);
      ctx.fillStyle = '#1e293b';
      ctx.font = '13px monospace';
      ctx.fillText(r.stt, 35, y + 20);
      ctx.fillText(r.code, 80, y + 20);
      ctx.fillText(r.qty, 300, y + 20);

      if (r.note.includes('GÒ')) {
        ctx.fillStyle = '#d97706';
        ctx.font = 'bold 12px sans-serif';
        ctx.fillText(r.note, 410, y + 20);
      } else {
        ctx.fillStyle = '#64748b';
        ctx.font = '12px sans-serif';
        ctx.fillText('THÀNH PHẨM', 410, y + 20);
      }
      y += 30;
    });

    // Dòng tổng cộng
    ctx.fillStyle = '#f8fafc';
    ctx.fillRect(20, y, 560, 35);
    ctx.strokeRect(20, y, 560, 35);
    ctx.fillStyle = '#0f172a';
    ctx.font = 'bold 14px sans-serif';
    ctx.fillText('TỔNG CỘNG XUẤT KHO:', 40, y + 23);
    ctx.font = 'bold 16px monospace';
    ctx.fillText('6,348 ĐÔI', 300, y + 23);

    canvas.toBlob((blob) => {
      if (blob) {
        const sampleFile = new File([blob], 'phieu-kho-mau.png', { type: 'image/png' });
        setFile(sampleFile);
        const url = URL.createObjectURL(sampleFile);
        setImagePreviewUrl(url);
        handleProcessImage(sampleFile);
      }
    }, 'image/png');
  };

  // Cập nhật số lượng inline
  const handleUpdateQuantity = (index: number, newQty: number | null) => {
    setExtractedItems((prev) => {
      const next = [...prev];
      next[index] = { ...next[index], quantity: newQty || 0 };
      return next;
    });
  };

  // Cập nhật công đoạn inline
  const handleUpdateProcess = (index: number, newProcess: ProcessType) => {
    setExtractedItems((prev) => {
      const next = [...prev];
      next[index] = { ...next[index], processType: newProcess };
      return next;
    });
  };

  // Xóa một dòng khỏi kết quả OCR
  const handleDeleteRow = (index: number) => {
    setExtractedItems((prev) => prev.filter((_, i) => i !== index));
  };

  // Xóa dữ liệu và tải lại ảnh
  const handleReset = () => {
    if (imagePreviewUrl && imagePreviewUrl.startsWith('blob:')) {
      URL.revokeObjectURL(imagePreviewUrl);
    }
    setFile(null);
    setImagePreviewUrl(null);
    setBatchTitle('');
    setReportedTotal(0);
    setExtractedItems([]);
    setIsSimulation(false);
  };

  // Áp dụng dữ liệu vào lưới chính
  const handleApply = () => {
    const validItems = extractedItems.filter((i) => i.quantity > 0 && i.styleCode.trim());
    if (validItems.length === 0) {
      message.warning('Không có mặt hàng hợp lệ nào để áp dụng.');
      return;
    }

    const converted: CreateShipmentItem[] = validItems.map((item) => ({
      styleCode: item.styleCode,
      quantity: item.quantity,
      processType: item.processType,
      unitPriceCMT: item.unitPriceCMT,
      unitPriceDAP: item.unitPriceDAP,
      unit: item.unit,
      pairPerCarton: item.pairPerCarton,
      description: item.description,
    }));

    onApply(converted, applyMode);
    message.success(`Đã áp dụng ${converted.length} mặt hàng vào bảng xuất hóa đơn!`);
    onClose();
  };

  const columns: ColumnsType<OcrItem> = [
    {
      title: 'STT',
      key: 'stt',
      width: 50,
      align: 'center',
      render: (_, __, index) => <span className="text-slate-400 font-mono text-xs">{index + 1}</span>,
    },
    {
      title: 'Mã hình thể',
      dataIndex: 'styleCode',
      key: 'styleCode',
      width: 140,
      render: (code: string, record) => (
        <div>
          <span className="font-mono font-semibold text-slate-900 text-xs">{code}</span>
          {record.description && (
            <div className="text-[11px] text-slate-400 truncate max-w-[130px]">{record.description}</div>
          )}
        </div>
      ),
    },
    {
      title: 'Số lượng (đôi)',
      dataIndex: 'quantity',
      key: 'quantity',
      width: 120,
      align: 'right',
      render: (qty: number, _, index) => (
        <InputNumber
          min={1}
          value={qty}
          size="small"
          onChange={(val) => handleUpdateQuantity(index, val)}
          className="font-mono font-bold text-xs w-24 text-right"
        />
      ),
    },
    {
      title: 'Quy trình',
      dataIndex: 'processType',
      key: 'processType',
      width: 140,
      render: (proc: ProcessType, _, index) => (
        <Select
          size="small"
          value={proc}
          onChange={(val) => handleUpdateProcess(index, val)}
          className="text-xs w-32"
          options={[
            { label: 'Thành hình', value: ProcessType.Standard },
            { label: 'Gò không may (.G)', value: ProcessType.GoKhongMay },
          ]}
        />
      ),
    },
    {
      title: 'Master Data',
      key: 'isMatched',
      width: 120,
      render: (_, record) =>
        record.isMatched ? (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium bg-emerald-50 text-emerald-700 border border-emerald-200/60">
            <CheckCircleOutlined className="text-[10px]" /> Đã khớp
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium bg-slate-100 text-slate-600 border border-slate-200">
            <ExclamationCircleOutlined className="text-[10px]" /> Mã mới
          </span>
        ),
    },
    {
      title: 'Xóa',
      key: 'delete',
      width: 50,
      align: 'center',
      render: (_, __, index) => (
        <Button
          type="text"
          danger
          size="small"
          icon={<DeleteOutlined />}
          onClick={() => handleDeleteRow(index)}
        />
      ),
    },
  ];

  return (
    <Modal
      title={
        <div className="flex items-center justify-between pr-6 border-b border-slate-100 pb-3">
          <div className="flex items-center space-x-2.5">
            <div className="w-8 h-8 rounded-lg bg-indigo-50 border border-indigo-200/80 flex items-center justify-center text-indigo-600">
              <CameraOutlined className="text-base" />
            </div>
            <div>
              <span className="font-semibold text-slate-900 text-base">
                Quét ảnh phiếu kho & Bóc tách dữ liệu (Vision OCR)
              </span>
              <p className="text-xs text-slate-500 font-normal m-0 mt-0.5">
                Nhận diện bảng kê giao hàng, đối soát tổng số đôi và tự động map công đoạn Gò không may
              </p>
            </div>
          </div>
          <span className="text-[11px] font-mono font-medium text-slate-600 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
            Hỗ trợ Ctrl+V / Kéo thả
          </span>
        </div>
      }
      open={visible}
      onCancel={onClose}
      width={1100}
      footer={[
        <Button key="cancel" onClick={onClose} className="border-slate-200 text-slate-700">
          Đóng
        </Button>,
        <Button
          key="apply"
          type="primary"
          icon={<ThunderboltOutlined />}
          disabled={extractedItems.length === 0}
          onClick={handleApply}
          className="bg-indigo-600 hover:bg-indigo-700"
        >
          Áp dụng vào bảng ({extractedItems.length} mã - {calculatedTotal.toLocaleString()} đôi)
        </Button>,
      ]}
    >
      <div className="py-2">
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          className="hidden"
          onChange={handleFileChange}
        />

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
          {/* Cột trái: Vùng tải ảnh & Preview */}
          <div className="lg:col-span-5 flex flex-col space-y-3">
            <div className="flex items-center justify-between text-xs text-slate-600">
              <span className="font-medium">Ảnh phiếu kho đã nạp:</span>
              {imagePreviewUrl && (
                <Space size="small">
                  <Button
                    size="small"
                    type="link"
                    icon={<ReloadOutlined />}
                    onClick={() => file && handleProcessImage(file)}
                    className="text-indigo-600 p-0 text-xs"
                  >
                    Quét lại
                  </Button>
                  <span className="text-slate-300">|</span>
                  <Button
                    size="small"
                    type="link"
                    icon={<DeleteOutlined />}
                    onClick={handleReset}
                    className="text-rose-600 p-0 text-xs"
                  >
                    Đổi ảnh khác
                  </Button>
                </Space>
              )}
            </div>

            {/* Dropzone hoặc Image Preview */}
            {!imagePreviewUrl ? (
              <div
                onDragOver={(e) => e.preventDefault()}
                onDrop={handleDrop}
                onClick={() => fileInputRef.current?.click()}
                className="border-2 border-dashed border-slate-300 hover:border-indigo-500 rounded-xl p-6 flex flex-col items-center justify-center text-center cursor-pointer transition-colors bg-slate-50/50 hover:bg-indigo-50/20 min-h-[320px]"
              >
                <div className="w-14 h-14 rounded-full bg-white shadow-2xs border border-slate-200 flex items-center justify-center text-indigo-600 mb-3">
                  <CloudUploadOutlined className="text-2xl" />
                </div>
                <div className="text-sm font-semibold text-slate-800">
                  Kéo thả ảnh phiếu kho vào đây
                </div>
                <div className="text-xs text-slate-500 mt-1 max-w-[240px]">
                  hoặc bấm để chọn file (.png, .jpg, .jpeg, .webp)
                </div>

                <div className="mt-4 inline-flex items-center px-2.5 py-1 rounded bg-indigo-50 border border-indigo-200/80 text-indigo-700 text-xs font-mono">
                  💡 Nhấn <strong>Ctrl + V</strong> để dán ảnh chụp màn hình Zalo
                </div>

                <div className="mt-5 pt-4 border-t border-slate-200 w-full flex justify-center">
                  <Button
                    size="small"
                    onClick={(e) => {
                      e.stopPropagation();
                      handleSampleReceipt();
                    }}
                    className="text-xs text-slate-600 bg-white border-slate-200 hover:border-indigo-500"
                  >
                    Thử với ảnh mẫu phiếu kho
                  </Button>
                </div>
              </div>
            ) : (
              <div className="border border-slate-200 rounded-xl overflow-hidden bg-slate-900/5 p-2 flex flex-col items-center justify-center min-h-[320px] max-h-[460px]">
                <img
                  src={imagePreviewUrl}
                  alt="Phiếu kho preview"
                  className="max-h-[420px] max-w-full object-contain rounded shadow-xs"
                />
              </div>
            )}

            {file && (
              <div className="flex items-center space-x-2 text-xs text-slate-500 px-1">
                <FileImageOutlined />
                <span className="truncate">{file.name || 'Ảnh chụp từ clipboard'}</span>
                <span>({(file.size / 1024).toFixed(1)} KB)</span>
              </div>
            )}
          </div>

          {/* Cột phải: Kết quả bóc tách & Đối soát */}
          <div className="lg:col-span-7 flex flex-col space-y-3">
            {loading ? (
              <div className="min-h-[320px] flex flex-col items-center justify-center space-y-3 border border-slate-200 rounded-xl bg-white p-8">
                <Spin size="large" />
                <div className="text-sm font-semibold text-slate-800">
                  AI đang phân tích và bóc tách bảng số liệu...
                </div>
                <div className="text-xs text-slate-500 text-center max-w-xs">
                  Hệ thống đang nhận diện mã hình thể, số lượng, quy trình Gò không may và đối soát với Master Data.
                </div>
              </div>
            ) : extractedItems.length > 0 ? (
              <>
                {/* Tiêu đề đợt giao */}
                <div>
                  <label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider block mb-1">
                    Tiêu đề đợt giao nhận diện được:
                  </label>
                  <Input
                    size="small"
                    value={batchTitle}
                    onChange={(e) => setBatchTitle(e.target.value)}
                    placeholder="VD: LẦN 16 29/8 5BUY HD THÀNH HÌNH"
                    className="font-medium text-xs text-slate-800"
                  />
                </div>

                {/* Thanh Đối Soát Tổng Số Đôi */}
                <div className="p-3 rounded-xl border border-slate-200 bg-slate-50 space-y-2">
                  <div className="flex items-center justify-between">
                    <div className="text-xs font-semibold text-slate-700 uppercase tracking-wider">
                      Đối soát tổng số đôi phiếu kho:
                    </div>
                    {isTotalMatched ? (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800 border border-emerald-300">
                        <CheckCircleOutlined /> Khớp 100% số tổng phiếu ({calculatedTotal.toLocaleString()} đôi)
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-rose-100 text-rose-800 border border-rose-300">
                        <ExclamationCircleOutlined /> Lệch số đôi: Chi tiết {calculatedTotal.toLocaleString()} ≠ Phiếu {reportedTotal.toLocaleString()}
                      </span>
                    )}
                  </div>

                  <div className="grid grid-cols-3 gap-2 pt-1 text-center">
                    <div className="bg-white p-2 rounded border border-slate-200/80">
                      <div className="text-[11px] text-slate-500">Tổng chi tiết nhận diện</div>
                      <div className="text-base font-bold font-mono text-slate-900">
                        {calculatedTotal.toLocaleString()} <span className="text-xs font-normal">đôi</span>
                      </div>
                    </div>
                    <div className="bg-white p-2 rounded border border-slate-200/80">
                      <div className="text-[11px] text-slate-500">Số ghi trên phiếu</div>
                      <div className="text-base font-bold font-mono text-slate-900">
                        {reportedTotal ? reportedTotal.toLocaleString() : 'Chưa ghi'} <span className="text-xs font-normal">đôi</span>
                      </div>
                    </div>
                    <div className="bg-white p-2 rounded border border-slate-200/80">
                      <div className="text-[11px] text-slate-500">Chênh lệch</div>
                      <div className={`text-base font-bold font-mono ${calculatedTotal - reportedTotal === 0 ? 'text-emerald-700' : 'text-rose-600'}`}>
                        {(calculatedTotal - reportedTotal).toLocaleString()} <span className="text-xs font-normal">đôi</span>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Thông báo mô phỏng nếu chưa có API Key */}
                {isSimulation && (
                  <Alert
                    type="info"
                    showIcon
                    message={simulationMessage || 'Đang hiển thị dữ liệu trích xuất mẫu (Demo).'}
                    className="text-xs py-1.5"
                  />
                )}

                {/* Bảng xem trước & chỉnh sửa */}
                <div className="space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold text-slate-700 uppercase tracking-wider">
                      Chi tiết mặt hàng ({extractedItems.length} dòng):
                    </span>
                    <div className="flex items-center space-x-2">
                      <span className="text-xs text-slate-500">Chế độ áp dụng:</span>
                      <Radio.Group
                        size="small"
                        value={applyMode}
                        onChange={(e) => setApplyMode(e.target.value)}
                        optionType="button"
                        buttonStyle="solid"
                      >
                        <Radio.Button value="replace">Thay thế toàn bộ</Radio.Button>
                        <Radio.Button value="append">Thêm nối tiếp</Radio.Button>
                      </Radio.Group>
                    </div>
                  </div>

                  <div className="border border-slate-200 rounded-lg overflow-hidden bg-white">
                    <Table
                      dataSource={extractedItems}
                      columns={columns}
                      rowKey={(_, index) => `ocr-${index}`}
                      pagination={false}
                      size="small"
                      scroll={{ y: 220 }}
                    />
                  </div>
                </div>
              </>
            ) : (
              <div className="min-h-[320px] flex flex-col items-center justify-center space-y-2 border border-slate-200 rounded-xl bg-white p-8 text-center text-slate-400">
                <CameraOutlined className="text-3xl text-slate-300" />
                <div className="text-sm font-medium text-slate-600">
                  Chưa có dữ liệu trích xuất
                </div>
                <div className="text-xs text-slate-400 max-w-xs">
                  Vui lòng tải ảnh lên ở cột bên trái hoặc bấm "Thử với ảnh mẫu phiếu kho" để bắt đầu bóc tách.
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </Modal>
  );
};
