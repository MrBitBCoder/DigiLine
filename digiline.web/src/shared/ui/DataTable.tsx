import type { ReactNode } from 'react'
import './design-system.css'

export interface TableColumn<T> { key: string; header: string; render: (row: T) => ReactNode; align?: 'left' | 'right' }
export function DataTable<T>({ columns, rows, getRowKey, caption }: { columns: TableColumn<T>[]; rows: T[]; getRowKey: (row: T) => string; caption: string }) {
  return <div className="table-scroll"><table className="data-table"><caption className="sr-only">{caption}</caption><thead><tr>{columns.map(column => <th key={column.key} className={column.align === 'right' ? 'align-right' : ''} scope="col">{column.header}</th>)}</tr></thead><tbody>{rows.map(row => <tr key={getRowKey(row)}>{columns.map(column => <td key={column.key} className={column.align === 'right' ? 'align-right' : ''} data-label={column.header}>{column.render(row)}</td>)}</tr>)}</tbody></table></div>
}
