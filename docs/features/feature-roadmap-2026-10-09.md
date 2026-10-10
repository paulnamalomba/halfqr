# Feature Roadmap — 2026-10-09

Features that fit the current architecture (PublicApi, Worker, RabbitMQ, Postgres, Redis, R2, stub Identity / Billing / Redirector services). Ordered roughly by dependency and value.

Related: [frontend-route-coverage-2026-10-09.md](./frontend-route-coverage-2026-10-09.md), [../security/api-security-audit-2026-10-09.md](../security/api-security-audit-2026-10-09.md).

## F1. API keys and per-key quotas
- `HalfQR.Identity` issues and revokes keys; PublicApi validates them in middleware.
- `AddRateLimiter` partitions by key. Redis holds counters across instances.
- Prerequisite for a public API and for security items H1, H2 and L1.

## F2. Dynamic (managed) QR codes
- `HalfQR.Redirector` resolves a short code to a target URL and issues an HTTP redirect.
- Target is editable after printing. Contract already has `QrRenderMode.Managed`.
- Postgres `qr` schema for short codes; Redis for hot lookups.
- Core paid feature.

## F3. Scan analytics
- Redirector records each scan (time, coarse geo, device class, referrer) to the `analytics` schema proposed in `system-architecture.md` §11.4.
- Dashboard in the webapp and later in `admin/`.
- Depends on F2.

## F4. Non-URL payload encoders
- Add encoders in `QrPayloadEncoder` for:
  - `Text`: raw text
  - `Email`: `mailto:` with subject and body
  - `Call`: `tel:`
  - `Sms`: `sms:` / `SMSTO:`
  - `VCard`: vCard 3.0
  - `WiFi`: `WIFI:T:<auth>;S:<ssid>;P:<password>;H:<hidden>;;` with escaping
  - `Event`: iCalendar `VEVENT`
- Relax `QrRenderRequestValidator.ValidateTarget` so these types validate their `Payload` fields instead of requiring `TargetUrl`.
- Unlocks frontend gap G1.

## F5. Batch and CSV rendering
- One request (or CSV upload) creates many render jobs; the Worker zips the artifacts.
- Fits the existing RabbitMQ and Worker pipeline.

## F6. Job expiry and cleanup
- TTL on job state and artifacts (file system and R2), swept by a Worker background service.
- Also closes security item L1 for anonymous jobs.

## F7. Job completion webhooks
- Optional `callbackUrl` on `SubmitRenderJobRequest`; the Worker POSTs a signed (HMAC) payload when the job ends.
- Alternative to the frontend's 1500 ms polling for API customers.
- Callback URLs must be validated against SSRF (block private, loopback and link-local ranges).

## F8. Scannability check
- After rendering, the Worker decodes the PNG (for example with ZXing.Net) and compares the result to `EncodedPayload`.
- Adds a `scannable` flag and warning to the job status. Catches bad contrast, oversized logos and aggressive finder shapes.

## F9. Admin dashboard
- `admin/` currently has only a README.
- Views: job throughput, failures by reason, per-key usage, storage size, key management.
- Depends on F1.

## F10. Accounts, saved designs and brand templates
- Identity accounts; `qr` schema stores reusable presets (colors, finder shape, data pattern, logo).
- Builder loads and saves templates.

## Smaller Additions
- `GET /api/v1/qr/limits` returning validation limits, so the webapp and docs read them from one source instead of hard-coding them.
- Additional gradient modes (`QrGradientMode` validation currently allows only `Linear`).
- PDF and EPS artifact formats for print customers.
- OpenAPI document (`Microsoft.AspNetCore.OpenApi`) to generate the API documentation page and client types.
