"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { Feedback, Field } from "@/features/profile/form-ui";
import {
  request,
  failure,
  date,
  workModes,
  employmentTypes,
  seniorityLevels,
  label,
  type CompanySummary,
  type JobSummary,
  type Page,
} from "./client";
import { CompanyPicker, Pagination } from "./shared";

export function useResource<T>(path: string, attempt = 0) {
  const key = `${path}:${attempt}`;
  const [state, setState] = useState<{
    key: string;
    data?: T;
    error?: string;
  }>();
  useEffect(() => {
    const abort = new AbortController();
    request<T>(path, undefined, undefined, abort.signal)
      .then((data) => {
        if (!abort.signal.aborted) setState({ key, data });
      })
      .catch((e) => {
        if (!abort.signal.aborted) setState({ key, error: failure(e) });
      });
    return () => abort.abort();
  }, [path, key]);
  return state?.key === key ? state : undefined;
}
function FilterText({
  name,
  title,
  value,
  change,
}: {
  name: string;
  title: string;
  value: string;
  change: (value: string) => void;
}) {
  return (
    <Field id={`filter-${name}`} title={title}>
      <input
        id={`filter-${name}`}
        value={value}
        maxLength={200}
        onChange={(e) => change(e.target.value)}
      />
    </Field>
  );
}
function FilterSelect({
  name,
  title,
  value,
  change,
  options,
}: {
  name: string;
  title: string;
  value: string;
  change: (value: string) => void;
  options: readonly string[];
}) {
  return (
    <Field id={`filter-${name}`} title={title}>
      <select
        id={`filter-${name}`}
        value={value}
        onChange={(e) => change(e.target.value)}
      >
        <option value="">Any</option>
        {options.map((o) => (
          <option key={o} value={o}>
            {label(o)}
          </option>
        ))}
      </select>
    </Field>
  );
}
function LoadError({ error, retry }: { error: string; retry: () => void }) {
  return (
    <>
      <Feedback error={error} />
      <button className="secondary-button" onClick={retry}>
        Try again
      </button>
    </>
  );
}

export function CompaniesPage({ saved = false }: { saved?: boolean }) {
  const [draft, setDraft] = useState({
    search: "",
    industry: "",
    location: "",
    sort: "name",
  });
  const [filters, setFilters] = useState(draft);
  const [page, setPage] = useState(1);
  const [attempt, setAttempt] = useState(0);
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(saved ? "Company saved." : "");
  const state = useResource<Page<CompanySummary>>(
    `companies?${new URLSearchParams({ ...filters, page: String(page), pageSize: "20" })}`,
    attempt,
  );
  const data = state?.data;
  async function remove(c: CompanySummary) {
    if (
      busy ||
      !window.confirm(
        `Delete ${c.name}? Companies with saved jobs must be kept.`,
      )
    )
      return;
    setBusy(c.id);
    setError("");
    setSuccess("");
    try {
      await request(`companies/${c.id}?version=${c.version}`, "DELETE");
      setSuccess("Company deleted.");
      setPage(1);
      setAttempt(attempt + 1);
    } catch (e) {
      setError(failure(e));
    } finally {
      setBusy("");
    }
  }
  return (
    <div className="editor-page">
      <header className="page-heading">
        <div>
          <p className="eyebrow">Your network</p>
          <h1>Companies</h1>
          <p>The organizations behind your next opportunity.</p>
        </div>
        <Link className="primary-button" href="/companies/new">
          Add company
        </Link>
      </header>
      <Feedback error={error} success={success} />
      <form
        className="list-filters"
        onSubmit={(e) => {
          e.preventDefault();
          setFilters(draft);
          setPage(1);
        }}
      >
        <FilterText
          name="search"
          title="Search companies"
          value={draft.search}
          change={(search) => setDraft((v) => ({ ...v, search }))}
        />
        <details>
          <summary>Filters & sort</summary>
          <div className="filter-grid">
            <FilterText
              name="industry"
              title="Industry"
              value={draft.industry}
              change={(industry) => setDraft((v) => ({ ...v, industry }))}
            />
            <FilterText
              name="location"
              title="Location"
              value={draft.location}
              change={(location) => setDraft((v) => ({ ...v, location }))}
            />
            <Field id="company-sort" title="Sort by">
              <select
                id="company-sort"
                value={draft.sort}
                onChange={(e) =>
                  setDraft((v) => ({ ...v, sort: e.target.value }))
                }
              >
                <option value="name">Company name</option>
                <option value="newest">Newest saved</option>
                <option value="oldest">Oldest saved</option>
              </select>
            </Field>
          </div>
        </details>
        <div className="filter-actions">
          <button className="secondary-button">Search companies</button>
          <button
            type="button"
            className="text-button"
            onClick={() => {
              const clear = {
                search: "",
                industry: "",
                location: "",
                sort: "name",
              };
              setDraft(clear);
              setFilters(clear);
              setPage(1);
            }}
          >
            Clear filters
          </button>
        </div>
      </form>
      {!state ? (
        <p role="status">Loading companies…</p>
      ) : state.error ? (
        <LoadError error={state.error} retry={() => setAttempt(attempt + 1)} />
      ) : (
        data && (
          <>
            {data.items.length ? (
              <div className="record-list">
                {data.items.map((c) => (
                  <article className="company-row" key={c.id}>
                    <div>
                      <h2>
                        <Link href={`/companies/${c.id}/edit`}>{c.name}</Link>
                      </h2>
                      <p>
                        {[c.industry, c.location].filter(Boolean).join(" · ") ||
                          "Company details not added"}
                      </p>
                    </div>
                    <Link
                      className="record-count"
                      href={`/jobs?companyId=${c.id}&state=all`}
                    >
                      {c.jobCount} {c.jobCount === 1 ? "job" : "jobs"}
                    </Link>
                    <div className="row-actions">
                      <Link
                        href={`/companies/${c.id}/edit`}
                        aria-label={`Edit ${c.name}`}
                      >
                        Edit<span className="sr-only"> {c.name}</span>
                      </Link>
                      <button
                        className="text-button"
                        aria-label={`Delete ${c.name}`}
                        disabled={!!busy}
                        onClick={() => void remove(c)}
                      >
                        {busy === c.id ? "Deleting…" : "Delete"}
                        <span className="sr-only"> {c.name}</span>
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            ) : (
              <div className="editor-empty">
                <h2>
                  {Object.values(filters).some((v, i) => i < 3 && v)
                    ? "No matching companies"
                    : "No companies yet"}
                </h2>
                <p>Add a company to start organizing your opportunities.</p>
                <Link href="/companies/new" className="text-button">
                  Add your first company
                </Link>
              </div>
            )}
            <Pagination
              page={page}
              pages={data.totalPages}
              count={data.totalCount}
              busy={!!busy}
              onPage={setPage}
            />
          </>
        )
      )}
    </div>
  );
}
export function JobsPage({
  companyId = "",
  state: initialState = "active",
}: {
  companyId?: string;
  state?: string;
}) {
  const initial = {
    search: "",
    companyId,
    location: "",
    workMode: "",
    employmentType: "",
    seniorityLevel: "",
    source: "",
    state: initialState,
    sort: "newest",
  };
  const [draft, setDraft] = useState(initial);
  const [filters, setFilters] = useState(initial);
  const [page, setPage] = useState(1);
  const [attempt, setAttempt] = useState(0);
  const query = new URLSearchParams({
    ...Object.fromEntries(Object.entries(filters).filter(([, v]) => v)),
    page: String(page),
    pageSize: "20",
  });
  const state = useResource<Page<JobSummary>>(`jobs?${query}`, attempt);
  const data = state?.data;
  const change = (key: keyof typeof draft, value: string) =>
    setDraft((v) => ({ ...v, [key]: value }));
  return (
    <div className="editor-page">
      <header className="page-heading">
        <div>
          <p className="eyebrow">Your next move</p>
          <h1>Jobs</h1>
          <p>A considered collection of opportunities worth exploring.</p>
        </div>
        <Link href="/jobs/new" className="primary-button">
          Add job
        </Link>
      </header>
      <form
        className="list-filters"
        onSubmit={(e) => {
          e.preventDefault();
          setFilters(draft);
          setPage(1);
        }}
      >
        <FilterText
          name="search"
          title="Search jobs"
          value={draft.search}
          change={(v) => change("search", v)}
        />
        <details>
          <summary>Filters & sort</summary>
          <div className="filter-grid">
            <CompanyPicker
              value={draft.companyId}
              optional
              onChange={(v) => change("companyId", v)}
            />
            <FilterText
              name="location"
              title="Location"
              value={draft.location}
              change={(v) => change("location", v)}
            />
            <FilterSelect
              name="workMode"
              title="Work mode"
              value={draft.workMode}
              change={(v) => change("workMode", v)}
              options={workModes}
            />
            <FilterSelect
              name="employmentType"
              title="Employment type"
              value={draft.employmentType}
              change={(v) => change("employmentType", v)}
              options={employmentTypes}
            />
            <FilterSelect
              name="seniorityLevel"
              title="Seniority"
              value={draft.seniorityLevel}
              change={(v) => change("seniorityLevel", v)}
              options={seniorityLevels}
            />
            <FilterText
              name="source"
              title="Source"
              value={draft.source}
              change={(v) => change("source", v)}
            />
            <Field id="filter-state" title="Show">
              <select
                id="filter-state"
                value={draft.state}
                onChange={(e) => change("state", e.target.value)}
              >
                <option value="active">Active jobs</option>
                <option value="archived">Archived jobs</option>
                <option value="all">All jobs</option>
              </select>
            </Field>
            <Field id="job-sort" title="Sort by">
              <select
                id="job-sort"
                value={draft.sort}
                onChange={(e) => change("sort", e.target.value)}
              >
                <option value="newest">Newest saved</option>
                <option value="oldest">Oldest saved</option>
                <option value="posted">Posted date</option>
                <option value="company">Company name</option>
                <option value="title">Job title</option>
              </select>
            </Field>
          </div>
        </details>
        <div className="filter-actions">
          <button className="secondary-button">Search jobs</button>
          <button
            type="button"
            className="text-button"
            onClick={() => {
              const clear = { ...initial, companyId: "" };
              setDraft(clear);
              setFilters(clear);
              setPage(1);
            }}
          >
            Clear filters
          </button>
        </div>
      </form>
      {!state ? (
        <p role="status">Loading jobs…</p>
      ) : state.error ? (
        <LoadError error={state.error} retry={() => setAttempt(attempt + 1)} />
      ) : (
        data && (
          <>
            {data.items.length ? (
              <div className="record-list">
                {data.items.map((j) => (
                  <article className="job-row" key={j.id}>
                    <div>
                      <p className="company-caption">{j.companyName}</p>
                      <h2>
                        <Link href={`/jobs/${j.id}`}>{j.title}</Link>
                      </h2>
                      <p>
                        {[
                          j.location,
                          j.workMode && label(j.workMode),
                          j.employmentType && label(j.employmentType),
                          j.seniorityLevel && label(j.seniorityLevel),
                        ]
                          .filter(Boolean)
                          .join(" · ")}
                      </p>
                    </div>
                    <div className="job-row-meta">
                      {j.archivedAtUtc && (
                        <span className="outline-label">Archived</span>
                      )}
                      {j.source && <span>{j.source}</span>}
                      <span>Saved {date(j.createdAtUtc)}</span>
                      {j.postedAtUtc && (
                        <span>Posted {date(j.postedAtUtc)}</span>
                      )}
                    </div>
                    <Link
                      className="record-open"
                      href={`/jobs/${j.id}`}
                      aria-label={`View ${j.title}`}
                    >
                      View<span className="sr-only"> {j.title}</span> →
                    </Link>
                  </article>
                ))}
              </div>
            ) : (
              <div className="editor-empty">
                <h2>
                  {filters.search ||
                  filters.companyId ||
                  filters.location ||
                  filters.workMode ||
                  filters.employmentType ||
                  filters.seniorityLevel ||
                  filters.source ||
                  filters.state !== "active"
                    ? "No matching jobs"
                    : "No jobs saved yet"}
                </h2>
                <p>
                  Found something interesting? Save the details here for your
                  next step.
                </p>
                <Link className="text-button" href="/jobs/new">
                  Add a job
                </Link>
              </div>
            )}
            <Pagination
              page={page}
              pages={data.totalPages}
              count={data.totalCount}
              onPage={setPage}
            />
          </>
        )
      )}
    </div>
  );
}
