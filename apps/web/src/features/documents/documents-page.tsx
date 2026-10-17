"use client";
import { useEffect, useRef, useState, type FormEvent } from "react";
import Link from "next/link";
import { authClient } from "@/features/auth/auth-client";
import { request, failure, type Page } from "@/features/opportunities/client";
import { Field, Feedback } from "@/features/profile/form-ui";
import { Pagination } from "@/features/opportunities/shared";
import "./documents.css";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";

export interface DocumentItem {
  id: string;
  name: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  type: string;
  createdAtUtc: string;
  resumeId: string | null;
  isPrimary: boolean;
  applicationCount: number;
}
const types = ["Resume", "CoverLetter", "Certificate", "Portfolio", "Other"];
export function DocumentsPage({ resumes = false }: { resumes?: boolean }) {
  const [data, setData] = useState<Page<DocumentItem>>();
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState(resumes ? "Resume" : "");
  const [type, setType] = useState(resumes ? "Resume" : "Other");
  const [name, setName] = useState("");
  const [file, setFile] = useState<File>();
  const input = useRef<HTMLInputElement>(null);
  const [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [pending, setPending] = useState<DocumentItem>();
  useEffect(() => {
    const abort = new AbortController();
    request<Page<DocumentItem>>(
      `documents?page=${page}${filter ? `&type=${filter}` : ""}`,
      undefined,
      undefined,
      abort.signal,
    )
      .then((result) => {
        if (!abort.signal.aborted) setData(result);
      })
      .catch((e) => {
        if (!abort.signal.aborted) setError(failure(e));
      });
    return () => abort.abort();
  }, [page, filter, revision]);
  async function action(work: () => Promise<unknown>, message: string) {
    setBusy(true);
    setError("");
    setSuccess("");
    try {
      await work();
      setSuccess(message);
      setPending(undefined);
      setData(undefined);
      setRevision((r) => r + 1);
    } catch (e) {
      setError(failure(e));
    } finally {
      setBusy(false);
    }
  }
  async function upload(e: FormEvent) {
    e.preventDefault();
    if (
      !file ||
      !file.name.toLowerCase().endsWith(".pdf") ||
      file.type !== "application/pdf" ||
      file.size === 0 ||
      file.size > 5 * 1024 * 1024
    ) {
      setError("Choose a PDF file between 1 byte and 5 MiB.");
      return;
    }
    const query = new URLSearchParams({
      filename: file.name,
      name: name.trim(),
      type,
    });
    await action(async () => {
      await authClient().request(`/api/v1/documents?${query}`, {
        method: "POST",
        headers: { "Content-Type": "application/pdf" },
        body: file,
      });
      setFile(undefined);
      setName("");
      if (input.current) input.current.value = "";
    }, "Document uploaded.");
  }
  return (
    <div className="documents-page">
      <header className="documents-heading">
        <div>
          <p className="eyebrow">YOUR CAREER LIBRARY</p>
          <h1>{resumes ? "Resumes" : "Documents"}</h1>
          <p>Keep the right version ready for your next opportunity.</p>
        </div>
        <Link
          className="secondary-button"
          href={resumes ? "/documents" : "/resumes"}
        >
          {resumes ? "All documents" : "Manage resumes"}
        </Link>
      </header>
      <Feedback error={pending ? undefined : error} success={success} />
      {error && (
        <button
          className="text-button"
          onClick={() => {
            setError("");
            setRevision((r) => r + 1);
          }}
        >
          Retry loading documents
        </button>
      )}
      <div className="documents-layout">
        <section className="document-upload" aria-labelledby="upload-title">
          <h2 id="upload-title">
            Upload {resumes ? "a resume" : "a document"}
          </h2>
          <p>PDF only · Up to 5 MiB per file</p>
          <form onSubmit={upload}>
            <fieldset disabled={busy}>
              <Field id="document-name" title="Document name">
                <input
                  id="document-name"
                  required
                  maxLength={200}
                  value={name}
                  placeholder="e.g. Software Engineer · October"
                  onChange={(e) => setName(e.target.value)}
                />
              </Field>
              {!resumes && (
                <Field id="document-type" title="Document type">
                  <select
                    id="document-type"
                    value={type}
                    onChange={(e) => setType(e.target.value)}
                  >
                    {types.map((t) => (
                      <option key={t} value={t}>
                        {t === "CoverLetter" ? "Cover letter" : t}
                      </option>
                    ))}
                  </select>
                </Field>
              )}
              <Field id="document-file" title="PDF file">
                <input
                  ref={input}
                  id="document-file"
                  type="file"
                  accept=".pdf,application/pdf"
                  required
                  onChange={(e) => setFile(e.target.files?.[0])}
                />
              </Field>
              <button className="primary-button" type="submit">
                {busy ? "Working…" : "Upload document"}
              </button>
            </fieldset>
          </form>
          <p className="field-hint">
            The first resume becomes primary. Choose a resume when creating or
            editing an application; changing the primary does not change
            existing assignments.
          </p>
        </section>
        <section className="document-library" aria-labelledby="library-title">
          <div className="documents-heading">
            <h2 id="library-title">
              {resumes ? "Your resumes" : "Your library"}
            </h2>
            {!resumes && (
              <Field id="document-filter" title="Filter by type">
                <select
                  id="document-filter"
                  value={filter}
                  disabled={busy}
                  onChange={(e) => {
                    setFilter(e.target.value);
                    setPage(1);
                    setData(undefined);
                  }}
                >
                  <option value="">All types</option>
                  {types.map((t) => (
                    <option key={t}>{t}</option>
                  ))}
                </select>
              </Field>
            )}
          </div>
          {!data ? (
            <p role="status">
              {error ? "Library unavailable. Try again." : "Loading documents…"}
            </p>
          ) : data.items.length === 0 ? (
            <div className="document-empty">
              <h3>A fresh start for your next chapter</h3>
              <p>
                {page > 1
                  ? "No documents on this page. Go back to the previous page."
                  : "Upload your first PDF to start your library."}
              </p>
            </div>
          ) : (
            <ul className="document-cards">
              {data.items.map((d) => (
                <li key={d.id} className="document-card">
                  <div>
                    <span className="document-kind">
                      {d.type === "CoverLetter" ? "Cover letter" : d.type} · PDF
                    </span>
                    <h3>
                      {d.name}{" "}
                      {d.isPrimary && (
                        <span className="primary-badge">Primary</span>
                      )}
                    </h3>
                    <p className="document-filename">{d.originalFileName}</p>
                    <p className="field-hint">
                      {(d.fileSizeBytes / 1024).toFixed(1)} KB ·{" "}
                      {new Date(d.createdAtUtc).toLocaleDateString()}{" "}
                      {d.resumeId &&
                        `· ${d.applicationCount} application${d.applicationCount === 1 ? "" : "s"}`}
                    </p>
                  </div>
                  <div className="document-actions">
                    <a
                      className="secondary-button"
                      aria-label={"Download " + d.name}
                      href={`/api/v1/documents/${d.id}/download`}
                      download
                      aria-disabled={busy}
                      onClick={async (event) => {
                        event.preventDefault();
                        if (busy) return;
                        setError("");
                        try {
                          await authClient().request("/api/v1/auth/me");
                          const download = document.createElement("a");
                          download.href = `/api/v1/documents/${d.id}/download`;
                          download.download = d.originalFileName;
                          document.body.append(download);
                          download.click();
                          download.remove();
                        } catch (e) {
                          setError(failure(e));
                        }
                      }}
                    >
                      Download<span className="sr-only">{d.name}</span>
                    </a>
                    {d.resumeId && !d.isPrimary && (
                      <button
                        className="secondary-button"
                        disabled={busy}
                        aria-label={"Make primary " + d.name}
                        onClick={() =>
                          action(
                            () =>
                              request(
                                `documents/resumes/${d.resumeId}/primary`,
                                "POST",
                              ),
                            "Primary resume updated.",
                          )
                        }
                      >
                        Make primary <span className="sr-only">{d.name}</span>
                      </button>
                    )}
                    <button
                      className="text-button"
                      disabled={busy || d.applicationCount > 0}
                      aria-label={"Delete " + d.name}
                      title={
                        d.applicationCount > 0
                          ? "Remove all application assignments before deleting"
                          : undefined
                      }
                      onClick={() => setPending(d)}
                    >
                      Delete<span className="sr-only">{d.name}</span>
                    </button>
                  </div>
                  {d.applicationCount > 0 && (
                    <p className="field-hint">
                      Assigned resumes are protected. Edit the linked
                      applications to remove this resume before deleting.
                    </p>
                  )}
                </li>
              ))}
            </ul>
          )}
          {data && (
            <Pagination
              page={page}
              pages={data.totalPages}
              count={data.totalCount}
              busy={busy}
              onPage={(p) => {
                setPage(p);
                setData(undefined);
              }}
            />
          )}
        </section>
      </div>
      {pending && (
        <ConfirmDialog
          title={`Delete ${pending.name}?`}
          description={`This permanently removes the file and its metadata.${pending.isPrimary ? " Your library will have no primary resume until you select another." : ""}`}
          error={error}
          busy={busy}
          onCancel={() => setPending(undefined)}
          onConfirm={() =>
            action(
              () => request(`documents/${pending.id}`, "DELETE"),
              "Document deleted.",
            )
          }
        />
      )}
    </div>
  );
}
