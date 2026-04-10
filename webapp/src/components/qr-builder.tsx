"use client";

import qrcodeGenerator from "qrcode-generator";
import AddPhotoAlternateRounded from "@mui/icons-material/AddPhotoAlternateRounded";
import AppsRounded from "@mui/icons-material/AppsRounded";
import AutoAwesomeRounded from "@mui/icons-material/AutoAwesomeRounded";
import CloudDownloadRounded from "@mui/icons-material/CloudDownloadRounded";
import CropSquareRounded from "@mui/icons-material/CropSquareRounded";
import ImageRounded from "@mui/icons-material/ImageRounded";
import LinkRounded from "@mui/icons-material/LinkRounded";
import PaletteRounded from "@mui/icons-material/PaletteRounded";
import PictureAsPdfRounded from "@mui/icons-material/PictureAsPdfRounded";
import ShareRounded from "@mui/icons-material/ShareRounded";
import SmartDisplayRounded from "@mui/icons-material/SmartDisplayRounded";
import TuneRounded from "@mui/icons-material/TuneRounded";
import WhatsApp from "@mui/icons-material/WhatsApp";
import { startTransition, useEffect, useId, useRef, useState, type ChangeEvent, type FormEvent } from "react";

type IconComponent = typeof LinkRounded;

type BuilderType = {
  id: BuilderTypeId;
  label: string;
  eyebrow: string;
  placeholder: string;
  summary: string;
  Icon: IconComponent;
};

type BuilderTypeId = "link" | "app" | "social" | "pdf" | "image" | "video" | "whatsapp";
type DesignSectionId = "frame" | "colour" | "logo";
type QrContentType = "Link" | "App" | "Social" | "Pdf" | "Image" | "Video" | "WhatsApp";
type QrJobStatus = "Queued" | "Running" | "Completed" | "Failed";
type FinderShape = "Square" | "Rounded" | "Circle";
type ErrorCorrectionLevel = "L" | "M" | "Q" | "H";
type QrDataPattern = "Square" | "Dotted";
type QrGradientMode = "None" | "Linear";
type QrLogoSourceType = "Svg" | "Png" | "Jpeg";

type UploadedLogo = {
  name: string;
  previewUrl: string;
  sizeBytes: number;
  aspectRatio: number;
  sourceType: QrLogoSourceType;
  svg?: string;
  contentBase64?: string;
  contentType?: string;
};

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
  colors: {
    dark: string;
    light: string;
  };
  data: {
    pattern: QrDataPattern;
    gradientMode: QrGradientMode;
    gradientStart?: string;
    gradientEnd?: string;
    gradientRotation: number;
  };
  logo?: {
    sourceType: QrLogoSourceType;
    svg?: string;
    contentBase64?: string;
    contentType?: string;
    sizePercent: number;
    removeBackground: boolean;
    backdropPaddingPercent: number;
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

type RenderDraftPreviewResponse = {
  resolvedTargetUrl: string;
  encodedPayload: string;
  configurationHash: string;
  payloadHash: string;
  svgMarkup: string;
};

type LogoPreset = {
  id: string;
  label: string;
  eyebrow: string;
  assetPath: string;
};

const publicApiBaseUrl = (process.env.NEXT_PUBLIC_HAVEQR_API_BASE_URL ?? "https://haveqr-api-demo.computemore.com")
  .trim()
  .replace(/\/$/, "");
const publicApiOrigin = new URL(publicApiBaseUrl).origin;
const apiProxyPrefix = "/api/haveqr";

const maxLogoBytes = 512 * 1024;
const pollIntervalMs = 1500;
const retryIntervalMs = 3000;
const draftPreviewDebounceMs = 450;
const previewQuietZoneModules = 4;
const previewFinderSizeModules = 7;
const previewFinderInnerSizeModules = 5;
const previewFinderCenterSizeModules = 3;
const previewDottedRadius = 0.38;

const builderTypes: BuilderType[] = [
  {
    id: "link",
    label: "Link",
    eyebrow: "Website",
    placeholder: "https://computemore.com/campaign/spring-launch",
    summary: "Classic static destination for pages, campaigns, and one-off flows.",
    Icon: LinkRounded,
  },
  {
    id: "app",
    label: "App",
    eyebrow: "Store route",
    placeholder: "https://apps.apple.com/app/id123456789",
    summary: "Deep-link into app stores, onboarding flows, or install landing pages.",
    Icon: AppsRounded,
  },
  {
    id: "social",
    label: "Social",
    eyebrow: "Profile route",
    placeholder: "https://www.instagram.com/computemore",
    summary: "Route scans into a profile, link hub, or campaign social page.",
    Icon: ShareRounded,
  },
  {
    id: "pdf",
    label: "PDF",
    eyebrow: "Hosted asset",
    placeholder: "https://cdn.haveqr.dev/brochures/launch-pack.pdf",
    summary: "Send scans into brochures, menus, decks, or printable collateral.",
    Icon: PictureAsPdfRounded,
  },
  {
    id: "image",
    label: "Image",
    eyebrow: "Hosted asset",
    placeholder: "https://cdn.haveqr.dev/posters/flyer-front.jpg",
    summary: "Point straight into poster art, packaging, menus, or image galleries.",
    Icon: ImageRounded,
  },
  {
    id: "video",
    label: "Video",
    eyebrow: "Hosted asset",
    placeholder: "https://www.youtube.com/watch?v=launch-demo",
    summary: "Launch product demos, promo clips, or embedded training content.",
    Icon: SmartDisplayRounded,
  },
  {
    id: "whatsapp",
    label: "WhatsApp",
    eyebrow: "Fallback route",
    placeholder: "https://wa.me/260977000000?text=Hello%20from%20HaveQR",
    summary: "Use a direct URL or generate a wa.me route from phone and message fields.",
    Icon: WhatsApp,
  },
];

const designSections: Array<{
  id: DesignSectionId;
  label: string;
  description: string;
  Icon: IconComponent;
}> = [
  {
    id: "frame",
    label: "Frame",
    description: "Shape the finder markers and choose how the main QR body is drawn.",
    Icon: CropSquareRounded,
  },
  {
    id: "colour",
    label: "Colour",
    description: "Tune the marker colour, background, and the optional linear gradient on the data body.",
    Icon: PaletteRounded,
  },
  {
    id: "logo",
    label: "Logo",
    description: "Upload a centred SVG, PNG, or JPEG logo and control its size and backdrop padding.",
    Icon: AddPhotoAlternateRounded,
  },
];

const logoPresets: LogoPreset[] = [
  {
    id: "scan-me",
    label: "Scan Me",
    eyebrow: "Generic callout",
    assetPath: "/assets/qr-watermarks/scan-me-logo_in-qr-code.png",
  },
  {
    id: "link",
    label: "Link",
    eyebrow: "Landing route",
    assetPath: "/assets/qr-watermarks/link-logo_in-qr-code.png",
  },
  {
    id: "menu",
    label: "Menu",
    eyebrow: "Restaurant card",
    assetPath: "/assets/qr-watermarks/menu-logo_in-qr-code.png",
  },
  {
    id: "whatsapp",
    label: "WhatsApp",
    eyebrow: "Chat fallback",
    assetPath: "/assets/qr-watermarks/whatsapp-logo_in-qr-code.png",
  },
];

export function QrBuilder() {
  const formId = useId();
  const [selectedType, setSelectedType] = useState<BuilderType>(builderTypes[0]);
  const [designSection, setDesignSection] = useState<DesignSectionId>("frame");
  const [targetUrl, setTargetUrl] = useState("https://computemore.com/campaign/spring-launch");
  const [phone, setPhone] = useState("+260977000000");
  const [message, setMessage] = useState("Hello from HaveQR");
  const [finderBorder, setFinderBorder] = useState<FinderShape>("Rounded");
  const [finderCenter, setFinderCenter] = useState<FinderShape>("Circle");
  const [dataPattern, setDataPattern] = useState<QrDataPattern>("Square");
  const [gradientMode, setGradientMode] = useState<QrGradientMode>("Linear");
  const [gradientStart, setGradientStart] = useState("#10243C");
  const [gradientEnd, setGradientEnd] = useState("#1F61C0");
  const [gradientRotation, setGradientRotation] = useState("135");
  const [darkColor, setDarkColor] = useState("#10243C");
  const [lightColor, setLightColor] = useState("#FFFFFF");
  const [sizePx, setSizePx] = useState("1024");
  const [eccLevel, setEccLevel] = useState<ErrorCorrectionLevel>("H");
  const [logoAsset, setLogoAsset] = useState<UploadedLogo | null>(null);
  const [logoSizePercent, setLogoSizePercent] = useState("18");
  const [logoBackdropPadding, setLogoBackdropPadding] = useState("40");
  const [removeLogoBackground, setRemoveLogoBackground] = useState(true);
  const [selectedPresetId, setSelectedPresetId] = useState<string | null>(null);
  const [loadingPresetId, setLoadingPresetId] = useState<string | null>(null);
  const [acceptedJob, setAcceptedJob] = useState<RenderJobAcceptedResponse | null>(null);
  const [latestJob, setLatestJob] = useState<RenderJobStatusResponse | null>(null);
  const [queuedRequestKey, setQueuedRequestKey] = useState<string | null>(null);
  const [draftPreview, setDraftPreview] = useState<RenderDraftPreviewResponse | null>(null);
  const [draftPreviewRequestKey, setDraftPreviewRequestKey] = useState<string | null>(null);
  const [draftErrorMessage, setDraftErrorMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isPolling, setIsPolling] = useState(false);
  const [isDraftSyncing, setIsDraftSyncing] = useState(false);
  const draftPreviewCacheRef = useRef(new Map<string, RenderDraftPreviewResponse>());

  const normalizedTargetUrl = targetUrl.trim();
  const normalizedPhone = phone.trim();
  const normalizedMessage = message.trim();
  const parsedSizePx = Number.parseInt(sizePx, 10);
  const parsedGradientRotation = Number.parseInt(gradientRotation, 10);
  const parsedLogoSizePercent = Number.parseInt(logoSizePercent, 10);
  const parsedLogoBackdropPadding = Number.parseInt(logoBackdropPadding, 10);
  const hasValidSize = Number.isFinite(parsedSizePx) && parsedSizePx >= 256 && parsedSizePx <= 4096;
  const hasValidGradientRotation = Number.isFinite(parsedGradientRotation) && parsedGradientRotation >= 0 && parsedGradientRotation <= 360;
  const hasValidLogoSize = Number.isFinite(parsedLogoSizePercent) && parsedLogoSizePercent >= 12 && parsedLogoSizePercent <= 24;
  const hasValidBackdropPadding = Number.isFinite(parsedLogoBackdropPadding) && parsedLogoBackdropPadding >= 10 && parsedLogoBackdropPadding <= 80;
  const resolvedSizePx = hasValidSize ? parsedSizePx : 1024;
  const resolvedGradientRotation = hasValidGradientRotation ? parsedGradientRotation : 135;
  const resolvedLogoSize = hasValidLogoSize ? parsedLogoSizePercent : 18;
  const resolvedBackdropPadding = hasValidBackdropPadding ? parsedLogoBackdropPadding : 40;
  const activeDesignSection = designSections.find((section) => section.id === designSection) ?? designSections[0];
  const requestPreview = buildRenderRequest({
    typeId: selectedType.id,
    targetUrl: normalizedTargetUrl,
    phone: normalizedPhone,
    message: normalizedMessage,
    sizePx: resolvedSizePx,
    finderBorder,
    finderCenter,
    errorCorrectionLevel: eccLevel,
    darkColor,
    lightColor,
    dataPattern,
    gradientMode,
    gradientStart,
    gradientEnd,
    gradientRotation: resolvedGradientRotation,
    logoAsset,
    logoSizePercent: resolvedLogoSize,
    logoBackdropPadding: resolvedBackdropPadding,
    removeLogoBackground,
  });
  const requestPreviewKey = JSON.stringify(requestPreview);
  const requestValidationError = validateRequest({
    typeId: selectedType.id,
    targetUrl: normalizedTargetUrl,
    phone: normalizedPhone,
    hasValidSize,
    hasValidGradientRotation,
    hasValidLogoSize,
    hasValidBackdropPadding,
  });
  const canAutoPreview = requestValidationError === null;
  const previewPayload = resolvePreviewPayload(selectedType.id, normalizedTargetUrl, normalizedPhone, normalizedMessage);
  const previewTarget = resolvePreviewTarget(selectedType.id, normalizedTargetUrl, normalizedPhone, normalizedMessage);
  const activeAcceptedJob = queuedRequestKey === requestPreviewKey ? acceptedJob : null;
  const activeLatestJob = queuedRequestKey === requestPreviewKey ? latestJob : null;
  const activeDraftPreview = draftPreviewRequestKey === requestPreviewKey ? draftPreview : null;
  const currentStatus = activeLatestJob?.status ?? activeAcceptedJob?.status ?? null;
  const preferredArtifact = selectPreferredArtifact(activeLatestJob?.artifacts ?? []);
  const previewArtifact = selectPreviewArtifact(activeLatestJob?.artifacts ?? []);
  const localPreviewSvgMarkup = canAutoPreview && previewPayload
    ? buildLocalPreviewSvgMarkup(previewPayload, requestPreview, logoAsset)
    : null;
  const previewImageSrc = previewArtifact?.downloadUrl
    ?? (activeDraftPreview ? toSvgDataUrl(activeDraftPreview.svgMarkup) : null)
    ?? (localPreviewSvgMarkup ? toSvgDataUrl(localPreviewSvgMarkup) : null);
  const previewBadge = getPreviewBadge(currentStatus, isPolling, isDraftSyncing, Boolean(activeDraftPreview), Boolean(localPreviewSvgMarkup));
  const previewSourceLabel = getPreviewSourceLabel(Boolean(previewArtifact), Boolean(activeDraftPreview), Boolean(localPreviewSvgMarkup));
  const previewMessage = getPreviewMessage(
    activeAcceptedJob,
    activeLatestJob,
    isPolling,
    isDraftSyncing,
    Boolean(activeDraftPreview),
    Boolean(localPreviewSvgMarkup),
  );
  const timelineText = getTimelineText(activeAcceptedJob, activeLatestJob);
  const requestDump = summarizeRequest(requestPreview);
  const logoHint = getLogoHint(logoAsset, removeLogoBackground);
  const renderPlan = activeLatestJob?.artifacts.length
    ? activeLatestJob.artifacts.map((artifact) => artifact.format.toUpperCase()).join(" + ")
    : `PNG ${resolvedSizePx}px · ECC ${eccLevel}`;
  const resolvedPreviewTarget = activeLatestJob?.resolvedTargetUrl ?? activeDraftPreview?.resolvedTargetUrl ?? previewPayload ?? previewTarget;
  const previewImageAlt = previewArtifact
    ? "Rendered QR artifact preview"
    : activeDraftPreview
      ? "Draft QR preview"
      : "Local QR preview";

  useEffect(() => {
    if (!activeAcceptedJob?.statusUrl) {
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
        const response = await fetch(activeAcceptedJob.statusUrl, {
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
  }, [activeAcceptedJob?.jobId, activeAcceptedJob?.statusUrl]);

  useEffect(() => {
    if (!canAutoPreview || !previewPayload) {
      setIsDraftSyncing(false);
      setDraftErrorMessage(null);
      return;
    }

    const cachedPreview = draftPreviewCacheRef.current.get(requestPreviewKey);

    if (cachedPreview) {
      setDraftPreview(cachedPreview);
      setDraftPreviewRequestKey(requestPreviewKey);
      setDraftErrorMessage(null);
      setIsDraftSyncing(false);
      return;
    }

    let cancelled = false;
    const abortController = new AbortController();
    const timeoutId = window.setTimeout(async () => {
      setIsDraftSyncing(true);
      setDraftErrorMessage(null);

      try {
        const response = await fetch(resolveApiUrl("/api/v1/qr/render/draft"), {
          method: "POST",
          headers: {
            Accept: "application/json",
            "Content-Type": "application/json",
          },
          body: requestPreviewKey,
          cache: "no-store",
          signal: abortController.signal,
        });

        if (!response.ok) {
          throw new Error(await readErrorResponse(response, "Unable to sync the draft preview."));
        }

        const preview = (await response.json()) as RenderDraftPreviewResponse;

        if (cancelled) {
          return;
        }

        draftPreviewCacheRef.current.set(requestPreviewKey, preview);
        setDraftPreview(preview);
        setDraftPreviewRequestKey(requestPreviewKey);
      } catch (error) {
        if (cancelled || isAbortError(error)) {
          return;
        }

        setDraftErrorMessage(getErrorMessage(error));
      } finally {
        if (!cancelled) {
          setIsDraftSyncing(false);
        }
      }
    }, draftPreviewDebounceMs);

    return () => {
      cancelled = true;
      abortController.abort();
      window.clearTimeout(timeoutId);
    };
  }, [canAutoPreview, previewPayload, requestPreviewKey]);

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void queueRenderJob();
  };

  const queueRenderJob = async () => {
    if (requestValidationError) {
      setErrorMessage(requestValidationError);
      return;
    }

    setAcceptedJob(null);
    setLatestJob(null);
    setQueuedRequestKey(null);
    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      const response = await fetch(resolveApiUrl("/api/v1/qr/render"), {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: requestPreviewKey,
      });

      if (!response.ok) {
        throw new Error(await readErrorResponse(response, "Unable to queue render job."));
      }

      const accepted = (await response.json()) as RenderJobAcceptedResponse;
      setQueuedRequestKey(requestPreviewKey);
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

  const handleLogoChange = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";

    if (!file) {
      return;
    }

    if (file.size > maxLogoBytes) {
      setErrorMessage(`Logo uploads must be ${(maxLogoBytes / 1024).toFixed(0)} KB or smaller.`);
      return;
    }

    try {
      const uploadedLogo = await toUploadedLogo(file);
      setSelectedPresetId(null);
      setLogoSizePercent("18");
      setRemoveLogoBackground(uploadedLogo.sourceType !== "Svg");
      setLogoAsset(uploadedLogo);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handlePresetSelect = async (preset: LogoPreset) => {
    setLoadingPresetId(preset.id);

    try {
      const presetLogo = await loadPresetLogo(preset);
      setLogoAsset(presetLogo);
      setSelectedPresetId(preset.id);
      setLogoSizePercent("12");
      setLogoBackdropPadding("40");
      setRemoveLogoBackground(true);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    } finally {
      setLoadingPresetId(null);
    }
  };

  const handleRemoveLogo = () => {
    setLogoAsset(null);
    setSelectedPresetId(null);
    setErrorMessage(null);
  };

  return (
    <div className="generator-layout">
      <form id={formId} className="generator-form-column" onSubmit={handleSubmit}>
        <section className="generator-step-card">
          <div className="generator-step-head">
            <span className="generator-step-badge">1</span>
            <div className="generator-step-copy">
              <h2>Choose your QR type</h2>
              <p>Select one of the currently supported routes, then move straight into the content fields.</p>
            </div>
          </div>

          <div className="generator-type-grid">
            {builderTypes.map((builderType) => {
              const TypeIcon = builderType.Icon;
              const isActive = builderType.id === selectedType.id;

              return (
                <button
                  key={builderType.id}
                  type="button"
                  className={isActive ? "generator-type-button generator-type-button-active" : "generator-type-button"}
                  onClick={() => {
                    startTransition(() => {
                      setSelectedType(builderType);
                      setTargetUrl(builderType.id === "whatsapp" ? "" : builderType.placeholder);
                    });
                  }}
                >
                  <span className="generator-type-icon">
                    <TypeIcon fontSize="small" />
                  </span>

                  <span className="generator-type-label">
                    <strong>{builderType.label}</strong>
                    <span>{builderType.eyebrow}</span>
                  </span>
                </button>
              );
            })}
          </div>
        </section>

        <section className="generator-step-card">
          <div className="generator-step-head">
            <span className="generator-step-badge">2</span>
            <div className="generator-step-copy">
              <h2>Complete the content</h2>
              <p>{selectedType.summary}</p>
            </div>
          </div>

          <label className="field field-wide">
            <span>{selectedType.id === "whatsapp" ? "Target URL or leave blank for fallback generation" : "Enter your destination"}</span>
            <input value={targetUrl} onChange={(event) => setTargetUrl(event.target.value)} placeholder={selectedType.placeholder} />
            {selectedType.id === "whatsapp" ? (
              <small className="field-hint">Leave this blank to synthesise a wa.me route from the phone and message fields.</small>
            ) : null}
          </label>

          {selectedType.id === "whatsapp" ? (
            <div className="field-grid field-grid-two">
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
        </section>

        <section className="generator-step-card">
          <div className="generator-step-head">
            <span className="generator-step-badge">3</span>
            <div className="generator-step-copy">
              <h2>Design your QR code</h2>
              <p>Adjust the frame, colour system, and centred logo before you queue the worker render.</p>
            </div>
          </div>

          <div className="generator-design-tabs">
            {designSections.map((section) => {
              const SectionIcon = section.Icon;
              const isActive = designSection === section.id;

              return (
                <button
                  key={section.id}
                  type="button"
                  className={isActive ? "generator-design-tab generator-design-tab-active" : "generator-design-tab"}
                  onClick={() => setDesignSection(section.id)}
                >
                  <SectionIcon fontSize="small" />
                  <span>{section.label}</span>
                </button>
              );
            })}
          </div>

          <div className="generator-design-panel">
            <p className="generator-inline-note">{activeDesignSection.description}</p>

            {designSection === "frame" ? (
              <>
                <div className="field-grid field-grid-three">
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

                  <label className="field">
                    <span>Data pattern</span>
                    <select value={dataPattern} onChange={(event) => setDataPattern(event.target.value as QrDataPattern)}>
                      <option>Square</option>
                      <option>Dotted</option>
                    </select>
                  </label>
                </div>

                <p className="field-hint">Finder markers remain structurally separate from the optional data gradient layer.</p>
              </>
            ) : null}

            {designSection === "colour" ? (
              <>
                <div className="field-grid field-grid-two">
                  <label className="field">
                    <span>Marker colour</span>
                    <input type="color" value={darkColor} onChange={(event) => setDarkColor(event.target.value)} />
                  </label>

                  <label className="field">
                    <span>Background colour</span>
                    <input type="color" value={lightColor} onChange={(event) => setLightColor(event.target.value)} />
                  </label>
                </div>

                <div className="field-grid field-grid-two">
                  <label className="field">
                    <span>Data gradient</span>
                    <select value={gradientMode} onChange={(event) => setGradientMode(event.target.value as QrGradientMode)}>
                      <option>Linear</option>
                      <option>None</option>
                    </select>
                    <small className="field-hint">The gradient applies only to the main data modules, not to the finder markers.</small>
                  </label>

                  <label className="field">
                    <span>Gradient rotation</span>
                    <input type="range" min="0" max="360" value={gradientRotation} onChange={(event) => setGradientRotation(event.target.value)} />
                    <small className="field-hint">{resolvedGradientRotation}°</small>
                  </label>
                </div>

                <div className="field-grid field-grid-two">
                  <label className="field">
                    <span>Gradient start</span>
                    <input type="color" value={gradientStart} onChange={(event) => setGradientStart(event.target.value)} disabled={gradientMode === "None"} />
                  </label>

                  <label className="field">
                    <span>Gradient end</span>
                    <input type="color" value={gradientEnd} onChange={(event) => setGradientEnd(event.target.value)} disabled={gradientMode === "None"} />
                  </label>
                </div>
              </>
            ) : null}

            {designSection === "logo" ? (
              <>
                <label className="upload-dropzone" htmlFor="haveqr-logo-upload">
                  <input id="haveqr-logo-upload" type="file" accept="image/svg+xml,image/png,image/jpeg" onChange={handleLogoChange} />
                  <span className="upload-dropzone-icon">
                    <AddPhotoAlternateRounded fontSize="small" />
                  </span>
                  <strong>{logoAsset ? logoAsset.name : "Drop a logo or browse"}</strong>
                  <span>{logoAsset ? formatBytes(logoAsset.sizeBytes) : "SVG, PNG, or JPEG up to 512 KB"}</span>
                </label>

                <div className="logo-preset-shell">
                  <p className="field-hint">Popular watermark presets</p>

                  <div className="logo-preset-grid">
                    {logoPresets.map((preset) => {
                      const isActive = selectedPresetId === preset.id;
                      const isLoading = loadingPresetId === preset.id;

                      return (
                        <button
                          key={preset.id}
                          type="button"
                          className={isActive ? "logo-preset-button logo-preset-button-active" : "logo-preset-button"}
                          onClick={() => void handlePresetSelect(preset)}
                          disabled={loadingPresetId !== null}
                        >
                          <span className="logo-preset-thumb">
                            <img src={preset.assetPath} alt="" aria-hidden="true" />
                          </span>

                          <span className="logo-preset-copy">
                            <strong>{isLoading ? "Loading..." : preset.label}</strong>
                            <span>{preset.eyebrow}</span>
                          </span>
                        </button>
                      );
                    })}
                  </div>

                  <p className="field-hint">Choosing a preset keeps the logo centred and resets the logo size to 12% by default.</p>
                </div>

                <div className="field-grid field-grid-two">
                  <label className="field">
                    <span>Logo size</span>
                    <input type="range" min="12" max="24" value={logoSizePercent} onChange={(event) => setLogoSizePercent(event.target.value)} disabled={!logoAsset} />
                    <small className="field-hint">{resolvedLogoSize}% of the active QR area</small>
                  </label>

                  <label className="field">
                    <span>Backdrop padding</span>
                    <input type="range" min="10" max="80" value={logoBackdropPadding} onChange={(event) => setLogoBackdropPadding(event.target.value)} disabled={!logoAsset} />
                    <small className="field-hint">{resolvedBackdropPadding}% extra quiet area behind the logo</small>
                  </label>
                </div>

                <label className="toggle-row">
                  <input
                    type="checkbox"
                    checked={removeLogoBackground}
                    onChange={(event) => setRemoveLogoBackground(event.target.checked)}
                    disabled={!logoAsset || logoAsset.sourceType === "Svg"}
                  />
                  <span>Use heuristic background removal for raster logo uploads.</span>
                </label>

                <div className="upload-meta-row">
                  <p>{logoHint}</p>
                  {logoAsset ? (
                    <button className="text-action" type="button" onClick={handleRemoveLogo}>
                      Remove logo
                    </button>
                  ) : null}
                </div>
              </>
            ) : null}
          </div>

          <div className="generator-output-bar">
            <div className="generator-output-label">
              <TuneRounded fontSize="small" />
              <span>Output and resilience</span>
            </div>

            <div className="field-grid field-grid-two">
              <label className="field">
                <span>PNG size</span>
                <input type="number" min="256" max="4096" value={sizePx} onChange={(event) => setSizePx(event.target.value)} />
                {!hasValidSize ? <small className="field-hint field-hint-error">Use a value from 256 to 4096.</small> : null}
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
          </div>
        </section>

        <details className="generator-developer-drawer">
          <summary>Developer payload preview</summary>
          <pre className="builder-json">{requestDump}</pre>
        </details>
      </form>

      <aside className="generator-preview-card">
        <div className="generator-step-head">
          <span className="generator-step-badge">4</span>
          <div className="generator-step-copy">
            <h2>Preview and download</h2>
            <p>Preview updates locally first, then syncs against the SVG draft endpoint before you queue the final worker render.</p>
          </div>
        </div>

        <div className="generator-preview-meta">
          <span className={previewBadge.className}>{previewBadge.label}</span>
          <span className="generator-api-chip">{previewSourceLabel}</span>
        </div>

        <div className="preview-stage">
          {previewImageSrc ? (
            <img className="preview-artifact" src={previewImageSrc} alt={previewImageAlt} />
          ) : (
            <div className="preview-placeholder">
              <strong>Enter a valid destination to start the live preview.</strong>
              <span>The builder switches from local SVG preview to server draft sync automatically once the request validates.</span>
            </div>
          )}
        </div>

        <div className="generator-preview-actions">
          <button className="preview-primary-action" form={formId} type="submit" disabled={isSubmitting}>
            <AutoAwesomeRounded fontSize="small" />
            <span>{isSubmitting ? "Generating..." : activeAcceptedJob ? "Generate another QR code" : "Generate QR code"}</span>
          </button>

          <button className="preview-secondary-action" type="button" disabled={!preferredArtifact} onClick={handlePreferredDownload}>
            <CloudDownloadRounded fontSize="small" />
            <span>Download QR code</span>
          </button>
        </div>

        <div className="preview-feedback" aria-live="polite">
          {previewMessage ? <p className="feedback-text">{previewMessage}</p> : null}
          {draftErrorMessage ? <p className="feedback-text feedback-text-error">{draftErrorMessage}</p> : null}
          {errorMessage ? <p className="feedback-text feedback-text-error">{errorMessage}</p> : null}
        </div>

        <div className="preview-summary-grid">
          <article className="preview-summary-card">
            <p className="preview-label">Resolved target</p>
            <p className="preview-value">{resolvedPreviewTarget}</p>
          </article>

          <article className="preview-summary-card">
            <p className="preview-label">Style</p>
            <p className="preview-value">{describeStyling(dataPattern, gradientMode, logoAsset)}</p>
          </article>

          <article className="preview-summary-card">
            <p className="preview-label">Render plan</p>
            <p className="preview-value">{renderPlan}</p>
          </article>

          <article className="preview-summary-card">
            <p className="preview-label">Timeline</p>
            <p className="preview-value">{timelineText}</p>
          </article>
        </div>

        {activeLatestJob?.artifacts.length ? (
          <div className="artifact-row">
            {activeLatestJob.artifacts.map((artifact) => (
              <a key={artifact.format} className="artifact-link" href={artifact.downloadUrl}>
                {artifact.format.toUpperCase()} · {formatBytes(artifact.sizeBytes)}
              </a>
            ))}
          </div>
        ) : null}
      </aside>
    </div>
  );
}

function buildRenderRequest(input: {
  typeId: BuilderTypeId;
  targetUrl: string;
  phone: string;
  message: string;
  sizePx: number;
  finderBorder: FinderShape;
  finderCenter: FinderShape;
  errorCorrectionLevel: ErrorCorrectionLevel;
  darkColor: string;
  lightColor: string;
  dataPattern: QrDataPattern;
  gradientMode: QrGradientMode;
  gradientStart: string;
  gradientEnd: string;
  gradientRotation: number;
  logoAsset: UploadedLogo | null;
  logoSizePercent: number;
  logoBackdropPadding: number;
  removeLogoBackground: boolean;
}): SubmitRenderJobRequest {
  return {
    contentType: toContentType(input.typeId),
    targetUrl: input.typeId === "whatsapp" && input.targetUrl.length === 0 ? undefined : input.targetUrl || undefined,
    payload:
      input.typeId === "whatsapp"
        ? {
            ...(input.phone ? { phone: input.phone } : {}),
            ...(input.message ? { message: input.message } : {}),
          }
        : {},
    errorCorrectionLevel: input.errorCorrectionLevel,
    output: {
      sizePx: input.sizePx,
    },
    finder: {
      borderShape: input.finderBorder,
      centerShape: input.finderCenter,
    },
    colors: {
      dark: input.darkColor,
      light: input.lightColor,
    },
    data: {
      pattern: input.dataPattern,
      gradientMode: input.gradientMode,
      gradientStart: input.gradientMode === "Linear" ? input.gradientStart : undefined,
      gradientEnd: input.gradientMode === "Linear" ? input.gradientEnd : undefined,
      gradientRotation: input.gradientRotation,
    },
    ...(input.logoAsset
      ? {
          logo: {
            sourceType: input.logoAsset.sourceType,
            svg: input.logoAsset.svg,
            contentBase64: input.logoAsset.contentBase64,
            contentType: input.logoAsset.contentType,
            sizePercent: input.logoSizePercent,
            removeBackground: input.removeLogoBackground,
            backdropPaddingPercent: input.logoBackdropPadding,
          },
        }
      : {}),
  };
}

function resolveApiUrl(path: string) {
  const resolvedUrl = new URL(path, `${publicApiBaseUrl}/`);

  if (resolvedUrl.origin === publicApiOrigin) {
    return `${apiProxyPrefix}${resolvedUrl.pathname}${resolvedUrl.search}`;
  }

  return resolvedUrl.toString();
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
  const previewPayload = resolvePreviewPayload(typeId, targetUrl, phone, message);

  if (previewPayload) {
    return previewPayload;
  }

  if (typeId !== "whatsapp") {
    return "Provide an absolute URL to activate live preview.";
  }

  return "Provide a WhatsApp URL or phone number.";
}

function resolvePreviewPayload(typeId: BuilderTypeId, targetUrl: string, phone: string, message: string) {
  if (typeId !== "whatsapp") {
    return targetUrl.length > 0 ? normalizeAbsoluteHttpUrl(targetUrl) : null;
  }

  if (targetUrl.length > 0) {
    return normalizeAbsoluteHttpUrl(targetUrl);
  }

  const normalizedPhone = phone.replace(/[^\d+]/g, "");

  if (normalizedPhone.length === 0) {
    return null;
  }

  const baseUrl = `https://wa.me/${normalizedPhone}`;
  return message ? `${baseUrl}?text=${encodeURIComponent(message)}` : baseUrl;
}

function validateRequest(input: {
  typeId: BuilderTypeId;
  targetUrl: string;
  phone: string;
  hasValidSize: boolean;
  hasValidGradientRotation: boolean;
  hasValidLogoSize: boolean;
  hasValidBackdropPadding: boolean;
}) {
  if (!input.hasValidSize) {
    return "PNG size must be between 256 and 4096 pixels.";
  }

  if (!input.hasValidGradientRotation) {
    return "Gradient rotation must stay between 0 and 360 degrees.";
  }

  if (!input.hasValidLogoSize) {
    return "Logo size must stay between 12 and 24 percent.";
  }

  if (!input.hasValidBackdropPadding) {
    return "Logo backdrop padding must stay between 10 and 80 percent.";
  }

  if (input.targetUrl.length > 0 && !isAbsoluteHttpUrl(input.targetUrl)) {
    return "Target URL must be a valid absolute http or https URL.";
  }

  if (input.typeId === "whatsapp") {
    const normalizedPhone = input.phone.replace(/[^\d+]/g, "");
    return input.targetUrl.length === 0 && normalizedPhone.length === 0
      ? "Provide a WhatsApp URL or a phone number for the fallback link."
      : null;
  }

  return input.targetUrl.length === 0 ? "Target URL is required for this content type." : null;
}

function isAbsoluteHttpUrl(value: string) {
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch {
    return false;
  }
}

function normalizeAbsoluteHttpUrl(value: string) {
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:" ? url.toString() : null;
  } catch {
    return null;
  }
}

function isTerminalStatus(status: QrJobStatus) {
  return status === "Completed" || status === "Failed";
}

function selectPreferredArtifact(artifacts: RenderArtifactDescriptor[]) {
  return artifacts.find((artifact) => artifact.format.toLowerCase() === "png") ?? artifacts[0] ?? null;
}

function selectPreviewArtifact(artifacts: RenderArtifactDescriptor[]) {
  return selectPreferredArtifact(artifacts);
}

function getPreviewBadge(
  status: QrJobStatus | null,
  isPolling: boolean,
  isDraftSyncing: boolean,
  hasDraftPreview: boolean,
  hasLocalPreview: boolean,
) {
  if (!status) {
    if (isDraftSyncing) {
      return { className: "status-chip status-chip-syncing", label: "Syncing draft" };
    }

    if (hasDraftPreview) {
      return { className: "status-chip status-chip-draft", label: "Draft synced" };
    }

    if (hasLocalPreview) {
      return { className: "status-chip status-chip-local", label: "Local preview" };
    }

    return { className: "status-chip status-chip-neutral", label: "Waiting for input" };
  }

  if (status === "Running" && isPolling) {
    return { className: "status-chip status-chip-running", label: "Rendering" };
  }

  if (status === "Completed") {
    return { className: "status-chip status-chip-completed", label: "Artifacts ready" };
  }

  if (status === "Failed") {
    return { className: "status-chip status-chip-failed", label: "Render failed" };
  }

  return { className: `status-chip status-chip-${status.toLowerCase()}`, label: status };
}

function getPreviewSourceLabel(hasPreviewArtifact: boolean, hasDraftPreview: boolean, hasLocalPreview: boolean) {
  if (hasPreviewArtifact) {
    return "Final render";
  }

  if (hasDraftPreview) {
    return "Server draft";
  }

  if (hasLocalPreview) {
    return "Local SVG";
  }

  return "Preview idle";
}

function getPreviewMessage(
  acceptedJob: RenderJobAcceptedResponse | null,
  latestJob: RenderJobStatusResponse | null,
  isPolling: boolean,
  isDraftSyncing: boolean,
  hasDraftPreview: boolean,
  hasLocalPreview: boolean,
) {
  if (acceptedJob) {
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

  if (isDraftSyncing) {
    return "Syncing the live preview against the SVG draft endpoint.";
  }

  if (hasDraftPreview) {
    return "Preview synced from the server draft. Generate when you need final downloadable artifacts.";
  }

  if (hasLocalPreview) {
    return "Local SVG preview is active. Keep editing and the server draft will follow automatically.";
  }

  return "Enter a valid destination to activate live preview.";
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

function describeStyling(dataPattern: QrDataPattern, gradientMode: QrGradientMode, logoAsset: UploadedLogo | null) {
  const parts = [dataPattern === "Dotted" ? "Dotted data modules" : "Square data modules"];

  if (gradientMode === "Linear") {
    parts.push("linear gradient on the data body");
  }

  if (logoAsset) {
    parts.push(`${logoAsset.sourceType} center logo`);
  }

  return parts.join(" · ");
}

function getLogoHint(logoAsset: UploadedLogo | null, removeLogoBackground: boolean) {
  if (!logoAsset) {
    return "Upload a centered logo to have the worker embed it on top of a safe backdrop in the middle of the QR.";
  }

  if (logoAsset.sourceType === "Svg") {
    return "SVG logos are sanitized and embedded as vector content in the final SVG render path.";
  }

  return removeLogoBackground
    ? "Raster logos use a local cutout heuristic aimed at common flat or light backgrounds before the worker embeds them."
    : "Raster logos stay intact and are placed over a clean backdrop in the middle of the QR.";
}

function summarizeRequest(request: SubmitRenderJobRequest) {
  if (!request.logo) {
    return JSON.stringify(request, null, 2);
  }

  return JSON.stringify(
    {
      ...request,
      logo:
        request.logo.sourceType === "Svg"
          ? {
              ...request.logo,
              svg: request.logo.svg ? `[svg payload omitted: ${request.logo.svg.length} chars]` : undefined,
            }
          : {
              ...request.logo,
              contentBase64: request.logo.contentBase64
                ? `[base64 payload omitted: ${request.logo.contentBase64.length} chars]`
                : undefined,
            },
    },
    null,
    2,
  );
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

function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === "AbortError";
}

function toSvgDataUrl(svgMarkup: string) {
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svgMarkup)}`;
}

function buildLocalPreviewSvgMarkup(encodedPayload: string, request: SubmitRenderJobRequest, logoAsset: UploadedLogo | null) {
  const qr = qrcodeGenerator(0, request.errorCorrectionLevel);
  qr.addData(encodedPayload);
  qr.make();

  const moduleCount = qr.getModuleCount();
  const parts: string[] = [
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${moduleCount} ${moduleCount}" role="img" aria-label="HaveQR code">`,
  ];

  appendLocalPreviewDefs(parts, request);
  parts.push(`<rect x="0" y="0" width="${moduleCount}" height="${moduleCount}" fill="${request.colors.light}"/>`);
  parts.push(`<g id="haveqr-data-modules" fill="${resolveLocalPreviewDataFill(request)}" shape-rendering="geometricPrecision">`);

  for (let row = 0; row < moduleCount; row += 1) {
    for (let column = 0; column < moduleCount; column += 1) {
      if (!qr.isDark(row, column) || isInPreviewFinderWindow(row, column, moduleCount)) {
        continue;
      }

      if (request.data.pattern === "Dotted") {
        parts.push(
          `<circle cx="${formatPreviewNumber(column + 0.5)}" cy="${formatPreviewNumber(row + 0.5)}" r="${formatPreviewNumber(previewDottedRadius)}"/>`,
        );
        continue;
      }

      parts.push(`<rect x="${column}" y="${row}" width="1" height="1"/>`);
    }
  }

  parts.push("</g>");
  parts.push(buildLocalPreviewFinderOverlay(moduleCount, request.finder.borderShape, request.finder.centerShape, request.colors.dark, request.colors.light));

  if (logoAsset && request.logo) {
    appendLocalPreviewLogo(parts, logoAsset, request.logo.sizePercent, request.logo.backdropPaddingPercent, request.colors.light, moduleCount);
  }

  parts.push("</svg>");
  return parts.join("");
}

function appendLocalPreviewDefs(parts: string[], request: SubmitRenderJobRequest) {
  if (request.data.gradientMode !== "Linear" || !request.data.gradientStart || !request.data.gradientEnd) {
    return;
  }

  const angle = request.data.gradientRotation * (Math.PI / 180);
  const x1 = 0.5 - (0.5 * Math.cos(angle));
  const y1 = 0.5 - (0.5 * Math.sin(angle));
  const x2 = 0.5 + (0.5 * Math.cos(angle));
  const y2 = 0.5 + (0.5 * Math.sin(angle));

  parts.push("<defs>");
  parts.push(
    `<linearGradient id="haveqr-data-gradient" gradientUnits="objectBoundingBox" x1="${formatPreviewNumber(x1)}" y1="${formatPreviewNumber(y1)}" x2="${formatPreviewNumber(x2)}" y2="${formatPreviewNumber(y2)}">`,
  );
  parts.push(`<stop offset="0%" stop-color="${request.data.gradientStart}"/>`);
  parts.push(`<stop offset="100%" stop-color="${request.data.gradientEnd}"/>`);
  parts.push("</linearGradient>");
  parts.push("</defs>");
}

function resolveLocalPreviewDataFill(request: SubmitRenderJobRequest) {
  return request.data.gradientMode === "Linear" ? "url(#haveqr-data-gradient)" : request.colors.dark;
}

function buildLocalPreviewFinderOverlay(
  moduleCount: number,
  finderBorder: FinderShape,
  finderCenter: FinderShape,
  darkColor: string,
  lightColor: string,
) {
  const origins: Array<[number, number]> = [
    [previewQuietZoneModules, previewQuietZoneModules],
    [moduleCount - previewQuietZoneModules - previewFinderSizeModules, previewQuietZoneModules],
    [previewQuietZoneModules, moduleCount - previewQuietZoneModules - previewFinderSizeModules],
  ];
  const parts = ["<g id=\"haveqr-finder-compositor\" shape-rendering=\"geometricPrecision\">"];

  for (const [originX, originY] of origins) {
    appendLocalPreviewRect(parts, originX, originY, previewFinderSizeModules, previewFinderSizeModules, lightColor, 0);
    appendLocalPreviewShape(parts, finderBorder, originX, originY, previewFinderSizeModules, darkColor);
    appendLocalPreviewShape(parts, finderBorder, originX + 1, originY + 1, previewFinderInnerSizeModules, lightColor);
    appendLocalPreviewShape(parts, finderCenter, originX + 2, originY + 2, previewFinderCenterSizeModules, darkColor);
  }

  parts.push("</g>");
  return parts.join("");
}

function appendLocalPreviewShape(parts: string[], shape: FinderShape, x: number, y: number, size: number, fill: string) {
  if (shape === "Circle") {
    appendLocalPreviewCircle(parts, x + (size / 2), y + (size / 2), size / 2, fill);
    return;
  }

  appendLocalPreviewRect(parts, x, y, size, size, fill, shape === "Rounded" ? getPreviewRoundedCornerRadius(size) : 0);
}

function appendLocalPreviewRect(parts: string[], x: number, y: number, width: number, height: number, fill: string, cornerRadius: number) {
  if (cornerRadius > 0) {
    parts.push(
      `<rect x="${formatPreviewNumber(x)}" y="${formatPreviewNumber(y)}" width="${formatPreviewNumber(width)}" height="${formatPreviewNumber(height)}" fill="${fill}" rx="${formatPreviewNumber(cornerRadius)}" ry="${formatPreviewNumber(cornerRadius)}"/>`,
    );
    return;
  }

  parts.push(
    `<rect x="${formatPreviewNumber(x)}" y="${formatPreviewNumber(y)}" width="${formatPreviewNumber(width)}" height="${formatPreviewNumber(height)}" fill="${fill}"/>`,
  );
}

function appendLocalPreviewCircle(parts: string[], centerX: number, centerY: number, radius: number, fill: string) {
  parts.push(
    `<circle cx="${formatPreviewNumber(centerX)}" cy="${formatPreviewNumber(centerY)}" r="${formatPreviewNumber(radius)}" fill="${fill}"/>`,
  );
}

function appendLocalPreviewLogo(
  parts: string[],
  logoAsset: UploadedLogo,
  sizePercent: number,
  backdropPaddingPercent: number,
  backdropFill: string,
  moduleCount: number,
) {
  const activeCodeSize = moduleCount - (previewQuietZoneModules * 2);
  const logoBoxSize = activeCodeSize * (sizePercent / 100);
  const aspectRatio = logoAsset.aspectRatio > 0 ? logoAsset.aspectRatio : 1;
  const logoWidth = aspectRatio >= 1 ? logoBoxSize : logoBoxSize * aspectRatio;
  const logoHeight = aspectRatio >= 1 ? logoBoxSize / aspectRatio : logoBoxSize;
  const x = (moduleCount - logoWidth) / 2;
  const y = (moduleCount - logoHeight) / 2;
  const backdropWidth = logoWidth * (1 + (backdropPaddingPercent / 100));
  const backdropHeight = logoHeight * (1 + (backdropPaddingPercent / 100));
  const backdropX = (moduleCount - backdropWidth) / 2;
  const backdropY = (moduleCount - backdropHeight) / 2;
  const cornerRadius = Math.min(backdropWidth, backdropHeight) * 0.22;

  parts.push(`<g id="haveqr-logo"><rect x="${formatPreviewNumber(backdropX)}" y="${formatPreviewNumber(backdropY)}" width="${formatPreviewNumber(backdropWidth)}" height="${formatPreviewNumber(backdropHeight)}" rx="${formatPreviewNumber(cornerRadius)}" ry="${formatPreviewNumber(cornerRadius)}" fill="${backdropFill}"/>`);
  parts.push(
    `<image x="${formatPreviewNumber(x)}" y="${formatPreviewNumber(y)}" width="${formatPreviewNumber(logoWidth)}" height="${formatPreviewNumber(logoHeight)}" href="${escapeSvgAttribute(logoAsset.previewUrl)}" preserveAspectRatio="xMidYMid meet"/>`,
  );
  parts.push("</g>");
}

function getPreviewRoundedCornerRadius(size: number) {
  if (size <= previewFinderCenterSizeModules) {
    return 0.85;
  }

  if (size <= previewFinderInnerSizeModules) {
    return 1.25;
  }

  return 1.75;
}

function isInPreviewFinderWindow(row: number, column: number, moduleCount: number) {
  const maxOrigin = moduleCount - previewQuietZoneModules - previewFinderSizeModules;
  return (row >= previewQuietZoneModules && row < previewQuietZoneModules + previewFinderSizeModules && column >= previewQuietZoneModules && column < previewQuietZoneModules + previewFinderSizeModules)
    || (row >= previewQuietZoneModules && row < previewQuietZoneModules + previewFinderSizeModules && column >= maxOrigin && column < maxOrigin + previewFinderSizeModules)
    || (row >= maxOrigin && row < maxOrigin + previewFinderSizeModules && column >= previewQuietZoneModules && column < previewQuietZoneModules + previewFinderSizeModules);
}

function formatPreviewNumber(value: number) {
  return Number.isInteger(value) ? value.toString() : value.toFixed(3).replace(/\.?0+$/, "");
}

function escapeSvgAttribute(value: string) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/"/g, "&quot;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

async function loadPresetLogo(preset: LogoPreset) {
  const response = await fetch(preset.assetPath, { cache: "force-cache" });

  if (!response.ok) {
    throw new Error(`Unable to load the ${preset.label} preset logo.`);
  }

  const blob = await response.blob();
  const file = new File([blob], preset.assetPath.split("/").pop() ?? `${preset.id}.png`, {
    type: blob.type || "image/png",
  });

  return toUploadedLogo(file);
}

async function toUploadedLogo(file: File): Promise<UploadedLogo> {
  if (file.type === "image/svg+xml") {
    const svg = await readFileAsText(file);
    const previewUrl = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
    const aspectRatio = await readImageAspectRatio(previewUrl);

    return {
      name: file.name,
      previewUrl,
      sizeBytes: file.size,
      aspectRatio,
      sourceType: "Svg",
      svg,
    };
  }

  if (file.type === "image/png" || file.type === "image/jpeg") {
    const dataUrl = await readFileAsDataUrl(file);
    const [, contentBase64] = dataUrl.split(",", 2);
    const aspectRatio = await readImageAspectRatio(dataUrl);

    return {
      name: file.name,
      previewUrl: dataUrl,
      sizeBytes: file.size,
      aspectRatio,
      sourceType: file.type === "image/png" ? "Png" : "Jpeg",
      contentBase64,
      contentType: file.type,
    };
  }

  throw new Error("Upload an SVG, PNG, or JPEG logo.");
}

function readFileAsText(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error("Unable to read the uploaded SVG logo."));
    reader.onload = () => resolve(typeof reader.result === "string" ? reader.result : "");
    reader.readAsText(file);
  });
}

function readFileAsDataUrl(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error("Unable to read the uploaded raster logo."));
    reader.onload = () => resolve(typeof reader.result === "string" ? reader.result : "");
    reader.readAsDataURL(file);
  });
}

function readImageAspectRatio(source: string) {
  return new Promise<number>((resolve) => {
    const image = new Image();

    image.onload = () => {
      if (image.naturalWidth > 0 && image.naturalHeight > 0) {
        resolve(image.naturalWidth / image.naturalHeight);
        return;
      }

      resolve(1);
    };

    image.onerror = () => resolve(1);
    image.src = source;
  });
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