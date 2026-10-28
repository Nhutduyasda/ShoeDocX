import React, { useEffect, useState } from 'react';
import { Table, Button, Space } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CameraOutlined,
  PlusOutlined,
  FolderOpenOutlined,
  DownloadOutlined,
  ArrowRightOutlined,
  AuditOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useAuth } from '../contexts/AuthContext';
import { getAllowedTabsForUser } from '../types/auth';
import { WorkspaceCards } from '../components/WorkspaceCards';
import { shipmentApi } from '../api/shipmentApi';
import { productMasterApi } from '../api/productMasterApi';
import type { SavedShipmentSummary, ProductMaster } from '../types';
import type { NavTabKey } from '../layouts/AppLayout';

interface OverviewPageProps {
  onNavigate: (tab: NavTabKey) => void;
  onOpenOrder: (id: number) => void;
}

export const OverviewPage: React.FC<OverviewPageProps> = ({
  onNavigate,
  onOpenOrder,
}) => {
  const { user } = useAuth();
  const allowedTabs = getAllowedTabsForUser(user);
  const [dataError, setDataError] = useState(false);
  const [shipments, setShipments] = useState<SavedShipmentSummary[]>([]);
  const [products, setProducts] = useState<ProductMaster[]>([]);
  const [loading, setLoading] = useState<boolean>(false);

  const loadData = async () => {
    try {
      setLoading(true);
      setDataError(false);
      const [shipmentList, productList] = await Promise.all([
        shipmentApi.getShipments().catch(() => { setDataError(true); return []; }),
        productMasterApi.getAll().catch(() => { setDataError(true); return []; }),
      ]);
      setShipments(shipmentList);
      setProducts(productList);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  // KPIs
  const totalShipments = shipments.length;
  const totalPairs = shipments.reduce((sum, s) => sum + (s.totalQuantity || 0), 0);
  const totalAmountDAP = shipments.reduce((sum, s) => sum + (s.totalAmountDAP || 0), 0);
  const totalProductCodes = products.length;

  const handleDownloadHistorical = async (id: number, invoiceNo: string) => {
    try {
      const blob = await shipmentApi.exportSavedShipmentExcel(id);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      const safeName = invoiceNo.trim().replace(/[/\\?%*:|"<>]/g, '-');
      a.download = `${safeName}_${dayjs().format('YYYY-MM-DD')}.xlsx`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch {
      // Ignored
    }
  };

  const recentColumns: ColumnsType<SavedShipmentSummary> = [
    {
      title: 'STT',
      key: 'stt',
      width: 50,
      align: 'center',
      fixed: 'left',
      render: (_, __, idx) => <span className="text-xs text-slate-500">{idx + 1}</span>,
    },
    {
      title: 'Số Hóa đơn (Invoice No)',
      dataIndex: 'invoiceNo',
      key: 'invoiceNo',
      width: 180,
      fixed: 'left',
      render: (no: string, record) => (
        <div>
          <span className="font-mono font-semibold text-slate-900 text-xs">{no}</span>
          <span className="text-[11px] text-slate-400 ml-1.5">{record.poSuffix}</span>
        </div>
      ),
    },
    {
      title: 'Ngày lập',
      dataIndex: 'invoiceDate',
      key: 'invoiceDate',
      width: 100,
      render: (d: string) => (
        <span className="text-xs font-mono text-slate-600">{dayjs(d).format('DD/MM/YYYY')}</span>
      ),
    },
    {
      title: 'Số dòng',
      dataIndex: 'itemCount',
      key: 'itemCount',
      width: 75,
      align: 'center',
      render: (cnt: number) => <span className="font-mono text-xs">{cnt}</span>,
    },
    {
      title: 'Tổng số đôi',
      dataIndex: 'totalQuantity',
      key: 'totalQuantity',
      align: 'right',
      width: 120,
      render: (q: number) => (
        <span className="font-mono text-xs font-semibold text-slate-900">
          {q.toLocaleString()} đôi
        </span>
      ),
    },
    {
      title: 'Tổng tiền DAP',
      dataIndex: 'totalAmountDAP',
      key: 'totalAmountDAP',
      align: 'right',
      width: 130,
      render: (amt: number) => (
        <span className="font-mono text-xs font-semibold text-blue-600">
          ${amt.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
        </span>
      ),
    },
    {
      title: 'Trạng thái',
      key: 'status',
      width: 130,
      align: 'center',
      render: () => (
        <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-[#F0FDF4] text-[#15803D] border border-[#BBF7D0]">
          Đã lưu hệ thống
        </span>
      ),
    },
    {
      title: 'Thao tác',
      key: 'action',
      align: 'right',
      width: 150,
      fixed: 'right',
      render: (_, record) => (
        <Space size={4}>
          <Button
            size="small"
            icon={<FolderOpenOutlined className="text-xs" />}
            onClick={() => onOpenOrder(record.id)}
            className="text-xs text-[#374151] hover:text-[#2563EB] border-[#D1D5DB] h-7 px-2"
          >
            Mở lại
          </Button>
          <Button
            size="small"
            icon={<DownloadOutlined className="text-xs" />}
            onClick={() => handleDownloadHistorical(record.id, record.invoiceNo)}
            className="text-xs text-[#374151] hover:text-[#2563EB] border-[#D1D5DB] h-7 px-2"
          >
            Tải Excel
          </Button>
        </Space>
      ),
    },
  ];

  return (
    <div className="enterprise-page">
      <WorkspaceCards onNavigate={onNavigate}/>
      {dataError && <div role="alert" className="signin-error">Không tải được một phần dữ liệu tổng quan. Các số liệu bên dưới có thể chưa đầy đủ. <button type="button" onClick={loadData}>Thử tải lại</button></div>}
      {/* Page Header */}
      <div className="enterprise-page-header">
        <div className="min-w-0 flex-1">
          <h1 className="enterprise-page-title">
            Tổng quan nghiệp vụ xuất khẩu
          </h1>
          <p className="enterprise-page-description">
            Hệ thống quản lý Commercial Invoice, Packing List và dữ liệu xuất hàng nhà máy Kingmaker III
          </p>
        </div>

        <div className="enterprise-actions">
          {allowedTabs.includes('settlement') && (<Button
            icon={<AuditOutlined className="text-xs" />}
            onClick={() => onNavigate('settlement')}
            className="text-xs h-9 px-3 border-[#D1D5DB] text-[#374151] hover:bg-[#F9FAFB] hover:text-[#2563EB]"
          >
            Quyết toán đối chiếu nội bộ
          </Button>)}
          {allowedTabs.includes('ocr') && (<Button
            icon={<CameraOutlined className="text-xs" />}
            onClick={() => onNavigate('ocr')}
            className="text-xs h-9 px-3 border-[#D1D5DB] text-[#374151] hover:bg-[#F9FAFB] hover:text-[#2563EB]"
          >
            Quét OCR phiếu kho
          </Button>)}
          {allowedTabs.includes('shipment') && (<Button
            type="primary"
            icon={<PlusOutlined className="text-xs" />}
            onClick={() => onNavigate('shipment')}
            className="bg-[#2563EB] hover:bg-[#1D4ED8] text-white text-xs h-9 px-4 shadow-none"
          >
            Lập hóa đơn mới
          </Button>)}
        </div>
      </div>

      {/* KPI Cards (Clean, Minimal, Flat, No gradients) */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-white border border-[#E5E7EB] rounded-lg p-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
          <div className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
            Chứng từ đã lập
          </div>
          <div className="mt-2 flex items-baseline justify-between">
            <span className="text-2xl font-semibold font-mono text-[#111827]">
              {totalShipments}
            </span>
            <span className="text-xs text-[#9CA3AF]">chứng từ</span>
          </div>
          <div className="mt-2 text-[11px] text-[#6B7280]">
            Lưu trong cơ sở dữ liệu
          </div>
        </div>

        <div className="bg-white border border-[#E5E7EB] rounded-lg p-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
          <div className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
            Tổng sản lượng đã xuất
          </div>
          <div className="mt-2 flex items-baseline justify-between">
            <span className="text-2xl font-semibold font-mono text-[#111827]">
              {totalPairs.toLocaleString()}
            </span>
            <span className="text-xs text-[#9CA3AF]">đôi giày</span>
          </div>
          <div className="mt-2 text-[11px] text-[#6B7280]">
            Tính trên tất cả đơn hàng
          </div>
        </div>

        <div className="bg-white border border-[#E5E7EB] rounded-lg p-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
          <div className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
            Tổng giá trị DAP
          </div>
          <div className="mt-2 flex items-baseline justify-between">
            <span className="text-2xl font-semibold font-mono text-[#2563EB]">
              ${totalAmountDAP.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </span>
            <span className="text-xs text-[#9CA3AF]">USD</span>
          </div>
          <div className="mt-2 text-[11px] text-[#6B7280]">
            Trị giá hóa đơn thương mại
          </div>
        </div>

        <div className="bg-white border border-[#E5E7EB] rounded-lg p-4 shadow-[0_1px_2px_rgba(0,0,0,0.05)]">
          <div className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
            Danh mục mã hàng (Master)
          </div>
          <div className="mt-2 flex items-baseline justify-between">
            <span className="text-2xl font-semibold font-mono text-[#111827]">
              {totalProductCodes}
            </span>
            <span className="text-xs text-[#9CA3AF]">mã hình thể</span>
          </div>
          <div className="mt-2 text-[11px] text-[#6B7280]">
            Có sẵn giá CMT, DAP & HS Code
          </div>
        </div>
      </div>

      {/* Recent Shipments Table Panel */}
      <div className="enterprise-panel p-5">
        <div className="flex items-center justify-between pb-3 mb-4 border-b border-[#F3F4F6]">
          <div>
            <h2 className="text-sm font-semibold text-[#111827] m-0">
              Chứng từ xuất hàng gần đây
            </h2>
            <p className="text-xs text-[#6B7280] m-0 mt-0.5">
              Danh sách các hóa đơn đã được lập và sẵn sàng tải Excel
            </p>
          </div>
          {allowedTabs.includes('history') && (<Button
            type="link"
            size="small"
            onClick={() => onNavigate('history')}
            className="text-[#2563EB] hover:text-[#1D4ED8] text-xs p-0 flex items-center gap-1"
          >
            <span>Xem tất cả</span>
            <ArrowRightOutlined className="text-[10px]" />
          </Button>)}
        </div>

        <div className="w-full overflow-x-auto min-w-0">
          <Table
            dataSource={shipments.slice(0, 5)}
            columns={recentColumns}
            rowKey="id"
            loading={loading}
            pagination={false}
            size="middle"
            scroll={{ x: 'max-content' }}
          />
        </div>
      </div>

    </div>
  );
};
