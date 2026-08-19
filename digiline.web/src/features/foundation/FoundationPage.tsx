import { Card, EmptyState, StatusBadge } from '../../shared/ui'
import './foundation-page.css'
export function FoundationPage() { return <div className="foundation-page">
  <header className="hero-copy"><span className="eyebrow">Doctor workspace</span><h1>Your queue, at a glance.</h1><p>The shared interface system is ready for live session data and doctor actions.</p></header>
  <div className="summary-grid" aria-label="Dashboard preview">
    <Card className="metric-card" role="article"><span>Current token</span><strong>—</strong><small>Waiting for an active session</small></Card>
    <Card className="metric-card" role="article"><span>Waiting</span><strong>0</strong><small>Patients in queue</small></Card>
    <Card className="metric-card" role="article"><span>Session state</span><strong className="metric-status"><StatusBadge status="Scheduled" /></strong><small>No active session</small></Card>
  </div>
  <EmptyState title="No session selected" message="Once authentication and doctor session APIs are connected, your active queue will appear here." />
</div> }
