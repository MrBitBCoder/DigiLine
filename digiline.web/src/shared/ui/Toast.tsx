import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { ToastContext, type ToastApi, type ToastType } from "./ToastContext";
import "./toast.css";

interface ToastMessage {
  id: number;
  type: ToastType;
  message: string;
  duration: number;
  expiresAt: number;
}

const durations: Record<ToastType, number> = {
  success: 4_000,
  info: 4_000,
  warning: 5_000,
  error: 6_000,
};

const labels: Record<ToastType, string> = {
  success: "Success",
  error: "Error",
  warning: "Warning",
  info: "Information",
};

const icons: Record<ToastType, string> = {
  success: "✓",
  error: "!",
  warning: "!",
  info: "i",
};

let nextToastId = 0;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const remove = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const show = useCallback((type: ToastType, message: string) => {
    const cleanMessage = message.trim();
    if (!cleanMessage) return;
    setToasts((current) => {
      if (current.some((toast) => toast.type === type && toast.message === cleanMessage))
        return current;
      const duration = durations[type];
      const toast = { id: ++nextToastId, type, message: cleanMessage, duration, expiresAt: Date.now() + duration };
      return [toast, ...current].slice(0, 5);
    });
  }, []);

  const api = useMemo<ToastApi>(() => ({
    success: (message) => show("success", message),
    error: (message) => show("error", message),
    warning: (message) => show("warning", message),
    info: (message) => show("info", message),
  }), [show]);

  return (
    <ToastContext.Provider value={api}>
      {children}
      <section className="toast-viewport" role="region" aria-label="Notifications">
        {toasts.map((toast) => <ToastItem key={toast.id} toast={toast} onRemove={remove} />)}
      </section>
    </ToastContext.Provider>
  );
}

function ToastItem({ toast, onRemove }: { toast: ToastMessage; onRemove: (id: number) => void }) {
  const remaining = useRef(toast.duration);
  const startedAt = useRef(0);
  const dismissTimer = useRef<number | undefined>(undefined);
  const removalTimer = useRef<number | undefined>(undefined);
  const hovered = useRef(false);
  const focused = useRef(false);
  const [leaving, setLeaving] = useState(false);

  const dismiss = useCallback(() => {
    if (leaving) return;
    window.clearTimeout(dismissTimer.current);
    setLeaving(true);
  }, [leaving]);

  const startTimer = useCallback(() => {
    if (leaving) return;
    window.clearTimeout(dismissTimer.current);
    startedAt.current = Date.now();
    dismissTimer.current = window.setTimeout(dismiss, remaining.current);
  }, [dismiss, leaving]);

  const pauseTimer = useCallback(() => {
    window.clearTimeout(dismissTimer.current);
    if (startedAt.current)
      remaining.current = Math.max(0, remaining.current - (Date.now() - startedAt.current));
    startedAt.current = 0;
  }, []);

  const resumeTimer = useCallback(() => {
    if (!hovered.current && !focused.current) startTimer();
  }, [startTimer]);

  useEffect(() => {
    if (leaving) removalTimer.current = window.setTimeout(() => onRemove(toast.id), 180);
    else startTimer();
    return () => {
      window.clearTimeout(dismissTimer.current);
      window.clearTimeout(removalTimer.current);
    };
  }, [leaving, onRemove, startTimer, toast.id]);

  return (
    <article
      className={`toast toast-${toast.type}${leaving ? " toast-leaving" : ""}`}
      role={toast.type === "error" ? "alert" : "status"}
      aria-live={toast.type === "error" ? "assertive" : "polite"}
      aria-atomic="true"
      onMouseEnter={() => { hovered.current = true; pauseTimer(); }}
      onMouseLeave={() => { hovered.current = false; resumeTimer(); }}
      onFocusCapture={() => { focused.current = true; pauseTimer(); }}
      onBlurCapture={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
          focused.current = false;
          resumeTimer();
        }
      }}
    >
      <span className="toast-icon" aria-hidden="true">{icons[toast.type]}</span>
      <div className="toast-content">
        <strong>{labels[toast.type]}</strong>
        <span>{toast.message}</span>
      </div>
      <button type="button" className="toast-dismiss" aria-label={`Dismiss ${labels[toast.type].toLowerCase()} notification`} onClick={dismiss}>×</button>
    </article>
  );
}
