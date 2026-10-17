"use client";
import {
  useEffect,
  useRef,
  isValidElement,
  cloneElement,
  type ReactElement,
} from "react";

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
      {isValidElement(children) &&
      typeof children.type === "string" &&
      ["input", "select", "textarea"].includes(children.type)
        ? cloneElement(children as ReactElement<Record<string, unknown>>, {
            id: (children.props as Record<string, unknown>).id ?? id,
            "aria-invalid": error
              ? true
              : (children.props as Record<string, unknown>)["aria-invalid"],
            "aria-describedby":
              [
                ...new Set(
                  [
                    String(
                      (children.props as Record<string, unknown>)[
                        "aria-describedby"
                      ] ?? "",
                    ),
                    error || hint ? `${id}-hint` : "",
                  ]
                    .join(" ")
                    .split(/\s+/)
                    .filter(Boolean),
                ),
              ].join(" ") || undefined,
          })
        : children}
      {(error || hint) && (
        <p id={`${id}-hint`} className={error ? "field-error" : "field-hint"}>
          {error || hint}
        </p>
      )}
    </div>
  );
}
