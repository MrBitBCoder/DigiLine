import { userRoles } from "../auth/authSession";
import { NavLink, Outlet } from "react-router-dom";
import "./app-layout.css";
import { useAuth } from "../auth/AuthContext";

export function AppLayout() {
  const { session, logout } = useAuth();
  return (
    <div className="app-shell">
      <header className="topbar">
        <NavLink className="brand" to="/" aria-label="DigiLine home">
          <span className="brand-mark" aria-hidden="true">
            D
          </span>
          <span>DigiLine</span>
        </NavLink>
        <div className="topbar-actions">
          <nav aria-label="Primary navigation">
            {session && userRoles(session.user).includes("Admin") && <NavLink className="nav-link" to="/admin">Clinic administration</NavLink>}
            {session && userRoles(session.user).includes("Doctor") && (
              <>
              <NavLink
                className={({ isActive }) =>
                  isActive ? "nav-link active" : "nav-link"
                }
                to="/doctor"
              >
                Doctor dashboard
              </NavLink>
              <NavLink className={({ isActive }) => isActive ? "nav-link active" : "nav-link"} to="/doctor/sessions">Sessions</NavLink>
              </>
            )}
            {session && userRoles(session.user).includes("Receptionist") && (
              <NavLink
                className={({ isActive }) =>
                  isActive ? "nav-link active" : "nav-link"
                }
                to="/staff"
              >
                Reception
              </NavLink>
            )}
          </nav>
          <div className="account">
            <NavLink className="nav-link" to="/account/password">Change password</NavLink>
            <span>
              <strong>{session?.user.name}</strong>
              <small>{session && userRoles(session.user).join(", ")}</small>
            </span>
            <button className="sign-out" type="button" onClick={logout}>
              Sign out
            </button>
          </div>
        </div>
      </header>
      <main className="page">
        <Outlet />
      </main>
      <footer className="footer">
        Queue care, without the waiting-room guesswork.
      </footer>
    </div>
  );
}
