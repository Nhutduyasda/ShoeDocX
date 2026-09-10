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

  const productMap = useMemo(() => {
    const map = new Map<string, ProductMaster>();
    products.forEach((p) => {
      map.set(p.styleCode.trim().toUpperCase(), p);
    });
    return map;
  }, [products]);

  const parsedData = useMemo(() => {
    if (!pastedText.trim()) {
      return { items: [], totalQuantity: 0, validCount: 0, invalidCount: 0 };
    }

    const lines = pastedText.split(/\r?\n/);
    const result: ParsedRowItem[] = [];

    lines.forEach((line, idx) => {
      const trimmed = line.trim();
      if (!trimmed) return;

      const lower = trimmed.toLowerCase();
      if (
        (lower.includes('mã') || lower.includes('style') || lower.includes('item')) &&
        (lower.includes('số lượng') || lower.includes('qty') || lower.includes('sl') || lower.includes('quantity'))
      ) {
        return;
      }

      let parts: string[] = [];
      if (trimmed.includes('\t')) {
        parts = trimmed.split('\t');
      } else if (trimmed.includes('|')) {
        parts = trimmed.split('|');
      } else if (trimmed.includes(',')) {
        parts = trimmed.split(',');
      } else {
        parts = trimmed.split(/\s+/);
      }

      parts = parts.map((p) => p.trim()).filter((p) => p.length > 0);
      if (parts.length === 0) return;

      let rawStyleCode = parts[0] || '';
      rawStyleCode = rawStyleCode.replace(/^["']|["']$/g, '').trim();

      const rawQtyStr = (parts[1] || '').replace(/[,.\s]/g, '');
      const parsedQty = parseInt(rawQtyStr, 10);

      const remainingNotes = parts.slice(2).join(' ');

      const isGoProcess =
        /g[oò]|gò\s*không\s*may|\.g/i.test(remainingNotes) ||
        rawStyleCode.toUpperCase().endsWith('.G');

      const processType = isGoProcess ? ProcessType.GoKhongMay : ProcessType.Standard;

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
      width: 44,
      align: 'center',
      render: (_, __, idx) => (
        <span className="font-mono text-xs text-slate-400">{idx + 1}</span>
      ),
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
            <div className="text-[11px] text-slate-400 truncate max-w-[180px]" title={record.description}>
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
      width: 110,
      align: 'right',
      render: (qty: number, record) => (
        <span
          className={`font-mono font-semibold text-xs ${
            record.isValid ? 'text-slate-900' : 'text-red-600'
          }`}
        >
          {record.isValid ? qty.toLocaleString() : 'Lỗi'}
        </span>
      ),
    },
    {
      title: 'Công đoạn',
      dataIndex: 'processType',
      key: 'processType',
      width: 130,
      render: (proc: ProcessType) =>
        proc === ProcessType.GoKhongMay ? (
          <span className="text-xs text-purple-700 font-medium">
            Gò không may (.G)
          </span>
        ) : (
          <span className="text-xs text-slate-700">
            Thành hình
          </span>
        ),
    },
    {
      title: 'Quy cách',
      dataIndex: 'pairPerCarton',
      key: 'pairPerCarton',
      width: 90,
      align: 'right',
      render: (pairs: number) => (
        <span className="font-mono text-xs text-slate-700">
          {pairs} đôi/thùng
        </span>
      ),
    },
    {
      title: 'Đối chiếu Master',
      key: 'matchStatus',
      width: 120,
      align: 'center',
      render: (_, record) =>
        record.matchedProduct ? (
          <Tooltip title={`CMT: $${record.unitPriceCMT} | DAP: $${record.unitPriceDAP}`}>
            <span className="inline-flex items-center gap-1 text-xs text-emerald-700">
              <CheckCircleOutlined className="text-[10px]" /> Đã khớp
            </span>
          </Tooltip>
        ) : (
          <span className="inline-flex items-center gap-1 text-xs text-slate-400">
            <ExclamationCircleOutlined className="text-[10px]" /> Mã mới
          </span>
        ),
    },
  ];

  return (
    <Modal
      title={
        <div className="pb-1">
          <div className="text-sm font-semibold text-slate-900">
            Dán nhanh dữ liệu (Quick Paste)
          </div>
          <div className="text-xs text-slate-500 font-normal mt-0.5">
            Dán dữ liệu được sao chép từ Excel hoặc Zalo để phân tích và đưa vào hóa đơn
          </div>
        </div>
      }
      open={visible}
      onCancel={onClose}
      width={860}
      footer={[
        <Button key="cancel" onClick={onClose} className="border-slate-300 text-slate-700 text-xs h-9 px-3.5">
          Hủy
        </Button>,
        <Button
          key="apply"
          type="primary"
          disabled={parsedData.validCount === 0}
          onClick={handleApply}
          className="bg-blue-600 hover:bg-blue-700 text-white text-xs h-9 px-4"
        >
          Áp dụng dữ liệu ({parsedData.validCount} mã - {parsedData.totalQuantity.toLocaleString()} đôi)
        </Button>,
      ]}
    >
      <div className="space-y-4 py-2">
        {/* Helper bar */}
        <div className="flex items-center justify-between text-xs text-slate-500">
          <span>
            Định dạng: <strong>[Mã hình thể] [Số lượng] [Ghi chú / Công đoạn]</strong>
          </span>
          <Space size="middle">
            <Button size="small" type="link" onClick={handlePasteExample} className="text-blue-600 p-0 text-xs">
              Dán dữ liệu mẫu
            </Button>
            <span className="text-slate-300">|</span>
            <Button size="small" type="link" icon={<ClearOutlined />} onClick={handleClear} className="text-slate-500 hover:text-slate-700 p-0 text-xs">
              Xóa nội dung
            </Button>
          </Space>
        </div>

        {/* Textarea */}
        <TextArea
          rows={5}
          value={pastedText}
          onChange={(e) => setPastedText(e.target.value)}
          placeholder={`Dán dữ liệu tại đây (từ Excel hoặc văn bản):
42072-030\t36\tThành phẩm
45428-2LX\t4032\tGÒ KHÔNG MAY
51200-1BK\t5\tMẫu lẻ`}
          className="font-mono text-xs"
        />

        {/* Parsed summary indicators */}
        {pastedText.trim() && (
          <div className="grid grid-cols-3 gap-3 p-3 bg-slate-50 border border-slate-200 rounded-lg text-center">
            <div className="bg-white p-2.5 rounded border border-slate-200">
              <div className="text-[11px] font-medium text-slate-500 uppercase">Mã hợp lệ</div>
              <div className="mt-0.5 text-base font-semibold font-mono text-slate-900">
                {parsedData.validCount} <span className="text-xs font-normal text-slate-400">mặt hàng</span>
              </div>
            </div>

            <div className="bg-white p-2.5 rounded border border-slate-200">
              <div className="text-[11px] font-medium text-slate-500 uppercase">Tổng số đôi</div>
              <div className="mt-0.5 text-base font-semibold font-mono text-slate-900">
                {parsedData.totalQuantity.toLocaleString()} <span className="text-xs font-normal text-slate-400">đôi</span>
              </div>
            </div>

            <div className="bg-white p-2.5 rounded border border-slate-200">
              <div className="text-[11px] font-medium text-slate-500 uppercase">Khớp Master Data</div>
              <div className="mt-0.5 text-base font-semibold font-mono text-slate-900">
                {parsedData.items.filter((r) => r.matchedProduct).length} / {parsedData.validCount}
              </div>
            </div>
          </div>
        )}

        {parsedData.invalidCount > 0 && (
          <Alert
            type="warning"
            showIcon
            message={`Có ${parsedData.invalidCount} dòng không hợp lệ sẽ tự động được bỏ qua.`}
            className="text-xs py-1.5"
          />
        )}

        {/* Parsed Preview Table */}
        {parsedData.items.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-700 uppercase tracking-wider">
                Kết quả nhận diện ({parsedData.items.length} dòng):
              </span>
              <div className="flex items-center space-x-2 text-xs text-slate-600">
                <span>Chế độ:</span>
                <Radio.Group
                  size="small"
                  value={applyMode}
                  onChange={(e) => setApplyMode(e.target.value)}
                  optionType="button"
                >
                  <Radio.Button value="replace">Thay thế bảng</Radio.Button>
                  <Radio.Button value="append">Thêm nối tiếp</Radio.Button>
                </Radio.Group>
              </div>
            </div>

            <Table
              dataSource={parsedData.items}
              columns={columns}
              pagination={false}
              size="small"
              scroll={{ y: 220 }}
            />
          </div>
        )}
      </div>
    </Modal>
  );
};
