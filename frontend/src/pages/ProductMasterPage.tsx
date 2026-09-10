import React, { useState, useEffect, useCallback } from 'react';
import {
  Table,
  Button,
  Input,
  Space,
  Popconfirm,
  Card,
  Row,
  Col,
  message,
  Tooltip,
} from 'antd';
import {
  PlusOutlined,
  UploadOutlined,
  DownloadOutlined,
  SearchOutlined,
  ReloadOutlined,
  EditOutlined,
  DeleteOutlined,
  AppstoreOutlined,
  DollarOutlined,
  ShoppingOutlined,
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
    }
  };

  // Handle Export Excel
  const handleExportExcel = async () => {
    try {
      setExporting(true);
      await productMasterApi.exportExcel();
      message.success('Đã xuất file Excel hóa đơn & đóng gói thành công!');
    } catch (err) {
      console.error(err);
      message.error('Không thể xuất file Excel.');
    } finally {
      setExporting(false);
    }
  };

  // Table Columns - Clean & Monochromatic
  const columns: ColumnsType<ProductMaster> = [
    {
      title: 'STT',
      key: 'index',
      width: 55,
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
        <span className="font-mono font-semibold text-xs text-slate-800 bg-slate-100 px-2 py-0.5 rounded border border-slate-200/60 tracking-tight">
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
      width: 120,
      align: 'right',
      render: (price: number) => (
        <span className="font-mono text-xs text-slate-700">
          ${price.toFixed(4)}
        </span>
      ),
    },
    {
      title: 'Đơn giá DAP',
      dataIndex: 'unitPriceDAP',
      key: 'unitPriceDAP',
      width: 120,
      align: 'right',
      render: (price: number) => (
        <span className="font-mono text-xs text-slate-700">
          ${price.toFixed(4)}
        </span>
      ),
    },
    {
      title: 'Mã HS',
      dataIndex: 'hsCode',
      key: 'hsCode',
      width: 110,
      align: 'center',
      render: (hs: string) => (
        <span className="font-mono text-xs text-slate-600 bg-slate-100 px-2 py-0.5 rounded">
          {hs}
        </span>
      ),
    },
    {
      title: 'ĐVT',
      dataIndex: 'unit',
      key: 'unit',
      width: 65,
      align: 'center',
      render: (unit: string) => (
        <span className="text-xs text-slate-500">{unit}</span>
      ),
    },
    {
      title: 'Đôi/CTN',
      dataIndex: 'pairPerCarton',
      key: 'pairPerCarton',
      width: 80,
      align: 'center',
      render: (pair: number) => (
        <span className="font-mono text-xs text-slate-600">
          {pair}
        </span>
      ),
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 90,
      align: 'center',
      render: (_, record) => (
        <Space size={4}>
          <Tooltip title="Chỉnh sửa mã sản phẩm">
            <Button
              type="text"
              size="small"
              className="text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded"
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
              description="Hành động này không thể hoàn tác."
              onConfirm={() => handleDelete(record.id, record.styleCode)}
              okText="Xóa"
              cancelText="Hủy"
              okButtonProps={{ danger: true, size: 'small' }}
              cancelButtonProps={{ size: 'small' }}
            >
              <Button
                type="text"
                size="small"
                className="text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded"
                icon={<DeleteOutlined className="text-xs" />}
              />
            </Popconfirm>
          </Tooltip>
        </Space>
      ),
    },
  ];

  // Quick statistics calculation
  const avgCmt = products.length > 0
    ? (products.reduce((acc, p) => acc + p.unitPriceCMT, 0) / products.length).toFixed(2)
    : '0.00';
  const avgDap = products.length > 0
    ? (products.reduce((acc, p) => acc + p.unitPriceDAP, 0) / products.length).toFixed(2)
    : '0.00';

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 space-y-5">
      {/* Page Title & Actions Bar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-200/80">
        <div>
          <h1 className="text-lg font-semibold text-slate-900 tracking-tight m-0">
            Danh mục Hàng hóa (Product Master)
          </h1>
          <p className="text-xs text-slate-500 mt-1 m-0">
            Quản lý mã hình thể gốc, giá gia công CMT, giá DAP và mã HS xuất khẩu
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Button
            icon={<DownloadOutlined />}
            onClick={handleExportExcel}
            loading={exporting}
            className="bg-white border-slate-200 text-slate-700 hover:bg-slate-50 hover:border-slate-300 text-xs h-8"
          >
            Xuất Excel
          </Button>

          <Button
            icon={<UploadOutlined />}
            onClick={() => setImportModalVisible(true)}
            className="bg-white border-slate-200 text-slate-700 hover:bg-slate-50 hover:border-slate-300 text-xs h-8"
          >
            Import Excel
          </Button>

          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              setSelectedProduct(null);
              setModalVisible(true);
            }}
            className="bg-indigo-600 hover:bg-indigo-500 text-white border-none shadow-none text-xs font-medium h-8"
          >
            Thêm mã giày mới
          </Button>
        </div>
      </div>

      {/* Flat Minimalist Metric Cards */}
      <Row gutter={16}>
        <Col xs={24} sm={8}>
          <Card
            className="bg-white border border-slate-200/80 rounded-xl shadow-sm hover:border-slate-300 transition-colors"
            bordered={false}
            bodyStyle={{ padding: '16px 20px' }}
          >
            <div className="flex items-start justify-between">
              <div>
                <p className="text-[11px] uppercase tracking-wider font-medium text-slate-500 m-0">
                  Tổng số mã sản phẩm
                </p>
                <div className="flex items-baseline space-x-1.5 mt-2">
                  <span className="text-2xl font-semibold text-slate-900 tracking-tight font-mono">
                    {totalCount}
                  </span>
                  <span className="text-xs text-slate-400 font-normal">mã hình thể</span>
                </div>
              </div>
              <div className="w-8 h-8 rounded-lg bg-slate-100 text-slate-500 flex items-center justify-center text-sm">
                <AppstoreOutlined />
              </div>
            </div>
          </Card>
        </Col>

        <Col xs={24} sm={8}>
          <Card
            className="bg-white border border-slate-200/80 rounded-xl shadow-sm hover:border-slate-300 transition-colors"
            bordered={false}
            bodyStyle={{ padding: '16px 20px' }}
          >
            <div className="flex items-start justify-between">
              <div>
                <p className="text-[11px] uppercase tracking-wider font-medium text-slate-500 m-0">
                  Đơn giá CMT trung bình
                </p>
                <div className="flex items-baseline space-x-1.5 mt-2">
                  <span className="text-2xl font-semibold text-slate-900 tracking-tight font-mono">
                    ${avgCmt}
                  </span>
                  <span className="text-xs text-slate-400 font-normal">USD/đôi</span>
                </div>
              </div>
              <div className="w-8 h-8 rounded-lg bg-slate-100 text-slate-500 flex items-center justify-center text-sm">
                <DollarOutlined />
              </div>
            </div>
          </Card>
        </Col>

        <Col xs={24} sm={8}>
          <Card
            className="bg-white border border-slate-200/80 rounded-xl shadow-sm hover:border-slate-300 transition-colors"
            bordered={false}
            bodyStyle={{ padding: '16px 20px' }}
          >
            <div className="flex items-start justify-between">
              <div>
                <p className="text-[11px] uppercase tracking-wider font-medium text-slate-500 m-0">
                  Đơn giá DAP trung bình
                </p>
                <div className="flex items-baseline space-x-1.5 mt-2">
                  <span className="text-2xl font-semibold text-slate-900 tracking-tight font-mono">
                    ${avgDap}
                  </span>
                  <span className="text-xs text-slate-400 font-normal">USD/đôi</span>
                </div>
              </div>
              <div className="w-8 h-8 rounded-lg bg-slate-100 text-slate-500 flex items-center justify-center text-sm">
                <ShoppingOutlined />
              </div>
            </div>
          </Card>
        </Col>
      </Row>

      {/* Filter & Search Bar */}
      <div className="bg-white p-3 rounded-xl border border-slate-200/80 shadow-sm flex flex-wrap items-center justify-between gap-3">
        <div className="flex-1 min-w-[280px] max-w-md">
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
            className="rounded-lg text-xs bg-slate-50/50 border-slate-200 hover:border-slate-300 focus:bg-white"
          />
        </div>

        <Button
          icon={<ReloadOutlined className="text-xs" />}
          onClick={() => fetchProducts()}
          loading={loading}
          className="bg-white border-slate-200 text-slate-600 hover:text-slate-900 hover:bg-slate-50 text-xs h-8"
        >
          Làm mới
        </Button>
      </div>

      {/* Data Table */}
      <div className="bg-white rounded-xl border border-slate-200/80 shadow-sm overflow-hidden">
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
            showTotal: (total) => (
              <span className="text-xs text-slate-400 font-normal">
                {total} sản phẩm
              </span>
            ),
            onChange: (p, ps) => {
              setPage(p);
              setPageSize(ps);
            },
          }}
          size="middle"
          rowClassName="hover:bg-slate-50/60 transition-colors"
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
    </div>
  );
};
