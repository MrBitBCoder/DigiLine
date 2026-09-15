import { useState, type ReactNode } from "react";
import "./design-system.css";

export interface TableColumn<T> {
  key: string;
  header: string;
  render: (row: T) => ReactNode;
  align?: "left" | "right";
}
export function DataTable<T>({
  columns,
  rows,
  getRowKey,
  caption,
  pageSize = 10,
  pageKey,
  page: controlledPage, totalCount, onPageChange,
}: {
  columns: TableColumn<T>[];
  rows: T[];
  getRowKey: (row: T) => string;
  caption: string;
  pageSize?: number;
  pageKey?: string;
  page?: number; totalCount?: number; onPageChange?: (page: number) => void;
}) {
  const [pageState, setPageState] = useState({ key: pageKey, page: 1 });
  const requestedPage = pageState.key === pageKey ? pageState.page : 1;
  const count = totalCount ?? rows.length;
  const totalPages = Math.max(1, Math.ceil(count / pageSize));
  const page = Math.min(controlledPage ?? requestedPage, totalPages);
  const visibleRows = controlledPage !== undefined ? rows : rows.slice((page - 1) * pageSize, page * pageSize);
  return (
    <div className="table-container">
    <div className="table-scroll">
      <table className="data-table" role="table">
        <caption className="sr-only">{caption}</caption>
        <thead role="rowgroup">
          <tr role="row">
            {columns.map((column) => (
              <th
                key={column.key}
                className={column.align === "right" ? "align-right" : ""}
                scope="col"
                role="columnheader"
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody role="rowgroup">
          {visibleRows.map((row) => (
            <tr role="row" key={getRowKey(row)}>
              {columns.map((column) => (
                <td
                  key={column.key}
                  className={column.align === "right" ? "align-right" : ""}
                  role="cell"
                  data-label={column.header}
                >
                  {column.render(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
    <nav className="table-pagination" aria-label={`${caption} pages`}>
      <span aria-live="polite">Showing {count ? (page - 1) * pageSize + 1 : 0}–{Math.min(page * pageSize, count)} of {count} · Page {page} of {totalPages}</span>
      <button type="button" disabled={page === 1} onClick={() => onPageChange ? onPageChange(page - 1) : setPageState({ key: pageKey, page: page - 1 })}>Previous {pageSize}</button>
      <button type="button" disabled={page === totalPages} onClick={() => onPageChange ? onPageChange(page + 1) : setPageState({ key: pageKey, page: page + 1 })}>Next {pageSize}</button>
    </nav>
    </div>
  );
}
