import type { ReactNode } from "react";

export type InfoCard = {
  title: string;
  body: ReactNode;
  meta?: ReactNode;
};

// Title + description cards used across marketing routes.
export function InfoCardGrid({ items, wide = false }: { items: InfoCard[]; wide?: boolean }) {
  return (
    <section className={wide ? "route-card-grid route-card-grid-wide" : "route-card-grid"}>
      {items.map((item) => (
        <article key={item.title} className="route-card">
          {item.meta}
          <h2>{item.title}</h2>
          <p>{item.body}</p>
        </article>
      ))}
    </section>
  );
}
