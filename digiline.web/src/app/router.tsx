import { createBrowserRouter } from 'react-router-dom'
import { AppLayout } from './AppLayout'
import { ProtectedRoute } from '../auth/ProtectedRoute'
import { RoleLanding } from '../auth/RoleLanding'
import { LoginPage } from '../features/auth/LoginPage'
import { DoctorDashboard } from '../features/doctor/DoctorDashboard'
import { ReceptionDashboard } from '../features/reception/ReceptionDashboard'
import { PublicClinicEntryPage } from '../features/public/PublicClinicEntryPage'
import { NotFoundPage } from '../shared/ui/NotFoundPage'

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/c/:clinicSlug', element: <PublicClinicEntryPage /> },
  { element: <ProtectedRoute />, children: [{ path: '/', element: <AppLayout />, children: [
    { index: true, element: <RoleLanding /> },
    { element: <ProtectedRoute roles={['Doctor']} />, children: [{ path: 'doctor', element: <DoctorDashboard /> }] },
    { element: <ProtectedRoute roles={['Receptionist']} />, children: [{ path: 'staff', element: <ReceptionDashboard /> }] },
    { path: '*', element: <NotFoundPage /> },
  ] }] },
])
