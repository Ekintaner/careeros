import { describe, expect, it, vi } from "vitest";
import { ApiError, NetworkError, createApiClient } from "./api-client";

describe("API client", () => {
  it("decodes JSON and supports empty success responses", async () => {
    const transport = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(Response.json({ status: "Healthy" }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    const api = createApiClient("http://localhost:5080", transport);
    expect(await api.request("/health/live")).toEqual({ status: "Healthy" });
    expect(await api.request("/empty")).toBeUndefined();
  });
  it("preserves ProblemDetails and HTTP status", async () => {
    const api = createApiClient(
      "http://localhost:5080",
      vi
        .fn<typeof fetch>()
        .mockResolvedValue(
          Response.json(
            { title: "Unavailable", traceId: "test-id" },
            { status: 503 },
          ),
        ),
    );
    await expect(api.request("/health")).rejects.toMatchObject({
      name: "ApiError",
      status: 503,
      problem: { title: "Unavailable", traceId: "test-id" },
    });
  });
  it("handles non-JSON failures", async () => {
    const api = createApiClient(
      "http://localhost:5080",
      vi
        .fn<typeof fetch>()
        .mockResolvedValue(new Response("Bad gateway", { status: 502 })),
    );
    await expect(api.request("/health")).rejects.toBeInstanceOf(ApiError);
  });
  it("distinguishes network failures and rejects foreign URLs", async () => {
    const transport = vi
      .fn<typeof fetch>()
      .mockRejectedValue(new TypeError("offline"));
    const api = createApiClient("http://localhost:5080", transport);
    await expect(api.request("/health")).rejects.toBeInstanceOf(NetworkError);
    await expect(api.request("//example.com")).rejects.toThrow("relative");
    expect(transport).toHaveBeenCalledTimes(1);
  });
});
