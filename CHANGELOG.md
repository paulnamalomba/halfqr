# HaveQR

All notable changes to the **HaveQR** project will be documented in this file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Contents

- [HaveQR](#haveqr)
  - [Contents](#contents)
  - [\[0.2.3.1\] - 2026-04-10](#0231---2026-04-10)
    - [Added](#added)
  - [\[0.2.3.0\] - 2026-04-10](#0230---2026-04-10)
    - [Added](#added-1)
    - [Changed](#changed)
    - [Validation](#validation)
  - [\[0.2.2.2\] - 2026-04-10](#0222---2026-04-10)
    - [Added](#added-2)
  - [\[0.2.2.1\] - 2026-04-10](#0221---2026-04-10)
    - [Added](#added-3)
    - [Changed](#changed-1)
    - [Validation](#validation-1)
  - [\[0.2.2.0\] - 2026-04-09](#0220---2026-04-09)
    - [Added](#added-4)
    - [Changed](#changed-2)
    - [Validation](#validation-2)
  - [\[0.2.1.0\] - 2026-04-09](#0210---2026-04-09)
    - [Added](#added-5)
    - [Changed](#changed-3)
    - [Validation](#validation-3)
  - [\[0.2.0.0\] - 2026-04-09](#0200---2026-04-09)
    - [Added](#added-6)
    - [Changed](#changed-4)
    - [Validation](#validation-4)
  - [\[0.1.1.0\] - 2026-04-09](#0110---2026-04-09)
    - [Changes](#changes)
  - [Validation:](#validation-5)
  - [\[0.1.0.0\] - 2026-04-09](#0100---2026-04-09)
    - [Changes](#changes-1)

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

- `dotnet build HaveQR.sln`
- `npm run build` from `webapp`
- Verified `POST /api/v1/qr/render/draft` returns `200` for a styled link render request against the local PublicApi
- Verified the builder transitions from local SVG preview to server draft preview
- Verified selecting a logo preset loads the image and resets logo size to `12%`

---

## [0.2.2.2] - 2026-04-10

### Added

- Implemented the styled QR slice end to end. The backend now accepts data styling and richer logo inputs, renders square or dotted data modules with linear gradients limited to the main QR body, and supports centered SVG, PNG, and JPEG logos with backdrop handling and raster background removal. 
- The webapp now uses the HaveQR favicon, ships a real landing page plus rebuilt builder UI, exposes the new styling controls, and swaps to worker artifacts instead of a fake preview once a job completes.
- Added a same-origin proxy in the webapp so local use no longer fails on browser CORS when talking to the hosted API.
- Completely implemeneted the `webapp` and it's components and pages, written in TypeScript with React and Next.js, and styled with Material UI and Emotion, copying over some boilerplate and patterns from afriflex.
  - The webapp as externally tunneled to `haveqr.computemore.com` is now the primary demo and test interface for the project, replacing the previous Postman collection and direct API calls.
- Completely overhauled the api as well

---

## [0.2.2.1] - 2026-04-10

### Added

- Added first-party Docker build assets for `webapp`, `HaveQR.PublicApi`, and `HaveQR.Worker`, plus a root `.dockerignore` for the monorepo bench.
- Added Compose-managed `webapp`, `public-api`, and `worker` services with host ports `5173` and `8083` and a shared render-job volume for the file-backed async pipeline.

### Changed

- Switched the webapp from same-origin rewrite calls to direct public API calls using `NEXT_PUBLIC_HAVEQR_API_BASE_URL`, while normalizing relative job and artifact URLs against the public API origin.
- Added configurable CORS and JSON string-enum serialization in `HaveQR.PublicApi`, and aligned persisted render-job JSON with the string-based request and response contract used by the webapp.
- Wired canonical R2 artifact settings through Docker Compose so the API and worker can switch from filesystem artifacts to Cloudflare R2 through environment configuration.
- Disabled AWS streaming payload signing and default checksum validation on R2 uploads so Cloudflare R2 accepts the .NET S3 `PutObject` flow.
- Updated operator docs and release-facing metadata for the current Dockerized devops/testing bench at `haveqr.computemore.com` and `haveqr-api-demo.computemore.com`.

### Validation

- `npm run build` from `webapp`
- `docker compose config`
- `docker compose build webapp public-api worker`
- `docker compose up -d`
- Verified `http://127.0.0.1:8083/healthz` and `http://127.0.0.1:5173`
- Verified a live render job queue, completion, and PNG artifact download through the Docker bench
- Verified the Cloudflare R2 upload-format fix; the currently provided bucket credentials still return `Access Denied` on write, so the local `.env` keeps `RENDER_ARTIFACT_PROVIDER=FileSystem` until bucket permissions are corrected
- `dotnet build HaveQR.sln` is still blocked on this machine because the installed SDK is `9.0.312` while the solution targets `.NET 10.0`

---

## [0.2.2.0] - 2026-04-09

### Added

- Added a same-origin Next.js rewrite so the webapp can call the render API through `/api/v1/qr/*` without browser-side CORS workarounds.
- Added webapp job polling and artifact download actions for generated SVG and PNG outputs.
- Added a finder-pattern SVG compositor in `HaveQR.QrEngine` so `square`, `rounded`, and `circle` now affect the three finder markers without changing normal data modules.
- Added provider-backed render storage seams with PostgreSQL job-state and Cloudflare R2 artifact implementations behind `IRenderJobStore`.
- Added runtime configuration and operator notes for split dev-machine and power-machine workflows in `QUICK_REFERENCE.md`.

### Changed

- Wired the builder form to submit real render jobs, validate request inputs, surface queue/render/failure state, and resolve WhatsApp fallback URLs in the preview.
- Expanded the builder status UI to reflect worker progress and available artifacts.
- Split the previous file-backed store into dedicated state and artifact providers while keeping filesystem storage as the default fallback.
- Updated the public API runtime version and aligned release-facing docs with the current implementation.

### Validation

- `npm run build --prefix webapp`
- `dotnet build HaveQR.sln`
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

- Added QRCoder-backed SVG rendering and Docker-safe PNG rasterization in `HaveQR.QrEngine`.
- Added RabbitMQ-backed job dispatch in `HaveQR.PublicApi` and worker-side queue consumption in `HaveQR.Worker`.
- Added file-backed render job state and artifact storage for generated SVG and PNG outputs.
- Added `QUICK_REFERENCE.md` with build, run, infrastructure, and request examples.

### Changed

- Refined the v1 content model so most QR categories now resolve from `TargetUrl`, with WhatsApp kept as the first special native builder flow.
- Updated `README.md` and `SYSTEM_ARCHITECTURE.md` to match the current implementation and quick-start commands.

### Validation

- `dotnet build HaveQR.sln`

---

## [0.1.1.0] - 2026-04-09

### Changes

- Created the solution and backend project structure in `HaveQR.sln` and `HaveQR.slnx`, with service projects under microservices.
- Added the first shared QR contracts in `HaveQR.Contracts`, including content types, finder shapes, render modes, output options, and async render job request and response models.
- Added real payload encoding logic in `QrPayloadEncoder.cs` and hashing in `Sha256HashService.cs`. This now canonicalizes link, text, email, call, SMS, WhatsApp, Wi-Fi, vCard, event, app, social, PDF, image, and video payloads.
- Replaced the template API with an initial async in-memory render flow in `Program.cs` and `InMemoryRenderJobService.cs`. The API now exposes health, content-type discovery, render job submission, and job status endpoints.
- Added infrastructure scaffolding in `docker-compose.yml` and `.env.example` for PostgreSQL, Redis, and RabbitMQ.
- Added tracked bootstrap placeholders for `README.md`.

## Validation:

- The full solution builds successfully through both `HaveQR.sln` and `HaveQR.slnx`.
- Target is .NET 10

---

## [0.1.0.0] - 2026-04-09

### Changes

- README.md: Added project bootstrap details and other introductory details. --- ADDED ---
- SYSTEM_ARCHITECTURE.md: Added system architecture details. --- ADDED ---