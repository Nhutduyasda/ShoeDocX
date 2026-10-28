import { useState, type FormEvent } from "react";
import {
  ArrowRight,
  FileSpreadsheet,
  Layers,
  ShieldCheck,
  Eye,
  EyeOff,
} from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { Button } from "../components/ui/button";
export function SignIn() {
  const { login } = useAuth();
  const [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [show, setShow] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError("");
    try {
      await login({
        username: String(data.get("username")).trim(),
        password: String(data.get("password")),
        rememberMe: data.get("rememberMe") === "on",
      });
    } catch {
      setError(
        "Không thể đăng nhập. Kiểm tra tài khoản, mật khẩu và kết nối rồi thử lại.",
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="signin">
      <section className="signin-story">
        <a className="workspace-brand" href="/" aria-label="ShoeDocX">
          <span className="brand-mark">
            <FileSpreadsheet />
          </span>
          <strong>ShoeDocX</strong>
        </a>
        <div className="signin-story-body">
          <span className="section-kicker">KHÔNG GIAN LÀM VIỆC CHỨNG TỪ</span>
          <h1>
            Từ dữ liệu đến
            <br />
            chứng từ hoàn chỉnh.
          </h1>
          <p>
            Kết nối nghiệp vụ kho, xuất nhập khẩu và kế toán trong một quy trình
            rõ ràng.
          </p>
          <div className="signin-feature">
            <Layers />
            <div>
              <strong>Mọi nghiệp vụ, một không gian</strong>
              <p>Invoice, Packing List, định mức và quyết toán.</p>
            </div>
          </div>
          <div className="signin-feature">
            <ShieldCheck />
            <div>
              <strong>Kiểm tra trước khi xuất</strong>
              <p>Quyền truy cập và phê duyệt theo tài khoản của bạn.</p>
            </div>
          </div>
        </div>
        <small>ShoeDocX · Quản lý chứng từ doanh nghiệp</small>
      </section>
      <main className="signin-main">
        <form onSubmit={submit} className="signin-form" aria-busy={busy}>
          <span className="section-kicker">CHÀO MỪNG TRỞ LẠI</span>
          <h1>Đăng nhập</h1>
          <p>Tiếp tục công việc trong không gian của bạn.</p>
          <label htmlFor="signin-username">Tên đăng nhập</label>
          <input
            id="signin-username"
            name="username"
            autoComplete="username"
            placeholder="Nhập tên đăng nhập"
            required
            disabled={busy}
          />
          <label htmlFor="signin-password">Mật khẩu</label>
          <div className="password-control">
            <input
              id="signin-password"
              name="password"
              type={show ? "text" : "password"}
              autoComplete="current-password"
              placeholder="Nhập mật khẩu"
              required
              disabled={busy}
            />
            <Button
              type="button"
              variant="ghost"
              size="icon"
              onClick={() => setShow((value) => !value)}
              aria-label={show ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
            >
              {show ? <EyeOff size={18} /> : <Eye size={18} />}
            </Button>
          </div>
          <label className="remember-control">
            <input
              type="checkbox"
              name="rememberMe"
              defaultChecked
              disabled={busy}
            />
            Ghi nhớ đăng nhập
          </label>
          {error && (
            <div className="signin-error" role="alert">
              {error}
            </div>
          )}
          <Button type="submit" className="signin-submit" disabled={busy}>
            {busy ? "Đang đăng nhập…" : "Đăng nhập"}
            <ArrowRight size={17} />
          </Button>
          <p className="signin-note">
            Sử dụng tài khoản nội bộ đã được cấp quyền.
            <br />
            Liên hệ quản trị viên nếu bạn chưa có tài khoản.
          </p>
        </form>
      </main>
    </div>
  );
}
