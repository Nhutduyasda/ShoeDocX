import React from 'react';
import { Modal, Table, Button, Tag, Tooltip } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined, InfoCircleOutlined } from '@ant-design/icons';
import type { PklPreviewResponse, PklBreakdownItem } from '../types';
import { ProcessType } from '../types';

interface PklPreviewModalProps {
  visible: boolean;
  onClose: () => void;
  data: PklPreviewResponse | null;
  onExportExcel: () => void;
  exporting: boolean;
  disableExport?: boolean;
}

export const PklPreviewModal: React.FC<PklPreviewModalProps> = ({
  visible,
  onClose,
  data,
  onExportExcel,
  exporting,
  disableExport = false,
}) => {
  if (!data) return null;

  const columns: ColumnsType<PklBreakdownItem> = [
    {
      title: 'Dải số kiện',
      dataIndex: 'cartonRange',
      key: 'cartonRange',
      width: 110,
      render: (range: string) => (
        <span className="font-mono text-xs font-semibold text-slate-900 bg-slate-50 px-2 py-0.5 rounded border border-slate-200">
          {range}
        </span>
      ),
    },
    {
      title: 'Phân loại',
      dataIndex: 'isOddCarton',
      key: 'isOddCarton',
      width: 95,
      render: (isOdd: boolean) =>
        isOdd ? (
          <Tag className="bg-amber-50 text-amber-700 border-amber-200 text-[11px] m-0">
            Thùng lẻ
          </Tag>
        ) : (
          <Tag className="bg-slate-50 text-slate-600 border-slate-200 text-[11px] m-0">
            Thùng chẵn
          </Tag>
        ),
    },
    {
      title: 'Mã hàng hóa',
      dataIndex: 'fullItemCode',
      key: 'fullItemCode',
      width: 180,
      render: (code: string, record) => (
        <div>
          <div className="font-mono text-xs font-medium text-slate-900">{code}</div>
          <div className="text-[11px] text-slate-400 truncate max-w-[200px]">
            {record.description || record.styleCode}
          </div>
        </div>
      ),
    },
    {
      title: 'Quy trình',
      dataIndex: 'processType',
      key: 'processType',
      width: 110,
      render: (type: number) =>
        type === ProcessType.GoKhongMay ? (
          <span className="text-xs text-purple-700 font-medium">
            Gò (.G)
          </span>
        ) : (
          <span className="text-xs text-slate-600">
            Thành hình
          </span>
        ),
    },
    {
      title: 'Quy cách',
      key: 'standardPairPerCarton',
      align: 'right',
      width: 95,
      render: (_, record) => {
        const std = record.standardPairPerCarton || 12;
        return (
          <span className="font-mono text-xs text-slate-700">
            {std} đôi/thùng
          </span>
        );
      },
    },
    {
      title: 'Số kiện',
      dataIndex: 'cartonCount',
      key: 'cartonCount',
      align: 'right',
      width: 80,
      render: (count: number) => (
        <span className="font-mono font-medium text-slate-800 text-xs">{count}</span>
      ),
    },
    {
      title: 'Đôi/Kiện',
      dataIndex: 'pairsPerCarton',
      key: 'pairsPerCarton',
      align: 'right',
      width: 80,
      render: (pairs: number, record) => (
        <span className={`font-mono text-xs ${record.isOddCarton ? 'text-amber-700 font-semibold' : 'text-slate-600'}`}>
          {pairs}
        </span>
      ),
    },
    {
      title: 'Tổng số đôi',
      dataIndex: 'quantity',
      key: 'quantity',
      align: 'right',
      width: 105,
      render: (qty: number) => (
        <span className="font-mono font-semibold text-slate-900 text-xs">
          {qty.toLocaleString()}
        </span>
      ),
    },
    {
      title: 'N.W (KGS)',
      dataIndex: 'netWeight',
      key: 'netWeight',
      align: 'right',
      width: 100,
      render: (nw: number) => (
        <span className="font-mono text-xs text-slate-700">{nw.toFixed(2)}</span>
      ),
    },
    {
      title: 'G.W (KGS)',
      dataIndex: 'grossWeight',
      key: 'grossWeight',
      align: 'right',
      width: 100,
      render: (gw: number) => (
        <span className="font-mono text-xs text-slate-700">{gw.toFixed(0)}</span>
      ),
    },
  ];

  return (
    <Modal
      title={
        <div className="flex items-center justify-between pr-6 pb-2 border-b border-slate-100">
          <div>
            <div className="text-sm font-semibold text-slate-900">
              Phân rã Đóng gói Packing List (PKL)
            </div>
            <div className="text-xs text-slate-500 font-normal mt-0.5">
              Chi tiết quy cách đóng gói và dải số kiện lũy kế
            </div>
          </div>
          <span className="font-mono text-xs bg-slate-50 text-slate-700 px-2 py-0.5 rounded border border-slate-200">
            {data.invoiceNo} {data.poSuffix}
          </span>
        </div>
      }
      open={visible}
      onCancel={onClose}
      width={1050}
      footer={[
        <Button key="close" onClick={onClose} className="border-slate-300 text-slate-700 text-xs h-9 px-3.5">
          Đóng
        </Button>,
        <Tooltip
          key="export"
          title={
            disableExport
              ? 'Vui lòng cập nhật thông tin Master Data cho các mã còn thiếu trước khi xuất file!'
              : ''
          }
        >
          <span>
            <Button
              type="primary"
              icon={<DownloadOutlined />}
              loading={exporting}
              disabled={disableExport}
              onClick={onExportExcel}
              className={
                disableExport
                  ? 'bg-slate-300 text-slate-500 cursor-not-allowed text-xs h-9 px-4'
                  : 'bg-blue-600 hover:bg-blue-700 text-white text-xs h-9 px-4'
              }
            >
              Xuất File Excel (.xlsx)
            </Button>
          </span>
        </Tooltip>,
      ]}
    >
      <div className="py-2 space-y-4">
        {/* KPI Summary Cards */}
        <div className="grid grid-cols-4 gap-3 bg-slate-50 p-3 rounded-lg border border-slate-200">
          <div className="bg-white p-3 rounded border border-slate-200">
            <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
              Tổng số lượng
            </div>
            <div className="mt-1 flex items-baseline">
              <span className="text-xl font-bold font-mono text-slate-900">
                {data.totalQuantity.toLocaleString()}
              </span>
              <span className="ml-1 text-xs text-slate-500">đôi</span>
            </div>
          </div>

          <div className="bg-white p-3 rounded border border-slate-200">
            <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
              Tổng số kiện
            </div>
            <div className="mt-1 flex items-baseline">
              <span className="text-xl font-bold font-mono text-slate-900">
                {data.totalCartons.toLocaleString()}
              </span>
              <span className="ml-1 text-xs text-slate-500">thùng</span>
            </div>
          </div>

          <div className="bg-white p-3 rounded border border-slate-200">
            <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
              Trọng lượng Net (N.W)
            </div>
            <div className="mt-1 flex items-baseline">
              <span className="text-xl font-bold font-mono text-slate-900">
                {data.totalNetWeight.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
              </span>
              <span className="ml-1 text-xs text-slate-500">KGS</span>
            </div>
          </div>

          <div className="bg-white p-3 rounded border border-slate-200">
            <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
              Trọng lượng Gross (G.W)
            </div>
            <div className="mt-1 flex items-baseline">
              <span className="text-xl font-bold font-mono text-slate-900">
                {data.totalGrossWeight.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 0 })}
              </span>
              <span className="ml-1 text-xs text-slate-500">KGS</span>
            </div>
          </div>
        </div>

        {/* Packing List Breakdown Table */}
        <Table
          dataSource={data.breakdownItems}
          columns={columns}
          rowKey={(_, index) => `${index}`}
          pagination={false}
          size="small"
          scroll={{ y: 360 }}
          summary={() => (
            <Table.Summary fixed>
              <Table.Summary.Row className="bg-slate-50 font-semibold text-slate-900">
                <Table.Summary.Cell index={0} colSpan={5}>
                  <div className="font-semibold text-slate-800 pl-2 text-xs">TỔNG CỘNG PACKING LIST</div>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={5} align="right">
                  <span className="font-mono text-slate-900 font-bold text-xs">
                    {data.totalCartons.toLocaleString()}
                  </span>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={6} align="right">
                  -
                </Table.Summary.Cell>
                <Table.Summary.Cell index={7} align="right">
                  <span className="font-mono text-slate-900 font-bold text-xs">
                    {data.totalQuantity.toLocaleString()}
                  </span>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={8} align="right">
                  <span className="font-mono text-slate-800 text-xs">
                    {data.totalNetWeight.toFixed(2)}
                  </span>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={9} align="right">
                  <span className="font-mono text-slate-800 text-xs">
                    {data.totalGrossWeight.toFixed(0)}
                  </span>
                </Table.Summary.Cell>
              </Table.Summary.Row>
            </Table.Summary>
          )}
        />

        <div className="text-xs text-slate-500 flex items-start space-x-1.5 pt-1">
          <InfoCircleOutlined className="text-slate-400 mt-0.5 shrink-0" />
          <span>
            Quy cách đóng gói tự động chia tách kiện chẵn và kiện lẻ theo quy định Master Data. Toàn bộ dải số kiện và trọng lượng sẽ được bảo toàn khi xuất file Excel 3 sheet thực tế.
          </span>
        </div>
      </div>
    </Modal>
  );
};
