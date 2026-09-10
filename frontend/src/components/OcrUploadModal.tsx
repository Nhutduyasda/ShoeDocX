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
  Tag,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  InboxOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  DeleteOutlined,
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

  const productMap = React.useMemo(() => {
    const map = new Map<string, ProductMaster>();
    products.forEach((p) => {
      map.set(p.styleCode.trim().toUpperCase(), p);
    });
    return map;
  }, [products]);

  const calculatedTotal = React.useMemo(() => {
    return extractedItems.reduce((acc, item) => acc + (item.quantity || 0), 0);
  }, [extractedItems]);

  const difference = calculatedTotal - reportedTotal;
  const isTotalMatched = reportedTotal > 0 && difference === 0;

  useEffect(() => {
    return () => {
      if (imagePreviewUrl && imagePreviewUrl.startsWith('blob:')) {
        URL.revokeObjectURL(imagePreviewUrl);
      }
    };
  }, [imagePreviewUrl]);

  const handleProcessImage = useCallback(async (imageFile: File | Blob) => {
    setLoading(true);
    try {
      const res = await ocrApi.extractFromImage(imageFile);
      setBatchTitle(res.title || '');
      setReportedTotal(res.reportedTotal || 0);
      setIsSimulation(res.isSimulation);
      setSimulationMessage(res.message || '');

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
      message.success(`Đã nhận diện ${enrichedItems.length} dòng hàng từ phiếu kho.`);
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
        || 'Không thể nhận diện ảnh phiếu kho. Vui lòng thử lại.';
      message.error(errorMsg);
    } finally {
      setLoading(false);
    }
  }, [productMap]);

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

  useEffect(() => {
    if (!visible) return;

    const handlePaste = (e: ClipboardEvent) => {
      const clipboardItems = e.clipboardData?.items;
      if (!clipboardItems) return;

      for (let i = 0; i < clipboardItems.length; i++) {
        if (clipboardItems[i].type.startsWith('image/')) {
          const blob = clipboardItems[i].getAsFile();
          if (blob) {
            e.preventDefault();
            message.info('Đã nhận diện ảnh từ Clipboard. Đang xử lý OCR...');

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

  const handleSampleReceipt = () => {
    const canvas = document.createElement('canvas');
    canvas.width = 600;
    canvas.height = 420;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    ctx.fillStyle = '#111827';
    ctx.font = 'bold 16px sans-serif';
    ctx.fillText('BẢNG KÊ GIAO HÀNG: LẦN 16 29/8 5BUY HD THÀNH HÌNH', 20, 35);

    ctx.strokeStyle = '#D1D5DB';
    ctx.lineWidth = 1;

    ctx.fillStyle = '#F9FAFB';
    ctx.fillRect(20, 50, 560, 30);
    ctx.strokeRect(20, 50, 560, 30);

    ctx.fillStyle = '#374151';
    ctx.font = 'bold 12px sans-serif';
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
      ctx.fillStyle = '#111827';
      ctx.font = '12px monospace';
      ctx.fillText(r.stt, 35, y + 20);
      ctx.fillText(r.code, 80, y + 20);
      ctx.fillText(r.qty, 300, y + 20);

      if (r.note.includes('GÒ')) {
        ctx.fillStyle = '#7E22CE';
        ctx.font = 'bold 11px sans-serif';
        ctx.fillText(r.note, 410, y + 20);
      } else {
        ctx.fillStyle = '#6B7280';
        ctx.font = '11px sans-serif';
        ctx.fillText('THÀNH PHẨM', 410, y + 20);
      }
      y += 30;
    });

    ctx.fillStyle = '#F9FAFB';
    ctx.fillRect(20, y, 560, 35);
    ctx.strokeRect(20, y, 560, 35);
    ctx.fillStyle = '#111827';
    ctx.font = 'bold 13px sans-serif';
    ctx.fillText('TỔNG CỘNG XUẤT KHO:', 40, y + 23);
    ctx.font = 'bold 14px monospace';
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

  const handleUpdateQuantity = (index: number, newQty: number | null) => {
    setExtractedItems((prev) => {
      const next = [...prev];
      next[index] = { ...next[index], quantity: newQty || 0 };
      return next;
    });
  };

  const handleUpdateProcess = (index: number, newProcess: ProcessType) => {
    setExtractedItems((prev) => {
      const next = [...prev];
      next[index] = { ...next[index], processType: newProcess };
      return next;
    });
  };

  const handleDeleteRow = (index: number) => {
    setExtractedItems((prev) => prev.filter((_, i) => i !== index));
  };

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
    message.success(`Đã áp dụng ${converted.length} mặt hàng vào hóa đơn.`);
    onClose();
  };

  const columns: ColumnsType<OcrItem> = [
    {
      title: 'STT',
      key: 'stt',
      width: 44,
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
          <span className="font-mono font-medium text-slate-900 text-xs">{code}</span>
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
      width: 110,
      align: 'right',
      render: (qty: number, _, index) => (
        <InputNumber
          min={1}
          value={qty}
          size="small"
          onChange={(val) => handleUpdateQuantity(index, val)}
          className="font-mono text-xs w-24 text-right"
        />
      ),
    },
    {
      title: 'Công đoạn',
      dataIndex: 'processType',
      key: 'processType',
      width: 135,
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
      title: 'Master',
      key: 'isMatched',
      width: 90,
      align: 'center',
      render: (_, record) =>
        record.isMatched ? (
          <Tag className="text-[11px] px-1.5 py-0 m-0 border-emerald-200 bg-emerald-50 text-emerald-700">
            Khớp
          </Tag>
        ) : (
          <Tag className="text-[11px] px-1.5 py-0 m-0 text-slate-500 border-slate-200 bg-slate-50">
            Mới
          </Tag>
        ),
    },
    {
      title: '',
      key: 'delete',
      width: 36,
      align: 'center',
      render: (_, __, index) => (
        <Button
          type="text"
          danger
          size="small"
          icon={<DeleteOutlined className="text-xs" />}
          onClick={() => handleDeleteRow(index)}
        />
      ),
    },
  ];

  return (
    <Modal
      title={
        <div className="pb-1">
          <div className="text-sm font-semibold text-slate-900">
            OCR phiếu kho
          </div>
          <div className="text-xs text-slate-500 font-normal mt-0.5">
            Trích xuất dữ liệu phiếu kho và đối chiếu số lượng trước khi đưa vào hóa đơn
          </div>
        </div>
      }
      open={visible}
      onCancel={onClose}
      width={1050}
      footer={[
        <Button key="cancel" onClick={onClose} className="border-slate-300 text-slate-700 text-xs h-9 px-3.5">
          Đóng
        </Button>,
        <Button
          key="apply"
          type="primary"
          disabled={extractedItems.length === 0}
          onClick={handleApply}
          className="bg-blue-600 hover:bg-blue-700 text-white text-xs h-9 px-4"
        >
          Áp dụng ({extractedItems.length} mã - {calculatedTotal.toLocaleString()} đôi)
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
          {/* Cột trái: Vùng tải ảnh */}
          <div className="lg:col-span-5 flex flex-col space-y-3">
            <div className="flex items-center justify-between text-xs text-slate-600">
              <span className="font-medium">Ảnh phiếu kho:</span>
              {imagePreviewUrl && (
                <Space size="middle">
                  <Button
                    size="small"
                    type="link"
                    icon={<ReloadOutlined />}
                    onClick={() => file && handleProcessImage(file)}
                    className="text-blue-600 p-0 text-xs"
                  >
                    Quét lại
                  </Button>
                  <span className="text-slate-300">|</span>
                  <Button
                    size="small"
                    type="link"
                    onClick={handleReset}
                    className="text-slate-500 hover:text-red-600 p-0 text-xs"
                  >
                    Đổi ảnh
                  </Button>
                </Space>
              )}
            </div>

            {!imagePreviewUrl ? (
              <div
                onDragOver={(e) => e.preventDefault()}
                onDrop={handleDrop}
                onClick={() => fileInputRef.current?.click()}
                className="border border-dashed border-slate-300 hover:border-blue-500 rounded-lg p-6 flex flex-col items-center justify-center text-center cursor-pointer transition-colors bg-white min-h-[300px]"
              >
                <div className="text-slate-400 mb-2">
                  <InboxOutlined style={{ fontSize: '36px' }} />
                </div>
                <div className="text-xs font-semibold text-slate-800">
                  Kéo thả ảnh phiếu kho hoặc click chọn file
                </div>
                <div className="text-[11px] text-slate-400 mt-1">
                  Định dạng .png, .jpg, .jpeg, .webp
                </div>

                <div className="mt-3 text-[11px] text-slate-500">
                  Hỗ trợ dán ảnh trực tiếp: <kbd className="px-1.5 py-0.5 bg-slate-100 border border-slate-200 rounded font-mono text-slate-700">Ctrl + V</kbd>
                </div>

                <div className="mt-4 pt-3 border-t border-slate-100 w-full flex justify-center">
                  <Button
                    size="small"
                    onClick={(e) => {
                      e.stopPropagation();
                      handleSampleReceipt();
                    }}
                    className="text-xs text-slate-600 border-slate-300 hover:border-blue-500"
                  >
                    Thử với ảnh mẫu phiếu kho
                  </Button>
                </div>
              </div>
            ) : (
              <div className="border border-slate-200 rounded-lg overflow-hidden bg-slate-50 p-2 flex flex-col items-center justify-center min-h-[300px] max-h-[440px]">
                <img
                  src={imagePreviewUrl}
                  alt="Phiếu kho"
                  className="max-h-[400px] max-w-full object-contain rounded"
                />
              </div>
            )}

            {file && (
              <div className="flex items-center space-x-1.5 text-[11px] text-slate-500 px-1">
                <FileImageOutlined />
                <span className="truncate">{file.name || 'Ảnh chụp từ clipboard'}</span>
                <span>({(file.size / 1024).toFixed(1)} KB)</span>
              </div>
            )}
          </div>

          {/* Cột phải: Kết quả & Đối chiếu */}
          <div className="lg:col-span-7 flex flex-col space-y-3">
            {loading ? (
              <div className="min-h-[300px] flex flex-col items-center justify-center space-y-3 border border-slate-200 rounded-lg bg-white p-8">
                <Spin />
                <div className="text-xs font-medium text-slate-700">
                  Đang phân tích bảng biểu và trích xuất dữ liệu OCR...
                </div>
              </div>
            ) : extractedItems.length > 0 ? (
              <>
                <div>
                  <label className="text-[11px] font-medium text-slate-600 block mb-1">
                    Tiêu đề phiếu kho nhận diện được:
                  </label>
                  <Input
                    size="middle"
                    value={batchTitle}
                    onChange={(e) => setBatchTitle(e.target.value)}
                    placeholder="VD: LẦN 16 29/8 5BUY HD THÀNH HÌNH"
                    className="text-xs font-medium"
                  />
                </div>

                {/* Reconciliation Bar (Đối chiếu số lượng) */}
                <div className="p-3 rounded-lg border border-slate-200 bg-slate-50 space-y-2">
                  <div className="flex items-center justify-between">
                    <div className="text-xs font-semibold text-slate-700">
                      Đối chiếu số lượng:
                    </div>
                    {isTotalMatched ? (
                      <Tag className="bg-emerald-50 text-emerald-700 border-emerald-200 text-xs m-0">
                        <CheckCircleOutlined className="mr-1" /> Khớp dữ liệu ({calculatedTotal.toLocaleString()} đôi)
                      </Tag>
                    ) : (
                      <Tag className="bg-red-50 text-red-700 border-red-200 text-xs m-0">
                        <ExclamationCircleOutlined className="mr-1" /> Lệch số đôi: {difference > 0 ? `+${difference}` : difference}
                      </Tag>
                    )}
                  </div>

                  <div className="grid grid-cols-3 gap-2 pt-1 text-center">
                    <div className="bg-white p-2 rounded border border-slate-200">
                      <div className="text-[11px] text-slate-500">Tổng nhận diện</div>
                      <div className="text-sm font-semibold font-mono text-slate-900">
                        {calculatedTotal.toLocaleString()} đôi
                      </div>
                    </div>
                    <div className="bg-white p-2 rounded border border-slate-200">
                      <div className="text-[11px] text-slate-500">Tổng trên phiếu</div>
                      <div className="text-sm font-semibold font-mono text-slate-900">
                        {reportedTotal ? `${reportedTotal.toLocaleString()} đôi` : 'Chưa ghi'}
                      </div>
                    </div>
                    <div className="bg-white p-2 rounded border border-slate-200">
                      <div className="text-[11px] text-slate-500">Chênh lệch</div>
                      <div className={`text-sm font-semibold font-mono ${difference === 0 ? 'text-emerald-700' : 'text-red-600'}`}>
                        {difference.toLocaleString()} đôi
                      </div>
                    </div>
                  </div>
                </div>

                {isSimulation && (
                  <Alert
                    type="info"
                    showIcon
                    message={simulationMessage || 'Đang hiển thị dữ liệu trích xuất mẫu.'}
                    className="text-xs py-1"
                  />
                )}

                {/* Editable Results Table */}
                <div className="space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold text-slate-700 uppercase tracking-wider">
                      Chi tiết mặt hàng ({extractedItems.length} dòng):
                    </span>
                    <div className="flex items-center space-x-2 text-xs text-slate-600">
                      <span>Chế độ:</span>
                      <Radio.Group
                        size="small"
                        value={applyMode}
                        onChange={(e) => setApplyMode(e.target.value)}
                        optionType="button"
                      >
                        <Radio.Button value="replace">Thay thế</Radio.Button>
                        <Radio.Button value="append">Thêm nối tiếp</Radio.Button>
                      </Radio.Group>
                    </div>
                  </div>

                  <Table
                    dataSource={extractedItems}
                    columns={columns}
                    rowKey={(_, index) => `ocr-${index}`}
                    pagination={false}
                    size="small"
                    scroll={{ y: 200 }}
                  />
                </div>
              </>
            ) : (
              <div className="min-h-[300px] flex flex-col items-center justify-center space-y-2 border border-slate-200 rounded-lg bg-white p-8 text-center text-slate-400">
                <InboxOutlined style={{ fontSize: '32px' }} />
                <div className="text-xs font-medium text-slate-600">
                  Chưa có dữ liệu trích xuất
                </div>
                <div className="text-xs text-slate-400 max-w-xs">
                  Tải ảnh phiếu kho lên ở cột bên trái hoặc dùng nút "Thử với ảnh mẫu" để kiểm tra bóc tách.
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </Modal>
  );
};
