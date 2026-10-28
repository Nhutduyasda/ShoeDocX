import { ArrowUpRight, FileSpreadsheet } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { navigationFor } from "../layouts/navigation";
import type { NavTabKey } from "../types/auth";
import { requestInvoiceDraftLeave } from "../services/invoiceDraftStore";
export function WorkspaceCards({
  onNavigate,
}: {
  onNavigate: (tab: NavTabKey) => void;
}) {
  const { user } = useAuth();
  return (
    <section className="workspace-launchpad" aria-label="Bắt đầu công việc">
      <div className="launchpad-heading">
        <span className="section-kicker">KHÔNG GIAN LÀM VIỆC</span>
        <h2>Hôm nay bạn cần làm gì?</h2>
        <p>Chọn nghiệp vụ để bắt đầu hoặc tiếp tục công việc đã lưu.</p>
      </div>
      <div className="workspace-card-grid">
        <button
          className="workspace-task-card studio-task"
          onClick={() =>
            requestInvoiceDraftLeave(() =>
              window.location.assign("/universal-doc-studio"),
            )
          }
        >
          <FileSpreadsheet size={23} />
          <ArrowUpRight className="task-arrow" size={18} />
          <strong>Trợ lý Excel văn phòng</strong>
          <span>Chọn mẫu, nhập dữ liệu, rà soát và xuất Excel.</span>
          <small>Tạo chứng từ theo mẫu →</small>
        </button>
        {navigationFor(user)
          .filter((item) => item.key !== "overview")
          .map((item) => (
            <button
              className="workspace-task-card"
              key={item.key}
              onClick={() => onNavigate(item.key)}
            >
              <ArrowUpRight className="task-arrow" size={18} />
              <strong>{item.label}</strong>
              <span>{item.description}</span>
              <small>{item.group}</small>
            </button>
          ))}
      </div>
    </section>
  );
}
