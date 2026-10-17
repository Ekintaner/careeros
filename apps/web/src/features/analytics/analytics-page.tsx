"use client";
import { useEffect, useState } from "react";
import "./analytics.css";
import { request, failure } from "@/features/opportunities/client";
export interface Row {
  label: string;
  count: number;
}
export interface Analytics {
  startUtc: string;
  endExclusiveUtc: string;
  total: number;
  applied: number;
  interviewed: number;
  offered: number;
  timeZone: string;
  asOfUtc: string;
  stages: Row[];
  trends: Row[];
  companies: Row[];
  roles: Row[];
  durations: (Row & { averageDays: number })[];
  interviews: Row[];
  tasks: Row[];
}
function Breakdown({ title, rows }: { title: string; rows: Row[] }) {
  const max = Math.max(1, ...rows.map((x) => x.count));
  return (
    <section className="analytics-panel" style={{ padding: 20, minWidth: 0 }}>
      <h2>{title}</h2>
      {rows.length === 0 ? (
        <p>No recorded data for this period.</p>
      ) : (
        <table style={{ width: "100%", tableLayout: "fixed" }}>
          <caption className="sr-only">{title}: recorded counts</caption>
          <thead>
            <tr>
              <th scope="col">Category</th>
              <th scope="col">Count</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((x, i) => (
              <tr key={`${x.label}-${i}`}>
                <th
                  scope="row"
                  style={{ overflowWrap: "anywhere", textAlign: "left" }}
                >
                  {x.label}
                </th>
                <td>
                  {x.count}
                  <div
                    aria-hidden="true"
                    style={{
                      height: 6,
                      width: `${(x.count / max) * 100}%`,
                      background: "var(--accent, #4169e1)",
                      borderRadius: 3,
                    }}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
export function AnalyticsPage() {
  const [query, setQuery] = useState("");
  const [attempt, retry] = useState(0);
  const [result, setResult] = useState<{
    key: string;
    data?: Analytics;
    error?: string;
  }>();
  const key = `${query}:${attempt}`;
  useEffect(() => {
    const c = new AbortController();
    request<Analytics>(`analytics${query}`, undefined, undefined, c.signal)
      .then((data) => {
        if (!c.signal.aborted) setResult({ key, data });
      })
      .catch((e) => {
        if (!c.signal.aborted) setResult({ key, error: failure(e) });
      });
    return () => c.abort();
  }, [query, key]);
  const current = result?.key === key ? result : undefined;
  const d = current?.data;
  return (
    <div className="editor-page analytics-page">
      <header className="page-heading">
        <p className="eyebrow">Your recorded progress</p>
        <h1>Analytics & reporting</h1>
        <p>Real application history, interviews and follow-up work.</p>
      </header>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          const f = new FormData(e.currentTarget);
          const p = new URLSearchParams();
          for (const [k, v] of f) if (v) p.set(k, String(v));
          setQuery(`?${p}`);
        }}
        style={{ display: "flex", flexWrap: "wrap", gap: 16, marginBlock: 24 }}
      >
        <label>
          From (inclusive)
          <input name="from" type="date" required />
        </label>
        <label>
          Through (inclusive)
          <input name="to" type="date" required />
        </label>
        <label>
          Time zone
          <input
            name="timeZone"
            defaultValue="UTC"
            required
            placeholder="Europe/Kyiv"
          />
        </label>
        <label>
          Trend interval
          <select name="interval">
            <option value="week">Weekly (Monday)</option>
            <option value="month">Monthly</option>
          </select>
        </label>
        <button type="submit">Apply filters</button>
      </form>
      <p>
        Default: last 90 calendar days in UTC. Date boundaries use the selected
        time zone; range is limited to 732 days.
      </p>
      {!current && <p role="status">Loading analytics…</p>}
      {current?.error && (
        <div role="alert">
          <p>{current.error}</p>
          <button onClick={() => retry(attempt + 1)}>Retry</button>
        </div>
      )}
      {d && (
        <>
          <p>
            As of{" "}
            {new Date(d.asOfUtc).toLocaleString(undefined, {
              timeZone: d.timeZone,
            })}{" "}
            · {d.timeZone}
          </p>
          <p>
            Applications created{" "}
            {new Date(d.startUtc).toLocaleDateString(undefined, {
              timeZone: d.timeZone,
            })}{" "}
            through{" "}
            {new Date(
              new Date(d.endExclusiveUtc).getTime() - 1,
            ).toLocaleDateString(undefined, { timeZone: d.timeZone })}{" "}
            (inclusive).
          </p>
          {d.total === 0 && (
            <p>
              No applications created in this period. Add applications or choose
              another date range.
            </p>
          )}
          <section className="analytics-panel" style={{ padding: 20 }}>
            <h2>Application cohort</h2>
            <p>
              <strong>{d.total}</strong> created · <strong>{d.applied}</strong>{" "}
              ever applied
            </p>
            <p>
              Interview conversion:{" "}
              {d.applied
                ? `${((100 * d.interviewed) / d.applied).toFixed(1)}%`
                : "N/A"}{" "}
              ({d.interviewed}/{d.applied}). Offer conversion:{" "}
              {d.applied
                ? `${((100 * d.offered) / d.applied).toFixed(1)}%`
                : "N/A"}{" "}
              ({d.offered}/{d.applied}).
            </p>
            <p>
              Denominator: cohort applications with a recorded applied date.
              Interview numerator: distinct applied applications with an
              Interview history event or a scheduled interview. Offer numerator:
              distinct applied applications with an Offer history event. These
              measure recorded progression through today, including future
              scheduled interviews.
            </p>
          </section>
          <div
            style={{
              display: "grid",
              gridTemplateColumns:
                "repeat(auto-fit,minmax(min(100%,320px),1fr))",
              gap: 20,
              marginBlock: 24,
            }}
          >
            <Breakdown title="Current stages & outcomes" rows={d.stages} />
            <Breakdown title="Applications created over time" rows={d.trends} />
            <Breakdown title="Companies (top 20)" rows={d.companies} />
            <Breakdown title="Role titles (top 20)" rows={d.roles} />
            <Breakdown
              title="Interviews scheduled in period"
              rows={d.interviews}
            />
            <Breakdown title="Tasks created in period" rows={d.tasks} />
          </div>
          <section className="analytics-panel" style={{ padding: 20 }}>
            <h2>Completed stage visits</h2>
            <p>
              Average elapsed days between consecutive immutable history events,
              across this application cohort. Repeated visits count separately.
              Current open visits and missing history are excluded; no history
              is reconstructed.
            </p>
            {d.durations.length === 0 ? (
              <p>No completed stage visits recorded.</p>
            ) : (
              <table style={{ width: "100%" }}>
                <thead>
                  <tr>
                    <th scope="col">Stage</th>
                    <th scope="col">Visits</th>
                    <th scope="col">Average days</th>
                  </tr>
                </thead>
                <tbody>
                  {d.durations.map((x) => (
                    <tr key={x.label}>
                      <th scope="row">{x.label}</th>
                      <td>{x.count}</td>
                      <td>{x.averageDays.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
          <p>
            Task status is current; reopening removes completion. Past
            interviews do not imply attendance or success. Outcomes are free
            text and are not inferred. Company/role labels reflect current
            records. Top lists may omit smaller groups.
          </p>
        </>
      )}
    </div>
  );
}
