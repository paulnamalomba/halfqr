# HalfQR System Imbroglio

> **A truthful, code-verified record of how the current HalfQR backend is actually wired, which parts are real runtime services versus naming boundaries, what data each layer moves, and where the implemented system still diverges from the plan in `SYSTEM_ARCHITECTURE.md`.**

**Version**: 0.3.0.0
**Verified against**: `microservices/`, `webapp/src/components/qr-builder.tsx`, `docker-compose.yml` (April 2026)  
**Primary implemented entry points**: `microservices/HalfQR.PublicApi/Program.cs`, `microservices/HalfQR.Worker/Program.cs`  
**Planning baseline**: `SYSTEM_ARCHITECTURE.md`

## Contents

- [HalfQR System Imbroglio](#halfqr-system-imbroglio)
  - [Contents](#contents)
  - [1. Why This File Exists](#1-why-this-file-exists)
  - [2. Reality Snapshot](#2-reality-snapshot)
  - [3. Process Model](#3-process-model)
  - [4. Technology Inventory](#4-technology-inventory)
  - [5. Data Transport Map](#5-data-transport-map)
  - [6. Implemented Service Boundaries](#6-implemented-service-boundaries)
  - [7. Public API Surface](#7-public-api-surface)
    - [Route map](#route-map)
    - [Middleware and serialization reality](#middleware-and-serialization-reality)
    - [CORS reality](#cors-reality)
    - [Important route contract detail](#important-route-contract-detail)
  - [8. Render Job Lifecycle](#8-render-job-lifecycle)
    - [8.1 Submission and Initial Persistence](#81-submission-and-initial-persistence)
    - [8.2 RabbitMQ Dispatch](#82-rabbitmq-dispatch)
    - [8.3 Worker Consumption Model](#83-worker-consumption-model)
    - [8.4 Rendering and Artifact Production](#84-rendering-and-artifact-production)
    - [8.5 Status Retrieval and Browser Polling](#85-status-retrieval-and-browser-polling)
  - [9. QR Engine Internals](#9-qr-engine-internals)
    - [9.1 Request Model Reality](#91-request-model-reality)
    - [9.2 Payload Encoding](#92-payload-encoding)
    - [9.3 Canonicalization and Hashing](#93-canonicalization-and-hashing)
    - [9.4 SVG Generation](#94-svg-generation)
    - [9.5 Finder Pattern Composition](#95-finder-pattern-composition)
    - [9.6 PNG Rasterization](#96-png-rasterization)
  - [10. Storage Subsystem](#10-storage-subsystem)
    - [10.1 Store Composition](#101-store-composition)
    - [10.2 File System Layout](#102-file-system-layout)
    - [10.3 PostgreSQL Job State Store](#103-postgresql-job-state-store)
    - [10.4 Cloudflare R2 Artifact Store](#104-cloudflare-r2-artifact-store)
    - [10.5 Serialization Format](#105-serialization-format)
  - [11. Docker Bench and Runtime Wiring](#11-docker-bench-and-runtime-wiring)
  - [12. Planned vs Implemented Delta](#12-planned-vs-implemented-delta)
  - [13. File and Method Reference Index](#13-file-and-method-reference-index)
    - [Core Runtime Entry Points](#core-runtime-entry-points)
    - [Render Flow Services](#render-flow-services)
    - [QR Engine](#qr-engine)
    - [Storage](#storage)
    - [Contracts](#contracts)
    - [Frontend Integration](#frontend-integration)
    - [Placeholder Services](#placeholder-services)
  - [14. Complexity and Risk Notes](#14-complexity-and-risk-notes)

---

## 1. Why This File Exists

`SYSTEM_ARCHITECTURE.md` is the direction document. This file is the reality document.

The architecture file describes the intended platform shape: anonymous-first QR creation, managed QR codes, dashboards, auth, billing, redirector services, Redis, PostgreSQL, R2, and a broader service topology. The current repository does not implement all of that yet.

This document focuses on what the code currently does:

- which processes actually run
- which libraries are in-process dependencies rather than remote services
- how a render request flows from browser to queue to worker to artifact download
- where storage is durable versus optional
- which enum values and options exist in the contract but are not yet backed by distinct behavior

If there is ever a disagreement between the planned architecture and the code, this file should describe the code.

---

## 2. Reality Snapshot

| Area | Current reality | Notes |
| --- | --- | --- |
| Actual long-running .NET runtimes | `HalfQR.PublicApi`, `HalfQR.Worker` | These are the only meaningful backend processes in the current implementation. |
| Shared code libraries | `HalfQR.Contracts`, `HalfQR.QrEngine` | These are not remote services. They are linked directly into the runtime processes. |
| Placeholder service names | `HalfQR.Identity`, `HalfQR.Redirector`, `HalfQR.Billing` | They exist as projects, but each currently returns `Hello World!` only. |
| CLI status | `HalfQR.Cli` is a bootstrap console placeholder | Its status message is stale and still mentions "in-memory" render jobs. |
| Active asynchronous backbone | RabbitMQ | The render flow really does queue work through RabbitMQ. |
| Default persistence path | File system | Render job state and artifacts default to `.data/render-jobs` locally or `/var/lib/halfqr/render-jobs` in Docker. |
| Optional state store | PostgreSQL | Implemented in code, but only active if `RenderStorage:JobStateProvider=PostgreSql` and a connection string is supplied. |
| Optional artifact store | Cloudflare R2 via S3 API | Implemented in code, but not the checked-in default path. |
| Redis usage | None in current code path | Redis is present in Docker Compose, but no runtime code uses it yet. |
| Auth / JWT / OAuth | Not implemented in the current backend runtime | No auth middleware, no tokens, no identity pipeline. |
| Managed QR mode | Declared in the contract | No distinct managed-flow behavior exists yet. |

The most important architectural truth is this: **HalfQR is currently a two-process render system, not a full multi-service platform yet.**

---

## 3. Process Model

The current execution model is split across two real backend processes plus shared libraries:

```text
Browser / CLI / Future clients
        |
        | HTTP JSON
        v
HalfQR.PublicApi  --------------------------->  state store (FileSystem or PostgreSQL)
        |
        | AMQP message containing only JobId
        v
RabbitMQ queue: halfqr.render.jobs
        |
        v
HalfQR.Worker  ------------------------------>  artifact store (FileSystem or R2)
        |
        | in-process calls into HalfQR.QrEngine
        v
QR generation, SVG composition, PNG rasterization
```

Important consequences:

1. `HalfQR.PublicApi` is not doing the rendering itself.
2. `HalfQR.Worker` is the authoritative rendering process.
3. `HalfQR.QrEngine` is not a deployed service boundary; it is a library referenced directly by both runtimes.
4. The queue message is intentionally tiny. The source of truth is the persisted render job state, not the RabbitMQ payload.
5. The platform names under `microservices/` are ahead of the actual network topology.

---

## 4. Technology Inventory

| Technology | Version / Package | Wire protocol or runtime role | What it actually does now |
| --- | --- | --- | --- |
| ASP.NET Core Minimal API | .NET 10 | HTTP JSON | Powers `HalfQR.PublicApi` routes. |
| .NET Worker Host | .NET 10 | background host | Powers `HalfQR.Worker`. |
| RabbitMQ.Client | 7.2.1 | AMQP 0-9-1 | Publishes and consumes render-job messages. |
| RabbitMQ server | 3.13 management image | TCP 5672 / HTTP 15672 | Active queue backbone in Docker bench. |
| QRCoder | 1.8.0 | in-process library | Generates the QR SVG base markup. |
| Svg.Skia | 4.0.0 | in-process library | Loads SVG into a rasterizable picture. |
| SkiaSharp native Linux assets | 2.88.9 | in-process native dependency | Produces final PNG bytes. |
| Npgsql | 10.0.2 | PostgreSQL protocol | Optional job state persistence store. |
| AWSSDK.S3 | 4.0.21 | S3-compatible HTTP | Optional Cloudflare R2 artifact storage. |
| Docker Compose | repo root | container orchestration | Runs webapp, API, worker, and staged infra. |
| PostgreSQL container | `postgres:16-alpine` | TCP 5432 in container | Present in bench, but only used if job-state provider is switched to PostgreSQL. |
| Redis container | `redis:7-alpine` | TCP 6379 in container | Present in bench, but not consumed by current code. |
| Next.js webapp | webapp package | browser UI | Queues render jobs, polls status, downloads artifacts. |

Notably absent from the current runtime:

- no ASP.NET Identity registration
- no JWT bearer setup
- no OAuth provider integration
- no Redis client registration
- no billing SDK or payment flow
- no redirect tracking service

---

## 5. Data Transport Map

```text
┌────────────────────────────────────────────────────────────────────────────┐
│                              USER BROWSER                                 │
│  Next.js QR builder                                                       │
└───────────────────────────────┬────────────────────────────────────────────┘
                                │
                                │ HTTPS / HTTP JSON
                                ▼
┌────────────────────────────────────────────────────────────────────────────┐
│                         HALFQR.PUBLICAPI                                  │
│                                                                            │
│  POST /api/v1/qr/render                                                    │
│  GET  /api/v1/qr/jobs/{jobId}                                              │
│  GET  /api/v1/qr/jobs/{jobId}/artifacts/{format}                           │
│                                                                            │
│  - persists RenderJobState                                                 │
│  - publishes RenderJobQueuedMessage                                        │
│  - exposes status and artifact download                                    │
└───────────────┬───────────────────────────────┬────────────────────────────┘
                │                               │
                │ FileSystem or PostgreSQL      │ AMQP message with JobId only
                ▼                               ▼
        Render job state store             RabbitMQ queue
                                                │
                                                ▼
┌────────────────────────────────────────────────────────────────────────────┐
│                           HALFQR.WORKER                                   │
│                                                                            │
│  - consumes queue                                                          │
│  - reloads persisted request                                               │
│  - runs QrRenderService                                                    │
│  - saves SVG and PNG artifacts                                             │
│  - updates status to Completed or Failed                                   │
└───────────────┬────────────────────────────────────────────────────────────┘
                │
                │ FileSystem or R2
                ▼
        Render artifact store
```

What does **not** currently move data in the render path:

- Redis
- Identity service
- Redirector service
- Billing service
- any event bus other than RabbitMQ

---

## 6. Implemented Service Boundaries

| Project | Actual role today | Boundary type |
| --- | --- | --- |
| `microservices/HalfQR.PublicApi` | Public HTTP surface for queueing renders, checking status, and downloading artifacts | real runtime process |
| `microservices/HalfQR.Worker` | Background queue consumer and renderer | real runtime process |
| `microservices/HalfQR.QrEngine` | Payload encoding, hashing, SVG composition, PNG rasterization, storage implementations | in-process shared library |
| `microservices/HalfQR.Contracts` | Request/response models, enums, options, queued message contracts | in-process shared library |
| `microservices/HalfQR.Cli` | Placeholder bootstrap console | placeholder project |
| `microservices/HalfQR.Identity` | `Hello World!` minimal API shell | placeholder service boundary |
| `microservices/HalfQR.Redirector` | `Hello World!` minimal API shell | placeholder service boundary |
| `microservices/HalfQR.Billing` | `Hello World!` minimal API shell | placeholder service boundary |

This means the repository is **service-oriented in naming**, but only partially **microservice-oriented in runtime reality**.

---

## 7. Public API Surface

The current API is a minimal API defined entirely in `microservices/HalfQR.PublicApi/Program.cs`.

### Route map

| Route | Method | Backing behavior | Notes |
| --- | --- | --- | --- |
| `/` | GET | static service info | Returns service name, status, version. |
| `/healthz` | GET | manual health response | Not a full health-check subsystem. |
| `/api/v1/qr/content-types` | GET | `Enum.GetNames<QrContentType>()` | Exposes every enum value, including ones not yet uniquely encoded. |
| `/api/v1/qr/render` | POST | `RenderJobService.EnqueueAsync` | Returns `202 Accepted` with job ID and relative status URL. |
| `/api/v1/qr/jobs/{jobId}` | GET | `RenderJobService.GetStatusAsync` | Returns persisted status and artifact descriptors. |
| `/api/v1/qr/jobs/{jobId}/artifacts/{format}` | GET | `RenderJobService.GetArtifactAsync` | Streams artifact bytes back through the API. |

### Middleware and serialization reality

Current middleware is intentionally small:

- CORS
- JSON enum string conversion
- route handlers

There is **no** current implementation of:

- authentication middleware
- authorization middleware
- rate limiting
- request validation pipeline
- Swagger or OpenAPI registration
- centralized exception handling middleware

### CORS reality

The API allows configured browser origins from `Cors:AllowedOrigins`, with defaults centered around:

- `https://www.halfqr.com`
- `http://localhost:5173`
- `http://127.0.0.1:5173`
- local fallback values for port `3000`

That exists because the webapp now calls the public API host directly instead of relying on same-origin rewrites.

### Important route contract detail

`RenderJobAcceptedResponse.StatusUrl` and the `DownloadUrl` values inside `RenderJobStatusResponse` are **relative** API paths, not absolute URLs. The webapp resolves them against `NEXT_PUBLIC_HALFQR_API_BASE_URL` before polling or downloading.

---

## 8. Render Job Lifecycle

The render job flow is the most important implemented subsystem in HalfQR.

### 8.1 Submission and Initial Persistence

The browser posts `SubmitRenderJobRequest` JSON to `/api/v1/qr/render`.

`RenderJobService.EnqueueAsync` performs this sequence:

1. Generate a new `Guid` job ID.
2. Create a `RenderJobState` object containing:
   - the full incoming request
   - `Status = Queued`
   - `CreatedAt`
3. Save the job state to the configured state store.
4. Dispatch a `RenderJobQueuedMessage` to RabbitMQ.
5. If dispatch fails, update the same stored job to `Failed` with a `FailureReason`.
6. Return `RenderJobAcceptedResponse(jobId, Queued, statusUrl)`.

Important architectural point:

- The full request is stored in the job state.
- The queue message contains only `JobId`.

That means **RabbitMQ is a wake-up signal, not the source of truth for request data**.

### 8.2 RabbitMQ Dispatch

`RabbitMqJobDispatcher.DispatchAsync` does the following on each submission:

1. Creates a new RabbitMQ connection.
2. Creates a new channel.
3. Declares the queue `halfqr.render.jobs` as durable.
4. Serializes `RenderJobQueuedMessage` to UTF-8 JSON.
5. Publishes to the default exchange with the queue name as the routing key.
6. Marks the message as persistent.

Deep detail:

- the dispatcher is registered as a singleton, but it still creates a fresh connection and channel per dispatch call
- there is no publisher confirm handling
- there is no custom exchange or routing topology yet

This is operationally simple, but not yet optimized for very high throughput.

### 8.3 Worker Consumption Model

`HalfQR.Worker` uses `BackgroundService` and an `AsyncEventingBasicConsumer`.

The consumption model is:

1. Start host.
2. Open one RabbitMQ connection and channel for the consumer loop.
3. Declare the same durable queue.
4. Register `ReceivedAsync` callback.
5. Consume with `autoAck = false`.
6. On successful processing, `BasicAck` the delivery.
7. On failure, `BasicNack(requeue: false)` the delivery.

Important behavior:

- there is no dead-letter exchange configured
- failed queue messages are not requeued
- the failure signal survives because job state is updated to `Failed` before the exception path completes
- there is no consumer prefetch tuning yet

The retry model today is effectively:

- in-memory retry of the top-level worker loop if the whole consumer crashes
- no per-message retry pipeline once a specific job fails

### 8.4 Rendering and Artifact Production

`Worker.ProcessAsync` is the heart of the render runtime.

Per job it does this:

1. Reload the stored job state by `JobId`.
2. If missing, log warning and skip.
3. Mark the job `Running` and set `StartedAt`.
4. Call `IQrRenderService.RenderAsync`.
5. Save SVG artifact.
6. Save PNG artifact.
7. Update the job to `Completed` with:
   - `EncodedPayload`
   - `ResolvedTargetUrl`
   - `ConfigurationHash`
   - `PayloadHash`
   - artifact descriptors
   - `CompletedAt`
8. If any exception occurs, update the job to `Failed` with `FailureReason = exception.Message`.

Status transitions are therefore:

```text
Queued -> Running -> Completed
Queued -> Running -> Failed
Queued -> Failed   (dispatch failure before worker sees it)
```

### 8.5 Status Retrieval and Browser Polling

`RenderJobService.GetStatusAsync` reads the persisted `RenderJobState` and maps it to `RenderJobStatusResponse`.

The webapp behavior in `webapp/src/components/qr-builder.tsx` is:

1. POST `/api/v1/qr/render`
2. normalize returned `statusUrl` with `resolveApiUrl`
3. poll that URL until the job reaches a terminal state
4. normalize each artifact `downloadUrl`
5. let the browser download the chosen artifact directly from the API route

So the browser never talks to RabbitMQ, PostgreSQL, the file system, or R2 directly. It only sees the public API.

---

## 9. QR Engine Internals

`HalfQR.QrEngine` contains the real rendering logic, and most of the implementation complexity is here.

### 9.1 Request Model Reality

`SubmitRenderJobRequest` exposes these major fields:

- `ContentType`
- `TargetUrl`
- `Payload`
- `Mode`
- `ErrorCorrectionLevel`
- `Output`
- `Logo`
- `Finder`
- `Colors`

What that means in practice today:

- `QrContentType` includes many semantic values: `Link`, `Text`, `Email`, `Call`, `Sms`, `WhatsApp`, `VCard`, `WiFi`, `Event`, `App`, `Social`, `Pdf`, `Image`, `Video`
- **only `WhatsApp` has special payload encoding behavior right now**
- every other content type goes through the same absolute-URL path
- `QrRenderMode.Managed` exists as an enum value, but there is currently no managed-specific branch in the render pipeline
- `QrOutputOptions.Format` exists, but the worker currently always produces both SVG and PNG artifacts regardless of that field

This is one of the clearest examples where the contract surface is ahead of the implemented semantics.

### 9.2 Payload Encoding

`QrPayloadEncoder.Encode` is intentionally simple:

- if `ContentType == WhatsApp`, use `BuildWhatsAppPayload`
- otherwise require a valid absolute `http` or `https` URL

`BuildWhatsAppPayload` supports two modes:

1. if `TargetUrl` is present, trust and normalize it as an absolute URL
2. otherwise build `https://wa.me/<phone>?text=<message>` from payload fields

Important details:

- phone input is normalized down to digits and `+`
- empty or invalid phone values throw
- other semantic content types such as `Email`, `Sms`, `VCard`, and `WiFi` do not yet have dedicated encoders

### 9.3 Canonicalization and Hashing

`QrRenderService.RenderAsync` computes two hashes:

- `ConfigurationHash`: hash of a canonicalized request view
- `PayloadHash`: hash of the final encoded payload string

Canonicalization details in `SerializeCanonicalRequest`:

- payload dictionary keys are sorted ordinally before serialization
- content type and mode are serialized as strings
- output, finder, and color options are included
- the logo section records only:
  - whether SVG exists
  - `SizePercent`

Important reality:

**The raw SVG logo markup is not part of the configuration hash.**

That means two requests with different logo SVG bodies but the same logo presence and `SizePercent` will currently produce the same `ConfigurationHash`.

If the hash is ever used for dedupe or cache identity, that behavior matters.

### 9.4 SVG Generation

`QrRenderService.GenerateSvg`:

1. creates QR matrix data from the encoded payload using QRCoder
2. converts the matrix to SVG with `SvgQRCode.GetGraphic`
3. passes optional `QrLogoOptions.Svg` directly to QRCoder as a logo
4. sends the generated SVG through `QrFinderSvgComposer.Compose`

Important implementation note:

- the architecture plan mentions SVG sanitization as a service responsibility
- the current code does **not** implement a dedicated SVG sanitization stage before logo embedding

Right now the system trusts the submitted logo SVG string.

### 9.5 Finder Pattern Composition

`QrFinderSvgComposer` is the custom visual layer that differentiates HalfQR from a plain QRCoder default output.

Key behavior:

- if both border and center shapes are `Square`, the original SVG is returned unchanged
- otherwise the composer injects an overlay group immediately before `</svg>`
- only the three finder regions are redrawn
- regular data modules remain untouched for scan safety

Supported shapes:

- `Square`
- `Rounded`
- `Circle`

Implementation details worth noting:

- quiet-zone and finder module sizes are hard-coded constants
- rounded corner radius varies by which ring is being drawn
- the compositor paints a light background rectangle first, then border, inner gap, and center

This is a pure SVG post-processing step, not a custom QR matrix generator.

### 9.6 PNG Rasterization

`QrRenderService.RasterizePng` takes the final SVG and turns it into PNG bytes.

Process:

1. load SVG into `SKSvg`
2. read picture bounds
3. create square `SKSurface`
4. scale uniformly to the requested output size
5. center the image on the canvas
6. encode as PNG

Failure behavior:

- if the SVG cannot be loaded, the method throws
- if bounds are invalid, the method throws

Those exceptions bubble back into `Worker.ProcessAsync`, which turns them into failed render jobs.

---

## 10. Storage Subsystem

### 10.1 Store Composition

`IRenderJobStore` is implemented by `CompositeRenderJobStore`, which delegates to two lower-level abstractions:

- `IRenderJobStateStore`
- `IRenderArtifactStore`

That split is important. It means job state and artifact storage can evolve independently.

Examples:

- file system state + file system artifacts
- PostgreSQL state + file system artifacts
- PostgreSQL state + R2 artifacts

### 10.2 File System Layout

The default file-system layout is deterministic:

```text
<root>/<jobIdN>/job.json
<root>/<jobIdN>/artifacts/qr.svg
<root>/<jobIdN>/artifacts/qr.png
```

Where:

- `<root>` is `.data/render-jobs` locally by default
- in Docker it is typically `/var/lib/halfqr/render-jobs`

`FileSystemRenderJobStateStore` writes the entire serialized `RenderJobState` to `job.json`.

`FileSystemRenderArtifactStore` writes artifacts by normalized format name:

- `svg` -> `qr.svg`
- `png` -> `qr.png`
- anything else -> `artifact.<format>`

### 10.3 PostgreSQL Job State Store

`PostgresRenderJobStateStore` is implemented, but optional.

It stores one row per job:

- `job_id uuid primary key`
- `state_json jsonb not null`
- `updated_at timestamptz`

Interesting implementation details:

- schema and table are lazily created by the application itself during first use
- initialization is guarded by a `SemaphoreSlim`
- state is still stored as the full serialized JSON document, not decomposed relational columns

This is effectively a durable JSON document store backed by PostgreSQL.

### 10.4 Cloudflare R2 Artifact Store

`R2RenderArtifactStore` is also implemented, but optional.

Key behavior:

- object key format is `<prefix>/<jobIdN>/artifacts/<fileName>` when a prefix exists
- if the prefix is empty, keys start directly at `<jobIdN>/artifacts/...`
- the API still serves downloads through its own `/artifacts/{format}` route
- there is no presigned-URL or direct-public-download handoff in the current backend flow

Cloudflare-specific interoperability details already exist in the code:

- `ForcePathStyle = true`
- `AuthenticationRegion = "auto"`
- `DisablePayloadSigning = true`
- `DisableDefaultChecksumValidation = true`

So R2 support is not theoretical. It is a real code path, just not the default checked-in artifact path.

### 10.5 Serialization Format

`RenderJobStateJson` configures JSON serialization for persisted job state.

Important detail:

- enums are serialized as strings, not integers

That keeps persisted state aligned with the HTTP contract consumed by the webapp, which also expects string enum values like `Queued`, `Running`, `Completed`, and `Failed`.

---

## 11. Docker Bench and Runtime Wiring

The repository root `docker-compose.yml` currently defines these services:

| Service | Purpose now | Host port |
| --- | --- | --- |
| `webapp` | Next.js frontend | `5173` |
| `public-api` | render submission, status, and artifact API | `8083` |
| `worker` | queue consumer and renderer | none exposed |
| `postgres` | staged state-store backend | `5433` |
| `redis` | staged future cache and rate-limit backend | `6380` |
| `rabbitmq` | active queue backbone | `5673`, `15673` |

Current wiring details:

- `public-api` and `worker` both receive RabbitMQ settings
- `public-api` and `worker` both receive render storage provider settings
- `public-api` and `worker` share the same mounted render-jobs volume
- `webapp` is built with `NEXT_PUBLIC_HALFQR_API_BASE_URL`
- the browser-side app is designed to call the public API hostname directly

What this means operationally:

- RabbitMQ is part of the live happy path
- file-system storage is the default live happy path
- PostgreSQL is staged and usable, but not automatically active
- Redis is staged only and currently unused

---

## 12. Planned vs Implemented Delta

| Area | Planned in `SYSTEM_ARCHITECTURE.md` | Implemented now | Consequence |
| --- | --- | --- | --- |
| Service topology | Public API, identity, redirector, billing, worker, CLI, dashboards | Public API and worker are real; identity, redirector, and billing are shells | The repo shape looks larger than the runtime actually is. |
| Auth | JWT, refresh tokens, Google OAuth2, roles | No auth pipeline in current backend | Public render flow is anonymous-only by reality, not policy plus auth. |
| Redirects | Dynamic QR redirector service | Not implemented | `Managed` mode is not backed by a redirect system. |
| Billing | Plans, entitlements, collections | Not implemented | Subscription logic is still architectural intent only. |
| Redis | rate limiting, cache, token state | no code usage found | Redis in Compose is future-facing, not current functionality. |
| PostgreSQL | broader platform persistence | only optional render job state store | Relational storage exists as an extension point, not a platform-wide data model yet. |
| R2 | object storage for assets | implemented as optional artifact store | Real code path exists, but the API still proxies downloads. |
| Semantic content types | email, sms, vcard, wifi, event, and others | all non-WhatsApp types currently collapse to absolute URL encoding | Contract breadth exceeds payload encoding depth. |
| Managed mode | dynamic saved and tracked QR lifecycle | enum only | No managed behavior branch exists in rendering or API routing. |
| Validation | robust API validation and abuse controls | basic runtime exceptions only | Invalid inputs fail late rather than through a first-class validation layer. |
| SVG sanitization | called for in architecture direction | not implemented | Logo SVG is currently trusted input. |
| CLI | local automation and CI commands | bootstrap console placeholder | No serious CLI workflow exists yet. |

One more subtle divergence:

- the CLI placeholder still prints that the Public API exposes an async **in-memory** render path
- the current render path is no longer in-memory; it uses persisted state plus queue-driven processing

---

## 13. File and Method Reference Index

### Core Runtime Entry Points

| File | Key methods or setup points | What it controls |
| --- | --- | --- |
| `microservices/HalfQR.PublicApi/Program.cs` | route registrations, CORS policy, JSON enum conversion, provider selection | the entire current public backend surface |
| `microservices/HalfQR.Worker/Program.cs` | host setup, provider selection, `AddHostedService<Worker>` | the queue consumer runtime |

### Render Flow Services

| File | Key methods | What they do |
| --- | --- | --- |
| `microservices/HalfQR.PublicApi/Services/RenderJobService.cs` | `EnqueueAsync`, `GetStatusAsync`, `GetArtifactAsync`, `ToResponse` | creates job records, dispatches queue messages, maps stored state to HTTP responses |
| `microservices/HalfQR.PublicApi/Services/RabbitMqJobDispatcher.cs` | `DispatchAsync` | declares the queue and publishes a persistent JSON job message |
| `microservices/HalfQR.Worker/Worker.cs` | `ExecuteAsync`, `ConsumeAsync`, `ProcessAsync` | manages the consumer loop and executes the actual render workflow |

### QR Engine

| File | Key methods | What they do |
| --- | --- | --- |
| `microservices/HalfQR.QrEngine/PayloadEncoding/QrPayloadEncoder.cs` | `Encode`, `BuildWhatsAppPayload`, `BuildUrlPayload`, `NormalizeAbsoluteUrl` | converts request intent into the payload string encoded in the QR code |
| `microservices/HalfQR.QrEngine/Rendering/QrRenderService.cs` | `RenderAsync`, `SerializeCanonicalRequest`, `GenerateSvg`, `RasterizePng` | performs the complete render pipeline and computes hashes |
| `microservices/HalfQR.QrEngine/Rendering/QrFinderSvgComposer.cs` | `Compose`, `BuildOverlay`, `AppendFinder`, `AppendShape` | redraws the three finder patterns with custom shapes |
| `microservices/HalfQR.QrEngine/Hashing/Sha256HashService.cs` | `Compute` | hashes configuration and payload strings |

### Storage

| File | Key methods | What they do |
| --- | --- | --- |
| `microservices/HalfQR.QrEngine/Storage/CompositeRenderJobStore.cs` | `SaveAsync`, `GetAsync`, `SaveArtifactAsync`, `GetArtifactAsync` | composes state and artifact stores into one abstraction |
| `microservices/HalfQR.QrEngine/Storage/FileSystemRenderJobStateStore.cs` | `SaveAsync`, `GetAsync` | writes and reads `job.json` |
| `microservices/HalfQR.QrEngine/Storage/FileSystemRenderArtifactStore.cs` | `SaveAsync`, `GetAsync` | writes and reads artifact files from disk |
| `microservices/HalfQR.QrEngine/Storage/FileSystemRenderStorageLayout.cs` | `EnsureJobDirectory`, `EnsureArtifactsDirectory`, `GetJobFilePath` | defines the on-disk directory structure |
| `microservices/HalfQR.QrEngine/Storage/RenderArtifactNaming.cs` | `NormalizeFormat`, `GetFileName` | normalizes artifact format names and file names |
| `microservices/HalfQR.QrEngine/Storage/PostgresRenderJobStateStore.cs` | `SaveAsync`, `GetAsync`, `EnsureInitializedAsync`, `GetQualifiedTableName` | optional JSONB-backed job-state persistence |
| `microservices/HalfQR.QrEngine/Storage/R2RenderArtifactStore.cs` | `SaveAsync`, `GetAsync`, `BuildObjectKey`, `CreateClient`, `ResolveServiceUrl` | optional R2-backed artifact persistence |
| `microservices/HalfQR.QrEngine/Storage/RenderJobStateJson.cs` | serializer options | keeps persisted enum encoding aligned with API responses |

### Contracts

| File | Purpose |
| --- | --- |
| `microservices/HalfQR.Contracts/Requests/SubmitRenderJobRequest.cs` | incoming render job request contract |
| `microservices/HalfQR.Contracts/Responses/RenderJobAcceptedResponse.cs` | accepted-job response contract |
| `microservices/HalfQR.Contracts/Responses/RenderJobStatusResponse.cs` | status polling response contract |
| `microservices/HalfQR.Contracts/Messages/RenderJobQueuedMessage.cs` | RabbitMQ message containing only `JobId` |
| `microservices/HalfQR.Contracts/Models/RenderJobState.cs` | durable job-state model |
| `microservices/HalfQR.Contracts/Models/RenderArtifactState.cs` | durable artifact metadata model |
| `microservices/HalfQR.Contracts/Models/QrLogoOptions.cs` | logo SVG and size contract |
| `microservices/HalfQR.Contracts/Models/QrFinderOptions.cs` | border and center finder shape contract |
| `microservices/HalfQR.Contracts/Models/QrColorOptions.cs` | dark and light color contract |
| `microservices/HalfQR.Contracts/Models/QrOutputOptions.cs` | output format and size contract |
| `microservices/HalfQR.Contracts/Enums/*.cs` | content types, finder shapes, ECC level, render mode, job status |
| `microservices/HalfQR.Contracts/Options/*.cs` | RabbitMQ, render storage, PostgreSQL store, and R2 option models |

### Frontend Integration

| File | Key functions | What they do |
| --- | --- | --- |
| `webapp/src/components/qr-builder.tsx` | `queueRenderJob`, `resolveApiUrl`, `normalizeAcceptedJob`, `normalizeJobStatusResponse` | posts render requests, polls job status, and resolves relative API URLs against the public API base |

### Placeholder Services

| File | Reality |
| --- | --- |
| `microservices/HalfQR.Identity/Program.cs` | `Hello World!` placeholder |
| `microservices/HalfQR.Redirector/Program.cs` | `Hello World!` placeholder |
| `microservices/HalfQR.Billing/Program.cs` | `Hello World!` placeholder |
| `microservices/HalfQR.Cli/Program.cs` | bootstrap console with stale status text |

---

## 14. Complexity and Risk Notes

These are the most important implementation truths a new contributor should understand immediately.

1. The queue message is intentionally tiny. If job state persistence breaks, the worker has nothing useful to render because RabbitMQ carries only the `JobId`.
2. The current backend is closer to a queued render appliance than to the full platform described in the architecture plan.
3. The public contract advertises more content types than the payload encoder truly supports semantically today.
4. `QrOutputOptions.Format` and `QrRenderMode.Managed` are ahead of the current behavior and should not be treated as authoritative implemented features.
5. The configuration hash ignores the raw logo SVG body, which may matter for dedupe, caching, and identity semantics later.
6. The current render pipeline trusts incoming logo SVG without a dedicated sanitization step.
7. RabbitMQ failure handling is simple and understandable, but there is no dead-letter queue or structured retry policy yet.
8. The API streams artifacts back through itself even when the backing store is remote object storage; there is no presigned URL path yet.
9. Redis and several named microservices are currently structural placeholders, not live parts of the runtime.
10. `HalfQR.PublicApi` registers `IQrPayloadEncoder` and `IHashService`, but the current public API path does not actually invoke them directly; those services are consumed by the worker-side render engine.

If the team keeps this document updated as the system grows, it will remain the fastest route to understanding the difference between **HalfQR as planned** and **HalfQR as implemented**.
