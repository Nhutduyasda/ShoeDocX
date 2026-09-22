import type { WarehouseBatchItem } from '../../../types/warehouse';
import type { CreateShipmentItem, ProcessType } from '../../../types';
import type { SubInvoiceAllocation } from '../types/shipmentDispatch';

export function balanceQuantity(quantity: number, invoiceCount: number, cartonSize = 12): number[] {
  if (!Number.isInteger(quantity) || quantity < 0) throw new Error('Quantity must be a non-negative integer.');
  if (!Number.isInteger(invoiceCount) || invoiceCount < 2 || invoiceCount > 10) throw new Error('Invoice count must be between 2 and 10.');
  const fullCartons = Math.floor(quantity / cartonSize);
  const remainder = quantity % cartonSize;
  const baseCartons = Math.floor(fullCartons / invoiceCount);
  const extraCartons = fullCartons % invoiceCount;
  const result = Array.from({ length: invoiceCount }, (_, index) => (baseCartons + (index < extraCartons ? 1 : 0)) * cartonSize);
  result[invoiceCount - 1] += remainder;
  if (result.reduce((sum, value) => sum + value, 0) !== quantity) throw new Error('Auto balance did not preserve quantity.');
  return result;
}

export function autoBalanceItems(items: WarehouseBatchItem[], invoiceCount: number): SubInvoiceAllocation[] {
  const subInvoices = Array.from({ length: invoiceCount }, (_, index) => ({ invoiceSuffixTitle: `INV ${index + 1}`, items: [] as CreateShipmentItem[] }));
  items.forEach((item) => balanceQuantity(item.quantity, invoiceCount).forEach((quantity, index) => {
    if (quantity > 0) subInvoices[index].items.push({ styleCode: normalizeStyle(item.styleCode), processType: item.processType as ProcessType, quantity });
  }));
  return subInvoices;
}

export const normalizeStyle = (value: string) => {
  const code = value.trim().toUpperCase();
  return code.endsWith('.G') ? code.slice(0, -2).trim() : code;
};

export const itemKey = (styleCode: string, processType: number) => `${normalizeStyle(styleCode)}::${processType}`;
