"use client";
import { useState } from "react";
import Link from "next/link";
import { useResource } from "@/features/opportunities/lists";
import { Feedback } from "@/features/profile/form-ui";
export interface Dashboard {
  asOfUtc: string;
  totalApplications: number;
  activeApplications: number;
  offers: number;
  savedJobs: number;
  openTasks: number;
  overdueTasks: number;
  upcomingInterviews: number;
  recentApplications: {
    id: string;
    title: string;
    company: string;
    status: string;
  }[];
  nextInterviews: {
    id: string;
    title: string;
    company: string;
    startUtc: string;
    endUtc: string;
  }[];
  nextTasks: {
    id: string;
    title: string;
    dueUtc: string | null;
    priority: string;
  }[];
}
const local = (value: string) =>
  new Date(value).toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });
export function DashboardPage() {
  const [attempt, retry] = useState(0);
  const result = useResource<Dashboard>("dashboard", attempt);
  const d = result?.data;
  return (
    <div className="editor-page dashboard-page">
      <header className="page-heading">
        <div>
          <p className="eyebrow">Your workspace</p>
          <h1>Overview</h1>
          <p>What is moving forward, and what needs your attention.</p>
        </div>
        <Link className="primary-button" href="/applications/new">
          Track application
        </Link>
      </header>
      <nav className="quick-actions" aria-label="Quick actions">
        <Link className="secondary-button" href="/jobs/new">
          Save a job
        </Link>
        <Link className="secondary-button" href="/tasks/new">
          Add a task
        </Link>
        <Link className="secondary-button" href="/interviews/new">
          Schedule interview
        </Link>
        <Link className="secondary-button" href="/resumes">
          Manage resumes
        </Link>
      </nav>
      {!result && (
        <p role="status" className="loading-state">
          Loading your workspace…
        </p>
      )}
      {result?.error && (
        <div className="state-panel">
          <Feedback error={result.error} />
          <button
            className="secondary-button"
            onClick={() => retry(attempt + 1)}
          >
            Reload overview
          </button>
        </div>
      )}
      {d && (
        <>
          <p className="field-hint">
            Current records · updated {local(d.asOfUtc)} · times shown in your
            device time zone
          </p>
          <section
            className="dashboard-metrics"
            aria-label="Current workspace counts"
          >
            {[
              {
                title: "Active applications",
                value: d.activeApplications,
                detail: `${d.totalApplications} total, including outcomes and archived`,
                href: "/applications",
              },
              {
                title: "Current offers",
                value: d.offers,
                detail: "Applications currently in Offer",
                href: "/applications",
              },
              {
                title: "Open tasks",
                value: d.openTasks,
                detail: `${d.overdueTasks} overdue`,
                href: "/tasks",
              },
              {
                title: "Upcoming interviews",
                value: d.upcomingInterviews,
                detail: "Includes interviews in progress",
                href: "/interviews",
              },
            ].map((x) => (
              <Link className="metric-card" key={x.title} href={x.href}>
                <span>{x.title}</span>
                <strong>{x.value}</strong>
                <small>{x.detail}</small>
              </Link>
            ))}
          </section>
          {d.totalApplications === 0 && (
            <section className="state-panel getting-started">
              <h2>Your next step starts here</h2>
              <p>
                {d.savedJobs
                  ? `${d.savedJobs} unarchived saved ${d.savedJobs === 1 ? "job is" : "jobs are"} ready to review. Track an application when you choose to pursue one.`
                  : "Save an opportunity, then track your application and next steps. Your actual activity will appear here."}
              </p>
              <Link className="back-link" href="/jobs">
                Review saved jobs →
              </Link>
            </section>
          )}
          <div className="dashboard-sections">
            <section className="workspace-panel">
              <div className="panel-heading">
                <h2>Recent applications</h2>
                <Link href="/applications">View pipeline →</Link>
              </div>
              {d.recentApplications.length ? (
                <ul className="activity-list">
                  {d.recentApplications.map((a) => (
                    <li key={a.id}>
                      <Link href={`/applications/${a.id}`}>
                        <strong>{a.title}</strong>
                        <span>{a.company}</span>
                      </Link>
                      <span className="application-status">{a.status}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="empty-state">
                  No applications yet. Track an existing job to begin.
                </p>
              )}
              <Link className="back-link" href="/applications">
                Open your application pipeline →
              </Link>
            </section>
            <section className="workspace-panel">
              <div className="panel-heading">
                <h2>Next interviews</h2>
                <Link href="/interviews">View schedule →</Link>
              </div>
              {d.nextInterviews.length ? (
                <ul className="activity-list">
                  {d.nextInterviews.map((i) => (
                    <li key={i.id}>
                      <Link href={`/interviews/${i.id}`}>
                        <strong>{i.title}</strong>
                        <span>{i.company}</span>
                        <time dateTime={i.startUtc}>{local(i.startUtc)}</time>
                      </Link>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="empty-state">
                  No upcoming interviews recorded. Schedule one when you have a
                  confirmed conversation.
                </p>
              )}
            </section>
            <section className="workspace-panel">
              <div className="panel-heading">
                <h2>Follow-up tasks</h2>
                <Link href="/tasks">View tasks →</Link>
              </div>
              {d.nextTasks.length ? (
                <ul className="activity-list">
                  {d.nextTasks.map((t) => (
                    <li key={t.id}>
                      <Link href={`/tasks/${t.id}`}>
                        <strong>{t.title}</strong>
                        <span>
                          {t.dueUtc ? `Due ${local(t.dueUtc)}` : "No due date"}
                        </span>
                      </Link>
                      <span className="outline-label">
                        {t.priority} priority
                      </span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="empty-state">
                  No open tasks. Add a follow-up when you need one.
                </p>
              )}
            </section>
            <section className="workspace-panel">
              <h2>Make your next move informed</h2>
              <p>
                Review recorded trends, confirm your skill evidence and prepare
                for the opportunities you saved.
              </p>
              <div className="dashboard-shortcuts">
                <Link href="/analytics">Analytics & reporting →</Link>
                <Link href="/skills">Skills Intelligence →</Link>
                <Link href="/profile">Career profile →</Link>
                <Link href="/jobs">Job matching & optional local AI →</Link>
              </div>
            </section>
          </div>
        </>
      )}
    </div>
  );
}
