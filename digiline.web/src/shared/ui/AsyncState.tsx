import type { ReactNode } from 'react'
import './async-state.css'
import { Button } from './Button'
type StateProps = { title: string; message: string; action?: ReactNode }
function StatePanel({ title, message, action }: StateProps) { return <section className="state-panel" role="status"><div className="state-icon" aria-hidden="true">✦</div><h2>{title}</h2><p>{message}</p>{action}</section> }
export function LoadingState({ message = 'Loading the latest queue…' }: { message?: string }) { return <section className="state-panel" role="status" aria-live="polite"><span className="spinner" aria-hidden="true" /><h2>Just a moment</h2><p>{message}</p></section> }
export function EmptyState(props: Partial<StateProps>) { return <StatePanel title={props.title ?? 'Nothing here yet'} message={props.message ?? 'New items will appear here when they are available.'} action={props.action} /> }
export function ErrorState({ message = 'We could not load this page.', onRetry }: { message?: string; onRetry?: () => void }) { return <StatePanel title="Something went wrong" message={message} action={onRetry && <Button onClick={onRetry}>Try again</Button>} /> }
