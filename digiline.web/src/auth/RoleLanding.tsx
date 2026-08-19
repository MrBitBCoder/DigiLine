import { Navigate } from 'react-router-dom'
import { useAuth } from './AuthContext'
export function RoleLanding() { const { session } = useAuth(); if (!session) return <Navigate to="/login" replace />; return <Navigate to={session.user.role === 'Doctor' ? '/doctor' : '/staff'} replace /> }
