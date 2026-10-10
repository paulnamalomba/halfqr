# API Security Audit — 2026-10-09

Scope: `microservices/HalfQR.PublicApi`, `microservices/HalfQR.QrEngine`, `webapp/src/app/api/halfqr/[...path]/route.ts`, `docker-compose.yml`, service `appsettings.json` files.

Related: [api-and-backend-security.md](./api-and-backend-security.md) (earlier open-API rollout review). This audit records the gaps that are still open in the code as of this date.

## Public API Surface

| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/` | none | Service banner, version |
| GET | `/healthz` | none | Liveness |
| GET | `/api/v1/qr/content-types` | none | Enum names |
| POST | `/api/v1/qr/render/draft` | none | Synchronous SVG render |
| POST | `/api/v1/qr/render` | none | Queues job via RabbitMQ |
| GET | `/api/v1/qr/jobs/{jobId:guid}` | none | Job status |
| GET | `/api/v1/qr/jobs/{jobId:guid}/artifacts/{format}` | none | Artifact download |

`HalfQR.Identity`, `HalfQR.Billing` and `HalfQR.Redirector` only map `GET /` returning `"Hello World!"`.

## High

### H1. No authentication on any endpoint
- Location: `microservices/HalfQR.PublicApi/Program.cs`
- No API key, token, or caller identity. No `AddAuthentication` / `RequireAuthorization`.
- Impact: unlimited anonymous use of render and draft endpoints; no way to attribute, throttle, or revoke abuse.
- Fix: API keys issued by `HalfQR.Identity`, validated by middleware in PublicApi. Keep the webapp on a server-side key used only by the proxy.

### H2. No rate limiting
- Location: `microservices/HalfQR.PublicApi/Program.cs`, `webapp/src/app/api/halfqr/[...path]/route.ts`
- `/api/v1/qr/render/draft` renders synchronously on the request thread. `/api/v1/qr/render` writes job state and publishes to RabbitMQ per call.
- The webapp proxy forwards any path under `/api/halfqr/` with no throttling, so it is an open relay to the API.
- Fix: `builder.Services.AddRateLimiter(...)` with partitioned fixed-window or token-bucket limits (per API key, fall back to per IP). Apply a stricter policy to `/render/draft`. Add edge rate limiting in front of the proxy.

### H3. Raster logo decompression bomb
- Location: `microservices/HalfQR.QrEngine/Rendering/QrLogoProcessor.cs:85`
- `SKBitmap.Decode(rawBytes)` decodes the full image. The validator caps only encoded size (`MaxRasterLogoBytes = 512 * 1024`), not pixel dimensions.
- A 512 KB PNG declaring 20000×20000 decodes to ~1.6 GB RGBA. The webapp's `maxRasterLogoPixels = 2_000_000` is client-side only.
- Fix: read dimensions before decoding.

```csharp
using var codec = SKCodec.Create(new MemoryStream(rawBytes))
    ?? throw new InvalidOperationException("Unable to decode the uploaded raster logo.");

if ((long)codec.Info.Width * codec.Info.Height > MaxRasterLogoPixels)
{
    throw new InvalidOperationException("Uploaded raster logos exceed the maximum pixel count.");
}
```

Also add the same check to `QrRenderRequestValidator.ValidateLogo` so it fails as a 400 before work starts.

### H4. Default credentials and exposed infra ports
- Location: `docker-compose.yml`, `microservices/HalfQR.PublicApi/appsettings.json:6`, `microservices/HalfQR.Worker/appsettings.json:6`
- Postgres and RabbitMQ default to `halfqr_dev_password`. The same password is committed in both `appsettings.json` files.
- Host-published ports: Postgres `5436`, Redis `6386` (no `requirepass`), RabbitMQ AMQP `5673`, RabbitMQ management UI `15673`.
- Fix: remove default passwords from compose (`${POSTGRES_PASSWORD:?required}`), drop the password from committed `appsettings.json`, set Redis `requirepass`, and stop publishing infra ports outside local dev (use a `docker-compose.override.yml` for dev ports).

## Medium

### M1. No request size or payload caps
- Kestrel default body limit is 30 MB. `SubmitRenderJobRequest.Payload` (`Dictionary<string, string?>`) has no key-count or value-length limit.
- Fix: set `MaxRequestBodySize` to ~1 MB for render routes; validate `Payload.Count` and each value length in `QrRenderRequestValidator`.

### M2. Internal exception text leaked to clients
- Location: `microservices/HalfQR.PublicApi/Services/RenderJobService.cs`
- `FailureReason = $"Queue dispatch failed: {exception.Message}"` is persisted and returned by `GET /api/v1/qr/jobs/{jobId}`.
- Fix: store a generic client message; log the exception server side.

### M3. Integer enum values accepted
- Location: `microservices/HalfQR.PublicApi/Program.cs` (`new JsonStringEnumConverter()`)
- `JsonStringEnumConverter` allows integer values by default, so `"contentType": 99` deserializes to an undefined enum.
- Fix: `new JsonStringEnumConverter(allowIntegerValues: false)` and/or `Enum.IsDefined` checks in the validator for `ContentType`, `Mode`, `ErrorCorrectionLevel`, `Data.Pattern`, `Finder` shape, `Logo.SourceType`.

### M4. SVG sanitizer gaps
- Location: `microservices/HalfQR.QrEngine/Rendering/QrLogoProcessor.cs` (`ValidateSvgTree`)
- `url(...)` is only checked in `style` attributes and `<style>` elements. Presentation attributes such as `fill="url(https://…)"`, `stroke`, `clip-path`, `mask`, `filter` are not checked.
- No limit on element count or `<use>` reference depth (amplification against the rasterizer).
- `XDocument.Parse` is used with default settings; pass an `XmlReader` with `DtdProcessing = DtdProcessing.Prohibit` and `XmlResolver = null` to make DTD rejection explicit.
- Fix: apply `ContainsUnsafeCssUrl` to every attribute value; cap total elements; reject `<use>` chains deeper than a small limit.

### M5. Missing security headers
- No HSTS, `X-Content-Type-Options: nosniff`, or CSP on API responses.
- SVG artifacts are served from the API origin as `image/svg+xml`. Opening one directly renders it in the API origin.
- Fix: add `UseHsts()` behind TLS, a header middleware for `nosniff`, and on artifact responses `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'` plus `Content-Disposition: attachment`.

### M6. Proxy forwards all inbound headers upstream
- Location: `webapp/src/app/api/halfqr/[...path]/route.ts` (`copyHeaders`)
- `Cookie`, `Authorization` and any client header reach the API. No body size cap on `request.arrayBuffer()`.
- Fix: allowlist headers (`content-type`, `accept`), and reject bodies over ~1 MB before reading.

## Low

### L1. Job and artifact access relies only on GUID secrecy
- Anyone holding a job ID can read status and artifacts indefinitely. No owner binding, no expiry.
- Fix: bind jobs to the API key that created them once H1 lands; add TTL cleanup in the Worker.

### L2. `Mode = Managed` accepted but unimplemented
- `QrRenderMode.Managed` passes validation, but there is no Redirector, so the output is behaviorally a static code.
- Fix: reject `Managed` with a 400 until the Redirector exists.

## Suggested Order
1. H3 (small, contained, removes a memory DoS).
2. M1, M3 (validator-only changes).
3. H2 (rate limiter).
4. M2, M5, M6.
5. H4 (deployment config).
6. H1, L1 (needs Identity).

## Remediation Status (2026-10-09)

| ID | Status | Where |
|---|---|---|
| H1 | Fixed. API keys (`hqr_sk_`) and access tokens (`hqr_pat_`) issued by `HalfQR.Identity`, verified by PublicApi through `/internal/v1/credentials/verify` with a 60 s cache. `ApiAccess:RequireApiKey` turns off anonymous access. | `HalfQR.PublicApi/Security/*`, `HalfQR.Identity` |
| H2 | Fixed. `AddRateLimiter` per credential (plan limit) or per client IP; the webapp proxy forwards the browser IP only with `X-HalfQR-Proxy-Secret`. Identity sign-in endpoints limited to 10/min per IP. | `HalfQR.PublicApi/Program.cs`, `HalfQR.Identity/Program.cs` |
| H3 | Fixed. `SKCodec` header check rejects rasters over 4096 × 4096 before decoding. | `QrLogoProcessor.EnsureDecodableRasterSize` |
| H4 | Fixed. Compose requires secrets from `.env` (`:?`), infra ports bound to `127.0.0.1`, Redis `requirepass`, dev passwords moved to `appsettings.Development.json`. | `docker-compose.yml`, `.env.example` |
| M1 | Fixed. 1 MB Kestrel body cap; payload entry, key, value and target URL length caps. | `QrRenderRequestValidator.ValidatePayloadShape` |
| M2 | Fixed. Generic queue failure text in PublicApi; Worker only surfaces `InvalidOperationException` messages. | `RenderJobService`, `Worker` |
| M3 | Fixed. `allowIntegerValues: false` plus `Enum.IsDefined` checks for every request enum. | `QrRenderRequestValidator.ValidateEnums` |
| M4 | Fixed. `url()` checked on every attribute, 4,000 element cap, 64 `<use>` cap, no `<use>` chains, DTDs prohibited. | `QrLogoProcessor` |
| M5 | Fixed. `nosniff`, `Referrer-Policy`, HSTS on HTTPS, sandboxed CSP on artifact downloads. | `HalfQR.PublicApi/Program.cs` |
| M6 | Fixed. Proxy forwards only `accept` and `content-type`, only `/api/v1/qr/*` paths, 1 MB body cap. | `webapp/src/app/api/halfqr/[...path]/route.ts` |
| L1 | Fixed for keyed jobs: jobs carry `OwnerCredentialId` and return 404 to other callers. Anonymous jobs still rely on GUID secrecy; TTL cleanup remains open (feature F6). | `RenderJobService.IsVisibleTo` |
| L2 | Fixed. `Mode = Managed` rejected with 400 until the Redirector ships. | `QrRenderRequestValidator.ValidateEnums` |

Additional controls added with the developer console:
- Sign-in emails are checked against `disposable-email-domains` (always blocked) and `spam-domains-list` (blocked unless in `trusted-email-providers.txt`, because that list includes Gmail-scale providers such as outlook.com and yahoo.com).
- API keys and access tokens require an active paid plan from `HalfQR.Billing`; credentials stop verifying when the plan lapses.
- PayChangu webhooks require an HMAC-SHA256 `Signature` in Production and are always re-verified against PayChangu (amount, currency, reference) before a plan activates. Mock mode is refused in Production.
- Dashboard mutations go through a same-origin-checked BFF; the session token stays in an `httpOnly` cookie.
