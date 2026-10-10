import Image from "next/image";
import type { ReactNode } from "react";

// Pure presentational primitives shared by the auth pages and the dashboard.
// Headers are title + description only; no eyebrows and no decorative icons.

export function BrandLockup({ label = "Developer console" }: { label?: string }) {
  return (
    <span className="brand-lockup">
      <Image src="/logos/halfqr_main.png" alt="HalfQR" width={144} height={79} priority />
      <span>{label}</span>
    </span>
  );
}

export function PageHeader({ title, description, actions }: { title: string; description?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="page-header">
      <div className="page-header-copy">
        <h1>{title}</h1>
        {description ? <p>{description}</p> : null}
      </div>
      {actions ? <div className="card-actions">{actions}</div> : null}
    </div>
  );
}

export function SectionCard({ title, description, actions, children, padded = false }: { title: string; description?: ReactNode; actions?: ReactNode; children: ReactNode; padded?: boolean }) {
  return (
    <section className="card">
      <div className="card-head">
        <div className="card-head-copy">
          <h2>{title}</h2>
          {description ? <p>{description}</p> : null}
        </div>
        {actions ? <div className="card-actions">{actions}</div> : null}
      </div>
      {padded ? <div className="card-body">{children}</div> : children}
    </section>
  );
}

export type NoticeTone = "info" | "success" | "warning" | "danger";

export function Notice({ tone, title, children, role }: { tone: NoticeTone; title?: string; children: ReactNode; role?: "alert" | "status" }) {
  return (
    <div className={`notice notice-${tone}`} role={role}>
      {title ? <strong>{title}</strong> : null}
      <span>{children}</span>
    </div>
  );
}

export function EmptyState({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="empty">
      <h3>{title}</h3>
      <p>{children}</p>
    </div>
  );
}

export function StatCard({ label, value, unit, meta, progress }: { label: string; value: ReactNode; unit?: string; meta?: ReactNode; progress?: number }) {
  return (
    <article className="card stat">
      <span className="stat-label">{label}</span>
      <span className="stat-value">
        {value}
        {unit ? <small>{unit}</small> : null}
      </span>
      {progress !== undefined ? (
        <div className="meter" aria-hidden="true">
          <span style={{ width: `${Math.max(0, Math.min(100, progress))}%` }} />
        </div>
      ) : null}
      {meta ? <span className="stat-meta">{meta}</span> : null}
    </article>
  );
}

export function ChipList({ items }: { items: string[] }) {
  return (
    <div className="chip-list">
      {items.map((item) => (
        <span key={item} className="chip">
          {item}
        </span>
      ))}
    </div>
  );
}

// Percentage of a plan limit in use, safe for a zero limit.
export function usagePercent(used: number, limit: number) {
  return limit > 0 ? (used / limit) * 100 : 0;
}
