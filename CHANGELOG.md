# HalfQR

All notable changes to the **HalfQR** project will be documented in this file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Contents

- [HalfQR](#halfqr)
  - [Contents](#contents)
  - [\[0.2.9.0\] - 2026-04-22](#0290---2026-04-22)
    - [Added](#added)
    - [Changed](#changed)
    - [Validation](#validation)
  - [\[0.2.8.0\] - 2026-04-22](#0280---2026-04-22)
    - [Added](#added-1)
    - [Changed](#changed-1)
    - [Validation](#validation-1)
  - [\[0.2.7.0\] - 2026-04-10](#0270---2026-04-10)
    - [Added](#added-2)
    - [Fixed](#fixed)
    - [Changed](#changed-2)
    - [Validation](#validation-2)
  - [\[0.2.6.0\] - 2026-04-10](#0260---2026-04-10)
    - [Fixed](#fixed-1)
    - [Changed](#changed-3)
    - [Validation](#validation-3)
  - [\[0.2.5.0\] - 2026-04-10](#0250---2026-04-10)
    - [Changed](#changed-4)
  - [\[0.2.4.0\] - 2026-04-10](#0240---2026-04-10)
    - [Changed](#changed-5)
    - [Fixed](#fixed-2)
    - [Validation](#validation-4)
  - [\[0.2.3.5\] - 2026-04-10](#0235---2026-04-10)
    - [Added](#added-3)
  - [\[0.2.3.4\] - 2026-04-10](#0234---2026-04-10)
    - [Added](#added-4)
  - [\[0.2.3.3\] - 2026-04-10](#0233---2026-04-10)
    - [Fixed](#fixed-3)
    - [Why](#why)
    - [Problem Solved](#problem-solved)
    - [Validation](#validation-5)
  - [\[0.2.3.2\] - 2026-04-10](#0232---2026-04-10)
    - [Changed](#changed-6)
    - [Validation](#validation-6)
  - [\[0.2.3.1\] - 2026-04-10](#0231---2026-04-10)
    - [Added](#added-5)
  - [\[0.2.3.0\] - 2026-04-10](#0230---2026-04-10)
    - [Added](#added-6)
    - [Changed](#changed-7)
    - [Validation](#validation-7)
  - [\[0.2.2.2\] - 2026-04-10](#0222---2026-04-10)
    - [Added](#added-7)
  - [\[0.2.2.1\] - 2026-04-10](#0221---2026-04-10)
    - [Added](#added-8)
    - [Changed](#changed-8)
    - [Validation](#validation-8)
  - [\[0.2.2.0\] - 2026-04-09](#0220---2026-04-09)
    - [Added](#added-9)
    - [Changed](#changed-9)
    - [Validation](#validation-9)
  - [\[0.2.1.0\] - 2026-04-09](#0210---2026-04-09)
    - [Added](#added-10)
    - [Changed](#changed-10)
    - [Validation](#validation-10)
  - [\[0.2.0.0\] - 2026-04-09](#0200---2026-04-09)
    - [Added](#added-11)
    - [Changed](#changed-11)
    - [Validation](#validation-11)
  - [\[0.1.1.0\] - 2026-04-09](#0110---2026-04-09)
    - [Changes](#changes)
  - [Validation:](#validation-12)
  - [\[0.1.0.0\] - 2026-04-09](#0100---2026-04-09)
    - [Changes](#changes-1)

---

## [0.2.9.0] - 2026-04-22

### Added

- Continued Fast versioning

### Changed

- Completely ovarhauled file names, envs and all other related issues to be hooked to HalfQR not HaveQR, as well as updating all the docs and guides to reflect this change. This is in preparation for the upcoming public release, where we want to have a more unique and memorable name that is still closely related to QR codes.
- Preparing for domain deployment and public release by updating all references to the project name and ensuring consistency across all documentation and codebase. This includes changing the website title, updating the README, and modifying any internal references to the project name in the code and documentation.

### Validation

- dotnet build HalfQR.sln
- npm run build from webapp
- Verified the reported R2 PDF URL decodes successfully through the new CLI for `Pdf` and `Link` renders without a logo
- Verified the same URL decodes successfully through the new CLI with the curated `menu` and `scan-me` preset logos applied

---

## [0.2.8.0] - 2026-04-22

### Added

- Continued Fast versioning

### Changed

- Changed the builder preview flow to prefer the server draft once it is available, keeping the on-screen QR closer to the authoritative backend render.

### Validation

- dotnet build HalfQR.sln
- npm run build from webapp
- Verified the reported R2 PDF URL decodes successfully through the new CLI for `Pdf` and `Link` renders without a logo
- Verified the same URL decodes successfully through the new CLI with the curated `menu` and `scan-me` preset logos applied

---

## [0.2.7.0] - 2026-04-10

### Added

- Added a `verify-render` command to `HalfQR.Cli` so draft SVG and final PNG artifacts can be rendered, decoded, and written to disk from the same backend pipeline used by the product.
- Added curated transparent SVG preset watermark assets for `Scan Me`, `Link`, `Menu`, and `WhatsApp` so preset logos no longer depend on brittle raster background removal.
- Added a shared `QrArtifactRasterizer` helper in `HalfQR.QrEngine` so SVG-to-PNG conversion is reusable across render validation and production rendering flows.

### Fixed

- Fixed the local preview matrix geometry to include the same quiet-zone padding used by the backend renderer, so preview placement no longer drifts away from the server-generated QR layout.
- Fixed the preset watermark path that could produce opaque or blackout center logos by switching the builder to transparent SVG presets.
- Fixed draft render failures surfacing as opaque server errors by returning a validation-style message when payload encoding cannot be completed.

### Changed

- Changed the builder preview flow to prefer the server draft once it is available, keeping the on-screen QR closer to the authoritative backend render.
- Changed the preview summary to show whether the builder is currently targeting a local API or the hosted API, removing ambiguity during render debugging.

### Validation

- dotnet build HalfQR.sln
- npm run build from webapp
- Verified the reported R2 PDF URL decodes successfully through the new CLI for `Pdf` and `Link` renders without a logo
- Verified the same URL decodes successfully through the new CLI with the curated `menu` and `scan-me` preset logos applied

---

## [0.2.6.0] - 2026-04-10

### Fixed

- Fixed the centered logo render path so the QR now reserves a true safe middle window before the logo is embedded, instead of painting the watermark on top of live QR data modules.
- Fixed the gradient rendering logic in both the local preview and backend SVG composer by switching the gradient to QR-wide user-space coordinates, so the data body now shows a visible directional gradient instead of looking flat.
- Fixed the default dark colour so finder markers and dark data modules now start from pure black until the user changes the colour settings.

### Changed

- Aligned the local preview geometry and backend SVG geometry so centered logo sizing, backdrop padding, and data-module exclusion use the same layout rules.
- Updated the builder guidance text to reflect the new safe-window logo behavior rather than implying the logo simply sits on top of the code.

### Validation

- npm run build from webapp
- dotnet build HalfQR.sln
- Verified the local preview SVG now emits a QR-wide gradient definition with `gradientUnits="userSpaceOnUse"`
- Verified the local backend render path now produces SVG and PNG artifacts with a visible gradient and a cleared centered logo window

---

## [0.2.5.0] - 2026-04-10

### Changed

- Fixed the non-loading assets
- Moving forward the versioning quickly

---

## [0.2.4.0] - 2026-04-10

### Changed

- Continued the CSS-first frontend cleanup by keeping layout, spacing, breakpoint, and visual rules in `globals.css` while leaving JSX focused on state, routing, and interaction behavior.
- Refined the header and hero presentation with updated spacing, brand sizing, theme tokens, and explicit hero text colours so the top-of-page content reads cleanly against the darker shell background.
- Updated the header component to use `next/image` for the brand mark and to close the mobile menu automatically on route changes and when the viewport returns to desktop widths.

### Fixed

- Fixed the mobile navigation dropdown so it stays anchored to the right side of the header actions and no longer drifts or overflows left on narrow screens.
- Fixed small-screen overflow in the QR type selector by switching the cards to a tighter wrapping grid, reducing card density, and hiding secondary copy where space is limited.
- Fixed small-screen overflow in the Popular Watermark Presets selector by applying the same wrapped-grid treatment, smaller thumbnails, and safer text wrapping inside the card.
- Added overflow guards such as `min-width: 0`, `overflow-wrap: anywhere`, and clipped horizontal bleed protection so nested grids and long labels stop forcing parent containers wider than intended.

### Validation

- npm run build from webapp
- Verified the QR type selector at a 390px mobile viewport renders across wrapped rows with zero horizontal overflow
- Verified the watermark preset selector at a 390px mobile viewport renders across wrapped rows with zero horizontal overflow

---

## [0.2.3.5] - 2026-04-10

### Added

- So now what we are doing is completely bootstrapping sisplays in css and functionality in jsx, no cross-confusion, only tweaking displays in jsx if necessary but avoiding that as much as possible
- Added active state styling to the mobile menu links, so when a user clicks a link in the mobile menu, it gets highlighted to indicate it's the current page. This improves navigation clarity on mobile devices.
- Updated the mobile menu link styles to ensure they are visually distinct and provide clear feedback on interaction, enhancing the overall user experience on mobile devices.
- Updated all css by adding block-style html comments so that we can easily identify which styles correspond to which components or features, making future maintenance and updates easier for developers.

---

## [0.2.3.4] - 2026-04-10

### Added

- Started documenting code improvements.

---

## [0.2.3.3] - 2026-04-10

### Fixed

- Normalized uploaded and preset raster logos in the webapp before they enter builder state, automatically scaling PNG and JPEG assets down when their image area exceeds the supported threshold while preserving aspect ratio.
- Added optimized-logo metadata and updated the Logo-tab guidance so the builder explains when a raster asset was scaled locally instead of failing later in the render pipeline.
- Kept raster background removal opt-in while making the upload path safer, so large raster logos can be previewed and rendered without silently changing other logo settings.

### Why

- The existing upload guard only checked file size in bytes. That allowed highly compressed but very large-dimension raster logos to pass validation even though the render path still had to decode and embed the full pixel area.
- Those requests could fail during draft preview or final rendering on stricter or older runtimes, producing an avoidable server-side error for an asset the builder should have normalized before submission.
- Fixing the issue in the webapp protects both manual uploads and preset raster logos without requiring users to preprocess artwork themselves.

### Problem Solved

- Prevented large-dimension PNG and JPEG logos from breaking draft preview sync when they were under the byte limit but still too large in raw image area.
- Removed the class of failures where the builder looked healthy locally but the server draft request errored as soon as the raster logo was included.
- Preserved the existing behavior for SVG logos and already-safe raster files, so only oversized raster inputs are transformed.

### Validation

- npm run build from webapp
- Verified the previously failing high-resolution raster logo path now scales the image locally and completes draft preview successfully
- Verified optimized raster logos keep their aspect ratio and continue through the normal final render request flow

---

## [0.2.3.2] - 2026-04-10

### Changed

- Switched raster logo background removal in the webapp to an explicit user-controlled checkbox instead of auto-enabling it for uploaded or preset raster logos.
- Defaulted the builder background-removal toggle to false, so raster logos stay intact unless the user deliberately opts into transparent-background cleanup.
- Updated the Logo-tab helper copy to explain that raster background removal is an opt-in transparency pass rather than a default upload behavior.

### Validation

- npm run build
- Verified the builder keeps raster logo background removal unchecked after manual upload and preset selection until the checkbox is explicitly enabled

---

## [0.2.3.1] - 2026-04-10

### Added

- Generated favicon and app icons for the web app, surfaced in the builder UI and as part of the public assets.

---

## [0.2.3.0] - 2026-04-10

### Added

- Added a synchronous SVG-only draft preview route at `POST /api/v1/qr/render/draft`, returning resolved target metadata plus SVG markup without queueing a worker job or generating PNG output.
- Added browser-side local SVG preview generation in the `webapp` so the builder can render styled QR previews immediately while the server draft sync catches up.
- Added Logo-tab preset selection backed by `webapp/public/assets/qr-watermarks`, with preset logos loading as centered overlays and defaulting to `12%` logo size.

### Changed

- Reworked the builder preview lifecycle into three stages: instant local preview, debounced server draft preview, and final worker artifact preview after an explicit render job completes.
- Updated the builder to debounce and cache draft-preview requests, ignore stale preview responses, and prevent outdated completed jobs from overwriting the current request state.
- Replaced the old approximate preview behavior with a more realistic SVG preview path that reflects the active data styling configuration, including live gradient settings and centered logo placement.
- Expanded the builder status UI and styles to distinguish `Local preview`, `Draft synced`, and final render states, and to surface the new preset-logo controls.

### Validation

- `dotnet build HalfQR.sln`
- `npm run build` from `webapp`
- Verified `POST /api/v1/qr/render/draft` returns `200` for a styled link render request against the local PublicApi
- Verified the builder transitions from local SVG preview to server draft preview
- Verified selecting a logo preset loads the image and resets logo size to `12%`

---

## [0.2.2.2] - 2026-04-10

### Added

- Implemented the styled QR slice end to end. The backend now accepts data styling and richer logo inputs, renders square or dotted data modules with linear gradients limited to the main QR body, and supports centered SVG, PNG, and JPEG logos with backdrop handling and raster background removal. 
- The webapp now uses the HalfQR favicon, ships a real landing page plus rebuilt builder UI, exposes the new styling controls, and swaps to worker artifacts instead of a fake preview once a job completes.
- Added a same-origin proxy in the webapp so local use no longer fails on browser CORS when talking to the hosted API.
- Completely implemeneted the `webapp` and it's components and pages, written in TypeScript with React and Next.js, and styled with Material UI and Emotion, copying over some boilerplate and patterns from afriflex.
  - The webapp as externally tunneled to `halfqr.computemore.com` is now the primary demo and test interface for the project, replacing the previous Postman collection and direct API calls.
- Completely overhauled the api as well

---

## [0.2.2.1] - 2026-04-10

### Added

- Added first-party Docker build assets for `webapp`, `HalfQR.PublicApi`, and `HalfQR.Worker`, plus a root `.dockerignore` for the monorepo bench.
- Added Compose-managed `webapp`, `public-api`, and `worker` services with host ports `5173` and `8083` and a shared render-job volume for the file-backed async pipeline.

### Changed

- Switched the webapp from same-origin rewrite calls to direct public API calls using `NEXT_PUBLIC_HALFQR_API_BASE_URL`, while normalizing relative job and artifact URLs against the public API origin.
- Added configurable CORS and JSON string-enum serialization in `HalfQR.PublicApi`, and aligned persisted render-job JSON with the string-based request and response contract used by the webapp.
- Wired canonical R2 artifact settings through Docker Compose so the API and worker can switch from filesystem artifacts to Cloudflare R2 through environment configuration.
- Disabled AWS streaming payload signing and default checksum validation on R2 uploads so Cloudflare R2 accepts the .NET S3 `PutObject` flow.
- Updated operator docs and release-facing metadata for the current Dockerized devops/testing bench at `halfqr.computemore.com` and `halfqr-api-demo.computemore.com`.

### Validation

- `npm run build` from `webapp`
- `docker compose config`
- `docker compose build webapp public-api worker`
- `docker compose up -d`
- Verified `http://127.0.0.1:8083/healthz` and `http://127.0.0.1:5173`
- Verified a live render job queue, completion, and PNG artifact download through the Docker bench
- Verified the Cloudflare R2 upload-format fix; the currently provided bucket credentials still return `Access Denied` on write, so the local `.env` keeps `RENDER_ARTIFACT_PROVIDER=FileSystem` until bucket permissions are corrected
- `dotnet build HalfQR.sln` is still blocked on this machine because the installed SDK is `9.0.312` while the solution targets `.NET 10.0`

---

## [0.2.2.0] - 2026-04-09

### Added

- Added a same-origin Next.js rewrite so the webapp can call the render API through `/api/v1/qr/*` without browser-side CORS workarounds.
- Added webapp job polling and artifact download actions for generated SVG and PNG outputs.
- Added a finder-pattern SVG compositor in `HalfQR.QrEngine` so `square`, `rounded`, and `circle` now affect the three finder markers without changing normal data modules.
- Added provider-backed render storage seams with PostgreSQL job-state and Cloudflare R2 artifact implementations behind `IRenderJobStore`.
- Added runtime configuration and operator notes for split dev-machine and power-machine workflows in `QUICK_REFERENCE.md`.

### Changed

- Wired the builder form to submit real render jobs, validate request inputs, surface queue/render/failure state, and resolve WhatsApp fallback URLs in the preview.
- Expanded the builder status UI to reflect worker progress and available artifacts.
- Split the previous file-backed store into dedicated state and artifact providers while keeping filesystem storage as the default fallback.
- Updated the public API runtime version and aligned release-facing docs with the current implementation.

### Validation

- `npm run build --prefix webapp`
- `dotnet build HalfQR.sln`
- End-to-end queue smoke test is still blocked on this dev machine because RabbitMQ and PostgreSQL are not reachable locally; use the documented tunnel and power-machine commands to run it against live infra.

---

## [0.2.1.0] - 2026-04-09

### Added

- Added a same-origin Next.js rewrite so the webapp can call the render API through `/api/v1/qr/*` without browser-side CORS workarounds.
- Added webapp job polling and artifact download actions for generated SVG and PNG outputs.

### Changed

- Wired the builder form to submit real render jobs, validate request inputs, surface queue/render/failure state, and resolve WhatsApp fallback URLs in the preview.
- Expanded the builder status UI to reflect worker progress and available artifacts.

### Validation

- `npm run build --prefix webapp`

---

## [0.2.0.0] - 2026-04-09

### Added

- Added QRCoder-backed SVG rendering and Docker-safe PNG rasterization in `HalfQR.QrEngine`.
- Added RabbitMQ-backed job dispatch in `HalfQR.PublicApi` and worker-side queue consumption in `HalfQR.Worker`.
- Added file-backed render job state and artifact storage for generated SVG and PNG outputs.
- Added `QUICK_REFERENCE.md` with build, run, infrastructure, and request examples.

### Changed

- Refined the v1 content model so most QR categories now resolve from `TargetUrl`, with WhatsApp kept as the first special native builder flow.
- Updated `README.md` and `SYSTEM_ARCHITECTURE.md` to match the current implementation and quick-start commands.

### Validation

- `dotnet build HalfQR.sln`

---

## [0.1.1.0] - 2026-04-09

### Changes

- Created the solution and backend project structure in `HalfQR.sln` and `HalfQR.slnx`, with service projects under microservices.
- Added the first shared QR contracts in `HalfQR.Contracts`, including content types, finder shapes, render modes, output options, and async render job request and response models.
- Added real payload encoding logic in `QrPayloadEncoder.cs` and hashing in `Sha256HashService.cs`. This now canonicalizes link, text, email, call, SMS, WhatsApp, Wi-Fi, vCard, event, app, social, PDF, image, and video payloads.
- Replaced the template API with an initial async in-memory render flow in `Program.cs` and `InMemoryRenderJobService.cs`. The API now exposes health, content-type discovery, render job submission, and job status endpoints.
- Added infrastructure scaffolding in `docker-compose.yml` and `.env.example` for PostgreSQL, Redis, and RabbitMQ.
- Added tracked bootstrap placeholders for `README.md`.

## Validation:

- The full solution builds successfully through both `HalfQR.sln` and `HalfQR.slnx`.
- Target is .NET 10

---

## [0.1.0.0] - 2026-04-09

### Changes

- README.md: Added project bootstrap details and other introductory details. --- ADDED ---
- SYSTEM_ARCHITECTURE.md: Added system architecture details. --- ADDED ---