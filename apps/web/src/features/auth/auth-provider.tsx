"use client";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from "react";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api-client";
import { authClient, type Account } from "./auth-client";

type AuthState = {
  account: Account | null;
  status: "loading" | "ready" | "error";
  reload: () => Promise<void>;
  accept: (account: Account) => void;
  logout: () => Promise<void>;
};
const AuthContext = createContext<AuthState | null>(null);
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [account, setAccount] = useState<Account | null>(null);
  const [status, setStatus] = useState<AuthState["status"]>("loading");
  const router = useRouter();
  const channel = useRef<BroadcastChannel | null>(null);
  const generation = useRef(0);
  const reload = useCallback(() => {
    const version = ++generation.current;
    return authClient()
      .request<Account>("/api/v1/auth/me")
      .then(
        (user) => {
          if (generation.current === version) {
            setAccount(user ?? null);
            setStatus("ready");
          }
        },
        (error) => {
          if (generation.current === version) {
            setAccount(null);
            setStatus(
              error instanceof ApiError && error.status === 401
                ? "ready"
                : "error",
            );
          }
        },
      );
  }, []);
  useEffect(() => {
    authClient().setOnExpired(() => {
      setAccount(null);
      setStatus("ready");
    });
    void reload();
    channel.current =
      typeof BroadcastChannel !== "undefined"
        ? new BroadcastChannel("careeros-auth")
        : null;
    if (channel.current)
      channel.current.onmessage = () => {
        void reload();
      };
    const counter = generation;
    return () => {
      counter.current++;
      channel.current?.close();
      channel.current = null;
      authClient().setOnExpired(() => {});
    };
  }, [reload]);
  function accept(value: Account) {
    generation.current++;
    setAccount(value);
    setStatus("ready");
  }
  async function logout() {
    await authClient().submit("logout");
    generation.current++;
    setAccount(null);
    setStatus("ready");
    channel.current?.postMessage("changed");
    router.replace("/login");
    router.refresh();
  }
  return (
    <AuthContext.Provider value={{ account, status, reload, accept, logout }}>
      {children}
    </AuthContext.Provider>
  );
}
export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error("AuthProvider is required.");
  return value;
}
export function AuthGate({ children }: { children: React.ReactNode }) {
  const { account, status, reload } = useAuth();
  const router = useRouter();
  useEffect(() => {
    if (status === "ready" && !account) router.replace("/login");
  }, [account, status, router]);
  if (status === "error")
    return (
      <main className="session-state">
        <h1>We couldn’t open your workspace</h1>
        <p>The account service may be unavailable.</p>
        <button onClick={() => void reload()}>Try again</button>
      </main>
    );
  if (status !== "ready" || !account)
    return (
      <main className="session-state" role="status">
        Opening your workspace…
      </main>
    );
  return children;
}
export function SessionRecovery() {
  const { account, status, reload } = useAuth();
  const router = useRouter();
  useEffect(() => {
    if (status === "ready") {
      router.replace(account ? "/" : "/login");
      router.refresh();
    }
  }, [account, status, router]);
  return (
    <main className="session-state">
      <p role="status">
        {status === "error"
          ? "Your account service is unavailable."
          : "Checking your session…"}
      </p>
      {status === "error" && (
        <button onClick={() => void reload()}>Try again</button>
      )}
    </main>
  );
}
export function AccountMenu() {
  const { account, logout } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);
  return (
    <div className="account-menu">
      <span>
        <strong>{account?.displayName}</strong>
        <small>{account?.email}</small>
      </span>
      <button
        disabled={busy}
        onClick={async () => {
          setBusy(true);
          setError(false);
          try {
            await logout();
          } catch {
            setError(true);
          } finally {
            setBusy(false);
          }
        }}
      >
        {busy ? "Signing out…" : "Sign out"}
      </button>
      {error && <span role="alert">Couldn’t sign out. Try again.</span>}
    </div>
  );
}
