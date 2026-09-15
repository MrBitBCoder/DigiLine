import { useSearchParams } from "react-router-dom";
import { DoctorDashboard } from "./DoctorDashboard";

export function DoctorDashboardRoute() {
  const [params] = useSearchParams();
  return <DoctorDashboard initialSessionId={params.get("session")} />;
}
