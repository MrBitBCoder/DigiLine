import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "./Toast";
import { useToast } from "./ToastContext";

function Controls() {
  const toast = useToast();
  return (
    <div>
      <button onClick={() => toast.success("Saved")}>Success</button>
      <button onClick={() => toast.error("Failed")}>Error</button>
      <button onClick={() => toast.warning("Check this")}>Warning</button>
      <button onClick={() => toast.info("Updated")}>Info</button>
      <button
        onClick={() => {
          for (let index = 0; index < 6; index++)
            toast.info(`Message ${index}`);
        }}
      >
        Many
      </button>
    </div>
  );
}

function show() {
  return render(
    <ToastProvider>
      <Controls />
    </ToastProvider>,
  );
}

afterEach(() => vi.useRealTimers());

describe("ToastProvider", () => {
  it("renders all notification types with accessible semantics in the shared viewport", () => {
    show();
    fireEvent.click(screen.getByText("Success"));
    fireEvent.click(screen.getByText("Error"));
    fireEvent.click(screen.getByText("Warning"));
    fireEvent.click(screen.getByText("Info"));
    expect(screen.getByRole("region", { name: "Notifications" })).toHaveClass(
      "toast-viewport",
    );
    expect(screen.getByRole("alert")).toHaveTextContent("ErrorFailed");
    expect(screen.getAllByRole("status")).toHaveLength(3);
  });

  it("automatically dismisses at the configured duration and supports manual dismissal", () => {
    vi.useFakeTimers();
    show();
    fireEvent.click(screen.getByText("Success"));
    act(() => vi.advanceTimersByTime(4_000));
    act(() => vi.advanceTimersByTime(180));
    expect(screen.queryByText("Saved")).not.toBeInTheDocument();
    fireEvent.click(screen.getByText("Error"));
    fireEvent.click(
      screen.getByRole("button", { name: "Dismiss error notification" }),
    );
    act(() => vi.advanceTimersByTime(180));
    expect(screen.queryByText("Failed")).not.toBeInTheDocument();
  });

  it("pauses dismissal while hovered or focused without stealing focus", () => {
    vi.useFakeTimers();
    show();
    const trigger = screen.getByText("Info");
    trigger.focus();
    fireEvent.click(trigger);
    expect(trigger).toHaveFocus();
    const item = screen.getByText("Updated").closest("article")!;
    fireEvent.mouseEnter(item);
    act(() => vi.advanceTimersByTime(5_000));
    expect(screen.getByText("Updated")).toBeInTheDocument();
    fireEvent.mouseLeave(item);
    const dismiss = screen.getByRole("button", {
      name: "Dismiss information notification",
    });
    dismiss.focus();
    act(() => vi.advanceTimersByTime(5_000));
    expect(screen.getByText("Updated")).toBeInTheDocument();
    fireEvent.blur(dismiss, { relatedTarget: null });
    act(() => vi.advanceTimersByTime(4_000));
    act(() => vi.advanceTimersByTime(180));
    expect(screen.queryByText("Updated")).not.toBeInTheDocument();
  });

  it("stacks newest first, suppresses duplicates, and retains at most five", () => {
    show();
    fireEvent.click(screen.getByRole("button", { name: "Success" }));
    fireEvent.click(screen.getByRole("button", { name: "Success" }));
    expect(screen.getAllByText("Saved")).toHaveLength(1);
    fireEvent.click(screen.getByText("Many"));
    const messages = screen
      .getByRole("region", { name: "Notifications" })
      .querySelectorAll("article");
    expect(messages).toHaveLength(5);
    expect(messages[0]).toHaveTextContent("Message 5");
  });
});
