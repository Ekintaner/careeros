import assert from "node:assert/strict";
import { test } from "node:test";

const origin = process.env.CAREEROS_PREVIEW_URL;
assert.match(
  origin ?? "",
  /^http:\/\/localhost:\d+$/,
  "Set CAREEROS_PREVIEW_URL to the running local production preview",
);
for (const cookie of [undefined, "careeros-access=invalid-test-token"]) {
  test(`server preserves deep link before rendering with ${cookie ? "invalid" : "no"} access cookie`, async () => {
    const response = await fetch(
      origin +
        "/interviews/00000000-0000-0000-0000-000000000014?source=http-regression",
      {
        redirect: "manual",
        headers: {
          "x-careeros-path": "//attacker.example",
          ...(cookie ? { Cookie: cookie } : {}),
        },
      },
    );
    assert.equal(response.status, 307);
    const destination = new URL(response.headers.get("location"), origin);
    assert.equal(destination.origin, origin);
    assert.equal(destination.pathname, "/session");
    assert.equal(
      destination.searchParams.get("returnTo"),
      "/interviews/00000000-0000-0000-0000-000000000014?source=http-regression",
    );
    assert.equal(response.headers.get("x-content-type-options"), "nosniff");
  });
}
test("actual same-origin proxy retains backend CSRF protection", async () => {
  const response = await fetch(origin + "/api/v1/auth/refresh", {
    method: "POST",
    headers: { "X-CareerOS-CSRF": "1", Origin: "https://attacker.example" },
  });
  assert.equal(response.status, 403);
  assert.equal(response.headers.get("cache-control"), "no-store");
});
