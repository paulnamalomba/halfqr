import Link from "next/link";
import type { ReactNode } from "react";

export type HeroAction = {
  href: string;
  label: string;
  variant?: "primary" | "secondary";
  external?: boolean;
};

// Title + description hero shared by every marketing route. No eyebrow, no icons.
export function PageHero({ title, summary, actions = [], aside }: { title: string; summary: ReactNode; actions?: HeroAction[]; aside?: ReactNode }) {
  return (
    <section className="route-hero-card">
      <div className="route-hero-copy">
        <h1 className="page-title">{title}</h1>
        <p className="page-summary">{summary}</p>
        {actions.length > 0 ? <ActionRow actions={actions} /> : null}
      </div>
      {aside}
    </section>
  );
}

export function ActionRow({ actions }: { actions: HeroAction[] }) {
  return (
    <div className="cta-row">
      {actions.map(({ href, label, variant = "secondary", external }) => {
        const className = variant === "primary" ? "web-button web-button-primary" : "web-button web-button-secondary";

        return external ? (
          <a key={href} className={className} href={href} target="_blank" rel="noreferrer">
            {label}
          </a>
        ) : (
          <Link key={href} className={className} href={href}>
            {label}
          </Link>
        );
      })}
    </div>
  );
}
