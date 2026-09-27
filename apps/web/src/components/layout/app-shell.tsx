import { AccountMenu } from "@/features/auth/auth-provider";
import Link from "next/link";
import { Icon, type IconName } from "@/components/ui/icon";
import { Breadcrumb, NavigationLink } from "./navigation-link";

const groups: { title: string; items: { label: string; icon: IconName }[] }[] =
  [
    {
      title: "Workspace",
      items: [
        { label: "Applications", icon: "applications" },
        { label: "Jobs", icon: "jobs" },
        { label: "Companies", icon: "companies" },
        { label: "Interviews", icon: "interviews" },
      ],
    },
    {
      title: "Career",
      items: [
        { label: "Resumes", icon: "resumes" },
        { label: "Analytics", icon: "analytics" },
      ],
    },
    {
      title: "Personal",
      items: [
        { label: "Tasks", icon: "tasks" },
        { label: "Documents", icon: "documents" },
      ],
    },
  ];

function Navigation() {
  return (
    <>
      <NavigationLink href="/">
        <Icon name="overview" />
        Overview
      </NavigationLink>
      <NavigationLink href="/profile">
        <Icon name="skills" />
        Career profile
      </NavigationLink>
      {groups.map((group) => (
        <section
          className="nav-group"
          key={group.title}
          aria-label={group.title}
        >
          <h2>{group.title}</h2>
          {group.items.map((item) =>
            item.icon === "jobs" || item.icon === "companies" ? (
              <NavigationLink key={item.label} href={`/${item.icon}`}>
                <Icon name={item.icon} />
                {item.label}
              </NavigationLink>
            ) : (
              <button
                key={item.label}
                className="nav-item"
                disabled
                title="Planned for a future release"
              >
                <Icon name={item.icon} />
                {item.label}
              </button>
            ),
          )}
        </section>
      ))}
      <div className="nav-footer">
        <NavigationLink href="/settings">
          <Icon name="settings" />
          Settings
        </NavigationLink>
        <p>
          Your career workspace
          <br />
          <span>More tools coming soon</span>
        </p>
      </div>
    </>
  );
}

export function AppShell({ children }: { children: React.ReactNode }) {
  return (
    <>
      <a className="skip-link" href="#main-content">
        Skip to content
      </a>
      <aside className="sidebar">
        <Link className="brand" href="/" aria-label="CareerOS home">
          <span className="brand-mark">
            C<span>↗</span>
          </span>
          CareerOS
        </Link>
        <nav aria-label="Main navigation">
          <Navigation />
        </nav>
      </aside>
      <div className="workspace">
        <header className="topbar">
          <Breadcrumb />
          <AccountMenu />
        </header>
        <details className="mobile-nav">
          <summary>
            CareerOS <span>Menu</span>
          </summary>
          <nav aria-label="Mobile navigation">
            <Navigation />
            <AccountMenu />
          </nav>
        </details>
        <main id="main-content" tabIndex={-1}>
          {children}
        </main>
        <footer className="workspace-footer">
          <span>CareerOS</span>
          <span>A little more clarity. A better next step.</span>
        </footer>
      </div>
    </>
  );
}
