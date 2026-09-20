import { expect, it, vi } from "vitest";
import WorkspaceLayout from "@/app/(workspace)/layout";
const account = vi.hoisted(() => vi.fn());
vi.mock("@/lib/server-account", () => ({ serverAccount: account }));
vi.mock("next/navigation", () => ({
  redirect: (path: string) => {
    throw new Error(`redirect:${path}`);
  },
}));
it("server layout redirects before rendering any workspace for anonymous requests", async () => {
  account.mockResolvedValue(null);
  await expect(WorkspaceLayout({ children: <p>Private</p> })).rejects.toThrow(
    "redirect:/session",
  );
});
it("server layout fails closed when account validation is unavailable", async () => {
  account.mockRejectedValue(new Error("unavailable"));
  await expect(WorkspaceLayout({ children: <p>Private</p> })).rejects.toThrow(
    "unavailable",
  );
});
