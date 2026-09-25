import type { OcrDetectedDocument, OcrItem, ProductMaster } from '../types';
import { ProcessType, normalizeProcessType } from '../types';
import { normalizeOcrStyleCode } from './normalizeOcrStyleCode';

export type ReconciliationStatus = 'MATCHED' | 'MISMATCH' | 'NO_REPORTED_TOTAL' | 'MANUALLY_CONFIRMED';

export function getReconciliationStatus(doc: OcrDetectedDocument): ReconciliationStatus {
  if (doc.isManuallyConfirmed && doc.reportedTotal !== doc.calculatedTotal) {
    return 'MANUALLY_CONFIRMED';
  }
  if (doc.reportedTotal === null || doc.reportedTotal === undefined) {
    return 'NO_REPORTED_TOTAL';
  }
  if (doc.reportedTotal === doc.calculatedTotal) {
    return 'MATCHED';
  }
  return 'MISMATCH';
}

export function getReconciliationBadge(status: ReconciliationStatus): { color: string; label: string; description: string } {
  switch (status) {
    case 'MATCHED':
      return { color: 'green', label: 'Khớp', description: 'Tổng trên phiếu khớp chính xác với tổng các dòng hàng.' };
    case 'MANUALLY_CONFIRMED':
      return { color: 'gold', label: 'Đã xác nhận thủ công', description: 'Đã đối chiếu ảnh gốc và xác nhận dòng hàng chính xác dù tổng lệch.' };
    case 'NO_REPORTED_TOTAL':
      return { color: 'default', label: 'Chưa xác định tổng', description: 'Phiếu kho không ghi rõ hoặc OCR không đọc được dòng TỔNG CỘNG.' };
    case 'MISMATCH':
    default:
      return { color: 'red', label: 'Lệch tổng', description: 'Tổng dòng hàng khác với tổng OCR đọc từ phiếu.' };
  }
}

/**
 * Tái làm giàu dữ liệu OCR khi đổi hoặc chọn đối tác (Zero AI Token).
 * Giữ nguyên 100% số lượng thực tế của người dùng, chỉ remap thông tin danh mục Master Data.
 */
export function reEnrichOcrDocumentsForPartner(
  documents: OcrDetectedDocument[],
  partnerId: number | null | undefined,
  products: ProductMaster[]
): OcrDetectedDocument[] {
  // Lọc sản phẩm theo thư mục đối tác nếu có
  const partnerProducts = partnerId
    ? products.filter((p) => p.folderId === partnerId)
    : products;

  const productMap = new Map<string, ProductMaster>();
  for (const p of partnerProducts) {
    productMap.set(p.styleCode.trim().toUpperCase(), p);
  }

  return documents.map((doc) => {
    const enrichedItems: OcrItem[] = doc.items.map((item) => {
      const cleanCode = normalizeOcrStyleCode(item.styleCode);
      const lookupKey = cleanCode.toUpperCase().replace(/\.G$/, '').trim();
      const pm = productMap.get(lookupKey);

      const isGo =
        item.processType === ProcessType.GoKhongMay ||
        item.note?.toUpperCase().includes('GÒ') ||
        item.note?.toUpperCase().includes('GO') ||
        cleanCode.toUpperCase().endsWith('.G');

      const processType = isGo ? ProcessType.GoKhongMay : ProcessType.Standard;
      const cmt = isGo ? (pm?.unitPriceCMT_Go ?? pm?.unitPriceCMT ?? item.unitPriceCMT ?? 0) : (pm?.unitPriceCMT ?? item.unitPriceCMT ?? 0);
      const dap = isGo ? (pm?.unitPriceDAP_Go ?? pm?.unitPriceDAP ?? item.unitPriceDAP ?? 0) : (pm?.unitPriceDAP ?? item.unitPriceDAP ?? 0);

      return {
        ...item,
        styleCode: cleanCode,
        processType: normalizeProcessType(processType),
        unitPriceCMT: cmt,
        unitPriceDAP: dap,
        pairPerCarton: pm?.pairPerCarton || item.pairPerCarton || 12,
        description: pm?.description ?? item.description ?? '',
        unit: pm?.unit ?? item.unit ?? 'đôi',
        isMatched: Boolean(pm),
      };
    });

    const calculatedTotal = enrichedItems.reduce((sum, i) => sum + (i.quantity || 0), 0);
    const discrepancy = doc.reportedTotal !== null && doc.reportedTotal !== undefined ? calculatedTotal - doc.reportedTotal : null;

    // Invalidate manual confirmation if calculated total changed
    const isManuallyConfirmed = doc.isManuallyConfirmed && doc.calculatedTotal === calculatedTotal;

    return {
      ...doc,
      items: enrichedItems,
      calculatedTotal,
      discrepancy,
      isTotalMatched: doc.reportedTotal !== null && doc.reportedTotal !== undefined && doc.reportedTotal === calculatedTotal,
      hasReportedTotal: doc.reportedTotal !== null && doc.reportedTotal !== undefined,
      hasStandardItems: enrichedItems.some((i) => i.processType === ProcessType.Standard),
      hasGoItems: enrichedItems.some((i) => i.processType === ProcessType.GoKhongMay),
      isManuallyConfirmed,
    };
  });
}
