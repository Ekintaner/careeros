"use client";
import { useEffect, useState } from "react";
import { useAuth } from "@/features/auth/auth-provider";
import { authClient } from "@/features/auth/auth-client";
import { validateAuth } from "@/features/auth/validation";
import { failure, read, write, type AccountSettings } from "./profile-client";
import { Feedback, Field, useUnsavedChanges } from "./form-ui";

export function SettingsPage() {
  const [settings, setSettings] = useState<AccountSettings>();
  const [error, setError] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const abort = new AbortController();
    read<AccountSettings>("account", abort.signal)
      .then(setSettings)
      .catch(() => {
        if (!abort.signal.aborted) setError(true);
      });
    return () => abort.abort();
  }, [attempt]);
  return (
    <div className="editor-page">
      <header className="page-heading">
        <div>
          <p className="eyebrow">Your workspace</p>
          <h1>Settings</h1>
          <p>Your account details and sign-in security.</p>
        </div>
      </header>
      {settings ? (
        <>
          <AccountEditor settings={settings} onSaved={setSettings} />
          <PasswordEditor />
        </>
      ) : error ? (
        <div role="alert">
          <p>We couldn’t load your account settings.</p>
          <button
            className="secondary-button"
            onClick={() => {
              setError(false);
              setAttempt(attempt + 1);
            }}
          >
            Try again
          </button>
        </div>
      ) : (
        <p role="status">Loading account settings…</p>
      )}
    </div>
  );
}
function AccountEditor({
  settings,
  onSaved,
}: {
  settings: AccountSettings;
  onSaved: (s: AccountSettings) => void;
}) {
  const { accept } = useAuth();
  const [name, setName] = useState(settings.account.displayName);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const dirty = name !== settings.account.displayName;
  useUnsavedChanges(dirty);
  return (
    <section className="editor-section" aria-labelledby="account-title">
      <div className="section-intro">
        <span className="section-number">01 / Account</span>
        <h2 id="account-title">Personal details</h2>
        <p>
          Your name appears in your workspace. Your email is your sign-in
          address.
        </p>
      </div>
      <div className="section-fields">
        <form
          noValidate
          onSubmit={async (e) => {
            e.preventDefault();
            if (busy) return;
            setError("");
            setSuccess("");
            if (!name.trim() || name.trim().length > 120) {
              setError("Enter your name (up to 120 characters).");
              return;
            }
            setBusy(true);
            try {
              const result = await write<AccountSettings>("account", "PUT", {
                displayName: name,
                version: settings.version,
              });
              if (!result) throw new Error("Missing account");
              onSaved(result);
              setName(result.account.displayName);
              accept(result.account);
              setSuccess("Account updated.");
            } catch (e) {
              setError(failure(e));
            } finally {
              setBusy(false);
            }
          }}
        >
          <Field id="display-name" title="Display name">
            <input
              id="display-name"
              autoComplete="name"
              value={name}
              maxLength={120}
              disabled={busy}
              onChange={(e) => {
                setName(e.target.value);
                setSuccess("");
              }}
            />
          </Field>
          <dl className="account-details">
            <div>
              <dt>Email address</dt>
              <dd>{settings.account.email}</dd>
            </div>
            <div>
              <dt>Email verification</dt>
              <dd>
                {settings.account.emailVerified
                  ? "Verified"
                  : "Not verified · delivery is not available yet"}
              </dd>
            </div>
            <div>
              <dt>Account created</dt>
              <dd>
                {new Intl.DateTimeFormat(undefined, {
                  dateStyle: "long",
                  timeZone: "UTC",
                }).format(new Date(settings.account.createdAtUtc))}
              </dd>
            </div>
          </dl>
          <p className="field-hint">
            Email changes will be available with a secure verification flow.
          </p>
          <div className="editor-actions">
            <span className="field-hint">
              {dirty ? "You have unsaved changes." : ""}
            </span>
            <button
              type="button"
              className="secondary-button"
              disabled={busy || !dirty}
              onClick={() => setName(settings.account.displayName)}
            >
              Discard changes
            </button>
            <button className="primary-button" disabled={busy || !dirty}>
              {busy ? "Saving…" : "Save account"}
            </button>
          </div>
          <Feedback error={error} success={success} />
          {error && (
            <button
              type="button"
              className="text-button"
              disabled={busy}
              onClick={async () => {
                if (
                  dirty &&
                  !window.confirm(
                    "Discard your unsaved name and load the latest account?",
                  )
                )
                  return;
                setBusy(true);
                try {
                  const result = await read<AccountSettings>("account");
                  onSaved(result);
                  setName(result.account.displayName);
                  setError("");
                } catch {
                  setError("Couldn’t reload your account. Try again.");
                } finally {
                  setBusy(false);
                }
              }}
            >
              Reload latest account
            </button>
          )}
        </form>
      </div>
    </section>
  );
}
function PasswordEditor() {
  const { passwordChanged } = useAuth();
  const [values, setValues] = useState({
    currentPassword: "",
    password: "",
    confirmation: "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [visible, setVisible] = useState(false);
  async function save(event: React.FormEvent) {
    event.preventDefault();
    if (busy) return;
    const found = validateAuth("reset-password", {
      ...values,
      email: "",
      displayName: "",
    });
    if (!values.currentPassword)
      found.currentPassword = "Enter your current password.";
    setErrors(found);
    setError("");
    if (Object.keys(found).length) {
      document.getElementById(`security-${Object.keys(found)[0]}`)?.focus();
      return;
    }
    setBusy(true);
    try {
      // Renew an expired session before the credential operation; do not interpret a wrong password as session expiry.
      await authClient().request("/api/v1/auth/me");
      // Existing auth mutation deliberately does not retry invalid current credentials.
      await authClient().submit("change-password", {
        currentPassword: values.currentPassword,
        newPassword: values.password,
      });
      setValues({ currentPassword: "", password: "", confirmation: "" });
      passwordChanged();
    } catch (e) {
      setError(failure(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="editor-section" aria-labelledby="security-title">
      <div className="section-intro">
        <span className="section-number">02 / Security</span>
        <h2 id="security-title">Change password</h2>
        <p>
          Changing your password signs out every session, including this one.
        </p>
      </div>
      <div className="section-fields">
        <form onSubmit={save} noValidate>
          {(
            [
              ["currentPassword", "Current password"],
              ["password", "New password"],
              ["confirmation", "Confirm new password"],
            ] as const
          ).map(([key, title]) => (
            <Field
              key={key}
              id={`security-${key}`}
              title={title}
              error={errors[key]}
              hint={
                key === "password"
                  ? "Use 15–128 characters. A few memorable words work well."
                  : undefined
              }
            >
              <input
                id={`security-${key}`}
                type={visible ? "text" : "password"}
                autoComplete={
                  key === "currentPassword"
                    ? "current-password"
                    : "new-password"
                }
                maxLength={128}
                value={values[key]}
                disabled={busy}
                aria-invalid={!!errors[key]}
                aria-describedby={`security-${key}-hint`}
                onChange={(e) =>
                  setValues((v) => ({ ...v, [key]: e.target.value }))
                }
              />
            </Field>
          ))}
          <button
            type="button"
            className="text-button"
            aria-pressed={visible}
            onClick={() => setVisible(!visible)}
          >
            {visible ? "Hide passwords" : "Show passwords"}
          </button>
          <Feedback error={error} />
          <div className="editor-actions">
            <button className="primary-button" disabled={busy}>
              {busy ? "Changing password…" : "Change password"}
            </button>
          </div>
        </form>
      </div>
    </section>
  );
}
