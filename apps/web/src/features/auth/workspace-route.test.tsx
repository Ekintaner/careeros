import { expect, it, vi } from "vitest";
import WorkspaceLayout from "@/app/(workspace)/layout";
const account = vi.hoisted(() => vi.fn());
vi.mock("next/headers", () => ({
  headers: async () =>
    new Headers({ "x-careeros-path": "/jobs/owned?view=details" }),
}));
vi.mock("@/lib/server-account", () => ({ serverAccount: account }));
vi.mock("next/navigation", () => ({
  redirect: (path: string) => {
    throw new Error(`redirect:${path}`);
  },
}));
it("server layout redirects before rendering any workspace for anonymous requests", async () => {
  account.mockResolvedValue(null);
  await expect(WorkspaceLayout({ children: <p>Private</p> })).rejects.toThrow(
    "redirect:/session?returnTo=%2Fjobs%2Fowned%3Fview%3Ddetails",
  );
});
it("server layout fails closed when account validation is unavailable", async () => {
  account.mockRejectedValue(new Error("unavailable"));
  await expect(WorkspaceLayout({ children: <p>Private</p> })).rejects.toThrow(
    "unavailable",
  );
});
