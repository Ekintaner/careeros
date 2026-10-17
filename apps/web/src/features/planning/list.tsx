"use client";
import { useState } from "react";
import Link from "next/link";
import { Feedback, Field } from "@/features/profile/form-ui";
import { useResource } from "@/features/opportunities/lists";
import { Pagination } from "@/features/opportunities/shared";
import { type Page } from "@/features/applications/client";
import { timestamp, type Interview, type CareerTask } from "./types";

export function PlanningList({
  tasks = false,
  applicationId,
}: {
  tasks?: boolean;
  applicationId?: string;
}) {
  const root = tasks ? "tasks" : "interviews";
  const [view, setView] = useState(tasks ? "open" : "upcoming");
  const [page, setPage] = useState(1);
  const [attempt, setAttempt] = useState(0);
  const data = useResource<Page<CareerTask | Interview>>(
    `${root}?view=${view}&page=${page}${applicationId ? `&applicationId=${encodeURIComponent(applicationId)}` : ""}`,
    attempt,
  );
  return (
    <section className="planning-page">
      <header className="page-header">
        <div>
          <p className="eyebrow">Your next step</p>
          <h1>{tasks ? "Career tasks" : "Interviews"}</h1>
          <p>
            {tasks
              ? "Follow up, prepare and keep your commitments visible."
              : "Prepare for real conversations and keep private notes."}
          </p>
        </div>
        <Link
          className="primary-button"
          href={`/${root}/new${applicationId ? `?applicationId=${applicationId}` : ""}`}
        >
          {tasks ? "New task" : "Schedule interview"}
        </Link>
      </header>
      <p className="field-hint">
        Times shown in your device timezone. Upcoming tasks are due within seven
        days.
      </p>
      <Field id={`${root}-view`} title="View">
        <select
          id={`${root}-view`}
          value={view}
          onChange={(e) => {
            setView(e.target.value);
            setPage(1);
          }}
        >
          {(tasks
            ? ["open", "overdue", "upcoming", "completed", "all"]
            : ["upcoming", "past", "all"]
          ).map((x) => (
            <option key={x} value={x}>
              {x.charAt(0).toUpperCase() + x.slice(1)}
            </option>
          ))}
        </select>
      </Field>
      <Feedback error={data?.error} />
      <button
        type="button"
        className="text-button"
        onClick={() => setAttempt((n) => n + 1)}
      >
        Refresh
      </button>
      {!data ? (
        <p role="status">Loading…</p>
      ) : (
        data.data && (
          <>
            {!data.data.items.length && (
              <p>No {tasks ? "tasks" : "interviews"} in this view.</p>
            )}
            <div className="planning-grid">
              {data.data.items.map((x) => (
                <article className="planning-card" key={x.id}>
                  <h2>
                    <Link href={`/${root}/${x.id}`}>
                      {"title" in x ? x.title : `${x.type} interview`}
                    </Link>
                  </h2>
                  {"title" in x ? (
                    <>
                      <p>
                        {x.priority} priority ·{" "}
                        {x.completedAtUtc ? "Completed" : "Open"}
                      </p>
                      <p>Due: {timestamp(x.dueAtUtc)}</p>
                    </>
                  ) : (
                    <>
                      <p>
                        {timestamp(x.scheduledStartUtc)} –{" "}
                        {timestamp(x.scheduledEndUtc)}
                      </p>
                      <p>{x.location}</p>
                    </>
                  )}
                  {x.applicationId && (
                    <Link href={`/applications/${x.applicationId}`}>
                      View application
                    </Link>
                  )}
                </article>
              ))}
            </div>
            <Pagination
              page={page}
              pages={data.data.totalPages}
              count={data.data.totalCount}
              onPage={setPage}
            />
          </>
        )
      )}
    </section>
  );
}
