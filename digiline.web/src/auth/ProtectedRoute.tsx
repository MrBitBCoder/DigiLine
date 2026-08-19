import { Navigate, Outlet, useLocation } from 'react-router-dom'
import type { StaffRole } from '../shared/types/domain'
import { useAuth } from './AuthContext'
export function ProtectedRoute({ roles }: { roles?: StaffRole[] }) { const { session } = useAuth(); const location = useLocation(); if (!session) return <Navigate to="/login" replace state={{ from: location.pathname }} />; if (roles && !roles.includes(session.user.role)) return <Navigate to="/" replace />; return <Outlet /> }
