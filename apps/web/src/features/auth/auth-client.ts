import { ApiError, createApiClient } from "@/lib/api-client";
export interface Account {
  id: string;
  email: string;
  displayName: string;
  emailVerified: boolean;
  createdAtUtc: string;
}

export function createAuthClient(
  baseUrl: string,
  transport: typeof fetch = fetch,
) {
  const api = createApiClient(baseUrl, transport);
  let refreshing: Promise<void> | undefined;
  let revision = 0;
  let onExpired = () => {};
  async function raw<T>(path: string, init: RequestInit = {}) {
    return api.request<T>(path, {
      ...init,
      credentials: "same-origin",
      cache: "no-store",
      headers: {
        "X-CareerOS-CSRF": "1",
        ...Object.fromEntries(new Headers(init.headers)),
      },
    });
  }
  async function refresh() {
    if (!refreshing) {
      refreshing = (async () => {
        // Web Locks coordinate rotating cookies across tabs. Server replay detection remains strict.
        const rotate = async () => {
          try {
            await raw<Account>("/api/v1/auth/me");
            return;
          } catch (error) {
            if (!(error instanceof ApiError && error.status === 401))
              throw error;
          }
          await raw<Account>("/api/v1/auth/refresh", { method: "POST" });
        };
        if (typeof navigator !== "undefined" && navigator.locks)
          await navigator.locks.request("careeros-session", rotate);
        else await rotate();
        revision++;
      })()
        .catch((error) => {
          if (error instanceof ApiError && error.status === 401) onExpired();
          throw error;
        })
        .finally(() => {
          refreshing = undefined;
        });
    }
    return refreshing;
  }
  return {
    setOnExpired(callback: () => void) {
      onExpired = callback;
    },
    refresh,
    async request<T>(
      path: string,
      init: RequestInit = {},
    ): Promise<T | undefined> {
      const started = revision;
      try {
        return await raw<T>(path, init);
      } catch (error) {
        if (!(error instanceof ApiError && error.status === 401)) throw error;
        if (started === revision) await refresh();
        try {
          return await raw<T>(path, init);
        } catch (retryError) {
          if (retryError instanceof ApiError && retryError.status === 401)
            onExpired();
          throw retryError;
        }
      }
    },
    async submit<T>(operation: string, body?: unknown) {
      // Auth mutations do not retry: invalid credentials are not an expired session.
      return raw<T>(`/api/v1/auth/${operation}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    },
  };
}
let singleton: ReturnType<typeof createAuthClient> | undefined;
export function authClient() {
  return (singleton ??= createAuthClient(window.location.origin));
}
