import React, { useState, useRef } from 'react';
import { ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import { AppLayout, type NavTabKey } from './layouts/AppLayout';
import { OverviewPage } from './pages/OverviewPage';
import { ProductMasterPage } from './pages/ProductMasterPage';
import { ShipmentPage, type ShipmentPageRef } from './pages/ShipmentPage';
import { enterpriseTheme } from './theme/themeConfig';

const App: React.FC = () => {
  const [currentTab, setCurrentTab] = useState<NavTabKey>('shipment');
  const [orderIdToLoad, setOrderIdToLoad] = useState<number | null>(null);
  const shipmentPageRef = useRef<ShipmentPageRef>(null);

  const handleNavigate = (tab: NavTabKey) => {
    setCurrentTab(tab);
    if (tab === 'ocr') {
      setTimeout(() => {
        shipmentPageRef.current?.openOcrModal();
      }, 50);
    }
  };

  const handleOpenOrder = (id: number) => {
    setOrderIdToLoad(id);
    setCurrentTab('shipment');
    setTimeout(() => {
      shipmentPageRef.current?.loadHistoricalOrder(id);
      setOrderIdToLoad(null);
    }, 50);
  };

  return (
    <ConfigProvider locale={viVN} theme={enterpriseTheme}>
      <AppLayout currentTab={currentTab} onTabChange={handleNavigate}>
        {currentTab === 'overview' && (
          <OverviewPage
            onNavigate={handleNavigate}
            onOpenOrder={handleOpenOrder}
          />
        )}

        {currentTab === 'products' && <ProductMasterPage />}

        {(currentTab === 'shipment' || currentTab === 'history' || currentTab === 'ocr') && (
          <ShipmentPage
            ref={shipmentPageRef}
            activeNavTab={currentTab}
            onTabChange={handleNavigate}
            initialOrderIdToLoad={orderIdToLoad}
          />
        )}
      </AppLayout>
    </ConfigProvider>
  );
};

export default App;
