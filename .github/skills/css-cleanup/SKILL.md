---
name: css-cleanup
description: Audit and safely improve a project's CSS architecture. Inspect CSS, SCSS, Razor CSS isolation files, Tailwind usage, and related stylesheets for duplication, overlap, dead rules, fragmentation, inconsistent tokens, and consolidation opportunities. Recommend changes before making broad CSS refactors.
---

# CSS Cleanup and Consolidation

Use this skill when asked to clean up, consolidate, organize, reduce, review, or refactor project CSS.

The goal is not simply to make CSS files smaller. The goal is to produce a CSS architecture that is easier to understand, maintain, and safely change while preserving behavior and accessibility.

## Core principle

**Analyze first. Refactor second.**

Do not immediately merge files or delete selectors.

First build an understanding of:

- where styles are defined
- which files are global
- which files are component-scoped
- which selectors are reused
- which selectors overlap
- which styles are duplicates
- which styles are likely dead
- which styles belong to third-party libraries
- which styles should remain isolated
- whether Tailwind utilities can replace custom CSS
- whether consolidation would change CSS specificity or load order

## Supported project patterns

Assume the project may contain:

- `.css`
- `.scss`
- `.razor.css`
- Tailwind CSS
- global stylesheets
- component-scoped Blazor CSS
- third-party component overrides
- CSS custom properties
- media queries
- `@supports`
- `@layer`
- pseudo-elements
- pseudo-classes
- `::deep`
- generated markup
- dynamically applied classes

Do not assume that all CSS files can safely be merged.

## Phase 1 — Inventory

Before recommending consolidation, inspect the project CSS structure.

Identify:

- global stylesheets
- page-specific stylesheets
- `.razor.css` files
- theme/token files
- utility files
- component styles
- third-party overrides
- Tailwind entry points
- generated/vendor CSS that should not be edited

Create a concise inventory when the project is large.

Example:

| Category | Files | Notes |
|---|---|---|
| Global | `app.css` | Site-wide styles |
| Theme | `theme.css` | Design tokens |
| Components | `*.razor.css` | Blazor CSS isolation |
| Vendor | `vendor.css` | Do not modify |
| Tailwind | `styles.css` | Tailwind v4 entry point |

## Phase 2 — Classify styles

Classify selectors into:

1. Global foundation
2. Design tokens
3. Layout
4. Typography
5. Components
6. Utilities
7. State/interaction
8. Responsive behavior
9. Accessibility
10. Third-party overrides
11. Temporary/legacy
12. Unknown

Do not consolidate styles merely because they have similar declarations. Their semantic role matters.

## Phase 3 — Detect duplication

Look for:

- identical declarations
- selectors with substantially identical declarations
- repeated color values
- repeated spacing values
- repeated font definitions
- repeated border/radius definitions
- repeated media queries
- duplicate utility classes
- repeated component patterns
- selectors that differ only by a parent scope
- rules that could share a common selector

Example:

```css
.card {
    border: 1px solid var(--border);
    border-radius: 0.5rem;
}

.panel {
    border: 1px solid var(--border);
    border-radius: 0.5rem;
}
```

Potential recommendation:

```css
.card,
.panel {
    border: 1px solid var(--border);
    border-radius: 0.5rem;
}
```

But only recommend this if the selectors have the same semantic purpose and are unlikely to diverge.

## Phase 4 — Detect overlapping selectors

Identify cases such as:

```css
.button { ... }

.primary-button { ... }

.button.primary { ... }

button.primary-button { ... }
```

Look for:

- unnecessary specificity
- competing declarations
- overrides that exist only because of earlier CSS
- repeated selectors across files
- styles that depend on source-order accidents

Prioritize reducing specificity conflicts over merely reducing line count.

## Phase 5 — Detect dead CSS carefully

A selector should NOT be labeled "dead" merely because it was not found by a simple text search.

Before recommending deletion, consider:

- Razor markup
- conditional rendering
- dynamically generated classes
- JavaScript
- third-party components
- CMS/content-generated markup
- `@attributes`
- `class` composition
- CSS pseudo-elements
- accessibility states
- route-dependent markup
- dynamically loaded components

Use confidence levels:

- **High confidence** — safe candidate for removal after verification.
- **Medium confidence** — likely unused but requires runtime/search verification.
- **Low confidence** — do not recommend deletion without additional evidence.

## Phase 6 — Blazor CSS isolation

Treat `.razor.css` as a special case.

Do not combine component-scoped styles simply because selectors are duplicated.

For each candidate:

1. Identify the owning `.razor` component.
2. Determine whether the selector relies on CSS isolation.
3. Check for `::deep`.
4. Check whether the same selector exists in another isolated component.
5. Determine whether the style is truly global or component-specific.
6. Determine whether moving it changes the generated isolation behavior.

Example:

```css
::deep .third-party-widget {
    ...
}
```

Do not blindly move this into a global stylesheet. The purpose of the selector may be specifically to cross the Blazor isolation boundary.

## Phase 7 — Tailwind integration

If the project uses Tailwind CSS v4, identify custom CSS that could reasonably be replaced by Tailwind utilities.

Good candidates:

```css
.flex-column {
    display: flex;
    flex-direction: column;
    gap: 1rem;
}
```

Potential Tailwind replacement:

```html
<div class="flex flex-col gap-4">
```

However, do not recommend converting CSS to Tailwind when:

- the selector represents a meaningful reusable component
- the rule contains complex selectors
- pseudo-elements are involved
- third-party markup requires the selector
- CSS isolation is important
- the style is more readable as semantic CSS
- converting it would create excessively long or difficult markup

The goal is not "zero custom CSS."

## Phase 8 — Consolidation candidates

For every consolidation opportunity, explain:

- source files
- selectors involved
- why they can be combined
- expected benefit
- potential risk
- whether behavior should be verified
- recommended destination

Example:

**Recommended**

`Components/Card.razor.css` and `Components/Panel.razor.css`

Both define the same border/radius token usage. Consider moving the shared visual primitive into `components.css`, while leaving component-specific rules isolated.

**Risk:** Low, provided the shared selector does not depend on CSS isolation.

## Phase 9 — CSS file consolidation

When multiple global CSS files overlap, consider consolidating them into logical layers rather than one giant stylesheet.

A useful structure may be:

```text
Styles/
├── app.css
├── theme.css
├── components.css
├── utilities.css
└── vendor-overrides.css
```

Do not create this structure automatically. Recommend it only when the existing project has enough fragmentation to justify it.

For Tailwind v4, preserve the project's Tailwind entry point and CSS-first configuration.

## Phase 10 — Specificity and source order

Before merging rules from separate files, analyze:

- selector specificity
- source order
- `!important`
- media query ordering
- `@layer`
- cascade layers
- component isolation
- inherited properties

A merge that reduces file count but changes the cascade is not a successful cleanup.

## Phase 11 — Design token cleanup

Look for repeated literals:

```css
color: #123456;
color: rgb(...);
color: var(--some-color);
```

and repeated values for:

- colors
- spacing
- typography
- borders
- radii
- shadows
- breakpoints

If the same conceptual value appears repeatedly, recommend a design token.

Do not replace unrelated values simply because they happen to be numerically identical.

## Phase 12 — Accessibility protection

Never remove or weaken styles that provide:

- visible focus indicators
- keyboard focus states
- sufficient contrast
- error-state distinction
- disabled-state distinction
- selected/current-state indication
- skip links
- screen-reader-only content
- reduced-motion behavior
- touch target sizing

When cleaning CSS, verify that focus-visible behavior remains intact.

For example, do not remove:

```css
:focus-visible {
    outline: 2px solid ...;
}
```

unless an equivalent or stronger mechanism exists.

## Phase 13 — Responsive behavior

When consolidating media queries:

- preserve breakpoint behavior
- preserve ordering
- preserve mobile-first behavior
- do not merge unrelated media queries simply because they use the same breakpoint
- check for conflicting declarations at each breakpoint

If Tailwind v4 can express the behavior cleanly, mention that as an optional modernization opportunity.

## Phase 14 — Prioritize recommendations

Every finding should receive a priority.

### Critical

Potential correctness, accessibility, or production-risk issue.

Examples:

- conflicting focus styles
- accidental override
- broken responsive rule
- unsafe specificity conflict

### High

Clear duplication or architectural problem with a meaningful maintenance benefit.

Examples:

- duplicate global component styles
- multiple competing theme definitions
- repeated large blocks of identical CSS

### Medium

Useful cleanup with moderate benefit.

Examples:

- repeated tokens
- redundant selectors
- fragmented utility styles

### Low

Cosmetic or optional improvement.

Examples:

- ordering
- naming consistency
- minor formatting
- small one-off simplifications

## Required output for a CSS audit

When asked to audit the project's CSS, provide:

### 1. Executive summary

State:

- approximate number of CSS files inspected
- major architectural issues
- biggest consolidation opportunities
- any high-risk areas

### 2. Consolidation opportunities

For each:

- Priority
- Files
- Selectors
- Recommendation
- Risk
- Expected benefit

### 3. Duplicate/overlapping styles

Identify duplicate or substantially overlapping declarations.

### 4. Dead CSS candidates

Separate by confidence:

- High
- Medium
- Low

Do not present uncertain selectors as confirmed dead CSS.

### 5. Tailwind opportunities

Identify custom CSS that could reasonably become Tailwind v4 utilities.

### 6. Blazor isolation considerations

Identify `.razor.css` files that should remain isolated and any candidates for shared/global extraction.

### 7. Recommended target structure

Only propose a new CSS structure if it materially improves maintainability.

### 8. Safe implementation order

Provide a sequence such as:

1. Remove confirmed dead CSS.
2. Consolidate exact duplicates.
3. Extract shared design tokens.
4. Resolve specificity conflicts.
5. Consolidate component patterns.
6. Convert simple candidates to Tailwind utilities.
7. Re-run visual/accessibility verification.

## Change safety rules

Do not:

- delete CSS based solely on naming
- merge all CSS files into one file
- move `.razor.css` rules globally without analyzing isolation
- rewrite third-party CSS
- remove accessibility styles
- change specificity casually
- change visual behavior as part of cleanup
- convert every custom rule into Tailwind
- introduce a new CSS architecture without evidence
- make broad unrelated formatting changes

## When making actual changes

If the user asks for implementation rather than recommendations:

1. Audit first.
2. State the significant findings.
3. Make the smallest safe change set.
4. Preserve behavior.
5. Preserve accessibility.
6. Preserve Blazor CSS isolation semantics.
7. Verify references to changed selectors.
8. Check for build-time Tailwind class discovery issues.
9. Summarize files changed and why.

If a change is potentially behavior-affecting, call it out explicitly.

## Definition of success

A successful CSS cleanup should result in:

- less duplication
- fewer unnecessary overrides
- clearer ownership of styles
- predictable cascade behavior
- reusable design tokens
- sensible Tailwind usage
- preserved Blazor isolation where appropriate
- preserved responsive behavior
- preserved WCAG-relevant states
- fewer maintenance points

**Do not optimize for the fewest CSS files. Optimize for the clearest CSS architecture.**
