import type { ReactNode } from "react";
import "./design-system.css";

export function PageHeader({
  eyebrow,
  title,
  description,
  context,
  actions,
  className = "",
}: {
  eyebrow?: ReactNode;
  title: ReactNode;
  description?: ReactNode;
  context?: ReactNode;
  actions?: ReactNode;
  className?: string;
}) {
  return (
    <header className={`page-header ${className}`}>
      <div className="page-header-copy">
        {eyebrow && <span className="eyebrow">{eyebrow}</span>}
        <h1>{title}</h1>
        {description && <p>{description}</p>}
      </div>
      {(context || actions) && (
        <div className="page-header-aside">
          {context && <div className="page-header-context">{context}</div>}
          {actions && <div className="page-header-actions">{actions}</div>}
        </div>
      )}
    </header>
  );
}
