import { CheckCircleFilled, CloseCircleFilled, SafetyCertificateOutlined } from '@ant-design/icons';
import { Button } from 'antd';

interface Props {
  original: number; allocated: number; serverValid?: boolean; isDirty: boolean;
  validating: boolean; exporting: boolean; canValidate: boolean; canExport: boolean;
  onValidate: () => void; onExport: () => void;
}

export function DispatchSafetyBar(props: Props) {
  const discrepancy = props.allocated - props.original;
  return <div className="sticky bottom-0 z-10 border-t border-slate-200 bg-white/95 backdrop-blur px-5 py-3 shadow-[0_-4px_16px_rgba(15,23,42,0.08)]">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div className="flex flex-wrap items-center gap-x-5 gap-y-1 text-xs">
        <span>Tổng gốc: <strong className="font-mono text-slate-900">{props.original.toLocaleString()}</strong> đôi</span>
        <span>Đã chia: <strong className="font-mono text-slate-900">{props.allocated.toLocaleString()}</strong> đôi</span>
        <span className={discrepancy === 0 ? 'text-emerald-700' : 'text-red-600'}>{discrepancy === 0 ? <><CheckCircleFilled /> Khớp trên màn hình</> : <><CloseCircleFilled /> {discrepancy < 0 ? `Thiếu ${(-discrepancy).toLocaleString()}` : `Dư ${discrepancy.toLocaleString()}`} đôi</>}</span>
        <span className={props.serverValid && !props.isDirty ? 'text-emerald-700' : 'text-slate-500'}><SafetyCertificateOutlined /> {props.serverValid && !props.isDirty ? 'Backend đã xác nhận hợp lệ' : 'Chưa kiểm tra với máy chủ'}</span>
      </div>
      <div className="flex gap-2">
        <Button disabled={!props.canValidate || props.exporting} loading={props.validating} onClick={props.onValidate}>Kiểm tra an toàn</Button>
        <Button type="primary" disabled={!props.canExport} loading={props.exporting} onClick={props.onExport}>{props.exporting ? 'Đang tạo bộ chứng từ...' : 'Xuất bộ hóa đơn (.zip)'}</Button>
      </div>
    </div>
  </div>;
}
