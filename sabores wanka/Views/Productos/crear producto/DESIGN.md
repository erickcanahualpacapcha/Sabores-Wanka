---
name: Fluent POS & Kitchen Studio
colors:
  surface: '#10131a'
  surface-dim: '#10131a'
  surface-bright: '#363941'
  surface-container-lowest: '#0b0e15'
  surface-container-low: '#191c22'
  surface-container: '#1d2027'
  surface-container-high: '#272a31'
  surface-container-highest: '#32353c'
  on-surface: '#e0e2ec'
  on-surface-variant: '#c0c7d4'
  inverse-surface: '#e0e2ec'
  inverse-on-surface: '#2d3038'
  outline: '#8a919e'
  outline-variant: '#404752'
  surface-tint: '#a3c9ff'
  primary: '#a3c9ff'
  on-primary: '#00315c'
  primary-container: '#0078d4'
  on-primary-container: '#ffffff'
  inverse-primary: '#0060ab'
  secondary: '#7bd0ff'
  on-secondary: '#00354a'
  secondary-container: '#00a6e0'
  on-secondary-container: '#00374d'
  tertiary: '#4edea3'
  on-tertiary: '#003824'
  tertiary-container: '#00875d'
  on-tertiary-container: '#ffffff'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#d3e3ff'
  primary-fixed-dim: '#a3c9ff'
  on-primary-fixed: '#001c39'
  on-primary-fixed-variant: '#004883'
  secondary-fixed: '#c4e7ff'
  secondary-fixed-dim: '#7bd0ff'
  on-secondary-fixed: '#001e2c'
  on-secondary-fixed-variant: '#004c69'
  tertiary-fixed: '#6ffbbe'
  tertiary-fixed-dim: '#4edea3'
  on-tertiary-fixed: '#002113'
  on-tertiary-fixed-variant: '#005236'
  background: '#10131a'
  on-background: '#e0e2ec'
  surface-variant: '#32353c'
typography:
  display:
    fontFamily: Geist
    fontSize: 40px
    fontWeight: '600'
    lineHeight: 52px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Geist
    fontSize: 28px
    fontWeight: '600'
    lineHeight: 36px
    letterSpacing: -0.015em
  headline-lg-mobile:
    fontFamily: Geist
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.01em
  headline-md:
    fontFamily: Geist
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
    letterSpacing: -0.01em
  title-sm:
    fontFamily: Geist
    fontSize: 16px
    fontWeight: '600'
    lineHeight: 22px
    letterSpacing: -0.005em
  body-lg:
    fontFamily: Geist
    fontSize: 15px
    fontWeight: '400'
    lineHeight: 22px
  body-md:
    fontFamily: Geist
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Geist
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
  label-lg:
    fontFamily: Geist
    fontSize: 14px
    fontWeight: '500'
    lineHeight: 20px
  label-md:
    fontFamily: Geist
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
    letterSpacing: 0.01em
  label-caps:
    fontFamily: Geist
    fontSize: 11px
    fontWeight: '600'
    lineHeight: 14px
    letterSpacing: 0.06em
  numeric-metric:
    fontFamily: Geist
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 30px
    letterSpacing: -0.02em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  gutter: 1rem
  gutter-kds: 0.75rem
  margin: 1.5rem
  margin-mobile: 1rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 0.75rem
  space-lg: 1.25rem
  space-xl: 2rem
---

## Brand & Style

This design system translates the precision, quiet power, and natural materials of modern Windows 11 Fluent Design into high-velocity culinary operations. Engineered for restaurant POS terminals, kitchen display systems (KDS), and back-of-house recipe costing matrices, the aesthetic balances functional utility with ambient beauty.

The design movement anchors in **Fluent Modernism** with an evolved Mica-dark philosophy:
- **Calm, High-Precision Focus**: Deep charcoal surfaces reduce eye fatigue in hot, low-light kitchen lines and high-contrast dining room floor terminals.
- **Physicality and Light**: Micro-subtle luminous strokes define interactive geometries without cognitive clutter.
- **Tactile Ergonomics**: Generous tap targets engineered for wet, fast-paced fingers, combined with desktop-grade density for inventory, recipe costing, and ingredient management.
- **Controlled Visual Energy**: Restrained neutral tones dominate 90% of screen real estate, reserving vibrant Fluent Blue accents for critical actions, order status triggers, and active station states.

## Colors

The palette establishes an authentic WinUI 3 Dark Mode hierarchy using a multi-tiered Mica and Acrylic material foundation.

### Palette Architecture
- **Mica Base Canvas (`#1c1f26`)**: The foundational backdrop representing deep desktop layer depths.
- **Layer 1 Surface (`#232730`)**: Elevated structural cards, side panels, and POS grid grouping containers.
- **Layer 2 Card / Interactive Base (`#2b303c`)**: Active inputs, floating modal bodies, list tiles, and kitchen ticket bodies.
- **Primary Accent (`#0078D4`)**: Signature Windows Fluent Blue, dedicated to primary actions, selected tabs, active orders, and confirmed kitchen tickets.
- **Secondary Accent (`#38bdf8`)**: Sky cyan highlight used for focus rings, hover states, inline badges, and ticket dispatch metrics.
- **Status Accents**:
  - Urgent / Delayed Ticket: `#ef4444` (Coral red alert)
  - Fire / Cooking: `#f59e0b` (Amber warning)
  - Station Ready / Completed: `#10b981` (Mint emerald green)
- **Stroke System**:
  - Default Structural Border: `rgba(255, 255, 255, 0.08)`
  - Elevated/Active Stroke: `rgba(255, 255, 255, 0.14)`
  - Control Stroke (Bottom light vector): `rgba(255, 255, 255, 0.04)` to `rgba(255, 255, 255, 0.16)`

## Typography

Typography embodies the technical clarity, clean vertical metrics, and variable weight balance of Windows 11 interface type using Geist.

- **Legibility in Motion**: High x-height ensures order items, allergy warnings, and recipe batch quantities can be parsed from 4 feet away under harsh kitchen pendant lighting.
- **Tabular Numerics**: Applied across all monetary prices, recipe margins, and countdown timers to eliminate visual jitter during real-time updates.
- **Hierarchy Tiers**: Section anchors use `headline-md`, form labels and tab items leverage `label-md`, and structural categories use `label-caps` for clear spatial division.

## Layout & Spacing

The layout model is governed by a strict 4px/8px incremental grid tailored to dual operating modalities: touch-first point of sale and dense desktop configuration.

### Screen Adaptations
- **Kitchen Display Screen (KDS 1080p / 4K)**: Fluid horizontal scroll kanban or dynamic multi-column grid with a compressed `gutter-kds` (12px) to maximize ticket density per viewport.
- **Station Terminal / Tablet (10" to 15")**: Two-column split with a persistent receipt rail (360px fixed) and a responsive menu/modifier grid.
- **Back-Office Management Desktop (1200px+)**: Structured 12-column layout with 24px margins, featuring master-detail views and full-height multi-tab configuration panels.
- **Mobile Manager Mode (&lt;768px)**: Single-column stacked layout with collapsible bottom sheets replacing right-docked detail panels.

## Elevation & Depth

Visual hierarchy combines Fluent Design acrylic tinting with ultra-precise stroke containment:

1. **Layer 0 (Canvas Base - `#1c1f26`)**: Zero elevation, subtle underlying texture.
2. **Layer 1 (Card & Group Container - `#232730`)**: Border stroke `1px solid rgba(255, 255, 255, 0.08)`. Shadow: `0 2px 4px rgba(0, 0, 0, 0.25)`.
3. **Layer 2 (Interactive Floating Surfaces / Flyouts - `#2b303c`)**: Border stroke `1px solid rgba(255, 255, 255, 0.12)`. Shadow: `0 8px 16px rgba(0, 0, 0, 0.35), 0 2px 4px rgba(0, 0, 0, 0.2)`.
4. **Layer 3 (WinUI 3 Dialog Modal & Command Bars)**: Centered acrylic scrim backdrop (`rgba(0, 0, 0, 0.65)` with 20px blur). Card container elevated with `0 24px 48px rgba(0, 0, 0, 0.5)` and a dual-stroke profile (`rgba(255, 255, 255, 0.14)` on top edge, `rgba(255, 255, 255, 0.05)` on bottom edge) mimicking downward directional light.

## Shapes

The geometric framework mirrors Windows 11 Fluent specifications:
- **8px (`rounded-md` / 0.5rem)**: Standard controls, form fields, action buttons, menu tiles, and interactive chips.
- **12px (`rounded-lg` / 0.75rem)**: Primary structural cards, modal dialogs, kitchen ticket panels, and flyout menus.
- **Pill (Fully rounded)**: Live status badges, station indicator tags, and active kitchen timers.

## Components

### Buttons & Interactive Controls
- **Primary Action**: Fluent Blue background (`#0078D4`), top inner highlight `1px solid rgba(255, 255, 255, 0.2)`, white text. On hover: `#1084d8`. On active press: scaled down to 0.98 with subtle brightness drop.
- **Secondary / Standard Button**: Background `#2b303c`, border `1px solid rgba(255, 255, 255, 0.08)`. On hover: border `rgba(255, 255, 255, 0.18)`, background `#323846`.
- **Kitchen Quick-Bump Button**: High-hit-target (minimum 48px height) styled with saturated status colors (green/amber) and bold typography.

### Form Inputs & Numeric Fields
- **Background**: `#1c1f26` inset within a `#2b303c` container.
- **Border**: `1px solid rgba(255, 255, 255, 0.08)`.
- **Focus State**: Accent ring `2px solid #38bdf8` with an inset offset of 1px.
- **Currency/Quantity Steppers**: Integrated large `+` and `-` touch buttons flanking the central tabular numeric input.

### WinUI 3 Navigation Tabs (Pivot Controls)
- Horizontally aligned above configuration modules: *General Info*, *Recipe & Ingredients Cost*, *Kitchen Station*, and *Pricing*.
- **Resting**: Semi-transparent text (`rgba(255, 255, 255, 0.6)`), zero border.
- **Active State**: Solid white text, accented by a 3px rounded Fluent Blue (`#0078D4`) horizontal indicator pill anchored 2px below the text label.

### Cards & Ticket Modules
- **Menu Item Cards**: 12px corner radius, background `#232730`, stroke `rgba(255, 255, 255, 0.08)`. Item name top-left, tabular price bottom-right in bold `#38bdf8`.
- **Kitchen Ticket Cards**: Top header strip colored by destination station (Grill, Cold, Saute, Expo). Internal item rows with line-item strikethrough upon station completion.

### WinUI 3 Dialog Modal
- Centered overlay, max-width 680px (expandable to 920px for detailed multi-column recipe costing).
- Header with integrated dismissal icon and bold category title.
- Footer action strip separated by an ultra-subtle divider (`rgba(255, 255, 255, 0.06)`), containing primary confirmation right-aligned and cancel/secondary left-aligned.