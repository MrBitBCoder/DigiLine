import type { HTMLAttributes, ReactNode } from "react";
import "./design-system.css";

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode;
  padding?: "none" | "compact" | "default";
}
export function Card({
  children,
  padding = "default",
  className = "",
  ...props
}: CardProps) {
  return (
    <div className={`ds-card ds-card-${padding} ${className}`} {...props}>
      {children}
    </div>
  );
}
