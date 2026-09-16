import { useNavigate } from "react-router-dom";
import { SessionManager } from "./SessionManager";
import "./doctor-dashboard.css";

export function DoctorSessionsPage() {
  const navigate = useNavigate();
  return <div className="doctor-dashboard">
    <SessionManager onSelect={async id => navigate(`/doctor?session=${encodeURIComponent(id)}`)} onDeleted={async () => {}} />
  </div>;
}
