import React, { useRef, useState } from 'react';
import { Modal, Button, Tabs, Dropdown, Tag, Tooltip } from 'antd';
import type { MenuProps } from 'antd';
import {
  PrinterOutlined,
  DownloadOutlined,
  CloseOutlined,
  FileTextOutlined,
  InboxOutlined,
  DownOutlined,
  CheckCircleOutlined,
} from '@ant-design/icons';
import { useReactToPrint } from 'react-to-print';
import type { DocumentPreviewResponse, InvoicePreviewItem, PklBreakdownItem } from '../types';

interface DocumentPreviewModalProps {
  visible: boolean;
  onClose: () => void;
  data: DocumentPreviewResponse | null;
  onExportExcel: () => void;
  exporting: boolean;
  disableExport?: boolean;
}

const format2 = (value: number) => value.toLocaleString('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

export const DocumentPreviewModal: React.FC<DocumentPreviewModalProps> = ({
  visible,
  onClose,
  data,
  onExportExcel,
  exporting,
  disableExport = false,
}) => {
  const [activeTab, setActiveTab] = useState<'inv' | 'pkl'>('inv');
  const [printScope, setPrintScope] = useState<'active' | 'inv' | 'pkl' | 'all'>('active');

  const invPrintRef = useRef<HTMLDivElement>(null);
  const pklPrintRef = useRef<HTMLDivElement>(null);
  const allPrintRef = useRef<HTMLDivElement>(null);

  const printInv = useReactToPrint({
    contentRef: invPrintRef,
    documentTitle: data ? `${data.invoice.invoiceNo || 'Commercial_Invoice'}_INV` : 'Commercial_Invoice',
  });

  const printPkl = useReactToPrint({
    contentRef: pklPrintRef,
    documentTitle: data ? `${data.packingList.invoiceNo || 'Packing_List'}_PKL` : 'Packing_List',
  });

  const printAll = useReactToPrint({
    contentRef: allPrintRef,
    documentTitle: data ? `${data.invoice.invoiceNo || 'Shipment_Document'}_FULL` : 'Shipment_Document',
  });

  if (!data) return null;

  const { invoice, packingList } = data;

  const handlePrint = (scope: 'active' | 'inv' | 'pkl' | 'all') => {
    setPrintScope(scope);
    if (scope === 'inv' || (scope === 'active' && activeTab === 'inv')) {
      printInv();
    } else if (scope === 'pkl' || (scope === 'active' && activeTab === 'pkl')) {
      printPkl();
    } else {
      printAll();
    }
  };

  const printMenuItems: MenuProps['items'] = [
    {
      key: 'print-current',
      label: `In trang hiện tại (${activeTab === 'inv' ? 'Hóa đơn INV' : 'Bảng kê PKL'})`,
      icon: <PrinterOutlined />,
      onClick: () => handlePrint('active'),
    },
    {
      key: 'print-inv',
      label: 'In riêng Hóa đơn (Commercial Invoice)',
      icon: <FileTextOutlined />,
      onClick: () => handlePrint('inv'),
    },
    {
      key: 'print-pkl',
      label: 'In riêng Bảng kê đóng gói (Packing List)',
      icon: <InboxOutlined />,
      onClick: () => handlePrint('pkl'),
    },
    {
      type: 'divider',
    },
    {
      key: 'print-all',
      label: 'In toàn bộ chứng từ (INV + PKL)',
      icon: <CheckCircleOutlined className="text-blue-600" />,
      onClick: () => handlePrint('all'),
    },
  ];

  return (
    <Modal
      open={visible}
      onCancel={onClose}
      width={1120}
      footer={null}
      destroyOnClose
      centered
      className="document-preview-modal p-0 top-6"
      title={
        <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-3 pr-8 border-b border-slate-200 gap-3">
          <div className="flex items-center gap-3">
            <span className="text-base font-semibold text-slate-900">
              Xem trước Hóa đơn & Đóng gói (Live Preview)
            </span>
            <Tag color="blue" className="font-mono text-xs">
              {invoice.invoiceNo || 'CHƯA CÓ SỐ HĐ'}
            </Tag>
          </div>

          <div className="flex items-center gap-2">
            <Dropdown menu={{ items: printMenuItems }} placement="bottomRight">
              <Button
                type="primary"
                icon={<PrinterOutlined />}
                onClick={() => handlePrint(printScope)}
                className="bg-blue-600 hover:bg-blue-700 text-white font-medium text-xs h-8 shadow-sm flex items-center gap-1"
              >
                <span>In chứng từ (Print)</span>
                <DownOutlined className="text-[10px]" />
              </Button>
            </Dropdown>

            <Tooltip
              title={
                disableExport
                  ? 'Vui lòng bổ sung đầy đủ Master Data trước khi xuất Excel'
                  : 'Tải file Excel hoàn chỉnh (.xlsx)'
              }
            >
              <Button
                icon={<DownloadOutlined />}
                loading={exporting}
                disabled={disableExport || exporting}
                onClick={onExportExcel}
                className="text-xs h-8 font-medium border-slate-300 text-slate-700 hover:text-blue-600 hover:border-blue-600"
              >
                Xuất File Excel (.xlsx)
              </Button>
            </Tooltip>

            <Button
              icon={<CloseOutlined />}
              onClick={onClose}
              className="text-xs h-8 text-slate-500 hover:text-slate-700"
            >
              Đóng
            </Button>
          </div>
        </div>
      }
    >
      <div className="bg-slate-100 -mx-6 -mb-6 p-4 sm:p-6 overflow-y-auto max-h-[calc(88vh-110px)]">
        {/* Navigation Tabs */}
        <div className="max-w-4xl mx-auto mb-4 bg-white rounded-lg p-1.5 shadow-sm border border-slate-200 flex justify-between items-center">
          <Tabs
            activeKey={activeTab}
            onChange={(key) => setActiveTab(key as 'inv' | 'pkl')}
            className="custom-preview-tabs flex-1"
            items={[
              {
                key: 'inv',
                label: (
                  <span className="flex items-center gap-2 px-2 py-0.5 text-xs font-semibold">
                    <FileTextOutlined /> Hóa đơn (Commercial Invoice)
                  </span>
                ),
              },
              {
                key: 'pkl',
                label: (
                  <span className="flex items-center gap-2 px-2 py-0.5 text-xs font-semibold">
                    <InboxOutlined /> Bảng kê đóng gói (Packing List)
                  </span>
                ),
              },
            ]}
          />
          <div className="text-xs text-slate-500 pr-3 font-medium">
            Khổ in: <span className="font-semibold text-slate-700">A4 Portrait (Chuẩn kế toán)</span>
          </div>
        </div>

        {/* Tab 1: Commercial Invoice (INV) */}
        {activeTab === 'inv' && (
          <div
            ref={invPrintRef}
            className="invoice-a4-sheet bg-white shadow-xl max-w-4xl mx-auto p-8 sm:p-10 border border-slate-200 print:shadow-none print:border-none print:p-0 print:m-0"
            style={{ minHeight: '297mm', fontFamily: 'Arial, "Helvetica Neue", sans-serif' }}
          >
            {/* Header Company */}
            <div className="text-center pb-4 border-b border-slate-300">
              <h1 className="text-base sm:text-lg font-bold uppercase tracking-wide text-slate-900 m-0">
                {invoice.sellerName}
              </h1>
              <p className="text-xs text-slate-600 mt-1 mb-0">
                {invoice.sellerAddressLine1}
              </p>
              {invoice.sellerAddressLine2 && <p className="text-xs text-slate-600 m-0">{invoice.sellerAddressLine2}</p>}
            </div>

            {/* Document Title */}
            <div className="text-center my-6">
              <h2 className="text-xl sm:text-2xl font-extrabold uppercase text-slate-900 tracking-wider m-0">
                COMMERCIAL INVOICE
              </h2>
              <div className="text-xs text-slate-500 mt-1 italic">HÓA ĐƠN THƯƠNG MẠI GIA CÔNG XUẤT KHẨU</div>
            </div>

            {/* Parties and Terms Metadata */}
            <div className="grid grid-cols-2 gap-4 text-xs border border-slate-400 p-4 mb-5 rounded-xs bg-slate-50/50">
              <div className="space-y-1.5 pr-3 border-r border-slate-300">
                <div>
                  <span className="font-semibold text-slate-800">Bên thuê gia công (Buyer):</span>
                  <div className="font-bold text-slate-900 mt-0.5">{invoice.buyerName}</div>
                </div>
                <div>
                  <span className="font-semibold text-slate-800">Địa chỉ (Address):</span>
                  <div className="text-slate-700 mt-0.5">{invoice.buyerAddressLine1}</div>
                  {invoice.buyerAddressLine2 && <div className="text-slate-700">{invoice.buyerAddressLine2}</div>}
                </div>
                <div><span className="font-semibold text-slate-800">Bên nhận gia công:</span><div className="font-bold">{invoice.sellerName}</div></div>
                <div><span className="font-semibold text-slate-800">Địa chỉ:</span><div>{invoice.sellerAddressLine1}</div>{invoice.sellerAddressLine2 && <div>{invoice.sellerAddressLine2}</div>}</div>
              </div>

              <div className="space-y-1.5 pl-3">
                <div className="flex justify-between">
                  <span className="font-semibold text-slate-800">Số hóa đơn (Invoice No):</span>
                  <span className="font-bold text-slate-900 font-mono">{invoice.invoiceNo}</span>
                </div>
                <div className="flex justify-between">
                  <span className="font-semibold text-slate-800">Ngày lập (Date):</span>
                  <span className="font-medium text-slate-800">{invoice.invoiceDate}</span>
                </div>
                <div className="flex justify-between">
                  <span className="font-semibold text-slate-800">Hợp đồng (Contract No):</span>
                  <span className="font-medium text-slate-800">{invoice.contractNo}</span>
                </div>
                <div className="flex justify-between">
                  <span className="font-semibold text-slate-800">Điều kiện giao (Terms):</span>
                  <span className="font-semibold text-blue-700">{invoice.deliveryTerms}</span>
                </div>
                <div className="flex justify-between">
                  <span className="font-semibold text-slate-800">Thanh toán (Payment):</span>
                  <span className="font-medium text-slate-800">{invoice.paymentTerms}</span>
                </div>
                <div className="flex justify-between">
                  <span className="font-semibold text-slate-800">Nước đến:</span>
                  <span className="font-medium text-slate-800">{invoice.destinationCountry}</span>
                </div>
              </div>
            </div>

            {/* Items Table */}
            <div className="overflow-x-auto">
              <table className="w-full text-[11px] border-collapse border border-slate-900">
                <thead>
                  <tr className="bg-slate-100 text-slate-900 text-center font-bold">
                    <th className="border border-slate-900 p-1.5 w-10">STT</th>
                    <th className="border border-slate-900 p-1.5 w-32">Mã hàng</th>
                    <th className="border border-slate-900 p-1.5">Mô tả hàng hóa</th>
                    <th className="border border-slate-900 p-1.5 w-16">Số lượng</th>
                    <th className="border border-slate-900 p-1.5 w-12">ĐVT</th>
                    <th className="border border-slate-900 p-1.5 w-20">Đơn giá CMT</th>
                    <th className="border border-slate-900 p-1.5 w-20">Đơn giá DAP</th>
                    <th className="border border-slate-900 p-1.5 w-24">Thành tiền CMT</th>
                    <th className="border border-slate-900 p-1.5 w-24">Thành tiền DAP</th>
                  </tr>
                </thead>
                <tbody>
                  {invoice.items.map((item: InvoicePreviewItem, idx: number) => (
                    <tr key={idx} className="hover:bg-slate-50/70">
                      <td className="border border-slate-900 p-1.5 text-center font-mono">{item.lineNo}</td>
                      <td className="border border-slate-900 p-1.5 font-mono font-semibold text-slate-900">
                        {item.fullItemCode}
                      </td>
                      <td className="border border-slate-900 p-1.5 text-slate-800">
                        {item.description}
                      </td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono font-medium">
                        {format2(item.quantity)}
                      </td>
                      <td className="border border-slate-900 p-1.5 text-center">{item.unit}</td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">
                        {item.unitPriceCMT.toFixed(2)}
                      </td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">
                        {item.unitPriceDAP.toFixed(2)}
                      </td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">
                        {format2(item.quantity * item.unitPriceCMT)}
                      </td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono font-semibold text-blue-900">
                        {format2(item.quantity * item.unitPriceDAP)}
                      </td>
                    </tr>
                  ))}
                  {/* Totals Row */}
                  <tr className="bg-slate-100 font-bold text-slate-900">
                    <td colSpan={3} className="border border-slate-900 p-2 text-center uppercase tracking-wider">
                      TỔNG CỘNG / TOTAL:
                    </td>
                    <td className="border border-slate-900 p-2 text-right font-mono text-sm text-slate-900">
                      {format2(invoice.totalQuantity)}
                    </td>
                    <td className="border border-slate-900 p-2 text-center"></td>
                    <td className="border border-slate-900 p-2"></td>
                    <td className="border border-slate-900 p-2"></td>
                    <td className="border border-slate-900 p-2 text-right font-mono">
                      {format2(invoice.totalAmountCMT)}
                    </td>
                    <td className="border border-slate-900 p-2 text-right font-mono text-sm text-blue-900">
                      {format2(invoice.totalAmountDAP)}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            {/* Amount In Words */}
            <div className="mt-3 p-2.5 bg-slate-50 border border-slate-300 rounded-xs text-xs text-slate-800">
              <span className="font-bold text-slate-900">Bằng chữ: </span>
              <span className="italic font-medium text-slate-800">{invoice.totalAmountDAPInWords}</span>
            </div>
            <div className="mt-6 text-left text-xs font-bold uppercase">{invoice.sellerName}</div>

            {/* Signatures Section - ABSOLUTELY CLEAN, NO OLD STAMP */}
            <div className="grid grid-cols-2 gap-8 mt-12 pt-6 text-center text-xs">
              <div>
                <div className="font-bold uppercase text-slate-900">ĐẠI DIỆN BÊN MUA (BUYER)</div>
                <div className="text-[11px] text-slate-500 italic mt-0.5">Ký tên & ghi rõ họ tên</div>
                <div className="h-28 flex items-center justify-center text-slate-300 italic text-[11px]">
                  (Chữ ký đại diện bên mua)
                </div>
              </div>
              <div>
                <div className="font-bold uppercase text-slate-900">ĐẠI DIỆN BÊN BÁN (SELLER)</div>
                <div className="text-[11px] text-slate-500 italic mt-0.5">Ký tên & đóng dấu hợp lệ</div>
                <div className="h-28 flex items-center justify-center text-slate-300 italic text-[11px]">
                  (Chữ ký đại diện bên bán)
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Tab 2: Packing List (PKL) */}
        {activeTab === 'pkl' && (
          <div
            ref={pklPrintRef}
            className="pkl-a4-sheet bg-white shadow-xl max-w-4xl mx-auto p-8 sm:p-10 border border-slate-200 print:shadow-none print:border-none print:p-0 print:m-0"
            style={{ minHeight: '297mm', fontFamily: 'Arial, "Helvetica Neue", sans-serif' }}
          >
            {/* Header Company */}
            <div className="text-center pb-4 border-b border-slate-300">
              <h1 className="text-base sm:text-lg font-bold uppercase tracking-wide text-slate-900 m-0">
                {invoice.sellerName}
              </h1>
              <p className="text-xs text-slate-600 mt-1 mb-0">
                {invoice.sellerAddressLine1}
              </p>
              {invoice.sellerAddressLine2 && <p className="text-xs text-slate-600 m-0">{invoice.sellerAddressLine2}</p>}
            </div>

            {/* Document Title */}
            <div className="text-center my-6">
              <h2 className="text-xl sm:text-2xl font-extrabold uppercase text-slate-900 tracking-wider m-0">
                PACKING LIST
              </h2>
              <div className="text-xs text-slate-500 mt-1 italic">BẢNG KÊ CHI TIẾT ĐÓNG GÓI LÔ HÀNG XUẤT KHẨU</div>
            </div>

            {/* Metadata Bar */}
            <div className="flex flex-wrap justify-between items-center text-xs border border-slate-400 p-3 mb-5 rounded-xs bg-slate-50/50">
              <div>
                <span className="font-semibold text-slate-700">Số hóa đơn: </span>
                <span className="font-bold font-mono text-slate-900">{packingList.invoiceNo}</span>
              </div>
              <div>
                <span className="font-semibold text-slate-700">Hợp đồng: </span>
                <span className="font-medium text-slate-900">{invoice.contractNo}</span>
              </div>
              <div>
                <span className="font-semibold text-slate-700">Ngày lập: </span>
                <span className="font-medium text-slate-900">{invoice.invoiceDate}</span>
              </div>
              <div>
                <span className="font-semibold text-slate-700">Quy cách: </span>
                <span className="font-medium text-slate-900">Theo Master Data từng mặt hàng</span>
              </div>
            </div>

            {/* PKL Table */}
            <div className="overflow-x-auto">
              <table className="w-full text-[11px] border-collapse border border-slate-900">
                <thead>
                  <tr className="bg-slate-100 text-slate-900 text-center font-bold">
                    <th className="border border-slate-900 p-1.5 w-20">Dải số kiện</th>
                    <th className="border border-slate-900 p-1.5 w-32">Mã hàng</th>
                    <th className="border border-slate-900 p-1.5">Mô tả hàng hóa</th>
                    <th className="border border-slate-900 p-1.5 w-16">Số lượng</th>
                    <th className="border border-slate-900 p-1.5 w-12">ĐVT</th>
                    <th className="border border-slate-900 p-1.5 w-16">Số kiện</th>
                    <th className="border border-slate-900 p-1.5 w-20">N.W (KGS)</th>
                    <th className="border border-slate-900 p-1.5 w-20">G.W (KGS)</th>
                  </tr>
                </thead>
                <tbody>
                  {packingList.breakdownItems.map((item: PklBreakdownItem, idx: number) => (
                    <tr key={idx} className={item.isOddCarton ? 'bg-amber-50/30' : 'hover:bg-slate-50/70'}>
                      <td className="border border-slate-900 p-1.5 text-center font-mono font-semibold text-slate-900">
                        {item.cartonRange}
                      </td>
                      <td className="border border-slate-900 p-1.5 font-mono font-semibold text-slate-900">
                        {item.fullItemCode}
                      </td>
                      <td className="border border-slate-900 p-1.5 text-slate-800">
                        {item.description}
                        {item.isOddCarton && (
                          <span className="text-[10px] text-amber-700 font-medium ml-1.5">
                            (Thùng lẻ: {item.pairsPerCarton} đôi)
                          </span>
                        )}
                      </td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono font-medium">
                        {format2(item.quantity)}
                      </td>
                      <td className="border border-slate-900 p-1.5 text-center">PRS</td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono font-medium">
                        {format2(item.cartonCount)}
                      </td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">
                        {item.netWeight.toFixed(2)}
                      </td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">
                        {item.grossWeight.toFixed(2)}
                      </td>
                    </tr>
                  ))}
                  {/* Totals Row */}
                  <tr className="bg-slate-100 font-bold text-slate-900">
                    <td colSpan={3} className="border border-slate-900 p-2 text-center uppercase tracking-wider">
                      TỔNG CỘNG / TOTAL:
                    </td>
                    <td className="border border-slate-900 p-2 text-right font-mono text-sm text-slate-900">
                      {format2(packingList.totalQuantity)}
                    </td>
                    <td className="border border-slate-900 p-2 text-center"></td>
                    <td className="border border-slate-900 p-2 text-right font-mono text-sm text-blue-900">
                      {format2(packingList.totalCartons)}
                    </td>
                    <td className="border border-slate-900 p-2 text-right font-mono">
                      {packingList.totalNetWeight.toFixed(2)}
                    </td>
                    <td className="border border-slate-900 p-2 text-right font-mono">
                      {packingList.totalGrossWeight.toFixed(2)}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div className="mt-6 text-left text-xs font-bold uppercase">{invoice.sellerName}</div>

            {/* Summary badges */}
            <div className="grid grid-cols-4 gap-3 mt-4 text-xs text-slate-700">
              <div className="bg-slate-50 p-2 rounded border border-slate-200 text-center">
                <div className="text-slate-500 text-[10px]">Tổng số lượng</div>
                <div className="font-bold text-slate-900 font-mono text-sm mt-0.5">
                  {format2(packingList.totalQuantity)}
                </div>
              </div>
              <div className="bg-slate-50 p-2 rounded border border-slate-200 text-center">
                <div className="text-slate-500 text-[10px]">Tổng số kiện</div>
                <div className="font-bold text-blue-700 font-mono text-sm mt-0.5">
                  {format2(packingList.totalCartons)} kiện
                </div>
              </div>
              <div className="bg-slate-50 p-2 rounded border border-slate-200 text-center">
                <div className="text-slate-500 text-[10px]">Tổng Net Weight</div>
                <div className="font-bold text-slate-900 font-mono text-sm mt-0.5">
                  {packingList.totalNetWeight.toFixed(2)} KGS
                </div>
              </div>
              <div className="bg-slate-50 p-2 rounded border border-slate-200 text-center">
                <div className="text-slate-500 text-[10px]">Tổng Gross Weight</div>
                <div className="font-bold text-slate-900 font-mono text-sm mt-0.5">
                  {packingList.totalGrossWeight.toFixed(2)} KGS
                </div>
              </div>
            </div>

            {/* Signatures Section */}
            <div className="grid grid-cols-2 gap-8 mt-12 pt-6 text-center text-xs">
              <div>
                <div className="font-bold uppercase text-slate-900">NGƯỜI LẬP BIỂU (PREPARED BY)</div>
                <div className="text-[11px] text-slate-500 italic mt-0.5">Ký & ghi rõ họ tên</div>
                <div className="h-28 flex items-center justify-center text-slate-300 italic text-[11px]">
                  (Chữ ký người lập biểu)
                </div>
              </div>
              <div>
                <div className="font-bold uppercase text-slate-900">XÁC NHẬN BÊN BÁN (AUTHORIZED SIGNATURE)</div>
                <div className="text-[11px] text-slate-500 italic mt-0.5">Ký tên & đóng dấu hợp lệ</div>
                <div className="h-28 flex items-center justify-center text-slate-300 italic text-[11px]">
                  (Chữ ký & con dấu đại diện)
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Hidden Container for Printing Full Set (INV + PKL continuous) */}
        <div style={{ display: 'none' }}>
          <div ref={allPrintRef} className="print-all-container">
            {/* Sheet 1: Commercial Invoice */}
            <div
              className="invoice-a4-sheet p-8"
              style={{ pageBreakAfter: 'always', fontFamily: 'Arial, "Helvetica Neue", sans-serif' }}
            >
              <div className="text-center pb-4 border-b border-slate-300">
                <h1 className="text-base font-bold uppercase tracking-wide text-slate-900 m-0">
                  {invoice.sellerName}
                </h1>
                <p className="text-xs text-slate-600 mt-1 mb-0">
                  {invoice.sellerAddressLine1}
                </p>
                {invoice.sellerAddressLine2 && <p className="text-xs text-slate-600 m-0">{invoice.sellerAddressLine2}</p>}
              </div>

              <div className="text-center my-6">
                <h2 className="text-xl font-extrabold uppercase text-slate-900 tracking-wider m-0">
                  COMMERCIAL INVOICE
                </h2>
                <div className="text-xs text-slate-500 mt-1 italic">HÓA ĐƠN THƯƠNG MẠI GIA CÔNG XUẤT KHẨU</div>
              </div>

              <div className="grid grid-cols-2 gap-4 text-xs border border-slate-400 p-4 mb-5 rounded-xs">
                <div className="space-y-1.5 pr-3 border-r border-slate-300">
                  <div>
                    <span className="font-semibold text-slate-800">Bên thuê gia công (Buyer):</span>
                    <div className="font-bold text-slate-900 mt-0.5">{invoice.buyerName}</div>
                  </div>
                  <div><span className="font-semibold">Bên nhận gia công:</span><div className="font-bold">{invoice.sellerName}</div></div>
                  <div><span className="font-semibold">Địa chỉ:</span><div>{invoice.sellerAddressLine1}</div>{invoice.sellerAddressLine2 && <div>{invoice.sellerAddressLine2}</div>}</div>
                  <div>
                    <span className="font-semibold text-slate-800">Địa chỉ (Address):</span>
                    <div className="text-slate-700 mt-0.5">{invoice.buyerAddressLine1}</div>
                    {invoice.buyerAddressLine2 && <div className="text-slate-700">{invoice.buyerAddressLine2}</div>}
                  </div>
                </div>

                <div className="space-y-1.5 pl-3">
                  <div className="flex justify-between">
                    <span className="font-semibold text-slate-800">Số hóa đơn (Invoice No):</span>
                    <span className="font-bold text-slate-900 font-mono">{invoice.invoiceNo}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="font-semibold text-slate-800">Ngày lập (Date):</span>
                    <span className="font-medium text-slate-800">{invoice.invoiceDate}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="font-semibold text-slate-800">Hợp đồng (Contract No):</span>
                    <span className="font-medium text-slate-800">{invoice.contractNo}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="font-semibold text-slate-800">Điều kiện giao (Terms):</span>
                    <span className="font-semibold text-blue-700">{invoice.deliveryTerms}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="font-semibold text-slate-800">Thanh toán (Payment):</span>
                    <span className="font-medium text-slate-800">{invoice.paymentTerms}</span>
                  </div>
                  <div className="flex justify-between"><span className="font-semibold text-slate-800">Nước đến:</span><span>{invoice.destinationCountry}</span></div>
                </div>
              </div>

              <table className="w-full text-[11px] border-collapse border border-slate-900">
                <thead>
                  <tr className="bg-slate-100 text-slate-900 text-center font-bold">
                    <th className="border border-slate-900 p-1.5 w-10">STT</th>
                    <th className="border border-slate-900 p-1.5 w-32">Mã hàng</th>
                    <th className="border border-slate-900 p-1.5">Mô tả hàng hóa</th>
                    <th className="border border-slate-900 p-1.5 w-16">Số lượng</th>
                    <th className="border border-slate-900 p-1.5 w-12">ĐVT</th>
                    <th className="border border-slate-900 p-1.5 w-20">Đơn giá CMT</th>
                    <th className="border border-slate-900 p-1.5 w-20">Đơn giá DAP</th>
                    <th className="border border-slate-900 p-1.5 w-24">Thành tiền CMT</th>
                    <th className="border border-slate-900 p-1.5 w-24">Thành tiền DAP</th>
                  </tr>
                </thead>
                <tbody>
                  {invoice.items.map((item: InvoicePreviewItem, idx: number) => (
                    <tr key={idx}>
                      <td className="border border-slate-900 p-1.5 text-center font-mono">{item.lineNo}</td>
                      <td className="border border-slate-900 p-1.5 font-mono font-semibold">{item.fullItemCode}</td>
                      <td className="border border-slate-900 p-1.5">{item.description}</td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">{format2(item.quantity)}</td>
                      <td className="border border-slate-900 p-1.5 text-center">{item.unit}</td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">{item.unitPriceCMT.toFixed(2)}</td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">{item.unitPriceDAP.toFixed(2)}</td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">
                        {format2(item.quantity * item.unitPriceCMT)}
                      </td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono font-semibold">
                        {format2(item.quantity * item.unitPriceDAP)}
                      </td>
                    </tr>
                  ))}
                  <tr className="bg-slate-100 font-bold text-slate-900">
                    <td colSpan={3} className="border border-slate-900 p-2 text-center uppercase">TỔNG CỘNG / TOTAL:</td>
                    <td className="border border-slate-900 p-2 text-right font-mono text-sm">{format2(invoice.totalQuantity)}</td>
                    <td className="border border-slate-900 p-2 text-center"></td>
                    <td className="border border-slate-900 p-2"></td>
                    <td className="border border-slate-900 p-2"></td>
                    <td className="border border-slate-900 p-2 text-right font-mono">
                      {format2(invoice.totalAmountCMT)}
                    </td>
                    <td className="border border-slate-900 p-2 text-right font-mono text-sm">
                      {format2(invoice.totalAmountDAP)}
                    </td>
                  </tr>
                </tbody>
              </table>

              <div className="mt-3 p-2 bg-slate-50 border border-slate-300 text-xs">
                <span className="font-bold">Bằng chữ: </span>
                <span className="italic font-medium">{invoice.totalAmountDAPInWords}</span>
              </div>
              <div className="mt-6 text-left text-xs font-bold uppercase">{invoice.sellerName}</div>

              <div className="grid grid-cols-2 gap-8 mt-12 pt-6 text-center text-xs">
                <div>
                  <div className="font-bold uppercase">ĐẠI DIỆN BÊN MUA (BUYER)</div>
                  <div className="text-[11px] text-slate-500 italic mt-0.5">Ký tên & ghi rõ họ tên</div>
                  <div className="h-28 flex items-center justify-center text-slate-300 italic text-[11px]">
                    (Chữ ký đại diện bên mua)
                  </div>
                </div>
                <div>
                  <div className="font-bold uppercase">ĐẠI DIỆN BÊN BÁN (SELLER)</div>
                  <div className="text-[11px] text-slate-500 italic mt-0.5">Ký tên & đóng dấu hợp lệ</div>
                  <div className="h-28 flex items-center justify-center text-slate-300 italic text-[11px]">
                    (Chữ ký đại diện bên bán)
                  </div>
                </div>
              </div>
            </div>

            {/* Sheet 2: Packing List */}
            <div
              className="pkl-a4-sheet p-8"
              style={{ fontFamily: 'Arial, "Helvetica Neue", sans-serif' }}
            >
              <div className="text-center pb-4 border-b border-slate-300">
                <h1 className="text-base font-bold uppercase tracking-wide text-slate-900 m-0">
                  {invoice.sellerName}
                </h1>
                <p className="text-xs text-slate-600 mt-1 mb-0">
                  {invoice.sellerAddressLine1}
                </p>
                {invoice.sellerAddressLine2 && <p className="text-xs text-slate-600 m-0">{invoice.sellerAddressLine2}</p>}
              </div>

              <div className="text-center my-6">
                <h2 className="text-xl font-extrabold uppercase text-slate-900 tracking-wider m-0">
                  PACKING LIST
                </h2>
                <div className="text-xs text-slate-500 mt-1 italic">BẢNG KÊ CHI TIẾT ĐÓNG GÓI LÔ HÀNG XUẤT KHẨU</div>
              </div>

              <div className="flex justify-between items-center text-xs border border-slate-400 p-3 mb-5">
                <div>
                  <span className="font-semibold">Số hóa đơn: </span>
                  <span className="font-bold font-mono">{packingList.invoiceNo}</span>
                </div>
                <div>
                  <span className="font-semibold">Hợp đồng: </span>
                  <span className="font-medium">{invoice.contractNo}</span>
                </div>
                <div>
                  <span className="font-semibold">Ngày lập: </span>
                  <span className="font-medium">{invoice.invoiceDate}</span>
                </div>
                <div>
                  <span className="font-semibold">Quy cách: </span>
                  <span className="font-medium">Theo Master Data từng mặt hàng</span>
                </div>
              </div>

              <table className="w-full text-[11px] border-collapse border border-slate-900">
                <thead>
                  <tr className="bg-slate-100 text-slate-900 text-center font-bold">
                    <th className="border border-slate-900 p-1.5 w-20">Dải số kiện</th>
                    <th className="border border-slate-900 p-1.5 w-32">Mã hàng</th>
                    <th className="border border-slate-900 p-1.5">Mô tả hàng hóa</th>
                    <th className="border border-slate-900 p-1.5 w-16">Số lượng</th>
                    <th className="border border-slate-900 p-1.5 w-12">ĐVT</th>
                    <th className="border border-slate-900 p-1.5 w-16">Số kiện</th>
                    <th className="border border-slate-900 p-1.5 w-20">N.W (KGS)</th>
                    <th className="border border-slate-900 p-1.5 w-20">G.W (KGS)</th>
                  </tr>
                </thead>
                <tbody>
                  {packingList.breakdownItems.map((item: PklBreakdownItem, idx: number) => (
                    <tr key={idx}>
                      <td className="border border-slate-900 p-1.5 text-center font-mono font-semibold">{item.cartonRange}</td>
                      <td className="border border-slate-900 p-1.5 font-mono font-semibold">{item.fullItemCode}</td>
                      <td className="border border-slate-900 p-1.5">{item.description}</td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">{format2(item.quantity)}</td>
                      <td className="border border-slate-900 p-1.5 text-center">PRS</td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">{item.cartonCount}</td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">{item.netWeight.toFixed(2)}</td>
                      <td className="border border-slate-900 p-1.5 text-right font-mono">{item.grossWeight.toFixed(2)}</td>
                    </tr>
                  ))}
                  <tr className="bg-slate-100 font-bold text-slate-900">
                    <td colSpan={3} className="border border-slate-900 p-2 text-center uppercase">TỔNG CỘNG / TOTAL:</td>
                    <td className="border border-slate-900 p-2 text-right font-mono text-sm">{format2(packingList.totalQuantity)}</td>
                    <td className="border border-slate-900 p-2 text-center"></td>
                    <td className="border border-slate-900 p-2 text-right font-mono text-sm">{format2(packingList.totalCartons)}</td>
                    <td className="border border-slate-900 p-2 text-right font-mono">{packingList.totalNetWeight.toFixed(2)}</td>
                    <td className="border border-slate-900 p-2 text-right font-mono">{packingList.totalGrossWeight.toFixed(2)}</td>
                  </tr>
                </tbody>
              </table>

              <div className="grid grid-cols-2 gap-8 mt-12 pt-6 text-center text-xs">
                <div>
                  <div className="font-bold uppercase">NGƯỜI LẬP BIỂU (PREPARED BY)</div>
                  <div className="text-[11px] text-slate-500 italic mt-0.5">Ký & ghi rõ họ tên</div>
                  <div className="h-28 flex items-center justify-center text-slate-300 italic text-[11px]">
                    (Chữ ký người lập biểu)
                  </div>
                </div>
                <div>
                  <div className="font-bold uppercase">XÁC NHẬN BÊN BÁN (AUTHORIZED SIGNATURE)</div>
                  <div className="text-[11px] text-slate-500 italic mt-0.5">Ký tên & đóng dấu hợp lệ</div>
                  <div className="h-28 flex items-center justify-center text-slate-300 italic text-[11px]">
                    (Chữ ký & con dấu đại diện)
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </Modal>
  );
};
