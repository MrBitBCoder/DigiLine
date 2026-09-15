import { userRoles } from "../auth/authSession";
import { useEffect, useRef } from "react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import "./app-layout.css";
import { useAuth } from "../auth/AuthContext";

export function AppLayout() {
  const { session, logout } = useAuth();
  const location = useLocation();
  const mainRef = useRef<HTMLElement>(null);

  useEffect(() => {
    const main = mainRef.current;
    if (!main) return;
    let focusedHeading: HTMLElement | null = null;
    let suppliedTabIndex = false;
    const focusHeading = () => {
      const heading = main.querySelector<HTMLElement>("h1");
      if (!heading) return false;
      suppliedTabIndex = heading.hasAttribute("tabindex");
      if (!suppliedTabIndex) heading.tabIndex = -1;
      heading.focus();
      focusedHeading = heading;
      return true;
    };
    let observer: MutationObserver | null = null;
    if (!focusHeading()) observer = new MutationObserver(() => {
      if (focusHeading()) observer?.disconnect();
    });
    observer?.observe(main, { childList: true, subtree: true });
    return () => {
      observer?.disconnect();
      if (focusedHeading && !suppliedTabIndex) focusedHeading.removeAttribute("tabindex");
    };
  }, [location.pathname, location.search]);

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
                end
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
      <main className="page" ref={mainRef}>
        <Outlet />
      </main>
      <footer className="footer">
        Queue care, without the waiting-room guesswork.
      </footer>
    </div>
  );
}
