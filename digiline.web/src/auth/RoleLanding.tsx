import { userRoles } from "./authSession";
import { Navigate } from "react-router-dom";
import { useAuth } from "./AuthContext";
export function RoleLanding() {
  const { session } = useAuth();
  if (!session) return <Navigate to="/login" replace />;
  return (
    <Navigate
      to={userRoles(session.user).includes("Admin") ? "/admin" : userRoles(session.user).includes("Doctor") ? "/doctor" : "/staff"}
      replace
    />
  );
}
