import React from 'react';
import {
  DatabaseOutlined,
  FileTextOutlined,
  HistoryOutlined,
  SafetyCertificateOutlined,
} from '@ant-design/icons';

export type ActiveNavTab = 'shipment' | 'products' | 'history';

interface NavbarProps {
  currentTab: ActiveNavTab;
  onTabChange: (tab: ActiveNavTab) => void;
}

export const Navbar: React.FC<NavbarProps> = ({ currentTab, onTabChange }) => {
  return (
    <header className="bg-white border-b border-slate-200 sticky top-0 z-30">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-14 flex items-center justify-between">
        {/* Brand Logo & Title */}
        <div
          className="flex items-center space-x-2.5 cursor-pointer select-none shrink-0"
          onClick={() => onTabChange('shipment')}
        >
          <div className="w-7 h-7 rounded bg-blue-600 text-white flex items-center justify-center font-bold text-xs">
            SD
          </div>
          <div className="flex items-center space-x-2">
            <span className="font-semibold text-slate-900 text-sm tracking-tight">
              ShoeDocX
            </span>
            <span className="text-slate-300 font-light">|</span>
            <span className="text-slate-500 text-xs font-normal">
              Export Documentation
            </span>
          </div>
        </div>

        {/* Flat / Segmented Navigation Tabs */}
        <nav className="flex items-center space-x-1 p-1 bg-slate-100 rounded-md border border-slate-200">
          <button
            type="button"
            onClick={() => onTabChange('shipment')}
            className={`flex items-center space-x-2 px-3 py-1.5 rounded text-xs font-medium transition-colors cursor-pointer border-none ${
              currentTab === 'shipment'
                ? 'bg-white text-blue-700 font-semibold'
                : 'bg-transparent text-slate-600 hover:text-slate-900'
            }`}
          >
            <FileTextOutlined className={currentTab === 'shipment' ? 'text-blue-600' : 'text-slate-400'} />
            <span>Lập Hóa đơn & Xuất hàng</span>
          </button>

          <button
            type="button"
            onClick={() => onTabChange('products')}
            className={`flex items-center space-x-2 px-3 py-1.5 rounded text-xs font-medium transition-colors cursor-pointer border-none ${
              currentTab === 'products'
                ? 'bg-white text-blue-700 font-semibold'
                : 'bg-transparent text-slate-600 hover:text-slate-900'
            }`}
          >
            <DatabaseOutlined className={currentTab === 'products' ? 'text-blue-600' : 'text-slate-400'} />
            <span>Master Data</span>
          </button>

          <button
            type="button"
            onClick={() => onTabChange('history')}
            className={`flex items-center space-x-2 px-3 py-1.5 rounded text-xs font-medium transition-colors cursor-pointer border-none ${
              currentTab === 'history'
                ? 'bg-white text-blue-700 font-semibold'
                : 'bg-transparent text-slate-600 hover:text-slate-900'
            }`}
          >
            <HistoryOutlined className={currentTab === 'history' ? 'text-blue-600' : 'text-slate-400'} />
            <span>Lịch sử</span>
          </button>
        </nav>

        {/* Server Status Badge & Session Info */}
        <div className="hidden md:flex items-center space-x-3 text-xs shrink-0">
          <div className="flex items-center space-x-1.5 bg-emerald-50 text-emerald-800 px-2 py-0.5 rounded border border-emerald-200 text-[11px] font-medium">
            <span className="inline-block w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
            <span>API Kết nối: 5270</span>
          </div>

          <div className="flex items-center space-x-1 text-slate-500 pl-2 border-l border-slate-200">
            <SafetyCertificateOutlined className="text-slate-400" />
            <span className="text-xs text-slate-600">Kingmaker III</span>
          </div>
        </div>
      </div>
    </header>
  );
};
