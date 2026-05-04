"use client"; // This is a client component, it uses state and effects and is interactive, so we need this directive at the top of the file

// Some imports here
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

// --- INFO ----
// The reason why we are here using types instead of the clssical typescript lib/api.ts is that the api is abstracting all functionality, i.e. SSR is king
// there are no client-side maps or complex data strcture building for example in classical e-commerce we can have catalog to cart item maps, which here are not needed

// IconComponent is a helper type
type IconComponent = typeof LinkRounded;

// BuilderType is the main type for the QR builder, 
// representing each content route type and its associated metadata
// IconComponet is runtime injected
type BuilderType = {
  id: BuilderTypeId;
  label: string;
  eyebrow: string;
  placeholder: string;
  summary: string;
  Icon: IconComponent;
};

// Here we have types for the various request and response shapes for the QR rendering API,
// These are all strongly typed and strongly agted to certain values of those types
type BuilderTypeId = "link" | "app" | "social" | "pdf" | "image" | "video" | "whatsapp";
type DesignSectionId = "frame" | "colour" | "logo";
type QrContentType = "Link" | "App" | "Social" | "Pdf" | "Image" | "Video" | "WhatsApp";
type QrJobStatus = "Queued" | "Running" | "Completed" | "Failed";
type FinderShape = "Square" | "Rounded" | "Circle";
type ErrorCorrectionLevel = "L" | "M" | "Q" | "H";
type QrDataPattern = "Square" | "Dotted";
type QrGradientMode = "None" | "Linear";
type QrLogoSourceType = "Svg" | "Png" | "Jpeg";

// UploadedLogo is the type for the logo asset that a user can upload in the builder, it includes both the original file metadata and the processed content ready for API submission
type UploadedLogo = {
  name: string;
  previewUrl: string;
  sizeBytes: number;
  aspectRatio: number;
  wasOptimized: boolean;
  sourceType: QrLogoSourceType;
  svg?: string;
  contentBase64?: string;
  contentType?: string;
};

type PreviewLogoLayout = {
  x: number;
  y: number;
  width: number;
  height: number;
  backdropX: number;
  backdropY: number;
  backdropWidth: number;
  backdropHeight: number;
  cornerRadius: number;
};

// These types represent the shapes of the requests we send to the API and the responses we receive, 
// all strongly typed for safety and clarity
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

// These types represent the shapes of the responses we receive from the API when we submit a render job, poll for status, and retrieve artifacts
type RenderJobAcceptedResponse = {
  jobId: string;
  status: QrJobStatus;
  statusUrl: string;
};

// These types represent the shapes of the responses we receive from the API when we request a draft preview, which is a lightweight rendering of the QR based on the current builder state, without queuing a full render job
type RenderArtifactDescriptor = {
  format: string;
  contentType: string;
  sizeBytes: number;
  downloadUrl: string;
};

// This type represents the shape of the response we receive when we poll for the status of a render job, which includes the current status, any resolved target URL, and the list of artifacts available when the job is completed
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

// This type represents the shape of the response we receive when we request a draft preview, which includes a resolved target URL, the encoded payload, and the SVG markup for the preview
type RenderDraftPreviewResponse = {
  resolvedTargetUrl: string;
  encodedPayload: string;
  configurationHash: string;
  payloadHash: string;
  svgMarkup: string;
};

// Here we define some constants for the builder, including the available content types, design sections, logo presets, and various configuration values for polling intervals, size limits, etc.
type LogoPreset = {
  id: string;
  label: string;
  eyebrow: string;
  assetPath: string;
};

// Here we introduce some const values for the builder, such as the available content types, design sections, logo presets, and various configuration values for polling intervals, size limits, etc.
const publicApiBaseUrl = (process.env.NEXT_PUBLIC_HALFQR_API_BASE_URL ?? "https://api.halfqr.com")
  .trim()
  .replace(/\/$/, "");
const publicApiOrigin = new URL(publicApiBaseUrl).origin;
const apiProxyPrefix = "/api/halfqr";

// A few more varibles regarding byte-parsing and render timing, these are used in the builder logic for validating uploads and managing the polling lifecycle
const maxLogoBytes = 512 * 1024;
const maxRasterLogoPixels = 2_000_000;
const pollIntervalMs = 1500;
const retryIntervalMs = 3000;
const draftPreviewDebounceMs = 450;
const previewQuietZoneModules = 4;
const previewFinderSizeModules = 7;
const previewFinderInnerSizeModules = 5;
const previewFinderCenterSizeModules = 3;
const previewDottedRadius = 0.38;
const defaultDarkColor = "#000000";
const defaultLightColor = "#FFFFFF";
const defaultGradientEnd = "#1F61C0";
const pngSizeOptions = [1024, 768, 512, 256, 128] as const;

// Hard-coded array that speicfies each builder type as selcted by the user, this is fired to our render job
const builderTypes: BuilderType[] = [
  {
    id: "link",
    label: "Link",
    eyebrow: "Website",
    placeholder: "https://computemore.com",
    summary: "Classic static destination for pages, campaigns, and one-off flows.",
    Icon: LinkRounded,
  },
  {
    id: "app",
    label: "App",
    eyebrow: "Google/Apple App Store Links",
    placeholder: "https://apps.apple.com/app/id123456789",
    summary: "Deep-link into app stores, onboarding flows, or install landing pages.",
    Icon: AppsRounded,
  },
  {
    id: "social",
    label: "Social",
    eyebrow: "Socials Profile Link",
    placeholder: "https://www.linkedin.com/paulnamalomba",
    summary: "Route scans into a profile, link hub, or campaign social page.",
    Icon: ShareRounded,
  },
  {
    id: "pdf",
    label: "PDF",
    eyebrow: "Link to a PDF",
    placeholder: "https://cdn.halfqr.com/brochures/launch-pack.pdf",
    summary: "Send scans into brochures, menus, decks, or printable collateral.",
    Icon: PictureAsPdfRounded,
  },
  {
    id: "image",
    label: "Image",
    eyebrow: "Link to an Image",
    placeholder: "https://cdn.halfqr.com/posters/flyer-front.jpg",
    summary: "Point straight into poster art, packaging, menus, or image galleries.",
    Icon: ImageRounded,
  },
  {
    id: "video",
    label: "Video",
    eyebrow: "Map to a Video Link",
    placeholder: "https://www.youtube.com/watch?v=<etc>",
    summary: "Launch product demos, promo clips, or embedded training content.",
    Icon: SmartDisplayRounded,
  },
  {
    id: "whatsapp",
    label: "WhatsApp",
    eyebrow: "WhatsApp Contact Link",
    placeholder: "https://wa.me/260977000000?text=Hello%20from%20HalfQR",
    summary: "Use a direct URL or generate a wa.me route from phone and message fields.",
    Icon: WhatsApp,
  },
];

// Design sections represent the different categories of visual styling that users can configure in the builder, such as the frame, colour, and logo settings
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

// Logo presets represent the different pre-defined logos that users can choose from, each with an ID, label, eyebrow, asset path, and a flag indicating whether to remove the background by default
const logoPresets: LogoPreset[] = [
  {
    id: "scan-me",
    label: "Scan Me",
    eyebrow: "Generic callout",
    assetPath: "/assets/qr-watermarks/scan-me-logo_in-qr-code.svg",
  },
  {
    id: "link",
    label: "Link",
    eyebrow: "Landing route",
    assetPath: "/assets/qr-watermarks/link-logo_in-qr-code.svg",
  },
  {
    id: "menu",
    label: "Menu",
    eyebrow: "Restaurant card",
    assetPath: "/assets/qr-watermarks/menu-logo_in-qr-code.svg",
  },
  {
    id: "whatsapp",
    label: "WhatsApp",
    eyebrow: "Chat fallback",
    assetPath: "/assets/qr-watermarks/whatsapp-logo_in-qr-code.svg",
  },
];

// Now we make exposed some functions and classes (methods) that are used by our builder component, these include the main QrBuilder component which is the default export, as well as some helper functions for building requests, validating input, and managing the preview state
export function QrBuilder() {
  // useId is a React hook that generates a unique ID for the form, this is used for accessibility and to associate labels with inputs
  const formId = useId();

  // --- State Variables ---
  // Arrays using prescribed data structures and api shapes for the builder state, these are used to manage the user input and the API interactions in a strongly typed way
  const [selectedType, setSelectedType] = useState<BuilderType>(builderTypes[0]);
  const [designSection, setDesignSection] = useState<DesignSectionId>("frame");
  const [targetUrl, setTargetUrl] = useState("https://computemore.com");
  const [phone, setPhone] = useState("+260977000000");
  const [message, setMessage] = useState("Hello from HalfQR");
  const [finderBorder, setFinderBorder] = useState<FinderShape>("Rounded");
  const [finderCenter, setFinderCenter] = useState<FinderShape>("Circle");
  const [dataPattern, setDataPattern] = useState<QrDataPattern>("Square");
  const [gradientMode, setGradientMode] = useState<QrGradientMode>("Linear");
  const [gradientStart, setGradientStart] = useState(defaultDarkColor);
  const [gradientEnd, setGradientEnd] = useState(defaultGradientEnd);
  const [gradientRotation, setGradientRotation] = useState("135");
  const [darkColor, setDarkColor] = useState(defaultDarkColor);
  const [lightColor, setLightColor] = useState(defaultLightColor);
  const [sizePx, setSizePx] = useState("1024");
  const [eccLevel, setEccLevel] = useState<ErrorCorrectionLevel>("H");
  const [logoAsset, setLogoAsset] = useState<UploadedLogo | null>(null);
  const [logoSizePercent, setLogoSizePercent] = useState("18");
  const [logoBackdropPadding, setLogoBackdropPadding] = useState("40");
  const [removeLogoBackground, setRemoveLogoBackground] = useState(false);
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
  
  // We use a ref to store a cache of draft previews keyed by the stringified request payload, this allows us to avoid making redundant API calls for draft previews when the user toggles between design sections or makes changes that don't affect the preview output
  const draftPreviewCacheRef = useRef(new Map<string, RenderDraftPreviewResponse>());

  // Here we have some derived state variables that compute values based on the current builder state, such as the normalized and validated input values, the request payload for the API, the preview image source, and various labels and messages to display in the UI
  const normalizedTargetUrl = targetUrl.trim();
  const normalizedPhone = phone.trim();
  const normalizedMessage = message.trim();
  const parsedSizePx = Number.parseInt(sizePx, 10);
  const parsedGradientRotation = Number.parseInt(gradientRotation, 10);
  const parsedLogoSizePercent = Number.parseInt(logoSizePercent, 10);
  const parsedLogoBackdropPadding = Number.parseInt(logoBackdropPadding, 10);
  const hasValidSize = Number.isFinite(parsedSizePx) && pngSizeOptions.includes(parsedSizePx as (typeof pngSizeOptions)[number]);
  const hasValidGradientRotation = Number.isFinite(parsedGradientRotation) && parsedGradientRotation >= 0 && parsedGradientRotation <= 360;
  const hasValidLogoSize = Number.isFinite(parsedLogoSizePercent) && parsedLogoSizePercent >= 12 && parsedLogoSizePercent <= 24;
  const hasValidBackdropPadding = Number.isFinite(parsedLogoBackdropPadding) && parsedLogoBackdropPadding >= 10 && parsedLogoBackdropPadding <= 80;
  const resolvedSizePx = hasValidSize ? parsedSizePx : 1024;
  const resolvedGradientRotation = hasValidGradientRotation ? parsedGradientRotation : 135;
  const resolvedLogoSize = hasValidLogoSize ? parsedLogoSizePercent : 18;
  const resolvedBackdropPadding = hasValidBackdropPadding ? parsedLogoBackdropPadding : 40;
  const activeDesignSection = designSections.find((section) => section.id === designSection) ?? designSections[0];
  
  // --- Request Payloads ---
  // JSON-shaped request payload that we would send to the API when submitting a render job, this is derived from the current builder state and is used for both the actual API submission and for generating the draft preview
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

  // Validate the request payload to determine if we can automatically generate a preview
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

  // --- Derived Preview State ---
  const previewPayload = resolvePreviewPayload(selectedType.id, normalizedTargetUrl, normalizedPhone, normalizedMessage);
  const previewTarget = resolvePreviewTarget(selectedType.id, normalizedTargetUrl, normalizedPhone, normalizedMessage);
  const activeAcceptedJob = queuedRequestKey === requestPreviewKey ? acceptedJob : null;
  const activeLatestJob = queuedRequestKey === requestPreviewKey ? latestJob : null;
  const activeDraftPreview = draftPreviewRequestKey === requestPreviewKey ? draftPreview : null;
  const currentStatus = activeLatestJob?.status ?? activeAcceptedJob?.status ?? null;
  const preferredArtifact = selectPreferredArtifact(activeLatestJob?.artifacts ?? []);
  const previewArtifact = selectPreviewArtifact(activeLatestJob?.artifacts ?? []);
  const configuredApiTarget = formatApiTarget(publicApiBaseUrl);
  const rawLocalPreviewSvgMarkup = canAutoPreview && previewPayload
    ? buildLocalPreviewSvgMarkup(previewPayload, requestPreview, logoAsset)
    : null;
  const localPreviewSvgMarkup = activeDraftPreview ? null : rawLocalPreviewSvgMarkup;
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
    // Quickly short-circuit if we don't have an active job with a status URL to poll, this avoids setting up the polling lifecycle when it's not needed
    if (!activeAcceptedJob?.statusUrl) {
      return;
    }

    let cancelled = false;
    let timeoutId: number | undefined;
    setIsPolling(true);

    // If not cancelled and we have a status URL, we schedule the next poll with the specified delay, this function is used to manage the polling lifecycle and ensure we keep polling at the right intervals until we get a terminal status or encounter an error
    const scheduleNext = (delayMs: number) => {
      if (!cancelled) {
        timeoutId = window.setTimeout(runPoll, delayMs);
      }
    };

    // Polling the api for the status of the active job, we fetch the status URL and handle the response, if we get a successful response we update the latest job state and check if we reached a terminal status, if we get an error we set the error message and schedule a retry
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
      setRemoveLogoBackground(false);
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
      setRemoveLogoBackground(false);
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
              {/* <p>Select one of the currently supported routes, then move straight into the content fields.</p> */}
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
                  {/* <span className="generator-type-icon"> */}
                    <TypeIcon fontSize="medium" />
                  {/* </span> */}

                  <span className="generator-type-label">
                    <strong>{builderType.label}</strong>
                    {/* <span>{builderType.eyebrow}</span> */}
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
              <h2>Your content routes...</h2>
            </div>
          </div>
          {/* <p>{selectedType.summary}</p> */}

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
              <p>Adjust the frame, colour system, and centred logo/brand imagery</p>
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

                {/* <p className="field-hint">Finder markers remain structurally separate from the optional data gradient layer.</p> */}
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
                <label className="upload-dropzone" htmlFor="halfqr-logo-upload">
                  <input id="halfqr-logo-upload" type="file" accept="image/svg+xml,image/png,image/jpeg" onChange={handleLogoChange} />
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
                  <span>Try to make the raster logo background transparent before embedding it.</span>
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
                <select value={sizePx} onChange={(event) => setSizePx(event.target.value)}>
                  {pngSizeOptions.map((option) => (
                    <option key={option} value={option.toString()}>
                      {option}
                    </option>
                  ))}
                </select>
                {!hasValidSize ? <small className="field-hint field-hint-error">Use one of: 1024, 768, 512, 256, or 128.</small> : null}
              </label>

              <label className="field">
                <span>Render quality</span>
                <select value={eccLevel} onChange={(event) => setEccLevel(event.target.value as ErrorCorrectionLevel)}>
                  <option value="L">Low</option>
                  <option value="M">Medium</option>
                  <option value="Q">Quartile</option>
                  <option value="H">High</option>
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
          </div>
        </div>
        <p>Preview your generated QR code.</p>

        {/* <div className="generator-preview-meta">
          <span className={previewBadge.className}>{previewBadge.label}</span>
          <span className="generator-api-chip">{previewSourceLabel}</span>
        </div> */}

        {/* <div className="preview-stage"> */}
          {previewImageSrc ? (
            <img className="preview-artifact" src={previewImageSrc} alt={previewImageAlt} />
          ) : (
            <div className="preview-placeholder">
              <strong>Enter a valid destination to start the live preview.</strong>
              <span>The builder switches from local SVG preview to server draft sync automatically once the request validates.</span>
            </div>
          )}
        {/* </div> */}

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

        {/* <div className="preview-summary-grid"> */}
          {/* <article className="preview-summary-card">
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
          </article> */}

          {/* <article className="preview-summary-card">
            <p className="preview-label">API target</p>
            <p className="preview-value">{configuredApiTarget}</p>
          </article> */}
        {/* </div> */}

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
    return "PNG size must be one of: 1024, 768, 512, 256, or 128 pixels.";
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
    return "Upload a centered logo to have the worker reserve a safe middle window before embedding the asset.";
  }

  if (logoAsset.sourceType === "Svg") {
    return "SVG logos are sanitized and embedded as vector content in the final SVG render path.";
  }

  if (logoAsset.wasOptimized) {
    return removeLogoBackground
      ? "Raster logo was scaled down locally before upload, and the background-removal heuristic remains opt-in for light or flat artwork."
      : "Raster logo was scaled down locally before upload so draft preview and final render requests stay within supported image limits.";
  }

  return removeLogoBackground
    ? "Raster logos now use an opt-in cutout heuristic to try to make flat or light backgrounds transparent before the worker embeds them."
    : "Raster logos stay intact by default; enable background removal only when you want the worker to try making the background transparent.";
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

function formatApiTarget(apiBaseUrl: string) {
  try {
    const url = new URL(apiBaseUrl);
    const originLabel = `${url.hostname}${url.port ? `:${url.port}` : ""}`;

    return ["localhost", "127.0.0.1"].includes(url.hostname)
      ? `Local API via ${originLabel}`
      : `Hosted API via ${originLabel}`;
  } catch {
    return apiBaseUrl;
  }
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

  const sourceModuleCount = qr.getModuleCount();
  const moduleCount = sourceModuleCount + (previewQuietZoneModules * 2);
  const logoLayout = logoAsset && request.logo
    ? resolvePreviewLogoLayout(logoAsset.aspectRatio, request.logo.sizePercent, request.logo.backdropPaddingPercent, moduleCount)
    : null;
  const parts: string[] = [
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${moduleCount} ${moduleCount}" role="img" aria-label="HalfQR code">`,
  ];

  appendLocalPreviewDefs(parts, request, moduleCount);
  parts.push(`<rect x="0" y="0" width="${moduleCount}" height="${moduleCount}" fill="${request.colors.light}"/>`);
  parts.push(`<g id="halfqr-data-modules" fill="${resolveLocalPreviewDataFill(request)}" shape-rendering="geometricPrecision">`);

  for (let row = 0; row < sourceModuleCount; row += 1) {
    for (let column = 0; column < sourceModuleCount; column += 1) {
      if (!qr.isDark(row, column)) {
        continue;
      }

      const paddedRow = row + previewQuietZoneModules;
      const paddedColumn = column + previewQuietZoneModules;

      if (isInPreviewFinderWindow(paddedRow, paddedColumn, moduleCount) || isInPreviewLogoSafeRegion(paddedRow, paddedColumn, logoLayout)) {
        continue;
      }

      if (request.data.pattern === "Dotted") {
        parts.push(
          `<circle cx="${formatPreviewNumber(paddedColumn + 0.5)}" cy="${formatPreviewNumber(paddedRow + 0.5)}" r="${formatPreviewNumber(previewDottedRadius)}"/>`,
        );
        continue;
      }

      parts.push(`<rect x="${paddedColumn}" y="${paddedRow}" width="1" height="1"/>`);
    }
  }

  parts.push("</g>");
  parts.push(buildLocalPreviewFinderOverlay(moduleCount, request.finder.borderShape, request.finder.centerShape, request.colors.dark, request.colors.light));

  if (logoAsset && logoLayout) {
    appendLocalPreviewLogo(parts, logoAsset, request.colors.light, logoLayout);
  }

  parts.push("</svg>");
  return parts.join("");
}

function appendLocalPreviewDefs(parts: string[], request: SubmitRenderJobRequest, moduleCount: number) {
  if (request.data.gradientMode !== "Linear" || !request.data.gradientStart || !request.data.gradientEnd) {
    return;
  }

  const { x1, y1, x2, y2 } = resolvePreviewGradientVector(moduleCount, request.data.gradientRotation);

  parts.push("<defs>");
  parts.push(
    `<linearGradient id="halfqr-data-gradient" gradientUnits="userSpaceOnUse" x1="${formatPreviewNumber(x1)}" y1="${formatPreviewNumber(y1)}" x2="${formatPreviewNumber(x2)}" y2="${formatPreviewNumber(y2)}">`,
  );
  parts.push(`<stop offset="0%" stop-color="${request.data.gradientStart}"/>`);
  parts.push(`<stop offset="100%" stop-color="${request.data.gradientEnd}"/>`);
  parts.push("</linearGradient>");
  parts.push("</defs>");
}

function resolveLocalPreviewDataFill(request: SubmitRenderJobRequest) {
  return request.data.gradientMode === "Linear" ? "url(#halfqr-data-gradient)" : request.colors.dark;
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
  const parts = ["<g id=\"halfqr-finder-compositor\" shape-rendering=\"geometricPrecision\">"];

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
  backdropFill: string,
  layout: PreviewLogoLayout,
) {
  parts.push(`<g id="halfqr-logo"><rect x="${formatPreviewNumber(layout.backdropX)}" y="${formatPreviewNumber(layout.backdropY)}" width="${formatPreviewNumber(layout.backdropWidth)}" height="${formatPreviewNumber(layout.backdropHeight)}" rx="${formatPreviewNumber(layout.cornerRadius)}" ry="${formatPreviewNumber(layout.cornerRadius)}" fill="${backdropFill}"/>`);
  parts.push(
    `<image x="${formatPreviewNumber(layout.x)}" y="${formatPreviewNumber(layout.y)}" width="${formatPreviewNumber(layout.width)}" height="${formatPreviewNumber(layout.height)}" href="${escapeSvgAttribute(logoAsset.previewUrl)}" preserveAspectRatio="xMidYMid meet"/>`,
  );
  parts.push("</g>");
}

function resolvePreviewGradientVector(moduleCount: number, rotation: number) {
  const angle = rotation * (Math.PI / 180);
  const directionX = Math.cos(angle);
  const directionY = Math.sin(angle);
  const activeCodeSize = moduleCount - (previewQuietZoneModules * 2);
  const center = moduleCount / 2;
  const maxDirection = Math.max(Math.abs(directionX), Math.abs(directionY), Number.EPSILON);
  const halfSpan = (activeCodeSize / 2) / maxDirection;

  return {
    x1: center - (directionX * halfSpan),
    y1: center - (directionY * halfSpan),
    x2: center + (directionX * halfSpan),
    y2: center + (directionY * halfSpan),
  };
}

function resolvePreviewLogoLayout(aspectRatio: number, sizePercent: number, backdropPaddingPercent: number, moduleCount: number): PreviewLogoLayout {
  const activeCodeSize = moduleCount - (previewQuietZoneModules * 2);
  const logoBoxSize = activeCodeSize * (sizePercent / 100);
  const resolvedAspectRatio = aspectRatio > 0 ? aspectRatio : 1;
  const width = resolvedAspectRatio >= 1 ? logoBoxSize : logoBoxSize * resolvedAspectRatio;
  const height = resolvedAspectRatio >= 1 ? logoBoxSize / resolvedAspectRatio : logoBoxSize;
  const x = (moduleCount - width) / 2;
  const y = (moduleCount - height) / 2;
  const backdropWidth = width * (1 + (backdropPaddingPercent / 100));
  const backdropHeight = height * (1 + (backdropPaddingPercent / 100));
  const backdropX = (moduleCount - backdropWidth) / 2;
  const backdropY = (moduleCount - backdropHeight) / 2;
  const cornerRadius = Math.min(backdropWidth, backdropHeight) * 0.22;

  return {
    x,
    y,
    width,
    height,
    backdropX,
    backdropY,
    backdropWidth,
    backdropHeight,
    cornerRadius,
  };
}

function isInPreviewLogoSafeRegion(row: number, column: number, layout: PreviewLogoLayout | null) {
  if (!layout) {
    return false;
  }

  const moduleCenterX = column + 0.5;
  const moduleCenterY = row + 0.5;

  return moduleCenterX >= layout.backdropX
    && moduleCenterX <= layout.backdropX + layout.backdropWidth
    && moduleCenterY >= layout.backdropY
    && moduleCenterY <= layout.backdropY + layout.backdropHeight;
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
      wasOptimized: false,
      sourceType: "Svg",
      svg,
    };
  }

  if (file.type === "image/png" || file.type === "image/jpeg") {
    const normalizedRaster = await normalizeRasterLogo(file);

    return {
      name: file.name,
      previewUrl: normalizedRaster.dataUrl,
      sizeBytes: normalizedRaster.sizeBytes,
      aspectRatio: normalizedRaster.aspectRatio,
      wasOptimized: normalizedRaster.wasOptimized,
      sourceType: file.type === "image/png" ? "Png" : "Jpeg",
      contentBase64: normalizedRaster.contentBase64,
      contentType: file.type,
    };
  }

  throw new Error("Upload an SVG, PNG, or JPEG logo.");
}

async function normalizeRasterLogo(file: File) {
  const dataUrl = await readFileAsDataUrl(file);
  const image = await loadImage(dataUrl, "Unable to decode the uploaded raster logo.");

  if (image.naturalWidth <= 0 || image.naturalHeight <= 0) {
    throw new Error("Raster logos must expose valid image dimensions.");
  }

  const aspectRatio = image.naturalWidth / image.naturalHeight;
  const targetDimensions = resolveRasterDimensions(image.naturalWidth, image.naturalHeight);

  if (targetDimensions.width === image.naturalWidth && targetDimensions.height === image.naturalHeight) {
    const [, contentBase64 = ""] = dataUrl.split(",", 2);

    return {
      dataUrl,
      contentBase64,
      aspectRatio,
      sizeBytes: file.size,
      wasOptimized: false,
    };
  }

  const resizedDataUrl = resizeRasterDataUrl(image, targetDimensions.width, targetDimensions.height, file.type);
  const [, contentBase64 = ""] = resizedDataUrl.split(",", 2);

  return {
    dataUrl: resizedDataUrl,
    contentBase64,
    aspectRatio,
    sizeBytes: estimateBase64Bytes(contentBase64),
    wasOptimized: true,
  };
}

function resolveRasterDimensions(width: number, height: number) {
  const pixelCount = width * height;

  if (pixelCount <= maxRasterLogoPixels) {
    return { width, height };
  }

  const scale = Math.sqrt(maxRasterLogoPixels / pixelCount);
  let scaledWidth = Math.max(1, Math.floor(width * scale));
  let scaledHeight = Math.max(1, Math.floor(height * scale));

  while ((scaledWidth * scaledHeight) > maxRasterLogoPixels) {
    if (scaledWidth >= scaledHeight && scaledWidth > 1) {
      scaledWidth -= 1;
      continue;
    }

    if (scaledHeight > 1) {
      scaledHeight -= 1;
      continue;
    }

    break;
  }

  return { width: scaledWidth, height: scaledHeight };
}

function resizeRasterDataUrl(image: HTMLImageElement, width: number, height: number, mimeType: string) {
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;

  const context = canvas.getContext("2d");

  if (!context) {
    throw new Error("Unable to prepare the uploaded raster logo.");
  }

  context.clearRect(0, 0, width, height);
  context.drawImage(image, 0, 0, width, height);
  return canvas.toDataURL(mimeType, mimeType === "image/jpeg" ? 0.92 : undefined);
}

function estimateBase64Bytes(contentBase64: string) {
  const sanitized = contentBase64.replace(/=+$/, "");
  return Math.floor((sanitized.length * 3) / 4);
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

function loadImage(source: string, errorMessage = "Unable to load the uploaded image.") {
  return new Promise<HTMLImageElement>((resolve, reject) => {
    const image = new Image();

    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error(errorMessage));
    image.src = source;
  });
}

function readImageAspectRatio(source: string) {
  return new Promise<number>((resolve) => {
    void loadImage(source)
      .then((image) => {
        if (image.naturalWidth > 0 && image.naturalHeight > 0) {
          resolve(image.naturalWidth / image.naturalHeight);
          return;
        }

        resolve(1);
      })
      .catch(() => resolve(1));
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