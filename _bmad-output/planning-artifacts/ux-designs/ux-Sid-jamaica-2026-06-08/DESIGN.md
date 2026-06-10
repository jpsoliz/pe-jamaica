---
name: "NLA Parcel Workflow Add-in"
status: final
description: "ArcGIS Pro add-in visual design for guided cadastral parcel extraction, review, validation, and local output creation."
sources:
  - _bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/prd.md
  - _bmad-output/planning-artifacts/prds/prd-Sid-jamaica-2026-06-08/addendum.md
updated: 2026-06-08
colors:
  surface-base: '#F7F8FA'
  surface-panel: '#FFFFFF'
  surface-subtle: '#EEF2F4'
  surface-inset: '#F3F6F7'
  text-primary: '#1F2933'
  text-secondary: '#52616B'
  text-muted: '#75838F'
  border-default: '#C9D2D8'
  border-strong: '#9AA8B2'
  primary: '#1F6B75'
  primary-foreground: '#FFFFFF'
  accent: '#5B7C99'
  accent-foreground: '#FFFFFF'
  success: '#2F7D4F'
  warning: '#B7791F'
  danger: '#B42318'
  info: '#2B6CB0'
  focus-ring: '#2F80A0'
typography:
  title:
    fontFamily: 'Segoe UI'
    fontSize: 16px
    fontWeight: '600'
    lineHeight: '1.35'
    letterSpacing: '0'
  section:
    fontFamily: 'Segoe UI'
    fontSize: 13px
    fontWeight: '600'
    lineHeight: '1.35'
    letterSpacing: '0'
  body:
    fontFamily: 'Segoe UI'
    fontSize: 12px
    fontWeight: '400'
    lineHeight: '1.4'
    letterSpacing: '0'
  label:
    fontFamily: 'Segoe UI'
    fontSize: 11px
    fontWeight: '600'
    lineHeight: '1.3'
    letterSpacing: '0'
  caption:
    fontFamily: 'Segoe UI'
    fontSize: 11px
    fontWeight: '400'
    lineHeight: '1.3'
    letterSpacing: '0'
rounded:
  sm: 2px
  md: 4px
  lg: 6px
spacing:
  '1': 4px
  '2': 8px
  '3': 12px
  '4': 16px
  '5': 20px
  pane-width-min: 360px
  pane-width-target: 420px
  table-row-height: 30px
components:
  step-nav-item:
    active-background: '{colors.surface-panel}'
    active-border: '{colors.primary}'
    complete-color: '{colors.success}'
    blocked-color: '{colors.danger}'
    radius: '{rounded.md}'
  primary-button:
    background: '{colors.primary}'
    foreground: '{colors.primary-foreground}'
    radius: '{rounded.md}'
  secondary-button:
    background: '{colors.surface-panel}'
    foreground: '{colors.text-primary}'
    border: '{colors.border-default}'
    radius: '{rounded.md}'
  status-pill:
    radius: '{rounded.sm}'
    success: '{colors.success}'
    warning: '{colors.warning}'
    danger: '{colors.danger}'
    info: '{colors.info}'
  review-table:
    row-height: '{spacing.table-row-height}'
    border: '{colors.border-default}'
    selected-background: '{colors.surface-subtle}'
  validation-finding:
    critical: '{colors.danger}'
    warning: '{colors.warning}'
    passed: '{colors.success}'
---

## Brand & Style

The add-in should feel like a serious ArcGIS Pro production tool: compact, precise, and steady. It is not a consumer app and not a landing page. The visual posture is "cadastral operations desk": dense but readable, restrained color, clear state signals, and no decorative illustration.

The design should sit comfortably beside ArcGIS Pro panels. Use familiar desktop tool proportions, small type, visible separators, compact buttons, tabular review surfaces, and persistent status. The user should feel that the add-in is helping them control a technical workflow, not selling them a feature.

## Colors

- **Primary teal (`{colors.primary}`)** is used for the current step, primary actions, and confirmed workflow progress. It should appear sparingly.
- **Slate accent (`{colors.accent}`)** is used for neutral technical emphasis, such as selected rows, metadata headers, or secondary active states.
- **Success, warning, danger, and info** are reserved for processing status, validation severity, and manual-process routing. Do not use them decoratively.
- **Surfaces** stay light and neutral so map content, DWG layers, and tabular data remain the visual focus.

## Typography

Use Segoe UI to match Windows desktop and ArcGIS Pro expectations. Typography is compact:

- `{typography.title}` for pane title and current transaction.
- `{typography.section}` for step headers and table group headings.
- `{typography.body}` for field values, table cells, and explanatory microcopy.
- `{typography.label}` for form labels and metadata labels.
- `{typography.caption}` for timestamps, paths, and diagnostic details.

Do not use hero-scale typography. This is a dock pane, and type should support scanning.

## Layout & Spacing

The dock pane target width is `{spacing.pane-width-target}` with a minimum usable width of `{spacing.pane-width-min}`. Use a vertical workflow layout:

1. Header: transaction ID, status, and current score/status.
2. Step navigator: Intake, Preflight, Review, Validation, Outputs, Sync Readiness.
3. Current step content.
4. Sticky action bar for primary/secondary actions.
5. Compact run log or status strip.

Spacing uses 4px increments. Data-dense areas may use `{spacing.2}` padding; step sections use `{spacing.3}` or `{spacing.4}`.

## Elevation & Depth

Use borders and tonal surface changes instead of shadows. Shadows should be limited to dialogs, popovers, and menus where ArcGIS Pro/WPF already uses elevation. Nested cards should be avoided.

## Shapes

Corners are tight: `{rounded.sm}` to `{rounded.lg}`. Use rectangular controls with small radius. Avoid pill-shaped buttons except for status badges where compact labeling helps scanability.

## Components

- **Step navigator**: vertical compact list. Active step uses `{components.step-nav-item.active-border}` left border. Complete steps show success state. Blocked steps show danger state and prevent downstream actions.
- **File picker row**: label, path/status, browse button, and remove/reselect action. Long paths truncate middle-first and expose full path on tooltip.
- **Preflight checklist**: grouped rows with severity icon, result label, evidence count, and details expander.
- **Review table**: fixed row height `{components.review-table.row-height}`. Missing values, edited values, and low-confidence rows must have distinct state indicators.
- **Validation finding**: severity color bar, rule ID, title, status, evidence link, and recommended action.
- **Manual Process decision panel**: high-salience but calm. It must make the user decision explicit without feeling like an emergency alert unless the result is zero usable extraction.
- **Output summary**: artifact list with generated feature classes, GeoJSON, GDB, reports, and logs.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Keep controls compact and aligned to ArcGIS Pro desktop expectations | Use marketing page patterns, oversized cards, or decorative backgrounds |
| Use severity color only for real validation and processing states | Use success/warning/danger as theme colors |
| Show paths, counts, timestamps, and evidence links where useful | Hide technical details behind vague "done" messages |
| Make review and manual-process decisions visually explicit | Let users infer whether data is approved or blocked |
| Keep line lengths and tables readable inside a narrow pane | Force wide data tables without horizontal strategy |
