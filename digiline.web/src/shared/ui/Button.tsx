import type { ButtonHTMLAttributes, ReactNode } from "react";
import "./design-system.css";

type ButtonVariant = "primary" | "secondary" | "danger" | "ghost";
interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  busy?: boolean;
  children: ReactNode;
}

export function Button({
  variant = "primary",
  busy = false,
  disabled,
  className = "",
  children,
  ...props
}: ButtonProps) {
  return (
    <button
      className={`ds-button ds-button-${variant} ${className}`}
      disabled={disabled || busy}
      aria-busy={busy || undefined}
      {...props}
    >
      {busy && <span className="button-spinner" aria-hidden="true" />}
      {children}
    </button>
  );
}
