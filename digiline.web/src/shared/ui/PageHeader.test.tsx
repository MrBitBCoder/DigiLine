import { render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import { Button } from "./Button";
import { PageHeader } from "./PageHeader";

it("keeps page identity, context, and primary action in one semantic header", () => {
  render(
    <PageHeader
      eyebrow="Workspace"
      title="Sessions"
      description="Manage clinic sessions."
      context={<span>Asia/Kolkata</span>}
      actions={<Button>Add session</Button>}
    />,
  );

  expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  expect(screen.getByRole("heading", { name: "Sessions" })).toBeInTheDocument();
  expect(screen.getByText("Asia/Kolkata")).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Add session" })).toBeEnabled();
});
