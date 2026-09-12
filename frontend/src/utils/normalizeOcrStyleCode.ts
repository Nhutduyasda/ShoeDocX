export const normalizeOcrStyleCode = (rawCode?: string): string => {
  if (!rawCode) return '';
  let trimmed = rawCode.trim();
  let hasGo = false;
  if (trimmed.toUpperCase().endsWith('.G')) {
    hasGo = true;
    trimmed = trimmed.slice(0, -2).trim();
  }
  // Xóa bỏ phần trong ngoặc đơn ở cuối mã: vd "BM5879-464(KM3)" -> "BM5879-464"
  let cleaned = trimmed.replace(/\s*\([^)]*\)$/, '').trim();
  if (hasGo && !cleaned.toUpperCase().endsWith('.G')) {
    cleaned += '.G';
  }
  return cleaned;
};
