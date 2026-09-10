import React, { useState, useMemo } from 'react';
import {
  Modal,
  Input,
  Button,
  Table,
  Radio,
  Space,
  Alert,
  Tooltip,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  ClearOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import type { CreateShipmentItem, ProductMaster } from '../types';
import { ProcessType } from '../types';

const { TextArea } = Input;

interface ParsedRowItem {
  key: string;
  styleCode: string;
  quantity: number;
  processType: ProcessType;
  rawNote: string;
  matchedProduct?: ProductMaster;
  unitPriceCMT: number;
  unitPriceDAP: number;
  unit: string;
  pairPerCarton: number;
  description: string;
  isValid: boolean;
  errorMessage?: string;
}

interface QuickPasteModalProps {
  visible: boolean;
  onClose: () => void;
  products: ProductMaster[];
  onApply: (items: CreateShipmentItem[], mode: 'replace' | 'append') => void;
}

export const QuickPasteModal: React.FC<QuickPasteModalProps> = ({
  visible,
  onClose,
  products,
  onApply,
}) => {
  const [pastedText, setPastedText] = useState<string>('');
  const [applyMode, setApplyMode] = useState<'replace' | 'append'>('replace');

  // Tạo map tra cứu nhanh sản phẩm theo mã viết hoa
  const productMap = useMemo(() => {
    const map = new Map<string, ProductMaster>();
    products.forEach((p) => {
      map.set(p.styleCode.trim().toUpperCase(), p);
    });
    return map;
  }, [products]);

  // Parser bóc tách dữ liệu từ văn bản
  const parsedData = useMemo(() => {
    if (!pastedText.trim()) {
      return { items: [], totalQuantity: 0, validCount: 0, invalidCount: 0 };
    }

    const lines = pastedText.split(/\r?\n/);
    const result: ParsedRowItem[] = [];

    lines.forEach((line, idx) => {
      const trimmed = line.trim();
      if (!trimmed) return;

      // Bỏ qua dòng tiêu đề nếu người dùng copy cả header từ Excel
      const lower = trimmed.toLowerCase();
      if (
        (lower.includes('mã') || lower.includes('style') || lower.includes('item')) &&
        (lower.includes('số lượng') || lower.includes('qty') || lower.includes('sl') || lower.includes('quantity'))
      ) {
        return;
      }

      // Tách cột: ưu tiên tab \t (từ Excel), tiếp theo là dấu gạch đứng |, dấu phẩy hoặc khoảng trắng
      let parts: string[] = [];
      if (trimmed.includes('\t')) {
        parts = trimmed.split('\t');
      } else if (trimmed.includes('|')) {
        parts = trimmed.split('|');
      } else if (trimmed.includes(',')) {
        parts = trimmed.split(',');
      } else {
        // Tách bởi 1 hoặc nhiều khoảng trắng
        parts = trimmed.split(/\s+/);
      }

      // Làm sạch các phần tử
      parts = parts.map((p) => p.trim()).filter((p) => p.length > 0);
      if (parts.length === 0) return;

      // Cột 1: Mã hình thể (StyleCode)
      let rawStyleCode = parts[0] || '';
      // Loại bỏ các ký tự dấu nháy kép thừa nếu copy từ CSV
      rawStyleCode = rawStyleCode.replace(/^["']|["']$/g, '').trim();

      // Cột 2: Số lượng (Quantity)
      const rawQtyStr = (parts[1] || '').replace(/[,.\s]/g, '');
      const parsedQty = parseInt(rawQtyStr, 10);

      // Cột 3 (nếu có): Ghi chú / Công đoạn
      const remainingNotes = parts.slice(2).join(' ');

      // Kiểm tra quy trình công nghệ từ ghi chú hoặc hậu tố mã (.G)
      const isGoProcess =
        /g[oò]|gò\s*không\s*may|\.g/i.test(remainingNotes) ||
        rawStyleCode.toUpperCase().endsWith('.G');

      const processType = isGoProcess ? ProcessType.GoKhongMay : ProcessType.Standard;

      // Chuẩn hóa mã gốc: nếu mã bị gắn đuôi .G thì có thể tách ra để tra cứu trong Master Data
      let lookupCode = rawStyleCode.toUpperCase();
      if (lookupCode.endsWith('.G')) {
        lookupCode = lookupCode.substring(0, lookupCode.length - 2).trim();
      }

      const matched = productMap.get(lookupCode);

      const isValid = Boolean(rawStyleCode) && !isNaN(parsedQty) && parsedQty > 0;

      result.push({
        key: `paste-${idx}-${rawStyleCode}`,
        styleCode: rawStyleCode,
        quantity: isValid ? parsedQty : 0,
        processType,
        rawNote: remainingNotes,
        matchedProduct: matched,
        unitPriceCMT: matched?.unitPriceCMT || 0,
        unitPriceDAP: matched?.unitPriceDAP || 0,
        unit: matched?.unit || 'đôi',
        pairPerCarton: matched?.pairPerCarton || 12,
        description: matched?.description || '',
        isValid,
        errorMessage: !rawStyleCode
          ? 'Thiếu mã hình thể'
          : isNaN(parsedQty) || parsedQty <= 0
          ? 'Số lượng không hợp lệ'
          : undefined,
      });
    });

    const validItems = result.filter((r) => r.isValid);
    const totalQuantity = validItems.reduce((sum, r) => sum + r.quantity, 0);

    return {
      items: result,
      totalQuantity,
      validCount: validItems.length,
      invalidCount: result.length - validItems.length,
    };
  }, [pastedText, productMap]);

  // Xử lý áp dụng dữ liệu vào bảng
  const handleApply = () => {
    const validRows = parsedData.items.filter((r) => r.isValid);
    if (validRows.length === 0) return;

    const mappedItems: CreateShipmentItem[] = validRows.map((r) => ({
      styleCode: r.styleCode,
      description: r.description,
      quantity: r.quantity,
      processType: r.processType,
      unitPriceCMT: r.unitPriceCMT,
      unitPriceDAP: r.unitPriceDAP,
      unit: r.unit,
      pairPerCarton: r.pairPerCarton,
    }));

    onApply(mappedItems, applyMode);
    setPastedText('');
    onClose();
  };

  const handlePasteExample = () => {
    const example = `42072-030\t36\tThành phẩm chuẩn
45428-2LX\t4032\tGÒ KHÔNG MAY
51200-1BK\t5\tHàng mẫu lẻ`;
    setPastedText(example);
  };

  const handleClear = () => {
    setPastedText('');
  };

  const columns: ColumnsType<ParsedRowItem> = [
    {
      title: 'STT',
      key: 'stt',
      width: 45,
      align: 'center',
      render: (_, __, idx) => (
        <span className="font-mono text-xs text-slate-400">{idx + 1}</span>
      ),
    },
    {
      title: 'Mã hình thể',
      dataIndex: 'styleCode',
      key: 'styleCode',
      width: 150,
      render: (code: string, record) => (
        <div>
          <span className="font-mono font-semibold text-slate-900">{code}</span>
          {record.description && (
            <div className="text-[11px] text-slate-400 truncate max-w-[200px]" title={record.description}>
              {record.description}
            </div>
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
      render: (qty: number, record) => (
        <span
          className={`font-mono font-bold ${
            record.isValid ? 'text-slate-900' : 'text-rose-600'
          }`}
        >
          {record.isValid ? qty.toLocaleString() : 'Lỗi'}
        </span>
      ),
    },
    {
      title: 'Loại công đoạn',
      dataIndex: 'processType',
      key: 'processType',
      width: 140,
      render: (proc: ProcessType) =>
        proc === ProcessType.GoKhongMay ? (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-amber-50 text-amber-700 border border-amber-200/60">
            Gò không may (.G)
          </span>
        ) : (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-slate-100 text-slate-700 border border-slate-200">
            Thành hình
          </span>
        ),
    },
    {
      title: 'Quy cách',
      dataIndex: 'pairPerCarton',
      key: 'pairPerCarton',
      width: 100,
      align: 'center',
      render: (pairs: number) => (
        <span className="font-mono text-xs text-slate-600 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
          {pairs} đôi/thùng
        </span>
      ),
    },
    {
      title: 'Master Data',
      key: 'matchStatus',
      width: 130,
      render: (_, record) =>
        record.matchedProduct ? (
          <Tooltip title={`Đơn giá CMT: $${record.unitPriceCMT} | DAP: $${record.unitPriceDAP}`}>
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium bg-emerald-50 text-emerald-700 border border-emerald-200/60">
              <CheckCircleOutlined className="text-[10px]" /> Đã khớp mã
            </span>
          </Tooltip>
        ) : (
          <Tooltip title="Mã chưa có trong danh mục. Hệ thống vẫn cho phép nhập.">
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium bg-slate-100 text-slate-600 border border-slate-200">
              <ExclamationCircleOutlined className="text-[10px]" /> Mã mới
            </span>
          </Tooltip>
        ),
    },
  ];

  return (
    <Modal
      title={
        <div className="flex items-center justify-between pr-6 border-b border-slate-100 pb-3">
          <div className="flex items-center space-x-2">
            <ThunderboltOutlined className="text-indigo-600 text-lg" />
            <span className="font-bold text-slate-900 text-base">
              Dán nhanh số liệu từ Clipboard (Quick Paste)
            </span>
          </div>
          <span className="text-[11px] font-mono font-medium text-slate-600 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
            Hỗ trợ Excel & Zalo
          </span>
        </div>
      }
      open={visible}
      onCancel={onClose}
      width={900}
      footer={[
        <Button key="cancel" onClick={onClose} className="border-slate-200 text-slate-700">
          Đóng
        </Button>,
        <Button
          key="apply"
          type="primary"
          icon={<ThunderboltOutlined />}
          disabled={parsedData.validCount === 0}
          onClick={handleApply}
          className="bg-indigo-600 hover:bg-indigo-700"
        >
          Áp dụng vào bảng ({parsedData.validCount} mã - {parsedData.totalQuantity.toLocaleString()} đôi)
        </Button>,
      ]}
    >
      <div className="space-y-4 py-2">
        {/* Hướng dẫn & Nút dán mẫu */}
        <div className="flex items-center justify-between text-xs text-slate-500">
          <span>
            Sao chép các cột <strong>[Mã hình thể] [Số lượng] [Ghi chú / Công đoạn]</strong> từ Excel hoặc tin nhắn Zalo rồi dán (Ctrl+V) vào ô bên dưới:
          </span>
          <Space size="small">
            <Button size="small" type="link" onClick={handlePasteExample} className="text-indigo-600 p-0 text-xs font-medium">
              Dán dữ liệu mẫu
            </Button>
            <span className="text-slate-300">|</span>
            <Button size="small" type="link" icon={<ClearOutlined />} onClick={handleClear} className="text-slate-500 hover:text-slate-700 p-0 text-xs">
              Xóa ô
            </Button>
          </Space>
        </div>

        {/* Ô Textarea nhập liệu */}
        <TextArea
          rows={5}
          value={pastedText}
          onChange={(e) => setPastedText(e.target.value)}
          placeholder={`Dán dữ liệu tại đây (dạng tab từ Excel hoặc khoảng trắng):
42072-030\t36\tThành phẩm
45428-2LX\t4032\tGÒ KHÔNG MAY
51200-1BK\t5\tMẫu lẻ`}
          className="font-mono text-xs border-slate-300 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
        />

        {/* Thanh đối soát kết quả bóc tách */}
        {pastedText.trim() && (
          <div className="grid grid-cols-3 gap-3 p-3 bg-slate-50 border border-slate-200 rounded-lg">
            <div className="bg-white p-2.5 rounded border border-slate-200/80">
              <div className="text-[11px] font-medium text-slate-500 uppercase">Mã hợp lệ bóc tách</div>
              <div className="mt-0.5 text-lg font-bold font-mono text-slate-900">
                {parsedData.validCount} <span className="text-xs font-normal text-slate-400">mặt hàng</span>
              </div>
            </div>

            <div className="bg-white p-2.5 rounded border border-slate-200/80">
              <div className="text-[11px] font-medium text-slate-500 uppercase">Tổng số đôi đối soát</div>
              <div className="mt-0.5 text-lg font-bold font-mono text-slate-900">
                {parsedData.totalQuantity.toLocaleString()} <span className="text-xs font-normal text-slate-400">đôi</span>
              </div>
            </div>

            <div className="bg-white p-2.5 rounded border border-slate-200/80">
              <div className="text-[11px] font-medium text-slate-500 uppercase">Khớp Master Data</div>
              <div className="mt-0.5 text-lg font-bold font-mono text-slate-800">
                {parsedData.items.filter((r) => r.matchedProduct).length} / {parsedData.validCount}
              </div>
            </div>
          </div>
        )}

        {/* Cảnh báo lỗi bóc tách nếu có dòng sai định dạng */}
        {parsedData.invalidCount > 0 && (
          <Alert
            type="warning"
            showIcon
            message={`Phát hiện ${parsedData.invalidCount} dòng chưa đúng định dạng. Các dòng này sẽ được bỏ qua khi áp dụng.`}
            className="text-xs py-1.5"
          />
        )}

        {/* Bảng xem trước dữ liệu bóc tách */}
        {parsedData.items.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-700 uppercase tracking-wider">
                Xem trước kết quả nhận diện ({parsedData.items.length} dòng)
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
                dataSource={parsedData.items}
                columns={columns}
                pagination={false}
                size="small"
                scroll={{ y: 220 }}
              />
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
};
