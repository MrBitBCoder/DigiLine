import { useNavigate } from "react-router-dom";
import { SessionManager } from "./SessionManager";
import "./doctor-dashboard.css";

export function DoctorSessionsPage() {
  const navigate = useNavigate();
  return <div className="doctor-dashboard">
    <header className="dashboard-heading"><div><span className="eyebrow">Doctor workspace</span><h1>Sessions</h1><p>Manage your schedule and review attendance totals.</p></div></header>
    <SessionManager onSelect={async id => navigate(`/doctor?session=${encodeURIComponent(id)}`)} onDeleted={async () => {}} />
  </div>;
}
