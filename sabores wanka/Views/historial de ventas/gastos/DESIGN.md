---
name: Obsidian Emerald
colors:
  surface: '#0f131c'
  surface-dim: '#0f131c'
  surface-bright: '#353942'
  surface-container-lowest: '#0a0e16'
  surface-container-low: '#181c24'
  surface-container: '#1c2028'
  surface-container-high: '#262a33'
  surface-container-highest: '#31353e'
  on-surface: '#dfe2ee'
  on-surface-variant: '#bbcabf'
  inverse-surface: '#dfe2ee'
  inverse-on-surface: '#2c3039'
  outline: '#86948a'
  outline-variant: '#3c4a42'
  surface-tint: '#4edea3'
  primary: '#4edea3'
  on-primary: '#003824'
  primary-container: '#10b981'
  on-primary-container: '#00422b'
  inverse-primary: '#006c49'
  secondary: '#d0bcff'
  on-secondary: '#3c0091'
  secondary-container: '#571bc1'
  on-secondary-container: '#c4abff'
  tertiary: '#ffb95f'
  on-tertiary: '#472a00'
  tertiary-container: '#e29100'
  on-tertiary-container: '#523200'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#6ffbbe'
  primary-fixed-dim: '#4edea3'
  on-primary-fixed: '#002113'
  on-primary-fixed-variant: '#005236'
  secondary-fixed: '#e9ddff'
  secondary-fixed-dim: '#d0bcff'
  on-secondary-fixed: '#23005c'
  on-secondary-fixed-variant: '#5516be'
  tertiary-fixed: '#ffddb8'
  tertiary-fixed-dim: '#ffb95f'
  on-tertiary-fixed: '#2a1700'
  on-tertiary-fixed-variant: '#653e00'
  background: '#0f131c'
  on-background: '#dfe2ee'
  surface-variant: '#31353e'
typography:
  headline-xl:
    fontFamily: Plus Jakarta Sans
    fontSize: 40px
    fontWeight: '700'
    lineHeight: 48px
    letterSpacing: -0.03em
  headline-xl-mobile:
    fontFamily: Plus Jakarta Sans
    fontSize: 30px
    fontWeight: '700'
    lineHeight: 38px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 32px
    fontWeight: '600'
    lineHeight: 40px
    letterSpacing: -0.025em
  headline-lg-mobile:
    fontFamily: Plus Jakarta Sans
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 22px
    fontWeight: '600'
    lineHeight: 28px
    letterSpacing: -0.02em
  title-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 24px
    letterSpacing: -0.015em
  body-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
    letterSpacing: -0.01em
  body-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
    letterSpacing: -0.005em
  body-sm:
    fontFamily: Plus Jakarta Sans
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
    letterSpacing: 0em
  label-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 13px
    fontWeight: '600'
    lineHeight: 18px
    letterSpacing: 0.01em
  label-sm:
    fontFamily: Plus Jakarta Sans
    fontSize: 11px
    fontWeight: '700'
    lineHeight: 14px
    letterSpacing: 0.04em
  code-num:
    fontFamily: Plus Jakarta Sans
    fontSize: 14px
    fontWeight: '600'
    lineHeight: 20px
    letterSpacing: -0.01em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  gutter: 1.25rem
  gutter-desktop: 1.5rem
  margin: 1rem
  margin-tablet: 1.5rem
  margin-desktop: 2.5rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 1rem
  space-lg: 1.5rem
  space-xl: 2rem
---

## Brand & Style

This design system targets sophisticated operators, venture-backed founders, and modern finance teams who demand institutional power wrapped in consumer-grade refinement. The emotional tone is decisive, calm, and uncompromisingly high-end—evoking the security of a private bank vault merged with the kinetic velocity of modern high-frequency tooling.

The visual direction merges **deep-space dark minimalism** with **tactile glassmorphism**. Translucent containers rest upon abyssal zinc layers, articulated by hairline luminescence rather than heavy drop shadows. Interactive states deploy subtle inner highlights, ambient focal flares, and kinetic emerald glows that reward precision without cluttering operational focus.

## Colors

The foundation is established on rich, cold-toned abyss tones:
- **Canvas Base:** `#0B0F17` (Deep Obsidian Zinc)
- **Elevated Canvas / Cards:** `#131A26` with opacity variants (`rgba(19, 26, 38, 0.7)`)
- **Primary Accent (`#10B981`):** Electric Emerald. Denotes monetary inflow, liquidity, positive balance, and primary task execution.
- **Secondary Accent (`#8B5CF6`):** Electric Violet. Applied to automation, foreign exchange, institutional integrations, and multi-currency controls.
- **Tertiary Accent (`#F59E0B`):** Warm Amber. Utilized for approval thresholds, pending expense status, policy flags, and rate alerts.

### Surface Color Rules
1. **Background Contrast:** Never use absolute black (`#000000`). All canvas planes must carry zinc undertones to preserve border glow perception.
2. **Hairline Outlines:** Interactive and static container borders leverage translucent white or emerald tinting (`rgba(255, 255, 255, 0.08)` to `rgba(16, 185, 129, 0.3)` on hover).
3. **Data Emphasizers:** Reserve vibrant saturation purely for actionable states, key financial totals, and micro-metrics. Secondary content rests at muted slate (`#94A3B8`).

## Typography

Plus Jakarta Sans delivers structural geometry with humanist terminals, rendering both dense expense tables and massive financial hero metrics legible across OLED monitors. 

### Typographic Hierarchy Rules
- **Monetary Figuration:** Set monetary sums, currency tickers, and account counters using tabular numerals (`font-feature-settings: "tnum" on, "cv05" on`) to maintain baseline harmony across multi-row breakdowns.
- **Negative Tracking:** Apply negative tracking (`-0.03em` to `-0.015em`) on all headline styles above 18px to tighten visual density and ensure an authoritative editorial appearance.
- **Micro Labels:** Micro-badges, table headers, and category tags utilize `label-sm` with explicit uppercase transformation and `0.04em` tracking for crisp scanning at rapid speeds.

## Layout & Spacing

The layout is built upon an 8-point base spatial rhythm, scaled tightly for complex financial dashboards, split-pane analytical views, and centered expense modals.

### Breakpoints & Fluid Adaptation
- **Mobile (< 768px):** 4-column structure, single-stack execution. Margins set to `margin` (16px), full-width drawer modals, fixed bottom action toolbars.
- **Tablet (768px - 1024px):** 8-column layout with `margin-tablet` (24px). Modals expand to max 560px width with glass underlays.
- **Desktop (> 1024px):** 12-column layout with `margin-desktop` (40px) and `gutter-desktop` (24px). Standard financial split screens pin metadata to fixed sidebars (360px) while records and charts consume fluid center columns.

### Density Tiers
Modal windows and dense data-entry sheets enforce compact padding (`space-sm` between input fields, `space-md` for structural fieldsets) to display high-context financial audits above the browser fold.

## Elevation & Depth

Visual hierarchy uses frosted optical depth and neon ray diffusion rather than dark drop shadows.

### Glassmorphism & Tiers
- **Tier 0 (Canvas Base):** Solid `#0B0F17`. Non-blurred ground plane.
- **Tier 1 (Surface Containers):** Background `rgba(19, 26, 38, 0.75)`, backdrop blur `16px`, subtle edge stroke `1px solid rgba(255, 255, 255, 0.06)`.
- **Tier 2 (Modals & Overlays):** Background `rgba(19, 26, 38, 0.88)`, backdrop blur `24px`, ambient backlight shadow `0 24px 48px -12px rgba(0, 0, 0, 0.7)`. Top border incorporates a gradient sheen: `linear-gradient(90deg, rgba(16, 185, 129, 0.3) 0%, rgba(255, 255, 255, 0.1) 50%, rgba(139, 92, 246, 0.3) 100%)`.
- **Tier 3 (Floating Menus / Tooltips):** Background `rgba(28, 38, 54, 0.95)`, backdrop blur `32px`, hairline edge `rgba(255, 255, 255, 0.12)`.

### Glowing Border Physics
Interactive components emit faint outer luminescence on focus or hover. Primary interactive items project an emerald aura (`box-shadow: 0 0 0 1px #10B981, 0 0 20px -2px rgba(16, 185, 129, 0.25)`). Invalid inputs or flagged audit warnings emit amber/crimson radiation (`rgba(245, 158, 11, 0.3)`).

## Shapes

The interface balances sharp digital calculation with humanized corner rounding.

- **Base Radius (0.5rem / 8px):** Input containers, nested micro-cards, table selection blocks, and operational list items.
- **Large Radius (1rem / 16px):** Parent cards, standalone widgets, segmented controls, and dialog dialog structures.
- **Extra Large Radius (1.5rem / 24px):** Primary expense modals, floating action bars, and system notifications.
- **Pill (Full Rounding / 9999px):** Status chips, currency flags, category tags, segmented pill toggles, and user avatar rings.

## Components

### Buttons
- **Primary:** Background gradient `linear-gradient(135deg, #10B981 0%, #059669 100%)`, solid white text, subtle inset white top glow `inset 0 1px 0 rgba(255, 255, 255, 0.2)`. On hover: `box-shadow: 0 0 16px rgba(16, 185, 129, 0.4)`. Active state compresses slightly (`transform: scale(0.98)`).
- **Secondary (Glass):** Background `rgba(255, 255, 255, 0.04)`, border `1px solid rgba(255, 255, 255, 0.1)`, text `#E2E8F0`. Hover triggers background `rgba(255, 255, 255, 0.08)` with border color shifting to `#10B981`.
- **Tertiary / Destructive:** Muted dark slate tone with ember hover (`#EF4444`).

### Input Fields & Selects
- Constructed with `background: rgba(11, 15, 23, 0.6)`, `border: 1px solid rgba(255, 255, 255, 0.08)`, and `border-radius: 0.5rem`.
- Inset shadow: `inset 0 2px 4px rgba(0, 0, 0, 0.4)` to generate physical depth.
- **Focus State:** Hairline border illuminates to `#10B981` accompanied by a soft internal glow: `box-shadow: inset 0 0 8px rgba(16, 185, 129, 0.15), 0 0 0 1px #10B981`.

### Micro-Badges & Status Chips
- Pill-shaped (`border-radius: 9999px`), padding `4px 10px`, typography `label-sm`.
- **Approved/Active:** Background `rgba(16, 185, 129, 0.1)`, border `1px solid rgba(16, 185, 129, 0.25)`, text `#34D399`. Features a 6px pulsating emerald LED dot.
- **Pending/Flagged:** Background `rgba(245, 158, 11, 0.1)`, border `1px solid rgba(245, 158, 11, 0.25)`, text `#FBBF24`.

### Pill Toggles & Segmented Controls
- Exterior container: background `rgba(11, 15, 23, 0.7)`, padding `4px`, border `1px solid rgba(255, 255, 255, 0.06)`, radius `9999px`.
- Active Pill: Background `linear-gradient(180deg, #1E293B 0%, #131A26 100%)`, border `1px solid rgba(255, 255, 255, 0.12)`, text `#FFFFFF`, drop shadow `0 2px 6px rgba(0, 0, 0, 0.5)`.

### Expense Modal Architecture
- Built as an ultra-refined command deck. Header displays categorized transaction breadcrumbs alongside an auto-reconcile pill badge.
- Center section hosts a split currency display (`headline-xl` numerical balance with an integrated drop-down currency switcher).
- Receipt attachment zones support drag-and-drop within a dashed border illuminated by Violet (`#8B5CF6`) upon drag hover.