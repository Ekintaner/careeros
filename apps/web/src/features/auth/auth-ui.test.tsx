import { StrictMode } from "react";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthForm } from "./auth-form";
import {
  AccountMenu,
  AuthGate,
  AuthProvider,
  SessionRecovery,
  useAuth,
} from "./auth-provider";
import { ApiError } from "@/lib/api-client";
const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  refresh: vi.fn(),
  request: vi.fn(),
  submit: vi.fn(),
  setOnExpired: vi.fn(),
}));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mocks.replace, refresh: mocks.refresh }),
}));
vi.mock("./auth-client", () => ({ authClient: () => mocks }));
const account = {
  id: "a",
  email: "person@example.com",
  displayName: "Person",
  emailVerified: false,
  createdAtUtc: "2026-01-01T00:00:00Z",
};
beforeEach(() => {
  vi.clearAllMocks();
  window.history.replaceState(null, "", "/");
  mocks.request.mockRejectedValue(new ApiError(401, {}));
});
function renderForm(
  mode: "login" | "register" | "forgot-password" | "reset-password",
) {
  render(
    <AuthProvider>
      <AuthForm mode={mode} />
    </AuthProvider>,
  );
}
describe("account forms and state", () => {
  it("does not restore a stale account lookup after the session expires", async () => {
    let resolve: (value: typeof account) => void = () => {};
    mocks.request.mockImplementation(
      () =>
        new Promise((done) => {
          resolve = done;
        }),
    );
    render(
      <AuthProvider>
        <AuthGate>Private workspace</AuthGate>
      </AuthProvider>,
    );
    act(() => {
      mocks.setOnExpired.mock.calls[0]![0]();
    });
    await act(async () => {
      resolve(account);
    });
    expect(screen.queryByText("Private workspace")).not.toBeInTheDocument();
    expect(mocks.replace).toHaveBeenCalledWith("/login");
  });
  it("returns a recovered session to the requested detail and query", async () => {
    mocks.request.mockResolvedValue(account);
    render(
      <AuthProvider>
        <SessionRecovery returnTo="/interviews/owned?view=all" />
      </AuthProvider>,
    );
    await waitFor(() =>
      expect(mocks.replace).toHaveBeenCalledWith("/interviews/owned?view=all"),
    );
  });
  it("preserves the destination when refresh fails and after login succeeds", async () => {
    const target = "/jobs/owned?view=details#notes";
    const view = render(
      <AuthProvider>
        <SessionRecovery returnTo={target} />
      </AuthProvider>,
    );
    await waitFor(() =>
      expect(mocks.replace).toHaveBeenCalledWith(
        `/login?returnTo=${encodeURIComponent(target)}`,
      ),
    );
    view.unmount();
    mocks.submit.mockResolvedValue(account);
    render(
      <AuthProvider>
        <AuthForm mode="login" returnTo={target} />
      </AuthProvider>,
    );
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Sign in" })).toBeEnabled(),
    );
    fireEvent.change(screen.getByLabelText("Email address"), {
      target: { value: "person@example.com" },
    });
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "test password" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
    await waitFor(() => expect(mocks.replace).toHaveBeenCalledWith(target));
  });
  it("retains the current route when a mounted workspace expires", async () => {
    window.history.replaceState(null, "", "/tasks/owned?view=open#details");
    render(
      <AuthProvider>
        <AuthGate>Private workspace</AuthGate>
      </AuthProvider>,
    );
    await waitFor(() =>
      expect(mocks.replace).toHaveBeenCalledWith(
        "/login?returnTo=%2Ftasks%2Fowned%3Fview%3Dopen%23details",
      ),
    );
  });
  it("clears the protected workspace and preserves password-change feedback on redirect", async () => {
    function PasswordChangedAction() {
      const auth = useAuth();
      return (
        <button onClick={auth.passwordChanged}>Complete password change</button>
      );
    }
    mocks.request.mockResolvedValue(account);
    render(
      <AuthProvider>
        <AuthGate>
          <p>Private settings</p>
          <PasswordChangedAction />
        </AuthGate>
      </AuthProvider>,
    );
    await screen.findByText("Private settings");
    fireEvent.click(
      screen.getByRole("button", { name: "Complete password change" }),
    );
    await waitFor(() =>
      expect(mocks.replace).toHaveBeenCalledWith("/login?password=changed"),
    );
    expect(screen.queryByText("Private settings")).not.toBeInTheDocument();
  });
  it("validates login before making a credential request", async () => {
    renderForm("login");
    const submit = screen.getByRole("button", { name: "Sign in" });
    await waitFor(() => expect(submit).toBeEnabled());
    fireEvent.click(submit);
    expect(
      screen.getByText("Enter a valid ASCII email address."),
    ).toBeVisible();
    expect(
      screen.getByText("Enter your password (up to 128 characters)."),
    ).toBeVisible();
    expect(mocks.submit).not.toHaveBeenCalled();
    expect(screen.getByLabelText("Email address")).toHaveFocus();
  });
  it("validates registration length, name and confirmation", async () => {
    renderForm("register");
    const submit = screen.getByRole("button", { name: "Create account" });
    await waitFor(() => expect(submit).toBeEnabled());
    fireEvent.change(screen.getByLabelText("Password", { exact: true }), {
      target: { value: "short" },
    });
    fireEvent.click(submit);
    expect(
      screen.getByText("Enter your name (up to 120 characters)."),
    ).toBeVisible();
    expect(
      screen.getByText("Use 15–128 characters without control characters."),
    ).toBeVisible();
    expect(screen.getByText("Your passwords do not match.")).toBeVisible();
    expect(mocks.submit).not.toHaveBeenCalled();
  });
  it("shows generic login failures and supports password visibility", async () => {
    mocks.submit.mockRejectedValue(
      new ApiError(401, { title: "Invalid email or password." }),
    );
    renderForm("login");
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Sign in" })).toBeEnabled(),
    );
    fireEvent.change(screen.getByLabelText("Email address"), {
      target: { value: "person@example.com" },
    });
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "incorrect" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Show password" }));
    expect(screen.getByLabelText("Password")).toHaveAttribute("type", "text");
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Invalid email or password.",
    );
  });
  it("hides protected content until authenticated and redirects anonymous users", async () => {
    render(
      <AuthProvider>
        <AuthGate>
          <p>Private workspace</p>
        </AuthGate>
      </AuthProvider>,
    );
    expect(screen.queryByText("Private workspace")).not.toBeInTheDocument();
    await waitFor(() => expect(mocks.replace).toHaveBeenCalledWith("/login"));
    expect(screen.queryByText("Private workspace")).not.toBeInTheDocument();
  });
  it("loads the account, renders protected content and signs out", async () => {
    mocks.request.mockResolvedValue(account);
    mocks.submit.mockResolvedValue(undefined);
    render(
      <AuthProvider>
        <AuthGate>
          <p>Private workspace</p>
          <AccountMenu />
        </AuthGate>
      </AuthProvider>,
    );
    expect(await screen.findByText("Private workspace")).toBeVisible();
    expect(screen.getByText("Person")).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));
    await waitFor(() => expect(mocks.submit).toHaveBeenCalledWith("logout"));
    await waitFor(() => expect(mocks.replace).toHaveBeenCalledWith("/login"));
    expect(screen.queryByText("Private workspace")).not.toBeInTheDocument();
  });
  it("does not pretend a failed logout succeeded", async () => {
    mocks.request.mockResolvedValue(account);
    mocks.submit.mockRejectedValue(new Error("offline"));
    render(
      <AuthProvider>
        <AccountMenu />
      </AuthProvider>,
    );
    await screen.findByText("Person");
    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Couldn’t sign out",
    );
    expect(mocks.replace).not.toHaveBeenCalled();
  });
  it("redirects authenticated users away from registration", async () => {
    mocks.request.mockResolvedValue(account);
    renderForm("register");
    await waitFor(() => expect(mocks.replace).toHaveBeenCalledWith("/"));
  });
  it("distinguishes service failure from an unauthenticated session", async () => {
    mocks.request.mockRejectedValue(new Error("offline"));
    render(
      <AuthProvider>
        <AuthGate>Private workspace</AuthGate>
      </AuthProvider>,
    );
    expect(
      await screen.findByText("We couldn’t open your workspace"),
    ).toBeVisible();
    expect(mocks.replace).not.toHaveBeenCalled();
  });
  it("shows delivery unavailability without claiming an email was sent", async () => {
    mocks.submit.mockRejectedValue(new ApiError(503, {}));
    renderForm("forgot-password");
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Request reset link" }),
      ).toBeEnabled(),
    );
    fireEvent.change(screen.getByLabelText("Email address"), {
      target: { value: "person@example.com" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Request reset link" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Password recovery is currently unavailable",
    );
  });
  it("consumes a reset fragment once under StrictMode without exposing it in the URL", async () => {
    window.history.replaceState(
      null,
      "",
      "/reset-password#token=test-only-reset",
    );
    mocks.submit.mockResolvedValue(undefined);
    render(
      <StrictMode>
        <AuthProvider>
          <AuthForm mode="reset-password" />
        </AuthProvider>
      </StrictMode>,
    );
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Reset password" }),
      ).toBeEnabled(),
    );
    expect(window.location.hash).toBe("");
    fireEvent.change(screen.getByLabelText("New password"), {
      target: { value: "several ordinary memorable words" },
    });
    fireEvent.change(screen.getByLabelText("Confirm password"), {
      target: { value: "several ordinary memorable words" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Reset password" }));
    await waitFor(() =>
      expect(mocks.submit).toHaveBeenCalledWith("reset-password", {
        token: "test-only-reset",
        newPassword: "several ordinary memorable words",
      }),
    );
    expect(await screen.findByText("Password updated")).toBeVisible();
  });
});
