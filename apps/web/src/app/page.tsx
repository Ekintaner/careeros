import { Icon } from "@/components/ui/icon";

export default function OverviewPage() {
  return (
    <div className="overview">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Your workspace</p>
          <h1>Overview</h1>
          <p>A clear view of your job search, all in one place.</p>
        </div>
        <span className="outline-label">Getting started</span>
      </div>
      <section className="welcome" aria-labelledby="welcome-title">
        <div className="welcome-copy">
          <span className="section-number">01 / A fresh start</span>
          <h2 id="welcome-title">
            Start building your
            <br className="desktop-break" /> job search workspace
          </h2>
          <p>
            Your applications, interviews and job-search insights will come
            together here as you build your workspace.
          </p>
          <div className="status-note">
            <span className="status-dot" />
            You’re viewing the foundation preview
          </div>
        </div>
        <div className="workspace-illustration" aria-hidden="true">
          <div className="illustration-tab" />
          <div className="illustration-sheet">
            <div className="illustration-heading">
              <span />
              <span />
            </div>
            <div className="illustration-line" />
            <div className="illustration-line short" />
            <div className="illustration-row">
              <i />
              <span />
            </div>
            <div className="illustration-row">
              <i />
              <span />
            </div>
            <div className="illustration-row">
              <i />
              <span />
            </div>
            <div className="illustration-stamp">↗</div>
          </div>
        </div>
      </section>
      <section className="workspace-sections" aria-labelledby="workspace-title">
        <div className="section-heading">
          <h2 id="workspace-title">Room for your next chapter</h2>
          <span>Built around your search</span>
        </div>
        <div className="empty-columns">
          <article>
            <Icon name="applications" size={22} />
            <h3>Application pipeline</h3>
            <p>Keep the path from first interest to final decision in view.</p>
            <div className="empty-message">No applications to display</div>
          </article>
          <article>
            <Icon name="interviews" size={22} />
            <h3>Upcoming interviews</h3>
            <p>Give every conversation the preparation it deserves.</p>
            <div className="empty-message">No interviews to display</div>
          </article>
          <article>
            <Icon name="analytics" size={22} />
            <h3>Job-search insights</h3>
            <p>Find patterns in your progress as your workspace grows.</p>
            <div className="empty-message">Insights need real activity</div>
          </article>
        </div>
      </section>
      <aside className="foundation-note">
        <span className="note-symbol">i</span>
        <p>
          <strong>A considered starting point.</strong> This preview establishes
          your workspace layout. Adding data, account access and the tools in
          the sidebar are planned for future releases.
        </p>
      </aside>
    </div>
  );
}
