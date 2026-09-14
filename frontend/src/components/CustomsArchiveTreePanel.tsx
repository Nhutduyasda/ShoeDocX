import React, { useMemo } from 'react';
import { Tree, Badge } from 'antd';
import type { DataNode } from 'antd/es/tree';
import {
  FolderOpenOutlined,
  ClockCircleOutlined,
  CheckCircleFilled,
  WarningFilled,
  CloseCircleFilled,
  BankOutlined,
  CalendarOutlined,
  FileTextOutlined,
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
              title: (
                <div className="flex items-center justify-between py-0.5 pr-1 text-xs text-emerald-800">
                  <span>Luồng 1 (Xanh) - Đã thông quan</span>
                  <span className="font-mono text-emerald-700 font-semibold ml-2">{greenCount}</span>
                </div>
              ),
            },
            {
              key: yellowKey,
              icon: <WarningFilled className="text-amber-500 text-[11px]" />,
              title: (
                <div className="flex items-center justify-between py-0.5 pr-1 text-xs text-amber-800">
                  <span>Luồng 2 (Vàng) - Kiểm tra hồ sơ</span>
                  <span className="font-mono text-amber-700 font-semibold ml-2">{yellowCount}</span>
                </div>
              ),
            },
            ...(redCount > 0
              ? [
                  {
                    key: redKey,
                    icon: <CloseCircleFilled className="text-rose-500 text-[11px]" />,
                    title: (
                      <div className="flex items-center justify-between py-0.5 pr-1 text-xs text-rose-800">
                        <span>Luồng 3 (Đỏ) - Kiểm hóa</span>
                        <span className="font-mono text-rose-700 font-semibold ml-2">{redCount}</span>
                      </div>
                    ),
                  },
                ]
              : []),
            {
              key: pendingKey,
              icon: <ClockCircleOutlined className="text-slate-400 text-[11px]" />,
              title: (
                <div className="flex items-center justify-between py-0.5 pr-1 text-xs text-slate-600">
                  <span>Chờ đối soát</span>
                  <span className="font-mono text-slate-500 ml-2">{pendingCount}</span>
                </div>
              ),
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
            icon: <FileTextOutlined className="text-amber-500 text-xs" />,
            title: (
              <div className="flex items-center justify-between py-0.5 pr-1 text-xs text-slate-700">
                <span className="truncate max-w-[145px]" title={contractTitle}>
                  {contractTitle}
                </span>
                <span className="text-[10px] font-mono px-1 rounded bg-slate-100 text-slate-600 ml-1">
                  {contractTotal}
                </span>
              </div>
            ),
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
          icon: <CalendarOutlined className="text-indigo-500 text-xs" />,
          title: (
            <div className="flex items-center justify-between py-0.5 pr-1 text-xs text-slate-700 font-medium">
              <span>Năm {year}</span>
              <span className="text-[10px] font-mono px-1 rounded bg-slate-100 text-slate-600 ml-1">
                {yearTotalCount}
              </span>
            </div>
          ),
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
        icon: <BankOutlined className="text-blue-600 text-xs" />,
        title: (
          <div className="flex items-center justify-between py-0.5 pr-1 text-xs font-semibold text-slate-800">
            <span className="truncate max-w-[140px]" title={bucket.partnerTitle}>
              {bucket.partnerTitle}
            </span>
            <span className="text-[10px] font-mono px-1.5 py-0.2 rounded-full bg-blue-50 text-blue-700 ml-1">
              {partnerTotalCount}
            </span>
          </div>
        ),
        children: yearNodes,
      });
    });

    // Root node
    const rootNode: DataNode = {
      key: 'all',
      icon: <FolderOpenOutlined className="text-amber-500 text-sm" />,
      title: (
        <div className="flex items-center justify-between py-1 pr-1 font-bold text-slate-800 text-xs">
          <span>Tất cả hồ sơ tờ khai</span>
          <Badge count={shipments.length} overflowCount={9999} className="site-badge-count-4 ml-1" />
        </div>
      ),
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
    <aside className="w-80 shrink-0 border-r border-slate-200 bg-slate-50/70 p-3 flex flex-col space-y-3 select-none">
      <div className="flex items-center justify-between">
        <div className="text-xs font-bold uppercase tracking-wider text-slate-700 flex items-center gap-1.5">
          <FolderOpenOutlined className="text-blue-600" />
          <span>Lưu trữ Hồ sơ Hải quan</span>
        </div>
        <span className="text-[11px] font-mono px-1.5 py-0.2 rounded bg-blue-100 text-blue-700 font-semibold">
          {totalCount} đơn
        </span>
      </div>

      <div className="text-[11px] text-slate-500 leading-tight">
        Phân cấp: <strong>Đối tác → Năm → Hợp đồng → Luồng xử lý VNACCS</strong>.
      </div>

      <div className="flex-1 overflow-y-auto bg-white p-2 rounded-lg border border-slate-200/80 shadow-xs min-h-[480px]">
        <Tree
          showIcon
          treeData={treeData}
          selectedKeys={[selectedKey || 'all']}
          defaultExpandedKeys={defaultExpandedKeys}
          onSelect={handleSelect}
          blockNode
          className="text-xs customs-archive-tree"
        />
      </div>

      <div className="p-2.5 rounded-lg bg-blue-50/80 border border-blue-200/60 text-[11px] text-blue-900 space-y-1">
        <div className="font-semibold flex items-center gap-1">
          <span>💡 Phân luồng tờ khai:</span>
        </div>
        <div className="flex items-center gap-1 text-[11px]">
          <CheckCircleFilled className="text-emerald-500 text-[10px]" />
          <span><strong>Luồng 1 (Xanh)</strong>: Miễn kiểm tra chứng từ & hàng hóa.</span>
        </div>
        <div className="flex items-center gap-1 text-[11px]">
          <WarningFilled className="text-amber-500 text-[10px]" />
          <span><strong>Luồng 2 (Vàng)</strong>: Kiểm tra hồ sơ chứng từ điện tử.</span>
        </div>
        <div className="flex items-center gap-1 text-[11px]">
          <CloseCircleFilled className="text-rose-500 text-[10px]" />
          <span><strong>Luồng 3 (Đỏ)</strong>: Kiểm tra thực tế hàng hóa xuất khẩu.</span>
        </div>
        <div className="flex items-center gap-1 text-[11px]">
          <ClockCircleOutlined className="text-slate-400 text-[10px]" />
          <span><strong>Chờ đối soát</strong>: Chưa có tờ khai hải quan VNACCS.</span>
        </div>
      </div>
    </aside>
  );
};

