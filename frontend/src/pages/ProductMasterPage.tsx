import React, { useState, useEffect, useCallback } from 'react';
import {
  Table,
  Button,
  Input,
  Popconfirm,
  message,
  Tooltip,
  Modal,
  Tag,
  Breadcrumb,
  TreeSelect,
  Dropdown,
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
  FolderOpenOutlined,
  ArrowRightOutlined,
  EllipsisOutlined,
} from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import type { ProductMaster, MasterDataFolder } from '../types';
import { productMasterApi } from '../api/productMasterApi';
import { masterDataFolderApi } from '../api/masterDataFolderApi';
import { ProductModal } from '../components/ProductModal';
import { ProductImportModal } from '../components/ProductImportModal';
import { FolderTreePanel } from '../components/FolderTreePanel';
import { useAuth } from '../contexts/AuthContext';
import { isAdminUser, isXnkUser } from '../types/auth';

export const ProductMasterPage: React.FC = () => {
  const { user } = useAuth();
  const canManageMasterData = isAdminUser(user) || isXnkUser(user);

  // Folder tree states
  const [treeData, setTreeData] = useState<MasterDataFolder[]>([]);
  const [selectedFolder, setSelectedFolder] = useState<MasterDataFolder | null>(null);

  // Products table states
  const [products, setProducts] = useState<ProductMaster[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [searchText, setSearchText] = useState<string>('');
  const [page, setPage] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(15);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);
  const [avgCmtBackend, setAvgCmtBackend] = useState<number | null>(null);
  const [avgDapBackend, setAvgDapBackend] = useState<number | null>(null);

  // Modal states
  const [modalVisible, setModalVisible] = useState<boolean>(false);
  const [selectedProduct, setSelectedProduct] = useState<ProductMaster | null>(null);
  const [importModalVisible, setImportModalVisible] = useState<boolean>(false);
  const [exporting, setExporting] = useState<boolean>(false);
  const [clearing, setClearing] = useState<boolean>(false);
  const [bulkUnitModalVisible, setBulkUnitModalVisible] = useState<boolean>(false);
  const [newBulkUnit, setNewBulkUnit] = useState<string>('đôi');
  const [updatingUnit, setUpdatingUnit] = useState<boolean>(false);

  // Bulk Move Modal
  const [bulkMoveModalVisible, setBulkMoveModalVisible] = useState<boolean>(false);
  const [targetMoveFolderId, setTargetMoveFolderId] = useState<number | null>(null);
  const [movingProducts, setMovingProducts] = useState<boolean>(false);

  // 1. Fetch Folder Tree
  const fetchTree = useCallback(async () => {
    try {
      const data = await masterDataFolderApi.getTree();
      setTreeData(data);

      // Nếu đang chọn folder, refresh lại thông tin folder đó
      setSelectedFolder(current => {
        if (!current) return null;
        const findFolder = (nodes: MasterDataFolder[], id: number): MasterDataFolder | null => {
          for (const n of nodes) {
            if (n.id === id) return n;
            if (n.children) {
              const f = findFolder(n.children, id);
              if (f) return f;
            }
          }
          return null;
        };
        return findFolder(data, current.id);
      });
    } catch {
      // ignore or show error
    }
  }, []);

  // 2. Fetch product list (filtered by search, page, folderId)
  const fetchProducts = useCallback(async () => {
    try {
      setLoading(true);
      const res = await productMasterApi.getPaged(
        searchText,
        page,
        pageSize,
        selectedFolder ? selectedFolder.id : null
      );
      setProducts(res.items);
      setTotalCount(res.totalCount);
      setAvgCmtBackend(res.avgUnitPriceCMT ?? null);
      setAvgDapBackend(res.avgUnitPriceDAP ?? null);
    } catch {
      message.error('Không thể tải danh sách sản phẩm');
    } finally {
      setLoading(false);
    }
  }, [searchText, page, pageSize, selectedFolder]);

  useEffect(() => {
    void fetchTree();
  }, [fetchTree]);

  useEffect(() => {
    void fetchProducts();
  }, [fetchProducts]);

  // Tính tổng tất cả sản phẩm từ cây thư mục
  const totalAllProducts = treeData.reduce((acc, f) => acc + f.totalProductCount, 0);

  // Handle Delete Single Product
  const handleDelete = async (id: number, styleCode: string) => {
    try {
      await productMasterApi.deleteProduct(id);
      message.success(`Đã xóa thành công mã hình thể "${styleCode}"`);
      fetchProducts();
      fetchTree();
    } catch {
      message.error('Không thể xóa mã hàng này.');
    }
  };

  // Handle Delete Products in Selected Folder
  const handleDeleteFolderProducts = async () => {
    if (!selectedFolder) {
      message.warning('Vui lòng chọn một thư mục cụ thể từ danh sách bên trái để xóa sản phẩm.');
      return;
    }

    try {
      setClearing(true);
      const res = await productMasterApi.deleteAll(selectedFolder.id);
      message.success(res.message || `Đã xóa tất cả sản phẩm trong thư mục "${selectedFolder.name}" thành công!`);
      setPage(1);
      fetchProducts();
      fetchTree();
    } catch {
      message.error('Không thể xóa sản phẩm trong thư mục này.');
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
      message.error(err.response?.data?.message || 'Không thể cập nhật ĐVT.');
    } finally {
      setUpdatingUnit(false);
    }
  };

  // Handle Bulk Move Products
  const handleConfirmBulkMove = async () => {
    if (selectedRowKeys.length === 0) return;
    try {
      setMovingProducts(true);
      const pIds = selectedRowKeys.map((k) => Number(k));
      await productMasterApi.bulkMove(pIds, targetMoveFolderId);
      message.success(`Đã chuyển ${pIds.length} sản phẩm thành công!`);
      setBulkMoveModalVisible(false);
      setSelectedRowKeys([]);
      fetchProducts();
      fetchTree();
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể chuyển sản phẩm');
    } finally {
      setMovingProducts(false);
    }
  };

  // Format tree data for TreeSelect
  const formatTreeSelect = (nodes: MasterDataFolder[]): any[] => {
    return nodes.map((node) => ({
      title: `${node.name} (${node.defaultPairsPerCarton} đôi/thùng)`,
      value: node.id,
      key: node.id,
      children: node.children ? formatTreeSelect(node.children) : [],
    }));
  };

  // Handle Export Excel
  const handleExportExcel = async () => {
    try {
      setExporting(true);
      await productMasterApi.exportExcel();
      message.success('Đã xuất file Excel Master Data thành công!');
    } catch {
      message.error('Không thể xuất file Excel.');
    } finally {
      setExporting(false);
    }
  };

  // Quick statistics calculation (Chỉ tính trung bình các mã có đơn giá > 0)
  const cmtValidItems = products.filter((p) => Number(p.unitPriceCMT) > 0);
  const clientAvgCmt = cmtValidItems.length > 0
    ? cmtValidItems.reduce((acc, p) => acc + Number(p.unitPriceCMT), 0) / cmtValidItems.length
    : 0;
  const finalAvgCmt = avgCmtBackend != null && avgCmtBackend > 0 ? avgCmtBackend : clientAvgCmt;
  const avgCmt = finalAvgCmt.toFixed(4);

  const dapValidItems = products.filter((p) => Number(p.unitPriceDAP) > 0);
  const clientAvgDap = dapValidItems.length > 0
    ? dapValidItems.reduce((acc, p) => acc + Number(p.unitPriceDAP), 0) / dapValidItems.length
    : 0;
  const finalAvgDap = avgDapBackend != null && avgDapBackend > 0 ? avgDapBackend : clientAvgDap;
  const avgDap = finalAvgDap.toFixed(4);

  const columns: ColumnsType<ProductMaster> = [
    {
      title: 'STT',
      key: 'index',
      width: 50,
      align: 'center',
      fixed: 'left',
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
      width: 140,
      fixed: 'left',
      render: (code: string) => (
        <span className="font-mono font-semibold text-xs text-slate-900 bg-slate-50 px-2 py-0.5 rounded border border-slate-200 inline-block">
          {code}
        </span>
      ),
    },
    {
      title: 'Thư mục',
      dataIndex: 'folderName',
      key: 'folderName',
      width: 150,
      render: (folderName: string | null) =>
        folderName ? (
          <Tooltip title={folderName}>
            <Tag color="blue" className="text-xs px-2 py-0.5 border-0 max-w-[140px] truncate inline-block align-middle font-medium cursor-help m-0">
              {folderName}
            </Tag>
          </Tooltip>
        ) : (
          <span className="text-xs text-slate-400 italic">Mặc định</span>
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
          <span className="text-slate-700 text-xs font-normal truncate block">{desc}</span>
        </Tooltip>
      ),
    },
    {
      title: (
        <Tooltip title="Đơn giá CMT tiêu chuẩn và đơn giá riêng cho hàng Gò (.G)">
          <span>Đơn giá CMT</span>
        </Tooltip>
      ),
      dataIndex: 'unitPriceCMT',
      key: 'unitPriceCMT',
      width: 110,
      align: 'right',
      render: (price: number, record) => (
        <div className="flex flex-col items-end">
          <span className="font-mono text-xs text-slate-800 font-medium">
            ${price.toFixed(4)}
          </span>
          {record.unitPriceCMT_Go != null && record.unitPriceCMT_Go > 0 && (
            <Tooltip title="Đơn giá CMT riêng cho hàng Gò không may (.G)">
              <span className="font-mono text-[10px] text-purple-700 bg-purple-50 px-1 rounded border border-purple-200 mt-0.5 cursor-help">
                Gò: ${record.unitPriceCMT_Go.toFixed(4)}
              </span>
            </Tooltip>
          )}
        </div>
      ),
    },
    {
      title: (
        <Tooltip title="Đơn giá DAP tiêu chuẩn và đơn giá riêng cho hàng Gò (.G)">
          <span>Đơn giá DAP</span>
        </Tooltip>
      ),
      dataIndex: 'unitPriceDAP',
      key: 'unitPriceDAP',
      width: 110,
      align: 'right',
      render: (price: number, record) => (
        <div className="flex flex-col items-end">
          <span className="font-mono text-xs font-semibold text-slate-900">
            ${price.toFixed(4)}
          </span>
          {record.unitPriceDAP_Go != null && record.unitPriceDAP_Go > 0 && (
            <Tooltip title="Đơn giá DAP riêng cho hàng Gò không may (.G)">
              <span className="font-mono text-[10px] text-purple-700 bg-purple-50 px-1 rounded border border-purple-200 mt-0.5 cursor-help">
                Gò: ${record.unitPriceDAP_Go.toFixed(4)}
              </span>
            </Tooltip>
          )}
        </div>
      ),
    },
    {
      title: 'Mã HS',
      dataIndex: 'hsCode',
      key: 'hsCode',
      width: 90,
      align: 'center',
      render: (hs: string) => (
        <span className="font-mono text-xs text-slate-600 bg-slate-50 px-1 py-0.5 rounded border border-slate-200">
          {hs}
        </span>
      ),
    },
    {
      title: 'ĐVT',
      dataIndex: 'unit',
      key: 'unit',
      width: 80,
      align: 'center',
      render: (unit: string) => (
        <span className="text-xs text-slate-600">{unit}</span>
      ),
    },
    {
      title: 'Đôi/CTN',
      dataIndex: 'pairPerCarton',
      key: 'pairPerCarton',
      width: 90,
      align: 'right',
      render: (pair: number) => (
        <Tag
          color={pair === 24 ? 'green' : 'blue'}
          className="font-mono text-xs font-semibold px-1.5 py-0 m-0"
        >
          {pair} đôi
        </Tag>
      ),
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 95,
      align: 'center',
      fixed: 'right',
      render: (_, record) => (
        <div className="flex items-center justify-center gap-1">
          <Tooltip title="Chỉnh sửa mã sản phẩm">
            <Button
              type="text"
              size="small"
              className="w-7 h-7 flex items-center justify-center p-0 text-slate-500 hover:text-blue-600 hover:bg-blue-50 rounded"
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
                className="w-7 h-7 flex items-center justify-center p-0 text-rose-500 hover:text-rose-700 hover:bg-rose-50 rounded"
                icon={<DeleteOutlined className="text-xs" />}
              />
            </Popconfirm>
          </Tooltip>
        </div>
      ),
    },
  ];

  const displayColumns = canManageMasterData
    ? columns
    : columns.filter((col) => col.key !== 'actions');

  return (
    <div className="space-y-4">
      {/* 1. Page Header */}
      <div className="flex justify-between items-start flex-wrap gap-4 pb-4 border-b border-slate-200 min-w-0">
        <div className="min-w-0">
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight m-0">
            Danh mục Hàng hóa (Master Data)
          </h1>
          <p className="text-sm text-slate-500 mt-1 m-0">
            Cấu trúc phân cấp theo cây thư mục đối tác, hợp đồng, đơn giá CMT/DAP và quy cách đóng gói (12 & 24 đôi/thùng)
          </p>
        </div>

        <div className="flex-shrink-0 flex items-center flex-wrap gap-2">
          {!canManageMasterData && (
            <Tag color="default" className="text-xs">
              👁️ Chế độ chỉ xem (Tra cứu danh mục)
            </Tag>
          )}

          {canManageMasterData && selectedRowKeys.length > 0 && (
            <Button
              icon={<ArrowRightOutlined />}
              onClick={() => {
                setTargetMoveFolderId(null);
                setBulkMoveModalVisible(true);
              }}
              className="bg-purple-600 hover:bg-purple-700 text-white font-medium text-xs h-8 px-3"
            >
              Chuyển ({selectedRowKeys.length})
            </Button>
          )}

          <Button
            icon={<DownloadOutlined />}
            onClick={handleExportExcel}
            loading={exporting}
            className="text-xs h-8 px-3 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium"
          >
            Xuất Excel
          </Button>

          {canManageMasterData && (
            <Tooltip title={selectedFolder ? `Nhập Excel vào thư mục "${selectedFolder.name}"` : 'Nhập Excel vào danh mục'}>
              <Button
                icon={<UploadOutlined />}
                onClick={() => setImportModalVisible(true)}
                className="text-xs h-8 px-3 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium"
              >
                Nhập Excel
              </Button>
            </Tooltip>
          )}

          {/* Menu Thao tác khác: Gom Đổi ĐVT và Xóa tất cả */}
          {canManageMasterData && (
            <Dropdown
              menu={{
                items: [
                  {
                    key: 'bulk-unit',
                    icon: <SwapOutlined />,
                    label: 'Đổi ĐVT hàng loạt',
                    disabled: totalCount === 0,
                    onClick: () => {
                      setNewBulkUnit('PRS');
                      setBulkUnitModalVisible(true);
                    },
                  },
                  {
                    type: 'divider',
                  },
                  {
                    key: 'delete-folder-products',
                    danger: true,
                    icon: <DeleteOutlined />,
                    label: selectedFolder
                      ? `Xóa tất cả sản phẩm trong "${selectedFolder.name}"`
                      : 'Xóa tất cả sản phẩm trong thư mục (Chưa chọn thư mục)',
                    disabled: !selectedFolder || totalCount === 0,
                    onClick: () => {
                      if (!selectedFolder) {
                        message.warning('Vui lòng chọn một thư mục từ cây thư mục bên trái để thực hiện xóa sản phẩm.');
                        return;
                      }
                      Modal.confirm({
                        title: `Xóa tất cả sản phẩm trong thư mục "${selectedFolder.name}"?`,
                        content: (
                          <div className="space-y-2">
                            <p className="m-0">
                              Bạn có chắc chắn muốn xóa toàn bộ <strong>{totalCount}</strong> sản phẩm thuộc thư mục{' '}
                              <strong>"{selectedFolder.name}"</strong>?
                            </p>
                            <p className="text-rose-600 text-xs m-0">
                              Lưu ý: Thao tác này chỉ xóa các sản phẩm bên trong thư mục này (giữ nguyên cấu trúc thư mục) và không thể hoàn tác!
                            </p>
                          </div>
                        ),
                        okText: `Xóa sản phẩm trong "${selectedFolder.name}"`,
                        okType: 'danger',
                        cancelText: 'Hủy',
                        onOk: handleDeleteFolderProducts,
                      });
                    },
                  },
                ],
              }}
              trigger={['click']}
            >
              <Button
                icon={<EllipsisOutlined />}
                loading={clearing}
                className="text-xs h-8 px-2.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium"
              >
                Thao tác khác
              </Button>
            </Dropdown>
          )}

          {/* Primary Action Button: Chỉ để icon + text "Thêm mã hàng" (bỏ dấu + trong string) */}
          {canManageMasterData && (
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => {
                setSelectedProduct(null);
                setModalVisible(true);
              }}
              className="bg-blue-600 hover:bg-blue-700 text-white font-medium text-xs h-8 px-3.5 shadow-sm"
            >
              Thêm mã hàng
            </Button>
          )}
        </div>
      </div>

      {/* 2. Main 2-Column Split Workspace with Solid Dividing Border */}
      <div className="flex flex-col lg:flex-row items-stretch w-full min-w-0 bg-white border border-slate-200 rounded-lg shadow-xs overflow-hidden">
        {/* Left Column: Folder Tree Panel (Width 288px / w-72) with Right Border and Padding */}
        <div className="w-full lg:w-72 flex-shrink-0 border-b lg:border-b-0 lg:border-r border-slate-200 min-h-[600px] flex flex-col bg-white pr-0 lg:pr-4">
          <FolderTreePanel
            treeData={treeData}
            selectedFolderId={selectedFolder ? selectedFolder.id : null}
            onSelectFolder={(folder) => {
              setSelectedFolder(folder);
              setPage(1);
            }}
            onRefresh={() => {
              fetchTree();
              fetchProducts();
            }}
            totalAllProducts={totalAllProducts}
            canManage={canManageMasterData}
          />
        </div>

        {/* Right Column: Products Table and Toolbar (flex-1) */}
        <div className="flex-1 min-w-0 p-3 sm:p-4 space-y-4 bg-slate-50/40 pl-0 lg:pl-4">
          {/* Breadcrumb & Selected Folder Header */}
          <div className="bg-white border border-slate-200 rounded-lg p-3 flex flex-col md:flex-row md:items-center md:justify-between gap-2.5">
            <div className="flex flex-wrap items-center gap-2 min-w-0 flex-1">
              <span className="text-slate-400 text-xs shrink-0">Vị trí:</span>
              <Breadcrumb
                className="text-xs shrink-0"
                items={[
                  {
                    title: (
                      <span
                        className={`cursor-pointer ${!selectedFolder ? 'font-semibold text-blue-700' : 'text-slate-600'}`}
                        onClick={() => setSelectedFolder(null)}
                      >
                        Tất cả sản phẩm
                      </span>
                    ),
                  },
                  ...(selectedFolder
                    ? [
                        {
                          title: (
                            <Tooltip title={selectedFolder.name}>
                              <span className="font-semibold text-slate-900 inline-flex items-center gap-1 max-w-[200px] truncate align-middle">
                                <FolderOpenOutlined className="text-amber-500 shrink-0" />
                                <span className="truncate">{selectedFolder.name}</span>
                              </span>
                            </Tooltip>
                          ),
                        },
                      ]
                    : []),
                ]}
              />

              {selectedFolder && (
                <div className="flex flex-wrap items-center gap-2 ml-1">
                  <Tag color={selectedFolder.defaultPairsPerCarton === 24 ? 'green' : 'blue'} className="text-xs py-0.5 px-2 m-0 shrink-0 font-medium">
                    Quy cách: {selectedFolder.defaultPairsPerCarton} đôi/thùng
                  </Tag>
                  {selectedFolder.customerName && (
                    <Tooltip title={`Đối tác / Khách hàng: ${selectedFolder.customerName}`} placement="top">
                      <Tag color="purple" className="text-xs py-0.5 px-2 m-0 max-w-[260px] truncate inline-block align-middle cursor-help font-medium">
                        {selectedFolder.customerName}
                      </Tag>
                    </Tooltip>
                  )}
                  {selectedFolder.contractNo && (
                    <Tooltip title={`Số hợp đồng: ${selectedFolder.contractNo}`} placement="top">
                      <Tag color="cyan" className="text-xs py-0.5 px-2 m-0 max-w-[260px] truncate inline-block align-middle cursor-help font-medium">
                        HĐ: {selectedFolder.contractNo}
                      </Tag>
                    </Tooltip>
                  )}
                </div>
              )}
            </div>

            <div className="text-xs text-slate-500 font-mono shrink-0 self-end md:self-center">
              Hiển thị: <strong className="text-slate-800">{totalCount}</strong> mã
            </div>
          </div>

          {/* Calm Metric Bar */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div className="bg-white border border-slate-200 rounded-lg p-3">
              <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
                Mã trong phạm vi lọc
              </div>
              <div className="mt-0.5 flex items-baseline space-x-1.5">
                <span className="text-xl font-semibold text-slate-900 font-mono">{totalCount}</span>
                <span className="text-xs text-slate-400">mã hình thể</span>
              </div>
            </div>

            <div className="bg-white border border-slate-200 rounded-lg p-3">
              <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
                Đơn giá CMT trung bình
              </div>
              <div className="mt-0.5 flex items-baseline space-x-1.5">
                <span className="text-xl font-semibold text-slate-900 font-mono">${avgCmt}</span>
                <span className="text-xs text-slate-400">USD/đôi</span>
              </div>
            </div>

            <div className="bg-white border border-slate-200 rounded-lg p-3">
              <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wider">
                Đơn giá DAP trung bình
              </div>
              <div className="mt-0.5 flex items-baseline space-x-1.5">
                <span className="text-xl font-semibold text-slate-900 font-mono">${avgDap}</span>
                <span className="text-xs text-slate-400">USD/đôi</span>
              </div>
            </div>
          </div>

          {/* Table Container */}
          <div className="bg-white border border-slate-200 rounded-lg p-3 space-y-3">
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

              <div className="flex items-center space-x-2">
                <Button
                  icon={<ReloadOutlined className="text-xs" />}
                  onClick={() => {
                    fetchProducts();
                    fetchTree();
                  }}
                  loading={loading}
                  className="text-xs text-slate-700 border-slate-300 hover:bg-slate-50 h-8 px-2.5"
                >
                  Làm mới
                </Button>
              </div>
            </div>

            {/* Ant Design Products Table */}
            <div className="w-full overflow-x-auto">
              <Table
                columns={displayColumns}
                dataSource={products}
                rowKey="id"
                loading={loading}
                size="small"
                rowSelection={
                  canManageMasterData
                    ? {
                        selectedRowKeys,
                        onChange: (keys) => setSelectedRowKeys(keys),
                      }
                    : undefined
                }
                pagination={{
                  current: page,
                  pageSize: pageSize,
                  total: totalCount,
                  showSizeChanger: true,
                  pageSizeOptions: ['15', '30', '50', '100'],
                  showTotal: (total, range) => (
                    <span className="text-xs text-slate-500">
                      {range[0]}-{range[1]} / {total} mã
                    </span>
                  ),
                  onChange: (newPage, newPageSize) => {
                    setPage(newPage);
                    setPageSize(newPageSize);
                  },
                }}
                className="border border-slate-100 rounded-lg"
                scroll={{ x: 'max-content' }}
              />
            </div>
          </div>
        </div>
      </div>

      {/* 4. Modal Thêm/Sửa Sản Phẩm */}
      <ProductModal
        visible={modalVisible}
        product={selectedProduct}
        folders={treeData}
        defaultFolderId={selectedFolder ? selectedFolder.id : null}
        onCancel={() => setModalVisible(false)}
        onSuccess={() => {
          setModalVisible(false);
          fetchProducts();
          fetchTree();
        }}
      />

      {/* 5. Modal Nhập File Excel */}
      <ProductImportModal
        visible={importModalVisible}
        targetFolder={selectedFolder}
        onCancel={() => setImportModalVisible(false)}
        onSuccess={() => {
          fetchProducts();
          fetchTree();
        }}
      />

      {/* 6. Modal Đổi ĐVT Toàn Bộ */}
      <Modal
        title={<span className="text-sm font-semibold text-slate-900">Đổi Đơn Vị Tính (ĐVT) Đồng Loạt</span>}
        open={bulkUnitModalVisible}
        onCancel={() => setBulkUnitModalVisible(false)}
        onOk={handleBulkUpdateUnit}
        confirmLoading={updatingUnit}
        okText="Xác nhận đổi tất cả"
        cancelText="Hủy"
        width={420}
      >
        <div className="space-y-3 my-3">
          <p className="text-xs text-slate-600 m-0">
            Nhập Đơn vị tính mới để cập nhật cho toàn bộ <strong>{totalCount}</strong> sản phẩm trong Master Data:
          </p>
          <Input
            value={newBulkUnit}
            onChange={(e) => setNewBulkUnit(e.target.value)}
            placeholder="Ví dụ: PRS, đôi, PR, chiếc..."
            className="text-xs"
          />
        </div>
      </Modal>

      {/* 7. Modal Chuyển thư mục hàng loạt */}
      <Modal
        title={<span className="text-sm font-semibold text-slate-900">Chuyển Thư Mục Hàng Loạt</span>}
        open={bulkMoveModalVisible}
        onCancel={() => setBulkMoveModalVisible(false)}
        onOk={handleConfirmBulkMove}
        confirmLoading={movingProducts}
        okText="Chuyển ngay"
        cancelText="Hủy"
        width={440}
      >
        <div className="space-y-3 my-3">
          <p className="text-xs text-slate-600 m-0">
            Bạn đang chọn <strong>{selectedRowKeys.length}</strong> sản phẩm để chuyển sang thư mục mới:
          </p>
          <TreeSelect
            treeData={formatTreeSelect(treeData)}
            placeholder="Chọn thư mục đích (hoặc để trống về Mặc định)"
            allowClear
            treeDefaultExpandAll
            value={targetMoveFolderId}
            onChange={(val) => setTargetMoveFolderId(val ?? null)}
            className="w-full text-xs"
          />
        </div>
      </Modal>
    </div>
  );
};
