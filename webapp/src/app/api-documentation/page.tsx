import ApiRounded from "@mui/icons-material/ApiRounded";
import CloudDownloadRounded from "@mui/icons-material/CloudDownloadRounded";
import CodeRounded from "@mui/icons-material/CodeRounded";
import ScheduleRounded from "@mui/icons-material/ScheduleRounded";
import SyncRounded from "@mui/icons-material/SyncRounded";
import TuneRounded from "@mui/icons-material/TuneRounded";
import Link from "next/link";

type IconComponent = typeof ApiRounded;

const endpointCards: Array<{
  method: string;
  route: string;
  title: string;
  description: string;
  Icon: IconComponent;
}> = [
  {
    method: "POST",
    route: "/api/v1/qr/render",
    title: "Queue a render job",
    description: "Submit the content route, visual styling, and output settings. The API returns a queued job with a status URL.",
    Icon: ApiRounded,
  },
  {
    method: "GET",
    route: "/api/v1/qr/jobs/{jobId}",
    title: "Poll job status",
    description: "Retrieve the current status, resolved payload, timestamps, hashes, and any rendered SVG or PNG artifacts.",
    Icon: ScheduleRounded,
  },
  {
    method: "GET",
    route: "/api/v1/qr/jobs/{jobId}/artifacts/{format}",
    title: "Download artifacts",
    description: "Fetch the persisted SVG or PNG output after the worker completes the render job.",
    Icon: CloudDownloadRounded,
  },
];

const requestNotes = [
  "Content types currently supported by the public generator: Link, App, Social, Pdf, Image, Video, and WhatsApp.",
  "Data styling supports square or dotted modules plus an optional two-colour linear gradient on the main QR body.",
  "Logo uploads accept Svg, Png, and Jpeg sources with centered placement and backdrop padding.",
  "In local webapp development, browser requests are routed through /api/halfqr/... to avoid CORS preflight failures.",
];

const sampleRequest = `const response = await fetch("https://halfqr-api-demo.computemore.com/api/v1/qr/render", {
  method: "POST",
  headers: {
    "Accept": "application/json",
    "Content-Type": "application/json"
  },
  body: JSON.stringify({
    contentType: "Link",
    targetUrl: "https://computemore.com/demo-launch",
    payload: {},
    errorCorrectionLevel: "H",
    output: { sizePx: 1024 },
    finder: { borderShape: "Rounded", centerShape: "Circle" },
    colors: { dark: "#10243C", light: "#FFFFFF" },
    data: {
      pattern: "Dotted",
      gradientMode: "Linear",
      gradientStart: "#F36D2C",
      gradientEnd: "#1F61C0",
      gradientRotation: 135
    }
  })
});`;

export default function ApiDocumentationPage() {
  return (
    <main className="site-page route-shell">
      <section className="route-hero-card">
        <div className="route-hero-copy">
          <p className="page-kicker">Public API documentation</p>
          <h1 className="page-title">Queue, poll, and download the same QR artifacts the webapp uses.</h1>
          <p className="page-summary">
            The public generator and the backend stay aligned on the same request model. Start with render submission, poll the job,
            then retrieve the final SVG or PNG from the artifact endpoints.
          </p>

          <div className="cta-row">
            <Link className="route-primary-link" href="/">
              Open generator
            </Link>
            <a className="route-secondary-link" href="https://halfqr-api-demo.computemore.com/healthz" target="_blank" rel="noreferrer">
              Check live API status
            </a>
          </div>
        </div>

        <div className="route-side-card">
          <div className="route-side-item">
            <SyncRounded fontSize="small" />
            <span>Async render lifecycle</span>
          </div>
          <div className="route-side-item">
            <TuneRounded fontSize="small" />
            <span>Styled data modules and logo uploads</span>
          </div>
          <div className="route-side-item">
            <CloudDownloadRounded fontSize="small" />
            <span>Canonical SVG plus derived PNG downloads</span>
          </div>
        </div>
      </section>

      <section className="route-card-grid route-card-grid-wide">
        {endpointCards.map((endpoint) => {
          const Icon = endpoint.Icon;

          return (
            <article key={endpoint.route} className="route-card endpoint-card">
              <div className="route-card-icon">
                <Icon fontSize="small" />
              </div>
              <div className="endpoint-method">{endpoint.method}</div>
              <h2>{endpoint.title}</h2>
              <p className="endpoint-route">{endpoint.route}</p>
              <p>{endpoint.description}</p>
            </article>
          );
        })}
      </section>

      <section className="route-split-grid">
        <article className="route-card docs-code-card">
          <div className="route-card-icon">
            <CodeRounded fontSize="small" />
          </div>
          <h2>Minimal render request</h2>
          <p>Start with a link QR, then add gradients, dotted data modules, or a centered logo when needed.</p>
          <pre>{sampleRequest}</pre>
        </article>

        <article className="route-card">
          <div className="route-card-icon">
            <TuneRounded fontSize="small" />
          </div>
          <h2>Implementation notes</h2>
          <ul className="route-list">
            {requestNotes.map((note) => (
              <li key={note}>{note}</li>
            ))}
          </ul>
        </article>
      </section>
    </main>
  );
}