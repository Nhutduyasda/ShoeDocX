import { Alert, Collapse, List, Tag } from 'antd';
import type { ValidateSplitResult } from '../types/shipmentDispatch';

export function DispatchValidationPanel({ result }: { result: ValidateSplitResult | null }) {
  if (!result) return null;
  return <div className="space-y-2">
    {result.errors.length > 0 && <Alert type="error" showIcon message="Lỗi phải xử lý trước khi xuất" description={<List size="small" dataSource={result.errors} renderItem={(item) => <List.Item><span>{item.message}</span><Tag color="red">{item.code}</Tag></List.Item>} />} />}
    {result.warnings.length > 0 && <Alert type="warning" showIcon message="Cảnh báo nghiệp vụ" description={<List size="small" dataSource={result.warnings} renderItem={(item) => <List.Item>{item.message}</List.Item>} />} />}
    {result.itemChecks.length > 0 && <Collapse size="small" items={[{ key: 'checks', label: `Đối soát ${result.itemChecks.length} mã hàng`, children:
      <List size="small" dataSource={result.itemChecks} renderItem={(item) => <List.Item>
        <span className="font-mono">{item.styleCode} · {item.processType === 2 ? 'Gò không may' : 'Thành hình'}</span>
        <span>{item.originalQty.toLocaleString()} → {item.allocatedQty.toLocaleString()} <Tag color={item.isMatched ? 'green' : 'red'}>{item.isMatched ? 'Khớp' : `${item.discrepancy > 0 ? '+' : ''}${item.discrepancy}`}</Tag></span>
      </List.Item>} /> }]} />}
  </div>;
}
