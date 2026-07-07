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
    title: "Preview-first, outcome-focused service",
    body: "You always see the outcome even before it is finalised",
    Icon: VerifiedRounded,
  },
  // {
  //   title: "AfriFlex visual energy",
  //   body: "HalfQR borrows qr.io's speed-to-creation but keeps its own cobalt, glass, and rounded-card visual identity.",
  //   Icon: BlurOnRounded,
  // },
  {
    title: "Fast interaction, and superior response times",
    body: "We want your work to be quick, in and out",
    Icon: AutoAwesomeRounded,
  },
];

export default function WhyHalfQrPage() {
  return (
    <main className="site-page route-shell">
      <section className="route-hero-card">
        <div className="route-hero-copy">
          <p className="page-kicker" style={{ color: "var(--text-white)" }}>Why HalfQR</p>
          <h1 className="page-title">Half the effort, same digital impact.</h1>
          <p className="page-summary">
            QR codes elevate a business digital footprint, save the paper and generate a code your customers can scan and access all your important information
          </p>

          <div className="cta-row">
            <Link className="route-primary-link" href="/">
              Try it out now
            </Link>
            <Link className="route-secondary-link" href="/product">
              View product details
            </Link>
          </div>
        </div>

        {/* <div className="route-side-card">
          <p className="route-side-note">
            Public UX policy: first-time QR creation must not depend on visible sign-in, while dashboard capabilities can remain behind a feature flag.
          </p>
        </div> */}
      </section>

      <section className="route-card-grid">
        {valueCards.map((card) => {
          const Icon = card.Icon;

          return (
            <article key={card.title} className="route-card">
              {/* <div className="route-card-icon"> */}
                <Icon fontSize="large" />
              {/* </div> */}
              <h2>{card.title}</h2>
              <p>{card.body}</p>
            </article>
          );
        })}
      </section>
    </main>
  );
}