import AutoAwesomeRounded from "@mui/icons-material/AutoAwesomeRounded";
import BlurOnRounded from "@mui/icons-material/BlurOnRounded";
import VerifiedRounded from "@mui/icons-material/VerifiedRounded";
import Link from "next/link";

type IconComponent = typeof AutoAwesomeRounded;

const valueCards: Array<{
  title: string;
  body: string;
  Icon: IconComponent;
}> = [
  {
    title: "Render truth first",
    body: "The page exposes only the styling controls the worker can actually render, so the preview and the final artifact stay aligned.",
    Icon: VerifiedRounded,
  },
  {
    title: "AfriFlex visual energy",
    body: "HaveQR borrows qr.io's speed-to-creation but keeps its own cobalt, glass, and rounded-card visual identity.",
    Icon: BlurOnRounded,
  },
  {
    title: "Fast first interaction",
    body: "A visitor lands directly on the generator, not on a sign-in wall or a marketing funnel that delays QR creation.",
    Icon: AutoAwesomeRounded,
  },
];

export default function WhyHaveQrPage() {
  return (
    <main className="site-page route-shell">
      <section className="route-hero-card">
        <div className="route-hero-copy">
          <p className="page-kicker">Why HaveQR</p>
          <h1 className="page-title">Get to QR creation quickly without flattening the brand.</h1>
          <p className="page-summary">
            The public webapp takes the best part of the qr.io pattern, immediate creation, and combines it with a cobalt-heavy visual
            system, glass navigation, rounded cards, and a stronger sense of product identity.
          </p>

          <div className="cta-row">
            <Link className="route-primary-link" href="/">
              Start with the generator
            </Link>
            <Link className="route-secondary-link" href="/product">
              View product surfaces
            </Link>
          </div>
        </div>

        <div className="route-side-card">
          <p className="route-side-note">
            Public UX policy: first-time QR creation must not depend on visible sign-in, while dashboard capabilities can remain behind a feature flag.
          </p>
        </div>
      </section>

      <section className="route-card-grid">
        {valueCards.map((card) => {
          const Icon = card.Icon;

          return (
            <article key={card.title} className="route-card">
              <div className="route-card-icon">
                <Icon fontSize="small" />
              </div>
              <h2>{card.title}</h2>
              <p>{card.body}</p>
            </article>
          );
        })}
      </section>
    </main>
  );
}