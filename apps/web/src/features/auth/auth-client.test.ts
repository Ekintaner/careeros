import { describe, expect, it, vi } from "vitest";
import { createAuthClient } from "./auth-client";
const unauthorized = () =>
  Response.json({ title: "Not authenticated" }, { status: 401 });
describe("credential refresh", () => {
  it("coalesces concurrent 401s and retries with cookies and CSRF header", async () => {
    let renewed = false;
    let rotations = 0;
    const transport = vi.fn<typeof fetch>(async (url, init) => {
      expect(init?.credentials).toBe("same-origin");
      expect(new Headers(init?.headers).get("X-CareerOS-CSRF")).toBe("1");
      if (String(url).endsWith("/refresh")) {
        rotations++;
        await Promise.resolve();
        renewed = true;
        return Response.json({ id: "a" });
      }
      return renewed ? Response.json({ id: "a" }) : unauthorized();
    });
    const client = createAuthClient("http://localhost", transport);
    const results = await Promise.all([
      client.request("/api/v1/auth/me"),
      client.request("/api/v1/auth/me"),
    ]);
    expect(rotations).toBe(1);
    expect(results).toEqual([{ id: "a" }, { id: "a" }]);
  });
  it("ends authentication on rejected refresh without looping", async () => {
    const transport = vi.fn<typeof fetch>(async () => unauthorized());
    const client = createAuthClient("http://localhost", transport);
    const expired = vi.fn();
    client.setOnExpired(expired);
    await expect(client.request("/api/v1/auth/me")).rejects.toMatchObject({
      status: 401,
    });
    expect(expired).toHaveBeenCalledOnce();
    expect(transport).toHaveBeenCalledTimes(3);
  });
  it("does not refresh or replay invalid login or failed logout", async () => {
    const transport = vi.fn<typeof fetch>(async () => unauthorized());
    const client = createAuthClient("http://localhost", transport);
    await expect(
      client.submit("login", { password: "incorrect" }),
    ).rejects.toMatchObject({ status: 401 });
    expect(transport).toHaveBeenCalledTimes(1);
  });
  it("preserves a network failure instead of assuming logout", async () => {
    const transport = vi.fn<typeof fetch>(async () => {
      throw new TypeError("offline");
    });
    const client = createAuthClient("http://localhost", transport);
    const expired = vi.fn();
    client.setOnExpired(expired);
    await expect(client.request("/api/v1/auth/me")).rejects.toMatchObject({
      name: "NetworkError",
    });
    expect(expired).not.toHaveBeenCalled();
  });
});
