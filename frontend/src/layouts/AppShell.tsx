import { useEffect, useState } from "react";
import * as Dialog from "@radix-ui/react-dialog";
import * as Dropdown from "@radix-ui/react-dropdown-menu";
import {
  LayoutDashboard,
  FileSpreadsheet,
  History,
  ScanLine,
  Warehouse,
  Calculator,
  Boxes,
  Database,
  Layers,
  ClipboardCheck,
  Landmark,
  PanelLeftClose,
  PanelLeftOpen,
  Menu,
  ChevronRight,
  LogOut,
  HelpCircle,
  Search,
  X,
  ArrowUpRight,
} from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { getDefaultTabForUser, type NavTabKey } from "../types/auth";
import { requestInvoiceDraftLeave } from "../services/invoiceDraftStore";
import { CheatsheetModal } from "../components/CheatsheetModal";
import { startOnboardingTour } from "../services/tourService";
import { Button } from "../components/ui/button";
import { navigationFor, destinations } from "./navigation";
const icons = {
  overview: LayoutDashboard,
  shipment: FileSpreadsheet,
  history: History,
  ocr: ScanLine,
  warehouse: Warehouse,
  bom: Calculator,
  "material-planning": ClipboardCheck,
  products: Database,
  materials: Boxes,
  "bom-master": Layers,
  settlement: Landmark,
};
export function AppShell({
  currentTab,
  onTabChange,
  children,
}: {
  currentTab: NavTabKey;
  onTabChange: (tab: NavTabKey) => void;
  children: React.ReactNode;
}) {
  const { user, logout } = useAuth();
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem("shoedocx_sidebar_collapsed") === "true";
    } catch {
      return false;
    }
  });
  const [mobile, setMobile] = useState(false),
    [search, setSearch] = useState(false),
    [query, setQuery] = useState(""),
    [help, setHelp] = useState(false);
  const items = navigationFor(user),
    current = destinations.find((item) => item.key === currentTab)!;
  function toggle() {
    setCollapsed((value) => {
      try {
        localStorage.setItem("shoedocx_sidebar_collapsed", String(!value));
      } catch {
        /* Optional preference. */
      }
      return !value;
    });
  }
  useEffect(() => {
    function shortcut(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "b") {
        e.preventDefault();
        toggle();
      }
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setSearch((value) => !value);
      }
    }
    window.addEventListener("keydown", shortcut);
    return () => window.removeEventListener("keydown", shortcut);
  }, []);
  const studio = () =>
    requestInvoiceDraftLeave(() =>
      window.location.assign("/universal-doc-studio"),
    );
  const brand = (
    <button
      className="workspace-brand"
      onClick={() => {
        onTabChange(getDefaultTabForUser(user));
        setMobile(false);
      }}
      aria-label="ShoeDocX — trang mặc định"
    >
      <span className="brand-mark">
        <FileSpreadsheet size={21} />
      </span>
      <span className="brand-copy">
        <strong>ShoeDocX</strong>
        <small>Không gian chứng từ</small>
      </span>
    </button>
  );
  function navigation() {
    return (
      <nav className="workspace-navigation" aria-label="Điều hướng chính">
        {Array.from(new Set(items.map((item) => item.group))).map((group) => (
          <section key={group}>
            <h2>{group}</h2>
            {items
              .filter((item) => item.group === group)
              .map((item) => {
                const Icon = icons[item.key];
                return (
                  <button
                    key={item.key}
                    title={item.label}
                    aria-label={item.label}
                    aria-current={currentTab === item.key ? "page" : undefined}
                    onClick={() => {
                      onTabChange(item.key);
                      setMobile(false);
                    }}
                  >
                    <Icon size={18} aria-hidden="true" />
                    <span>{item.label}</span>
                  </button>
                );
              })}
          </section>
        ))}
        <section>
          <h2>Công cụ văn phòng</h2>
          <button
            onClick={studio}
            title="Trợ lý Excel"
            aria-label="Trợ lý Excel"
          >
            <FileSpreadsheet size={18} />
            <span>Trợ lý Excel</span>
            <ArrowUpRight size={14} />
          </button>
        </section>
      </nav>
    );
  }
  return (
    <div className={`workspace-shell ${collapsed ? "is-collapsed" : ""}`}>
      <a className="skip-link" href="#workspace-main">
        Đến nội dung chính
      </a>
      <aside className="workspace-sidebar">
        {brand}
        {navigation()}
        <div className="sidebar-footer">
          <span className="workspace-avatar">
            {user?.fullName?.charAt(0) || "U"}
          </span>
          <div className="brand-copy">
            <strong>{user?.fullName}</strong>
            <small>{user?.departmentName || user?.department}</small>
          </div>
        </div>
      </aside>
      <div className="workspace-body">
        <header className="workspace-header">
          <Button
            variant="ghost"
            size="icon"
            className="desktop-toggle"
            onClick={toggle}
            aria-label={collapsed ? "Mở rộng menu" : "Thu gọn menu"}
            aria-expanded={!collapsed}
          >
            {collapsed ? <PanelLeftOpen /> : <PanelLeftClose />}
          </Button>
          <Dialog.Root open={mobile} onOpenChange={setMobile}>
            <Dialog.Trigger asChild>
              <Button
                variant="ghost"
                size="icon"
                className="mobile-toggle"
                aria-label="Mở menu điều hướng"
              >
                <Menu />
              </Button>
            </Dialog.Trigger>
            <Dialog.Portal>
              <Dialog.Overlay className="ui-overlay" />
              <Dialog.Content className="mobile-sidebar">
                <Dialog.Title className="sr-only">
                  Điều hướng ShoeDocX
                </Dialog.Title>
                <Dialog.Description className="sr-only">
                  Các phân hệ được cấp quyền cho tài khoản hiện tại.
                </Dialog.Description>
                {brand}
                <Dialog.Close asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="mobile-close"
                    aria-label="Đóng menu"
                  >
                    <X />
                  </Button>
                </Dialog.Close>
                {navigation()}
              </Dialog.Content>
            </Dialog.Portal>
          </Dialog.Root>
          <nav className="workspace-breadcrumb" aria-label="Đường dẫn">
            <span>{current.group}</span>
            <ChevronRight size={14} />
            <strong>{current.label}</strong>
          </nav>
          <div className="header-actions">
            <Button
              variant="outline"
              className="search-trigger"
              onClick={() => setSearch(true)}
              aria-label="Tìm phân hệ"
            >
              <Search size={16} />
              <span>Tìm phân hệ</span>
              <kbd>Ctrl K</kbd>
            </Button>
            <Dropdown.Root>
              <Dropdown.Trigger asChild>
                <Button variant="ghost" size="icon" aria-label="Trợ giúp">
                  <HelpCircle />
                </Button>
              </Dropdown.Trigger>
              <Dropdown.Portal>
                <Dropdown.Content className="ui-menu" align="end">
                  <Dropdown.Item onSelect={() => setHelp(true)}>
                    Tài liệu thao tác nhanh
                  </Dropdown.Item>
                  <Dropdown.Item onSelect={() => startOnboardingTour()}>
                    Hướng dẫn sử dụng
                  </Dropdown.Item>
                </Dropdown.Content>
              </Dropdown.Portal>
            </Dropdown.Root>
            <Dropdown.Root>
              <Dropdown.Trigger asChild>
                <Button
                  variant="ghost"
                  className="account-trigger"
                  aria-label="Menu tài khoản"
                >
                  <span className="workspace-avatar">
                    {user?.fullName?.charAt(0) || "U"}
                  </span>
                  <span>{user?.fullName}</span>
                </Button>
              </Dropdown.Trigger>
              <Dropdown.Portal>
                <Dropdown.Content className="ui-menu" align="end">
                  <Dropdown.Label>
                    <strong>{user?.fullName}</strong>
                    <small>{user?.departmentName || user?.department}</small>
                  </Dropdown.Label>
                  <Dropdown.Separator />
                  <Dropdown.Item onSelect={logout}>
                    <LogOut size={16} />
                    Đăng xuất
                  </Dropdown.Item>
                </Dropdown.Content>
              </Dropdown.Portal>
            </Dropdown.Root>
          </div>
        </header>
        <main id="workspace-main" tabIndex={-1} className="workspace-main">
          {children}
        </main>
        <footer className="workspace-footer">
          <span>ShoeDocX · Không gian làm việc</span>
          <span>Kho · Xuất nhập khẩu · Kế toán</span>
        </footer>
      </div>
      <Dialog.Root open={search} onOpenChange={setSearch}>
        <Dialog.Portal>
          <Dialog.Overlay className="ui-overlay" />
          <Dialog.Content className="command-dialog">
            <Dialog.Title>Tìm phân hệ</Dialog.Title>
            <Dialog.Description>
              Chỉ hiển thị các phân hệ bạn được phép truy cập.
            </Dialog.Description>
            <label className="sr-only" htmlFor="destination-search">
              Tên phân hệ
            </label>
            <input
              id="destination-search"
              placeholder="Nhập tên phân hệ…"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
            <div className="command-results">
              {items
                .filter((item) =>
                  item.label
                    .toLocaleLowerCase("vi")
                    .includes(query.toLocaleLowerCase("vi")),
                )
                .map((item) => (
                  <button
                    key={item.key}
                    onClick={() => {
                      onTabChange(item.key);
                      setSearch(false);
                    }}
                  >
                    {item.label}
                    <ChevronRight size={16} />
                  </button>
                ))}
            </div>
            <Dialog.Close asChild>
              <Button variant="outline">Đóng</Button>
            </Dialog.Close>
          </Dialog.Content>
        </Dialog.Portal>
      </Dialog.Root>
      <CheatsheetModal
        open={help}
        onClose={() => setHelp(false)}
        onStartTour={() => {
          setHelp(false);
          startOnboardingTour();
        }}
      />
    </div>
  );
}
