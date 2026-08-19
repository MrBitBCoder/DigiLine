import { NavLink, Outlet } from 'react-router-dom'
import './app-layout.css'
import { useAuth } from '../auth/AuthContext'

export function AppLayout() {
  const { session, logout } = useAuth()
  return <div className="app-shell">
    <header className="topbar">
      <NavLink className="brand" to="/doctor" aria-label="DigiLine home"><span className="brand-mark" aria-hidden="true">D</span><span>DigiLine</span></NavLink>
      <div className="topbar-actions">
        <nav aria-label="Primary navigation">
          {session?.user.role === 'Doctor' && <NavLink className={({ isActive }) => isActive ? 'nav-link active' : 'nav-link'} to="/doctor">Doctor dashboard</NavLink>}
          {session?.user.role === 'Receptionist' && <NavLink className={({ isActive }) => isActive ? 'nav-link active' : 'nav-link'} to="/staff">Reception</NavLink>}
        </nav>
        <div className="account"><span><strong>{session?.user.name}</strong><small>{session?.user.role}</small></span><button className="sign-out" type="button" onClick={logout}>Sign out</button></div>
      </div>
    </header>
    <main className="page"><Outlet /></main>
    <footer className="footer">Queue care, without the waiting-room guesswork.</footer>
  </div>
}
