# Frontend Route Coverage — 2026-10-09

How well the webapp (`webapp/src`) uses the backend routes and contract options exposed by `HalfQR.PublicApi`.

The browser calls the API through the Next.js proxy `webapp/src/app/api/halfqr/[...path]/route.ts`, which forwards `GET` and `POST` to `HALFQR_API_BASE_URL`.

## Route Coverage

| Backend route | Used by webapp | Where |
|---|---|---|
| `POST /api/v1/qr/render/draft` | Yes | `qr-builder.tsx` (debounced 450 ms preview) |
| `POST /api/v1/qr/render` | Yes | `qr-builder.tsx` submit |
| `GET /api/v1/qr/jobs/{jobId}` | Yes | `qr-builder.tsx` polling every 1500 ms |
| `GET /api/v1/qr/jobs/{jobId}/artifacts/{format}` | Yes | Download links and preview image |
| `GET /api/v1/qr/content-types` | **No** | Content types are hard-coded in `qr-builder.tsx:43` |
| `GET /healthz` | No | Ops only; optional status indicator |
| `GET /` | No | Ops only |

## Contract Coverage Gaps

### G1. Content types
- Backend enum `QrContentType` (`microservices/HalfQR.Contracts/Enums/QrContentType.cs`): `Link`, `Text`, `Email`, `Call`, `Sms`, `WhatsApp`, `VCard`, `WiFi`, `Event`, `App`, `Social`, `Pdf`, `Image`, `Video` (14).
- Webapp type (`qr-builder.tsx:43`): `Link`, `App`, `Social`, `Pdf`, `Image`, `Video`, `WhatsApp` (7).
- Missing in the webapp: `Text`, `Email`, `Call`, `Sms`, `VCard`, `WiFi`, `Event`.
- Backend gap too: `QrRenderRequestValidator.ValidateTarget` requires `TargetUrl` for every type except `WhatsApp`, and `QrPayloadEncoder.Encode` only builds URL or WhatsApp payloads. These seven types cannot be rendered meaningfully today, so frontend work depends on backend encoders (see [feature-roadmap-2026-10-09.md](./feature-roadmap-2026-10-09.md), F4).

### G2. Render mode
- `QrRenderMode.Managed` exists in the contract. The webapp has no UI for it, and the backend has no Redirector. Out of scope until the Redirector ships.

### G3. Output and error correction options
- `SubmitRenderJobRequest.ErrorCorrectionLevel` and `Output.SizePx` (256 to 4096) are part of the contract. Confirm whether the builder exposes them; the request builder (`buildRenderRequest`) sends defaults in the paths reviewed.

### G4. Services with no routes yet
- `HalfQR.Identity`, `HalfQR.Billing`, `HalfQR.Redirector`: only `GET /` "Hello World!". No frontend work possible yet.
- `admin/` contains only `README.md`.

### G5. API documentation page is incomplete
- `webapp/src/app/api-documentation/page.tsx` documents `POST /api/v1/qr/render`, `GET /api/v1/qr/jobs/{jobId}`, `GET /api/v1/qr/jobs/{jobId}/artifacts/{format}`.
- Missing: `POST /api/v1/qr/render/draft`, `GET /api/v1/qr/content-types`.
- No documentation of validation limits (size 256 to 4096 px, SVG logo ≤ 120,000 chars, raster logo ≤ 512 KB, logo size 12 to 24 %, backdrop padding 10 to 80 %).

## Recommended Frontend Work
1. Load content types from `GET /api/v1/qr/content-types` and filter against the types the builder has forms for, so new backend types do not need a frontend deploy to appear.
2. Add `/render/draft` and `/content-types` to the API documentation page, with the validation limits.
3. Expose error correction level and output size in the builder's advanced section.
4. Add forms for `Text`, `Email`, `Call`, `Sms`, `VCard`, `WiFi`, `Event` once backend encoders exist.
