"use client";
import { useEffect, useRef } from "react";

export function Feedback({
  error,
  success,
}: {
  error?: string;
  success?: string;
}) {
  const ref = useRef<HTMLParagraphElement>(null);
  useEffect(() => {
    if (error) ref.current?.focus();
  }, [error]);
  return (
    <>
      {error && (
        <p ref={ref} tabIndex={-1} role="alert" className="auth-error">
          {error}
        </p>
      )}
      {success && (
        <p role="status" className="save-success">
          {success}
        </p>
      )}
    </>
  );
}
export function useUnsavedChanges(dirty: boolean) {
  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);
}
export function Field({
  id,
  title,
  error,
  hint,
  children,
}: {
  id: string;
  title: string;
  error?: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="editor-field">
      <label htmlFor={id}>{title}</label>
      {children}
      {(error || hint) && (
        <p id={`${id}-hint`} className={error ? "field-error" : "field-hint"}>
          {error || hint}
        </p>
      )}
    </div>
  );
}
