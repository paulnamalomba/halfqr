# HalfQR

[![Version](https://img.shields.io/badge/version-0.3.0.0-blue)](https://github.com/paulnamalomba/halfqr/releases/tag/0.3.0.0)
[![Backend](https://img.shields.io/badge/backend-.NET%2010-512BD4)](#technology-stack)
[![Frontend](https://img.shields.io/badge/frontend-Next.js-black)](#technology-stack)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

> Anonymous-first QR generation platform for branded QR codes with SVG logo embedding, finder marker customization, high-resolution PNG export, async processing, and a future dashboard/subscription model.

## Contents

- [HalfQR](#halfqr)
  - [Contents](#contents)
  - [Overview](#overview)
  - [Product Direction](#product-direction)
    - [Core generator features](#core-generator-features)
    - [Platform features](#platform-features)
  - [Quick Start](#quick-start)
  - [Architecture at a Glance](#architecture-at-a-glance)
    - [Why SVG-first matters](#why-svg-first-matters)
    - [Rendering model](#rendering-model)
  - [Technology Stack](#technology-stack)
  - [Monorepo Layout](#monorepo-layout)
    - [Service intent](#service-intent)
  - [Rendering and Storage Strategy](#rendering-and-storage-strategy)
    - [QR rendering rules](#qr-rendering-rules)
    - [Finder marker shapes](#finder-marker-shapes)
    - [Persistence rules](#persistence-rules)
    - [Hashing policy](#hashing-policy)
  - [Inspiration](#inspiration)
  - [Delivery Phases](#delivery-phases)
    - [Phase 1](#phase-1)
    - [Phase 2](#phase-2)
    - [Phase 3](#phase-3)
  - [Quick Navigation](#quick-navigation)
  - [Current Status](#current-status)

## Overview

HalfQR started from the idea of a simple SVG-to-QR generator, but the product direction is broader: a monorepo-based platform for generating branded QR codes across link-backed categories such as links, apps, social destinations, PDFs, images, videos, and landing pages, with WhatsApp kept as the first special-case native flow. The current backend now generates canonical SVG, rasterizes PNG, queues work through RabbitMQ, and exposes artifact downloads through the public API.

The core user flow is still simple:

1. Choose a QR content type such as link, app, social, PDF, image, video, or WhatsApp.
2. Provide a `targetUrl` for most types, or provide WhatsApp phone and message fields when using the special WhatsApp flow.
3. Upload an SVG logo with a white or transparent background.
4. Choose QR size, error correction, logo size, and finder marker style.
5. Queue the render and download the generated SVG or PNG when the job completes.

The platform direction extends that with a public webapp, admin tooling, REST APIs, a CLI, dynamic redirects, analytics, subscriptions, and future account-based dashboards.

## Product Direction

### Core generator features

- SVG logo in the center of the QR code
- semantic QR categories backed by normalized target URLs for most types
- WhatsApp support through either an explicit URL or a generated `wa.me` link from phone and message fields
- finder marker customization for border and center shapes
- configurable size, ECC level, and logo scale
- batch generation from structured input
- high-resolution PNG output for digital and print workflows
- canonical SVG output plus artifact download endpoints
- async processing via RabbitMQ-backed jobs
- CLI support for local automation and CI pipelines
- roadmap path to additional 2D barcode formats beyond QR once a second encoding engine is introduced

### Platform features

- public anonymous QR creation without sign-in
- REST API for the webapp and automation
- signup, signin, refresh tokens, and Google OAuth2 in the backend
- future customer dashboard for saved QR codes and analytics
- admin dashboard for moderation, support, and growth metrics
- subscription and billing capabilities for paid plans
- dynamic redirect support for managed QR codes

## Quick Start

Current scaffold prerequisites:

- .NET SDK `10.0.x`
- Node.js `20.x` or newer
- Docker if you want the local devops bench

Core commands:

```bash
dotnet build HalfQR.sln
npm install --prefix webapp
npm run build --prefix webapp
docker compose build webapp public-api worker
docker compose up -d webapp public-api worker postgres redis rabbitmq
docker compose logs -f webapp public-api worker rabbitmq
```

Current bench routing:

- `http://127.0.0.1:5173` -> webapp -> `https://www.halfqr.com`
- `http://127.0.0.1:8083` -> public API -> `https://api.halfqr.com`
- The browser-facing webapp bundle is built to call the public API hostname directly through `NEXT_PUBLIC_HALFQR_API_BASE_URL`.

See [QUICK_REFERENCE.md](QUICK_REFERENCE.md) for request examples and configuration keys.

## Architecture at a Glance

- Frontend: Next.js + React + TypeScript for the public webapp and admin surfaces.
- Backend: ASP.NET Core microservices organized under `microservices`.
- QR engine: QRCoder, using an SVG-first render path instead of bitmap-first rendering.
- Content model: normalized `targetUrl` for most categories, with WhatsApp as the first special-case native builder.
- Output pipeline: generate canonical SVG, then rasterize to high-quality PNG.
- Finder styles: custom composition layer for `square`, `rounded`, and `circle` finder markers.
- Data layer: PostgreSQL for metadata, Redis for rate limiting and short-lived cache, Cloudflare R2 for assets.
- Messaging: RabbitMQ for async jobs and batch workflow fanout.
- Deployment: Docker Compose behind public tunnels at `www.halfqr.com` and `api.halfqr.com`.

### Why SVG-first matters

- SVG logo support is a primary requirement.
- Linux containers are the deployment target.
- Finder marker customization is easier at the SVG composition layer.
- Print-quality PNGs are better derived from a canonical SVG render.

### Rendering model

- The editor experience should feel fast in the browser.
- The authoritative QR generation should happen server-side.
- Anonymous one-off renders can remain transient.
- Saved or managed dynamic QR codes can persist assets and metadata.

## Technology Stack

| Layer | Technology | Notes |
| --- | --- | --- |
| Frontend | Next.js, React, TypeScript | Public marketing site, QR builder, future customer dashboard |
| Admin | Next.js, React, TypeScript | Internal operations and support console |
| Backend | ASP.NET Core on .NET 10 | Public API, identity, redirector, billing, workers |
| QR Engine | QRCoder `SvgQRCode` | QR matrix generation plus SVG logo embedding |
| Image Pipeline | SVG composition + PNG rasterization | High-quality output suitable for export |
| Database | PostgreSQL | Auth, QR metadata, analytics, billing |
| Cache | Redis | Rate limiting, short-lived preview and token state |
| Object Storage | Cloudflare R2 | Generated PNG and optional canonical SVG assets |
| Messaging | RabbitMQ | Batch jobs, async processing, domain events |
| Deployment | Docker Compose | Current devops bench at `www.halfqr.com` and `api.halfqr.com` |

## Monorepo Layout

The target repository structure is:

```text
halfqr/
├── webapp/
├── admin/
├── docs/
├── microservices/
│   ├── HalfQR.PublicApi/
│   ├── HalfQR.QrEngine/
│   ├── HalfQR.Identity/
│   ├── HalfQR.Redirector/
│   ├── HalfQR.Billing/
│   ├── HalfQR.Worker/
│   ├── HalfQR.Contracts/
│   └── HalfQR.Cli/
├── inspiration/
├── docker-compose.yml
├── HalfQR.sln
└── SYSTEM_ARCHITECTURE.md
```

### Service intent

- `HalfQR.PublicApi`: public REST surface for the webapp, admin, and CLI
- `HalfQR.QrEngine`: QR generation, SVG sanitization, finder customization, PNG export
- `HalfQR.Identity`: auth, refresh tokens, OAuth2, roles
- `HalfQR.Redirector`: dynamic route resolution and scan tracking
- `HalfQR.Billing`: plans, subscriptions, checkout, entitlement logic
- `HalfQR.Worker`: batch processing, storage uploads, rollups, async jobs
- `HalfQR.Cli`: command-line entry point for rendering and batch automation

## Rendering and Storage Strategy

### QR rendering rules

- Use QRCoder as the QR matrix engine.
- Treat the generated SVG as the canonical render artifact.
- Rasterize PNG from the final composed SVG.
- Keep regular data modules square for scan reliability.
- Apply visual customization primarily to the three finder markers.
- Default logo-bearing renders to ECC `H`.

### Finder marker shapes

Supported v1 marker options:

- `square`
- `rounded`
- `circle`

These are applied separately to:

- finder border shape
- finder center shape

### Persistence rules

- Anonymous transient direct-payload QR codes do not need permanent payload storage.
- Saved static QR codes can store assets and metadata when the user explicitly chooses to save them.
- Dynamic or asset-backed QR codes must store an encrypted destination or hosted asset reference for redirects and delivery.

### Hashing policy

Hashes are useful for:

- deduplication
- cache keys
- aggregate counters
- privacy-friendly indexing without exposing raw payload text

Hashes are not enough for:

- redirect resolution
- editable managed QR codes
- recoverable saved payloads by themselves

The design therefore uses hashes for indexing and analytics, while storing encrypted destinations or recoverable payloads when dynamic behavior or editing requires them.

## Inspiration

- `qr.io`: reference for product positioning, information architecture, and conversion-oriented QR builder flow
- `AfriFlex`: reference for visual direction, typography, spacing, color hierarchy, rounded surfaces, and motion tone

The goal is to take strategic inspiration, not to reproduce markup, assets, or copy.

## Delivery Phases

### Phase 1

- public landing page
- QR builder
- server-side SVG-to-PNG generation
- finder marker customization
- anonymous one-off downloads
- CLI render command

### Phase 2

- saved QR records
- dynamic redirect routes
- scan tracking
- batch processing via RabbitMQ
- admin visibility into jobs and metrics

### Phase 3

- signup and signin
- Google OAuth2
- customer dashboard
- subscriptions and plan entitlements
- paid analytics and storage retention features

## Quick Navigation

| Document | Description |
| --- | --- |
| [README.md](README.md) | Repository overview and product direction |
| [SYSTEM_ARCHITECTURE.md](SYSTEM_ARCHITECTURE.md) | Detailed architecture decisions, service boundaries, data model, and deployment strategy |

## Current Status

This repository currently contains:

- a buildable `.NET` solution under `HalfQR.sln`
- QR contracts, hashing, URL-backed payload normalization, finder-pattern SVG composition, and QRCoder-based SVG/PNG rendering
- provider-backed render storage with filesystem defaults plus PostgreSQL and Cloudflare R2 implementations behind the same runtime facade
- a RabbitMQ-backed job dispatch path between `HalfQR.PublicApi` and `HalfQR.Worker`
- root quick-start documentation and a real `webapp` builder that can submit jobs, poll status, and download artifacts

The next implementation step is to replace placeholder dynamic redirect persistence with PostgreSQL and Redis, then widen the content model beyond the first URL-backed and WhatsApp flows.
