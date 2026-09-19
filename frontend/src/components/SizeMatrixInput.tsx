import type { FC, KeyboardEvent } from 'react';
import { Button, InputNumber, Segmented, Tooltip } from 'antd';
import { ClearOutlined, InfoCircleOutlined } from '@ant-design/icons';

const FEMALE_SIZES = ['35', '36', '37', '38', '39', '40'];
const MALE_SIZES = ['39', '40', '41', '42', '43', '44', '45'];

interface SizeMatrixInputProps {
  sizes: string[];
  values: Record<string, number>;
  disabled?: boolean;
  onSizesChange: (sizes: string[]) => void;
  onChange: (size: string, quantity: number) => void;
  onClear: () => void;
}

export const SizeMatrixInput: FC<SizeMatrixInputProps> = ({
  sizes,
  values,
  disabled,
  onSizesChange,
  onChange,
  onClear,
}) => {
  const selectedRange = sizes.join(',') === MALE_SIZES.join(',') ? 'male' : 'female';

  const moveFocus = (event: KeyboardEvent<HTMLInputElement>, offset: number) => {
    const inputs = Array.from(
      event.currentTarget.closest('table')?.querySelectorAll<HTMLInputElement>('input[data-size-input="true"]') ?? [],
    );
    const currentIndex = inputs.indexOf(event.currentTarget);
    const target = inputs[currentIndex + offset];
    if (target) {
      event.preventDefault();
      target.focus();
      target.select();
    }
  };

  return (
    <section className="enterprise-panel min-w-0" aria-labelledby="size-matrix-heading">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-4 py-3 sm:px-5">
        <div>
          <div className="flex items-center gap-2">
            <h2 id="size-matrix-heading" className="m-0 text-sm font-semibold text-slate-800">Dải size sản xuất</h2>
            <Tooltip title="Dùng Tab hoặc phím mũi tên trái/phải để di chuyển giữa các ô.">
              <InfoCircleOutlined className="text-xs text-slate-400" />
            </Tooltip>
          </div>
          <p className="m-0 mt-1 text-xs text-slate-500">Nhập số đôi theo chiều ngang như trên bảng tính.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Segmented
            size="small"
            value={selectedRange}
            disabled={disabled}
            onChange={(value) => onSizesChange(value === 'male' ? MALE_SIZES : FEMALE_SIZES)}
            options={[
              { label: 'Nữ 35–40', value: 'female' },
              { label: 'Nam 39–45', value: 'male' },
            ]}
          />
          <Button size="small" icon={<ClearOutlined />} onClick={onClear} disabled={disabled}>
            Xóa trắng dải size
          </Button>
        </div>
      </div>

      <div className="max-w-full overflow-x-auto p-4 sm:p-5">
        <table className="w-full min-w-[660px] table-fixed border-separate border-spacing-0 overflow-hidden rounded-md border border-slate-200">
          <thead>
            <tr>
              <th className="w-32 border-b border-r border-slate-200 bg-slate-100 px-3 py-3 text-left text-xs font-semibold text-slate-600">
                SIZE
              </th>
              {sizes.map((size) => (
                <th key={size} className="border-b border-r border-slate-200 bg-slate-50 px-2 py-3 text-center font-mono text-sm font-semibold text-slate-700 last:border-r-0">
                  {size}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            <tr>
              <th className="border-r border-slate-200 bg-white px-3 py-3 text-left text-xs font-medium text-slate-500">
                SỐ LƯỢNG
              </th>
              {sizes.map((size) => (
                <td key={size} className="border-r border-slate-200 bg-white p-1.5 last:border-r-0 focus-within:bg-indigo-50 focus-within:ring-2 focus-within:ring-inset focus-within:ring-indigo-500">
                  <InputNumber
                    data-size-input="true"
                    aria-label={`Số lượng size ${size}`}
                    value={values[size] || undefined}
                    min={0}
                    max={999999}
                    precision={0}
                    controls={false}
                    disabled={disabled}
                    placeholder="0"
                    className="w-full font-mono"
                    onChange={(value) => onChange(size, Math.max(0, Number(value ?? 0)))}
                    onFocus={(event) => event.target.select()}
                    onKeyDown={(event) => {
                      if (event.key === 'ArrowLeft') moveFocus(event, -1);
                      if (event.key === 'ArrowRight') moveFocus(event, 1);
                    }}
                  />
                </td>
              ))}
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  );
};
