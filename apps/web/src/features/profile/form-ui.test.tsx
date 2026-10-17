import { render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import { Field } from "./form-ui";
it("connects shared form feedback to its control without duplicate IDs", () => {
  render(
    <Field
      id="field"
      title="Title"
      hint="Use a clear title."
      error="Title is required."
    >
      <input aria-describedby="field-hint" />
    </Field>,
  );
  const input = screen.getByLabelText("Title");
  expect(input).toHaveAttribute("aria-invalid", "true");
  expect(input).toHaveAttribute("aria-describedby", "field-hint");
  expect(input).toHaveAccessibleDescription("Title is required.");
});
