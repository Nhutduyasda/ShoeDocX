import React, { useState, useRef } from 'react';
import { ConfigProvider, Spin } from 'antd';
import viVN from 'antd/locale/vi_VN';
import { AppLayout, type NavTabKey } from './layouts/AppLayout';
import { OverviewPage } from './pages/OverviewPage';
import { ProductMasterPage } from './pages/ProductMasterPage';
import { ShipmentPage, type ShipmentPageRef } from './pages/ShipmentPage';
import { CustomsSettlementPage } from './pages/CustomsSettlementPage';
import { WarehousePage } from './pages/WarehousePage';
import { BomCalculatorPage } from './pages/BomCalculatorPage';
import { MaterialManagementPage } from './pages/MaterialManagementPage';
import { BomManagementPage } from './pages/BomManagementPage';
import { MaterialPlanningPage } from './pages/MaterialPlanningPage';
import { LoginPage } from './pages/LoginPage';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { getAllowedTabsForUser, getDefaultTabForUser } from './types/auth';
import { enterpriseTheme } from './theme/themeConfig';

const MainApp: React.FC = () => {
  const { isAuthenticated, loading, user } = useAuth();
  const [selectedTab, setSelectedTab] = useState<NavTabKey | null>(null);
  const [orderIdToLoad, setOrderIdToLoad] = useState<number | null>(null);
  const shipmentPageRef = useRef<ShipmentPageRef>(null);

  // Khi user thay đổi (đăng nhập tài khoản mới hoặc đổi role), tự động reset tab về mặc định của role đó
  const lastUserIdRef = useRef<string | undefined>(user?.id);
  React.useEffect(() => {
    if (user?.id !== lastUserIdRef.current) {
      lastUserIdRef.current = user?.id;
      setSelectedTab(getDefaultTabForUser(user));
    }
  }, [user]);

  const allowedTabs = getAllowedTabsForUser(user);
  const defaultTab = getDefaultTabForUser(user);

  // Đảm bảo tab hiện tại luôn nằm trong danh sách hợp lệ của role
  const currentTab: NavTabKey = selectedTab && allowedTabs.includes(selectedTab)
    ? selectedTab
    : defaultTab;

  const handleNavigate = (tab: NavTabKey) => {
    if (!allowedTabs.includes(tab)) {
      setSelectedTab(defaultTab);
      return;
    }
    setSelectedTab(tab);
    if (tab === 'ocr') {
      setTimeout(() => {
        shipmentPageRef.current?.openOcrModal();
      }, 50);
    }
  };

  const handleOpenOrder = (id: number) => {
    setOrderIdToLoad(id);
    setSelectedTab('shipment');
    setTimeout(() => {
      shipmentPageRef.current?.loadHistoricalOrder(id);
      setOrderIdToLoad(null);
    }, 50);
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-slate-50 flex flex-col items-center justify-center space-y-4">
        <Spin size="large" />
        <div className="text-slate-500 text-xs tracking-wider">Đang xác thực phiên làm việc...</div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <LoginPage />;
  }

  return (
    <AppLayout currentTab={currentTab} onTabChange={handleNavigate}>
      {currentTab === 'overview' && (
        <OverviewPage
          onNavigate={handleNavigate}
          onOpenOrder={handleOpenOrder}
        />
      )}

      {currentTab === 'warehouse' && <WarehousePage />}

      {currentTab === 'bom' && <BomCalculatorPage />}

      {currentTab === 'materials' && <MaterialManagementPage />}

      {currentTab === 'bom-master' && <BomManagementPage />}

      {currentTab === 'material-planning' && <MaterialPlanningPage />}

      {currentTab === 'products' && <ProductMasterPage />}

      {currentTab === 'settlement' && <CustomsSettlementPage />}

      {(currentTab === 'shipment' || currentTab === 'history' || currentTab === 'ocr') && (
        <ShipmentPage
          ref={shipmentPageRef}
          activeNavTab={currentTab}
          onTabChange={handleNavigate}
          initialOrderIdToLoad={orderIdToLoad}
        />
      )}
    </AppLayout>
  );
};

const App: React.FC = () => {
  return (
    <ConfigProvider locale={viVN} theme={enterpriseTheme}>
      <AuthProvider>
        <MainApp />
      </AuthProvider>
    </ConfigProvider>
  );
};

export default App;
