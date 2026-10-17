"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { Field } from "@/features/profile/form-ui";
import { request, type CompanySummary, type Page } from "./client";

export function Pagination({
  page,
  pages,
  count,
  busy,
  onPage,
}: {
  page: number;
  pages: number;
  count: number;
  busy?: boolean;
  onPage: (page: number) => void;
}) {
  return (
    <nav className="list-pagination" aria-label="Pagination">
      <span>
        {count} {count === 1 ? "record" : "records"} · Page {page} of{" "}
        {Math.max(1, pages)}
      </span>
      <div>
        <button
          type="button"
          className="secondary-button"
          disabled={busy || page <= 1}
          onClick={() => onPage(page - 1)}
        >
          Previous
        </button>
        <button
          type="button"
          className="secondary-button"
          disabled={busy || page >= pages}
          onClick={() => onPage(page + 1)}
        >
          Next
        </button>
      </div>
    </nav>
  );
}
export function CompanyPicker({
  value,
  selectedName,
  onChange,
  disabled,
  optional = false,
}: {
  value: string;
  selectedName?: string;
  onChange: (id: string) => void;
  disabled?: boolean;
  optional?: boolean;
}) {
  const [search, setSearch] = useState("");
  const [page, setPage] = useState<Page<CompanySummary>>();
  const [error, setError] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const abort = new AbortController();
    const timer = setTimeout(() => {
      request<Page<CompanySummary>>(
        `companies?pageSize=20&search=${encodeURIComponent(search)}`,
        undefined,
        undefined,
        abort.signal,
      )
        .then((p) => {
          if (!abort.signal.aborted) {
            setPage(p);
            setError(false);
          }
        })
        .catch(() => {
          if (!abort.signal.aborted) setError(true);
        });
    }, 200);
    return () => {
      clearTimeout(timer);
      abort.abort();
    };
  }, [search, attempt]);
  return (
    <div className="company-picker">
      <Field id="company-search" title="Find a company">
        <input
          id="company-search"
          maxLength={200}
          value={search}
          disabled={disabled}
          placeholder="Search your companies"
          onChange={(e) => setSearch(e.target.value)}
        />
      </Field>
      <Field id="companyId" title={optional ? "Company" : "Company (required)"}>
        <select
          id="companyId"
          value={value}
          disabled={disabled}
          onChange={(e) => onChange(e.target.value)}
        >
          <option value="">
            {optional ? "All companies" : "Choose a company"}
          </option>
          {value && !page?.items.some((c) => c.id === value) && (
            <option value={value}>{selectedName || "Selected company"}</option>
          )}
          {page?.items.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
              {c.location ? ` · ${c.location}` : ""}
            </option>
          ))}
        </select>
      </Field>
      {error ? (
        <p role="alert">
          Couldn’t load companies.{" "}
          <button
            type="button"
            className="text-button"
            onClick={() => setAttempt(attempt + 1)}
          >
            Retry companies
          </button>
        </p>
      ) : !page ? (
        <p role="status">Loading companies…</p>
      ) : page.totalCount === 0 ? (
        <p className="field-hint">
          {search ? "No matching companies." : "No companies yet."}{" "}
          <Link href="/companies/new">Create a company</Link> first, then return
          here.
        </p>
      ) : page.totalCount > 20 ? (
        <p className="field-hint">
          Showing 20 matches. Refine your search to find a company.
        </p>
      ) : null}
    </div>
  );
}
