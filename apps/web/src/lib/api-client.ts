export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: ProblemDetails,
  ) {
    super(problem.title ?? `Request failed (${status})`);
    this.name = "ApiError";
  }
}

export class NetworkError extends Error {
  constructor(cause: unknown) {
    super("The API could not be reached.", { cause });
    this.name = "NetworkError";
  }
}

export function createApiClient(
  baseUrl: string,
  transport: typeof fetch = fetch,
) {
  const base = new URL(baseUrl);
  if (
    !["http:", "https:"].includes(base.protocol) ||
    base.username ||
    base.password ||
    base.search ||
    base.hash
  ) {
    throw new Error(
      "API base URL must be an HTTP(S) URL without credentials, query or fragment.",
    );
  }
  return {
    async request<T>(
      path: string,
      init: RequestInit = {},
    ): Promise<T | undefined> {
      if (!path.startsWith("/") || path.startsWith("//") || path.includes("\\"))
        throw new Error("API paths must be relative to the configured origin.");
      const url = new URL(path, base);
      if (url.origin !== base.origin)
        throw new Error("Cross-origin API path rejected.");
      let response: Response;
      try {
        response = await transport(url, {
          ...init,
          headers: {
            Accept: "application/json",
            ...Object.fromEntries(new Headers(init.headers)),
          },
        });
      } catch (error) {
        if (init.signal?.aborted) throw error;
        throw new NetworkError(error);
      }
      if (!response.ok) {
        let problem: ProblemDetails = {};
        try {
          const body: unknown = await response.json();
          if (body && typeof body === "object") {
            const candidate = body as Record<string, unknown>;
            problem = {
              title:
                typeof candidate.title === "string"
                  ? candidate.title
                  : undefined,
              detail:
                typeof candidate.detail === "string"
                  ? candidate.detail
                  : undefined,
              traceId:
                typeof candidate.traceId === "string"
                  ? candidate.traceId
                  : undefined,
            };
          }
        } catch {
          /* Non-JSON failures retain the HTTP status. */
        }
        throw new ApiError(response.status, problem);
      }
      if (response.status === 204) return undefined;
      return response.json() as Promise<T>;
    },
  };
}

export function getApiClient() {
  if (typeof window === "undefined")
    throw new Error("Use a server-only client on the server.");
  return createApiClient(window.location.origin);
}
