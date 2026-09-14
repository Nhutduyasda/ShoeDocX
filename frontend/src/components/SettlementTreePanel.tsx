import React, { useMemo } from 'react';
import { Tree } from 'antd';
import type { DataNode } from 'antd/es/tree';
import {
  FolderOutlined,
  FolderOpenOutlined,
  AuditOutlined,
  CalendarOutlined,
  CheckCircleFilled,
  ClockCircleOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import type { MasterDataFolder, SettlementPeriodSummary } from '../types';

interface SettlementTreePanelProps {
  folders: MasterDataFolder[];
  savedPeriods: SettlementPeriodSummary[];
  selectedKey: string;
  onSelectNode: (
    folderId: number | null,
    year: number | null,
    status: 'Draft' | 'Finalized' | null,
    periodId?: number | null
  ) => void;
}

export const SettlementTreePanel: React.FC<SettlementTreePanelProps> = ({
  folders,
  savedPeriods,
  selectedKey,
  onSelectNode,
}) => {
  const currentYear = dayjs().year();

  const treeData = useMemo(() => {
    // Phân nhóm savedPeriods theo FolderId -> Year -> Status
    const folderPeriodsMap = new Map<number, SettlementPeriodSummary[]>();
    savedPeriods.forEach((p) => {
      const fId = p.contractFolderId || 0;
      if (!folderPeriodsMap.has(fId)) {
        folderPeriodsMap.set(fId, []);
      }
      folderPeriodsMap.get(fId)!.push(p);
    });

    const rootNodes: DataNode[] = [
      {
        key: 'all',
        title: (
          <div className="flex items-center justify-between py-1 pr-1 font-semibold text-slate-800">
            <span className="flex items-center gap-1.5">
              <FolderOpenOutlined className="text-amber-500" />
              Tất cả Hợp đồng & Khách hàng
            </span>
            <span className="text-xs px-1.5 py-0.2 rounded-full bg-slate-100 text-slate-600 font-mono">
              {folders.length}
            </span>
          </div>
        ),
        children: folders.map((folder) => {
          const periodsForFolder = folderPeriodsMap.get(folder.id) || [];

          // Tìm danh sách các năm (bao gồm năm hiện tại và năm trong periods)
          const yearsSet = new Set<number>([currentYear]);
          periodsForFolder.forEach((p) => {
            if (p.year) yearsSet.add(p.year);
          });
          const sortedYears = Array.from(yearsSet).sort((a, b) => b - a);

          return {
            key: `folder_${folder.id}`,
            title: (
              <div className="flex items-center justify-between py-0.5 pr-1 font-medium text-slate-800">
                <span className="flex items-center gap-1.5 truncate max-w-[150px]" title={folder.name}>
                  <FolderOutlined className="text-blue-500 shrink-0" />
                  <span className="truncate">{folder.name}</span>
                </span>
                {periodsForFolder.length > 0 && (
                  <span className="text-[10px] px-1 rounded bg-blue-50 text-blue-700 font-mono">
                    {periodsForFolder.length} kỳ
                  </span>
                )}
              </div>
            ),
            children: sortedYears.map((year) => {
              const periodsInYear = periodsForFolder.filter((p) => p.year === year);
              const drafts = periodsInYear.filter((p) => p.status === 'Draft');
              const finalized = periodsInYear.filter((p) => p.status === 'Finalized');

              return {
                key: `folder_${folder.id}_year_${year}`,
                title: (
                  <div className="flex items-center justify-between py-0.5 pr-1 text-xs text-slate-700 font-medium">
                    <span className="flex items-center gap-1.5">
                      <CalendarOutlined className="text-indigo-500" />
                      Năm tài chính {year}
                    </span>
                    <span className="text-[10px] font-mono text-slate-400">
                      {periodsInYear.length} kỳ
                    </span>
                  </div>
                ),
                children: [
                  {
                    key: `folder_${folder.id}_year_${year}_status_Draft`,
                    title: (
                      <div className="flex items-center justify-between py-0.5 pr-1 text-xs text-amber-700">
                        <span className="flex items-center gap-1.5">
                          <ClockCircleOutlined className="text-amber-500 text-[11px]" />
                          Bản nháp (Draft)
                        </span>
                        <span className="font-mono text-amber-600 font-semibold">{drafts.length}</span>
                      </div>
                    ),
                  },
                  {
                    key: `folder_${folder.id}_year_${year}_status_Finalized`,
                    title: (
                      <div className="flex items-center justify-between py-0.5 pr-1 text-xs text-emerald-700">
                        <span className="flex items-center gap-1.5">
                          <CheckCircleFilled className="text-emerald-500 text-[11px]" />
                          Bản chính thức (Finalized)
                        </span>
                        <span className="font-mono text-emerald-600 font-semibold">
                          {finalized.length}
                        </span>
                      </div>
                    ),
                  },
                ],
              };
            }),
          };
        }),
      },
    ];

    return rootNodes;
  }, [folders, savedPeriods, currentYear]);

  const handleSelect = (selectedKeys: React.Key[]) => {
    if (selectedKeys.length === 0) return;
    const key = String(selectedKeys[0]);

    if (key === 'all') {
      onSelectNode(null, null, null);
      return;
    }

    if (key.startsWith('folder_')) {
      const parts = key.split('_'); // ['folder', '5', 'year', '2026', 'status', 'Draft']
      const folderId = parseInt(parts[1], 10);

      if (parts.length === 2) {
        // Chỉ chọn folder
        onSelectNode(folderId, null, null);
      } else if (parts.length === 4 && parts[2] === 'year') {
        // Chọn folder + year
        const year = parseInt(parts[3], 10);
        onSelectNode(folderId, year, null);
      } else if (parts.length === 6 && parts[4] === 'status') {
        // Chọn folder + year + status
        const year = parseInt(parts[3], 10);
        const status = parts[5] as 'Draft' | 'Finalized';
        // Tìm xem có kỳ nào tương ứng không
        const matchedPeriod = savedPeriods.find(
          (p) => (p.contractFolderId || 0) === folderId && p.year === year && p.status === status
        );
        onSelectNode(folderId, year, status, matchedPeriod?.id);
      }
    }
  };

  return (
    <aside className="w-72 shrink-0 border-r border-slate-200 bg-slate-50/70 p-3 flex flex-col space-y-3 select-none">
      <div className="flex items-center justify-between">
        <div className="text-xs font-bold uppercase tracking-wider text-slate-600 flex items-center gap-1.5">
          <AuditOutlined className="text-emerald-600" />
          <span>Hồ Sơ Quyết Toán</span>
        </div>
        <span className="text-[11px] font-mono px-1.5 py-0.2 rounded bg-emerald-100 text-emerald-800 font-semibold">
          {savedPeriods.length} kỳ đã lưu
        </span>
      </div>

      <div className="text-[11px] text-slate-500 leading-tight">
        Cây phân cấp Khách hàng / Hợp đồng → Năm tài chính → Bản nháp / Bản chính thức Mẫu 16.
      </div>

      <div className="flex-1 overflow-y-auto bg-white p-2 rounded-lg border border-slate-200/80 shadow-xs">
        <Tree
          treeData={treeData}
          selectedKeys={[selectedKey || 'all']}
          defaultExpandedKeys={['all']}
          onSelect={handleSelect}
          blockNode
          className="text-xs settlement-archive-tree"
        />
      </div>

      <div className="p-2.5 rounded-lg bg-emerald-50/80 border border-emerald-200/60 text-[11px] text-emerald-900 space-y-1">
        <div className="font-semibold flex items-center gap-1">
          <span>⚖️ Quản lý Mẫu 16 Mở rộng:</span>
        </div>
        <div>• <strong>Bản nháp</strong>: Dữ liệu tính toán nội bộ, được phép hiệu chỉnh.</div>
        <div>• <strong>Bản chính thức</strong>: Hồ sơ đã chốt và khóa sổ để nộp cơ quan Hải quan.</div>
      </div>
    </aside>
  );
};
