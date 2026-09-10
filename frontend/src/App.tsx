import React, { useState } from 'react';
import { ConfigProvider } from 'antd';
import { Navbar } from './components/Navbar';
import { ProductMasterPage } from './pages/ProductMasterPage';
import { ShipmentPage } from './pages/ShipmentPage';

const App: React.FC = () => {
  const [currentTab, setCurrentTab] = useState<'products' | 'shipment'>('shipment');

  return (
    <ConfigProvider
      theme={{
        token: {
          colorPrimary: '#4f46e5', // Refined Indigo accent
          borderRadius: 8,
          fontFamily:
            '-apple-system, BlinkMacSystemFont, "Inter", "Segoe UI", Roboto, sans-serif',
          colorBgBase: '#ffffff',
          colorText: '#334155',
          colorTextHeading: '#0f172a',
          colorTextSecondary: '#64748b',
          colorBorder: '#e2e8f0',
          colorBorderSecondary: '#f1f5f9',
          controlHeight: 36,
        },
        components: {
          Table: {
            headerBg: '#f8fafc',
            headerColor: '#475569',
            borderColor: '#e2e8f0',
            rowHoverBg: '#f8fafc',
            fontSize: 13,
          },
          Card: {
            paddingLG: 20,
          },
          Button: {
            primaryShadow: 'none',
            defaultBorderColor: '#cbd5e1',
            defaultColor: '#334155',
            fontWeight: 500,
          },
          Input: {
            activeBorderColor: '#4f46e5',
            hoverBorderColor: '#94a3b8',
          },
          Tag: {
            borderRadiusSM: 4,
          },
          Modal: {
            titleFontSize: 16,
          },
        },
      }}
    >
      <div className="min-h-screen bg-slate-50 flex flex-col font-sans text-slate-800 antialiased">
        <Navbar currentTab={currentTab} onTabChange={setCurrentTab} />

        <main className="flex-1 py-6 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8">
          {currentTab === 'shipment' ? <ShipmentPage /> : <ProductMasterPage />}
        </main>

        <footer className="border-t border-slate-200/80 py-5 text-center text-xs text-slate-400 bg-white/50">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex flex-col sm:flex-row items-center justify-between gap-2">
            <span>
              ShoeExport Commercial Invoice & Packing List Automation
            </span>
            <span className="text-slate-400">
              Phase 2 • Core Business Logic & Multi-Sheet Excel Engine
            </span>
          </div>
        </footer>
      </div>
    </ConfigProvider>
  );
};

export default App;
