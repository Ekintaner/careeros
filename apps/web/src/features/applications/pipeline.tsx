"use client";
import { useState, useSyncExternalStore, type FormEvent } from "react";
import Link from "next/link";
import { Field, Feedback } from "@/features/profile/form-ui";
import { useResource } from "@/features/opportunities/lists";
import { CompanyPicker, Pagination } from "@/features/opportunities/shared";
import {
  stages,
  outcomes,
  date,
  type Status,
  type StageCount,
  type ApplicationCard,
  type Page,
} from "./client";
import { StatusControl } from "./status-control";

const stageMedia = "(max-width: 1100px)";
function subscribeToWidth(change: () => void) {
  if (typeof window.matchMedia !== "function") return () => {};
  const media = window.matchMedia(stageMedia);
  media.addEventListener("change", change);
  return () => media.removeEventListener("change", change);
}
const compactSnapshot = () =>
  typeof window.matchMedia === "function" &&
  window.matchMedia(stageMedia).matches;
const serverSnapshot = () => true;
function Column({
  status,
  query,
  count,
  reload,
}: {
  status: Status;
  query: string;
  count?: number;
  reload: () => void;
}) {
  const [page, setPage] = useState(1);
  const resource = useResource<Page<ApplicationCard>>(
    `applications?${query}&status=${status}&page=${page}&pageSize=10`,
  );
  return (
    <section className="pipeline-column" aria-label={`${status} applications`}>
      <header>
        <h2>{status}</h2>
        <span className="stage-count" aria-label={`${status} count`}>
          {count ?? "—"}
        </span>
      </header>
      {!resource ? (
        <p role="status">Loading…</p>
      ) : resource.error ? (
        <Feedback error={resource.error} />
      ) : (
        <>
          {!resource.data?.items.length && (
            <p className="stage-empty">
              No applications in {status.toLowerCase()}.
            </p>
          )}
          {resource.data?.items.map((a) => (
            <article key={a.id} className="application-card">
              <p className="card-company">{a.companyName}</p>
              <h3>
                <Link href={`/applications/${a.id}`}>{a.jobTitle}</Link>
              </h3>
              {a.location && <p className="card-location">{a.location}</p>}
              <p className="card-date">
                {a.appliedAtUtc
                  ? `Applied ${date(a.appliedAtUtc)}`
                  : `Saved ${date(a.createdAtUtc)}`}
              </p>
              {a.resumeId && (
                <span className="outline-label">Resume attached</span>
              )}
              <StatusControl application={a} onChanged={reload} />
            </article>
          ))}
          {resource.data && resource.data.totalPages > 1 && (
            <Pagination
              page={page}
              pages={resource.data.totalPages}
              count={resource.data.totalCount}
              onPage={setPage}
            />
          )}
        </>
      )}
    </section>
  );
}
export function ApplicationsPage() {
  const compact = useSyncExternalStore(
    subscribeToWidth,
    compactSnapshot,
    serverSnapshot,
  );
  const [attempt, setAttempt] = useState(0);
  const [view, setView] = useState("pipeline");
  const [mobile, setMobile] = useState<Status>("Saved");
  const [search, setSearch] = useState("");
  const [company, setCompany] = useState("");
  const [sort, setSort] = useState("updated");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [filters, setFilters] = useState("sort=updated");
  const state = view === "archived" ? "archived" : "active";
  const query = `${filters}&state=${state}`;
  const summary = useResource<StageCount[]>(
    `applications/summary?${query}`,
    attempt,
  );
  const visible: readonly Status[] =
    view === "pipeline"
      ? stages
      : view === "outcomes"
        ? outcomes
        : ["Archived"];
  const selected = visible.includes(mobile) ? mobile : (visible[0] ?? "Saved");
  function apply(e: FormEvent) {
    e.preventDefault();
    const q = new URLSearchParams({ search, sort });
    if (company) q.set("companyId", company);
    if (from) q.set("appliedFrom", `${from}T00:00:00Z`);
    if (to) q.set("appliedTo", `${to}T23:59:59.999Z`);
    setFilters(q.toString());
  }
  const count = (s: Status) =>
    summary?.data?.find((x) => x.status === s)?.count;
  const total = summary?.data
    ?.filter((x) => visible.includes(x.status))
    .reduce((n, x) => n + x.count, 0);
  return (
    <div className="editor-page pipeline-page">
      <header className="page-heading job-heading">
        <div>
          <p className="eyebrow">YOUR SEARCH, IN MOTION</p>
          <h1>Applications</h1>
          <p>Keep the next step in view. Every move keeps its history.</p>
        </div>
        <Link className="primary-button" href="/applications/new">
          Track application
        </Link>
      </header>
      <div
        className="pipeline-views"
        role="group"
        aria-label="Application view"
      >
        {(
          [
            ["pipeline", "Active pipeline"],
            ["outcomes", "Outcomes"],
            ["archived", "Archived"],
          ] as const
        ).map(([value, title]) => (
          <button
            key={value}
            aria-pressed={view === value}
            className="secondary-button"
            onClick={() => setView(value)}
          >
            {title}
          </button>
        ))}
      </div>
      <details className="filter-panel">
        <summary>Search & filters</summary>
        <form onSubmit={apply}>
          <div className="filter-grid">
            <Field id="application-search" title="Search jobs or companies">
              <input
                id="application-search"
                value={search}
                maxLength={200}
                onChange={(e) => setSearch(e.target.value)}
              />
            </Field>
            <CompanyPicker value={company} onChange={setCompany} optional />
            <Field id="application-sort" title="Sort">
              <select
                id="application-sort"
                value={sort}
                onChange={(e) => setSort(e.target.value)}
              >
                {[
                  ["updated", "Recently updated"],
                  ["newest", "Newest created"],
                  ["oldest", "Oldest created"],
                  ["applied", "Applied date"],
                  ["company", "Company"],
                  ["title", "Job title"],
                ].map(([v, t]) => (
                  <option value={v} key={v}>
                    {t}
                  </option>
                ))}
              </select>
            </Field>
            <Field id="applied-from" title="Applied from (UTC)">
              <input
                id="applied-from"
                type="date"
                value={from}
                max={to || undefined}
                onChange={(e) => setFrom(e.target.value)}
              />
            </Field>
            <Field id="applied-to" title="Applied through (UTC)">
              <input
                id="applied-to"
                type="date"
                value={to}
                min={from || undefined}
                onChange={(e) => setTo(e.target.value)}
              />
            </Field>
          </div>
          <button className="secondary-button" type="submit">
            Apply filters
          </button>
        </form>
      </details>
      <div className="pipeline-meta">
        <p>
          {total === undefined
            ? "Loading counts…"
            : `${total} ${total === 1 ? "application" : "applications"}`}{" "}
          ·{" "}
          {view === "pipeline"
            ? "Active stages"
            : view === "outcomes"
              ? "Completed outcomes"
              : "Preserved history"}
        </p>
        <button
          className="text-button"
          onClick={() => setAttempt((n) => n + 1)}
        >
          Refresh pipeline
        </button>
      </div>
      <Feedback error={summary?.error} />
      {total === 0 && (
        <p className="pipeline-empty">
          {view === "pipeline"
            ? "No active applications match this view. Track an existing job to start your pipeline."
            : "No applications match this view."}
        </p>
      )}
      <div className="mobile-stage">
        <Field id="mobile-stage" title="Pipeline stage">
          <select
            id="mobile-stage"
            value={selected}
            onChange={(e) => setMobile(e.target.value as Status)}
          >
            {visible.map((s) => (
              <option key={s} value={s}>
                {s} ({count(s) ?? "…"})
              </option>
            ))}
          </select>
        </Field>
      </div>
      <div
        className={`pipeline-board ${view !== "pipeline" ? "outcome-board" : ""}`}
        role="region"
        aria-label="Application pipeline columns"
        tabIndex={0}
        key={`${query}:${attempt}:${view}`}
      >
        {(compact ? [selected] : visible).map((s) => (
          <div
            key={s}
            className={
              s === selected ? "stage-slot mobile-selected" : "stage-slot"
            }
          >
            <Column
              status={s}
              query={query}
              count={count(s)}
              reload={() => setAttempt((n) => n + 1)}
            />
          </div>
        ))}
      </div>
    </div>
  );
}
