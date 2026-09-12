import { useState, useEffect } from 'react';
import { Drawer, Breadcrumb, Tooltip, Dropdown, Button, Tag } from 'antd';
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
  CheckCircleFilled,
  AuditOutlined,
  QuestionCircleOutlined,
  RocketOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  LogoutOutlined,
} from '@ant-design/icons';
import { CheatsheetModal } from '../components/CheatsheetModal';
import { startOnboardingTour } from '../services/tourService';
import { useAuth } from '../contexts/AuthContext';
import { isKhoUser, isKeToanUser, isXnkUser, isAdminUser, getDefaultTabForUser, type NavTabKey } from '../types/auth';

export type { NavTabKey };

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
  const { user, logout } = useAuth();
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

  const menuGroups: MenuGroupDef[] = (() => {
    if (isKhoUser(user)) {
      return [
        {
          title: 'KHO THÀNH PHẨM (成品鞋仓库)',
          items: [
            {
              key: 'warehouse',
              label: 'Lưới xuất kho (Fast-Grid)',
              icon: <FileTextOutlined className="text-base" />,
            },
            {
              key: 'products',
              label: 'Tra cứu mã giày & Quy cách',
              icon: <DatabaseOutlined className="text-base" />,
            },
          ],
        },
      ];
    }

    if (isXnkUser(user)) {
      return [
        {
          title: 'NGHIỆP VỤ XUẤT NHẬP KHẨU (进出口)',
          items: [
            {
              key: 'shipment',
              label: 'Lập Invoice / Packing List',
              icon: <FileTextOutlined className="text-base" />,
            },
            {
              key: 'history',
              label: 'Lịch sử chứng từ',
              icon: <HistoryOutlined className="text-base" />,
            },
            {
              key: 'ocr',
              label: 'Quét ảnh phiếu kho',
              icon: <CameraOutlined className="text-base" />,
              badge: 'OCR',
            },
          ],
        },
        {
          title: 'DANH MỤC & HỢP ĐỒNG',
          items: [
            {
              key: 'products',
              label: 'Master Data mã giày & Hợp đồng',
              icon: <DatabaseOutlined className="text-base" />,
            },
          ],
        },
      ];
    }

    if (isKeToanUser(user)) {
      return [
        {
          title: 'KẾ TOÁN DOANH THU (财务核算)',
          items: [
            {
              key: 'overview',
              label: 'Thống kê Doanh thu CMT',
              icon: <DashboardOutlined className="text-base" />,
            },
            {
              key: 'settlement',
              label: 'Quyết toán đối chiếu nội bộ',
              icon: <AuditOutlined className="text-base" />,
              badge: 'BCQT',
            },
            {
              key: 'history',
              label: 'Lô hàng đã thông quan (HĐ)',
              icon: <HistoryOutlined className="text-base" />,
            },
            {
              key: 'products',
              label: 'Bảng giá gia công CMT/DAP',
              icon: <DatabaseOutlined className="text-base" />,
            },
          ],
        },
      ];
    }

    // Ban Giám Đốc / Quản trị viên (Admin)
    return [
      {
        title: 'TỔNG QUAN',
        items: [
          {
            key: 'overview',
            label: 'Tổng quan điều hành',
            icon: <DashboardOutlined className="text-base" />,
          },
        ],
      },
      {
        title: 'BỘ PHẬN KHO',
        items: [
          {
            key: 'warehouse',
            label: 'Giám sát xuất kho (Fast-Grid)',
            icon: <RocketOutlined className="text-base" />,
          },
        ],
      },
      {
        title: 'BỘ PHẬN XNK',
        items: [
          {
            key: 'shipment',
            label: 'Lập Invoice / Packing List',
            icon: <FileTextOutlined className="text-base" />,
          },
          {
            key: 'history',
            label: 'Lịch sử chứng từ',
            icon: <HistoryOutlined className="text-base" />,
          },
          {
            key: 'ocr',
            label: 'Quét ảnh phiếu kho',
            icon: <CameraOutlined className="text-base" />,
            badge: 'OCR',
          },
        ],
      },
      {
        title: 'BỘ PHẬN KẾ TOÁN',
        items: [
          {
            key: 'settlement',
            label: 'Báo cáo Quyết toán BCQT',
            icon: <AuditOutlined className="text-base" />,
            badge: 'BCQT',
          },
        ],
      },
      {
        title: 'HỆ THỐNG',
        items: [
          {
            key: 'products',
            label: 'Master Data & Cấu hình',
            icon: <DatabaseOutlined className="text-base" />,
          },
        ],
      },
    ];
  })();

  const getBreadcrumbItems = () => {
    switch (currentTab) {
      case 'overview':
        return [
          { title: <span className="text-slate-400">Hệ thống</span> },
          { title: <span className="text-slate-700 font-medium">Tổng quan nghiệp vụ</span> },
        ];
      case 'warehouse':
        return [
          { title: <span className="text-slate-400">Kho & Giao nhận</span> },
          { title: <span className="text-slate-700 font-medium">Lưới xuất kho thành phẩm (Fast-Grid / 成品鞋出货交接)</span> },
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
          { title: <span className="text-slate-700 font-medium">Quyết toán Hải quan (đối chiếu nội bộ) & Thống kê Kim ngạch</span> },
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
      key: 'user-profile',
      label: (
        <div className="py-1">
          <div className="font-semibold text-xs text-slate-800">{user?.fullName || 'Người dùng'}</div>
          <div className="text-[11px] text-blue-600 font-medium">{user?.departmentName || 'Hệ thống'}</div>
          <div className="text-[10px] text-slate-400 font-mono">@{user?.username}</div>
        </div>
      ),
      disabled: true,
    },
    { type: 'divider' },
    {
      key: 'org',
      label: (
        <div className="py-0.5">
          <div className="text-xs text-slate-700 font-medium">Hải An New Material / KM3</div>
          <div className="text-[10px] text-slate-400">Gia công xuất khẩu E52 CMT/DAP</div>
        </div>
      ),
      disabled: true,
    },
    { type: 'divider' },
    {
      key: 'logout',
      icon: <LogoutOutlined className="text-rose-500" />,
      label: <span className="text-xs text-rose-600 font-medium">Đăng xuất tài khoản</span>,
      onClick: () => logout(),
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
                onTabChange(getDefaultTabForUser(user));
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
                className="flex items-center space-x-2.5 pl-2.5 border-l border-slate-200 cursor-pointer bg-transparent border-none text-left p-1 rounded-lg hover:bg-slate-100 transition-colors"
              >
                <div className="w-8 h-8 rounded-full bg-blue-50 text-blue-600 flex items-center justify-center text-xs font-semibold border border-blue-200 shadow-xs">
                  <UserOutlined />
                </div>
                <div className="hidden md:flex flex-col text-left leading-tight">
                  <div className="flex items-center space-x-1.5">
                    <span className="text-xs font-semibold text-slate-800">{user?.fullName || 'Người dùng'}</span>
                    <Tag
                      color={
                        isKhoUser(user)
                          ? 'green'
                          : isXnkUser(user)
                          ? 'blue'
                          : isKeToanUser(user)
                          ? 'gold'
                          : isAdminUser(user)
                          ? 'purple'
                          : 'default'
                      }
                      className="m-0 text-[10px] leading-4 px-1.5 py-0 border-0 font-medium"
                    >
                      {user?.departmentName || 'Phòng ban'}
                    </Tag>
                  </div>
                  <span className="text-[10px] text-slate-400 font-mono">@{user?.username}</span>
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
