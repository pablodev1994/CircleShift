# Design tokens

CircleShift's look is defined by a small set of CSS custom properties in
[`apps/web/src/styles.css`](../../apps/web/src/styles.css). Components use the
tokens, never raw colors, so a palette change happens in one place.

## Color

| Token | Value | Used for |
|---|---|---|
| `--cs-color-bg` | `#f8faf9` | Page background |
| `--cs-color-surface` | `#ffffff` | Header, cards, hero, skip link |
| `--cs-color-text` | `#1f2933` | Body text |
| `--cs-color-muted` | `#52606d` | Secondary text: step descriptions, footer, role preview |
| `--cs-color-primary` | `#0f766e` | Links, primary buttons, active navigation, step numbers |
| `--cs-color-primary-contrast` | `#ffffff` | Text on primary backgrounds |
| `--cs-color-accent` | `#b45309` | Focus outline, notice border |
| `--cs-color-border` | `#d9e2ec` | Dividers and card borders (decorative only) |
| `--cs-color-notice-bg` | `#fffbeb` | Background of `.notice` callouts |

### Contrast (WCAG 2.2)

Ratios are computed with the WCAG relative-luminance formula. Every text pair
meets AA for normal text (4.5:1); the focus outline meets the 3:1 non-text
minimum.

| Foreground | Background | Ratio | Result |
|---|---|---|---|
| text `#1f2933` | surface `#ffffff` | 14.76:1 | AAA |
| text `#1f2933` | bg `#f8faf9` | 14.08:1 | AAA |
| text `#1f2933` | notice-bg `#fffbeb` | 14.23:1 | AAA |
| muted `#52606d` | surface `#ffffff` | 6.46:1 | AA |
| muted `#52606d` | bg `#f8faf9` | 6.16:1 | AA |
| primary `#0f766e` | surface `#ffffff` | 5.47:1 | AA |
| primary `#0f766e` | bg `#f8faf9` | 5.22:1 | AA |
| primary-contrast `#ffffff` | primary `#0f766e` | 5.47:1 | AA |
| accent outline `#b45309` | bg `#f8faf9` | 4.79:1 | ≥ 3:1 non-text |

`--cs-color-border` (1.31:1 on white) is only for decoration. Never use it as
the only boundary of a form field or control; inputs get a darker outline when
forms arrive in the profile work.

## Spacing, shape and type

| Token | Value | Notes |
|---|---|---|
| `--cs-space` | `1rem` | Base gap; layouts use multiples (0.25×–2×) |
| `--cs-radius` | `0.5rem` | Cards, buttons, navigation pills |
| `--cs-font` | `system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif` | System stack: no web-font download |

Body text is 1rem with a 1.5 line height. Content is capped at `60rem` wide.

## Interaction rules

- **Focus:** every focusable element shows a 3px `--cs-color-accent` outline
  with a 2px offset (`:focus-visible`). Don't remove it.
- **Skip link:** the first focusable element jumps to `#main`.
- **Current page:** active navigation uses `--cs-color-primary` with
  `aria-current="page"`; color is never the only signal.
- **Color scheme:** light only for the MVP (`color-scheme: light`). A dark
  palette must repeat the contrast table above before it ships.

## Adding a token

1. Add the custom property to `:root` in `styles.css` with a `--cs-` prefix.
2. If it is a color, add it to both tables here with its measured ratios.
3. Use the token in components; raw hex values stay in `:root` only.
