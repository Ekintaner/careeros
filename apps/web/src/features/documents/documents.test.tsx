import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { DocumentsPage, type DocumentItem } from "./documents-page";
import { ApiError } from "@/lib/api-client";
const mocks = vi.hoisted(() => ({ request: vi.fn() }));
vi.mock("@/features/auth/auth-client", () => ({ authClient: () => mocks }));
const item: DocumentItem = {
  id: "doc",
  name: "Engineering resume",
  originalFileName: "cv.pdf",
  contentType: "application/pdf",
  fileSizeBytes: 4096,
  type: "Resume",
  createdAtUtc: "2026-10-01T00:00:00Z",
  resumeId: "resume",
  isPrimary: false,
  applicationCount: 0,
};
const page = (items: DocumentItem[]) => ({
  items,
  page: 1,
  pageSize: 20,
  totalCount: items.length,
  totalPages: 1,
});
beforeEach(() => {
  mocks.request.mockReset();
  mocks.request.mockResolvedValue(page([item]));
});
it("renders metadata, download, primary selection and resume filtering", async () => {
  render(<DocumentsPage resumes />);
  expect(
    await screen.findByRole("heading", { name: "Engineering resume" }),
  ).toBeVisible();
  expect(
    screen.getByRole("link", { name: "Download Engineering resume" }),
  ).toHaveAttribute("href", "/api/v1/documents/doc/download");
  expect(mocks.request).toHaveBeenCalledWith(
    "/api/v1/documents?page=1&type=Resume",
    expect.anything(),
  );
  fireEvent.click(
    screen.getByRole("button", { name: "Make primary Engineering resume" }),
  );
  await waitFor(() =>
    expect(mocks.request).toHaveBeenCalledWith(
      "/api/v1/documents/resumes/resume/primary",
      expect.objectContaining({ method: "POST" }),
    ),
  );
});
it("uploads actual file bytes with metadata and resets form on success", async () => {
  render(<DocumentsPage />);
  await screen.findByRole("heading", { name: "Engineering resume" });
  fireEvent.change(screen.getByLabelText("Document name"), {
    target: { value: "My certificate" },
  });
  const file = new File(["%PDF-1.7\nbody\n%%EOF"], "certificate.pdf", {
    type: "application/pdf",
  });
  fireEvent.change(screen.getByLabelText("PDF file"), {
    target: { files: [file] },
  });
  fireEvent.submit(
    screen.getByRole("button", { name: "Upload document" }).closest("form")!,
  );
  await screen.findByText("Document uploaded.");
  expect(mocks.request).toHaveBeenCalledWith(
    expect.stringContaining("name=My+certificate"),
    expect.objectContaining({ method: "POST", body: file }),
  );
  expect(screen.getByLabelText("Document name")).toHaveValue("");
});
it("rejects oversized files before upload", async () => {
  render(<DocumentsPage />);
  await screen.findByRole("heading", { name: "Engineering resume" });
  fireEvent.change(screen.getByLabelText("Document name"), {
    target: { value: "Too big" },
  });
  fireEvent.change(screen.getByLabelText("PDF file"), {
    target: {
      files: [
        new File([new Uint8Array(5 * 1024 * 1024 + 1)], "big.pdf", {
          type: "application/pdf",
        }),
      ],
    },
  });
  fireEvent.submit(
    screen.getByRole("button", { name: "Upload document" }).closest("form")!,
  );
  expect(await screen.findByRole("alert")).toHaveTextContent("5 MiB");
  expect(mocks.request).toHaveBeenCalledTimes(1);
});
it("requires confirmation and surfaces server deletion conflicts", async () => {
  render(<DocumentsPage />);
  await screen.findByRole("heading", { name: "Engineering resume" });
  fireEvent.click(
    screen.getByRole("button", { name: "Delete Engineering resume" }),
  );
  expect(screen.getByRole("alertdialog")).toBeVisible();
  expect(mocks.request).toHaveBeenCalledTimes(1);
  mocks.request.mockRejectedValueOnce(
    new ApiError(409, { title: "Assigned resume cannot be deleted." }),
  );
  fireEvent.click(screen.getByRole("button", { name: "Permanently delete" }));
  expect(await screen.findByRole("alert")).toBeVisible();
  expect(screen.getByRole("alertdialog")).toBeVisible();
});
it("protects assigned resumes in the library", async () => {
  mocks.request.mockResolvedValue(page([{ ...item, applicationCount: 1 }]));
  render(<DocumentsPage />);
  await screen.findByRole("heading", { name: "Engineering resume" });
  expect(
    screen.getByRole("button", { name: "Delete Engineering resume" }),
  ).toBeDisabled();
});
it("shows empty and recoverable error states", async () => {
  mocks.request.mockRejectedValueOnce(new Error("offline"));
  mocks.request.mockResolvedValue(page([]));
  render(<DocumentsPage />);
  await screen.findByRole("alert");
  fireEvent.click(
    screen.getByRole("button", { name: "Retry loading documents" }),
  );
  expect(
    await screen.findByText("Upload your first PDF to start your library."),
  ).toBeVisible();
});
