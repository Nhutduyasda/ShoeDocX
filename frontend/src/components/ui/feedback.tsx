import { Component, type ReactNode } from "react";
import { Button } from "./button";
export function PageSkeleton() {
  return (
    <div className="page-skeleton" role="status" aria-label="Đang tải nội dung">
      <div />
      <div />
      <div />
      <span className="sr-only">Đang tải nội dung…</span>
    </div>
  );
}
export class WorkspaceErrorBoundary extends Component<
  { children: ReactNode },
  { failed: boolean }
> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    return this.state.failed ? (
      <section className="workspace-error" role="alert">
        <h1>Không thể hiển thị nội dung</h1>
        <p>
          Thử tải lại trang. Kiểm tra bản nháp đã lưu trước khi nhập lại dữ
          liệu.
        </p>
        <Button onClick={() => window.location.reload()}>Tải lại trang</Button>
      </section>
    ) : (
      this.props.children
    );
  }
}
