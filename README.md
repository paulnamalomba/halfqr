# HaveQR

[![Version](https://img.shields.io/badge/version-0.1.0.0-blue)](https://github.com/paulnamalomba/haveqr/releases/tag/0.1.0.0)
[![Backend](https://img.shields.io/badge/backend-.NET%208-512BD4)](#technology-stack)
[![Frontend](https://img.shields.io/badge/frontend-Next.js-black)](#technology-stack)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

> Anonymous-first QR generation platform for branded QR codes with SVG logo embedding, finder marker customization, high-resolution PNG export, batch processing, and a future dashboard/subscription model.

## Contents

- [HaveQR](#haveqr)
  - [Contents](#contents)
  - [Overview](#overview)
  - [Product Direction](#product-direction)
    - [Core generator features](#core-generator-features)
    - [Platform features](#platform-features)
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

HaveQR started from the idea of a simple SVG-to-QR generator, but the planned product is broader: a monorepo-based platform for generating branded QR codes for links, text, contact data, communications actions, Wi-Fi access, events, app/social destinations, and media-backed content, exporting print-quality assets, and eventually managing saved and trackable QR campaigns through a web dashboard.

The core user flow is still simple:

1. Choose a QR content type such as link, text, email, call, SMS, WhatsApp, vCard, Wi-Fi, event, app, social, PDF, image, or video.
2. Provide the payload, destination, or hosted asset reference for that content type.
3. Upload an SVG logo with a white or transparent background.
4. Choose QR size, error correction, logo size, and finder marker style.
5. Generate a high-quality PNG.

The platform direction extends that with a public webapp, admin tooling, REST APIs, a CLI, dynamic redirects, analytics, subscriptions, and future account-based dashboards.

## Product Direction

### Core generator features

- SVG logo in the center of the QR code
- multi-content QR creation for link, text, email, call, SMS, WhatsApp, vCard, Wi-Fi, event, app, social, PDF, image, and video flows
- finder marker customization for border and center shapes
- configurable size, ECC level, and logo scale
- batch generation from structured input
- high-resolution PNG output for digital and print workflows
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

## Architecture at a Glance

- Frontend: Next.js + React + TypeScript for the public webapp and admin surfaces.
- Backend: ASP.NET Core microservices organized under `microservices`.
- QR engine: QRCoder, using an SVG-first render path instead of bitmap-first rendering.
- Content model: typed payload builders for direct QR data plus managed destination wrappers for app, social, PDF, image, and video experiences.
- Output pipeline: generate canonical SVG, then rasterize to high-quality PNG.
- Finder styles: custom composition layer for `square`, `rounded`, and `circle` finder markers.
- Data layer: PostgreSQL for metadata, Redis for rate limiting and short-lived cache, Cloudflare R2 for assets.
- Messaging: RabbitMQ for async jobs and batch workflow fanout.
- Deployment: Docker Compose behind a public tunnel or reverse proxy at `haveqr.computemore.com`.

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
| Backend | ASP.NET Core on .NET 8 | Public API, identity, redirector, billing, workers |
| QR Engine | QRCoder `SvgQRCode` | QR matrix generation plus SVG logo embedding |
| Image Pipeline | SVG composition + PNG rasterization | High-quality output suitable for export |
| Database | PostgreSQL | Auth, QR metadata, analytics, billing |
| Cache | Redis | Rate limiting, short-lived preview and token state |
| Object Storage | Cloudflare R2 | Generated PNG and optional canonical SVG assets |
| Messaging | RabbitMQ | Batch jobs, async processing, domain events |
| Deployment | Docker Compose | Initial hosted environment at `haveqr.computemore.com` |

## Monorepo Layout

The target repository structure is:

```text
haveqr/
├── webapp/
├── admin/
├── docs/
├── microservices/
│   ├── HaveQR.PublicApi/
│   ├── HaveQR.QrEngine/
│   ├── HaveQR.Identity/
│   ├── HaveQR.Redirector/
│   ├── HaveQR.Billing/
│   ├── HaveQR.Worker/
│   ├── HaveQR.Contracts/
│   └── HaveQR.Cli/
├── inspiration/
├── docker-compose.yml
├── HaveQR.sln
└── SYSTEM_ARCHITECTURE.md
```

### Service intent

- `HaveQR.PublicApi`: public REST surface for the webapp, admin, and CLI
- `HaveQR.QrEngine`: QR generation, SVG sanitization, finder customization, PNG export
- `HaveQR.Identity`: auth, refresh tokens, OAuth2, roles
- `HaveQR.Redirector`: dynamic route resolution and scan tracking
- `HaveQR.Billing`: plans, subscriptions, checkout, entitlement logic
- `HaveQR.Worker`: batch processing, storage uploads, rollups, async jobs
- `HaveQR.Cli`: command-line entry point for rendering and batch automation

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

- the initial README
- the architecture definition in `SYSTEM_ARCHITECTURE.md`
- saved inspiration sources under `inspiration/`

The next implementation step is to scaffold the monorepo and begin with the `.NET` solution, public API, QR engine service, and Next.js web surfaces.
