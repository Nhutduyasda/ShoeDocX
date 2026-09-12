import { driver } from 'driver.js';
import 'driver.js/dist/driver.css';

export const TOUR_STORAGE_KEY = 'has_completed_tour_v1';

export function hasCompletedTour(): boolean {
  try {
    return localStorage.getItem(TOUR_STORAGE_KEY) === 'true';
  } catch {
    return false;
  }
}

export function setTourCompleted(): void {
  try {
    localStorage.setItem(TOUR_STORAGE_KEY, 'true');
  } catch {
    // ignore
  }
}

export function resetTourStatus(): void {
  try {
    localStorage.removeItem(TOUR_STORAGE_KEY);
  } catch {
    // ignore
  }
}

export function startOnboardingTour(options?: { onComplete?: () => void }): void {
  const driverObj = driver({
    showProgress: true,
    animate: true,
    allowClose: true,
    smoothScroll: true,
    stagePadding: 6,
    stageRadius: 10,
    popoverClass: 'shoedocx-tour-popover',
    progressText: 'Bước {{current}} / {{total}}',
    nextBtnText: 'Tiếp tục →',
    prevBtnText: '← Quay lại',
    doneBtnText: 'Hoàn tất ✓',
    steps: [
      {
        element: '#tour-invoice-header',
        popover: {
          title: '1. Số Hóa đơn & Đặt tên File Tự động',
          description:
            'Hệ thống tự động nhảy số hóa đơn và tên file Excel tiếp theo. Bạn có thể gõ trực tiếp số mới vào đây nếu muốn bắt đầu từ số khác.',
          side: 'bottom',
          align: 'start',
        },
      },
      {
        element: '#tour-data-import',
        popover: {
          title: '2. Nhập số liệu Kho siêu tốc',
          description:
            'Không cần gõ tay! Bạn chỉ cần nhấn chụp màn hình Zalo rồi bấm [Ctrl + V] vào trang web, hoặc nhấn [Dán nhanh] để copy trực tiếp bảng từ file Excel của xưởng.',
          side: 'bottom',
          align: 'start',
        },
      },
      {
        element: '#tour-reconciliation-bar',
        popover: {
          title: '3. Tự động Tra giá & Đối soát Số lượng',
          description:
            'Hệ thống tự ráp đơn giá CMT/DAP từ Master Data. Hãy nhìn nhãn màu xanh [Khớp phiếu kho] để chắc chắn số lượng không bị lệch trước khi xuất hàng.',
          side: 'bottom',
          align: 'center',
        },
      },
      {
        element: '#tour-export-button',
        popover: {
          title: '4. Xuất File Chuẩn Hoàn tất',
          description:
            'Nhấn vào đây để tải về file Excel hoàn chỉnh (.xlsx). Nếu đơn có cả hàng Thành hình và Gò không may, hệ thống sẽ hỏi để tự động tách thành 2 file riêng biệt.',
          side: 'left',
          align: 'center',
        },
      },
    ],
    onHighlightStarted: () => {
      if (typeof window !== 'undefined' && window.scrollX !== 0) {
        window.scrollTo({ left: 0 });
      }
    },
    onDestroyed: () => {
      if (typeof window !== 'undefined' && window.scrollX !== 0) {
        window.scrollTo({ left: 0 });
      }
      setTourCompleted();
      options?.onComplete?.();
    },
  });

  driverObj.drive();
}
