import React from 'react';
import { Modal, Table, Button } from 'antd';
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
}

export const PklPreviewModal: React.FC<PklPreviewModalProps> = ({
  visible,
  onClose,
  data,
  onExportExcel,
  exporting,
}) => {
  if (!data) return null;

  const columns: ColumnsType<PklBreakdownItem> = [
    {
      title: 'Dải số kiện',
      dataIndex: 'cartonRange',
      key: 'cartonRange',
      width: 120,
      render: (range: string) => (
        <span className="inline-flex items-center px-2 py-0.5 rounded font-mono text-xs font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200">
          {range}
        </span>
      ),
    },
    {
      title: 'Phân loại',
      dataIndex: 'isOddCarton',
      key: 'isOddCarton',
      width: 110,
      render: (isOdd: boolean) =>
        isOdd ? (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-amber-50 text-amber-700 border border-amber-200">
            Thùng lẻ
          </span>
        ) : (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-slate-100 text-slate-700 border border-slate-200">
            Thùng chẵn
          </span>
        ),
    },
    {
      title: 'Mã hàng hóa',
      dataIndex: 'fullItemCode',
      key: 'fullItemCode',
      width: 200,
      render: (code: string, record) => (
        <div>
          <div className="font-mono text-xs font-medium text-slate-900">{code}</div>
          <div className="text-[11px] text-slate-500 truncate max-w-[220px]">
            {record.description || record.styleCode}
          </div>
        </div>
      ),
    },
    {
      title: 'Quy trình',
      dataIndex: 'processType',
      key: 'processType',
      width: 120,
      render: (type: number) =>
        type === ProcessType.GoKhongMay ? (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs bg-amber-50 text-amber-700 border border-amber-200/60 font-medium">
            Gò không may (.G)
          </span>
        ) : (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs bg-slate-100 text-slate-700 border border-slate-200 font-medium">
            Thành phẩm
          </span>
        ),
    },
    {
      title: 'Quy cách',
      key: 'standardPairPerCarton',
      align: 'center',
      width: 110,
      render: (_, record) => {
        const std = record.standardPairPerCarton || 12;
        return (
          <span className="font-mono text-xs text-slate-700 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
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
      width: 90,
      render: (count: number) => (
        <span className="font-mono font-medium text-slate-800">{count}</span>
      ),
    },
    {
      title: 'Đôi/Kiện',
      dataIndex: 'pairsPerCarton',
      key: 'pairsPerCarton',
      align: 'right',
      width: 90,
      render: (pairs: number, record) => (
        <span className={`font-mono text-xs ${record.isOddCarton ? 'text-amber-700 font-bold' : 'text-slate-600'}`}>
          {pairs}
        </span>
      ),
    },
    {
      title: 'Tổng số đôi',
      dataIndex: 'quantity',
      key: 'quantity',
      align: 'right',
      width: 110,
      render: (qty: number) => (
        <span className="font-mono font-semibold text-slate-900">
          {qty.toLocaleString()}
        </span>
      ),
    },
    {
      title: 'N.W (KGS)',
      dataIndex: 'netWeight',
      key: 'netWeight',
      align: 'right',
      width: 105,
      render: (nw: number) => (
        <span className="font-mono text-xs text-slate-600">{nw.toFixed(2)}</span>
      ),
    },
    {
      title: 'G.W (KGS)',
      dataIndex: 'grossWeight',
      key: 'grossWeight',
      align: 'right',
      width: 105,
      render: (gw: number) => (
        <span className="font-mono text-xs font-medium text-slate-800">{gw.toFixed(0)}</span>
      ),
    },
  ];

  return (
    <Modal
      title={
        <div className="flex items-center justify-between pr-6 border-b border-slate-100 pb-3">
          <div className="flex items-center space-x-2">
            <span className="w-2 h-2 rounded-full bg-indigo-600"></span>
            <span className="font-semibold text-slate-900 text-base">
              Xem trước Phân rã Đóng gói Packing List (PKL)
            </span>
          </div>
          <span className="text-xs font-mono bg-slate-100 text-slate-600 px-2.5 py-1 rounded border border-slate-200">
            {data.invoiceNo} {data.poSuffix}
          </span>
        </div>
      }
      open={visible}
      onCancel={onClose}
      width={1050}
      footer={[
        <Button key="close" onClick={onClose}>
          Đóng
        </Button>,
        <Button
          key="export"
          type="primary"
          icon={<DownloadOutlined />}
          loading={exporting}
          onClick={onExportExcel}
          className="bg-indigo-600 hover:bg-indigo-700"
        >
          Xuất File Excel Đa Sheet (.xlsx)
        </Button>,
      ]}
    >
      <div className="py-2 space-y-4">
        {/* KPI Summary Cards */}
        <div className="grid grid-cols-4 gap-3 bg-slate-50 p-3 rounded-lg border border-slate-200">
          <div className="bg-white p-3 rounded border border-slate-200/80 shadow-2xs">
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

          <div className="bg-white p-3 rounded border border-slate-200/80 shadow-2xs">
            <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
              Tổng số kiện (Cartons)
            </div>
            <div className="mt-1 flex items-baseline">
              <span className="text-xl font-bold font-mono text-slate-900">
                {data.totalCartons.toLocaleString()}
              </span>
              <span className="ml-1 text-xs text-slate-500">thùng</span>
            </div>
          </div>

          <div className="bg-white p-3 rounded border border-slate-200/80 shadow-2xs">
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

          <div className="bg-white p-3 rounded border border-slate-200/80 shadow-2xs">
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
        <div className="border border-slate-200 rounded-lg overflow-hidden bg-white">
          <Table
            dataSource={data.breakdownItems}
            columns={columns}
            rowKey={(_, index) => `${index}`}
            pagination={false}
            size="small"
            scroll={{ y: 380 }}
            summary={() => (
              <Table.Summary fixed>
                <Table.Summary.Row className="bg-slate-50 font-semibold text-slate-900">
                  <Table.Summary.Cell index={0} colSpan={5}>
                    <div className="font-semibold text-slate-800 pl-2">TỔNG CỘNG PACKING LIST</div>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={5} align="right">
                    <span className="font-mono text-slate-900 font-bold">
                      {data.totalCartons.toLocaleString()}
                    </span>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={6} align="right">
                    -
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={7} align="right">
                    <span className="font-mono text-slate-900 font-bold">
                      {data.totalQuantity.toLocaleString()}
                    </span>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={8} align="right">
                    <span className="font-mono text-slate-800">
                      {data.totalNetWeight.toFixed(2)}
                    </span>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={9} align="right">
                    <span className="font-mono text-slate-800">
                      {data.totalGrossWeight.toFixed(0)}
                    </span>
                  </Table.Summary.Cell>
                </Table.Summary.Row>
              </Table.Summary>
            )}
          />
        </div>

        {/* Rule explanation tip */}
        <div className="bg-slate-50 p-2.5 rounded text-xs text-slate-500 border border-slate-200/60 flex items-start space-x-2">
          <InfoCircleOutlined className="text-slate-400 mt-0.5" />
          <span>
            Quy cách đóng gói: Tự động phân rã kiện chẵn/lẻ theo quy cách (đôi/thùng) của từng mã hàng.
            Dải số kiện lũy kế liên tục và công thức tính trọng lượng động được bảo toàn 100% khi xuất sang file Excel mẫu thực tế.
          </span>
        </div>
      </div>
    </Modal>
  );
};
