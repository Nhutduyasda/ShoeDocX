import React, { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import {
  Input,
  DatePicker,
  Button,
  Select,
  Tag,
  Modal,
  Drawer,
  message,
  Tooltip,
  Popconfirm,
  AutoComplete,
  Spin,
} from 'antd';
import type { InputRef } from 'antd';
import {
  RocketOutlined,
  SaveOutlined,
  PlusOutlined,
  DeleteOutlined,
  SnippetsOutlined,
  HistoryOutlined,
  FileExcelOutlined,
  ThunderboltOutlined,
  FolderOpenOutlined,
  CheckCircleFilled,
  AppstoreOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { warehouseApi } from '../api/warehouseApi';
import { masterDataFolderApi } from '../api/masterDataFolderApi';
import type {
  WarehouseBatchSummary,
  WarehouseBatchStatus,
  ProductLookupItem,
} from '../types/warehouse';
import type { MasterDataFolder } from '../types';
import { useAuth } from '../contexts/AuthContext';

export type ProcessLockMode = 'ONLY_GO' | 'ONLY_STANDARD' | 'BOTH' | 'NO_PRICE' | 'NEW_CODE';

interface GridRow {
  key: string;
  styleCode: string;
  quantity: number | null;
  processType: number; // 1: Standard (Thành hình), 2: GoKhongMay (Gò không may)
  isPendingReview?: boolean;
  note?: string;
  lockMode?: ProcessLockMode;
  lockReason?: string;
}

const createBlankRow = (processType = 1, lockMode: ProcessLockMode = 'NEW_CODE'): GridRow => ({
  key: 'row_' + Math.random().toString(36).substring(2, 9),
  styleCode: '',
  quantity: null,
  processType,
  isPendingReview: false,
  note: '',
  lockMode,
});

export const WarehousePage: React.FC = () => {
  const { user } = useAuth();

  // Thông tin phiếu xuất kho
  const [batchId, setBatchId] = useState<number | null>(null);
  const [batchNumber, setBatchNumber] = useState<string>('LẦN 14');
  const [exportDate, setExportDate] = useState<Dayjs>(dayjs());
  const [contractNote, setContractNote] = useState<string>('5BUY HD THÀNH HÌNH');
  const [contractFolderId, setContractFolderId] = useState<number | null>(null);
  const [status, setStatus] = useState<WarehouseBatchStatus>('Draft');
  const [submittedAt, setSubmittedAt] = useState<string | null>(null);
  const [shipmentOrderId, setShipmentOrderId] = useState<number | null>(null);

  // Lưới dữ liệu (Fast-Grid)
  const [rows, setRows] = useState<GridRow[]>(() => [
    createBlankRow(1),
    createBlankRow(1),
    createBlankRow(1),
    createBlankRow(1),
    createBlankRow(1),
  ]);

  // Danh mục thư mục & Sản phẩm của hợp đồng hiện tại
  const [folders, setFolders] = useState<MasterDataFolder[]>([]);
  const [folderProducts, setFolderProducts] = useState<ProductLookupItem[]>([]);
  const [loadingProducts, setLoadingProducts] = useState<boolean>(false);
  const [productPickerVisible, setProductPickerVisible] = useState<boolean>(false);

  // Cache sản phẩm đã tra cứu để nhận diện tức thì và đối chiếu giá
  const productCacheRef = useRef<Map<string, ProductLookupItem>>(new Map());

  // Lịch sử phiếu
  const [historyDrawerVisible, setHistoryDrawerVisible] = useState<boolean>(false);
  const [historyBatches, setHistoryBatches] = useState<WarehouseBatchSummary[]>([]);
  const [loadingHistory, setLoadingHistory] = useState<boolean>(false);

  // Modal Dán từ Excel
  const [pasteModalVisible, setPasteModalVisible] = useState<boolean>(false);
  const [pasteContent, setPasteContent] = useState<string>('');

  // Loading states
  const [saving, setSaving] = useState<boolean>(false);
  const [submitting, setSubmitting] = useState<boolean>(false);
  const savingRef = useRef<boolean>(false);
  const submittingRef = useRef<boolean>(false);

  // Autocomplete cache cho mã giày
  const [lookupOptions, setLookupOptions] = useState<{ [rowKey: string]: { value: string; label: React.ReactNode }[] }>({});

  // Input refs để điều khiển con trỏ phím Enter/Tab mượt như Excel
  const inputRefs = useRef<{ [key: string]: InputRef | null }>({});

  // Tập hợp tất cả các mã đã được chọn trong bảng để loại trừ khỏi gợi ý
  const allSelectedCodes = useMemo(() => {
    const set = new Set<string>();
    rows.forEach((r) => {
      const code = (r.styleCode || '').trim().toUpperCase();
      if (code) {
        set.add(code);
        if (code.endsWith('.G')) {
          set.add(code.slice(0, -2).trim());
        }
      }
    });
    return set;
  }, [rows]);

  // Danh mục mã trong modal picker sau khi đã loại trừ các mã đã chọn vào bảng
  const availablePickerProducts = useMemo(() => {
    return folderProducts.filter((p) => {
      const code = p.styleCode.trim().toUpperCase();
      return !allSelectedCodes.has(code) && !allSelectedCodes.has(code + '.G');
    });
  }, [folderProducts, allSelectedCodes]);

  // Lấy tập hợp mã đã chọn ở các dòng khác (để loại trừ trên gợi ý AutoComplete của dòng hiện tại)
  const getOtherSelectedCodes = useCallback(
    (currentIndex: number): Set<string> => {
      const set = new Set<string>();
      rows.forEach((r, idx) => {
        if (idx !== currentIndex) {
          const code = (r.styleCode || '').trim().toUpperCase();
          if (code) {
            set.add(code);
            if (code.endsWith('.G')) {
              set.add(code.slice(0, -2).trim());
            }
          }
        }
      });
      return set;
    },
    [rows]
  );

  // Tạo option cho AutoComplete kèm nhãn công đoạn trực quan
  const renderProductOption = useCallback((p: ProductLookupItem) => {
    const hasStd = p.hasStandardPrice ?? ((p.unitPriceCMT ?? 0) > 0 || (p.unitPriceDAP ?? 0) > 0);
    const hasGo =
      p.hasGoPrice ??
      ((p.unitPriceCMT_Go ?? p.unitPriceGoKhongMay ?? 0) > 0 || (p.unitPriceDAP_Go ?? 0) > 0);
    return {
      value: p.styleCode,
      label: (
        <div className="flex justify-between items-center py-1 text-xs">
          <span className="font-bold font-mono text-blue-600">{p.styleCode}</span>
          <div className="flex items-center space-x-1">
            {hasGo && !hasStd && (
              <Tag color="orange" className="text-[10px] m-0 font-semibold">
                Gò
              </Tag>
            )}
            {hasStd && !hasGo && (
              <Tag color="blue" className="text-[10px] m-0">
                Thành hình
              </Tag>
            )}
            {hasStd && hasGo && (
              <Tag color="purple" className="text-[10px] m-0">
                Cả 2
              </Tag>
            )}
            <span className="text-slate-500 text-[11px] truncate max-w-[180px]">
              {p.description || p.customer || ''}
            </span>
          </div>
        </div>
      ),
    };
  }, []);

  // Tra cứu sản phẩm trong cache hoặc danh mục hợp đồng
  const findCachedProduct = useCallback(
    (code: string): ProductLookupItem | undefined => {
      const upper = (code || '').trim().toUpperCase();
      if (!upper) return undefined;
      if (productCacheRef.current.has(upper)) return productCacheRef.current.get(upper);
      if (upper.endsWith('.G')) {
        const base = upper.slice(0, -2).trim();
        if (productCacheRef.current.has(base)) return productCacheRef.current.get(base);
      }
      return folderProducts.find((p) => {
        const pCode = p.styleCode.trim().toUpperCase();
        return pCode === upper || (upper.endsWith('.G') && pCode === upper.slice(0, -2).trim());
      });
    },
    [folderProducts]
  );

  // Suy luận công đoạn (Thành hình vs Gò không may) và trạng thái khóa dựa trên Master Data
  const resolveRowConfig = useCallback(
    (
      styleCode: string,
      matched?: ProductLookupItem,
      noteText: string = contractNote
    ): { processType: number; lockMode: ProcessLockMode; lockReason?: string; isPendingReview: boolean } => {
      const upper = (styleCode || '').trim().toUpperCase();
      if (!upper) {
        return {
          processType: 1,
          lockMode: 'NEW_CODE',
          isPendingReview: false,
        };
      }

      // 1. Nếu mã có hậu tố .G -> 100% Gò không may (khóa cứng)
      if (upper.endsWith('.G')) {
        return {
          processType: 2,
          lockMode: 'ONLY_GO',
          lockReason: 'Mã giày có hậu tố .G quy ước cho hàng Gò không may',
          isPendingReview: !matched,
        };
      }

      // 2. Tra cứu đối chiếu Master Data
      const product = matched || findCachedProduct(upper);
      if (product) {
        const hasStandard =
          product.hasStandardPrice ?? ((product.unitPriceCMT ?? 0) > 0 || (product.unitPriceDAP ?? 0) > 0);
        const hasGo =
          product.hasGoPrice ??
          ((product.unitPriceCMT_Go ?? product.unitPriceGoKhongMay ?? 0) > 0 || (product.unitPriceDAP_Go ?? 0) > 0);

        if (!hasStandard && hasGo) {
          return {
            processType: 2,
            lockMode: 'ONLY_GO',
            lockReason: 'Master Data chỉ có đơn giá Gò không may cho mã này (Cố định)',
            isPendingReview: false,
          };
        }

        if (hasStandard && !hasGo) {
          return {
            processType: 1,
            lockMode: 'ONLY_STANDARD',
            lockReason: 'Master Data chỉ có đơn giá Thành hình cho mã này (Cố định)',
            isPendingReview: false,
          };
        }

        if (hasStandard && hasGo) {
          const noteUpper = (noteText || '').toUpperCase();
          const preferGo = noteUpper.includes('GÒ') || noteUpper.includes('GO');
          return {
            processType: preferGo ? 2 : 1,
            lockMode: 'BOTH',
            lockReason: 'Mã này có cả đơn giá Thành hình và Gò không may trong Master Data',
            isPendingReview: false,
          };
        }

        return {
          processType: 1,
          lockMode: 'NO_PRICE',
          lockReason: 'Mã đã có trong Master Data nhưng chưa cấu hình đơn giá',
          isPendingReview: false,
        };
      }

      // 3. Không có trong Master Data (Mã mới)
      return {
        processType: 1,
        lockMode: 'NEW_CODE',
        lockReason: 'Mã chưa có trong Master Data. Hệ thống sẽ lưu và chuyển XNK đối soát sau.',
        isPendingReview: true,
      };
    },
    [contractNote, findCachedProduct]
  );

  // Tải danh mục hợp đồng & lịch sử khi mở trang
  useEffect(() => {
    masterDataFolderApi.getTree()
      .then((data) => setFolders(data))
      .catch((err) => console.error('Lỗi tải thư mục hợp đồng:', err));

    let isMounted = true;
    warehouseApi.getBatches()
      .then((data) => {
        if (isMounted) setHistoryBatches(data);
      })
      .catch((err) => console.error('Lỗi tải lịch sử lô xuất kho:', err));

    return () => {
      isMounted = false;
    };
  }, []);

  // Tải danh sách mã giày gợi ý khi thay đổi hợp đồng (FolderId)
  const loadProductsForFolder = useCallback(async (folderId: number | null) => {
    setLoadingProducts(true);
    try {
      const items = await warehouseApi.lookupProducts('', folderId);
      setFolderProducts(items);

      // Lưu vào cache
      items.forEach((p: ProductLookupItem) => {
        productCacheRef.current.set(p.styleCode.trim().toUpperCase(), p);
      });

      // Tạo sẵn options gợi ý cho các dòng (sẽ được tự động lọc theo từng dòng khi mở)
      const defaultOptions = items.map(renderProductOption);

      setLookupOptions((prev) => {
        const next = { ...prev };
        rows.forEach((r) => {
          next[r.key] = defaultOptions;
        });
        return next;
      });

      // Tự động rà soát lại các dòng hiện tại để cập nhật lockMode và processType
      setRows((prev) =>
        prev.map((r) => {
          if (!r.styleCode.trim()) return r;
          const matched = items.find((p: ProductLookupItem) => p.styleCode.trim().toUpperCase() === r.styleCode.trim().toUpperCase());
          if (!matched) return r;
          const config = resolveRowConfig(r.styleCode, matched);
          return {
            ...r,
            processType: config.processType,
            lockMode: config.lockMode,
            lockReason: config.lockReason,
            isPendingReview: config.isPendingReview,
          };
        })
      );
    } catch (err) {
      console.error('Lỗi nạp danh sách mã của hợp đồng:', err);
    } finally {
      setLoadingProducts(false);
    }
  }, [rows, resolveRowConfig]);

  // Khi người dùng chọn Thư mục Hợp đồng
  const handleFolderChange = (val: number | null) => {
    setContractFolderId(val);
    const selectedFolder = folders.find((f) => f.id === val);
    if (selectedFolder) {
      // Tự động điền gợi ý ghi chú hợp đồng nếu đang để trống
      if (!contractNote.trim() || contractNote === '5BUY HD THÀNH HÌNH') {
        const autoNote = `${selectedFolder.contractNo || selectedFolder.name} THÀNH HÌNH`.trim();
        setContractNote(autoNote);
      }
    }
    loadProductsForFolder(val);
  };

  // Tải lịch sử các đợt hàng
  const loadHistory = useCallback(async () => {
    setLoadingHistory(true);
    try {
      const data = await warehouseApi.getBatches();
      setHistoryBatches(data);
    } catch (err) {
      console.error('Lỗi tải lịch sử lô xuất kho:', err);
    } finally {
      setLoadingHistory(false);
    }
  }, []);

  // Tính toán tổng số lượng
  const totalQuantity = rows.reduce((sum, r) => sum + (r.quantity || 0), 0);
  const standardQuantity = rows
    .filter((r) => r.processType === 1)
    .reduce((sum, r) => sum + (r.quantity || 0), 0);
  const goKhongMayQuantity = rows
    .filter((r) => r.processType === 2)
    .reduce((sum, r) => sum + (r.quantity || 0), 0);
  const validRowsCount = rows.filter((r) => r.styleCode.trim() && (r.quantity || 0) > 0).length;
  const pendingReviewCount = rows.filter((r) => r.styleCode.trim() && r.isPendingReview).length;
  const isLocked = status !== 'Draft';

  // Xử lý thay đổi mã giày & gợi ý thông minh
  const handleStyleCodeChange = (index: number, val: string) => {
    const upperVal = val.toUpperCase();
    const cachedProduct = findCachedProduct(upperVal);
    const initialConfig = resolveRowConfig(upperVal, cachedProduct);

    setRows((prev) => {
      const next = [...prev];
      next[index] = {
        ...next[index],
        styleCode: upperVal,
        processType: initialConfig.processType,
        lockMode: initialConfig.lockMode,
        lockReason: initialConfig.lockReason,
        isPendingReview: initialConfig.isPendingReview,
      };
      return next;
    });

    // Tìm kiếm tức thời khi gõ
    if (val.trim()) {
      warehouseApi.lookupProducts(val.trim(), contractFolderId)
        .then((items) => {
          items.forEach((p: ProductLookupItem) => {
            productCacheRef.current.set(p.styleCode.trim().toUpperCase(), p);
          });

          const otherCodes = getOtherSelectedCodes(index);
          const options = items
            .filter((p: ProductLookupItem) => {
              const u = p.styleCode.trim().toUpperCase();
              return !otherCodes.has(u) && !otherCodes.has(u + '.G');
            })
            .map(renderProductOption);
          const rowKey = rows[index]?.key;
          if (rowKey) {
            setLookupOptions((prev) => ({ ...prev, [rowKey]: options }));
          }

          // Khớp chính xác nếu tìm thấy sản phẩm trong kết quả tìm kiếm
          const exactMatch = items.find(
            (p: ProductLookupItem) =>
              p.styleCode.trim().toUpperCase() === upperVal ||
              (upperVal.endsWith('.G') && p.styleCode.trim().toUpperCase() === upperVal.slice(0, -2).trim())
          );
          if (exactMatch) {
            const refinedConfig = resolveRowConfig(upperVal, exactMatch);
            setRows((prev) => {
              const next = [...prev];
              if (next[index] && next[index].styleCode === upperVal) {
                next[index] = {
                  ...next[index],
                  processType: refinedConfig.processType,
                  lockMode: refinedConfig.lockMode,
                  lockReason: refinedConfig.lockReason,
                  isPendingReview: refinedConfig.isPendingReview,
                };
              }
              return next;
            });
          }
        })
        .catch(() => {});
    }
  };

  // Khi chọn mã giày từ dropdown gợi ý AutoComplete
  const handleStyleCodeSelect = (index: number, val: string) => {
    const upperVal = val.toUpperCase();
    const cachedProduct = findCachedProduct(upperVal);
    const config = resolveRowConfig(upperVal, cachedProduct);

    setRows((prev) => {
      const next = [...prev];
      next[index] = {
        ...next[index],
        styleCode: upperVal,
        processType: config.processType,
        lockMode: config.lockMode,
        lockReason: config.lockReason,
        isPendingReview: config.isPendingReview,
      };
      return next;
    });
  };

  // Khi focus vào ô mã giày: hiện danh sách mã có sẵn của hợp đồng (đã lọc bỏ các mã đã chọn ở dòng khác)
  const handleStyleCodeFocus = (index: number) => {
    const rowKey = rows[index]?.key;
    if (!rowKey) return;

    const otherCodes = getOtherSelectedCodes(index);

    if (folderProducts.length > 0) {
      const options = folderProducts
        .filter((p: ProductLookupItem) => {
          const u = p.styleCode.trim().toUpperCase();
          return !otherCodes.has(u) && !otherCodes.has(u + '.G');
        })
        .map(renderProductOption);
      setLookupOptions((prev) => ({ ...prev, [rowKey]: options }));
    } else {
      warehouseApi
        .lookupProducts('', contractFolderId)
        .then((items) => {
          setFolderProducts(items);
          items.forEach((p: ProductLookupItem) => {
            productCacheRef.current.set(p.styleCode.trim().toUpperCase(), p);
          });
          const options = items
            .filter((p: ProductLookupItem) => {
              const u = p.styleCode.trim().toUpperCase();
              return !otherCodes.has(u) && !otherCodes.has(u + '.G');
            })
            .map(renderProductOption);
          setLookupOptions((prev) => ({ ...prev, [rowKey]: options }));
        })
        .catch(() => {});
    }
  };

  // Xử lý thay đổi số lượng
  const handleQuantityChange = (index: number, val: string) => {
    const num = parseInt(val.replace(/[^\d]/g, ''), 10);
    setRows((prev) => {
      const next = [...prev];
      next[index] = {
        ...next[index],
        quantity: isNaN(num) ? null : num,
      };
      return next;
    });
  };

  // Đổi loại công đoạn (Thành hình vs Gò không may)
  const handleProcessTypeChange = (index: number, type: number) => {
    setRows((prev) => {
      const next = [...prev];
      next[index] = {
        ...next[index],
        processType: type,
      };
      return next;
    });
  };

  // Đổi ghi chú dòng
  const handleNoteChange = (index: number, val: string) => {
    setRows((prev) => {
      const next = [...prev];
      next[index] = {
        ...next[index],
        note: val,
      };
      return next;
    });
  };

  // Thêm dòng mới
  const addRows = (count = 1, processType = 1) => {
    setRows((prev) => {
      const newItems = Array.from({ length: count }, () => createBlankRow(processType));
      return [...prev, ...newItems];
    });
  };

  // Xóa dòng
  const deleteRow = (index: number) => {
    setRows((prev) => {
      if (prev.length <= 1) {
        return [createBlankRow()];
      }
      return prev.filter((_, i) => i !== index);
    });
  };

  // Xóa trắng bảng tạo mới đợt xuất kho
  const handleCreateNewBatch = useCallback(() => {
    setBatchId(null);
    setStatus('Draft');
    setSubmittedAt(null);
    setShipmentOrderId(null);
    setExportDate(dayjs());
    const nextNum = 'LẦN ' + (historyBatches.length + 1);
    setBatchNumber(nextNum);
    setFolderProducts([]);
    setRows([
      createBlankRow(1),
      createBlankRow(1),
      createBlankRow(1),
      createBlankRow(1),
      createBlankRow(1),
    ]);
    message.success(`Đã làm sạch bảng để tạo đợt mới (${nextNum})! Bạn có thể bắt đầu nhập liệu.`);
  }, [historyBatches.length]);

  const resetForm = handleCreateNewBatch;

  // Chọn nhanh mã từ Modal Hợp đồng
  const handlePickProduct = (product: ProductLookupItem) => {
    productCacheRef.current.set(product.styleCode.trim().toUpperCase(), product);
    const config = resolveRowConfig(product.styleCode, product);

    // Tìm dòng trống đầu tiên hoặc thêm mới
    const emptyIndex = rows.findIndex((r) => !r.styleCode.trim());
    if (emptyIndex !== -1) {
      setRows((prev) => {
        const next = [...prev];
        next[emptyIndex] = {
          ...next[emptyIndex],
          styleCode: product.styleCode,
          quantity: next[emptyIndex].quantity || 12,
          processType: config.processType,
          lockMode: config.lockMode,
          lockReason: config.lockReason,
          isPendingReview: config.isPendingReview,
        };
        return next;
      });
    } else {
      setRows((prev) => [
        ...prev,
        {
          key: 'picked_' + Math.random().toString(36).substring(2, 9),
          styleCode: product.styleCode,
          quantity: 12,
          processType: config.processType,
          lockMode: config.lockMode,
          lockReason: config.lockReason,
          isPendingReview: config.isPendingReview,
          note: '',
        },
      ]);
    }
    message.success(
      `Đã thêm mã ${product.styleCode} (${config.processType === 2 ? 'Gò không may' : 'Thành hình'}) vào bảng!`
    );
  };

  // Bấm Enter ở ô Mã giày -> nhảy sang ô Số lượng
  const handleStyleCodeKeyDown = (e: React.KeyboardEvent<HTMLInputElement>, index: number) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      const qtyInput = inputRefs.current[`qty_${index}`];
      if (qtyInput) {
        qtyInput.focus();
        qtyInput.input?.select();
      }
    } else if (e.key === 'ArrowDown' && index < rows.length - 1) {
      e.preventDefault();
      inputRefs.current[`style_${index + 1}`]?.focus();
    } else if (e.key === 'ArrowUp' && index > 0) {
      e.preventDefault();
      inputRefs.current[`style_${index - 1}`]?.focus();
    }
  };

  // Bấm Enter ở ô Số lượng -> nhảy sang dòng tiếp theo (tự thêm dòng mới nếu hết)
  const handleQuantityKeyDown = (e: React.KeyboardEvent<HTMLInputElement>, index: number) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      if (index === rows.length - 1) {
        const currentType = rows[index].processType;
        const newRow = createBlankRow(currentType);
        setRows((prev) => [...prev, newRow]);
        setTimeout(() => {
          inputRefs.current[`style_${index + 1}`]?.focus();
        }, 40);
      } else {
        inputRefs.current[`style_${index + 1}`]?.focus();
      }
    } else if (e.key === 'ArrowDown' && index < rows.length - 1) {
      e.preventDefault();
      inputRefs.current[`qty_${index + 1}`]?.focus();
    } else if (e.key === 'ArrowUp' && index > 0) {
      e.preventDefault();
      inputRefs.current[`qty_${index - 1}`]?.focus();
    }
  };

  // Xử lý dán dữ liệu từ Clipboard (Ctrl+V) vào lưới
  const handleGridPaste = useCallback((e: React.ClipboardEvent) => {
    const text = e.clipboardData.getData('text');
    if (!text || (!text.includes('\t') && !text.includes('\n'))) return;

    e.preventDefault();

    const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0);
    const parsedRows: GridRow[] = [];

    for (const line of lines) {
      const parts = line.split('\t').map((p) => p.trim());
      if (parts.length >= 2) {
        const rawCode = parts[0].toUpperCase();
        const rawQty = parseInt(parts[1].replace(/[^\d]/g, ''), 10);
        const cachedProd = findCachedProduct(rawCode);
        const config = resolveRowConfig(rawCode, cachedProd);

        let pType = config.processType;
        if (parts[2] && parts[2].toLowerCase().includes('gò')) {
          pType = 2;
        }

        if (rawCode) {
          parsedRows.push({
            key: 'paste_' + Math.random().toString(36).substring(2, 9),
            styleCode: rawCode,
            quantity: isNaN(rawQty) ? null : rawQty,
            processType: pType,
            lockMode: config.lockMode,
            lockReason: config.lockReason,
            isPendingReview: config.isPendingReview,
            note: parts[2] || '',
          });
        }
      }
    }

    if (parsedRows.length > 0) {
      setRows((prev) => {
        const cleanPrev = prev.filter((r) => r.styleCode.trim() || (r.quantity || 0) > 0);
        return [...cleanPrev, ...parsedRows];
      });
      message.success(`Đã tự động nhận diện và dán ${parsedRows.length} dòng từ bảng tính Excel!`);
    }
  }, [findCachedProduct, resolveRowConfig]);

  // Xử lý modal dán từ Excel
  const applyPastedText = () => {
    if (!pasteContent.trim()) {
      message.warning('Vui lòng dán nội dung từ Excel vào ô trước.');
      return;
    }

    const lines = pasteContent.split(/\r?\n/).filter((l) => l.trim().length > 0);
    const parsed: GridRow[] = [];

    for (const line of lines) {
      let parts = line.split('\t');
      if (parts.length < 2) {
        parts = line.split(/[,;]/);
      }
      if (parts.length < 2) {
        parts = line.trim().split(/\s{2,}/);
      }

      const style = parts[0]?.trim().toUpperCase() || '';
      const qtyStr = parts[1]?.trim().replace(/[^\d]/g, '') || '';
      const qty = parseInt(qtyStr, 10);
      const cachedProd = findCachedProduct(style);
      const config = resolveRowConfig(style, cachedProd);

      let pType = config.processType;
      if (parts[2] && parts[2].toLowerCase().includes('gò')) {
        pType = 2;
      }

      if (style) {
        parsed.push({
          key: 'paste_m_' + Math.random().toString(36).substring(2, 9),
          styleCode: style,
          quantity: isNaN(qty) ? null : qty,
          processType: pType,
          lockMode: config.lockMode,
          lockReason: config.lockReason,
          isPendingReview: config.isPendingReview,
          note: parts[2]?.trim() || '',
        });
      }
    }

    if (parsed.length === 0) {
      message.error('Không tìm thấy dòng dữ liệu hợp lệ (Cần ít nhất: Mã giày và Số lượng).');
      return;
    }

    setRows((prev) => {
      const cleanPrev = prev.filter((r) => r.styleCode.trim() || (r.quantity || 0) > 0);
      return [...cleanPrev, ...parsed];
    });

    setPasteContent('');
    setPasteModalVisible(false);
    message.success(`Đã nhập thành công ${parsed.length} mã giày vào bảng!`);
  };

  // Lưu bản nháp (Save Draft)
  const handleSaveDraft = useCallback(async () => {
    if (savingRef.current || isLocked) return;
    const validItems = rows.filter((r) => r.styleCode.trim() && (r.quantity || 0) > 0);
    if (validItems.length === 0) {
      message.warning('Vui lòng nhập ít nhất 1 dòng mã giày và số lượng hợp lệ.');
      return;
    }

    savingRef.current = true;
    setSaving(true);
    try {
      const payload = {
        id: batchId ?? undefined,
        batchNumber: batchNumber.trim(),
        exportDate: exportDate.format('YYYY-MM-DD'),
        contractNote: contractNote.trim(),
        contractFolderId: contractFolderId,
        submitImmediately: false,
        items: validItems.map((r) => ({
          styleCode: r.styleCode.trim(),
          quantity: r.quantity || 0,
          processType: r.processType,
          note: r.note,
        })),
      };

      const result = await warehouseApi.saveBatch(payload);
      setBatchId(result.id);
      setStatus(result.status || 'Draft');
      message.success(`Đã lưu bản nháp thành công! (Lô: ${result.batchName || batchNumber})`);
      loadHistory();
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Lỗi khi lưu bản nháp.';
      message.error(errorMsg);
    } finally {
      setSaving(false);
      savingRef.current = false;
    }
  }, [rows, batchId, batchNumber, exportDate, contractNote, contractFolderId, loadHistory, isLocked]);

  // Bàn giao cho XNK (Submit to XNK) với chốt chặn an toàn Validation Guard
  const handleSubmitToXnk = async () => {
    if (submittingRef.current || isLocked) return;
    const validItems = rows.filter((r) => r.styleCode.trim() && (r.quantity || 0) > 0);
    if (validItems.length === 0) {
      message.warning('Phiếu xuất kho chưa có dòng mặt hàng nào có số lượng > 0.');
      return;
    }

    // Kiểm tra tính nhất quán giữa công đoạn và Master Data
    const mismatchedRows: string[] = [];
    validItems.forEach((r, idx) => {
      const prod = findCachedProduct(r.styleCode);
      if (prod) {
        const hasStd = prod.hasStandardPrice ?? ((prod.unitPriceCMT ?? 0) > 0 || (prod.unitPriceDAP ?? 0) > 0);
        const hasGo =
          prod.hasGoPrice ??
          ((prod.unitPriceCMT_Go ?? prod.unitPriceGoKhongMay ?? 0) > 0 || (prod.unitPriceDAP_Go ?? 0) > 0);

        if (r.processType === 1 && !hasStd && hasGo) {
          mismatchedRows.push(`Dòng ${idx + 1} (${r.styleCode}): Master Data chỉ có giá Gò không may nhưng đang chọn Thành hình`);
        } else if (r.processType === 2 && hasStd && !hasGo) {
          mismatchedRows.push(`Dòng ${idx + 1} (${r.styleCode}): Master Data chỉ có giá Thành hình nhưng đang chọn Gò không may`);
        }
      }
    });

    const executeSubmit = async () => {
      submittingRef.current = true;
      setSubmitting(true);
      try {
        const payload = {
          id: batchId ?? undefined,
          batchNumber: batchNumber.trim(),
          exportDate: exportDate.format('YYYY-MM-DD'),
          contractNote: contractNote.trim(),
          contractFolderId: contractFolderId,
          submitImmediately: true,
          items: validItems.map((r) => {
            // Tự động bảo toàn công đoạn nếu phát hiện sản phẩm chỉ có 1 loại giá
            let finalType = r.processType;
            const prod = findCachedProduct(r.styleCode);
            if (prod) {
              const hasStd = prod.hasStandardPrice ?? ((prod.unitPriceCMT ?? 0) > 0 || (prod.unitPriceDAP ?? 0) > 0);
              const hasGo =
                prod.hasGoPrice ??
                ((prod.unitPriceCMT_Go ?? prod.unitPriceGoKhongMay ?? 0) > 0 || (prod.unitPriceDAP_Go ?? 0) > 0);
              if (!hasStd && hasGo) finalType = 2;
              else if (hasStd && !hasGo) finalType = 1;
            }
            return {
              styleCode: r.styleCode.trim(),
              quantity: r.quantity || 0,
              processType: finalType,
              note: r.note,
            };
          }),
        };

        const result = await warehouseApi.saveBatch(payload);
        setBatchId(result.id);
        setStatus('SubmittedToXnk');
        setSubmittedAt(result.submittedAt || new Date().toISOString());
        loadHistory();

        Modal.success({
          title: 'Bàn giao cho XNK thành công!',
          content: (
            <div className="space-y-2 mt-2 text-sm text-slate-700">
              <div>
                Đã bàn giao lô <strong>{batchNumber}</strong> gồm <strong>{validItems.length} mã giày</strong> với tổng số{' '}
                <strong className="text-emerald-600">{totalQuantity.toLocaleString()} đôi</strong> cho phòng XNK.
              </div>
              <div className="text-xs text-slate-500 bg-slate-50 p-2.5 rounded border border-slate-200">
                ℹ️ Bảng dữ liệu đợt này đã được chuyển sang trạng thái <strong>[Đã bàn giao XNK]</strong> và khóa để chống chỉnh sửa trùng lặp. Đơn giá và công đoạn đã được đồng bộ chuẩn xác với Master Data.
              </div>
            </div>
          ),
          okText: '➕ Tạo đợt xuất kho mới ngay',
          cancelText: 'Xem lại đợt vừa giao',
          okCancel: true,
          onOk: () => {
            handleCreateNewBatch();
          },
        });
      } catch (err: unknown) {
        const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Lỗi khi bàn giao lô hàng cho XNK.';
        message.error(errorMsg);
      } finally {
        setSubmitting(false);
        submittingRef.current = false;
      }
    };

    if (mismatchedRows.length > 0) {
      Modal.confirm({
        title: 'Phát hiện mã có thể lệch đơn giá so với Master Data!',
        content: (
          <div className="space-y-2 text-xs text-slate-700">
            <p className="text-amber-700 font-semibold">
              Các mã sau đây không khớp cấu hình đơn giá khai báo trong Master Data:
            </p>
            <ul className="list-disc pl-4 space-y-1 text-slate-600">
              {mismatchedRows.map((msg, i) => (
                <li key={i}>{msg}</li>
              ))}
            </ul>
            <p className="text-slate-500 mt-2">
              Hệ thống sẽ tự động hiệu chỉnh về đúng công đoạn có đơn giá để bảo toàn chứng từ cho XNK. Bạn có muốn tiếp tục bàn giao?
            </p>
          </div>
        ),
        okText: 'Tự động sửa & Bàn giao ngay',
        cancelText: 'Xem lại',
        onOk: () => executeSubmit(),
      });
      return;
    }

    await executeSubmit();
  };

  // Nạp 1 lô từ lịch sử vào Fast-Grid
  const loadBatchToGrid = async (id: number) => {
    try {
      const batch = await warehouseApi.getBatchById(id);
      setBatchId(batch.id);
      setBatchNumber(batch.batchNumber);
      setExportDate(dayjs(batch.exportDate));
      setContractNote(batch.contractNote);
      setContractFolderId(batch.contractFolderId || null);
      setStatus(batch.status);
      setSubmittedAt(batch.submittedAt || null);
      setShipmentOrderId(batch.shipmentOrderId || null);

      if (batch.contractFolderId) {
        loadProductsForFolder(batch.contractFolderId);
      }

      if (batch.items && batch.items.length > 0) {
        setRows(
          batch.items.map((item) => {
            const rawProcess = String(item.processType || '');
            const rawCode = (item.styleCode || '').trim().toUpperCase();
            const isGo =
              (item.processType as unknown) === 2 ||
              rawProcess === 'GoKhongMay' ||
              rawProcess.toLowerCase().includes('go') ||
              rawCode.endsWith('.G');
            const cfg = resolveRowConfig(item.styleCode);
            return {
              key: 'item_' + item.id,
              styleCode: item.styleCode,
              quantity: item.quantity,
              processType: isGo ? 2 : (cfg.lockMode === 'ONLY_GO' ? 2 : 1),
              lockMode: cfg.lockMode,
              lockReason: cfg.lockReason,
              isPendingReview: item.isPendingReview,
              note: item.note || '',
            };
          })
        );
      } else {
        setRows([createBlankRow()]);
      }

      setHistoryDrawerVisible(false);
      message.success(`Đã nạp lô xuất kho: ${batch.batchName}`);
    } catch (err) {
      console.error('Lỗi nạp lô xuất kho:', err);
      message.error('Không thể nạp dữ liệu lô xuất kho.');
    }
  };

  // Xóa lô hàng trong lịch sử
  const handleDeleteBatch = async (id: number, e?: React.MouseEvent) => {
    e?.stopPropagation();
    try {
      await warehouseApi.deleteBatch(id);
      message.success('Đã xóa lô hàng thành công.');
      if (batchId === id) {
        resetForm();
      }
      loadHistory();
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Không thể xóa lô hàng.';
      message.error(errorMsg);
    }
  };

  // Lắng nghe phím tắt Ctrl+S
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
        e.preventDefault();
        handleSaveDraft();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [handleSaveDraft]);

  return (
    <div className="space-y-6 max-w-[1550px] mx-auto pb-12" onPaste={handleGridPaste}>
      {/* 1. Page Header (Đồng bộ chuẩn phong cách giao diện ShoeDocX) */}
      <div className="flex justify-between items-start flex-wrap gap-4 pb-4 border-b border-slate-200">
        <div>
          <div className="flex items-center space-x-2">
            <h1 className="text-2xl font-bold text-slate-900 tracking-tight m-0">
              Lưới xuất kho thành phẩm (成品鞋出货交接单)
            </h1>
            {status === 'Draft' && (
              <Tag className="border-slate-300 text-slate-600 bg-slate-100 text-xs font-semibold px-2 py-0.5 m-0">
                📝 Bản nháp (草稿)
              </Tag>
            )}
            {status === 'SubmittedToXnk' && (
              <Tag className="border-blue-300 text-blue-700 bg-blue-50 text-xs font-semibold px-2 py-0.5 m-0">
                🚀 Đã bàn giao XNK {submittedAt ? `(${dayjs(submittedAt).format('HH:mm DD/MM')})` : ''}
              </Tag>
            )}
            {status === 'ProcessedByXnk' && (
              <Tag className="border-emerald-300 text-emerald-700 bg-emerald-50 text-xs font-semibold px-2 py-0.5 m-0">
                ✅ XNK đã tiếp nhận {shipmentOrderId ? `(HĐ #${shipmentOrderId})` : ''}
              </Tag>
            )}
          </div>
          <p className="text-sm text-slate-500 mt-1 m-0">
            Thủ kho: <strong className="text-slate-700 font-semibold">{user?.fullName || 'Thủ kho'}</strong> • Nhập liệu nhanh bằng phím Enter • Tự động tô màu Gò không may • Bàn giao 1-click cho XNK
          </p>
        </div>

        {/* Thanh nút bấm chức năng */}
        <div className="flex-shrink-0 flex items-center flex-wrap gap-2">
          <Button
            icon={<HistoryOutlined />}
            onClick={() => {
              loadHistory();
              setHistoryDrawerVisible(true);
            }}
            className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium"
          >
            Lịch sử ({historyBatches.length})
          </Button>

          <Button
            type={isLocked ? 'primary' : 'default'}
            icon={<PlusOutlined />}
            onClick={handleCreateNewBatch}
            className={
              isLocked
                ? 'bg-blue-600 hover:bg-blue-700 text-white font-semibold text-xs h-9 px-3.5 shadow-xs'
                : 'text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium'
            }
          >
            {isLocked ? '➕ Tạo đợt xuất mới' : 'Làm sạch / Tạo mới'}
          </Button>

          <Button
            icon={<SaveOutlined />}
            loading={saving}
            onClick={handleSaveDraft}
            disabled={isLocked || saving || submitting}
            className="text-xs h-9 px-3.5 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium"
          >
            Lưu nháp (Ctrl+S)
          </Button>

          <Popconfirm
            title="Xác nhận bàn giao sang XNK?"
            description={
              <div>
                Bạn chuẩn bị bàn giao lô <strong>{batchNumber}</strong> với{' '}
                <strong className="text-emerald-600">{totalQuantity.toLocaleString()} đôi</strong> cho bộ phận XNK.
                {pendingReviewCount > 0 && (
                  <div className="text-amber-600 text-xs mt-1">
                    ⚠️ Có {pendingReviewCount} mã mới chưa đăng ký trong Master Data (XNK sẽ đối soát sau).
                  </div>
                )}
              </div>
            }
            onConfirm={handleSubmitToXnk}
            okText="Bàn giao ngay"
            cancelText="Hủy"
            okButtonProps={{ className: 'bg-emerald-600' }}
            disabled={isLocked || saving || submitting || validRowsCount === 0}
          >
            <Button
              type="primary"
              icon={<RocketOutlined />}
              loading={submitting}
              disabled={isLocked || saving || submitting || validRowsCount === 0}
              className="bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs h-9 px-4 shadow-sm"
            >
              🚀 BÀN GIAO CHO XNK ({totalQuantity.toLocaleString()} đôi)
            </Button>
          </Popconfirm>
        </div>
      </div>

      {/* Thông báo trạng thái khi lô hàng đã được bàn giao hoặc xử lý */}
      {status === 'SubmittedToXnk' && (
        <div className="bg-emerald-50 border border-emerald-300 rounded-lg p-4 flex items-center justify-between flex-wrap gap-3 shadow-xs">
          <div className="flex items-center space-x-3">
            <span className="text-2xl">🚀</span>
            <div>
              <div className="text-sm font-bold text-emerald-950">
                Đợt hàng {batchNumber} ĐÃ BÀN GIAO CHO PHÒNG XNK THÀNH CÔNG!
              </div>
              <div className="text-xs text-emerald-700 mt-0.5">
                Bàn giao lúc: {submittedAt ? dayjs(submittedAt).format('HH:mm:ss DD/MM/YYYY') : 'Vừa xong'}. Bảng dữ liệu hiện đang được khóa để bảo vệ tính toàn vẹn và chống trùng lặp.
              </div>
            </div>
          </div>
          <div className="flex items-center space-x-2">
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={handleCreateNewBatch}
              className="bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs h-8 shadow-xs"
            >
              ➕ Tạo đợt xuất kho tiếp theo
            </Button>
            <Button
              icon={<HistoryOutlined />}
              onClick={() => {
                loadHistory();
                setHistoryDrawerVisible(true);
              }}
              className="text-xs h-8 border-emerald-300 text-emerald-800 bg-white hover:bg-emerald-50"
            >
              Xem lịch sử ({historyBatches.length})
            </Button>
          </div>
        </div>
      )}

      {status === 'ProcessedByXnk' && (
        <div className="bg-blue-50 border border-blue-300 rounded-lg p-4 flex items-center justify-between flex-wrap gap-3 shadow-xs">
          <div className="flex items-center space-x-3">
            <span className="text-2xl">✅</span>
            <div>
              <div className="text-sm font-bold text-blue-950">
                Phòng XNK đã tiếp nhận và lập Hóa đơn xuất khẩu cho đợt {batchNumber}!
              </div>
              <div className="text-xs text-blue-700 mt-0.5">
                {shipmentOrderId ? `Đơn hàng liên kết: #${shipmentOrderId}. ` : ''}Dữ liệu đã vào sổ sách kế toán & hải quan.
              </div>
            </div>
          </div>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={handleCreateNewBatch}
            className="bg-blue-600 hover:bg-blue-700 text-white font-semibold text-xs h-8 shadow-xs"
          >
            ➕ Tạo đợt xuất kho mới
          </Button>
        </div>
      )}

      {/* 2. Card Thông tin đợt xuất (Clean Enterprise White Card) */}
      <div className="bg-white border border-slate-200 rounded-lg p-5 space-y-4 shadow-sm">
        <div className="text-xs font-semibold text-slate-800 uppercase tracking-wider pb-2 border-b border-slate-100 flex items-center justify-between">
          <span>1. Thông tin đợt xuất hàng (轮次与合同信息)</span>
          <span className="text-[11px] text-slate-400 font-normal">
            Dấu <span className="text-rose-500">*</span> là thông tin bắt buộc
          </span>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4 text-xs">
          <div>
            <label className="text-slate-700 font-semibold block mb-1.5">
              ĐỢT / SỐ LẦN (批次 / 轮次) <span className="text-rose-500">*</span>
            </label>
            <Input
              value={batchNumber}
              onChange={(e) => setBatchNumber(e.target.value)}
              placeholder="VD: LẦN 14"
              disabled={isLocked}
              className="font-bold text-sm text-slate-900 h-9"
            />
          </div>

          <div>
            <label className="text-slate-700 font-semibold block mb-1.5">
              NGÀY XUẤT ĐI HÀNG (出货日期) <span className="text-rose-500">*</span>
            </label>
            <DatePicker
              value={exportDate}
              onChange={(val) => val && setExportDate(val)}
              format="DD/MM/YYYY"
              disabled={isLocked}
              className="w-full h-9"
            />
          </div>

          <div>
            <label className="text-slate-700 font-semibold block mb-1.5 flex items-center justify-between">
              <span>HỒ SƠ HỢP ĐỒNG / KHÁCH HÀNG</span>
              <span className="text-[10px] text-blue-600 font-normal">Tự động gợi ý mã</span>
            </label>
            <Select
              value={contractFolderId}
              onChange={handleFolderChange}
              placeholder="-- Chọn khách hàng / Hợp đồng --"
              allowClear
              disabled={isLocked}
              className="w-full h-9"
              options={folders.map((f) => ({
                value: f.id,
                label: `${f.name} ${f.customerName ? `(${f.customerName})` : ''}`,
              }))}
            />
          </div>

          <div>
            <label className="text-slate-700 font-semibold block mb-1.5">
              GHI CHÚ HỢP ĐỒNG / LOẠI ĐƠN <span className="text-rose-500">*</span>
            </label>
            <Input
              value={contractNote}
              onChange={(e) => setContractNote(e.target.value)}
              placeholder="VD: 5BUY HD THÀNH HÌNH"
              disabled={isLocked}
              className="font-medium text-slate-800 h-9"
            />
          </div>
        </div>

        {/* Thanh trợ giúp gợi ý mã của Hợp đồng */}
        {contractFolderId && (
          <div className="bg-blue-50/60 border border-blue-200 rounded-lg p-3 flex items-center justify-between flex-wrap gap-2 text-xs">
            <div className="flex items-center space-x-2 text-blue-900">
              <span className="text-base">💡</span>
              <span>
                Hợp đồng đang chọn có <strong>{folderProducts.length} mã giày</strong> trong Master Data. Hệ thống sẽ ưu tiên gợi ý các mã này khi bạn gõ phím.
              </span>
            </div>
            <Button
              size="small"
              icon={<AppstoreOutlined />}
              onClick={() => setProductPickerVisible(true)}
              className="bg-white border-blue-300 text-blue-700 hover:bg-blue-50 font-medium text-xs h-7"
            >
              Chọn nhanh từ danh sách ({folderProducts.length} mã)
            </Button>
          </div>
        )}
      </div>

      {/* 3. LƯỚI NHẬP LIỆU FAST-GRID (Clean White Table với màu cam/vàng đặc trưng của xưởng) */}
      <div className="bg-white border border-slate-200 rounded-lg p-5 space-y-4 shadow-sm">
        <div className="flex justify-between items-center flex-wrap gap-3 pb-3 border-b border-slate-100">
          <div className="text-xs font-semibold text-slate-800 uppercase tracking-wider">
            2. Danh sách Hàng xuất kho ({validRowsCount} mã hợp lệ • Tổng: <span className="text-emerald-600 font-bold">{totalQuantity.toLocaleString()} đôi</span>)
          </div>

          <div className="flex-shrink-0 flex items-center flex-wrap gap-2">
            <Button
              size="small"
              icon={<PlusOutlined />}
              onClick={() => addRows(1, 1)}
              disabled={isLocked}
              className="text-xs h-8 px-3 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium disabled:opacity-50"
            >
              + 1 dòng
            </Button>
            <Button
              size="small"
              icon={<PlusOutlined />}
              onClick={() => addRows(5, 1)}
              disabled={isLocked}
              className="text-xs h-8 px-3 border-slate-300 text-slate-700 hover:bg-slate-50 font-medium disabled:opacity-50"
            >
              + 5 dòng (加5行)
            </Button>
            <Button
              size="small"
              icon={<PlusOutlined />}
              onClick={() => addRows(1, 2)}
              disabled={isLocked}
              className="text-xs h-8 px-3 border-orange-300 text-orange-800 bg-orange-50 hover:bg-orange-100 font-medium disabled:opacity-50"
            >
              + 1 dòng Gò không may
            </Button>
            <Button
              size="small"
              icon={<SnippetsOutlined />}
              onClick={() => setPasteModalVisible(true)}
              disabled={isLocked}
              className="text-xs h-8 px-3 border-emerald-300 text-emerald-800 bg-emerald-50 hover:bg-emerald-100 font-medium disabled:opacity-50"
            >
              📋 Dán từ Excel (Clipboard)
            </Button>
          </div>
        </div>

        {/* Bảng dữ liệu Fast-Grid */}
        <div className="border border-slate-200 rounded-lg overflow-x-auto shadow-sm">
          <table className="w-full text-left border-collapse min-w-[950px]">
            {/* Table Header */}
            <thead>
              <tr className="bg-slate-50 text-slate-700 text-xs uppercase tracking-wider font-semibold border-b border-slate-200 select-none">
                <th className="py-3 px-3 w-14 text-center border-r border-slate-200">
                  STT<br/><span className="text-[10px] text-slate-400 font-normal">序号</span>
                </th>
                <th className="py-3 px-4 border-r border-slate-200">
                  HÌNH THỂ / MÃ GIÀY<br/><span className="text-[10px] text-slate-400 font-normal">鞋型 (Style Code)</span>
                </th>
                <th className="py-3 px-4 w-52 border-r border-slate-200">
                  CÔNG ĐOẠN<br/><span className="text-[10px] text-slate-400 font-normal">工序 (Process Type)</span>
                </th>
                <th className="py-3 px-4 w-48 text-right border-r border-slate-200">
                  SỐ LƯỢNG ĐI HÀNG<br/><span className="text-[10px] text-slate-400 font-normal">交货数量 (Đôi / Pairs)</span>
                </th>
                <th className="py-3 px-4 border-r border-slate-200">
                  GHI CHÚ<br/><span className="text-[10px] text-slate-400 font-normal">备注 (Note)</span>
                </th>
                <th className="py-3 px-2 w-16 text-center">
                  XÓA<br/><span className="text-[10px] text-slate-400 font-normal">操作</span>
                </th>
              </tr>
            </thead>

            {/* Table Body */}
            <tbody className="divide-y divide-slate-100 text-sm">
              {rows.map((row, index) => {
                const isGo = row.processType === 2;
                return (
                  <tr
                    key={row.key}
                    className={`transition-colors duration-150 ${
                      isGo
                        ? 'bg-orange-50/80 hover:bg-orange-100/70 border-l-4 border-l-orange-500'
                        : 'bg-white hover:bg-slate-50/80'
                    }`}
                  >
                    {/* STT */}
                    <td
                      className={`py-2 px-3 text-center border-r select-none text-xs font-mono ${
                        isGo ? 'border-orange-200 text-orange-800 font-bold' : 'border-slate-200 text-slate-400'
                      }`}
                    >
                      {index + 1}
                    </td>

                    {/* Mã Giày (Style Code) */}
                    <td className={`py-1.5 px-3 border-r ${isGo ? 'border-orange-200' : 'border-slate-200'}`}>
                      {(() => {
                        const otherCodes = getOtherSelectedCodes(index);
                        const rowOpts = (
                          lookupOptions[row.key] || folderProducts.map(renderProductOption)
                        ).filter((opt) => {
                          const upper = opt.value.trim().toUpperCase();
                          return !otherCodes.has(upper) && !otherCodes.has(upper + '.G');
                        });
                        return (
                          <div className="relative flex items-center space-x-2">
                            <AutoComplete
                              options={rowOpts}
                              value={row.styleCode}
                              onChange={(val) => handleStyleCodeChange(index, val)}
                              onSelect={(val) => handleStyleCodeSelect(index, val)}
                              onFocus={() => handleStyleCodeFocus(index)}
                              className="w-full"
                              disabled={isLocked}
                            >
                              <Input
                                ref={(el) => {
                                  inputRefs.current[`style_${index}`] = el;
                                }}
                                onKeyDown={(e) => handleStyleCodeKeyDown(e, index)}
                                placeholder="Nhập mã giày (VD: 40700-066)..."
                                disabled={isLocked}
                                className={`font-mono font-bold tracking-wide text-sm h-8 ${
                                  isGo
                                    ? 'bg-orange-50/60 border-orange-300 text-orange-950 placeholder-orange-400'
                                    : 'bg-white border-slate-300 text-slate-900 focus:border-blue-500'
                                }`}
                              />
                            </AutoComplete>

                            {row.isPendingReview && row.styleCode.trim() && (
                              <Tooltip title="Mã này chưa có trong Master Data. Hệ thống vẫn lưu bình thường và chuyển cho XNK đối soát sau.">
                                <Tag color="warning" className="text-[10px] whitespace-nowrap m-0 cursor-help">
                                  ⚠️ Mã mới
                                </Tag>
                              </Tooltip>
                            )}
                          </div>
                        );
                      })()}
                    </td>

                    {/* Công đoạn (Thành hình vs Gò không may) */}
                    <td className={`py-1.5 px-3 border-r ${isGo ? 'border-orange-200' : 'border-slate-200'}`}>
                      <Tooltip title={row.lockReason || (isGo ? 'Hàng Gò không may' : 'Hàng Thành hình')}>
                        <Select
                          value={row.processType}
                          onChange={(val) => handleProcessTypeChange(index, val)}
                          disabled={isLocked || row.lockMode === 'ONLY_GO' || row.lockMode === 'ONLY_STANDARD'}
                          className="w-full"
                          options={[
                            {
                              value: 1,
                              label: (
                                <div className="flex items-center justify-between">
                                  <span>Thành hình (标准)</span>
                                  {row.lockMode === 'ONLY_STANDARD' && (
                                    <span className="text-[10px] text-slate-400 font-normal ml-1">🔒 Khóa</span>
                                  )}
                                </div>
                              ),
                              disabled: row.lockMode === 'ONLY_GO',
                            },
                            {
                              value: 2,
                              label: (
                                <div className="flex items-center justify-between">
                                  <span className="font-semibold text-orange-600">🔶 GÒ KHÔNG MAY (仅成型)</span>
                                  {row.lockMode === 'ONLY_GO' && (
                                    <span className="text-[10px] text-orange-500 font-normal ml-1">🔒 Khóa</span>
                                  )}
                                </div>
                              ),
                              disabled: row.lockMode === 'ONLY_STANDARD',
                            },
                          ]}
                        />
                      </Tooltip>
                      {row.lockMode === 'BOTH' && (
                        <div className="text-[10px] text-indigo-600 mt-0.5 leading-tight font-medium">
                          ✨ Đa hình thức (có cả 2 giá)
                        </div>
                      )}
                      {row.lockMode === 'NO_PRICE' && row.styleCode.trim() && (
                        <div className="text-[10px] text-amber-600 mt-0.5 leading-tight">
                          ⚠️ Chưa có đơn giá Master Data
                        </div>
                      )}
                    </td>

                    {/* Số lượng */}
                    <td className={`py-1.5 px-3 border-r ${isGo ? 'border-orange-200' : 'border-slate-200'}`}>
                      <Input
                        ref={(el) => {
                          inputRefs.current[`qty_${index}`] = el;
                        }}
                        value={row.quantity !== null ? row.quantity.toLocaleString('en-US') : ''}
                        onChange={(e) => handleQuantityChange(index, e.target.value)}
                        onKeyDown={(e) => handleQuantityKeyDown(e, index)}
                        placeholder="Số đôi..."
                        disabled={isLocked}
                        className={`text-right font-mono font-bold text-sm h-8 ${
                          isGo
                            ? 'bg-orange-50/60 border-orange-300 text-orange-950 placeholder-orange-400'
                            : 'bg-white border-slate-300 text-slate-900 focus:border-emerald-500'
                        }`}
                      />
                    </td>

                    {/* Ghi chú */}
                    <td className={`py-1.5 px-3 border-r ${isGo ? 'border-orange-200' : 'border-slate-200'}`}>
                      <Input
                        value={row.note || ''}
                        onChange={(e) => handleNoteChange(index, e.target.value)}
                        placeholder="Ghi chú (KM3, Đợt 2...)"
                        disabled={isLocked}
                        className={`text-xs h-8 ${
                          isGo ? 'bg-orange-50/60 border-orange-300' : 'bg-white border-slate-300'
                        }`}
                      />
                    </td>

                    {/* Xóa dòng */}
                    <td className="py-1.5 px-2 text-center">
                      <Button
                        type="text"
                        size="small"
                        icon={<DeleteOutlined className="text-xs" />}
                        onClick={() => deleteRow(index)}
                        disabled={isLocked}
                        className="text-slate-400 hover:text-rose-600 disabled:opacity-30"
                      />
                    </td>
                  </tr>
                );
              })}
            </tbody>

            {/* 4. Chân bảng TỔNG CỘNG 共计 (Màu vàng sáng y hệt thực tế xưởng) */}
            <tfoot>
              <tr className="bg-yellow-200 text-yellow-950 font-bold border-t-2 border-yellow-400 select-none">
                <td colSpan={3} className="py-3 px-4 text-xs uppercase tracking-wide border-r border-yellow-300">
                  <div className="flex items-center space-x-2">
                    <span className="text-base">📊</span>
                    <span className="font-extrabold text-sm">TỔNG CỘNG 共计</span>
                    <span className="text-xs font-normal text-yellow-900">
                      ({validRowsCount} mã hợp lệ | Thành hình: {standardQuantity.toLocaleString()} đôi | Gò không may: {goKhongMayQuantity.toLocaleString()} đôi)
                    </span>
                  </div>
                </td>
                <td className="py-3 px-4 text-right font-mono text-base font-black border-r border-yellow-300">
                  {totalQuantity.toLocaleString()} đôi
                </td>
                <td colSpan={2} className="py-3 px-4 text-xs font-normal text-yellow-900">
                  {pendingReviewCount > 0 ? (
                    <span className="text-amber-900 font-bold">
                      ⚠️ {pendingReviewCount} mã tạm chờ XNK duyệt
                    </span>
                  ) : (
                    <span className="text-emerald-900 font-semibold inline-flex items-center gap-1">
                      <CheckCircleFilled className="text-emerald-700" /> Đầy đủ thông tin
                    </span>
                  )}
                </td>
              </tr>
            </tfoot>
          </table>
        </div>

        {/* Hướng dẫn thao tác nhanh cho thủ kho */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs text-slate-500 bg-slate-50 p-3 rounded-lg border border-slate-200">
          <div className="flex items-start space-x-2">
            <ThunderboltOutlined className="text-amber-500 text-sm mt-0.5" />
            <div>
              <strong className="text-slate-800">Gõ phím Enter siêu tốc:</strong> Nhập mã giày → nhấn <code>Enter</code> nhảy sang số lượng → nhấn <code>Enter</code> tự thêm dòng mới.
            </div>
          </div>
          <div className="flex items-start space-x-2">
            <FileExcelOutlined className="text-emerald-600 text-sm mt-0.5" />
            <div>
              <strong className="text-slate-800">Dán từ Excel (Ctrl+V):</strong> Copy 2 cột (Mã giày, Số lượng) từ Excel/Zalo và nhấn Ctrl+V trực tiếp vào bảng.
            </div>
          </div>
          <div className="flex items-start space-x-2">
            <RocketOutlined className="text-blue-600 text-sm mt-0.5" />
            <div>
              <strong className="text-slate-800">Bàn giao 1-Click:</strong> Bấm <code>BÀN GIAO CHO XNK</code>, hệ thống đẩy dữ liệu sang máy phòng XNK ngay tức khắc.
            </div>
          </div>
        </div>
      </div>

      {/* Modal Chọn nhanh mã từ Hợp đồng */}
      <Modal
        title={
          <div className="flex items-center space-x-2 text-sm font-bold text-slate-800">
            <FolderOpenOutlined className="text-blue-600 text-base" />
            <span>
              Danh mục mã giày của Hợp đồng ({availablePickerProducts.length}/{folderProducts.length} mã khả dụng)
            </span>
          </div>
        }
        open={productPickerVisible}
        onCancel={() => setProductPickerVisible(false)}
        footer={null}
        width={750}
      >
        <div className="space-y-3 py-2">
          <p className="text-xs text-slate-500 m-0">
            Bấm vào bất kỳ mã giày nào bên dưới để chèn nhanh vào bảng phiếu xuất kho (các mã đã có trong bảng sẽ tự động ẩn):
          </p>
          {loadingProducts ? (
            <div className="text-center py-8">
              <Spin tip="Đang tải danh mục mã..." />
            </div>
          ) : availablePickerProducts.length === 0 ? (
            <div className="text-center py-8 text-slate-500 text-xs">
              {folderProducts.length > 0
                ? '✅ Toàn bộ các mã trong hợp đồng này đã được thêm vào bảng xuất kho!'
                : 'Hợp đồng này chưa có mã sản phẩm nào được đăng ký trong Master Data.'}
            </div>
          ) : (
            <div className="max-h-[400px] overflow-y-auto divide-y divide-slate-100 border border-slate-200 rounded-lg">
              {availablePickerProducts.map((p) => (
                <div
                  key={p.id}
                  onClick={() => handlePickProduct(p)}
                  className="p-3 hover:bg-blue-50/60 cursor-pointer transition-colors flex items-center justify-between"
                >
                  <div>
                    <div className="flex items-center space-x-2">
                      <span className="font-bold font-mono text-sm text-blue-700">{p.styleCode}</span>
                      {p.customer && (
                        <Tag className="text-[10px] bg-slate-100 text-slate-600 border-slate-200">
                          {p.customer}
                        </Tag>
                      )}
                      {(p.hasGoPrice && !p.hasStandardPrice) && (
                        <Tag color="orange" className="text-[10px] font-semibold m-0">🔶 Gò không may</Tag>
                      )}
                      {(p.hasStandardPrice && !p.hasGoPrice) && (
                        <Tag color="blue" className="text-[10px] m-0">Thành hình</Tag>
                      )}
                      {(p.hasStandardPrice && p.hasGoPrice) && (
                        <Tag color="purple" className="text-[10px] m-0">✨ Cả 2 giá</Tag>
                      )}
                    </div>
                    <div className="text-xs text-slate-500 mt-0.5">
                      {p.description || 'Không có mô tả'}
                    </div>
                  </div>
                  <div className="text-right">
                    <Button size="small" type="primary" className="bg-blue-600 text-xs">
                      + Thêm vào bảng
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </Modal>

      {/* Modal Dán dữ liệu từ Excel */}
      <Modal
        title="📋 Dán dữ liệu từ bảng Excel / Zalo (批量粘贴)"
        open={pasteModalVisible}
        onCancel={() => setPasteModalVisible(false)}
        onOk={applyPastedText}
        okText="Áp dụng vào bảng"
        cancelText="Hủy"
        width={650}
      >
        <p className="text-xs text-slate-500 mb-2">
          Copy bảng từ file Excel (chứa cột <strong>Mã giày</strong> và <strong>Số lượng</strong>), sau đó dán vào ô bên dưới:
        </p>
        <Input.TextArea
          rows={9}
          value={pasteContent}
          onChange={(e) => setPasteContent(e.target.value)}
          placeholder={`40700-066\t288\n40700-011\t500\nYL3564-100\t120\tGò không may`}
          className="font-mono text-xs"
        />
        <div className="text-[11px] text-slate-400 mt-2">
          Mẹo: Nếu mã có đuôi <code>.G</code> hoặc cột ghi chú có chữ <code>Gò</code>, hệ thống sẽ tự động phân loại thành <strong>Gò không may</strong>.
        </div>
      </Modal>

      {/* Drawer Lịch sử các đợt xuất kho */}
      <Drawer
        title="📜 Lịch sử các đợt xuất kho (交货历史记录)"
        placement="right"
        width={600}
        open={historyDrawerVisible}
        onClose={() => setHistoryDrawerVisible(false)}
      >
        <div className="space-y-3">
          {loadingHistory ? (
            <div className="text-center py-8">
              <Spin tip="Đang tải lịch sử các đợt giao..." />
            </div>
          ) : historyBatches.length === 0 ? (
            <div className="text-center py-8 text-slate-400 text-xs">
              Chưa có đợt xuất kho nào được ghi nhận.
            </div>
          ) : (
            historyBatches.map((b) => (
              <div
                key={b.id}
                onClick={() => loadBatchToGrid(b.id)}
                className={`p-3.5 rounded-lg border cursor-pointer transition-all hover:shadow-sm ${
                  b.id === batchId
                    ? 'border-emerald-500 bg-emerald-50/40'
                    : 'border-slate-200 bg-white hover:border-slate-300'
                }`}
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center space-x-2">
                    <span className="font-extrabold text-sm text-blue-700">{b.batchNumber}</span>
                    <span className="text-xs text-slate-500">({dayjs(b.exportDate).format('DD/MM/YYYY')})</span>
                    {b.status === 'Draft' && <Tag className="text-[10px]">Bản nháp</Tag>}
                    {b.status === 'SubmittedToXnk' && <Tag color="processing" className="text-[10px]">Đã bàn giao XNK</Tag>}
                    {b.status === 'ProcessedByXnk' && <Tag color="success" className="text-[10px]">XNK đã tiếp nhận</Tag>}
                  </div>

                  <div className="flex items-center space-x-2">
                    <span className="font-mono font-bold text-emerald-600 text-sm">
                      {b.totalQuantity.toLocaleString()} đôi
                    </span>
                    {b.status === 'Draft' && (
                      <Popconfirm
                        title="Xóa lô hàng này?"
                        onConfirm={(e) => handleDeleteBatch(b.id, e)}
                        okText="Xóa"
                        cancelText="Hủy"
                      >
                        <Button
                          type="text"
                          danger
                          size="small"
                          icon={<DeleteOutlined />}
                          onClick={(e) => e.stopPropagation()}
                        />
                      </Popconfirm>
                    )}
                  </div>
                </div>

                <div className="text-xs text-slate-700 mt-1 font-medium">
                  {b.contractNote}
                </div>

                <div className="text-[11px] text-slate-400 mt-1 flex justify-between">
                  <span>{b.itemCount} mã hàng (Gò: {b.goCount} | Thành hình: {b.thanhHinhCount})</span>
                  <span>{b.submittedAt ? `Bàn giao: ${dayjs(b.submittedAt).format('HH:mm DD/MM')}` : 'Chưa bàn giao'}</span>
                </div>
              </div>
            ))
          )}
        </div>
      </Drawer>
    </div>
  );
};
