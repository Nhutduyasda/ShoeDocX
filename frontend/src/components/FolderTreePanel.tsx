import React, { useState } from 'react';
import {
  Tree,
  Button,
  Input,
  Dropdown,
  Tag,
  Tooltip,
  message,
  Empty,
  Modal,
} from 'antd';
import type { MenuProps } from 'antd';
import type { DataNode, TreeProps } from 'antd/es/tree';
import {
  FolderOutlined,
  FolderOpenOutlined,
  PlusOutlined,
  SettingOutlined,
  DeleteOutlined,
  MoreOutlined,
  AppstoreOutlined,
} from '@ant-design/icons';
import type { MasterDataFolder } from '../types';
import { masterDataFolderApi } from '../api/masterDataFolderApi';
import { FolderConfigModal } from './FolderConfigModal';

interface FolderTreePanelProps {
  treeData: MasterDataFolder[];
  selectedFolderId: number | null;
  onSelectFolder: (folder: MasterDataFolder | null) => void;
  onRefresh: () => void;
  totalAllProducts: number;
  canManage?: boolean;
}

export const FolderTreePanel: React.FC<FolderTreePanelProps> = ({
  treeData,
  selectedFolderId,
  onSelectFolder,
  onRefresh,
  totalAllProducts,
  canManage = true,
}) => {
  const [searchKeyword, setSearchKeyword] = useState('');
  const [configModalVisible, setConfigModalVisible] = useState(false);
  const [editingFolder, setEditingFolder] = useState<MasterDataFolder | null>(null);
  const [parentFolderIdForNew, setParentFolderIdForNew] = useState<number | null>(null);

  // Mở modal tạo thư mục gốc mới
  const handleOpenCreateRoot = () => {
    setEditingFolder(null);
    setParentFolderIdForNew(null);
    setConfigModalVisible(true);
  };

  // Mở modal tạo thư mục con
  const handleOpenCreateChild = (parent: MasterDataFolder, e?: React.MouseEvent) => {
    e?.stopPropagation();
    setEditingFolder(null);
    setParentFolderIdForNew(parent.id);
    setConfigModalVisible(true);
  };

  // Mở modal cấu hình/sửa thư mục
  const handleOpenEdit = (folder: MasterDataFolder, e?: React.MouseEvent) => {
    e?.stopPropagation();
    setEditingFolder(folder);
    setParentFolderIdForNew(null);
    setConfigModalVisible(true);
  };

  // Xóa thư mục
  const handleDelete = async (folder: MasterDataFolder, cascade: boolean) => {
    try {
      await masterDataFolderApi.deleteFolder(folder.id, cascade);
      message.success(`Đã xóa thư mục "${folder.name}"`);
      if (selectedFolderId === folder.id) {
        onSelectFolder(null);
      }
      onRefresh();
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể xóa thư mục');
    }
  };

  const confirmDelete = (folder: MasterDataFolder, cascade: boolean) => {
    Modal.confirm({
      title: cascade ? 'Xóa thư mục và toàn bộ sản phẩm?' : 'Xóa thư mục?',
      content: cascade
        ? `Bạn có chắc muốn xóa "${folder.name}" và toàn bộ sản phẩm bên trong không? Hành động này không thể hoàn tác.`
        : `Bạn có chắc muốn xóa "${folder.name}" không? Sản phẩm bên trong sẽ được giữ lại.`,
      okText: 'Xóa',
      okType: 'danger',
      cancelText: 'Hủy',
      onOk: () => handleDelete(folder, cascade),
    });
  };

  const getFolderMenu = (folder: MasterDataFolder): MenuProps => ({
    items: [
      { key: 'open', label: 'Mở', icon: <FolderOpenOutlined />, onClick: () => onSelectFolder(folder) },
      { key: 'add-sub', label: 'Tạo thư mục con', icon: <PlusOutlined />, onClick: () => handleOpenCreateChild(folder) },
      { key: 'edit', label: 'Đổi tên / Cấu hình', icon: <SettingOutlined />, onClick: () => handleOpenEdit(folder) },
      { type: 'divider' },
      { key: 'delete-detach', danger: true, label: 'Xóa thư mục', icon: <DeleteOutlined />, onClick: () => confirmDelete(folder, false) },
      { key: 'delete-cascade', danger: true, label: 'Xóa thư mục và sản phẩm', icon: <DeleteOutlined />, onClick: () => confirmDelete(folder, true) },
    ],
  });

  // Kéo thả di chuyển thư mục
  const onDrop: TreeProps['onDrop'] = async (info) => {
    const dropKey = Number(info.node.key);
    const dragKey = Number(info.dragNode.key);

    let targetParentId: number | null = null;

    if (!info.dropToGap) {
      // Thả vào bên trong folder đích
      targetParentId = dropKey;
    } else {
      // Thả vào giữa hoặc đầu/cuối của anh em -> tìm parent của dropKey
      const findParentId = (nodes: MasterDataFolder[], targetId: number): number | null | undefined => {
        for (const n of nodes) {
          if (n.children?.some((c) => c.id === targetId)) return n.id;
          if (n.children) {
            const res = findParentId(n.children, targetId);
            if (res !== undefined) return res;
          }
        }
        return undefined;
      };
      const pId = findParentId(treeData, dropKey);
      targetParentId = pId === undefined ? null : pId;
    }

    try {
      await masterDataFolderApi.move(dragKey, {
        targetParentId,
        displayOrder: 0,
      });
      message.success('Đã cập nhật vị trí thư mục');
      onRefresh();
    } catch (err: any) {
      message.error(err.response?.data?.message || 'Không thể di chuyển thư mục');
    }
  };

  // Render từng Node trong cây
  const convertToTreeNodes = (folders: MasterDataFolder[]): DataNode[] => {
    const keyword = searchKeyword.trim().toLocaleLowerCase('vi');
    const containsMatch = (folder: MasterDataFolder): boolean =>
      !keyword ||
      folder.name.toLocaleLowerCase('vi').includes(keyword) ||
      Boolean(folder.customerName?.toLocaleLowerCase('vi').includes(keyword)) ||
      Boolean(folder.children?.some(containsMatch));

    return folders
      .filter(containsMatch)
      .map((folder) => {
        const isSelected = selectedFolderId === folder.id;
        const count = folder.totalProductCount;
        const is24Pairs = folder.defaultPairsPerCarton === 24;

        return {
          key: folder.id,
          title: (
            <Dropdown menu={getFolderMenu(folder)} trigger={canManage ? ['contextMenu'] : []}>
            <div className={`flex items-center justify-between group gap-1.5 w-full min-w-0 ${isSelected ? 'font-semibold' : 'font-normal'}`}>
              <div className="flex items-center space-x-1.5 min-w-0 flex-1 overflow-hidden">
                <Tooltip
                  title={folder.customerName ? `${folder.name} (${folder.customerName})` : folder.name}
                  mouseEnterDelay={0.3}
                  placement="topLeft"
                >
                  <span className="truncate text-xs font-medium block">
                    {folder.name}
                  </span>
                </Tooltip>
                {is24Pairs && (
                  <Tag color="green" className="text-[10px] px-1 py-0 border-0 leading-tight shrink-0 m-0">
                    24
                  </Tag>
                )}
              </div>

              <div className="flex items-center space-x-1 shrink-0 ml-1">
                <span
                  className={`text-[11px] px-1.5 py-0.2 rounded-full font-mono shrink-0 ${
                    count > 0 ? 'bg-slate-200 text-slate-700 font-medium' : 'bg-slate-100 text-slate-400'
                  }`}
                >
                  {count}
                </span>

                {/* Dropdown menu thao tác trên thư mục */}
                {canManage && (
                  <Dropdown
                    menu={getFolderMenu(folder)}
                    trigger={['click']}
                  >
                    <Button
                      type="text"
                      size="small"
                      className="p-0.5 h-6 w-6 text-slate-400 hover:text-blue-600 hover:bg-slate-200/80 rounded transition-colors shrink-0 flex items-center justify-center"
                      icon={<MoreOutlined className="text-xs" />}
                      onClick={(e) => e.stopPropagation()}
                      title="Thao tác thư mục"
                    />
                  </Dropdown>
                )}
              </div>
            </div>
            </Dropdown>
          ),
          children: folder.children ? convertToTreeNodes(folder.children) : [],
        };
      });
  };

  return (
    <div className="flex flex-col h-full bg-white w-full select-none">
      {/* Header Panel */}
      <div className="p-3 border-b border-slate-200 flex items-center justify-between bg-slate-50">
        <div className="flex items-center space-x-2">
          <FolderOutlined className="text-blue-600 text-sm" />
          <span className="font-semibold text-xs text-slate-800 tracking-wide uppercase">
            Thư mục
          </span>
        </div>
        {canManage && (
          <Tooltip title="Tạo thư mục mới">
            <Button
              type="primary"
              size="small"
              icon={<PlusOutlined />}
              onClick={handleOpenCreateRoot}
              className="bg-[#2563EB] hover:bg-[#1D4ED8] text-white text-xs h-7 px-2 shadow-xs"
            >
              Tạo thư mục
            </Button>
          </Tooltip>
        )}
      </div>

      {/* Tìm kiếm nhanh thư mục */}
      <div className="px-3 pt-2 pb-1">
        <Input.Search
          placeholder="Lọc thư mục..."
          size="small"
          allowClear
          value={searchKeyword}
          onChange={(e) => setSearchKeyword(e.target.value)}
          className="text-xs"
        />
      </div>

      {/* Button "Tất cả sản phẩm" */}
      <div className="px-2 pt-1 pb-1">
        <div
          onClick={() => onSelectFolder(null)}
          className={`flex items-center justify-between px-2.5 py-1.5 rounded cursor-pointer transition-colors text-xs ${
            selectedFolderId === null
              ? 'bg-[#EFF6FF] text-[#1D4ED8] font-semibold border border-[#BFDBFE]'
              : 'hover:bg-[#F3F4F6] text-[#374151]'
          }`}
        >
          <div className="flex items-center space-x-2">
            <AppstoreOutlined />
            <span>Tất cả sản phẩm</span>
          </div>
          <span
            className={`text-[11px] px-2 py-0.2 rounded-full font-mono ${
              selectedFolderId === null ? 'bg-[#DBEAFE] text-[#1D4ED8]' : 'bg-[#E5E7EB] text-[#4B5563]'
            }`}
          >
            {totalAllProducts}
          </span>
        </div>
      </div>

      <div className="px-2 py-1 text-[11px] font-medium text-slate-400 uppercase tracking-wider">
        Cây phân cấp đối tác
      </div>

      {/* Tree hiển thị thư mục */}
      <div className="flex-1 overflow-y-auto overflow-x-hidden px-1 py-1 folder-tree-container">
        <style>{`
          .folder-tree-custom .ant-tree-treenode {
            width: 100% !important;
            max-width: 100% !important;
            min-width: 0 !important;
            display: flex !important;
            align-items: center !important;
            padding: 2px 2px !important;
            box-sizing: border-box !important;
          }
          .folder-tree-custom .ant-tree-node-content-wrapper {
            flex: 1 1 0% !important;
            min-width: 0 !important;
            width: 0 !important;
            overflow: hidden !important;
            display: flex !important;
            align-items: center !important;
            padding: 0 2px !important;
            box-sizing: border-box !important;
          }
          .folder-tree-custom .ant-tree-draggable-icon {
            flex-shrink: 0 !important;
            width: 12px !important;
            line-height: 24px !important;
            opacity: 0.45;
          }
          .folder-tree-custom .ant-tree-switcher {
            flex-shrink: 0 !important;
            width: 18px !important;
          }
          .folder-tree-custom .ant-tree-indent {
            flex-shrink: 0 !important;
          }
        `}</style>
        {treeData.length === 0 ? (
          <div className="text-center py-6 text-xs text-slate-400">
            <Empty
              image={Empty.PRESENTED_IMAGE_SIMPLE}
              description={<span className="text-xs">Chưa có thư mục. Hãy tạo thư mục để bắt đầu.</span>}
            />
          </div>
        ) : (
          <Tree
            className="folder-tree-custom enterprise-folder-tree"
            draggable={canManage}
            blockNode
            showIcon
            defaultExpandAll
            onDrop={canManage ? onDrop : undefined}
            selectedKeys={selectedFolderId !== null ? [selectedFolderId] : []}
            onSelect={(keys) => {
              if (keys.length === 0) {
                onSelectFolder(null);
              } else {
                const id = Number(keys[0]);
                const findFolder = (nodes: MasterDataFolder[]): MasterDataFolder | null => {
                  for (const n of nodes) {
                    if (n.id === id) return n;
                    if (n.children) {
                      const found = findFolder(n.children);
                      if (found) return found;
                    }
                  }
                  return null;
                };
                onSelectFolder(findFolder(treeData));
              }
            }}
            treeData={convertToTreeNodes(treeData)}
            icon={({ expanded }) =>
              expanded ? (
                <FolderOpenOutlined className="text-amber-500 text-sm" />
              ) : (
                <FolderOutlined className="text-amber-500 text-sm" />
              )
            }
          />
        )}
      </div>

      {/* Modal Cấu hình / Tạo mới thư mục */}
      <FolderConfigModal
        visible={configModalVisible}
        folder={editingFolder}
        parentFolderId={parentFolderIdForNew}
        treeData={treeData}
        onCancel={() => setConfigModalVisible(false)}
        onSuccess={() => {
          setConfigModalVisible(false);
          onRefresh();
        }}
      />
    </div>
  );
};
