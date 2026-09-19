import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, message, Skeleton } from 'antd';
import { CalculatorOutlined, SaveOutlined } from '@ant-design/icons';
import { bomApi } from '../api/bomApi';
import { BomResultTable } from '../components/BomResultTable';
import { ProductionOrderForm } from '../components/ProductionOrderForm';
import { SizeMatrixInput } from '../components/SizeMatrixInput';
import type { BomMasterOption, MaterialRequirementResult, SaveProductionOrderRequest } from '../types/bom';

const FEMALE_SIZES = ['35', '36', '37', '38', '39', '40'];

export const BomCalculatorPage = () => {
  const [orderId, setOrderId] = useState<number>();
  const [orderNo, setOrderNo] = useState('');
  const [styleCode, setStyleCode] = useState<string>();
  const [processType, setProcessType] = useState<'Standard' | 'GoKhongMay' | 1 | 2>('Standard');
  const [bomSelection, setBomSelection] = useState<string>();
  const [sizes, setSizes] = useState<string[]>(FEMALE_SIZES);
  const [sizeValues, setSizeValues] = useState<Record<string, number>>({});
  const [bomOptions, setBomOptions] = useState<BomMasterOption[]>([]);
  const [result, setResult] = useState<MaterialRequirementResult>();
  const [loadingMasters, setLoadingMasters] = useState(true);
  const [saving, setSaving] = useState(false);
  const [calculating, setCalculating] = useState(false);

  useEffect(() => {
    let active = true;
    bomApi.getMasters()
      .then((items) => {
        if (active) setBomOptions(items);
      })
      .finally(() => {
        if (active) setLoadingMasters(false);
      });
    return () => { active = false; };
  }, []);

  const totalQuantity = useMemo(
    () => sizes.reduce((sum, size) => sum + (sizeValues[size] ?? 0), 0),
    [sizeValues, sizes],
  );

  const markChanged = () => setResult(undefined);

  const buildRequest = (): SaveProductionOrderRequest | undefined => {
    if (!orderNo.trim()) {
      message.warning('Vui lòng nhập số lệnh sản xuất.');
      return undefined;
    }
    if (!styleCode) {
      message.warning('Vui lòng chọn mã hình thể có BOM.');
      return undefined;
    }
    const sizeRuns = sizes
      .filter((size) => (sizeValues[size] ?? 0) > 0)
      .map((size) => ({ sizeName: size, quantity: sizeValues[size] }));
    if (sizeRuns.length === 0) {
      message.warning('Vui lòng nhập số lượng cho ít nhất một size.');
      return undefined;
    }
    return { orderNo: orderNo.trim(), styleCode, processType, sizeRuns };
  };

  const persistOrder = async (request: SaveProductionOrderRequest) => {
    const saved = orderId
      ? await bomApi.updateOrder(orderId, request)
      : await bomApi.createOrder(request);
    setOrderId(saved.id);
    return saved;
  };

  const handleSave = async () => {
    const request = buildRequest();
    if (!request) return;
    setSaving(true);
    try {
      await persistOrder(request);
      message.success('Đã lưu lệnh sản xuất.');
    } finally {
      setSaving(false);
    }
  };

  const handleCalculate = async () => {
    const request = buildRequest();
    if (!request) return;
    setCalculating(true);
    try {
      const saved = await persistOrder(request);
      const calculated = await bomApi.calculate(saved.id);
      setResult(calculated);
      message.success('Đã bóc tách nhu cầu vật tư.');
    } finally {
      setCalculating(false);
    }
  };

  const busy = saving || calculating;

  return (
    <div className="enterprise-page pb-2">
      <div className="enterprise-page-header">
        <div>
          <h1 className="enterprise-page-title">BOM & Material Calculator</h1>
          <p className="enterprise-page-description">Nhập dải size và tự động bóc tách nguyên phụ liệu cho lệnh sản xuất.</p>
        </div>
        <div className="enterprise-actions">
          <Button icon={<SaveOutlined />} loading={saving} disabled={calculating} onClick={handleSave}>
            Lưu Lệnh sản xuất
          </Button>
          <Button type="primary" icon={<CalculatorOutlined />} loading={calculating} disabled={saving} onClick={handleCalculate}>
            Chạy Bóc tách Vật tư
          </Button>
        </div>
      </div>

      {loadingMasters ? (
        <section className="enterprise-panel p-5"><Skeleton active paragraph={{ rows: 2 }} /></section>
      ) : bomOptions.length === 0 ? (
        <Alert type="warning" showIcon message="Chưa có BOM khả dụng" description="Hãy tạo ít nhất một BOM trước khi lập lệnh sản xuất." />
      ) : (
        <ProductionOrderForm
          orderNo={orderNo}
          bomSelection={bomSelection}
          totalQuantity={totalQuantity}
          bomOptions={bomOptions}
          loadingBomOptions={loadingMasters}
          onOrderNoChange={(value) => {
            setOrderNo(value);
            markChanged();
          }}
          onBomSelectionChange={(value) => {
            const selected = bomOptions.find((item) => `${item.styleCode}::${item.processType}` === value);
            setBomSelection(value);
            setStyleCode(selected?.styleCode);
            setProcessType(selected?.processType ?? 'Standard');
            markChanged();
          }}
        />
      )}

      <SizeMatrixInput
        sizes={sizes}
        values={sizeValues}
        disabled={busy}
        onSizesChange={(nextSizes) => {
          setSizes(nextSizes);
          setSizeValues((current) => Object.fromEntries(nextSizes.map((size) => [size, current[size] ?? 0])));
          markChanged();
        }}
        onChange={(size, quantity) => {
          setSizeValues((current) => ({ ...current, [size]: quantity }));
          markChanged();
        }}
        onClear={() => {
          setSizeValues({});
          markChanged();
        }}
      />

      <BomResultTable result={result} />
    </div>
  );
};
