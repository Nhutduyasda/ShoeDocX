import { useState, useEffect } from 'react';
import { Drawer, Breadcrumb, Tooltip, Dropdown, Button } from 'antd';
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
  QuestionCircleOutlined,
  RocketOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
} from '@ant-design/icons';
import { CheatsheetModal } from '../components/CheatsheetModal';
import { startOnboardingTour } from '../services/tourService';

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
  const [cheatsheetOpen, setCheatsheetOpen] = useState<boolean>(false);
  const [collapsed, setCollapsed] = useState<boolean>(() => {
    try {
      return localStorage.getItem('shoedocx_sidebar_collapsed') === 'true';
    } catch {
      return false;
    }
  });

  const toggleCollapsed = () => {
    setCollapsed((prev) => {
      const next = !prev;
      try {
        localStorage.setItem('shoedocx_sidebar_collapsed', String(next));
      } catch {
        // ignore
      }
      return next;
    });
  };

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'b') {
        e.preventDefault();
        toggleCollapsed();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

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
          label: 'Quyết toán Hải quan (Mẫu 16)',
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
          { title: <span className="text-slate-700 font-medium">Quyết toán Hải quan (Mẫu 16) & Thống kê Kim ngạch</span> },
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

  const renderSidebarContent = (isDrawer: boolean = false) => {
    const isMini = collapsed && !isDrawer;

    return (
      <div className="h-full flex flex-col justify-between bg-white select-none">
        <div className="flex flex-col min-h-0 flex-1">
          {/* Sidebar Brand Header */}
          <div
            className={`h-14 flex items-center border-b border-slate-200 shrink-0 transition-all duration-300 ${
              isMini ? 'justify-center px-2' : 'justify-between px-4'
            }`}
          >
            <div
              className="flex items-center space-x-3 cursor-pointer min-w-0"
              onClick={() => {
                onTabChange('shipment');
                setMobileDrawerOpen(false);
              }}
            >
              <div className="w-8 h-8 rounded-lg bg-blue-600 text-white flex items-center justify-center font-bold text-xs tracking-tight shrink-0 shadow-xs">
                SD
              </div>
              {!isMini && (
                <div className="flex flex-col min-w-0">
                  <span className="font-semibold text-sm tracking-tight text-slate-900 leading-tight">
                    ShoeDocX
                  </span>
                  <span className="text-[11px] text-slate-500 font-normal leading-tight truncate">
                    Export Documentation
                  </span>
                </div>
              )}
            </div>

            {!isDrawer && !isMini && (
              <Tooltip title="Thu gọn thanh menu (Ctrl + B)">
                <button
                  type="button"
                  onClick={toggleCollapsed}
                  className="hidden lg:flex items-center justify-center w-7 h-7 rounded text-slate-400 hover:text-slate-700 hover:bg-slate-100 cursor-pointer border-none bg-transparent transition-colors"
                  aria-label="Collapse sidebar"
                >
                  <MenuFoldOutlined className="text-xs" />
                </button>
              </Tooltip>
            )}
          </div>

          {/* Navigation Menu */}
          <div className={`space-y-4 flex-1 overflow-y-auto ${isMini ? 'p-2' : 'p-3'}`}>
            {menuGroups.map((group) => (
              <div key={group.title}>
                {!isMini ? (
                  <div className="px-2.5 mb-1.5 text-[11px] font-semibold tracking-wider text-slate-400 uppercase">
                    {group.title}
                  </div>
                ) : (
                  <div className="my-2 border-t border-slate-100" />
                )}
                <div className="space-y-1">
                  {group.items.map((item) => {
                    const isActive = currentTab === item.key;
                    const buttonContent = (
                      <button
                        key={item.key}
                        type="button"
                        onClick={() => {
                          onTabChange(item.key);
                          setMobileDrawerOpen(false);
                        }}
                        className={`w-full flex items-center rounded-lg text-xs transition-colors cursor-pointer border-none text-left ${
                          isMini
                            ? 'justify-center h-10 px-0'
                            : 'justify-between px-3 h-10'
                        } ${
                          isActive
                            ? 'bg-blue-50 text-blue-700 font-semibold'
                            : 'bg-transparent text-slate-700 hover:bg-slate-50 hover:text-slate-900 font-normal'
                        }`}
                      >
                        <div
                          className={`flex items-center min-w-0 ${
                            isMini ? 'justify-center w-full' : 'space-x-2.5'
                          }`}
                        >
                          <span
                            className={`shrink-0 flex items-center text-sm ${
                              isActive ? 'text-blue-600' : 'text-slate-400'
                            }`}
                          >
                            {item.icon}
                          </span>
                          {!isMini && <span className="truncate">{item.label}</span>}
                        </div>
                        {!isMini && item.badge && (
                          <span className="shrink-0 text-[10px] font-mono font-medium px-1.5 py-0.5 rounded bg-slate-100 text-slate-500 border border-slate-200 ml-1.5">
                            {item.badge}
                          </span>
                        )}
                      </button>
                    );

                    if (isMini) {
                      return (
                        <Tooltip
                          key={item.key}
                          placement="right"
                          title={
                            <div>
                              <div className="font-semibold">{item.label}</div>
                              {item.badge && (
                                <div className="text-[11px] text-slate-300 mt-0.5">
                                  Phân hệ: {item.badge}
                                </div>
                              )}
                            </div>
                          }
                        >
                          {buttonContent}
                        </Tooltip>
                      );
                    }

                    return buttonContent;
                  })}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Sidebar Footer Info */}
        <div
          className={`border-t border-slate-200 bg-slate-50/60 shrink-0 ${
            isMini ? 'p-2 flex flex-col items-center' : 'p-3.5'
          }`}
        >
          {isMini ? (
            <Tooltip
              placement="right"
              title="Mở rộng thanh menu (Ctrl + B)"
            >
              <button
                type="button"
                onClick={toggleCollapsed}
                className="w-8 h-8 flex items-center justify-center rounded-md text-slate-500 hover:text-blue-600 hover:bg-slate-200/60 cursor-pointer border-none bg-transparent transition-colors"
                aria-label="Expand sidebar"
              >
                <MenuUnfoldOutlined className="text-sm" />
              </button>
            </Tooltip>
          ) : (
            <div className="flex items-center space-x-2.5 text-[11px] text-slate-600">
              <SafetyCertificateOutlined className="text-slate-400 text-xs shrink-0" />
              <div className="min-w-0 truncate">
                <div className="font-medium text-slate-700 truncate">Kingmaker III</div>
                <div className="text-[10px] text-slate-400 truncate">Nghiệp vụ xuất nhập khẩu</div>
              </div>
            </div>
          )}
        </div>
      </div>
    );
  };

  return (
    <div className="flex h-screen w-screen overflow-hidden bg-slate-50 text-slate-800 font-sans antialiased">
      {/* Desktop Sidebar */}
      <aside
        className={`hidden lg:flex flex-col shrink-0 border-r border-slate-200 bg-white h-screen z-30 transition-all duration-300 ease-in-out ${
          collapsed ? 'w-[72px]' : 'w-64'
        }`}
      >
        {renderSidebarContent(false)}
      </aside>

      {/* Mobile Drawer Sidebar */}
      <Drawer
        placement="left"
        closable={false}
        onClose={() => setMobileDrawerOpen(false)}
        open={mobileDrawerOpen}
        styles={{ body: { padding: 0 } }}
        width={256}
      >
        {renderSidebarContent(true)}
      </Drawer>

      {/* Main Layout Area */}
      <div className="flex-1 flex flex-col min-w-0 h-screen overflow-hidden">
        {/* Top Header */}
        <header className="h-14 bg-white border-b border-slate-200 shrink-0 px-4 sm:px-6 flex items-center justify-between z-20">
          <div className="flex items-center space-x-3">
            <button
              type="button"
              onClick={() => setMobileDrawerOpen(true)}
              className="lg:hidden p-1.5 rounded-md border border-slate-200 text-slate-600 hover:bg-slate-50 cursor-pointer"
              aria-label="Open sidebar menu"
            >
              <MenuOutlined />
            </button>
            <Tooltip title={collapsed ? 'Mở rộng thanh menu (Ctrl + B)' : 'Thu gọn thanh menu (Ctrl + B)'}>
              <button
                type="button"
                onClick={toggleCollapsed}
                className="hidden lg:flex items-center justify-center w-8 h-8 rounded-md text-slate-500 hover:text-slate-900 hover:bg-slate-100 cursor-pointer border border-transparent hover:border-slate-200 transition-colors"
                aria-label="Toggle sidebar"
              >
                {collapsed ? <MenuUnfoldOutlined className="text-sm" /> : <MenuFoldOutlined className="text-sm" />}
              </button>
            </Tooltip>
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

            {/* Quick Onboarding / Help Menu */}
            <Dropdown
              menu={{
                items: [
                  {
                    key: 'interactive-tour',
                    icon: <RocketOutlined className="text-blue-600" />,
                    label: 'Chạy lại Tour hướng dẫn tương tác',
                    onClick: () => {
                      if (currentTab !== 'shipment') {
                        onTabChange('shipment');
                        setTimeout(() => startOnboardingTour(), 400);
                      } else {
                        startOnboardingTour();
                      }
                    },
                  },
                  {
                    key: 'cheatsheet',
                    icon: <FileTextOutlined className="text-emerald-600" />,
                    label: 'Xem Sơ đồ 4 bước xuất chứng từ',
                    onClick: () => setCheatsheetOpen(true),
                  },
                ],
              }}
              trigger={['click']}
              placement="bottomRight"
            >
              <Button
                size="small"
                icon={<QuestionCircleOutlined className="text-amber-500" />}
                className="flex items-center text-xs text-slate-700 hover:text-blue-600 border-slate-200 bg-slate-50/70 hover:bg-white h-7 px-2.5 font-medium cursor-pointer"
              >
                <span>Hướng dẫn nhanh</span>
              </Button>
            </Dropdown>

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

        {/* Page Main Content Container: independent vertical scroll, overflow-x-hidden */}
        <main className="flex-1 flex flex-col min-w-0 overflow-y-auto overflow-x-hidden">
          <div className="p-4 sm:p-6 max-w-[1600px] w-full mx-auto space-y-6 flex-1">
            {children}
          </div>

          {/* Footer */}
          <footer className="border-t border-slate-200 py-3 px-6 text-xs text-slate-500 bg-white shrink-0 mt-auto">
            <div className="flex flex-col sm:flex-row items-center justify-between gap-1 max-w-[1600px] mx-auto">
              <span>
                ShoeDocX • Hệ thống tự động hóa lập Commercial Invoice & Packing List nội bộ
              </span>
              <span className="text-slate-400 text-[11px]">
                Kingmaker III (Việt Nam) Footwear Co., Ltd
              </span>
            </div>
          </footer>
        </main>
      </div>

      <CheatsheetModal
        open={cheatsheetOpen}
        onClose={() => setCheatsheetOpen(false)}
        onStartTour={() => {
          if (currentTab !== 'shipment') {
            onTabChange('shipment');
            setTimeout(() => startOnboardingTour(), 400);
          } else {
            startOnboardingTour();
          }
        }}
      />
    </div>
  );
};
