import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Button } from "./Button";
import { ConfirmationDialog } from "./ConfirmationDialog";
import { TextField } from "./FormControls";

describe("shared form controls", () => {
  it("associates field guidance and errors with the input", () => {
    const view = render(
      <TextField label="Email" hint="Use your work email." />,
    );
    const input = screen.getByRole("textbox", { name: "Email" });
    expect(input).toHaveAccessibleDescription("Use your work email.");

    view.rerender(<TextField label="Email" error="Enter a valid email." />);
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAccessibleDescription("Enter a valid email.");
  });

  it("disables a busy button and exposes its progress state", () => {
    render(<Button busy>Save changes</Button>);
    expect(screen.getByRole("button", { name: "Save changes" })).toBeDisabled();
    expect(
      screen.getByRole("button", { name: "Save changes" }),
    ).toHaveAttribute("aria-busy", "true");
  });
});

describe("ConfirmationDialog", () => {
  it("focuses the safe action and handles Escape without confirming", async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    const onConfirm = vi.fn();
    render(
      <ConfirmationDialog
        open
        title="Delete session?"
        message="This cannot be undone."
        confirmLabel="Delete session"
        cancelLabel="Keep session"
        destructive
        onCancel={onCancel}
        onConfirm={onConfirm}
      />,
    );

    expect(screen.getByRole("button", { name: "Keep session" })).toHaveFocus();
    fireEvent(
      screen.getByRole("dialog"),
      new Event("cancel", { cancelable: true }),
    );
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Delete session" }));
    expect(onConfirm).toHaveBeenCalledOnce();
  });
});
