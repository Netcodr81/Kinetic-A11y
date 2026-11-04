---
name: tailwind-css-v4
description: Use Tailwind CSS v4 correctly and consistently in this project. Apply this skill when creating, modifying, reviewing, or refactoring Tailwind CSS classes, CSS files, themes, responsive styles, variants, or component styling.
---

# Tailwind CSS v4

Use this skill whenever working with Tailwind CSS in the project.

## Primary goals

- Use Tailwind CSS v4 syntax and architecture.
- Prefer utility classes for component styling when they improve consistency and maintainability.
- Keep custom CSS intentional and minimal.
- Preserve accessibility, responsive behavior, and existing visual behavior.
- Do not introduce Tailwind CSS v3 configuration patterns into a v4 project.
- Follow the project's existing conventions when they are more specific than this skill.

## Tailwind v4 fundamentals

Tailwind CSS v4 is CSS-first.

Prefer:

```css
@import "tailwindcss";
```

over the v3-era:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```

Do not create or modify a `tailwind.config.js`/`tailwind.config.ts` solely because a v3 tutorial recommends it. In v4, use CSS-first configuration unless the project explicitly requires a compatibility configuration.

Use CSS theme variables when defining project-level design tokens, for example:

```css
@theme {
  --color-brand: #123456;
  --font-display: "Inter", sans-serif;
}
```

Use the project's established theme tokens rather than introducing near-duplicate values.

## Utility-first styling

Prefer Tailwind utilities for:

- spacing
- sizing
- layout
- flexbox
- grid
- typography
- colors
- borders
- radius
- shadows
- responsive behavior
- focus states
- hover/focus/disabled states
- common visibility and positioning behavior

Example:

```razor
<div class="flex flex-col gap-4 rounded-lg border p-6 shadow-sm">
    ...
</div>
```

Do not create a custom CSS class merely to combine a few ordinary Tailwind utilities unless the combination is a repeated semantic pattern that genuinely benefits from abstraction.

Avoid unnecessary abstractions such as:

```css
.my-card {
    display: flex;
    padding: 1rem;
    border-radius: 0.5rem;
}
```

when the same result is clearer as Tailwind utilities.

## Responsive design

Use Tailwind's responsive variants rather than writing custom media queries for ordinary responsive behavior.

Prefer:

```html
<div class="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4">
```

over a custom media-query implementation when the behavior maps naturally to Tailwind utilities.

Use the project's established breakpoints. Do not invent arbitrary breakpoints without a specific design requirement.

Remember that Tailwind's responsive model is mobile-first.

## State and interaction variants

Prefer Tailwind variants for common states:

```html
<button class="hover:bg-gray-100 focus-visible:outline-2 disabled:opacity-50">
```

Use `focus-visible` for keyboard-focused styling where appropriate.

Never remove visible focus indicators merely to achieve a visual design.

For accessibility-sensitive components, verify:

- keyboard focus remains visible
- focus order remains logical
- interactive elements remain distinguishable
- hover-only information is not the sole way to access content
- contrast remains adequate
- disabled and selected states remain understandable

## Arbitrary values

Use arbitrary values sparingly:

```html
<div class="w-[37rem]">
```

They are appropriate when:

- a design genuinely requires a one-off value
- the value cannot reasonably be represented by an existing project token
- removing the value would materially reduce fidelity

Do not use arbitrary values to recreate an undocumented design system.

If the same arbitrary value appears repeatedly, consider defining a theme token or shared component pattern.

## Custom CSS

Custom CSS is appropriate for:

- third-party library integration
- browser-specific behavior not represented by Tailwind
- complex selectors
- pseudo-elements where utility classes are insufficient
- CSS features requiring custom declarations
- component-library overrides
- generated or dynamically controlled markup
- styles that are materially clearer as semantic CSS
- cases where Blazor CSS isolation requires CSS selectors

Before adding custom CSS, check whether an existing Tailwind utility or project token already solves the problem.

## Blazor CSS isolation

This project uses Blazor. Treat component-scoped `.razor.css` files as intentional boundaries.

Do not automatically merge every `.razor.css` file into a global stylesheet.

Be aware of CSS isolation behavior and the use of `::deep` where a component must style descendants rendered outside the isolated scope.

Before changing an isolated stylesheet:

1. Determine which `.razor` component owns it.
2. Identify selectors used by that component.
3. Check whether styles depend on isolation.
4. Check whether `::deep` is involved.
5. Check whether the style is actually shared by multiple components.
6. Only then recommend moving or consolidating it.

## Class ordering

Use the project's existing class-order convention.

If no convention exists, keep classes logically grouped:

1. layout
2. positioning
3. sizing
4. spacing
5. typography
6. colors/background
7. borders/radius/shadow
8. state/interaction
9. responsive variants

Do not perform large class reordering changes unless they improve consistency or are explicitly requested.

## Existing CSS variables and tokens

Before creating a new color, spacing value, radius, font, or breakpoint:

1. Search for an existing project token.
2. Reuse it if appropriate.
3. If no token exists and the value is repeated or foundational, consider adding a theme token.
4. Avoid creating aliases that merely rename an existing value.

## Component reuse

When the same visual pattern appears repeatedly, consider whether it belongs in:

- a reusable Blazor component
- a shared Tailwind pattern
- a CSS component layer
- a theme token

Do not create abstractions solely to eliminate a few repeated classes.

Optimize for maintainability and clarity, not minimum character count.

## Dynamic class construction

Be careful with Tailwind classes constructed dynamically in C#.

Avoid patterns that prevent Tailwind from discovering the complete class names at build time, such as:

```csharp
$"bg-{color}-500"
```

Prefer a complete mapping:

```csharp
var classes = color switch
{
    "red" => "bg-red-500",
    "blue" => "bg-blue-500",
    _ => "bg-gray-500"
};
```

If dynamic class generation is unavoidable, ensure the project's Tailwind v4 source detection/configuration can discover the classes.

## Do not use v3 patterns

Do not introduce these merely because they appear in older documentation:

- `@tailwind base`
- `@tailwind components`
- `@tailwind utilities`
- JavaScript-first theme configuration when CSS-first configuration is sufficient
- v3-specific plugin/configuration patterns
- assumptions that every project needs `tailwind.config.js`

If existing v3-era configuration is encountered, do not migrate it automatically unless migration is part of the requested work.

## Review checklist

When reviewing Tailwind code:

- Is this valid Tailwind v4 syntax?
- Is there already a utility for this?
- Is there already a project token for this value?
- Is custom CSS actually necessary?
- Is the responsive behavior mobile-first and intentional?
- Are hover/focus/disabled states covered?
- Is keyboard focus visible?
- Are arbitrary values justified?
- Are dynamically generated classes discoverable?
- Could this change break Blazor CSS isolation?
- Does the change duplicate an existing component or design pattern?
- Does it preserve WCAG accessibility requirements?

## Output expectations

When making a change, briefly explain non-obvious decisions.

When reviewing code, distinguish:

- **Required** — correctness, accessibility, build/runtime issues.
- **Recommended** — maintainability or consistency improvements.
- **Optional** — stylistic or future cleanup.

Do not make broad unrelated CSS changes while implementing a feature.
