# Preferences

## Working Style
- Read memory files at session start; update at session end
- Ask clarifying questions before implementing; suggest improvements and wait for confirmation
- Keep changes minimal and targeted — do not refactor unrelated code
- Validate changes compile before finishing (run build)
- Stick to project folder: `C:\Users\lasr\source\repos\3D-Filament-Tracker-and-Cost-Calculator\FilamentTracker/`
- Prefer simplification through reusable components/services when it reduces duplication and keeps code easy to work with

## Code Style
- C# 14 / .NET 10 / Blazor Server
- Use existing CSS variables and component classes — never hardcode colors or spacing that already have a variable
- Match file-level conventions: inline styles only for dynamic/one-off values; shared styles go in `site.css`
- Comments only where they match existing style or explain non-obvious logic
- Prefer `replace_string_in_file` over full file rewrites

## Design Preferences
- **Dark mode is the primary/default theme** — light mode is fully supported but dark is the lead
- Deep navy/space aesthetic for dark mode; clean white/slate for light mode
- Radial gradient background blobs (blue + green tones) on both themes
- All interactive elements must respect `var(--focus)` for accessibility
- Font weights: 700 for labels, 800 for UI text, 900+ for values/headings — use heavy weights throughout
- Border radius scale: 22px (cards/page headers), 18px (tiles/KPIs/strips), 16px (panels/nav), 12px (buttons/inputs), 8–10px (inner boxes/tags), 999px (pills/badges)
- Hover interactions: `filter: brightness(1.06–1.08)` + subtle `translateY(-2px)` on tiles
- Status colors must always use CSS variables (`--ok`, `--warn`, `--danger`) — never raw hex for status
- Emoji icons are part of the UI language — used consistently in section headers and nav buttons
- Animations kept subtle: pulse for live status dots, smooth progress bar transitions
- AMS page preference: keep slot card backgrounds neutral (no full-card color tint), but keep/widen the small color swatch preview
