# HaveQR

All notable changes to the **HaveQR** project will be documented in this file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Contents

- [HaveQR](#haveqr)
  - [Contents](#contents)
  - [\[0.2.1.0\] - 2026-04-09](#0210---2026-04-09)
    - [Added](#added)
    - [Changed](#changed)
    - [Validation](#validation)
  - [\[0.2.0.0\] - 2026-04-09](#0200---2026-04-09)
    - [Added](#added-1)
    - [Changed](#changed-1)
    - [Validation](#validation-1)
  - [\[0.1.1.0\] - 2026-04-09](#0110---2026-04-09)
    - [Changes](#changes)
  - [Validation:](#validation-2)
  - [\[0.1.0.0\] - 2026-04-09](#0100---2026-04-09)
    - [Changes](#changes-1)

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