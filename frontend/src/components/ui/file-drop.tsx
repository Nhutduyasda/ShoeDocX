import type { RefObject } from "react";
import { UploadCloud, FileSpreadsheet, X } from "lucide-react";
import { Button } from "./button";
export function FileDrop({
  file,
  disabled,
  inputRef,
  onChange,
  onError,
  removeLabel = "Gỡ tệp đã chọn",
}: {
  file: File | null;
  disabled: boolean;
  inputRef: RefObject<HTMLInputElement | null>;
  onChange: (file: File | null) => void;
  onError: (message: string) => void;
  removeLabel?: string;
}) {
  function select(file?: File) {
    if (!file) return;
    if (!file.name.toLowerCase().endsWith(".xlsx")) {
      onError("Chọn tệp Excel có định dạng .xlsx.");
      return;
    }
    if (file.size > 2 * 1024 * 1024) {
      onError(
        "Tệp vượt quá 2 MiB. Tổng mẫu, ví dụ và nguồn vẫn được máy chủ kiểm tra.",
      );
      return;
    }
    onChange(file);
  }
  return (
    <div
      className={`office-upload ${disabled ? "is-disabled" : ""}`}
      onDragOver={(e) => {
        e.preventDefault();
      }}
      onDrop={(e) => {
        e.preventDefault();
        if (!disabled) select(e.dataTransfer.files[0]);
      }}
    >
      <UploadCloud size={30} aria-hidden="true" />
      <strong>Kéo tệp Excel vào đây</strong>
      <p>Hoặc chọn tệp từ máy tính · .xlsx · tối đa 2 MiB</p>
      <label className="office-upload-picker">
        Chọn mẫu Excel
        <input
          ref={inputRef}
          type="file"
          accept=".xlsx"
          aria-label="Mẫu Excel đầu ra"
          disabled={disabled}
          onChange={(e) => select(e.target.files?.[0])}
        />
      </label>
      {file && (
        <div className="office-selected-file">
          <FileSpreadsheet size={20} />
          <span>
            <small>Đã chọn trên máy · chưa tải lên máy chủ</small>
            <strong>{file.name}</strong>
            <small>{(file.size / 1024).toFixed(1)} KiB</small>
          </span>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            disabled={disabled}
            aria-label={removeLabel}
            title={removeLabel}
            onClick={() => {
              onChange(null);
              if (inputRef.current) inputRef.current.value = "";
            }}
          >
            <X size={16} />
          </Button>
        </div>
      )}
    </div>
  );
}
