import AutoAwesomeRounded from "@mui/icons-material/AutoAwesomeRounded";
import BrushRounded from "@mui/icons-material/BrushRounded";
import CloudDoneRounded from "@mui/icons-material/CloudDoneRounded";
import { QrBuilder } from "@/components/qr-builder";

type IconComponent = typeof AutoAwesomeRounded;

const generatorFacts: Array<{
  title: string;
  detail: string;
  Icon: IconComponent;
}> = [
  {
    title: "Generator-first landing page",
    detail: "The visitor lands directly on QR creation instead of a marketing-first hero stack.",
    Icon: AutoAwesomeRounded,
  },
  {
    title: "Styled data body",
    detail: "Switch between square or dotted data modules and keep gradients off the finder markers.",
    Icon: BrushRounded,
  },
  {
    title: "Real worker output",
    detail: "Queue once, poll the job, and retrieve the final SVG or PNG artifact when it completes.",
    Icon: CloudDoneRounded,
  },
];

export default function HomePage() {
  return (
    <main className="page-shell-home">
      {/* main content leader */}
      <section className="generator-hero generator-hero-bleed">
        <div className="generator-hero-copy">
          <p className="page-kicker" style={{ color: "var(--text-white)" }}>HalfQR: Half the Effort</p>
          <h1 className="page-title" style={{ color: "var(--cobalt-light)" }}>Quickly generate a branding-aware QR code for your business webpages</h1>
          <p className="page-summary" style={{ color: "var(--cobalt-light-white)" }}>
            We provide branding logo-definitions, marker-shape definition, and data-shape definition - explore below...
          </p>
        </div>

        {/* Will wire properly later */}
        {/* <div className="generator-fact-grid">
          {generatorFacts.map((fact) => {
            const Icon = fact.Icon;

            return (
              <article key={fact.title} className="generator-fact-card">
                <div className="route-card-icon">
                  <Icon fontSize="small" />
                </div>
                <h2>{fact.title}</h2>
                <p>{fact.detail}</p>
              </article>
            );
          })}
        </div> */}
      </section>

      {/* QR Builder Section */}
      <div className="site-page page-shell-home-builder">
        <QrBuilder />
      </div>
    </main>
  );
}