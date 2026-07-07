# API And Backend Security For HalfQR Open API Rollout

## Contents

- [API And Backend Security For HalfQR Open API Rollout](#api-and-backend-security-for-halfqr-open-api-rollout)
  - [Contents](#contents)
  - [Purpose](#purpose)
  - [Executive Summary](#executive-summary)
  - [What The Current Code Actually Exposes](#what-the-current-code-actually-exposes)
    - [Public API surface today](#public-api-surface-today)
    - [Worker pipeline today](#worker-pipeline-today)
    - [PostgreSQL usage today](#postgresql-usage-today)
    - [Webapp behavior today](#webapp-behavior-today)
  - [Backdoor Review Outcome](#backdoor-review-outcome)
  - [High-Risk Findings And Hardening Advice](#high-risk-findings-and-hardening-advice)
    - [1. Anonymous render endpoints need rate limits before public launch](#1-anonymous-render-endpoints-need-rate-limits-before-public-launch)
    - [2. Job status and artifact routes are anonymous and rely mainly on GUID secrecy](#2-job-status-and-artifact-routes-are-anonymous-and-rely-mainly-on-guid-secrecy)
    - [3. Internal exception text currently leaks into user-visible job status](#3-internal-exception-text-currently-leaks-into-user-visible-job-status)
    - [4. Request validation is good, but not deep enough for a hostile public API](#4-request-validation-is-good-but-not-deep-enough-for-a-hostile-public-api)
    - [5. SVG sanitization is meaningfully better than average, but it should not be the only guard](#5-svg-sanitization-is-meaningfully-better-than-average-but-it-should-not-be-the-only-guard)
    - [6. The webapp proxy route is too generic for a larger production API surface](#6-the-webapp-proxy-route-is-too-generic-for-a-larger-production-api-surface)
    - [7. PostgreSQL access is safer than raw string SQL, but production roles are still too powerful](#7-postgresql-access-is-safer-than-raw-string-sql-but-production-roles-are-still-too-powerful)
    - [8. Default infrastructure values are too permissive for internet-facing deployment](#8-default-infrastructure-values-are-too-permissive-for-internet-facing-deployment)
    - [9. Redis is planned for rate limiting, but not actually protecting the runtime yet](#9-redis-is-planned-for-rate-limiting-but-not-actually-protecting-the-runtime-yet)
  - [Open API Security Model That Fits HalfQR](#open-api-security-model-that-fits-halfqr)
    - [Recommended trust tiers](#recommended-trust-tiers)
      - [Tier 0: anonymous public access](#tier-0-anonymous-public-access)
      - [Tier 1: API key access](#tier-1-api-key-access)
      - [Tier 2: authenticated ownership](#tier-2-authenticated-ownership)
  - [Concrete Technical Tips By Layer](#concrete-technical-tips-by-layer)
    - [API gateway and edge](#api-gateway-and-edge)
    - [ASP.NET public API](#aspnet-public-api)
    - [Worker and queue](#worker-and-queue)
    - [Artifact storage](#artifact-storage)
    - [Logging and observability](#logging-and-observability)
  - [PostgreSQL Prompt Injection Protection](#postgresql-prompt-injection-protection)
    - [Current reality in this codebase](#current-reality-in-this-codebase)
    - [The core rule](#the-core-rule)
    - [Safe architecture for AI plus PostgreSQL](#safe-architecture-for-ai-plus-postgresql)
    - [Mandatory guardrails if you ever add AI-assisted database access](#mandatory-guardrails-if-you-ever-add-ai-assisted-database-access)
      - [1. Use a dedicated read-only AI role](#1-use-a-dedicated-read-only-ai-role)
      - [2. Lock the search path](#2-lock-the-search-path)
      - [3. Use views or stored procedures as the contract](#3-use-views-or-stored-procedures-as-the-contract)
      - [4. Enforce statement shape](#4-enforce-statement-shape)
      - [5. Apply database timeouts aggressively](#5-apply-database-timeouts-aggressively)
      - [6. Make AI database sessions read-only by default](#6-make-ai-database-sessions-read-only-by-default)
      - [7. Log prompt-to-query lineage safely](#7-log-prompt-to-query-lineage-safely)
      - [8. Separate retrieval from transactional data](#8-separate-retrieval-from-transactional-data)
    - [Prompt injection scenarios that matter for HalfQR specifically](#prompt-injection-scenarios-that-matter-for-halfqr-specifically)
    - [A concrete PostgreSQL policy for HalfQR](#a-concrete-postgresql-policy-for-halfqr)
  - [Additional Security Improvements That Fit This Codebase Well](#additional-security-improvements-that-fit-this-codebase-well)
    - [Sanitize outputs, not only inputs](#sanitize-outputs-not-only-inputs)
    - [Add lifecycle cleanup for anonymous jobs](#add-lifecycle-cleanup-for-anonymous-jobs)
    - [Treat RabbitMQ as private infrastructure only](#treat-rabbitmq-as-private-infrastructure-only)
    - [Use separate environments and secrets discipline](#use-separate-environments-and-secrets-discipline)
  - [Recommended Rollout Order](#recommended-rollout-order)
    - [P0: must do before open API launch](#p0-must-do-before-open-api-launch)
    - [P1: should do immediately after launch](#p1-should-do-immediately-after-launch)
    - [P2: required before AI plus PostgreSQL features](#p2-required-before-ai-plus-postgresql-features)
  - [Final Take](#final-take)

---

## Purpose

This document is a context-aware security review and rollout guide for exposing HalfQR as an open API while keeping the system safely anonymous-first.

It is based on the current code in:

- `microservices/HalfQR.PublicApi`
- `microservices/HalfQR.Worker`
- `microservices/HalfQR.QrEngine`
- `webapp/src/components/qr-builder.tsx`
- `webapp/src/app/api/halfqr/[...path]/route.ts`
- `docker-compose.yml`
- `SYSTEM_IMBROGLIO.md`

The goal is not to lock the product down so hard that it stops being open. The goal is to keep the anonymous QR generation experience, while preventing abuse, data leakage, queue exhaustion, malicious SVG uploads, and future AI-to-database failures.

## Executive Summary

The current codebase is not showing evidence of an intentional backdoor, but it is not yet hardened enough for a public open API rollout.

The most important facts from the current implementation are:

1. The active backend is effectively two real processes: `HalfQR.PublicApi` and `HalfQR.Worker`.
2. The public API currently exposes anonymous render, job polling, and artifact download routes.
3. The active runtime has CORS and request validation, but no authentication, no authorization, no rate limiting, no quota enforcement, and no abuse controls.
4. PostgreSQL is currently used only as an optional render-job state store, not as a general application database yet.
5. The PostgreSQL code is already using parameterized values for job data, which is good, but the app currently performs schema and table creation at runtime, which is not ideal for production.
6. The webapp proxy route is a generic pass-through to the upstream API host and should not remain that open as the backend grows.
7. The SVG upload path has a meaningful sanitizer and size limits already, which is a strong start, but it still needs surrounding controls because the platform will be internet-facing.
8. Internal exception messages can currently flow back into persisted job state and then back out through anonymous status endpoints.

If you expose this as an open API without adding controls around identity, quotas, validation depth, edge filtering, worker isolation, and database policy, your main problem will not be classic SQL injection first. Your first problem will be abuse, denial-of-service, data leakage, and storage exhaustion.

## What The Current Code Actually Exposes

### Public API surface today

`microservices/HalfQR.PublicApi/Program.cs` exposes:

- `GET /`
- `GET /healthz`
- `GET /api/v1/qr/content-types`
- `POST /api/v1/qr/render/draft`
- `POST /api/v1/qr/render`
- `GET /api/v1/qr/jobs/{jobId}`
- `GET /api/v1/qr/jobs/{jobId}/artifacts/{format}`

What is missing in that same file is equally important:

- no `AddAuthentication`
- no `UseAuthentication`
- no `AddAuthorization`
- no `UseAuthorization`
- no `AddRateLimiter`
- no `UseRateLimiter`
- no request-body size policy
- no response header hardening policy

That means the platform is currently relying on GUID unpredictability, CORS origin checks, and validation logic, not on an actual API security perimeter.

### Worker pipeline today

`microservices/HalfQR.Worker/Worker.cs` consumes RabbitMQ messages, loads the persisted job state, renders SVG and PNG, stores artifacts, and writes the updated job state back.

This is a clean architecture for asynchronous rendering, but it means the open API is also an open queue producer. If the public endpoint is abused, the queue and worker become the actual blast radius.

### PostgreSQL usage today

`microservices/HalfQR.QrEngine/Storage/PostgresRenderJobStateStore.cs` is the only current PostgreSQL path in the active rendering flow. The good news is:

- job identifiers are passed as parameters
- JSON state is passed as a parameter
- schema and table names are quoted

The important risks are:

- the application credential performs `CREATE SCHEMA IF NOT EXISTS` and `CREATE TABLE IF NOT EXISTS` at runtime
- the store persists full job state JSON, which may grow in the future
- the open API can indirectly drive database write volume if no upstream quotas exist

### Webapp behavior today

The browser-side builder in `webapp/src/components/qr-builder.tsx` is clearly client-driven. It builds requests, uploads logo content, and polls job status.

The local proxy route in `webapp/src/app/api/halfqr/[...path]/route.ts` forwards requests to the upstream API host by path. That is useful for same-origin local development, but it is intentionally generic. As more endpoints are added later, that route can accidentally become a broader relay than you intended.

## Backdoor Review Outcome

I did not find evidence of a deliberate backdoor in the code I reviewed.

I did not see:

- hidden admin bypass routes
- secret hardcoded master tokens
- covert database access paths
- suspicious path traversal helpers
- direct shell execution from user input
- raw SQL built from request data in the active render flow

That is the good news.

The real risk is not a secret malicious branch. The real risk is that the current system is still at the stage where "open" mostly means "anonymous and lightly validated," which is not enough once real internet traffic starts hitting it.

## High-Risk Findings And Hardening Advice

### 1. Anonymous render endpoints need rate limits before public launch

This is the largest immediate issue.

Your public API lets anonymous callers:

- submit draft renders
- enqueue full renders
- poll job status
- download generated artifacts

The current architecture has no built-in control over:

- requests per IP
- requests per API key
- concurrent jobs per caller
- bytes uploaded per minute
- render cost per minute
- artifact download storms

Why this matters technically:

- logo uploads can still consume CPU, memory, queue slots, and storage even if they pass validation
- draft renders can be used as a cheap amplification endpoint
- full render requests can create a queue backlog and worker saturation
- status polling can become a multiplier on every accepted job

What to do:

1. Add ASP.NET rate limiting in `HalfQR.PublicApi` immediately.
2. Put a stricter rule on `POST /api/v1/qr/render` than on `POST /api/v1/qr/render/draft`.
3. Add edge rate limiting at the reverse proxy or WAF, not only inside ASP.NET.
4. Track quota on at least IP, user agent fingerprint, and eventually API key.
5. Add a hard cap on outstanding queued jobs per caller.
6. Add burst and sustained limits separately.

Recommended model:

- anonymous draft preview: low cost, low quota, aggressively rate-limited
- anonymous full render: allowed, but smaller quota and harder throttling
- API key render: higher quota, better observability, easier abuse response
- authenticated saved/managed QR flows later: separate tier entirely

### 2. Job status and artifact routes are anonymous and rely mainly on GUID secrecy

`GET /api/v1/qr/jobs/{jobId}` and `GET /api/v1/qr/jobs/{jobId}/artifacts/{format}` are accessible without auth.

GUIDs are reasonably hard to guess, so this is not instantly catastrophic. But GUID secrecy is not the same as access control.

Why this matters:

- if a job ID leaks through logs, browser history, analytics, screenshots, or referrers, artifacts remain retrievable
- if later you persist customer-owned QR codes or managed assets, this model becomes insufficient immediately
- download URLs are relative paths, which is fine operationally, but still means possession of the job ID is effectively the credential

What to do:

1. Keep anonymous access only for short-lived, one-off jobs.
2. Add expiration to anonymous job state and artifacts.
3. Add signed artifact URLs or short-lived download tokens.
4. Do not use bare job IDs as long-term authorization material.
5. For future saved QR assets, require authenticated ownership checks.

Practical policy:

- anonymous jobs: expire aggressively, for example 15 minutes to 24 hours depending on product intent
- saved jobs: move to authenticated ownership model
- exported artifacts: use signed URLs with TTL, not stable public URLs

### 3. Internal exception text currently leaks into user-visible job status

This is a concrete code issue.

In `RenderJobService` and `Worker`, failures are written to `FailureReason` using raw `exception.Message`. That value is then returned by the status API.

Why this matters:

- queue connectivity failures can disclose infrastructure details
- storage failures can disclose internal file paths, bucket names, service URLs, or library behavior
- rendering failures can disclose parser behavior and validation internals to attackers performing recon

What to do:

1. Replace raw exception messages with stable public error codes.
2. Keep detailed exception text only in server-side logs.
3. Return user-safe categories such as `validation_failed`, `render_failed`, `queue_unavailable`, or `artifact_unavailable`.
4. Include a correlation ID in responses and logs.

This is one of the cheapest security improvements you can make because it reduces attacker feedback without changing the product behavior.

### 4. Request validation is good, but not deep enough for a hostile public API

`QrRenderRequestValidator` already does useful work:

- size range checks
- hex color checks
- absolute `http` and `https` validation
- SVG character cap
- raster byte cap
- logo size and padding bounds

That is a solid foundation.

What it still does not strongly limit:

- total request body size at the HTTP layer
- payload dictionary key count
- payload key length
- payload value length
- `targetUrl` length
- repeated status polls from the same caller
- JSON nesting and overall shape complexity

Why this matters:

The request contract uses `Dictionary<string, string?> Payload`. That is flexible, but it also means an attacker can send many keys or extremely large strings unless you explicitly stop them. Even if the renderer ignores most of that data, JSON parsing, logging, persistence, and queueing still pay the cost.

What to do:

1. Add Kestrel request-body limits.
2. Add per-field maximum lengths.
3. Limit payload key count to a very small number for each content type.
4. Reject unknown keys rather than accepting arbitrary payload dictionaries.
5. Define schema-per-content-type instead of one free-form dictionary for all future flows.

Best direction:

Move from a generic payload bag to explicit contracts such as:

- `LinkRenderRequest`
- `WhatsAppRenderRequest`
- `PdfRenderRequest`

That makes abuse resistance much easier.

### 5. SVG sanitization is meaningfully better than average, but it should not be the only guard

`QrLogoProcessor` already does several correct things:

- allow-list of SVG elements
- rejection of `on*` event attributes
- rejection of `javascript:` and `expression(`
- rejection of non-local `href` references
- rejection of `style` values using `url(`
- raster logo downscaling and background stripping pipeline

This is genuinely useful work and should be kept.

However, for an open API, treat SVG as hostile forever.

Additional hardening I recommend:

1. Explicitly reject `DOCTYPE` and entity declarations.
2. Reject comments, processing instructions, and any namespace you do not need.
3. Cap the number of total SVG nodes and attributes.
4. Cap path command count and total coordinate count if you see pathological SVGs in testing.
5. Add wall-clock timeouts around expensive parsing or rasterization operations.
6. Serve generated SVG downloads with safer headers.

Important output rule:

Because generated SVG files are later downloadable, treat them as potentially active content even after sanitization. Prefer download-oriented handling with:

- `Content-Disposition: attachment`
- `X-Content-Type-Options: nosniff`
- restrictive CSP where applicable at the edge

### 6. The webapp proxy route is too generic for a larger production API surface

`webapp/src/app/api/halfqr/[...path]/route.ts` forwards arbitrary subpaths beneath `/api/halfqr` to the upstream API host.

That is acceptable as a development convenience, but it becomes risky if left broad while the backend grows.

Why this matters:

- future auth endpoints may be unintentionally proxied
- future admin endpoints may be unintentionally reachable through a broader path pattern than intended
- the route currently deletes `origin` and `referer`, which is reasonable for proxying, but it is still a relay that should be constrained

What to do:

1. Replace generic pass-through with an allow-list of exact backend routes.
2. Use a server-only upstream variable for proxying and avoid relying on `NEXT_PUBLIC_*` values for server-side trust decisions.
3. Add timeout, body-size, and content-type checks in the proxy route.
4. Decide whether the proxy is a development-only feature or a permanent public edge.

If it is permanent, secure it like a gateway. If it is temporary, keep it narrow and documented as temporary.

### 7. PostgreSQL access is safer than raw string SQL, but production roles are still too powerful

The current PostgreSQL path is better than many early-stage systems because job data is parameterized.

That said, `PostgresRenderJobStateStore` still performs runtime DDL:

- `CREATE SCHEMA IF NOT EXISTS`
- `CREATE TABLE IF NOT EXISTS`

That means the app database role needs schema creation rights if you deploy it unchanged.

For a public open API, that is not the right long-term posture.

What to do:

1. Move schema creation into migrations or deployment-time SQL.
2. Run the application with a least-privilege role that cannot create or alter schema.
3. Separate reader, writer, and migration roles.
4. Keep PostgreSQL private on the network. Never expose it publicly because the API is public.
5. Require TLS between app and database in non-local environments.

Suggested roles:

- `halfqr_migrator`: DDL only, used in CI/CD or deployment
- `halfqr_api_writer`: insert and update only on required tables
- `halfqr_worker_writer`: write access only where worker needs it
- `halfqr_ai_readonly`: future read-only role if you ever add AI retrieval tooling

### 8. Default infrastructure values are too permissive for internet-facing deployment

The checked-in configuration shows several development defaults that are fine locally but must not survive into production assumptions:

- `AllowedHosts` is `*`
- docker-compose defaults use development-style passwords
- RabbitMQ management is published on a host port
- PostgreSQL is published on a host port
- Redis is published on a host port

This is normal for local development. It is not normal for public production exposure.

What to do:

1. Keep PostgreSQL, Redis, and RabbitMQ on private networks only.
2. Do not expose RabbitMQ management publicly.
3. Rotate all default credentials before any shared environment is used.
4. Remove any assumption that `AllowedHosts = *` is acceptable outside local or controlled staging contexts.
5. Terminate TLS at the edge and forward trusted headers correctly.

### 9. Redis is planned for rate limiting, but not actually protecting the runtime yet

Your architecture documents mention Redis for rate limiting and short-lived cache state, but `SYSTEM_IMBROGLIO.md` accurately notes that Redis is not used in the current code path.

This matters because people often think a system is protected because the architecture says it is. The code says otherwise.

Treat the current runtime as if there is no distributed rate limiting at all, because in practical terms that is true.

## Open API Security Model That Fits HalfQR

The right design for HalfQR is not "close the API." The right design is a tiered trust model.

### Recommended trust tiers

#### Tier 0: anonymous public access

Keep anonymous access for:

- content types listing
- draft preview
- limited one-off render submission

Controls required:

- WAF rules
- IP-based rate limiting
- body-size caps
- queue-depth protection
- artifact expiry
- anti-bot controls when abuse rises

#### Tier 1: API key access

Use API keys for:

- higher render volumes
- automated integrations
- better quotas
- better audit logs

Controls required:

- per-key quotas
- per-key analytics
- key rotation
- scoped privileges
- revocation support

#### Tier 2: authenticated ownership

Reserve user auth for:

- saved QR assets
- managed/dynamic QR codes
- billing-linked features
- customer dashboards
- scan analytics tied to accounts

This keeps the product open without confusing anonymous creation with persistent ownership.

## Concrete Technical Tips By Layer

### API gateway and edge

1. Put the public API behind a reverse proxy, WAF, or API gateway.
2. Enforce TLS and HSTS at the edge.
3. Rate-limit before requests reach ASP.NET.
4. Geo-block or ASN-block obvious abusive traffic if needed.
5. Add response headers:
   - `X-Content-Type-Options: nosniff`
   - `Referrer-Policy: no-referrer` or a strict equivalent
   - `Permissions-Policy` with only what you need
6. Log rejected requests separately from successful requests.

### ASP.NET public API

1. Add `AddRateLimiter` and `UseRateLimiter`.
2. Add request-body limits at server and endpoint level.
3. Add structured problem details with safe error codes.
4. Add endpoint-specific authorization when saved assets arrive.
5. Add correlation IDs to requests and logs.
6. Cap status polling frequency per caller.
7. Reject unsupported content types early.

### Worker and queue

1. Add queue depth alarms.
2. Add dead-letter queues for poison jobs.
3. Cap worker concurrency deliberately so one flood does not trigger memory collapse.
4. Set render timeouts.
5. Add storage quotas and eviction for anonymous jobs.
6. Separate anonymous and paid workload queues later if volume grows.

### Artifact storage

1. Expire anonymous artifacts aggressively.
2. Keep R2 buckets private.
3. Prefer signed URLs or gateway download checks.
4. Avoid stable, public, guessable artifact paths for long-lived content.
5. Scan stored SVG artifacts for policy compliance if you later persist them longer-term.

### Logging and observability

1. Never log full user payloads by default.
2. Never log base64 logo bodies.
3. Log render cost metrics:
   - request size
   - queue latency
   - render duration
   - output size
4. Alert on spikes in:
   - anonymous render rate
   - failed SVG validations
   - worker failure rate
   - status polling volume
   - artifact download volume per IP

## PostgreSQL Prompt Injection Protection

### Current reality in this codebase

There is no active LLM or natural-language-to-SQL pipeline in the code I reviewed.

That means prompt injection is not currently happening in the live render flow.

However, since you explicitly want open API guidance and mentioned prompt injection protection for PostgreSQL, the correct approach is to design for it now before an AI support tool, admin assistant, analytics helper, or natural-language dashboard query feature gets added.

### The core rule

Never let model output become executable SQL.

Do not do this:

1. user prompt
2. LLM generates SQL text
3. backend runs SQL text directly against PostgreSQL

That pattern turns prompt injection into database injection by design.

### Safe architecture for AI plus PostgreSQL

Use this pattern instead:

1. user prompt enters an AI-facing service
2. the AI service classifies intent into a small, server-defined action set
3. backend code maps that action to a prepared query, stored procedure, or read-only view
4. only the backend writes the final SQL
5. PostgreSQL is accessed with a restricted role

In other words, the model can choose an intent, not author arbitrary SQL.

### Mandatory guardrails if you ever add AI-assisted database access

#### 1. Use a dedicated read-only AI role

Create a database role such as `halfqr_ai_readonly` with:

- read-only access only
- no DDL
- no DML
- no extension management
- no role management
- no `COPY` to arbitrary destinations
- no access to secrets tables

Never reuse your main app writer role for AI features.

#### 2. Lock the search path

Set a fixed `search_path` explicitly for the AI role so the model cannot influence schema resolution indirectly.

Example policy idea:

- AI role can see only curated views in `ai_read`
- AI role cannot access raw `auth`, `billing`, or internal operational schemas directly

#### 3. Use views or stored procedures as the contract

Expose only curated read models, for example:

- `ai_read.qr_render_summary`
- `ai_read.daily_render_counts`
- `ai_read.plan_usage_summary`

The AI should query a narrow semantic layer, not raw tables.

#### 4. Enforce statement shape

If you absolutely must let the model produce a query-like artifact, validate all of the following before execution:

- single statement only
- `SELECT` only
- no semicolons beyond end of statement
- no comments
- no subcommands that escape to admin surfaces
- no references outside an allow-list of views
- no function calls outside an allow-list
- hard row limit
- hard timeout

Even then, the safer design is still server-generated SQL.

#### 5. Apply database timeouts aggressively

Set at least:

- `statement_timeout`
- `lock_timeout`
- `idle_in_transaction_session_timeout`

This stops one bad generated query from becoming a database availability incident.

#### 6. Make AI database sessions read-only by default

Use:

- read-only transaction mode
- low statement timeout
- row limit enforcement

Treat every AI query as untrusted, even if it came from your own model prompt.

#### 7. Log prompt-to-query lineage safely

For each AI-driven data request, log:

- caller identity
- prompt hash
- chosen server action
- final SQL identifier or stored procedure name
- row count returned
- execution time
- decision trace or rule outcome

Do not log raw secrets or personally sensitive payloads.

#### 8. Separate retrieval from transactional data

If you later introduce embeddings, semantic search, or RAG:

- store retrieval content in a separate schema or service boundary
- do not co-locate the AI retrieval role with the main transactional writer role
- keep auth data, billing data, and operational secrets out of the AI retrieval surface

### Prompt injection scenarios that matter for HalfQR specifically

These are realistic future risks for this product:

1. An AI support assistant reads saved QR metadata and is tricked into disclosing another tenant's data.
2. A dashboard assistant receives a malicious prompt in a QR name, campaign name, or analytics label and then broadens its query scope.
3. A developer convenience tool lets natural-language admin prompts produce direct SQL against production PostgreSQL.
4. A log-analysis agent reads failure text or stored payload content and is manipulated into running broader queries.

The correct defense is not "better prompting." The correct defense is architectural containment.

### A concrete PostgreSQL policy for HalfQR

When PostgreSQL becomes a broader platform store, I recommend this split:

- `auth` schema: never exposed to AI roles
- `billing` schema: never exposed to AI roles
- `qr` schema: exposed only through curated read views if needed
- `analytics` schema: exposed through pre-aggregated read views only
- `ai_read` schema: the only schema visible to AI querying roles

Then enforce:

- no public PostgreSQL ingress
- no superuser app credentials
- migrations outside runtime app role
- read-only AI role
- RLS for tenant-bound data if accounts are added
- query allow-list, not model freedom

## Additional Security Improvements That Fit This Codebase Well

### Sanitize outputs, not only inputs

You already sanitize SVG input. Also sanitize output handling.

Generated SVG is still a sensitive content type because browsers treat SVG as document-like in many contexts.

### Add lifecycle cleanup for anonymous jobs

Anonymous job state and artifacts should be short-lived. Otherwise your open API becomes a free storage platform.

Add:

- TTL for job records
- artifact cleanup worker
- storage quotas
- cleanup metrics

### Treat RabbitMQ as private infrastructure only

Do not let public deployment expose:

- AMQP ports to the public internet
- management UI to the public internet
- default credentials in any shared environment

### Use separate environments and secrets discipline

For production or any externally reachable staging:

- no default passwords
- no shared long-lived secrets
- use secret managers where possible
- rotate credentials on deployment boundary changes

## Recommended Rollout Order

### P0: must do before open API launch

1. Add ASP.NET and edge rate limiting.
2. Add request-body limits and per-field length caps.
3. Replace raw exception messages with safe public error codes.
4. Add artifact expiration and job retention policy.
5. Lock infrastructure to private networks for PostgreSQL, Redis, and RabbitMQ.
6. Remove production reliance on app-level DDL for PostgreSQL.
7. Narrow the webapp proxy route to an allow-list.

### P1: should do immediately after launch

1. Introduce API keys for higher-volume consumers.
2. Add signed artifact downloads.
3. Add structured abuse detection dashboards.
4. Separate anonymous workload from trusted workload.
5. Add gateway-level bot protection if traffic justifies it.

### P2: required before AI plus PostgreSQL features

1. Create a dedicated AI read-only PostgreSQL role.
2. Build curated views or stored procedures for AI retrieval.
3. Ban raw model-authored SQL.
4. Add query allow-list validation and timeout policy.
5. Keep AI retrieval away from auth and billing data.

## Final Take

HalfQR is already structured in a way that can become secure as an open API. The code is not chaotic, and the SVG handling is more thoughtful than many early implementations.

But right now the system is still in the stage where the main trust assumption is that callers will behave reasonably. An open API rollout removes that assumption.

The correct strategy is:

- keep anonymous QR creation
- add strong outer abuse controls
- reduce information leakage
- narrow proxy behavior
- harden artifact access
- move PostgreSQL to least privilege
- design future AI access so prompt injection cannot become executable SQL

If you do that, the platform can remain open without becoming easy to exhaust, easy to scrape, or easy to pivot through into backend infrastructure.
