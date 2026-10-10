# Code Simplification And Review — 2026-10-09

Two passes over the repository:
1. `code-simplifier` agent across all `.cs` sources in `microservices/` and `webapp/src`.
2. `/code-review high` on the resulting diff.

## 1. Simplification Pass (applied, uncommitted)

Verification: `dotnet build HalfQR.sln` passed with 0 warnings and 0 errors. Webapp `tsc --noEmit` passed before and after (run against a scratch install, because `webapp/node_modules` is not installed). No lint script exists in `webapp/package.json`.

### .NET

| File | Change |
|---|---|
| `microservices/HalfQR.Cli/Program.cs` | Preset-selected check runs once. SVG/PNG/JPEG logo branches share one builder. Preset paths share one folder. |
| `microservices/HalfQR.PublicApi/Program.cs` | Removed single-use `statusUrl` variable. |
| `microservices/HalfQR.QrEngine/Rendering/QrFinderSvgComposer.cs` | Deleted uncalled internal `Compose(...)` and unused `QRCoder` using. Finder constants shared with `QrSvgComposer`. |
| `microservices/HalfQR.QrEngine/Rendering/QrSvgComposer.cs` | Reuses finder constants. Added `IsInFinderSpan` helper. `AppendLogo` closes `</g>` once. |
| `microservices/HalfQR.QrEngine/Rendering/QrLogoProcessor.cs` | Removed unused `GetLuminance` and duplicate `Format`. |
| `microservices/HalfQR.QrEngine/Rendering/SvgNumberFormat.cs` | New internal shared `Format(double)`, replacing three copies. |
| `microservices/HalfQR.QrEngine/Storage/FileSystemRenderStorageLayout.cs` | Added `JobFileName` constant for `"job.json"`. |
| `microservices/HalfQR.QrEngine/Storage/FileSystemRenderJobStateStore.cs` | Uses `JobFileName`. |

### Webapp

| File | Change |
|---|---|
| `webapp/src/components/qr-builder.tsx` | Removed `selectPreviewArtifact` alias. Nested ternaries (alt text, submit label) replaced by helpers. `targetUrl` simplified to `input.targetUrl \|\| undefined`. Flattened `resolvePreviewPayload`. Added `normalizePhoneDigits`. `isAbsoluteHttpUrl` reuses `normalizeAbsoluteHttpUrl`. Merged duplicate `<rect>` templates. Added `isInPreviewFinderSpan`. SVG logo uploads reuse `toSvgDataUrl`. `readImageAspectRatio` is a plain promise chain. |
| `webapp/src/components/site-header.tsx` | Removed unused `next/image` import. |

### Deliberately not changed
- Values that feed only commented-out UI in `qr-builder.tsx`: `getPreviewBadge`, `getPreviewSourceLabel`, `getTimelineText`, `describeStyling`, `formatApiTarget`, `renderPlan`, `resolvedPreviewTarget`. `page.tsx` `generatorFacts` ("Will wire properly later").
- Duplicate storage-provider registration and RabbitMQ setup in `HalfQR.PublicApi` and `HalfQR.Worker`. Sharing it needs a DI package reference in `HalfQR.QrEngine` or `HalfQR.Contracts`.
- `clsx` is imported by `webapp/src/utils/classname-modulariser.ts` but not declared in `webapp/package.json`.

## 2. Review Findings

No behavior regressions found in the simplification diff. Confirmed safe: `QrLogoOptions` is a `sealed record` (CLI `with` compiles); removed `Compose` and `GetLuminance` had no callers; `selectPreviewArtifact` was a pure alias.

### Correctness

#### R1. SVG logo aspect ratio mismatch between preview and server (pre-existing)
- Location: `webapp/src/components/qr-builder.tsx:1905` (`readImageAspectRatio`)
- An SVG with only `viewBox="0 0 400 100"` (no `width`/`height`) reports `naturalWidth` 0 in Firefox, so the function returns 1. The server derives 4:1 from the `viewBox`. The preview's logo and safe area do not match the final render.
- Fix: for `image/svg+xml`, parse the `viewBox` from the file text as a fallback before using `naturalWidth`/`naturalHeight`.

### Tooling

#### R2. Prettier quote style conflicts with codebase
- Location: `webapp/.prettierrc:4`
- `"singleQuote": true`, but the code uses double quotes (~405 double-quote lines vs 2 single-quote lines in `qr-builder.tsx`). `npm run format` would rewrite nearly every line.
- Fix: `"singleQuote": false`, or reformat in a dedicated commit.

#### R3. `prettier` not declared as a dependency
- Location: `webapp/package.json:9`
- `format` / `format:check` call `prettier`, which is only present transitively (3.8.3 in the lockfile).
- Fix: add `prettier` to `devDependencies` with a pinned version.

#### R4. Format glob targets non-existent folders
- Location: `webapp/package.json:9`
- Glob `{src,apps,packages,scripts,tests}/**/*`: `apps`, `packages`, `scripts`, `tests` do not exist in `webapp`. Root files (`next.config.ts`, `global.d.ts`) are never checked.
- Fix: target `.` and rely on `.prettierignore`.

### Repository hygiene

#### R5. Build outputs tracked by git
- Location: `.gitignore:18`
- `*/obj/` and `*/bin/` match one level only, so `microservices/HalfQR.*/obj/` is tracked. Every build modifies ~30 tracked files.
- Fix:

```gitignore
**/obj/
**/bin/
*.tsbuildinfo
```

then `git rm -r --cached microservices/*/obj microservices/*/bin`.

#### R6. `webapp/tsconfig.tsbuildinfo` untracked and not ignored
- Excluded by `.prettierignore` but not `.gitignore`. Covered by the R5 fix.

### Performance

#### R7. Local preview SVG rebuilt on every render
- Location: `webapp/src/components/qr-builder.tsx:405` (`rawLocalPreviewSvgMarkup`)
- Runs `qrcodeGenerator`, SVG building and `encodeURIComponent` on every keystroke and every 1500 ms status poll, even when `activeDraftPreview` or `preferredArtifact` replaces it.
- Fix: `useMemo` keyed on the render inputs, and skip when a draft or artifact preview exists.

### Duplication

#### R8. Finder geometry defined twice in C#
- Location: `microservices/HalfQR.QrEngine/Rendering/QrSvgComposer.cs:193`
- `IsInFinderWindow` / `IsInFinderSpan` recompute finder origins that `QrFinderSvgComposer.BuildOverlay` defines. A geometry change in one leaves data modules under or beside the overlay and can break scanning.
- Fix: expose one `QrFinderSvgComposer.IsInFinderWindow(row, column, moduleCount)` and call it from `QrSvgComposer`.

#### R9. Finder geometry duplicated in TypeScript preview
- Location: `webapp/src/components/qr-builder.tsx:1707`
- `isInPreviewFinderWindow` / `isInPreviewFinderSpan` mirror the C# geometry by hand. Server changes must be repeated here or the preview drifts from the final render.
- Fix: rely on `/api/v1/qr/render/draft` for previews where latency allows, or document the shared constants in one place with a test that compares both.

## Notes
- During the simplification run, another process added `webapp/.prettierrc`, `webapp/.prettierignore`, `format` scripts in `webapp/package.json`, and deleted `webapp/webapp/package-lock.json`. R2 to R4 refer to those changes.
- The review did not run `dotnet build` or `tsc` to avoid more churn in tracked `obj/` folders.
