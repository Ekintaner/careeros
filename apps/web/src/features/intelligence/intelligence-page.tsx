"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { request, failure } from "@/features/opportunities/client";
import { Feedback } from "@/features/profile/form-ui";
import type { Intelligence, SkillInsight, SkillJobs } from "./types";

function useRead<T>(path: string, attempt: number) {
  const key = `${path}:${attempt}`;
  const [result, setResult] = useState<{
    key: string;
    data?: T;
    error?: string;
  } | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    request<T>(path, undefined, undefined, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setResult({ key, data });
      })
      .catch((e) => {
        if (!controller.signal.aborted) setResult({ key, error: failure(e) });
      });
    return () => controller.abort();
  }, [path, key]);
  return result?.key === key ? result : null;
}
function SkillJobsList({
  skill,
  state,
}: {
  skill: SkillInsight;
  state: string;
}) {
  const [page, setPage] = useState(1);
  const [attempt, setAttempt] = useState(0);
  const result = useRead<SkillJobs>(
    `intelligence/skills/${skill.skillId}/jobs?state=${state}&page=${page}&pageSize=10`,
    attempt,
  );
  const data = result?.data;
  return (
    <section
      id={`skill-jobs-${skill.skillId}`}
      className="insight-jobs"
      aria-label={`Jobs requiring ${skill.name}`}
    >
      {!result && <p role="status">Loading jobs for {skill.name}…</p>}
      {result?.error && (
        <>
          <Feedback error={result.error} />
          <button
            className="secondary-button"
            onClick={() => setAttempt((a) => a + 1)}
          >
            Retry jobs
          </button>
        </>
      )}
      {data && (
        <>
          <p className="field-hint">
            Current career profile coverage for each complete job, calculated
            with the CODEX-008 matching rules.
          </p>
          {data.items.length === 0 ? (
            <p>
              No jobs on this page. The saved requirements may have changed;
              reload the analysis.
            </p>
          ) : (
            <ul>
              {data.items.map((job) => (
                <li key={job.id}>
                  <div>
                    <Link href={`/jobs/${job.id}`}>{job.title}</Link>
                    <p>
                      {job.companyName} · {job.requirementLevel}
                      {job.archived ? " · Archived" : ""}
                    </p>
                  </div>
                  <p>
                    <strong>
                      {job.profileCoverage === null
                        ? "Not scored"
                        : `${job.profileCoverage}% profile coverage`}
                    </strong>
                    <br />
                    <span className="field-hint">
                      {job.earnedProfilePoints}/{job.possiblePoints} points
                    </span>
                  </p>
                </li>
              ))}
            </ul>
          )}
          {data.totalPages > 1 && (
            <div className="insight-pagination">
              <button
                className="secondary-button"
                disabled={page === 1}
                onClick={() => setPage((p) => p - 1)}
              >
                Previous jobs
              </button>
              <span>
                Page {page} of {data.totalPages}
              </span>
              <button
                className="secondary-button"
                disabled={page >= data.totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next jobs
              </button>
            </div>
          )}
        </>
      )}
    </section>
  );
}
const statusLabel = {
  supported: "Profile + resume",
  evidenceGap: "Evidence to complete",
  missing: "No recorded skill",
};
export function SkillsIntelligencePage() {
  const [filters, setFilters] = useState({
    state: "active",
    status: "all",
    sort: "priority",
    search: "",
    page: 1,
  });
  const [search, setSearch] = useState("");
  const [attempt, setAttempt] = useState(0);
  const [expanded, setExpanded] = useState<string | null>(null);
  const query = new URLSearchParams({
    state: filters.state,
    status: filters.status,
    sort: filters.sort,
    page: String(filters.page),
    pageSize: "20",
    search: filters.search,
  });
  const result = useRead<Intelligence>(`intelligence/skills?${query}`, attempt);
  const data = result?.data;
  const summary = data?.summary;
  function change(key: "state" | "status" | "sort", value: string) {
    setFilters((f) => ({ ...f, [key]: value, page: 1 }));
    setExpanded(null);
  }
  function clear() {
    setSearch("");
    setFilters((f) => ({
      ...f,
      status: "all",
      sort: "priority",
      search: "",
      page: 1,
    }));
    setExpanded(null);
  }
  return (
    <div className="editor-page intelligence-page">
      <header className="page-heading intelligence-heading">
        <div>
          <p className="eyebrow">A clearer learning direction</p>
          <h1>Skills intelligence</h1>
          <p>
            Find the skills your saved opportunities ask for, and decide what to
            learn, record or support with evidence.
          </p>
        </div>
        <Link className="secondary-button" href="/profile">
          Update career profile
        </Link>
      </header>
      <div className="intelligence-scope">
        <label htmlFor="intelligence-state">Job scope</label>
        <select
          id="intelligence-state"
          value={filters.state}
          onChange={(e) => change("state", e.target.value)}
        >
          <option value="active">Active jobs</option>
          <option value="archived">Archived jobs</option>
          <option value="all">All saved jobs</option>
        </select>
        <button
          className="text-button"
          onClick={() => {
            setAttempt((a) => a + 1);
            setExpanded(null);
          }}
        >
          Reload analysis
        </button>
      </div>
      {!result && <p role="status">Analyzing your saved job skills…</p>}
      {result?.error && (
        <>
          <Feedback error={result.error} />
          <button
            className="secondary-button"
            onClick={() => setAttempt((a) => a + 1)}
          >
            Retry analysis
          </button>
        </>
      )}
      {data && summary && (
        <>
          <div
            className="intelligence-metrics"
            aria-label="Skill analysis summary"
          >
            <article>
              <p>Jobs with requirements</p>
              <strong>
                {summary.analyzedJobs}
                <span> / {summary.totalJobs}</span>
              </strong>
              <small>
                {summary.unassessedJobs} without recorded requirements
              </small>
            </article>
            <article>
              <p>Career profile coverage</p>
              <strong>
                {summary.profileCoverage === null
                  ? "Not scored"
                  : `${summary.profileCoverage}%`}
              </strong>
              <small>
                {summary.earnedProfilePoints}/{summary.possiblePoints} weighted
                points
              </small>
            </article>
            <article>
              <p>No recorded skill</p>
              <strong>{summary.missingSkills}</strong>
              <small>Learning or assessment opportunities</small>
            </article>
            <article>
              <p>Evidence to complete</p>
              <strong>{summary.evidenceGapSkills}</strong>
              <small>One source recorded, one to review</small>
            </article>
          </div>
          <p className="intelligence-context">
            {summary.distinctSkills} distinct requested skills ·{" "}
            {summary.profileSkills} in your career profile ·{" "}
            {summary.supportedSkills} also supported by a confirmed resume
            record. Summary and priority denominators use the whole selected job
            scope; skill filters below affect only the list.
          </p>
          <details className="intelligence-method">
            <summary>How priorities and evidence work</summary>
            <div>
              <p>
                Each saved job counts once per skill. Required skills carry
                weight 3, preferred skills weight 1, using the same exact
                canonical names as job matching. Demand share is jobs requesting
                the skill ÷ jobs with recorded requirements.
              </p>
              <p>
                Action priority = (3 × required jobs + preferred jobs) ÷ (3 ×
                jobs with requirements) × 100, rounded to one decimal. It is a
                relative demand signal, not a hiring probability or
                learning-time estimate. Skills recorded in both sources need no
                action and receive action priority 0.
              </p>
              <p>
                Learning priority applies only when neither your profile nor any
                confirmed resume records the skill. A skill in one source
                belongs to evidence review, with learning priority 0.
                Profile-only records need resume evidence; resume-only records
                need profile review. These are your own records, not independent
                proof of ability. Missing records do not mean you lack the
                skill.
              </p>
              <p>
                Profile coverage is total earned profile points ÷ total
                requirement points across assessed jobs, not the average of job
                percentages. Resumes do not inflate that profile score. Jobs
                without manual requirements are excluded from that denominator;
                descriptions are not parsed. Reload after editing requirements
                or evidence elsewhere.
              </p>
            </div>
          </details>
          {summary.totalJobs === 0 ? (
            <section className="intelligence-empty">
              <h2>No saved jobs in this scope</h2>
              <p>
                Save an opportunity or change the job scope to begin. No
                statistics are inferred from an empty collection.
              </p>
              <Link className="primary-button" href="/jobs">
                Manage jobs
              </Link>
            </section>
          ) : summary.analyzedJobs === 0 ? (
            <section className="intelligence-empty">
              <h2>Add job requirements to begin</h2>
              <p>
                Your saved jobs have no recorded skill requirements. Open a job
                and use Edit job requirements in the Skill match panel.
              </p>
              <Link className="primary-button" href="/jobs">
                Review saved jobs
              </Link>
            </section>
          ) : (
            <>
              <div className="intelligence-list-heading">
                <div>
                  <p className="eyebrow">From demand to a next step</p>
                  <h2>Your skill priorities</h2>
                </div>
                <span>{data.totalCount} skills match</span>
              </div>
              <form
                className="intelligence-filters"
                onSubmit={(e) => {
                  e.preventDefault();
                  setFilters((f) => ({ ...f, search, page: 1 }));
                  setExpanded(null);
                }}
              >
                <div className="editor-field">
                  <label htmlFor="intelligence-search">
                    Search requested skills
                  </label>
                  <input
                    id="intelligence-search"
                    maxLength={100}
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                  />
                </div>
                <div className="editor-field">
                  <label htmlFor="intelligence-status">Evidence status</label>
                  <select
                    id="intelligence-status"
                    value={filters.status}
                    onChange={(e) => change("status", e.target.value)}
                  >
                    <option value="all">All evidence statuses</option>
                    <option value="missing">No recorded skill</option>
                    <option value="evidenceGap">Evidence to complete</option>
                    <option value="supported">Profile + resume</option>
                  </select>
                </div>
                <div className="editor-field">
                  <label htmlFor="intelligence-sort">Sort skills</label>
                  <select
                    id="intelligence-sort"
                    value={filters.sort}
                    onChange={(e) => change("sort", e.target.value)}
                  >
                    <option value="priority">Action priority</option>
                    <option value="demand">Most requested</option>
                    <option value="name">Skill name</option>
                  </select>
                </div>
                <button className="secondary-button">Search skills</button>
                <button className="text-button" type="button" onClick={clear}>
                  Clear skill filters
                </button>
              </form>
              {data.items.length === 0 ? (
                <section className="intelligence-empty">
                  <h3>No skills match these filters</h3>
                  <p>Try a different search or evidence status.</p>
                  <button className="secondary-button" onClick={clear}>
                    Show all requested skills
                  </button>
                </section>
              ) : (
                <div className="intelligence-skills">
                  {data.items.map((skill) => (
                    <article className="intelligence-skill" key={skill.skillId}>
                      <div className="insight-card-heading">
                        <div>
                          <span className={`insight-badge ${skill.status}`}>
                            {statusLabel[skill.status]}
                          </span>
                          <h3>{skill.name}</h3>
                        </div>
                        <div className="insight-priority">
                          <strong>
                            {skill.actionPriority}
                            <span>/100</span>
                          </strong>
                          <small>
                            {skill.status === "missing"
                              ? "Learning / assessment priority"
                              : skill.status === "evidenceGap"
                                ? "Evidence review priority"
                                : "No current action priority"}
                          </small>
                        </div>
                      </div>
                      <div className="insight-demand">
                        <p>
                          <strong>
                            {skill.jobCount} of {summary.analyzedJobs}
                          </strong>{" "}
                          assessed jobs request this skill{" "}
                          <span>({skill.demandPercent}%)</span>
                        </p>
                        <meter
                          min={0}
                          max={100}
                          value={skill.demandPercent}
                          aria-label={`Demand share for ${skill.name}`}
                        />
                        <p className="field-hint">
                          {skill.requiredJobs} required · {skill.preferredJobs}{" "}
                          preferred · {skill.weightedDemand} weighted demand
                          points
                        </p>
                      </div>
                      <div className="insight-evidence">
                        <p>
                          {skill.inProfile
                            ? "✓ Recorded in career profile"
                            : "Not recorded in career profile"}
                        </p>
                        <p>
                          {skill.confirmedResumes > 0
                            ? `Confirmed in ${skill.confirmedResumes} resume${skill.confirmedResumes === 1 ? "" : "s"}`
                            : "No confirmed resume evidence"}
                        </p>
                      </div>
                      <div className="insight-next">
                        <strong>{skill.action}</strong>
                        <p>
                          {skill.status === "missing"
                            ? "Check whether you already have this skill before treating it as a learning goal. Record real experience when available."
                            : skill.status === "evidenceGap"
                              ? skill.inProfile
                                ? "Confirm this skill only in a resume that supports it. Use a job’s Skill match panel to record that evidence."
                                : "Your resume records this skill. Review your profile before planning more learning."
                              : "Keep the profile and resume evidence accurate as your experience develops."}
                        </p>
                      </div>
                      <div className="insight-actions">
                        <button
                          className="secondary-button"
                          aria-expanded={expanded === skill.skillId}
                          aria-controls={`skill-jobs-${skill.skillId}`}
                          onClick={() =>
                            setExpanded((v) =>
                              v === skill.skillId ? null : skill.skillId,
                            )
                          }
                        >
                          View jobs for {skill.name} ({skill.jobCount})
                        </button>
                        <Link href="/profile">Review profile skills →</Link>
                      </div>
                      {expanded === skill.skillId && (
                        <SkillJobsList
                          key={`${skill.skillId}-${filters.state}`}
                          skill={skill}
                          state={filters.state}
                        />
                      )}
                    </article>
                  ))}
                </div>
              )}
              {data.totalPages > 1 && (
                <nav
                  className="insight-pagination"
                  aria-label="Skill pagination"
                >
                  <button
                    className="secondary-button"
                    disabled={filters.page === 1}
                    onClick={() => {
                      setFilters((f) => ({ ...f, page: f.page - 1 }));
                      setExpanded(null);
                    }}
                  >
                    Previous skills
                  </button>
                  <span>
                    Page {data.page} of {data.totalPages}
                  </span>
                  <button
                    className="secondary-button"
                    disabled={filters.page >= data.totalPages}
                    onClick={() => {
                      setFilters((f) => ({ ...f, page: f.page + 1 }));
                      setExpanded(null);
                    }}
                  >
                    Next skills
                  </button>
                </nav>
              )}
            </>
          )}
        </>
      )}
      <aside className="intelligence-history">
        <h2>History, without guesswork</h2>
        <p>
          Trends are not available yet. Current requirements and their edit
          dates do not provide reliable historical snapshots of skill demand.
          This page shows current saved records only.
        </p>
        <p className="field-hint">
          No external market data, AI inference or automatic skill extraction.
          Recommendations depend on the jobs and evidence you record.
        </p>
      </aside>
    </div>
  );
}
