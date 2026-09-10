import React from 'react';
import {
  DatabaseOutlined,
  FileTextOutlined,
} from '@ant-design/icons';

interface NavbarProps {
  currentTab: 'products' | 'shipment';
  onTabChange: (tab: 'products' | 'shipment') => void;
}

export const Navbar: React.FC<NavbarProps> = ({ currentTab, onTabChange }) => {
  return (
    <header className="bg-white border-b border-slate-200/80 sticky top-0 z-30">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-15 flex items-center justify-between">
        {/* Brand Logo & Title */}
        <div className="flex items-center space-x-3 cursor-pointer" onClick={() => onTabChange('shipment')}>
          <div className="w-8 h-8 rounded-lg bg-indigo-600 text-white flex items-center justify-center font-bold text-sm tracking-tighter shadow-xs">
            SE
          </div>
          <div className="flex items-center space-x-2.5">
            <span className="font-semibold text-slate-900 text-sm tracking-tight">
              ShoeExport
            </span>
            <span className="text-slate-300">/</span>
            <span className="text-slate-600 text-sm font-normal">
              INV & PKL Automation
            </span>
            <span className="text-[11px] font-mono font-medium text-indigo-700 bg-indigo-50 px-1.5 py-0.5 rounded border border-indigo-200/60">
              Phase 3
            </span>
          </div>
        </div>

        {/* Minimal Navigation Tabs */}
        <nav className="flex items-center space-x-2">
          <button
            type="button"
            onClick={() => onTabChange('shipment')}
            className={`flex items-center space-x-2 px-3 py-1.5 rounded-md text-xs font-medium transition-colors cursor-pointer ${
              currentTab === 'shipment'
                ? 'bg-indigo-50 text-indigo-700 border border-indigo-200 font-semibold'
                : 'text-slate-600 hover:text-slate-900 hover:bg-slate-50 border border-transparent'
            }`}
          >
            <FileTextOutlined className={currentTab === 'shipment' ? 'text-indigo-600' : 'text-slate-400'} />
            <span>Xuất Hóa đơn & PKL (INV & PKL)</span>
          </button>

          <button
            type="button"
            onClick={() => onTabChange('products')}
            className={`flex items-center space-x-2 px-3 py-1.5 rounded-md text-xs font-medium transition-colors cursor-pointer ${
              currentTab === 'products'
                ? 'bg-indigo-50 text-indigo-700 border border-indigo-200 font-semibold'
                : 'text-slate-600 hover:text-slate-900 hover:bg-slate-50 border border-transparent'
            }`}
          >
            <DatabaseOutlined className={currentTab === 'products' ? 'text-indigo-600' : 'text-slate-400'} />
            <span>Danh mục Hàng hóa (Master Data)</span>
          </button>
        </nav>

        {/* Status Indicator */}
        <div className="hidden sm:flex items-center space-x-2 text-xs text-slate-500">
          <span className="relative flex h-2 w-2">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
            <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500"></span>
          </span>
          <span className="font-mono text-[11px] text-slate-600">Port 5270</span>
        </div>
      </div>
    </header>
  );
};
