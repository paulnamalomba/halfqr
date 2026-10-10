# HalfQR

## UI design rules (webapp + console)

These apply to every page in `webapp/`: the marketing site, the QR builder, sign-in/sign-up and the developer console.

### Type
- The only typeface is Plus Jakarta Sans, loaded once in `webapp/src/app/layout.tsx` as `--font-sans`. Monospace is used only for code, keys and references.
- Normal text is `text-xs` (0.75rem). Buttons, inputs, labels and body copy all use this size.
- The largest text anywhere is `text-base` (1rem), including heroes, headline and marketing cards.

### Controls and surfaces
- All buttons, icon buttons, text fields, search fields, dropdowns, date pickers and selectors share one height.
- Controls use a 12px radius. Cards, tables, dialogs and other parent containers use a 14px radius.
- No box-shadows on buttons. No hover scale; size never changes on hover.

### Content
- Headers are title + description only. No eyebrow/kicker labels above titles.
- Do not use SVG icons unless necessary: only icon-only controls (menu, close) and required brand marks (Google "G").
- Selected/active states use a shade only: a light shade on dark backgrounds, a dark shade on light backgrounds. Never a left-border bar or inset ring.

### Where to change things
Webapp and console share the treatment but keep separate, configurable tokens:

| Token set | File | Scope |
|---|---|---|
| `--web-font-body`, `--web-font-heading`, `--web-control-height`, `--web-control-radius`, `--web-surface-radius`, `--web-active-on-light`, `--web-active-on-dark` | `webapp/src/app/globals.css` | `:root` |
| `--console-font-body`, `--console-font-heading`, `--console-control-height`, `--console-control-radius`, `--console-surface-radius`, `--console-active-on-light`, `--console-active-on-dark` | `webapp/src/app/console.css` | `.console` |

Never hard-code sizes, radii or heights that these tokens cover.

### Shared building blocks
Reuse these before writing new markup:
- Webapp: `components/site/page-hero.tsx` (`PageHero`, `ActionRow`), `components/site/info-card-grid.tsx`, `.web-button` / `.web-button-primary` / `.web-button-secondary`.
- Console: `components/console/ui.tsx` (`BrandLockup`, `PageHeader`, `SectionCard`, `Notice`, `EmptyState`, `StatCard`, `ChipList`), `.btn` variants, `.icon-btn`, `.console-input`, `.console-select`, `.choice-row`, `.card`, `.table`.

### Sign-in page
Carded hero on the left: logo with "Developer console" at the top, terminal sample at the bottom, nothing in between. Keep the form copy terse.

## Code style
Prefer pure, reusable functions and components and shared CSS classes. Extract repeated markup or logic instead of copying it.
