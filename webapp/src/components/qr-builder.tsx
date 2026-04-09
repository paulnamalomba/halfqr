"use client";

import { startTransition, useEffect, useState, type FormEvent } from "react";

type BuilderType = {
  id: BuilderTypeId;
  label: string;
  eyebrow: string;
  placeholder: string;
  summary: string;
};

type BuilderTypeId = "link" | "app" | "social" | "pdf" | "image" | "video" | "whatsapp";
type QrContentType = "Link" | "App" | "Social" | "Pdf" | "Image" | "Video" | "WhatsApp";
type QrJobStatus = "Queued" | "Running" | "Completed" | "Failed";
type FinderShape = "Square" | "Rounded" | "Circle";
type ErrorCorrectionLevel = "L" | "M" | "Q" | "H";

type SubmitRenderJobRequest = {
  contentType: QrContentType;
  targetUrl?: string;
  payload: Record<string, string>;
  errorCorrectionLevel: ErrorCorrectionLevel;
  output: {
    sizePx: number;
  };
  finder: {
    borderShape: FinderShape;
    centerShape: FinderShape;
  };
};

type RenderJobAcceptedResponse = {
  jobId: string;
  status: QrJobStatus;
  statusUrl: string;
};

type RenderArtifactDescriptor = {
  format: string;
  contentType: string;
  sizeBytes: number;
  downloadUrl: string;
};

type RenderJobStatusResponse = {
  jobId: string;
  status: QrJobStatus;
  contentType: QrContentType;
  encodedPayload?: string;
  resolvedTargetUrl?: string;
  configurationHash?: string;
  payloadHash?: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  failureReason?: string;
  artifacts: RenderArtifactDescriptor[];
};

const publicApiBaseUrl = (process.env.NEXT_PUBLIC_HAVEQR_API_BASE_URL ?? "https://haveqr-api-demo.computemore.com")
  .trim()
  .replace(/\/$/, "");

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

const pollIntervalMs = 1500;
const retryIntervalMs = 3000;

export function QrBuilder() {
  const [selectedType, setSelectedType] = useState<BuilderType>(builderTypes[0]);
  const [targetUrl, setTargetUrl] = useState("https://computemore.com/campaign/spring-launch");
  const [phone, setPhone] = useState("+260977000000");
  const [message, setMessage] = useState("Hello from HaveQR");
  const [finderBorder, setFinderBorder] = useState<FinderShape>("Rounded");
  const [finderCenter, setFinderCenter] = useState<FinderShape>("Circle");
  const [sizePx, setSizePx] = useState("1024");
  const [eccLevel, setEccLevel] = useState<ErrorCorrectionLevel>("H");
  const [acceptedJob, setAcceptedJob] = useState<RenderJobAcceptedResponse | null>(null);
  const [latestJob, setLatestJob] = useState<RenderJobStatusResponse | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isPolling, setIsPolling] = useState(false);

  const normalizedTargetUrl = targetUrl.trim();
  const normalizedPhone = phone.trim();
  const normalizedMessage = message.trim();
  const parsedSizePx = Number.parseInt(sizePx, 10);
  const hasValidSize = Number.isFinite(parsedSizePx) && parsedSizePx > 0;
  const resolvedSizePx = hasValidSize ? parsedSizePx : 1024;
  const previewTarget = resolvePreviewTarget(selectedType.id, normalizedTargetUrl, normalizedPhone, normalizedMessage);
  const requestPreview = buildRenderRequest(
    selectedType.id,
    normalizedTargetUrl,
    normalizedPhone,
    normalizedMessage,
    resolvedSizePx,
    finderBorder,
    finderCenter,
    eccLevel,
  );
  const currentStatus = latestJob?.status ?? acceptedJob?.status ?? null;
  const preferredArtifact = selectPreferredArtifact(latestJob?.artifacts ?? []);
  const statusMessage = getStatusMessage(acceptedJob, latestJob, isPolling);
  const timelineText = getTimelineText(acceptedJob, latestJob);

  useEffect(() => {
    if (!acceptedJob?.statusUrl) {
      return;
    }

    let cancelled = false;
    let timeoutId: number | undefined;

    setIsPolling(true);

    const scheduleNext = (delayMs: number) => {
      if (!cancelled) {
        timeoutId = window.setTimeout(runPoll, delayMs);
      }
    };

    const runPoll = async () => {
      try {
        const response = await fetch(acceptedJob.statusUrl, {
          cache: "no-store",
          headers: {
            Accept: "application/json",
          },
        });

        if (!response.ok) {
          throw new Error(await readErrorResponse(response, "Unable to load render status."));
        }

        const status = (await response.json()) as RenderJobStatusResponse;

        if (cancelled) {
          return;
        }

        setLatestJob(normalizeJobStatusResponse(status));
        setErrorMessage(null);

        if (isTerminalStatus(status.status)) {
          setIsPolling(false);
          return;
        }

        scheduleNext(pollIntervalMs);
      } catch (error) {
        if (cancelled) {
          return;
        }

        setErrorMessage(getErrorMessage(error));
        scheduleNext(retryIntervalMs);
      }
    };

    void runPoll();

    return () => {
      cancelled = true;

      if (timeoutId !== undefined) {
        window.clearTimeout(timeoutId);
      }
    };
  }, [acceptedJob?.jobId, acceptedJob?.statusUrl]);

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void queueRenderJob();
  };

  const queueRenderJob = async () => {
    const validationError = validateRequest(selectedType.id, normalizedTargetUrl, normalizedPhone, hasValidSize);

    if (validationError) {
      setErrorMessage(validationError);
      return;
    }

    setAcceptedJob(null);
    setLatestJob(null);
    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      const response = await fetch(resolveApiUrl("/api/v1/qr/render"), {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify(requestPreview),
      });

      if (!response.ok) {
        throw new Error(await readErrorResponse(response, "Unable to queue render job."));
      }

      const accepted = (await response.json()) as RenderJobAcceptedResponse;
      setAcceptedJob(normalizeAcceptedJob(accepted));
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handlePreferredDownload = () => {
    if (!preferredArtifact) {
      return;
    }

    window.location.assign(preferredArtifact.downloadUrl);
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
                    setTargetUrl(builderType.id === "whatsapp" ? "" : builderType.placeholder);
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
        <form className="panel panel-form" onSubmit={handleSubmit}>
          <div className="panel-header">
            <div>
              <p className="section-label">Builder inputs</p>
              <h2>Prepare the request</h2>
            </div>
            <span className="status-chip">v0.2.2.1</span>
          </div>

          <label className="field">
            <span>Target URL</span>
            <input
              value={targetUrl}
              onChange={(event) => setTargetUrl(event.target.value)}
              placeholder={selectedType.placeholder}
            />
            {selectedType.id === "whatsapp" ? (
              <small className="field-hint">Leave blank to synthesize a `wa.me` link from the phone and message fields.</small>
            ) : null}
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
              <select value={finderBorder} onChange={(event) => setFinderBorder(event.target.value as FinderShape)}>
                <option>Square</option>
                <option>Rounded</option>
                <option>Circle</option>
              </select>
            </label>
            <label className="field">
              <span>Finder center</span>
              <select value={finderCenter} onChange={(event) => setFinderCenter(event.target.value as FinderShape)}>
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
              {!hasValidSize ? <small className="field-hint field-hint-error">Use a whole number greater than zero.</small> : null}
            </label>
            <label className="field">
              <span>ECC</span>
              <select value={eccLevel} onChange={(event) => setEccLevel(event.target.value as ErrorCorrectionLevel)}>
                <option>L</option>
                <option>M</option>
                <option>Q</option>
                <option>H</option>
              </select>
            </label>
          </div>

          <div className="action-row">
            <button className="primary-action" type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Queueing Render Job..." : acceptedJob ? "Queue Another Render Job" : "Queue Render Job"}
            </button>
            <button className="secondary-action" type="button" disabled={!preferredArtifact} onClick={handlePreferredDownload}>
              {preferredArtifact ? `Download ${preferredArtifact.format.toUpperCase()}` : "Download When Ready"}
            </button>
          </div>

          <div className="job-feedback" aria-live="polite">
            {statusMessage ? <p className="feedback-text">{statusMessage}</p> : null}
            {errorMessage ? <p className="feedback-text feedback-text-error">{errorMessage}</p> : null}
            {latestJob?.artifacts.length ? (
              <div className="artifact-row">
                {latestJob.artifacts.map((artifact) => (
                  <a key={artifact.format} className="artifact-link" href={artifact.downloadUrl}>
                    {artifact.format.toUpperCase()} · {formatBytes(artifact.sizeBytes)}
                  </a>
                ))}
              </div>
            ) : null}
          </div>
        </form>

        <aside className="panel panel-preview">
          <div className="panel-header">
            <div>
              <p className="section-label">Request preview</p>
              <h2>Resolved payload</h2>
            </div>
            <span className={getStatusChipClassName(currentStatus)}>{getStatusLabel(currentStatus, isPolling)}</span>
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
              <p className="preview-value">{latestJob?.resolvedTargetUrl ?? previewTarget}</p>
              <p className="preview-label">Artifact plan</p>
              <p className="preview-value">
                {latestJob?.artifacts.length
                  ? latestJob.artifacts.map((artifact) => artifact.format.toUpperCase()).join(" + ")
                  : "Canonical SVG + derived PNG"}
              </p>
            </div>
          </div>

          <div className="job-summary">
            <div>
              <p className="preview-label">Job timeline</p>
              <p className="preview-value">{timelineText}</p>
            </div>
            <div>
              <p className="preview-label">Encoded payload</p>
              <p className="preview-value">{latestJob?.encodedPayload ?? "Worker will resolve this after queue pickup."}</p>
            </div>
            <div>
              <p className="preview-label">Status detail</p>
              <p className="preview-value">{latestJob?.failureReason ?? statusMessage}</p>
            </div>
          </div>

          <pre className="request-dump">{JSON.stringify(requestPreview, null, 2)}</pre>
        </aside>
      </section>
    </main>
  );
}

function buildRenderRequest(
  typeId: BuilderTypeId,
  targetUrl: string,
  phone: string,
  message: string,
  sizePx: number,
  finderBorder: FinderShape,
  finderCenter: FinderShape,
  errorCorrectionLevel: ErrorCorrectionLevel,
): SubmitRenderJobRequest {
  return {
    contentType: toContentType(typeId),
    targetUrl: typeId === "whatsapp" && targetUrl.length === 0 ? undefined : targetUrl || undefined,
    payload:
      typeId === "whatsapp"
        ? {
            ...(phone ? { phone } : {}),
            ...(message ? { message } : {}),
          }
        : {},
    errorCorrectionLevel,
    output: {
      sizePx,
    },
    finder: {
      borderShape: finderBorder,
      centerShape: finderCenter,
    },
  };
}

function resolveApiUrl(path: string) {
  return new URL(path, `${publicApiBaseUrl}/`).toString();
}

function normalizeAcceptedJob(acceptedJob: RenderJobAcceptedResponse): RenderJobAcceptedResponse {
  return {
    ...acceptedJob,
    statusUrl: resolveApiUrl(acceptedJob.statusUrl),
  };
}

function normalizeJobStatusResponse(job: RenderJobStatusResponse): RenderJobStatusResponse {
  return {
    ...job,
    artifacts: job.artifacts.map((artifact) => ({
      ...artifact,
      downloadUrl: resolveApiUrl(artifact.downloadUrl),
    })),
  };
}

function toContentType(typeId: BuilderTypeId): QrContentType {
  switch (typeId) {
    case "link":
      return "Link";
    case "app":
      return "App";
    case "social":
      return "Social";
    case "pdf":
      return "Pdf";
    case "image":
      return "Image";
    case "video":
      return "Video";
    case "whatsapp":
      return "WhatsApp";
  }
}

function resolvePreviewTarget(typeId: BuilderTypeId, targetUrl: string, phone: string, message: string) {
  if (typeId !== "whatsapp") {
    return targetUrl || "Provide an absolute URL to queue a render.";
  }

  if (targetUrl.length > 0) {
    return targetUrl;
  }

  const normalizedPhone = phone.replace(/[^\d+]/g, "");

  if (normalizedPhone.length === 0) {
    return "Provide a WhatsApp URL or phone number.";
  }

  const baseUrl = `https://wa.me/${normalizedPhone}`;
  return message ? `${baseUrl}?text=${encodeURIComponent(message)}` : baseUrl;
}

function validateRequest(typeId: BuilderTypeId, targetUrl: string, phone: string, hasValidSize: boolean) {
  if (!hasValidSize) {
    return "PNG size must be a whole number greater than zero.";
  }

  if (targetUrl.length > 0 && !isAbsoluteHttpUrl(targetUrl)) {
    return "Target URL must be a valid absolute http or https URL.";
  }

  if (typeId === "whatsapp") {
    const normalizedPhone = phone.replace(/[^\d+]/g, "");
    return targetUrl.length === 0 && normalizedPhone.length === 0
      ? "Provide a WhatsApp URL or a phone number for the fallback link."
      : null;
  }

  return targetUrl.length === 0 ? "Target URL is required for this content type." : null;
}

function isAbsoluteHttpUrl(value: string) {
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch {
    return false;
  }
}

function isTerminalStatus(status: QrJobStatus) {
  return status === "Completed" || status === "Failed";
}

function selectPreferredArtifact(artifacts: RenderArtifactDescriptor[]) {
  return artifacts.find((artifact) => artifact.format.toLowerCase() === "png") ?? artifacts[0] ?? null;
}

function getStatusLabel(status: QrJobStatus | null, isPolling: boolean) {
  if (!status) {
    return "Ready to queue";
  }

  if (status === "Running" && isPolling) {
    return "Rendering";
  }

  if (status === "Completed") {
    return "Artifacts ready";
  }

  if (status === "Failed") {
    return "Render failed";
  }

  return status;
}

function getStatusChipClassName(status: QrJobStatus | null) {
  if (!status) {
    return "status-chip status-chip-neutral";
  }

  return `status-chip status-chip-${status.toLowerCase()}`;
}

function getStatusMessage(
  acceptedJob: RenderJobAcceptedResponse | null,
  latestJob: RenderJobStatusResponse | null,
  isPolling: boolean,
) {
  if (!acceptedJob) {
    return "Queue a render job to start polling the worker and unlock artifact downloads.";
  }

  if (!latestJob) {
    return `Job ${shortenJobId(acceptedJob.jobId)} queued. Waiting for worker pickup.`;
  }

  switch (latestJob.status) {
    case "Queued":
      return `Job ${shortenJobId(latestJob.jobId)} is queued${isPolling ? " and being polled." : "."}`;
    case "Running":
      return `Job ${shortenJobId(latestJob.jobId)} is rendering SVG and PNG artifacts.`;
    case "Completed":
      return `Job ${shortenJobId(latestJob.jobId)} completed with ${latestJob.artifacts.length} artifact(s) ready.`;
    case "Failed":
      return latestJob.failureReason ?? `Job ${shortenJobId(latestJob.jobId)} failed during rendering.`;
  }
}

function getTimelineText(acceptedJob: RenderJobAcceptedResponse | null, latestJob: RenderJobStatusResponse | null) {
  if (!acceptedJob) {
    return "No render job queued yet.";
  }

  if (!latestJob) {
    return `Queued job ${shortenJobId(acceptedJob.jobId)}. Waiting for the first worker update.`;
  }

  return [
    `Created ${formatTimestamp(latestJob.createdAt)}`,
    latestJob.startedAt ? `Started ${formatTimestamp(latestJob.startedAt)}` : null,
    latestJob.completedAt ? `Completed ${formatTimestamp(latestJob.completedAt)}` : null,
  ]
    .filter((value): value is string => value !== null)
    .join(" · ");
}

function formatTimestamp(value: string) {
  return new Date(value).toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

function shortenJobId(jobId: string) {
  return jobId.slice(0, 8);
}

function formatBytes(value: number) {
  if (value < 1024) {
    return `${value} B`;
  }

  if (value < 1024 * 1024) {
    return `${(value / 1024).toFixed(1)} KB`;
  }

  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Unexpected request failure.";
}

async function readErrorResponse(response: Response, fallbackMessage: string) {
  const contentType = response.headers.get("content-type") ?? "";

  if (contentType.includes("application/json")) {
    const responseClone = response.clone();
    const payload = (await responseClone.json().catch(() => null)) as
      | {
          title?: string;
          detail?: string;
          errors?: Record<string, string[]>;
        }
      | null;

    if (payload?.detail) {
      return payload.detail;
    }

    if (payload?.title) {
      return payload.title;
    }

    if (payload?.errors) {
      const messages = Object.values(payload.errors).flat();
      if (messages.length > 0) {
        return messages.join(" ");
      }
    }
  }

  const text = await response.text().catch(() => "");
  return text.trim() || fallbackMessage;
}