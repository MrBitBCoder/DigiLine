import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from "react";
import "./design-system.css";

type ButtonVariant = "primary" | "secondary" | "danger" | "ghost";
interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  busy?: boolean;
  children: ReactNode;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button({
  variant = "primary",
  busy = false,
  disabled,
  className = "",
  children,
  ...props
}, ref) {
  return (
    <button
      className={`ds-button ds-button-${variant} ${className}`}
      ref={ref}
      disabled={disabled || busy}
      aria-busy={busy || undefined}
      {...props}
    >
      {busy && <span className="button-spinner" aria-hidden="true" />}
      {children}
    </button>
  );
});
