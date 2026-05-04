# Styling and UX fixes

This document records the recent frontend styling and layout adjustments made in the webapp to improve visual consistency, interaction feedback, and preview presentation.

## Scope

The changes covered here affect:

- the home page layout shell
- shared global styling in the app stylesheet
- builder section header alignment
- interactive button hover behavior
- preview-stage media sizing
- colour input swatch presentation

Primary files involved:

- [webapp/src/app/page.tsx](../../webapp/src/app/page.tsx)
- [webapp/src/app/globals.css](../../webapp/src/app/globals.css)

---

## 1. Home page width constraint fix

### Problem

The home page hero section was rendering full bleed while similar sections on other routes were correctly constrained to the shared site width.

### Change

The home route structure was updated so the shared `site-page` container is applied at the `main` level instead of only around the builder section.

### Technical detail

In [webapp/src/app/page.tsx](../../webapp/src/app/page.tsx):

- `main` was changed from `page-shell-home` to `site-page page-shell-home`
- the hero section no longer uses `generator-hero-bleed`
- the nested `site-page` wrapper around the builder was removed

### Result

The home hero and builder now follow the same horizontal max-width rules as the other route pages.

---

## 2. Global button hover scaling

### Problem

Buttons did not have the requested stronger hover emphasis.

### Change

A shared hover-scale rule was added for enabled `button` elements, and existing hover-lift styles were updated to combine lift and scale.

### Technical detail

In [webapp/src/app/globals.css](../../webapp/src/app/globals.css):

- `button:not(:disabled):hover` now applies `transform: scale(1.06)`
- the existing grouped hover rule for styled interactive controls was changed from:
  - `translateY(-1px)`
- to:
  - `translateY(-1px) scale(1.06)`

This preserves the original lifted visual treatment while adding the requested scaling effect.

### Result

Buttons now feel more reactive and visually prominent on hover, while disabled buttons remain unaffected.

---

## 3. Builder step header vertical alignment

### Problem

The numbered step badge and the step heading copy were not vertically centered against each other.

### Change

The flex alignment in the shared step header wrapper was updated.

### Technical detail

In [webapp/src/app/globals.css](../../webapp/src/app/globals.css):

- `.generator-step-head` changed from `align-items: flex-start` to `align-items: center`

### Result

Step badges such as `1`, `2`, `3`, and `4` now share a cleaner vertical center with their corresponding heading blocks.

---

## 4. Preview stage image fill behavior

### Problem

The preview image was rendering as a smaller centered object inside the preview stage, leaving unused space around it.

### Change

The preview stage and preview media rules were updated so the rendered preview occupies the full stage footprint.

### Technical detail

In [webapp/src/app/globals.css](../../webapp/src/app/globals.css):

#### `.preview-stage`

- added `width: 100%`
- added `aspect-ratio: 1`
- changed `min-width: 420px` to `min-width: 0`
- removed internal spacing by changing `padding: 20px` to `padding: 0`

#### `.preview-artifact, .preview-mock`

- changed width from `min(100%, 300px)` to `100%`
- added `height: 100%`
- changed border radius to `inherit`
- removed the drop shadow to avoid the inset-card appearance

#### `.preview-artifact`

- removed inner padding
- removed the explicit border
- retained `object-fit: contain`

#### Mobile override

- the small-screen preview sizing rule was updated so preview media still use `width: 100%`

### Result

The preview now fills the stage area instead of appearing as a smaller framed object inside it. Because `object-fit: contain` is still used, the image remains centered and undistorted while maximizing its size within the stage.

---

## 5. Colour input swatch fill and rounded corners

### Problem

The native colour preview boxes did not visually fill their input containers and did not fully match the rounded shape of their parent control.

### Change

The colour input was restyled so its swatch occupies the full control and inherits the parent rounding.

### Technical detail

In [webapp/src/app/globals.css](../../webapp/src/app/globals.css):

#### `.field input[type="color"]`

- removed inset spacing by changing `padding: 4px` to `padding: 0`
- added `overflow: hidden`
- added `cursor: pointer`

#### WebKit-specific swatch styling

- `::-webkit-color-swatch-wrapper { padding: 0; }`
- `::-webkit-color-swatch { border: none; border-radius: inherit; }`

#### Firefox swatch styling

- `::-moz-color-swatch { border: none; border-radius: inherit; }`

### Result

The colour preview block now fills its full input surface and visually matches the rounded geometry of the parent field.

---

## Implementation notes

### Shared design intent

These changes collectively moved the UI toward a more consistent visual system:

- shared page sections respect the same width container
- step headings align more cleanly
- interactive controls have stronger hover affordance
- preview surfaces use available space more effectively
- colour controls better match surrounding rounded components

### CSS strategy used

The fixes were applied in the shared global stylesheet rather than adding component-local overrides. This keeps the styling rules centralized and consistent across the public webapp surface.

### Functional impact

These changes are visual and interaction-oriented only. They do not alter:

- request payload generation
- QR rendering logic
- API communication flow
- upload processing logic
- job polling behavior

---

## Summary

Recent styling work improved:

1. home page layout consistency
2. hover affordance for buttons
3. vertical alignment of step headers
4. preview-stage media usage
5. colour input presentation

The end result is a tighter, more consistent builder experience with better use of space and more polished control behavior.
