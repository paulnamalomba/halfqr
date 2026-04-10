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
          <p className="page-kicker" style={{ color: "var(--text-white)" }}>HaveQR: Easy, Anonymous QR Code Generation</p>
          <h1 className="page-title" style={{ color: "var(--cobalt-light)" }}>Have a production-ready QR code that you can share with others</h1>
          <p className="page-summary" style={{ color: "var(--cobalt-light-white)" }}>
            We provide advanced features such as marker-shape definition, we also provide data-shape definitions, custom logo-definitions for the generated code and completely synchronous processing - so what you see is what you get!
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

      <QrBuilder />
    </main>
  );
}