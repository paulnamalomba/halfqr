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
    <main className="site-page page-shell-home">
      <section className="generator-hero">
        <div className="generator-hero-copy">
          <p className="page-kicker">Public QR generator</p>
          <h1 className="page-title">Build a production-ready QR code without leaving the page.</h1>
          <p className="page-summary">
            The landing page now behaves like the generator itself: choose a supported route, complete the content, style the code,
            then queue a real worker render against the live API.
          </p>
        </div>

        <div className="generator-fact-grid">
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
        </div>
      </section>

      <QrBuilder />
    </main>
  );
}