import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { expect, it } from "vitest";
import { DataTable } from "./DataTable";

it("paginates ten rows and resets when the table context changes", async () => {
  const rows = Array.from({ length: 12 }, (_, id) => ({ id: String(id + 1) }));
  const props = {
    columns: [
      { key: "id", header: "ID", render: (row: { id: string }) => row.id },
    ],
    getRowKey: (row: { id: string }) => row.id,
    caption: "Tokens",
  };
  const user = userEvent.setup();
  const view = render(<DataTable {...props} rows={rows} pageKey="session-1" />);
  expect(
    screen.getByText("Showing 1–10 of 12 · Page 1 of 2"),
  ).toBeInTheDocument();
  expect(screen.queryByText("11")).not.toBeInTheDocument();
  await user.click(screen.getByRole("button", { name: "Next 10" }));
  expect(
    screen.getByText("Showing 11–12 of 12 · Page 2 of 2"),
  ).toBeInTheDocument();
  expect(screen.getByText("11")).toBeInTheDocument();
  await user.click(screen.getByRole("button", { name: "Previous 10" }));
  expect(screen.queryByText("11")).not.toBeInTheDocument();
  await user.click(screen.getByRole("button", { name: "Next 10" }));
  view.rerender(
    <DataTable {...props} rows={rows.slice(0, 5)} pageKey="session-1" />,
  );
  expect(
    screen.getByText("Showing 1–5 of 5 · Page 1 of 1"),
  ).toBeInTheDocument();
  view.rerender(<DataTable {...props} rows={rows} pageKey="session-2" />);
  expect(screen.queryByText("11")).not.toBeInTheDocument();
});
