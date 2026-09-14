import React, { useMemo } from 'react';
import { Tree } from 'antd';
import type { DataNode } from 'antd/es/tree';
import {
  FolderOpenOutlined,
  FolderOutlined,
  ClockCircleOutlined,
  CheckCircleFilled,
  WarningFilled,
  CloseCircleFilled,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import type { SavedShipmentSummary, MasterDataFolder } from '../types';
import { ShipmentStatus } from '../types';

export interface CustomsTreeFilter {
  partnerFolderId?: number;
  partnerName?: string;
  year?: number;
  contractNo?: string;
  channel?: number;
  pending?: boolean;
  customsStatus?: string;
  breadcrumb?: string;
}

interface CustomsArchiveTreePanelProps {
  shipments: SavedShipmentSummary[];
  folders?: MasterDataFolder[];
  selectedKey: string;
  onSelectNode: (key: string, filter: CustomsTreeFilter) => void;
}

const TreeRow: React.FC<{
  label: string;
  count: number;
  emphasis?: boolean;
}> = ({ label, count, emphasis = false }) => {
  return (
    <div className="flex h-9 min-w-0 w-full items-center gap-2 pr-2">
      <span
        className={`min-w-0 flex-1 truncate text-[13px] ${emphasis ? 'font-semibold text-slate-800' : 'font-normal text-slate-700'}`}
        title={label}
      >
        {label}
      </span>
      <span className={`ml-2 min-w-5 shrink-0 text-right font-mono text-xs tabular-nums ${count === 0 ? 'text-slate-300' : 'text-slate-400'}`}>
        {count}
      </span>
    </div>
  );
};

export const CustomsArchiveTreePanel: React.FC<CustomsArchiveTreePanelProps> = ({
  shipments,
  folders = [],
  selectedKey,
  onSelectNode,
}) => {
  // Bản đồ lưu bộ lọc tương ứng với từng node key
  const { treeData, totalCount, filterMap, defaultExpandedKeys } = useMemo(() => {
    const fMap = new Map<string, CustomsTreeFilter>();

    // 1. Ánh xạ các folder để tìm root folder (Đối tác)
    const folderDict = new Map<number, MasterDataFolder>();
    folders.forEach((f) => folderDict.set(f.id, f));

    const getRootFolder = (fId: number): MasterDataFolder | undefined => {
      let curr = folderDict.get(fId);
      const visited = new Set<number>();
      while (curr && curr.parentId && folderDict.has(curr.parentId) && !visited.has(curr.id)) {
        visited.add(curr.id);
        curr = folderDict.get(curr.parentId);
      }
      return curr;
    };

    const rootFolders = folders.filter((f) => !f.parentId);

    // 2. Nhóm dữ liệu: Partner -> Year -> Contract -> List<Shipment>
    interface PartnerBucket {
      partnerFolderId?: number;
      partnerTitle: string;
      years: Map<number, Map<string, SavedShipmentSummary[]>>;
    }

    const partnerBuckets = new Map<string, PartnerBucket>();

    // Khởi tạo các root folders từ MasterDataFolder trước
    rootFolders.forEach((rf) => {
      const pKey = `partner_${rf.id}`;
      const title = rf.customerName || rf.name;
      partnerBuckets.set(pKey, {
        partnerFolderId: rf.id,
        partnerTitle: title,
        years: new Map(),
      });
    });

    // Gom toàn bộ shipments vào các nhóm
    shipments.forEach((s) => {
      let pKey: string;
      let pTitle: string;
      let pFolderId: number | undefined;

      if (s.contractFolderId && folderDict.has(s.contractFolderId)) {
        const root = getRootFolder(s.contractFolderId);
        if (root) {
          pKey = `partner_${root.id}`;
          pTitle = root.customerName || root.name;
          pFolderId = root.id;
        } else {
          pKey = 'partner_other';
          pTitle = s.customerName || 'Đối tác khác';
        }
      } else {
        // Thử tìm root folder theo tên khách hàng
        const matched = rootFolders.find(
          (r) =>
            (r.customerName && r.customerName.toLowerCase() === s.customerName.toLowerCase()) ||
            (r.name && r.name.toLowerCase() === s.customerName.toLowerCase())
        );
        if (matched) {
          pKey = `partner_${matched.id}`;
          pTitle = matched.customerName || matched.name;
          pFolderId = matched.id;
        } else {
          pKey = `partner_cust_${s.customerName || 'other'}`;
          pTitle = s.customerName || 'Đối tác khác';
        }
      }

      if (!partnerBuckets.has(pKey)) {
        partnerBuckets.set(pKey, {
          partnerFolderId: pFolderId,
          partnerTitle: pTitle,
          years: new Map(),
        });
      }

      const bucket = partnerBuckets.get(pKey)!;
      const dateStr = s.clearanceDate || s.invoiceDate;
      const year = dayjs(dateStr).isValid() ? dayjs(dateStr).year() : dayjs().year();

      if (!bucket.years.has(year)) {
        bucket.years.set(year, new Map());
      }
      const contractsMap = bucket.years.get(year)!;
      const contractNo = s.contractNo ? s.contractNo.trim() : 'Không có HĐ';

      if (!contractsMap.has(contractNo)) {
        contractsMap.set(contractNo, []);
      }
      contractsMap.get(contractNo)!.push(s);
    });

    // 3. Xây dựng cây DataNode[]
    const partnerNodes: DataNode[] = [];
    const expandedKeys: string[] = ['all'];

    fMap.set('all', { breadcrumb: '' });

    partnerBuckets.forEach((bucket, pKey) => {
      const yearNodes: DataNode[] = [];
      let partnerTotalCount = 0;

      const sortedYears = Array.from(bucket.years.keys()).sort((a, b) => b - a);

      sortedYears.forEach((year) => {
        const contractsMap = bucket.years.get(year)!;
        const contractNodes: DataNode[] = [];
        let yearTotalCount = 0;

        const sortedContracts = Array.from(contractsMap.keys()).sort();

        sortedContracts.forEach((contractNo) => {
          const list = contractsMap.get(contractNo)!;
          const safeContractSlug = contractNo
            .replace(/[/\\ :]/g, '_')
            .replace(/[^a-zA-Z0-9_\u00C0-\u1EF9-]/g, '');
          const contractKey = `${pKey}_year_${year}_contract_${safeContractSlug}`;
          const contractTitle = contractNo.startsWith('HĐ') ? contractNo : `HĐ: ${contractNo}`;

          let greenCount = 0;
          let yellowCount = 0;
          let redCount = 0;
          let pendingCount = 0;

          list.forEach((s) => {
            if (s.status === ShipmentStatus.Cleared && s.customsChannel === 1) {
              greenCount++;
            } else if (s.status === ShipmentStatus.Cleared && s.customsChannel === 2) {
              yellowCount++;
            } else if (s.status === ShipmentStatus.Cleared && s.customsChannel === 3) {
              redCount++;
            } else {
              pendingCount++;
            }
          });

          const greenKey = `${contractKey}_channel_green`;
          const yellowKey = `${contractKey}_channel_yellow`;
          const redKey = `${contractKey}_channel_red`;
          const pendingKey = `${contractKey}_channel_pending`;

          // Đăng ký bộ lọc cho từng leaf node
          fMap.set(greenKey, {
            partnerFolderId: bucket.partnerFolderId,
            partnerName: bucket.partnerTitle,
            year,
            contractNo,
            channel: 1,
            customsStatus: 'Cleared',
            breadcrumb: `${bucket.partnerTitle} > Năm ${year} > ${contractTitle} > Luồng 1 (Xanh)`,
          });
          fMap.set(yellowKey, {
            partnerFolderId: bucket.partnerFolderId,
            partnerName: bucket.partnerTitle,
            year,
            contractNo,
            channel: 2,
            customsStatus: 'Cleared',
            breadcrumb: `${bucket.partnerTitle} > Năm ${year} > ${contractTitle} > Luồng 2 (Vàng)`,
          });
          fMap.set(redKey, {
            partnerFolderId: bucket.partnerFolderId,
            partnerName: bucket.partnerTitle,
            year,
            contractNo,
            channel: 3,
            customsStatus: 'Cleared',
            breadcrumb: `${bucket.partnerTitle} > Năm ${year} > ${contractTitle} > Luồng 3 (Đỏ)`,
          });
          fMap.set(pendingKey, {
            partnerFolderId: bucket.partnerFolderId,
            partnerName: bucket.partnerTitle,
            year,
            contractNo,
            pending: true,
            customsStatus: 'Pending',
            breadcrumb: `${bucket.partnerTitle} > Năm ${year} > ${contractTitle} > Chờ đối soát`,
          });

          // Cấp 4: Trạng thái Luồng Hải quan & Đối soát
          const channelNodes: DataNode[] = [
            {
              key: greenKey,
              icon: <CheckCircleFilled className="text-emerald-500 text-[11px]" />,
              title: <TreeRow label="Luồng 1 (Xanh) · Đã thông quan" count={greenCount} />,
            },
            {
              key: yellowKey,
              icon: <WarningFilled className="text-amber-500 text-[11px]" />,
              title: <TreeRow label="Luồng 2 (Vàng) · Kiểm tra hồ sơ" count={yellowCount} />,
            },
            ...(redCount > 0
              ? [
                  {
                    key: redKey,
                    icon: <CloseCircleFilled className="text-rose-500 text-[11px]" />,
                    title: <TreeRow label="Luồng 3 (Đỏ) · Kiểm hóa" count={redCount} />,
                  },
                ]
              : []),
            {
              key: pendingKey,
              icon: <ClockCircleOutlined className="text-slate-400 text-[11px]" />,
              title: <TreeRow label="Chờ đối soát" count={pendingCount} />,
            },
          ];

          fMap.set(contractKey, {
            partnerFolderId: bucket.partnerFolderId,
            partnerName: bucket.partnerTitle,
            year,
            contractNo,
            breadcrumb: `${bucket.partnerTitle} > Năm ${year} > ${contractTitle}`,
          });

          const contractTotal = list.length;
          yearTotalCount += contractTotal;

          // Cấp 3: Số Hợp đồng
          contractNodes.push({
            key: contractKey,
            icon: <FolderOutlined />,
            title: <TreeRow label={contractTitle} count={contractTotal} />,
            children: channelNodes,
          });
        });

        const yearKey = `${pKey}_year_${year}`;
        fMap.set(yearKey, {
          partnerFolderId: bucket.partnerFolderId,
          partnerName: bucket.partnerTitle,
          year,
          breadcrumb: `${bucket.partnerTitle} > Năm ${year}`,
        });

        partnerTotalCount += yearTotalCount;

        // Cấp 2: Năm xuất hàng
        yearNodes.push({
          key: yearKey,
          icon: <FolderOutlined />,
          title: <TreeRow label={`Năm ${year}`} count={yearTotalCount} />,
          children: contractNodes,
        });
      });

      fMap.set(pKey, {
        partnerFolderId: bucket.partnerFolderId,
        partnerName: bucket.partnerTitle,
        breadcrumb: bucket.partnerTitle,
      });

      expandedKeys.push(pKey);

      // Cấp 1: Đối tác / Khách hàng
      partnerNodes.push({
        key: pKey,
        icon: <FolderOutlined />,
        title: <TreeRow label={bucket.partnerTitle} count={partnerTotalCount} emphasis />,
        children: yearNodes,
      });
    });

    // Root node
    const rootNode: DataNode = {
      key: 'all',
      icon: <FolderOpenOutlined className="text-amber-500 text-sm" />,
      title: <TreeRow label="Tất cả hồ sơ tờ khai" count={shipments.length} emphasis />,
      children: partnerNodes,
    };

    return {
      treeData: [rootNode],
      totalCount: shipments.length,
      filterMap: fMap,
      defaultExpandedKeys: expandedKeys,
    };
  }, [shipments, folders]);

  const handleSelect = (selectedKeys: React.Key[]) => {
    if (selectedKeys.length === 0) return;
    const key = String(selectedKeys[0]);
    const filter = filterMap.get(key) || {};
    onSelectNode(key, filter);
  };

  return (
    <aside className="w-full lg:w-72 shrink-0 border-b lg:border-b-0 lg:border-r border-slate-200 bg-slate-50 flex flex-col select-none min-w-0">
      <div className="flex items-center justify-between border-b border-slate-200 px-3 py-3">
        <div className="text-xs font-bold uppercase tracking-wider text-slate-700 flex items-center gap-1.5">
          <FolderOpenOutlined className="text-blue-600" />
          <span>Lưu trữ Hồ sơ Hải quan</span>
        </div>
        <span className="text-[11px] font-mono px-1.5 py-0.2 rounded bg-blue-100 text-blue-700 font-semibold">
          {totalCount} đơn
        </span>
      </div>

      <div className="px-3 py-2 text-[11px] text-slate-500 leading-5 border-b border-slate-200 bg-white">
        Phân cấp: <strong>Đối tác → Năm → Hợp đồng → Luồng xử lý VNACCS</strong>.
      </div>

      <div className="flex-1 overflow-y-auto overflow-x-hidden bg-white p-2 min-h-[360px] lg:min-h-[520px]">
        <Tree
          showIcon
          treeData={treeData}
          selectedKeys={[selectedKey || 'all']}
          defaultExpandedKeys={defaultExpandedKeys}
          onSelect={handleSelect}
          blockNode
          className="text-xs customs-archive-tree enterprise-folder-tree"
        />
      </div>

    </aside>
  );
};

