import { expect, it } from "vitest";
import { safeReturnTo, loginDestination } from "./return-to";
import { proxy } from "@/proxy";
import { NextRequest } from "next/server";

it.each([
  "https://evil.example",
  "//evil.example",
  "/\\evil.example",
  "/login",
  "/session?returnTo=/session",
  "/api/v1/auth/logout",
  "/jobs/../../login",
  "/jobs/%2f%2fevil",
  "/jobs/\nsecret",
  "/jobs/\x7fsecret",
  null,
  ["/jobs"],
])("rejects unsafe or looping destinations: %s", (value) => {
  expect(safeReturnTo(value)).toBe("/");
});
it("preserves detail, query and fragment and encodes a single login parameter", () => {
  const target = "/jobs/owned?search=a%20b&state=all#notes";
  expect(safeReturnTo(target)).toBe(target);
  expect(loginDestination(target)).toBe(
    `/login?returnTo=${encodeURIComponent(target)}`,
  );
  expect(loginDestination(target, "/login?password=changed")).toContain(
    "changed&returnTo=",
  );
});
it("overwrites a forged routing header with the actual URL", () => {
  const result = proxy(
    new NextRequest("http://localhost/tasks/owned?view=all", {
      headers: { "x-careeros-path": "//evil.example" },
    }),
  );
  expect(result.headers.get("x-middleware-request-x-careeros-path")).toBe(
    "/tasks/owned?view=all",
  );
});
