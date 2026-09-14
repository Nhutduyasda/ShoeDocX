import React, { useState, useEffect } from 'react';
import { Modal, Button, InputNumber, Space, Tag, Input, Row, Col } from 'antd';
import {
  CheckCircleFilled,
  CloseCircleFilled,
  PlusOutlined,
  DeleteOutlined,
  ThunderboltOutlined,
  ClearOutlined,
} from '@ant-design/icons';

const DEFAULT_SIZES = ['35', '36', '37', '38', '39', '40', '41', '42', '43', '44'];

interface SizeBreakdownModalProps {
  open: boolean;
  onClose: () => void;
  itemIndex: number;
  itemStyleCode: string;
  itemDescription?: string;
  targetQuantity: number;
  initialJson?: string | null;
  onSave: (updatedJson: string) => void;
}

export const SizeBreakdownModal: React.FC<SizeBreakdownModalProps> = ({
  open,
  onClose,
  itemIndex,
  itemStyleCode,
  itemDescription,
  targetQuantity,
  initialJson,
  onSave,
}) => {
  const [breakdown, setBreakdown] = useState<{ [size: string]: number }>({});
  const [newSizeInput, setNewSizeInput] = useState<string>('');

  useEffect(() => {
    if (!open) return;
    if (initialJson && initialJson.trim()) {
      try {
        const parsed = JSON.parse(initialJson);
        if (typeof parsed === 'object' && parsed !== null && !Array.isArray(parsed)) {
          const valid: { [size: string]: number } = {};
          Object.entries(parsed).forEach(([k, v]) => {
            const num = Number(v);
            if (!isNaN(num) && num > 0) valid[k] = num;
          });
          setBreakdown(valid);
          return;
        }
      } catch {
        // invalid JSON
      }
    }

    const init: { [size: string]: number } = {};
    DEFAULT_SIZES.forEach((s) => {
      init[s] = 0;
    });
    setBreakdown(init);
  }, [open, initialJson]);

  const totalPairs = Object.values(breakdown).reduce((sum, val) => sum + (val || 0), 0);
  const isMatched = totalPairs === targetQuantity;
  const diff = totalPairs - targetQuantity;

  const handleSizeChange = (size: string, qty: number | null) => {
    setBreakdown((prev) => ({
      ...prev,
      [size]: Math.max(0, qty || 0),
    }));
  };

  const handleAddCustomSize = () => {
    const s = newSizeInput.trim().toUpperCase();
    if (!s) return;
    if (!(s in breakdown)) {
      setBreakdown((prev) => ({
        ...prev,
        [s]: 0,
      }));
    }
    setNewSizeInput('');
  };

  const handleRemoveSize = (size: string) => {
    setBreakdown((prev) => {
      const copy = { ...prev };
      delete copy[size];
      return copy;
    });
  };

  const handleAutoDistribute = () => {
    const activeSizes = Object.keys(breakdown);
    if (activeSizes.length === 0 || targetQuantity <= 0) return;

    const basePerSize = Math.floor(targetQuantity / activeSizes.length);
    let remainder = targetQuantity % activeSizes.length;

    const updated: { [size: string]: number } = {};
    activeSizes.forEach((s) => {
      updated[s] = basePerSize + (remainder > 0 ? 1 : 0);
      if (remainder > 0) remainder--;
    });
    setBreakdown(updated);
  };

  const handleClearAll = () => {
    const cleared: { [size: string]: number } = {};
    Object.keys(breakdown).forEach((s) => {
      cleared[s] = 0;
    });
    setBreakdown(cleared);
  };

  const handleSave = () => {
    const filtered: { [size: string]: number } = {};
    Object.entries(breakdown).forEach(([s, q]) => {
      if (q > 0) filtered[s] = q;
    });
    onSave(JSON.stringify(filtered));
    onClose();
  };

  return (
    <Modal
      title={
        <div className="flex items-center space-x-2">
          <span className="font-semibold text-slate-800">
            Phân Bổ Size Breakdown: Dòng #{itemIndex + 1} ({itemStyleCode})
          </span>
        </div>
      }
      open={open}
      onCancel={onClose}
      width={720}
      footer={[
        <Button key="cancel" onClick={onClose}>
          Hủy bỏ
        </Button>,
        <Button
          key="save"
          type="primary"
          disabled={!isMatched}
          onClick={handleSave}
          className={isMatched ? 'bg-emerald-600 hover:bg-emerald-500' : ''}
        >
          {isMatched ? 'Lưu Size Breakdown' : `Chưa khớp (${totalPairs}/${targetQuantity} đôi)`}
        </Button>,
      ]}
    >
      <div className="space-y-4 py-2">
        {itemDescription && (
          <div className="text-xs text-slate-500 bg-slate-50 p-2 rounded border border-slate-200">
            Mô tả sản phẩm: <span className="font-medium text-slate-800">{itemDescription}</span>
          </div>
        )}

        {/* Trạng thái đối soát khớp số lượng */}
        <div
          className={`p-3 rounded-lg border flex items-center justify-between ${
            isMatched
              ? 'bg-emerald-50/60 border-emerald-300 text-emerald-950'
              : 'bg-rose-50/60 border-rose-300 text-rose-950'
          }`}
        >
          <div className="flex items-center space-x-2">
            {isMatched ? (
              <CheckCircleFilled className="text-emerald-600 text-lg" />
            ) : (
              <CloseCircleFilled className="text-rose-600 text-lg" />
            )}
            <div>
              <div className="font-semibold text-sm">
                {isMatched
                  ? 'Số lượng Size Breakdown khớp hoàn toàn với số lượng dòng'
                  : 'Cảnh báo: Tổng Size Breakdown lệch với số lượng khai báo'}
              </div>
              <div className="text-xs opacity-80">
                Mục tiêu: <strong>{targetQuantity.toLocaleString()} đôi</strong> | Hiện tại:{' '}
                <strong>{totalPairs.toLocaleString()} đôi</strong>
                {!isMatched && (
                  <span className="ml-1 font-bold">
                    ({diff > 0 ? `Thừa +${diff} đôi` : `Thiếu ${Math.abs(diff)} đôi`})
                  </span>
                )}
              </div>
            </div>
          </div>
          <Tag color={isMatched ? 'success' : 'error'} className="text-sm px-2.5 py-0.5 font-bold">
            {totalPairs} / {targetQuantity}
          </Tag>
        </div>

        {/* Thanh công cụ thao tác nhanh */}
        <div className="flex flex-wrap items-center justify-between gap-2 bg-slate-50 p-2.5 rounded-lg border border-slate-200">
          <Space size={8}>
            <Button
              size="small"
              icon={<ThunderboltOutlined />}
              onClick={handleAutoDistribute}
              className="text-xs"
            >
              Phân bổ đều ({targetQuantity} đôi)
            </Button>
            <Button
              size="small"
              icon={<ClearOutlined />}
              onClick={handleClearAll}
              className="text-xs"
            >
              Xóa số lượng
            </Button>
          </Space>

          <Space.Compact size="small" style={{ maxWidth: 220 }}>
            <Input
              placeholder="Thêm size (vd: 36.5, 45)"
              value={newSizeInput}
              onChange={(e) => setNewSizeInput(e.target.value)}
              onPressEnter={handleAddCustomSize}
              className="text-xs"
            />
            <Button type="default" icon={<PlusOutlined />} onClick={handleAddCustomSize}>
              Thêm
            </Button>
          </Space.Compact>
        </div>

        {/* Lưới nhập từng size */}
        <div className="max-h-72 overflow-y-auto pr-1">
          <Row gutter={[10, 10]}>
            {Object.entries(breakdown).map(([size, qty]) => (
              <Col span={6} key={size}>
                <div className="flex items-center justify-between p-2 bg-white border border-slate-200 rounded-lg hover:border-blue-400 transition-colors">
                  <div className="flex flex-col">
                    <span className="text-xs font-bold text-slate-700 font-mono">Size {size}</span>
                    <InputNumber
                      size="small"
                      min={0}
                      value={qty}
                      onChange={(v) => handleSizeChange(size, v)}
                      className="w-20 text-xs font-mono font-semibold"
                      placeholder="0"
                    />
                  </div>
                  <Button
                    type="text"
                    danger
                    size="small"
                    icon={<DeleteOutlined className="text-[11px]" />}
                    onClick={() => handleRemoveSize(size)}
                    className="opacity-50 hover:opacity-100 ml-1"
                  />
                </div>
              </Col>
            ))}
          </Row>
        </div>
      </div>
    </Modal>
  );
};
