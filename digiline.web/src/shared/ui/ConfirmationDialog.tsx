import { useEffect, useRef } from 'react'
import { Button } from './Button'
import './design-system.css'

interface ConfirmationDialogProps { open: boolean; title: string; message: string; confirmLabel?: string; destructive?: boolean; busy?: boolean; onConfirm: () => void; onCancel: () => void }
export function ConfirmationDialog({ open, title, message, confirmLabel = 'Confirm', destructive = false, busy = false, onConfirm, onCancel }: ConfirmationDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  useEffect(() => { const dialog = dialogRef.current; if (!dialog) return; if (open && !dialog.open) dialog.showModal(); else if (!open && dialog.open) dialog.close() }, [open])
  return <dialog ref={dialogRef} className="confirm-dialog" onCancel={event => { event.preventDefault(); if (!busy) onCancel() }} onClose={() => { if (open && !busy) onCancel() }} aria-labelledby="confirm-title" aria-describedby="confirm-message">
    <div className="dialog-icon" aria-hidden="true">!</div><h2 id="confirm-title">{title}</h2><p id="confirm-message">{message}</p><div className="dialog-actions"><Button variant="secondary" onClick={onCancel} disabled={busy} autoFocus>Go back</Button><Button variant={destructive ? 'danger' : 'primary'} onClick={onConfirm} busy={busy}>{confirmLabel}</Button></div>
  </dialog>
}
