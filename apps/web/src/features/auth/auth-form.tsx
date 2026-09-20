"use client";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api-client";
import { authClient, type Account } from "./auth-client";
import { useAuth } from "./auth-provider";
import { validateAuth, type AuthMode } from "./validation";
const copy = {
  login: ["Welcome back", "Sign in to your CareerOS workspace.", "Sign in"],
  register: [
    "Make room for what’s next",
    "Create your personal job-search workspace.",
    "Create account",
  ],
  "forgot-password": [
    "Let’s get you back in",
    "Enter the email address you use for CareerOS.",
    "Request reset link",
  ],
  "reset-password": [
    "A fresh start",
    "Choose a new password for your account.",
    "Reset password",
  ],
};
export function AuthForm({ mode }: { mode: AuthMode }) {
  const { account, status, accept, reload } = useAuth();
  const router = useRouter();
  const [values, setValues] = useState({
    email: "",
    password: "",
    displayName: "",
    confirmation: "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [visible, setVisible] = useState(false);
  const token = useRef("");
  useEffect(() => {
    if (mode === "reset-password") {
      const fragment = new URLSearchParams(window.location.hash.slice(1));
      if (fragment.has("token")) token.current = fragment.get("token") ?? "";
      window.history.replaceState(null, "", window.location.pathname);
    }
  }, [mode]);
  useEffect(() => {
    if (account && (mode === "login" || mode === "register")) {
      router.replace("/");
      router.refresh();
    }
  }, [account, mode, router]);
  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const found = validateAuth(mode, values);
    setErrors(found);
    setError("");
    if (Object.keys(found).length) return;
    if (mode === "reset-password" && !token.current) {
      setError("This reset link is missing or invalid. Request a new one.");
      return;
    }
    setBusy(true);
    try {
      if (mode === "login" || mode === "register") {
        const user = await authClient().submit<Account>(mode, {
          email: values.email.trim(),
          password: values.password,
          ...(mode === "register"
            ? { displayName: values.displayName.trim() }
            : {}),
        });
        if (!user) throw new Error("Missing account");
        accept(user);
        router.replace("/");
        router.refresh();
      } else {
        await authClient().submit(
          mode,
          mode === "forgot-password"
            ? { email: values.email.trim() }
            : { token: token.current, newPassword: values.password },
        );
        setDone(true);
        token.current = "";
        setValues({
          email: "",
          password: "",
          confirmation: "",
          displayName: "",
        });
        if (mode === "reset-password") void reload();
      }
    } catch (failure) {
      setError(
        failure instanceof ApiError && failure.status < 500
          ? failure.message
          : failure instanceof ApiError && failure.status === 503
            ? "Password recovery is currently unavailable. Please try again later."
            : "We couldn’t reach your account service. Please try again.",
      );
    } finally {
      setBusy(false);
    }
  }
  function field(
    name: keyof typeof values,
    label: string,
    type: string,
    autoComplete: string,
    maxLength: number,
  ) {
    return (
      <div className="auth-field">
        <label htmlFor={name}>{label}</label>
        <input
          id={name}
          name={name}
          type={type}
          autoComplete={autoComplete}
          maxLength={maxLength}
          value={values[name]}
          disabled={busy}
          aria-invalid={!!errors[name]}
          aria-describedby={
            errors[name]
              ? `${name}-error`
              : name === "password" && mode !== "login"
                ? "password-hint"
                : undefined
          }
          onChange={(event) =>
            setValues({ ...values, [name]: event.target.value })
          }
        />
        {errors[name] && (
          <p className="field-error" id={`${name}-error`}>
            {errors[name]}
          </p>
        )}
      </div>
    );
  }
  return (
    <main className="auth-page">
      <header className="auth-brand">
        <Link href="/login" className="brand">
          <span className="brand-mark">
            C<span>↗</span>
          </span>
          CareerOS
        </Link>
        <span>Your next chapter, organized.</span>
      </header>
      <div className="auth-layout">
        <aside className="auth-aside">
          <span className="eyebrow">A little more clarity</span>
          <h2>
            One place.
            <br />
            Your next move.
          </h2>
          <p>
            A focused space for the opportunities, conversations and decisions
            ahead.
          </p>
          <div className="auth-rule" />
          <span className="section-number">YOUR WORKSPACE / YOUR PACE</span>
        </aside>
        <section className="auth-card" aria-labelledby="auth-title">
          <span className="eyebrow">
            {mode === "register" ? "Get started" : "Your account"}
          </span>
          <h1 id="auth-title">{copy[mode][0]}</h1>
          <p className="auth-intro">{copy[mode][1]}</p>
          {done ? (
            <div role="status" className="auth-success">
              <h2>
                {mode === "forgot-password"
                  ? "Request received"
                  : "Password updated"}
              </h2>
              <p>
                {mode === "forgot-password"
                  ? "If an account matches that address, recovery instructions will be available through the configured delivery channel."
                  : "Sign in with your new password. Your previous sessions have been signed out."}
              </p>
              <Link href="/login">Back to sign in →</Link>
            </div>
          ) : (
            <form onSubmit={submit} noValidate>
              {mode === "register" &&
                field("displayName", "Your name", "text", "name", 120)}
              {mode !== "reset-password" &&
                field("email", "Email address", "email", "email", 254)}
              {mode !== "forgot-password" && (
                <>
                  {field(
                    "password",
                    mode === "reset-password" ? "New password" : "Password",
                    visible ? "text" : "password",
                    mode === "login" ? "current-password" : "new-password",
                    128,
                  )}
                  <div className="password-tools">
                    <button
                      type="button"
                      aria-pressed={visible}
                      onClick={() => setVisible(!visible)}
                    >
                      {visible ? "Hide password" : "Show password"}
                    </button>
                    {mode === "login" && (
                      <Link href="/forgot-password">Forgot password?</Link>
                    )}
                  </div>
                  {mode !== "login" && (
                    <>
                      <p id="password-hint" className="field-hint">
                        At least 15 characters. A few memorable words work well.
                      </p>
                      {field(
                        "confirmation",
                        "Confirm password",
                        visible ? "text" : "password",
                        "new-password",
                        128,
                      )}
                    </>
                  )}
                </>
              )}
              {error && (
                <p role="alert" className="auth-error">
                  {error}
                </p>
              )}
              {status === "error" && !error && (
                <p role="status" className="field-hint">
                  Session checking is unavailable. You can try signing in again.
                </p>
              )}
              <button
                className="auth-submit"
                type="submit"
                disabled={busy || status === "loading"}
              >
                {busy ? "Please wait…" : copy[mode][2]}
                <span aria-hidden="true">↗</span>
              </button>
            </form>
          )}
          {!done && (
            <p className="auth-switch">
              {mode === "login" ? (
                <>
                  New to CareerOS?{" "}
                  <Link href="/register">Create an account</Link>
                </>
              ) : (
                <Link href="/login">Back to sign in</Link>
              )}
            </p>
          )}
        </section>
      </div>
      <footer className="auth-footer">
        CareerOS <span>A considered next step.</span>
      </footer>
    </main>
  );
}
