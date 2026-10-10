import Link from "next/link";
import { InfoCardGrid } from "@/components/site/info-card-grid";
import { PageHero } from "@/components/site/page-hero";


const endpointCards: Array<{
  method: string;
  route: string;
  title: string;
  description: string;
}> = [
  {
    method: "POST",
    route: "/api/v1/qr/render/draft",
    title: "Render a draft preview",
    description: "Synchronously validate a request and return the SVG markup and hashes without queueing a job. Requires the qr:render scope when a key is sent.",
  },
  {
    method: "POST",
    route: "/api/v1/qr/render",
    title: "Queue a render job",
    description: "Submit the content route, visual styling, and output settings. The API returns a queued job with a status URL.",
  },
  {
    method: "GET",
    route: "/api/v1/qr/jobs/{jobId}",
    title: "Poll job status",
    description: "Retrieve the current status, resolved payload, timestamps, hashes, and any rendered SVG or PNG artifacts.",
  },
  {
    method: "GET",
    route: "/api/v1/qr/jobs/{jobId}/artifacts/{format}",
    title: "Download artifacts",
    description: "Fetch the persisted SVG or PNG output after the worker completes the render job.",
  },
  {
    method: "GET",
    route: "/api/v1/qr/content-types",
    title: "List content types",
    description: "Return every content type the API accepts, so clients can stay in sync without hard-coding the list.",
  },
];

const authenticationNotes = [
  "Send an API key or access token as Authorization: Bearer hqr_sk_… (or hqr_pat_…), or in the X-Api-Key header.",
  "Scopes: qr:render covers /render and /render/draft. qr:read covers job status and artifact downloads.",
  "Jobs created with a key are private to that key. Other callers get 404 for the job and its artifacts.",
  "Keyed requests use your plan's per-minute limit. Anonymous requests are limited per IP and return 429 when exceeded.",
  "Invalid, expired or revoked credentials return 401. A valid credential without the needed scope returns 403.",
  "Limits: output 256 to 4096 px, SVG logos up to 120,000 characters, raster logos up to 512 KB and 4096 × 4096 px, request bodies up to 1 MB.",
];

const requestNotes = [
  "Content types currently supported by the public generator: Link, App, Social, Pdf, Image, Video, and WhatsApp.",
  "Data styling supports square or dotted modules plus an optional two-colour linear gradient on the main QR body.",
  "Logo uploads accept Svg, Png, and Jpeg sources with centered placement and backdrop padding.",
  // "In local webapp development, browser requests are routed through /api/halfqr/... to avoid CORS preflight failures.",
];

const sampleRequest = `const response = await fetch("https://api.halfqr.com/api/v1/qr/render", {
  method: "POST",
  headers: {
    "Authorization": \`Bearer \${process.env.HALFQR_API_KEY}\`,
    "Accept": "application/json",
    "Content-Type": "application/json"
  },
  body: JSON.stringify({
    contentType: "Link",
    targetUrl: "https://computemore.com",
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
      <PageHero
        title="Built for devs: queue, poll and generate on demand."
        summary="The generator and the API share one request model. Submit a render, poll the job, then download the SVG or PNG."
        actions={[
          { href: "/", label: "Open generator", variant: "primary" },
          { href: "/dashboard/api-keys", label: "Get an API key" },
          { href: "/changelog", label: "Changelog" },
        ]}
      />

      <InfoCardGrid
        wide
        items={endpointCards.map((endpoint) => ({
          title: endpoint.title,
          meta: (
            <p className="endpoint-route">
              <span className="endpoint-method">{endpoint.method}</span> {endpoint.route}
            </p>
          ),
          body: endpoint.description,
        }))}
      />

      <article className="route-card docs-code-card">
        <h2>Authentication and limits</h2>
        <p>
          The web builder works without an account. For server integrations, create an API key or access token in the{" "}
          <Link href="/dashboard/api-keys" style={{ textDecoration: "underline" }}>
            developer console
          </Link>
          . Credentials need an active paid plan.
        </p>
        <ul className="route-list">
          {authenticationNotes.map((note) => (
            <li key={note}>{note}</li>
          ))}
        </ul>
      </article>

      {/* <section className="route-card"> */}
        <article className="route-card docs-code-card">
          <h2>Minimal render request</h2>
          <p>Start with a link QR, then add gradients, dotted data modules, or a centered logo when needed.</p>
          <pre>{sampleRequest}</pre>
        </article>

        {/* <article className="route-card">
          <h2>Implementation notes</h2>
          <ul className="route-list">
            {requestNotes.map((note) => (
              <li key={note}>{note}</li>
            ))}
          </ul>
        </article> */}
      {/* </section> */}
    </main>
  );
}