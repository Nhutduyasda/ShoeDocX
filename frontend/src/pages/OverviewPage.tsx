import React, { useEffect, useState } from 'react';
import { Table, Button, Space, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  FileTextOutlined,
  DatabaseOutlined,
  CameraOutlined,
  PlusOutlined,
  FolderOpenOutlined,
  DownloadOutlined,
  ArrowRightOutlined,
  AuditOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
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
  const [shipments, setShipments] = useState<SavedShipmentSummary[]>([]);
  const [products, setProducts] = useState<ProductMaster[]>([]);
  const [loading, setLoading] = useState<boolean>(false);

  const loadData = async () => {
    try {
      setLoading(true);
      const [shipmentList, productList] = await Promise.all([
        shipmentApi.getShipments().catch(() => []),
        productMasterApi.getAll().catch(() => []),
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
      title: 'Số Hóa đơn (Invoice No)',
      dataIndex: 'invoiceNo',
      key: 'invoiceNo',
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
      width: 110,
      align: 'center',
      render: () => (
        <Tag className="bg-emerald-50 text-emerald-700 border-emerald-200 text-[11px] m-0">
          Đã lưu hệ thống
        </Tag>
      ),
    },
    {
      title: 'Thao tác',
      key: 'action',
      align: 'center',
      width: 160,
      render: (_, record) => (
        <Space size={4}>
          <Button
            size="small"
            icon={<FolderOpenOutlined className="text-xs" />}
            onClick={() => onOpenOrder(record.id)}
            className="text-xs text-slate-700 hover:text-blue-600 border-slate-200"
          >
            Mở lại
          </Button>
          <Button
            size="small"
            icon={<DownloadOutlined className="text-xs" />}
            onClick={() => handleDownloadHistorical(record.id, record.invoiceNo)}
            className="text-xs text-slate-700 hover:text-blue-600 border-slate-200"
          >
            Tải Excel
          </Button>
        </Space>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-4 border-b border-slate-200">
        <div>
          <h1 className="text-xl font-semibold text-slate-900 tracking-tight m-0">
            Tổng quan nghiệp vụ xuất khẩu
          </h1>
          <p className="text-xs text-slate-500 mt-1 m-0">
            Hệ thống quản lý Commercial Invoice, Packing List và dữ liệu xuất hàng nhà máy Kingmaker III
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button
            icon={<AuditOutlined />}
            onClick={() => onNavigate('settlement')}
            className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50"
          >
            Quyết toán Mẫu 16
          </Button>
          <Button
            icon={<CameraOutlined />}
            onClick={() => onNavigate('ocr')}
            className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50"
          >
            Quét OCR phiếu kho
          </Button>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => onNavigate('shipment')}
            className="bg-blue-600 hover:bg-blue-700 text-xs h-9 px-4"
          >
            Lập hóa đơn mới
          </Button>
        </div>
      </div>

      {/* KPI Cards (Clean, Minimal, No gradients) */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-white border border-slate-200 rounded-lg p-4">
          <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
            Chứng từ đã lập
          </div>
          <div className="mt-2 flex items-baseline justify-between">
            <span className="text-2xl font-semibold font-mono text-slate-900">
              {totalShipments}
            </span>
            <span className="text-xs text-slate-400">chứng từ</span>
          </div>
          <div className="mt-2 text-[11px] text-slate-500">
            Lưu trong cơ sở dữ liệu
          </div>
        </div>

        <div className="bg-white border border-slate-200 rounded-lg p-4">
          <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
            Tổng sản lượng đã xuất
          </div>
          <div className="mt-2 flex items-baseline justify-between">
            <span className="text-2xl font-semibold font-mono text-slate-900">
              {totalPairs.toLocaleString()}
            </span>
            <span className="text-xs text-slate-400">đôi giày</span>
          </div>
          <div className="mt-2 text-[11px] text-slate-500">
            Tính trên tất cả đơn hàng
          </div>
        </div>

        <div className="bg-white border border-slate-200 rounded-lg p-4">
          <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
            Tổng giá trị DAP
          </div>
          <div className="mt-2 flex items-baseline justify-between">
            <span className="text-2xl font-semibold font-mono text-blue-600">
              ${totalAmountDAP.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </span>
            <span className="text-xs text-slate-400">USD</span>
          </div>
          <div className="mt-2 text-[11px] text-slate-500">
            Trị giá hóa đơn thương mại
          </div>
        </div>

        <div className="bg-white border border-slate-200 rounded-lg p-4">
          <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
            Danh mục mã hàng (Master)
          </div>
          <div className="mt-2 flex items-baseline justify-between">
            <span className="text-2xl font-semibold font-mono text-slate-900">
              {totalProductCodes}
            </span>
            <span className="text-xs text-slate-400">mã hình thể</span>
          </div>
          <div className="mt-2 text-[11px] text-slate-500">
            Có sẵn giá CMT, DAP & HS Code
          </div>
        </div>
      </div>

      {/* Recent Shipments Table Panel */}
      <div className="bg-white border border-slate-200 rounded-lg p-5">
        <div className="flex items-center justify-between pb-3 mb-4 border-b border-slate-100">
          <div>
            <h2 className="text-sm font-semibold text-slate-900 m-0">
              Chứng từ xuất hàng gần đây
            </h2>
            <p className="text-xs text-slate-500 m-0 mt-0.5">
              Danh sách các hóa đơn đã được lập và sẵn sàng tải Excel
            </p>
          </div>
          <Button
            type="link"
            size="small"
            onClick={() => onNavigate('history')}
            className="text-blue-600 hover:text-blue-700 text-xs p-0 flex items-center gap-1"
          >
            <span>Xem tất cả</span>
            <ArrowRightOutlined className="text-[10px]" />
          </Button>
        </div>

        <Table
          dataSource={shipments.slice(0, 5)}
          columns={recentColumns}
          rowKey="id"
          loading={loading}
          pagination={false}
          size="middle"
        />
      </div>

      {/* Quick Access Business Workflows */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div
          onClick={() => onNavigate('shipment')}
          className="bg-white border border-slate-200 hover:border-blue-400 rounded-lg p-4 cursor-pointer transition-colors"
        >
          <div className="flex items-center space-x-3">
            <div className="w-9 h-9 rounded bg-blue-50 text-blue-600 flex items-center justify-center text-base">
              <FileTextOutlined />
            </div>
            <div>
              <div className="text-xs font-semibold text-slate-900">Lập Hóa đơn INV & PKL</div>
              <div className="text-[11px] text-slate-500 mt-0.5">
                Nhập kho, đối soát và tự động sinh 3 sheet Excel
              </div>
            </div>
          </div>
        </div>

        <div
          onClick={() => onNavigate('ocr')}
          className="bg-white border border-slate-200 hover:border-blue-400 rounded-lg p-4 cursor-pointer transition-colors"
        >
          <div className="flex items-center space-x-3">
            <div className="w-9 h-9 rounded bg-blue-50 text-blue-600 flex items-center justify-center text-base">
              <CameraOutlined />
            </div>
            <div>
              <div className="text-xs font-semibold text-slate-900">Quét OCR Phiếu Kho</div>
              <div className="text-[11px] text-slate-500 mt-0.5">
                Chụp ảnh bảng kê, nhận diện số đôi & công đoạn Gò
              </div>
            </div>
          </div>
        </div>

        <div
          onClick={() => onNavigate('products')}
          className="bg-white border border-slate-200 hover:border-blue-400 rounded-lg p-4 cursor-pointer transition-colors"
        >
          <div className="flex items-center space-x-3">
            <div className="w-9 h-9 rounded bg-blue-50 text-blue-600 flex items-center justify-center text-base">
              <DatabaseOutlined />
            </div>
            <div>
              <div className="text-xs font-semibold text-slate-900">Master Data Hàng Hóa</div>
              <div className="text-[11px] text-slate-500 mt-0.5">
                Quản lý mã hàng, đơn giá gia công CMT, DAP và quy cách
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
