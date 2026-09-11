import React, { useState, useEffect, useCallback } from 'react';
import {
  Table,
  Button,
  Input,
  Space,
  Popconfirm,
  message,
  Tooltip,
  Modal,
} from 'antd';
import {
  PlusOutlined,
  UploadOutlined,
  DownloadOutlined,
  SearchOutlined,
  ReloadOutlined,
  EditOutlined,
  DeleteOutlined,
  SwapOutlined,
} from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import type { ProductMaster } from '../types';
import { productMasterApi } from '../api/productMasterApi';
import { ProductModal } from '../components/ProductModal';
import { ProductImportModal } from '../components/ProductImportModal';

export const ProductMasterPage: React.FC = () => {
  const [products, setProducts] = useState<ProductMaster[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [searchText, setSearchText] = useState<string>('');
  const [page, setPage] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(10);
  const [totalCount, setTotalCount] = useState<number>(0);

  // Modal states
  const [modalVisible, setModalVisible] = useState<boolean>(false);
  const [selectedProduct, setSelectedProduct] = useState<ProductMaster | null>(null);
  const [importModalVisible, setImportModalVisible] = useState<boolean>(false);
  const [exporting, setExporting] = useState<boolean>(false);
  const [clearing, setClearing] = useState<boolean>(false);
  const [bulkUnitModalVisible, setBulkUnitModalVisible] = useState<boolean>(false);
  const [newBulkUnit, setNewBulkUnit] = useState<string>('đôi');
  const [updatingUnit, setUpdatingUnit] = useState<boolean>(false);

  // Fetch product list
  const fetchProducts = useCallback(async () => {
    try {
      setLoading(true);
      const res = await productMasterApi.getPaged(searchText, page, pageSize);
      setProducts(res.items);
      setTotalCount(res.totalCount);
    } catch (err) {
      console.error('Lỗi khi tải danh sách sản phẩm:', err);
    } finally {
      setLoading(false);
    }
  }, [searchText, page, pageSize]);

  useEffect(() => {
    fetchProducts();
  }, [fetchProducts]);

  // Handle Delete
  const handleDelete = async (id: number, styleCode: string) => {
    try {
      await productMasterApi.deleteProduct(id);
      message.success(`Đã xóa thành công mã hình thể "${styleCode}"`);
      fetchProducts();
    } catch (err) {
      console.error(err);
      message.error('Không thể xóa mã hàng này.');
    }
  };

  // Handle Delete All
  const handleDeleteAll = async () => {
    try {
      setClearing(true);
      const res = await productMasterApi.deleteAll();
      message.success(res.message || 'Đã xóa toàn bộ danh mục hàng hóa thành công!');
      setPage(1);
      fetchProducts();
    } catch (err) {
      console.error(err);
      message.error('Không thể xóa danh mục hàng hóa.');
    } finally {
      setClearing(false);
    }
  };

  // Handle Bulk Update Unit
  const handleBulkUpdateUnit = async () => {
    if (!newBulkUnit.trim()) {
      message.warning('Vui lòng nhập đơn vị tính mới');
      return;
    }
    try {
      setUpdatingUnit(true);
      const res = await productMasterApi.bulkUpdateUnit(newBulkUnit.trim());
      message.success(res.message || `Đã cập nhật ĐVT thành "${newBulkUnit.trim()}" cho tất cả sản phẩm!`);
      setBulkUnitModalVisible(false);
      fetchProducts();
    } catch (err: any) {
      console.error(err);
      message.error(err.response?.data?.message || 'Không thể cập nhật ĐVT.');
    } finally {
      setUpdatingUnit(false);
    }
  };

  // Handle Export Excel
  const handleExportExcel = async () => {
    try {
      setExporting(true);
      await productMasterApi.exportExcel();
      message.success('Đã xuất file Excel Master Data thành công!');
    } catch (err) {
      console.error(err);
      message.error('Không thể xuất file Excel.');
    } finally {
      setExporting(false);
    }
  };

  // Quick statistics calculation
  const avgCmt = products.length > 0
    ? (products.reduce((acc, p) => acc + p.unitPriceCMT, 0) / products.length).toFixed(4)
    : '0.0000';
  const avgDap = products.length > 0
    ? (products.reduce((acc, p) => acc + p.unitPriceDAP, 0) / products.length).toFixed(4)
    : '0.0000';

  const columns: ColumnsType<ProductMaster> = [
    {
      title: 'STT',
      key: 'index',
      width: 50,
      align: 'center',
      render: (_, __, index) => (
        <span className="font-mono text-xs text-slate-400">
          {(page - 1) * pageSize + index + 1}
        </span>
      ),
    },
    {
      title: 'Mã hình thể (Style Code)',
      dataIndex: 'styleCode',
      key: 'styleCode',
      width: 170,
      render: (code: string) => (
        <span className="font-mono font-semibold text-xs text-slate-900 bg-slate-50 px-2 py-0.5 rounded border border-slate-200">
          {code}
        </span>
      ),
    },
    {
      title: 'Mô tả hàng hóa hải quan',
      dataIndex: 'description',
      key: 'description',
      ellipsis: {
        showTitle: false,
      },
      render: (desc: string) => (
        <Tooltip placement="topLeft" title={desc}>
          <span className="text-slate-700 text-xs font-normal">{desc}</span>
        </Tooltip>
      ),
    },
    {
      title: 'Đơn giá CMT',
      dataIndex: 'unitPriceCMT',
      key: 'unitPriceCMT',
      width: 110,
      align: 'right',
      render: (price: number) => (
        <span className="font-mono text-xs text-slate-800">
          ${price.toFixed(4)}
        </span>
      ),
    },
    {
      title: 'Đơn giá DAP',
      dataIndex: 'unitPriceDAP',
      key: 'unitPriceDAP',
      width: 110,
      align: 'right',
      render: (price: number) => (
        <span className="font-mono text-xs font-medium text-slate-900">
          ${price.toFixed(4)}
        </span>
      ),
    },
    {
      title: (
        <Tooltip title="Đơn giá CMT riêng cho hàng Gò không may (.G)">
          <span>CMT Gò</span>
        </Tooltip>
      ),
      dataIndex: 'unitPriceCMT_Go',
      key: 'unitPriceCMT_Go',
      width: 105,
      align: 'right',
      render: (price: number | null | undefined) =>
        price && price > 0 ? (
          <span className="font-mono text-xs text-purple-700 font-medium">
            ${price.toFixed(4)}
          </span>
        ) : (
          <span className="text-xs text-slate-400">—</span>
        ),
    },
    {
      title: (
        <Tooltip title="Đơn giá DAP riêng cho hàng Gò không may (.G)">
          <span>DAP Gò</span>
        </Tooltip>
      ),
      dataIndex: 'unitPriceDAP_Go',
      key: 'unitPriceDAP_Go',
      width: 105,
      align: 'right',
      render: (price: number | null | undefined) =>
        price && price > 0 ? (
          <span className="font-mono text-xs text-purple-700 font-medium">
            ${price.toFixed(4)}
          </span>
        ) : (
          <span className="text-xs text-slate-400">—</span>
        ),
    },
    {
      title: 'Mã HS',
      dataIndex: 'hsCode',
      key: 'hsCode',
      width: 100,
      align: 'center',
      render: (hs: string) => (
        <span className="font-mono text-xs text-slate-600 bg-slate-50 px-1.5 py-0.5 rounded border border-slate-200">
          {hs}
        </span>
      ),
    },
    {
      title: 'ĐVT',
      dataIndex: 'unit',
      key: 'unit',
      width: 60,
      align: 'center',
      render: (unit: string) => (
        <span className="text-xs text-slate-600">{unit}</span>
      ),
    },
    {
      title: 'Đôi/CTN',
      dataIndex: 'pairPerCarton',
      key: 'pairPerCarton',
      width: 80,
      align: 'right',
      render: (pair: number) => (
        <span className="font-mono text-xs text-slate-800 font-medium">
          {pair}
        </span>
      ),
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 80,
      align: 'center',
      render: (_, record) => (
        <Space size={2}>
          <Tooltip title="Chỉnh sửa mã sản phẩm">
            <Button
              type="text"
              size="small"
              className="text-slate-500 hover:text-blue-600 hover:bg-slate-100"
              icon={<EditOutlined className="text-xs" />}
              onClick={() => {
                setSelectedProduct(record);
                setModalVisible(true);
              }}
            />
          </Tooltip>

          <Tooltip title="Xóa mã sản phẩm">
            <Popconfirm
              title={`Xóa mã "${record.styleCode}"?`}
              description="Hành động này sẽ xóa dữ liệu khỏi Master Data."
              onConfirm={() => handleDelete(record.id, record.styleCode)}
              okText="Xóa"
              cancelText="Hủy"
              okButtonProps={{ danger: true, size: 'small' }}
              cancelButtonProps={{ size: 'small' }}
            >
              <Button
                type="text"
                danger
                size="small"
                className="text-slate-400 hover:text-red-600 hover:bg-red-50"
                icon={<DeleteOutlined className="text-xs" />}
              />
            </Popconfirm>
          </Tooltip>
        </Space>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      {/* 1. Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-4 border-b border-slate-200">
        <div>
          <h1 className="text-xl font-semibold text-slate-900 tracking-tight m-0">
            Danh mục Hàng hóa (Master Data)
          </h1>
          <p className="text-xs text-slate-500 mt-1 m-0">
            Quản lý mã hình thể gốc, giá gia công CMT, DAP và quy cách đóng gói xuất khẩu
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Popconfirm
            title="Xóa tất cả danh mục hàng hóa?"
            description="Bạn có chắc chắn muốn xóa toàn bộ mã sản phẩm trong Master Data? Thao tác này không thể hoàn tác!"
            onConfirm={handleDeleteAll}
            okText="Xóa tất cả"
            cancelText="Hủy"
            okButtonProps={{ danger: true, size: 'small' }}
            cancelButtonProps={{ size: 'small' }}
          >
            <Button
              danger
              icon={<DeleteOutlined />}
              loading={clearing}
              disabled={totalCount === 0}
              className="text-xs h-9 px-3.5 font-normal"
            >
              Xóa tất cả
            </Button>
          </Popconfirm>

          <Button
            icon={<DownloadOutlined />}
            onClick={handleExportExcel}
            loading={exporting}
            className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-normal"
          >
            Xuất Excel
          </Button>

          <Button
            icon={<UploadOutlined />}
            onClick={() => setImportModalVisible(true)}
            className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-normal"
          >
            Nhập Excel
          </Button>

          <Button
            icon={<SwapOutlined />}
            onClick={() => {
              setNewBulkUnit('đôi');
              setBulkUnitModalVisible(true);
            }}
            disabled={totalCount === 0}
            className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-normal"
          >
            Đổi ĐVT tất cả
          </Button>

          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              setSelectedProduct(null);
              setModalVisible(true);
            }}
            className="bg-blue-600 hover:bg-blue-700 text-white font-medium text-xs h-9 px-4"
          >
            + Thêm mã hàng
          </Button>
        </div>
      </div>

      {/* 2. Calm Operational Metric Bar */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="bg-white border border-slate-200 rounded-lg p-3.5">
          <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
            Tổng số mã sản phẩm
          </div>
          <div className="mt-1 flex items-baseline space-x-1.5">
            <span className="text-2xl font-semibold text-slate-900 font-mono">
              {totalCount}
            </span>
            <span className="text-xs text-slate-400">mã hình thể</span>
          </div>
        </div>

        <div className="bg-white border border-slate-200 rounded-lg p-3.5">
          <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
            Đơn giá CMT trung bình
          </div>
          <div className="mt-1 flex items-baseline space-x-1.5">
            <span className="text-2xl font-semibold text-slate-900 font-mono">
              ${avgCmt}
            </span>
            <span className="text-xs text-slate-400">USD/đôi</span>
          </div>
        </div>

        <div className="bg-white border border-slate-200 rounded-lg p-3.5">
          <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
            Đơn giá DAP trung bình
          </div>
          <div className="mt-1 flex items-baseline space-x-1.5">
            <span className="text-2xl font-semibold text-slate-900 font-mono">
              ${avgDap}
            </span>
            <span className="text-xs text-slate-400">USD/đôi</span>
          </div>
        </div>
      </div>

      {/* 3. Table Container with Integrated Toolbar */}
      <div className="bg-white border border-slate-200 rounded-lg p-4 space-y-3">
        {/* Table Toolbar */}
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="w-full sm:w-80">
            <Input
              placeholder="Tìm theo mã hình thể, mô tả hoặc HS Code..."
              prefix={<SearchOutlined className="text-slate-400 text-xs mr-1" />}
              value={searchText}
              onChange={(e) => {
                setSearchText(e.target.value);
                setPage(1);
              }}
              allowClear
              size="middle"
              className="text-xs"
            />
          </div>

          <Button
            icon={<ReloadOutlined className="text-xs" />}
            onClick={() => fetchProducts()}
            loading={loading}
            className="text-xs text-slate-700 border-slate-300 hover:bg-slate-50 h-9 px-3"
          >
            Làm mới
          </Button>
        </div>

        {/* Enterprise Data Table */}
        <Table
          columns={columns}
          dataSource={products}
          rowKey="id"
          loading={loading}
          pagination={{
            current: page,
            pageSize: pageSize,
            total: totalCount,
            showSizeChanger: true,
            pageSizeOptions: ['10', '20', '50', '100'],
            showTotal: (total, range) => (
              <span className="text-xs text-slate-500">
                Hiển thị {range[0]}-{range[1]} / {total} mã hàng
              </span>
            ),
            onChange: (p, ps) => {
              setPage(p);
              setPageSize(ps);
            },
          }}
          size="middle"
        />
      </div>

      {/* Product Add / Edit Modal */}
      <ProductModal
        visible={modalVisible}
        product={selectedProduct}
        onCancel={() => setModalVisible(false)}
        onSuccess={() => {
          setModalVisible(false);
          fetchProducts();
        }}
      />

      {/* Product Excel Import Modal */}
      <ProductImportModal
        visible={importModalVisible}
        onCancel={() => setImportModalVisible(false)}
        onSuccess={() => {
          fetchProducts();
        }}
      />

      {/* Modal Đổi ĐVT hàng loạt */}
      <Modal
        title="Đổi Đơn Vị Tính (ĐVT) cho tất cả hàng hóa"
        open={bulkUnitModalVisible}
        onCancel={() => setBulkUnitModalVisible(false)}
        onOk={handleBulkUpdateUnit}
        confirmLoading={updatingUnit}
        okText="Cập nhật tất cả"
        cancelText="Hủy"
        okButtonProps={{ className: 'bg-blue-600' }}
      >
        <div className="space-y-4 py-2">
          <p className="text-xs text-slate-500 m-0">
            Thao tác này sẽ cập nhật đơn vị tính cho toàn bộ <strong>{totalCount}</strong> mã sản phẩm hiện có trong Master Data.
          </p>

          <div>
            <label className="block text-xs font-medium text-slate-700 mb-1.5">
              Chọn nhanh đơn vị tính phổ biến:
            </label>
            <div className="flex flex-wrap gap-2">
              {['đôi', 'PR', 'PCE', 'Cặp', 'THÙNG', 'Chiếc'].map((u) => (
                <Button
                  key={u}
                  size="small"
                  type={newBulkUnit === u ? 'primary' : 'default'}
                  onClick={() => setNewBulkUnit(u)}
                  className={newBulkUnit === u ? 'bg-blue-600 text-white' : 'border-slate-300 text-slate-700'}
                >
                  {u}
                </Button>
              ))}
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-700 mb-1.5">
              Hoặc nhập đơn vị tính tùy chỉnh:
            </label>
            <Input
              value={newBulkUnit}
              onChange={(e) => setNewBulkUnit(e.target.value)}
              placeholder="Nhập ĐVT (ví dụ: đôi, PR, PCE...)"
              maxLength={20}
              className="font-mono text-xs"
            />
          </div>
        </div>
      </Modal>
    </div>
  );
};
