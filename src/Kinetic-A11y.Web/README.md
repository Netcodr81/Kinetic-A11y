# Kinetic_A11y.Web2

A .NET 10 Blazor Web App configured for InteractiveServer used for CIO Public facing websites

## Quick Start (for New Developers)

### 1) Prerequisites

Install these tools first:

- **.NET SDK 10**
- **Node.js + npm** (required for Tailwind CSS build)
- **SQL Server** (local or remote)
- **Git**

Optional but helpful:

- **Visual Studio 2026** (or newer)

From the repository root:

```powershell
dotnet restore Kinetic_A11y.Web2.slnx
```

From the `src/Kinetic_A11y.Web2.Web` directory, install front-end dependencies:

```powershell
npm install
```

Run the web app:

```powershell
dotnet run --project src/Kinetic_A11y.Web2.Web
```

## First-Run Configuration

1. Set `ConnectionStrings:DefaultConnection` in `src/Kinetic_A11y.Web2.Web/appsettings.Development.json`.
2. If needed, restore and apply database tooling/migrations from `src/Kinetic_A11y.Web2.Web`.
3. Configure any environment-specific secrets via user secrets or environment variables.

## Technologies Used

### Backend / App Platform

- **ASP.NET Core (.NET 10)**
- **Blazor Web App** (Razor Components + Interactive Server mode)
- **Entity Framework Core 10**
- **SQL Server**

### UI / Front-End

- **Blazor components (.razor)**
- **Tailwind CSS 4** (`@tailwindcss/cli`)
- **BlazorBlueprint.Components**
- **Blazor.Sonner** (toast notifications)

### Supporting Libraries

- **Serilog** (console, Seq, SQL sinks)
- **FusionCache**
- **FluentValidation**
- **Microsoft Playwright** (PDF generation flow)
- **Ardalis.Result / Ardalis.SmartEnum**

---

## Architecture and Folder Structure

This codebase follows a **feature-first / vertical-slice style** while still using shared infrastructure and common utilities.

```text
src/
  Kinetic_A11y.Web2.Web/
    Features/
        <FeatureName>/       # feature-specific domain, UI, and behavior
            Components/         # small reusable UI components for this feature
            Pages/              # Razor pages for this feature
            DomainModels/       # domain entities and business rules
            FormModels/         # form input models (if needed)

    Components/           # shared app shell/layout/pages/utilities
    Infrastructure/       # EF Core DbContext, entity configurations, seeding
    Common/               # shared abstractions, options, extensions, PDF services
    Repositories/         # data access abstractions/implementations
    Migrations/           # EF Core migrations
    wwwroot/              # static assets (css/js/icons/images)

```

### How to think about responsibilities

- **Features/**: business behavior and UI for specific capabilities
- **DomainModels/**: domain entities and business rules
- **Infrastructure/**: persistence and framework-specific setup
- **Common/**: reusable cross-feature support code
- **Components/**: shared UI building blocks and app layout

---

## Useful Development Notes

### Tailwind CSS workflow

The project runs a Tailwind build before normal .NET build/publish.

Manual commands (from `src/Kinetic_A11y.Web2.Web`):

```powershell
npm run tailwind-build
npm run tailwind-watch (use this in development for auto-rebuild on changes)
```

### Logging

Serilog is configured via `appsettings*.json`.

- Development logs can go to Console
---

## First Contribution Checklist

If you are new to the project, use this checklist:

1. Build and run the app locally
2. Confirm you can complete the assessment wizard
3. Run unit tests
4. Read one feature end-to-end (`Features/Assessment` is the best starting point)
5. Make a small change with a focused PR

Good first tasks:

- Add or adjust validation rules
- Add unit tests for domain behavior
- Improve component styling in one feature page
- Add a small reusable UI component

---

## Troubleshooting

### App fails to start with DB errors

- Check `DefaultConnection` connection string
- Verify SQL Server is reachable
- Run `dotnet ef database update`

### Tailwind styles are missing

- Run `npm ci` in `src/CIOBlazorPublicTemplate.Web`
- Run `npm run tailwind-build`
- Rebuild the solution

### `dotnet ef` command not found

- Run `dotnet tool restore` in `src/CIOBlazorPublicTemplate.Web`

---

## Where to Start Reading Code

For the fastest onboarding path:

1. `src/Kinetic_A11y.Web2.Web/Program.cs`
2. `src/Kinetic_A11y.Web2.Web/ServiceConfiguration.cs`
3. `src/Kinetic_A11y.Web2.Web/Infrastructure/AppDbContext.cs`

This gives you the startup flow, dependency wiring, UI flow, persistence, and tests in a practical order.

---

## How to Create Your First Feature

Use this pattern so your work stays consistent with the existing architecture.

### Step 1: Create a feature folder

Inside `src/Kinetic_A11y.Web2.Web/Features/`, add a folder for your capability.

Example:

- `Features/Notifications/`

Then add subfolders similar to existing slices:

- `Components/`
- `Pages/`
- `DomainModels/`
- `FormModels/` (if needed)

### Step 2: Add domain model(s) first

Create domain classes that represent business concepts and rules.

Keep domain logic in these classes (not in Razor markup). Keep models explicit and focused.

### Step 3: Add UI page and components

Create a page in `Pages/` and small reusable components in `Components/`.

Follow the current approach:

- Keep components small
- Keep state local when possible
- Put business behavior in domain/application code, not UI markup

### Step 4: Register dependencies

If your feature needs services/repositories, register them in:

- `src/Kinetic_A11y.Web2.Web/ServiceConfiguration.cs`

Use explicit registrations and avoid unnecessary abstractions.

### Step 5: Add persistence (if required)

If the feature needs database storage:

1. Add/update entities/configuration in `Infrastructure/Configurations/`
2. Update `AppDbContext`
3. Create an EF migration
4. Apply migration locally

### Step 6: Verify end-to-end locally

Before opening a PR:

- Build the solution
- Run tests
- Run the app and click through your new feature flow

---

### Definition of Done (Quick Check)

A feature is ready when:

- Folder structure matches feature-first conventions
- Business logic is not buried in Razor markup
- Dependencies are registered cleanly
- Migrations are included (if persistence changed)
- README/docs are updated when behavior changes
