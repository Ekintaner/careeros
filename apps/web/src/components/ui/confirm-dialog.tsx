"use client";
import { useEffect, useId, useRef } from "react";
export function ConfirmDialog({
  title,
  description,
  error,
  busy,
  onCancel,
  onConfirm,
}: {
  title: string;
  description: string;
  error?: string;
  busy: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const dialog = useRef<HTMLDialogElement>(null),
    cancel = useRef<HTMLButtonElement>(null);
  const id = useId();
  useEffect(() => {
    const node = dialog.current;
    if (!node) return;
    const previous = document.activeElement as HTMLElement | null;
    if (typeof node.showModal === "function") node.showModal();
    else node.setAttribute("open", "");
    cancel.current?.focus();
    return () => {
      if (node.open && typeof node.close === "function") node.close();
      if (previous?.isConnected) previous.focus();
      else document.getElementById("main-content")?.focus();
    };
  }, []);
  return (
    <dialog
      ref={dialog}
      className="confirm-dialog"
      role="alertdialog"
      aria-labelledby={`${id}-title`}
      aria-describedby={`${id}-description`}
      aria-busy={busy}
      onCancel={(e) => {
        e.preventDefault();
        if (!busy) onCancel();
      }}
    >
      <h2 id={`${id}-title`}>{title}</h2>
      <p id={`${id}-description`}>{description}</p>
      {error && (
        <p role="alert" className="auth-error">
          {error}
        </p>
      )}
      <div className="dialog-actions">
        <button
          ref={cancel}
          type="button"
          disabled={busy}
          className="secondary-button"
          onClick={onCancel}
        >
          Cancel
        </button>
        <button
          type="button"
          disabled={busy}
          className="danger-button"
          onClick={onConfirm}
        >
          {busy ? "Deleting…" : "Permanently delete"}
        </button>
      </div>
    </dialog>
  );
}
