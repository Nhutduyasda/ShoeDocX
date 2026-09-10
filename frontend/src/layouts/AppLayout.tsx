import { useState } from 'react';
import { Drawer, Breadcrumb, Tooltip, Dropdown } from 'antd';
import type { MenuProps } from 'antd';
import {
  FileTextOutlined,
  DatabaseOutlined,
  HistoryOutlined,
  CameraOutlined,
  DashboardOutlined,
  MenuOutlined,
  SafetyCertificateOutlined,
  UserOutlined,
  SettingOutlined,
  CheckCircleFilled,
  AuditOutlined,
} from '@ant-design/icons';

export type NavTabKey = 'overview' | 'shipment' | 'ocr' | 'history' | 'settlement' | 'products';

interface AppLayoutProps {
  currentTab: NavTabKey;
  onTabChange: (tab: NavTabKey) => void;
  children: React.ReactNode;
}

interface MenuItemDef {
  key: NavTabKey;
  label: string;
  icon: React.ReactNode;
  badge?: string;
}

interface MenuGroupDef {
  title: string;
  items: MenuItemDef[];
}

export const AppLayout: React.FC<AppLayoutProps> = ({
  currentTab,
  onTabChange,
  children,
}) => {
  const [mobileDrawerOpen, setMobileDrawerOpen] = useState<boolean>(false);

  const menuGroups: MenuGroupDef[] = [
    {
      title: 'TỔNG QUAN',
      items: [
        {
          key: 'overview',
          label: 'Tổng quan & Thống kê',
          icon: <DashboardOutlined className="text-base" />,
        },
      ],
    },
    {
      title: 'NGHIỆP VỤ',
      items: [
        {
          key: 'shipment',
          label: 'Lập Invoice / Packing List',
          icon: <FileTextOutlined className="text-base" />,
        },
        {
          key: 'ocr',
          label: 'OCR Phiếu kho',
          icon: <CameraOutlined className="text-base" />,
          badge: 'Vision',
        },
        {
          key: 'history',
          label: 'Lịch sử chứng từ',
          icon: <HistoryOutlined className="text-base" />,
        },
        {
          key: 'settlement',
          label: 'Báo cáo Quyết toán Hải quan',
          icon: <AuditOutlined className="text-base" />,
          badge: 'Mẫu 16',
        },
      ],
    },
    {
      title: 'DANH MỤC',
      items: [
        {
          key: 'products',
          label: 'Master Data hàng hóa',
          icon: <DatabaseOutlined className="text-base" />,
        },
      ],
    },
  ];

  const getBreadcrumbItems = () => {
    switch (currentTab) {
      case 'overview':
        return [
          { title: <span className="text-slate-400">Hệ thống</span> },
          { title: <span className="text-slate-700 font-medium">Tổng quan nghiệp vụ</span> },
        ];
      case 'shipment':
        return [
          { title: <span className="text-slate-400">Nghiệp vụ</span> },
          { title: <span className="text-slate-700 font-medium">Lập Invoice / Packing List</span> },
        ];
      case 'ocr':
        return [
          { title: <span className="text-slate-400">Nghiệp vụ</span> },
          { title: <span className="text-slate-700 font-medium">Quét OCR phiếu kho</span> },
        ];
      case 'history':
        return [
          { title: <span className="text-slate-400">Nghiệp vụ</span> },
          { title: <span className="text-slate-700 font-medium">Lịch sử xuất hàng</span> },
        ];
      case 'settlement':
        return [
          { title: <span className="text-slate-400">Nghiệp vụ</span> },
          { title: <span className="text-slate-700 font-medium">Báo cáo Quyết toán Mẫu 16 (BCQT-SP-GSQL)</span> },
        ];
      case 'products':
        return [
          { title: <span className="text-slate-400">Danh mục</span> },
          { title: <span className="text-slate-700 font-medium">Master Data hàng hóa</span> },
        ];
      default:
        return [{ title: 'ShoeDocX' }];
    }
  };

  const userMenuItems: MenuProps['items'] = [
    {
      key: 'org',
      label: (
        <div className="py-1">
          <div className="font-semibold text-xs text-slate-800">Kingmaker III (VN) Footwear</div>
          <div className="text-[11px] text-slate-400">KCN VSIP Quảng Ngãi</div>
        </div>
      ),
      disabled: true,
    },
    { type: 'divider' },
    {
      key: 'settings',
      icon: <SettingOutlined />,
      label: <span className="text-xs">Cấu hình hệ thống</span>,
    },
  ];

  const renderSidebarContent = () => (
    <div className="h-full flex flex-col justify-between bg-white select-none">
      <div>
        {/* Sidebar Brand Header */}
        <div className="h-14 px-5 flex items-center border-b border-slate-200">
          <div
            className="flex items-center space-x-2.5 cursor-pointer"
            onClick={() => {
              onTabChange('shipment');
              setMobileDrawerOpen(false);
            }}
          >
            <div className="w-7 h-7 rounded bg-blue-600 text-white flex items-center justify-center font-bold text-xs tracking-tight">
              SD
            </div>
            <div className="flex flex-col">
              <span className="font-semibold text-sm tracking-tight text-slate-900 leading-tight">
                ShoeDocX
              </span>
              <span className="text-[11px] text-slate-500 font-normal leading-tight">
                Export Documentation
              </span>
            </div>
          </div>
        </div>

        {/* Navigation Menu */}
        <div className="p-3 space-y-5">
          {menuGroups.map((group) => (
            <div key={group.title}>
              <div className="px-2.5 mb-1.5 text-[11px] font-semibold tracking-wider text-slate-400 uppercase">
                {group.title}
              </div>
              <div className="space-y-0.5">
                {group.items.map((item) => {
                  const isActive = currentTab === item.key;
                  return (
                    <button
                      key={item.key}
                      type="button"
                      onClick={() => {
                        onTabChange(item.key);
                        setMobileDrawerOpen(false);
                      }}
                      className={`w-full flex items-center justify-between px-3 h-10 rounded-md text-xs transition-colors cursor-pointer border-none text-left ${
                        isActive
                          ? 'bg-blue-50 text-blue-700 font-semibold'
                          : 'bg-transparent text-slate-700 hover:bg-slate-50 hover:text-slate-900 font-normal'
                      }`}
                    >
                      <div className="flex items-center space-x-2.5">
                        <span className={isActive ? 'text-blue-600' : 'text-slate-400'}>
                          {item.icon}
                        </span>
                        <span>{item.label}</span>
                      </div>
                      {item.badge && (
                        <span className="text-[10px] font-mono font-medium px-1.5 py-0.2 rounded bg-slate-100 text-slate-500 border border-slate-200">
                          {item.badge}
                        </span>
                      )}
                    </button>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Sidebar Footer Info */}
      <div className="p-3.5 border-t border-slate-200 bg-slate-50/60">
        <div className="flex items-center space-x-2 text-[11px] text-slate-600">
          <SafetyCertificateOutlined className="text-slate-400 text-xs shrink-0" />
          <div className="truncate">
            <div className="font-medium text-slate-700 truncate">Kingmaker III</div>
            <div className="text-[10px] text-slate-400 truncate">Nghiệp vụ xuất nhập khẩu</div>
          </div>
        </div>
      </div>
    </div>
  );

  return (
    <div className="min-h-screen bg-slate-50 flex font-sans antialiased text-slate-900">
      {/* Desktop Fixed Sidebar */}
      <aside className="hidden lg:block w-[240px] shrink-0 border-r border-slate-200 bg-white sticky top-0 h-screen z-20">
        {renderSidebarContent()}
      </aside>

      {/* Mobile Drawer Sidebar */}
      <Drawer
        placement="left"
        closable={false}
        onClose={() => setMobileDrawerOpen(false)}
        open={mobileDrawerOpen}
        styles={{ body: { padding: 0 } }}
        width={240}
      >
        {renderSidebarContent()}
      </Drawer>

      {/* Main Layout Area */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Top Header */}
        <header className="h-14 bg-white border-b border-slate-200 sticky top-0 z-10 px-4 sm:px-6 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <button
              type="button"
              onClick={() => setMobileDrawerOpen(true)}
              className="lg:hidden p-1.5 rounded-md border border-slate-200 text-slate-600 hover:bg-slate-50 cursor-pointer"
              aria-label="Open sidebar menu"
            >
              <MenuOutlined />
            </button>
            <Breadcrumb items={getBreadcrumbItems()} className="text-xs" />
          </div>

          <div className="flex items-center space-x-3">
            {/* System Connection Badge */}
            <Tooltip title="Kết nối Backend API (Cổng 5270) ổn định">
              <div className="hidden sm:flex items-center space-x-1.5 px-2 py-0.5 rounded border border-emerald-200 bg-emerald-50/80 text-[11px] font-medium text-emerald-800">
                <CheckCircleFilled className="text-emerald-500 text-[11px]" />
                <span>API Sẵn sàng</span>
              </div>
            </Tooltip>

            {/* User Account / Organization Menu */}
            <Dropdown menu={{ items: userMenuItems }} trigger={['click']}>
              <button
                type="button"
                className="flex items-center space-x-2 pl-2 border-l border-slate-200 cursor-pointer bg-transparent border-none text-left p-1 rounded hover:bg-slate-50"
              >
                <div className="w-7 h-7 rounded-full bg-slate-100 text-slate-600 flex items-center justify-center text-xs font-semibold border border-slate-200">
                  <UserOutlined />
                </div>
                <div className="hidden md:block text-left leading-none">
                  <span className="text-xs font-medium text-slate-800 block">XNK Officer</span>
                  <span className="text-[10px] text-slate-400 block mt-0.5">Xuất khẩu</span>
                </div>
              </button>
            </Dropdown>
          </div>
        </header>

        {/* Page Main Content Container */}
        <main className="flex-1 p-4 sm:p-6 lg:p-8 w-full max-w-full">
          {children}
        </main>

        {/* Footer */}
        <footer className="border-t border-slate-200 py-3 px-6 text-xs text-slate-500 bg-white">
          <div className="flex flex-col sm:flex-row items-center justify-between gap-1">
            <span>
              ShoeDocX • Hệ thống tự động hóa lập Commercial Invoice & Packing List nội bộ
            </span>
            <span className="text-slate-400 text-[11px]">
              Kingmaker III (Việt Nam) Footwear Co., Ltd
            </span>
          </div>
        </footer>
      </div>
    </div>
  );
};
