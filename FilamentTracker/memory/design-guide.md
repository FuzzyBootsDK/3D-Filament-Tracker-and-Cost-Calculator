# Filament Tracker — Style & Design Guide

## 1. Overview

Filament Tracker uses a **dark-first, utility-focused design language** built entirely on CSS custom properties. The app currently ships with five dark-family themes (Dark, Nebula, Starbucks, Harmony, Spring), each using radial gradient background blobs to add depth without being distracting. The UI prioritises dense, scannable information layouts with heavy typography and rounded cards.

---

## 2. Color System

All colors are defined as CSS custom properties in `wwwroot/css/site.css`. **Never use hardcoded hex values for theme-sensitive colors.**

### Dark Mode (`.dark`)
| Token | Value | Usage |
|---|---|---|
| `--bg` | `#0b1220` | Page background base |
| `--panel` | `#101a2c` | Primary card/panel background |
| `--panel2` | `#0f172a` | Secondary panel, input backgrounds |
| `--panelSolid` | `#0c1323` | Modals (no transparency) |
| `--border` | `rgba(255,255,255,.10)` | Standard border |
| `--border2` | `rgba(255,255,255,.14)` | Elevated border (modal edges) |
| `--text` | `#e6edf7` | Primary text |
| `--muted` | `rgba(230,237,247,.68)` | Secondary/label text |
| `--shadow` | `0 18px 55px rgba(0,0,0,.52)` | Elevated shadow |

### Semantic / Accent Colors (all themes)
| Token | Value | Usage |
|---|---|---|
| `--accent` | `#3b82f6` | Primary interactive accent (blue) |
| `--ok` | `#10b981` | Success / in-stock / connected |
| `--warn` | `#f59e0b` | Warning / low stock |
| `--danger` | `#ef4444` | Error / critical stock / destructive |
| `--focus` | `0 0 0 3px rgba(59,130,246,.35/.25)` | Keyboard focus ring |

### Background Gradient
Each theme uses a two-blob radial gradient overlaid on the base `--bg`:
```css
background:
  radial-gradient(1200px 600px at 30% -10%, rgba(59,130,246,.18/.08), transparent 60%),
  radial-gradient(900px 500px at 90% 10%,  rgba(16,185,129,.10/.05), transparent 55%),
  var(--bg);
```
Blue blob top-left with a complementary secondary blob; exact opacity/colors vary by theme.

### Gradient Accents (UI elements)
- **Primary action gradient**: `linear-gradient(135deg, #4f46e5, #7c3aed)` (indigo → purple)
- **Progress bar**: `linear-gradient(90deg, #3b82f6, #6366f1)` (blue → indigo)
- **Nozzle temp box**: `linear-gradient(135deg, #4f46e5, #7c3aed)`
- **Bed temp box**: `linear-gradient(135deg, #10b981, #06b6d4)`
- **Fan/warning box**: `linear-gradient(135deg, #f59e0b, #ef4444)`
- **Nav bar background**: `linear-gradient(180deg, rgba(255,255,255,.06), rgba(255,255,255,.02))`

---

## 2.5 Named Color Themes

`ThemeService.ThemeName` (string) drives the CSS class on the root `<div>` in `MainLayout.razor` and `document.body.className` via JS interop. Available values: `"dark"`, `"nebula"`, `"starbucks"`, `"harmony"`, `"spring"`.

All four named variants are **dark-based** — they inherit dark-default CSS rules and only override the token values below. Per-theme accent overrides for `.navbtn.active`, `.btn.primary`, `.spoolRow.selected`, and `.tinyBtn.primary` are defined at the end of `site.css`.

### 🌌 Nebula (`.nebula`)
Palette source: deep indigo + violet + magenta highlights.

| Token | Value | Role |
|---|---|---|
| `--bg` | `#141E30` | Page background (deep indigo) |
| `--panel` | `#1F2A44` | Cards |
| `--panel2` | `#1A243A` | Secondary panels |
| `--panelSolid` | `#1F2A44` | Modals |
| `--border` | `rgba(255,255,255,.14)` | Standard border |
| `--border2` | `rgba(255,255,255,.20)` | Elevated border |
| `--text` | `#E6E8F5` | Primary text |
| `--muted` | `rgba(230,232,245,.68)` | Secondary text |
| `--accent` | `#c084fc` | Interactive accent (violet) |
| `--ok` | `#22c55e` | Success |
| `--warn` | `#f59e0b` | Warning |
| `--danger` | `#ef4444` | Danger |
| `--focus` | `0 0 0 3px rgba(192,132,252,.35)` | Focus ring |

Background: `radial-gradient(at 30% -10%, rgba(168,85,247,.20))` + `radial-gradient(at 90% 10%, rgba(236,72,153,.10))` over `#141E30`.

Accent overrides:
- `.navbtn.active`: `rgba(192,132,252,.22)` bg, `rgba(192,132,252,.42)` border
- `.btn.primary`: `linear-gradient(135deg, #a855f7, #c084fc)`

### ☕ Starbucks (`.starbucks`)
Palette source: forest green + warm copper + dark brown cream.

| Token | Value | Role |
|---|---|---|
| `--bg` | `#1e1a16` | Page background (dark warm brown) |
| `--panel` | `#1A322F` | Cards (deep forest green) |
| `--panel2` | `#152924` | Secondary panels (darker forest) |
| `--panelSolid` | `#1c2e2b` | Modals |
| `--border` | `rgba(222,215,207,.12)` | Standard border (cream-tinted) |
| `--border2` | `rgba(222,215,207,.18)` | Elevated border |
| `--text` | `#DED7CF` | Primary text (warm stone/cream) |
| `--muted` | `rgba(222,215,207,.60)` | Secondary text |
| `--accent` | `#927E63` | Interactive accent (copper brown) |
| `--ok` | `#4a9e6b` | Success (muted forest green) |
| `--warn` | `#c9922a` | Warning (warm amber) |
| `--danger` | `#c0392b` | Danger (deep red) |
| `--focus` | `0 0 0 3px rgba(146,126,99,.40)` | Focus ring |

Background: `radial-gradient(at 30% -10%, rgba(26,50,47,.65))` + `radial-gradient(at 90% 10%, rgba(146,126,99,.18))` over `#1e1a16`.

Accent overrides:
- `.navbtn.active`: `rgba(146,126,99,.22)` bg, `rgba(146,126,99,.40)` border
- `.btn.primary`: `linear-gradient(135deg, #927E63, #7a6852)`

---

### 🎵 Harmony (`.harmony`)
Palette source: deep navy + vivid orange + steel blue + pale sage.

| Token | Value | Role |
|---|---|---|
| `--bg` | `#08192a` | Page background (near-black navy) |
| `--panel` | `#0F2A3D` | Cards (deep navy) |
| `--panel2` | `#0a2233` | Secondary panels |
| `--panelSolid` | `#0d2739` | Modals |
| `--border` | `rgba(113,158,189,.18)` | Standard border (steel-blue tinted) |
| `--border2` | `rgba(113,158,189,.28)` | Elevated border |
| `--text` | `#DDEFCE` | Primary text (pale sage/mint) |
| `--muted` | `rgba(221,239,206,.60)` | Secondary text |
| `--accent` | `#FFA439` | Interactive accent (vivid orange) |
| `--ok` | `#52a87a` | Success (muted sage green) |
| `--warn` | `#e08030` | Warning (darker orange) |
| `--danger` | `#e05252` | Danger |
| `--focus` | `0 0 0 3px rgba(255,164,57,.35)` | Focus ring (orange glow) |

Background: `radial-gradient(at 30% -10%, rgba(15,42,61,.80))` + `radial-gradient(at 90% 10%, rgba(113,158,189,.18))` over `#08192a`.

Accent overrides:
- `.navbtn.active`: `rgba(255,164,57,.18)` bg, `rgba(255,164,57,.38)` border
- `.btn.primary`: `linear-gradient(135deg, #FFA439, #e08030)` with `color: #0a1e2e` (dark text on orange)

---

### 🌿 Spring (`.spring`)
Palette source: deep forest green + vivid teal.

| Token | Value | Role |
|---|---|---|
| `--bg` | `#06231D` | Page background (deep forest) |
| `--panel` | `#0C342C` | Cards (dark forest green) |
| `--panel2` | `#09231a` | Secondary panels |
| `--panelSolid` | `#0b3027` | Modals |
| `--border` | `rgba(7,102,83,.32)` | Standard border (teal-tinted) |
| `--border2` | `rgba(7,102,83,.48)` | Elevated border |
| `--text` | `#bce0d6` | Primary text (light mint) |
| `--muted` | `rgba(188,224,214,.60)` | Secondary text |
| `--accent` | `#0db897` | Interactive accent (bright teal) |
| `--ok` | `#22c69a` | Success (spring green) |
| `--warn` | `#e8a030` | Warning (warm amber) |
| `--danger` | `#d95050` | Danger |
| `--focus` | `0 0 0 3px rgba(13,184,151,.35)` | Focus ring (teal glow) |

Background: `radial-gradient(at 30% -10%, rgba(7,102,83,.40))` + `radial-gradient(at 90% 10%, rgba(12,52,44,.70))` over `#06231D`.

Accent overrides:
- `.navbtn.active`: `rgba(13,184,151,.20)` bg, `rgba(13,184,151,.40)` border
- `.btn.primary`: `linear-gradient(135deg, #0db897, #09997d)`

---

### Theme Picker UI
The `SettingsPage.razor` Appearance section renders a `.themeGrid` of `.themeCard` elements. Each card shows four `.themeSwatch` color bars (bg → panel → accent → text) and a label. The active theme gets `border-color: var(--accent)`. Cards use `flex: 1 1 120px` for responsive wrapping.

---

## 3. Typography

### Font Stack
```css
--font: ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial;
```
System font stack — no external font dependencies.

### Weight Scale
| Weight | Usage |
|---|---|
| `700` | Form labels, `<label>` elements, helper text |
| `800` | Body UI text, button labels, sub-headings, `.navbtn`, `.detailLabel` |
| `900` | KPI values, tile headings, stat numbers, `.kpi .value`, `.progressText` |
| `950` | Panel titles, modal headings |
| `1000` | Close/icon buttons |

**Rule:** Always use 800+ for anything interactive. Values and counts use 900+. The heavy weight scale is a defining characteristic of this UI — do not use 400 or 600 for new elements.

### Size Scale
| Size | Usage |
|---|---|
| `12px` | Labels, helper text, metadata, pills |
| `13px` | Detail values, compact table cells |
| `14px` | Standard body, nav status text |
| `16px` | Modal headings |
| `20px` | Nav button text |
| `28px` | Page `<h1>` headings |
| `32px` | Large progress percentage |
| `40px` | Idle state icons |

---

## 4. Spacing & Layout

### Page Wrapper
```css
.wrap { max-width: 2000px; margin: 0 auto; padding: 22px; }
```

### Border Radius Scale
| Value | Usage |
|---|---|
| `999px` | Pills, badges, status dots, rounded buttons |
| `22px` | Page header, formCard, nav bar, modals |
| `18px` | Tiles, KPI cards, strips, warning banners |
| `16px` | Standard panels, nav buttons, page sections |
| `14px` | Spool rows, inner list items |
| `12px` | Buttons (`.btn`), inputs, checkboxes, icon buttons |
| `10px` | Small info boxes, temp box wrappers |
| `8px` | Inline tags, print detail rows, disconnect message |

### Top Navigation Layout
- Flex row, wraps to column below 1200px
- `navButtons`: flex wrap, items `flex: 1 1 calc(33.333% - 12px)` → 3-column grid naturally
- Below 720px: 2-column (`calc(50% - 12px)`); below 480px: full width
- Live tracker widget fixed at `360px` max-width, `max-width: 40%`, moves below nav on mobile

### Grid Systems
- **Inventory tiles**: `repeat(auto-fill, minmax(200px, 1fr))`
- **KPIs**: `repeat(3, minmax(0, 1fr))`
- **Form grid**: `repeat(4, minmax(0, 1fr))` with `.colSpan2/3/4` helpers
- **Calculator**: `minmax(0, 1.3fr) minmax(0, 1fr)` two-column, collapses at 900px

---

## 5. Component Library

### `formCard` — Section Container
```css
margin-top: 14px; border: 1px solid var(--border); border-radius: 22px;
background: rgba(255,255,255,.03); padding: 16px;
```
Used on every Settings section, Help sections, and any form page. Light mode: `background: var(--panel)`.

### `.pageHeader` — Page Title Block
```css
margin-top: 16px; border: 1px solid var(--border); border-radius: 22px;
background: rgba(255,255,255,.03); padding: 18px;
```
Contains `<h1>` (28px) and `.sub` (12px muted). Every page starts with this.

### `.tile` — Inventory Card
- `border-radius: 18px`, `min-height: 150px`
- Left status rail: 4px wide, colored by `--ok` / `--warn` / `--danger`
- Hover: `translateY(-2px)` + `brightness(1.06)`
- `.stacked` modifier for multi-spool variants

### `.kpi` — Stat Card
- `border-radius: 18px`, flex row, space-between
- `.label` (12px muted) + `.value` (28px weight-900, `#93c5fd` dark / `#3b82f6` light)

### `.btn` — Button
- Base: `border-radius: 12px`, `font-weight: 800`, `padding: 10px 12px`
- `.primary`: blue tint `rgba(59,130,246,.18)` + blue border
- `.danger`: red tint `rgba(239,68,68,.16)` + red border
- Hover: `brightness(1.08)`; Focus: `box-shadow: var(--focus)`

### `.input` / `select` / `textarea`
- `border-radius: 12px`, `padding: 11px 12px`, `width: 100%`
- Focus: `box-shadow: var(--focus)` (no outline)
- Selects: forced `background: var(--panel)` with `!important` for cross-browser consistency

### `.pill` — Badge/Tag
- `border-radius: 999px`, `font-size: 12px`, `font-weight: 900`
- `.warn`: amber tint; `.danger`: red tint; `.ok`: green tint

### `.strip` — Alert/Warning Banner
- `border-radius: 18px`, flex row, space-between
- Default: amber theme (`rgba(245,158,11,.25)` border, `.10` background)
- Can be overridden inline for success/error variants

### `.modal`
- `border-radius: 22px`, `max-width: min(860px, 100%)`, `max-height: 90vh`
- Backdrop: `rgba(0,0,0,.72)` with `backdrop-filter: blur(2px)`
- Header, scrollable body, and footer sections separated by `var(--border)` dividers

### `.liveTracker` — Live Tracking Widget
- `border-radius: 18px`, `border: 1px solid var(--border)`, flex column
- Header with centered title + status indicator
- Status dot: 8px circle with `animation: pulse 2s infinite` when connected (green `#22c55e`)
- Disconnected dot: slate `#94a3b8`, no animation

---

## 6. Status & Feedback Colors

### Stock Status (Inventory Tiles)
| State | CSS class | Color |
|---|---|---|
| Healthy | `.ok` | `var(--ok)` — `#10b981` green |
| Low stock | `.low` | `var(--warn)` — `#f59e0b` amber |
| Critical | `.critical` | `var(--danger)` — `#ef4444` red |

### Connection Status
| State | Color | Dot animation |
|---|---|---|
| Connected | `rgb(34,197,94)` | Pulse (2s infinite) |
| Disconnected | `rgb(148,163,184)` | None |

### WiFi Signal Bars (5-bar system)
| Bars | Color |
|---|---|
| 1–2 | `#ef4444` (red/weak) |
| 3 | `#f59e0b` (amber/ok) |
| 4–5 | `#22c55e` (green/strong) |
| Inactive | `rgba(255,255,255,.18)` |

---

## 7. Animations

| Animation | Usage |
|---|---|
| `pulse` (opacity 1→0.5→1, 2s infinite) | Connected status dot |
| `scroll-left` (translateX 0%→-100%, 24s linear infinite) | Long filename marquee |
| `transition: width 0.25–0.3s ease` | Progress bar fill |
| `transition: transform .12s ease, filter .12s ease` | Tile hover lift |
| `filter: brightness(1.06–1.08)` | Button/tile hover |

---

## 8. Icon Language

Emoji are used as icons throughout the UI. This is intentional and consistent:

| Context | Examples |
|---|---|
| Navigation buttons | 📦 🔁 🗄️ 🧮 🖨️ ⚙️ ❓ ➕ |
| Section headers (h2) | 🎨 💱 🌍 ⚖️ 🖨️ |
| Status | ✅ ⚠️ 🔴 💤 |
| Actions | 💾 📥 📤 🗑️ |
| Data types | 🌡️ 🛏️ ⏱️ 📏 🎯 |

---

## 9. Accessibility

- All interactive elements expose `var(--focus)` on `:focus` (keyboard navigation)
- `outline: none` on inputs/buttons is always paired with a `box-shadow` focus ring
- `tabindex="0"` on non-button clickable elements (e.g., tiles)
- `user-select: none` on nav buttons to prevent accidental text selection

---

## 10. Do's and Don'ts

| ✅ Do | ❌ Don't |
|---|---|
| Use `var(--accent)`, `var(--ok)`, `var(--warn)`, `var(--danger)` for status colors | Hardcode `#3b82f6` or `#ef4444` for theme-sensitive UI |
| Use `filter: brightness()` for hover states | Change `background-color` on hover for dark glass panels |
| Use `formCard` for new page sections | Create one-off container styles that duplicate `formCard` |
| Keep emoji icons consistent with the existing vocabulary | Mix emoji styles or use SVG icons inconsistently |
| Use `border-radius: 22px` on top-level cards, 18px on tiles/KPIs | Use arbitrary radius values |
| Apply `font-weight: 800+` to all UI text | Use weights below 700 for new interactive elements |
| Test both dark and light mode | Build only for dark mode |
