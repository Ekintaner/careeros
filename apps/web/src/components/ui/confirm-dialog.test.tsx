import { render, screen, fireEvent } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { ConfirmDialog } from "./confirm-dialog";
it("focuses cancel and restores the trigger on dismiss", () => {
  const cancel = vi.fn();
  const trigger = document.createElement("button");
  document.body.append(trigger);
  trigger.focus();
  const r = render(
    <ConfirmDialog
      title="Delete file?"
      description="Permanent removal."
      busy={false}
      onCancel={cancel}
      onConfirm={vi.fn()}
    />,
  );
  expect(screen.getByRole("alertdialog")).toHaveAccessibleName("Delete file?");
  expect(screen.getByRole("button", { name: "Cancel" })).toHaveFocus();
  fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
  expect(cancel).toHaveBeenCalledTimes(1);
  r.unmount();
  expect(trigger).toHaveFocus();
  trigger.remove();
});
it("prevents cancellation during a destructive request", () => {
  const cancel = vi.fn();
  render(
    <ConfirmDialog
      title="Delete file?"
      description="Permanent removal."
      busy={true}
      onCancel={cancel}
      onConfirm={vi.fn()}
    />,
  );
  fireEvent(
    screen.getByRole("alertdialog"),
    new Event("cancel", { bubbles: true, cancelable: true }),
  );
  expect(cancel).not.toHaveBeenCalled();
  expect(screen.getByRole("button", { name: "Deleting…" })).toBeDisabled();
});
