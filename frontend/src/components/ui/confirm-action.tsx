import { useState, type ReactNode } from "react";
import * as Dialog from "@radix-ui/react-dialog";
import { Button } from "./button";
export function ConfirmAction({
  children,
  title,
  description,
  disabled,
  onConfirm,
}: {
  children: ReactNode;
  title: string;
  description: string;
  disabled?: boolean;
  onConfirm: () => Promise<void>;
}) {
  const [open, setOpen] = useState(false),
    [working, setWorking] = useState(false);
  async function confirm() {
    setWorking(true);
    try {
      await onConfirm();
      setOpen(false);
    } finally {
      setWorking(false);
    }
  }
  return (
    <Dialog.Root
      open={open}
      onOpenChange={(next) => {
        if (!working) setOpen(next);
      }}
    >
      <Dialog.Trigger asChild>
        <Button variant="outline" disabled={disabled}>
          {children}
        </Button>
      </Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay className="ui-overlay" />
        <Dialog.Content className="command-dialog confirmation-dialog">
          <Dialog.Title>{title}</Dialog.Title>
          <Dialog.Description>{description}</Dialog.Description>
          <div className="confirmation-actions">
            <Dialog.Close asChild>
              <Button variant="outline" disabled={working} autoFocus>
                Hủy
              </Button>
            </Dialog.Close>
            <Button
              variant="destructive"
              disabled={working}
              onClick={() => void confirm()}
            >
              {working ? "Đang xử lý…" : "Xác nhận xóa"}
            </Button>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
