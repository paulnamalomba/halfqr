"use client";

import { startTransition, useDeferredValue, useState } from "react";

type BuilderType = {
  id: string;
  label: string;
  eyebrow: string;
  placeholder: string;
  summary: string;
};

const builderTypes: BuilderType[] = [
  {
    id: "link",
    label: "Link",
    eyebrow: "URL-backed",
    placeholder: "https://computemore.com/campaign/spring-launch",
    summary: "Classic static destination for pages, campaigns, and one-off flows.",
  },
  {
    id: "app",
    label: "App",
    eyebrow: "URL-backed",
    placeholder: "https://apps.apple.com/app/id123456789",
    summary: "Deep-link into app stores, onboarding flows, or install landing pages.",
  },
  {
    id: "social",
    label: "Social",
    eyebrow: "URL-backed",
    placeholder: "https://www.instagram.com/computemore",
    summary: "Route scans into a profile, Link-in-bio page, or a social landing hub.",
  },
  {
    id: "pdf",
    label: "PDF",
    eyebrow: "URL-backed",
    placeholder: "https://cdn.haveqr.dev/brochures/launch-pack.pdf",
    summary: "Point to hosted brochures, menus, decks, or product sheets.",
  },
  {
    id: "image",
    label: "Image",
    eyebrow: "URL-backed",
    placeholder: "https://cdn.haveqr.dev/posters/flyer-front.jpg",
    summary: "Send scans straight into an image asset or a gallery landing view.",
  },
  {
    id: "video",
    label: "Video",
    eyebrow: "URL-backed",
    placeholder: "https://www.youtube.com/watch?v=launch-demo",
    summary: "Launch promo clips, product demos, or hosted training videos.",
  },
  {
    id: "whatsapp",
    label: "WhatsApp",
    eyebrow: "Special-case",
    placeholder: "https://wa.me/260977000000?text=Hello%20from%20HaveQR",
    summary: "Use a direct URL or generate a `wa.me` route from phone and message fields.",
  },
];

export function QrBuilder() {
  const [selectedType, setSelectedType] = useState<BuilderType>(builderTypes[0]);
  const [targetUrl, setTargetUrl] = useState("https://computemore.com/campaign/spring-launch");
  const [phone, setPhone] = useState("+260977000000");
  const [message, setMessage] = useState("Hello from HaveQR");
  const [finderBorder, setFinderBorder] = useState("Rounded");
  const [finderCenter, setFinderCenter] = useState("Circle");
  const [sizePx, setSizePx] = useState("1024");
  const [eccLevel, setEccLevel] = useState("H");
  const deferredTargetUrl = useDeferredValue(targetUrl);

  const previewTarget =
    selectedType.id === "whatsapp" && deferredTargetUrl.trim().length === 0
      ? `https://wa.me/${phone.replace(/[^\d+]/g, "")}?text=${encodeURIComponent(message)}`
      : deferredTargetUrl;

  const requestPreview = {
    contentType: capitalize(selectedType.id),
    targetUrl: selectedType.id === "whatsapp" && deferredTargetUrl.trim().length === 0 ? undefined : deferredTargetUrl,
    payload:
      selectedType.id === "whatsapp"
        ? {
            phone,
            message,
          }
        : {},
    errorCorrectionLevel: eccLevel,
    output: {
      sizePx: Number(sizePx),
    },
    finder: {
      borderShape: finderBorder,
      centerShape: finderCenter,
    },
  };

  return (
    <main className="builder-shell">
      <div className="builder-backdrop builder-backdrop-one" />
      <div className="builder-backdrop builder-backdrop-two" />
      <section className="hero-grid">
        <div className="hero-copy">
          <p className="eyebrow">HaveQR Builder</p>
          <h1>Queue branded QR renders without a sign-in wall.</h1>
          <p className="hero-text">
            The initial frontend is aligned to the live backend slice: most categories resolve from a target URL,
            while WhatsApp can also synthesize a `wa.me` link. SVG stays authoritative. PNG is derived for export.
          </p>
          <div className="hero-callouts">
            <article>
              <strong>Async render path</strong>
              <span>Public API queues the job, worker writes SVG and PNG artifacts, client polls for retrieval.</span>
            </article>
            <article>
              <strong>Current v1 simplification</strong>
              <span>Semantic categories stay visible in the UI even when the request contract resolves to `targetUrl`.</span>
            </article>
          </div>
        </div>

        <div className="hero-panel">
          <div className="section-label">Content type</div>
          <div className="type-grid">
            {builderTypes.map((builderType) => (
              <button
                key={builderType.id}
                type="button"
                className={builderType.id === selectedType.id ? "type-pill type-pill-active" : "type-pill"}
                onClick={() => {
                  startTransition(() => {
                    setSelectedType(builderType);
                    setTargetUrl(builderType.placeholder);
                  });
                }}
              >
                <span>{builderType.label}</span>
                <small>{builderType.eyebrow}</small>
              </button>
            ))}
          </div>
          <p className="type-summary">{selectedType.summary}</p>
        </div>
      </section>

      <section className="workspace-grid">
        <form className="panel panel-form">
          <div className="panel-header">
            <div>
              <p className="section-label">Builder inputs</p>
              <h2>Prepare the request</h2>
            </div>
            <span className="status-chip">v0.2.0.0</span>
          </div>

          <label className="field">
            <span>Target URL</span>
            <input
              value={targetUrl}
              onChange={(event) => setTargetUrl(event.target.value)}
              placeholder={selectedType.placeholder}
            />
          </label>

          {selectedType.id === "whatsapp" ? (
            <div className="field-grid">
              <label className="field">
                <span>Phone fallback</span>
                <input value={phone} onChange={(event) => setPhone(event.target.value)} />
              </label>
              <label className="field">
                <span>Message fallback</span>
                <input value={message} onChange={(event) => setMessage(event.target.value)} />
              </label>
            </div>
          ) : null}

          <div className="field-grid">
            <label className="field">
              <span>Finder border</span>
              <select value={finderBorder} onChange={(event) => setFinderBorder(event.target.value)}>
                <option>Square</option>
                <option>Rounded</option>
                <option>Circle</option>
              </select>
            </label>
            <label className="field">
              <span>Finder center</span>
              <select value={finderCenter} onChange={(event) => setFinderCenter(event.target.value)}>
                <option>Square</option>
                <option>Rounded</option>
                <option>Circle</option>
              </select>
            </label>
          </div>

          <div className="field-grid field-grid-tight">
            <label className="field">
              <span>PNG size</span>
              <input value={sizePx} onChange={(event) => setSizePx(event.target.value)} />
            </label>
            <label className="field">
              <span>ECC</span>
              <select value={eccLevel} onChange={(event) => setEccLevel(event.target.value)}>
                <option>L</option>
                <option>M</option>
                <option>Q</option>
                <option>H</option>
              </select>
            </label>
          </div>

          <div className="action-row">
            <button className="primary-action" type="button">
              Queue Render Job
            </button>
            <button className="secondary-action" type="button">
              Download When Ready
            </button>
          </div>
        </form>

        <aside className="panel panel-preview">
          <div className="panel-header">
            <div>
              <p className="section-label">Request preview</p>
              <h2>Resolved payload</h2>
            </div>
            <span className="status-chip status-chip-dark">Worker-ready</span>
          </div>

          <div className="preview-card">
            <div className="preview-qr">
              <span />
              <span />
              <span />
              <div className="preview-core" />
            </div>
            <div className="preview-meta">
              <p className="preview-label">Resolved target</p>
              <p className="preview-value">{previewTarget}</p>
              <p className="preview-label">Artifact plan</p>
              <p className="preview-value">Canonical SVG + derived PNG</p>
            </div>
          </div>

          <pre className="request-dump">{JSON.stringify(requestPreview, null, 2)}</pre>
        </aside>
      </section>
    </main>
  );
}

function capitalize(value: string) {
  return value.slice(0, 1).toUpperCase() + value.slice(1);
}