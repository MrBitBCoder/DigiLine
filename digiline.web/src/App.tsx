import { RouterProvider } from "react-router-dom";
import { router } from "./app/router";
import { AuthProvider } from "./auth/AuthProvider";
import { ToastProvider } from "./shared/ui";

export default function App() {
  return (
    <ToastProvider>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </ToastProvider>
  );
}
