import { createContext, useContext } from "react";

export type ToastType = "success" | "error" | "warning" | "info";

export interface ToastApi {
  success: (message: string) => void;
  error: (message: string) => void;
  warning: (message: string) => void;
  info: (message: string) => void;
}

const fallbackToast: ToastApi = {
  success: () => undefined,
  error: () => undefined,
  warning: () => undefined,
  info: () => undefined,
};

export const ToastContext = createContext<ToastApi>(fallbackToast);

export function useToast() {
  return useContext(ToastContext);
}
