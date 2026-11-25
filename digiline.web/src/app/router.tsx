import {
  RegistrationPage,
  AcceptInvitationPage,
  AdministrationPage,
} from "../features/admin/AdministrationPages";
import { createBrowserRouter } from "react-router-dom";
import { AppLayout } from "./AppLayout";
import { ProtectedRoute } from "../auth/ProtectedRoute";
import { RoleLanding } from "../auth/RoleLanding";
import { LoginPage } from "../features/auth/LoginPage";
import {
  ChangePasswordPage,
  ForgotPasswordPage,
  ResetPasswordPage,
} from "../features/auth/PasswordPages";
import { DoctorDashboardRoute } from "../features/doctor/DoctorDashboardRoute";
import { DoctorSessionsPage } from "../features/doctor/DoctorSessionsPage";
import { ReceptionDashboard } from "../features/reception/ReceptionDashboard";
import { PublicClinicEntryPage } from "../features/public/PublicClinicEntryPage";
import { NotFoundPage } from "../shared/ui/NotFoundPage";

export const router = createBrowserRouter([
  { path: "/register", element: <RegistrationPage /> },
  { path: "/accept-invitation", element: <AcceptInvitationPage /> },
  { path: "/login", element: <LoginPage /> },
  { path: "/forgot-password", element: <ForgotPasswordPage /> },
  { path: "/reset-password", element: <ResetPasswordPage /> },
  { path: "/c/:clinicSlug", element: <PublicClinicEntryPage /> },
  {
    element: <ProtectedRoute />,
    children: [
      {
        path: "/",
        element: <AppLayout />,
        children: [
          { index: true, element: <RoleLanding /> },
          {
            element: <ProtectedRoute roles={["Admin"]} />,
            children: [{ path: "admin", element: <AdministrationPage /> }],
          },
          { path: "account/password", element: <ChangePasswordPage /> },
          {
            element: <ProtectedRoute roles={["Doctor"]} />,
            children: [
              { path: "doctor", element: <DoctorDashboardRoute /> },
              { path: "doctor/sessions", element: <DoctorSessionsPage /> },
            ],
          },
          {
            element: <ProtectedRoute roles={["Receptionist"]} />,
            children: [{ path: "staff", element: <ReceptionDashboard /> }],
          },
          { path: "*", element: <NotFoundPage /> },
        ],
      },
    ],
  },
]);
