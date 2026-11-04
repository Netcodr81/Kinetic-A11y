# Custom Components Guide

The template includes reusable Blazor components for common UI patterns, forms, and the site shell. Component source is under `src/Kinetic_A11y.Web2.Web/Components`. The examples below assume the component is in a Razor page or component in the web project; add the listed `@using` directives when the component namespace is not already imported.

## Component locations

| Folder | Components |
|---|---|
| `Components/Common` | `Alert`, `LinkedCard`, `LinkedCardGrid`, `StylizedHeader`, `ReturnToTopButton`, and `RedirectToUnauthorized` |
| `Components/Forms` | `CIOButton` and `DualListBox<TItem>` |
| `Components/Layout` | Site banner, header, footer, layouts, navigation dropdown, and skip link |
| `Components/Pages` | Not-found and unauthorized pages, plus the standard error and home pages |

## Common components

### Alert

`Common/Alert/Alert.razor` presents a message in an accessible, type-specific status panel. A blank or null `Message` renders nothing.

| Parameter | Type | Default / behavior |
|---|---|---|
| `Message` | `string?` | Message text; required for the alert to render |
| `Type` | `AlertType` | `Info`; options are `Info`, `Success`, `Warning`, and `Error` |
| `Heading` | `string?` | Optional title; otherwise a title is chosen from the alert type |
| `Dismissible` | `bool` | `true`; shows the Dismiss button |
| `OnDismiss` | `EventCallback` | Invoked when Dismiss is clicked; the parent must clear/hide the message if it should disappear |
| `Class` | `string` | Optional additional container CSS classes |

Errors use `role="alert"` and assertive announcements; the other types use a polite status announcement.

```razor
@using Kinetic_A11y.Web2.Web.Components.Common.Alert

<Alert Message="@statusMessage"
       Type="AlertType.Success"
       Heading="Saved"
       OnDismiss="ClearStatus" />
```

### StylizedHeader

`Common/StylizedHeader/StylizedHeader.razor` renders a semantic `h1` through `h5` with the template underline style. Use `HeaderLevel` to match the document outline, not merely the desired visual size.

| Parameter | Type | Default / behavior |
|---|---|---|
| `HeaderText` | `string` | Required heading content |
| `HeaderLevel` | `HeaderLevel` | `H1` (the default enum value) |
| `FontSize` | `string` | `1.99rem;` |
| `BorderColor` | `string` | `#ffc44d` underline color |
| `FontColor` | `string` | `#000000` (currently not applied by the component markup) |
| Unmatched attributes | HTML attributes | Captured and applied to the heading element |

```razor
@using Kinetic_A11y.Web2.Web.Components.Common.StylizedHeader

<StylizedHeader HeaderText="Account settings"
                HeaderLevel="HeaderLevel.H1"
                FontSize="2rem"
                BorderColor="#ffc44d" />
```

### LinkedCard and LinkedCardGrid

`Common/LinkedCard/LinkedCard.razor` renders one linked card. It becomes a link only when it is not disabled and has a nonblank destination; otherwise it renders a non-interactive disabled card. `Href` is preferred over the compatibility alias `LinkUrl`. `Target` is preferred over `LinkTarget`; the default target is `_self`. A `_blank` link receives `rel="noopener noreferrer"`.

| Parameter | Type | Default / behavior |
|---|---|---|
| `Heading` | `string` | Card heading |
| `Text` | `string?` | Optional supporting text |
| `Href` / `LinkUrl` | `string?` | Destination URL |
| `Target` | `string?` | Link target; falls back to `LinkTarget` |
| `LinkTarget` | `string` | `_self` |
| `Disabled` | `bool` | `false`; prevents the card from being a link |
| `IconContent` | `Icon?` | Optional Lucide icon configuration |
| `BackgroundImageUrl` | `string?` | Optional background image URL |
| `HeadingColor`, `HeadingHoverColor` | `string?` | Heading colors; defaults to `#00607f` |
| `TextColor`, `TextHoverColor` | `string?` | Supporting text colors; defaults to `#00607f` |
| `BackgroundColor` | `string?` | Card background; defaults to `#ffffff` |
| `IconColor`, `IconHoverColor` | `string?` | Icon colors; defaults to `#00607f` |
| `BorderColor` | `string?` | Border color; defaults to `#ffc44d` |
| `ContainerClass` | `string?` | Optional class for the card container |
| Unmatched attributes | HTML attributes | Captured on the anchor or disabled container |

The `Icon` model is in `Common/LinkedCardGrid` and supports `Name` (required Lucide icon name), `Color`, `Size` (24), `StrokeWidth` (2), `Fill` (`none`), `Class`, `AriaLabel`, and `AdditionalAttributes`.

`Common/LinkedCardGrid/LinkedCardGrid.razor` lays out `LinkedCardGridItem` records and passes their card settings through to `LinkedCard`. Set `Items` to the collection and optionally change `Columns` (default `3`) or add a wrapper `Class`. `LinkedCardGridItem` supports the card content, URL, target, disabled state, icon, image, and color properties listed above; its URL property is named `Url`.

```razor
@using Kinetic_A11y.Web2.Web.Components.Common.LinkedCardGrid

<LinkedCardGrid Columns="2" Items="@cards" />

@code {
    private readonly LinkedCardGridItem[] cards =
    [
        new() { Heading = "Services", Text = "Browse services", Url = "/services" },
        new() { Heading = "Help", Url = "/help", Disabled = true }
    ];
}
```

### ReturnToTopButton

`Common/ReturnToTopButton/ReturnToTopButton.razor` renders a styled link that smoothly scrolls to the element whose ID is `TargetAnchorId`. Include an element with that ID on the page; the default is `top`. The component imports its colocated JavaScript module on first render.

| Parameter | Type | Default / behavior |
|---|---|---|
| `ButtonText` | `string` | Empty; when empty, an accessible label is generated |
| `TargetAnchorId` | `string` | `top`; a leading `#` is accepted |
| `ButtonVariant` | `ReturnToTopButtonVariant` | `Primary`; options are `Primary`, `Secondary`, and `Outline` |
| `IconLocation` | `ReturnToTopButtonIconLocation` | `Prepend`; options are `Prepend` and `Append` |
| `IconContent` | `RenderFragment?` | Optional icon markup |
| `ShowDivider` | `bool` | `false`; adds a top border and spacing |
| `DividerColor` | `string?` | Optional divider color |
| `MinWidth`, `MaxWidth`, `Width` | `string?` | Optional CSS width values |
| `ContainerClass` | `string?` | Optional wrapper class |

```razor
@using Kinetic_A11y.Web2.Web.Components.Common.ReturnToTopButton

<div id="top"></div>
<ReturnToTopButton ButtonText="Back to top"
                   TargetAnchorId="top"
                   ButtonVariant="ReturnToTopButtonVariant.Outline" />
```

### RedirectToUnauthorized

`Common/Authorization/RedirectToUnauthorized.razor` is used by `Routes.razor` for a route the current user cannot access. It redirects an authenticated user to `/unauthorized`; an unauthenticated user is sent to `/auth/login` with the current path as `returnUrl`. It is an application routing component rather than visible page content.

## Form components

### CIOButton

`Forms/Button/CIOButton.razor` is a button with template styling, busy/disabled handling, and support for either text or child markup.

| Parameter | Type | Default / behavior |
|---|---|---|
| `Text` | `string` | Required fallback button label |
| `OnClick` | `EventCallback` | Click callback |
| `IsBusy` | `bool` | `false`; disables the button and sets `aria-busy` |
| `BusyText` | `string?` | Optional label while busy |
| `BusyContent` | `RenderFragment?` | Optional busy markup; takes precedence over `BusyText` |
| `ChildContent` | `RenderFragment?` | Normal-state content; takes precedence over `Text` |
| `IsDisabled` | `bool` | `false` |
| `CssClass` | `string?` | Additional button classes |
| `ButtonType` | `ButtonType` | `Button`; options are `Button`, `Submit`, and `Reset` |
| Unmatched attributes | HTML attributes | Captured on the native `<button>` |

```razor
@using Kinetic_A11y.Web2.Web.Components.Forms.Button

<CIOButton Text="Save"
           ButtonType="ButtonType.Submit"
           IsBusy="@saving"
           BusyText="Saving..."
           OnClick="SaveAsync" />
```

### DualListBox

`Forms/DualListBox/DualListBox.razor` is a generic, searchable dual-list selector. `Items` is the full collection; `SelectedItems` is the selected subset. Bind `SelectedItems` to receive updates when items are transferred.

| Parameter | Type | Default / behavior |
|---|---|---|
| `Items` | `IEnumerable<TItem>` | Empty collection |
| `SelectedItems` | `IEnumerable<TItem>` | Empty collection |
| `SelectedItemsChanged` | `EventCallback<IEnumerable<TItem>>` | Called with the updated selection |
| `ItemText` | `Func<TItem, string>` | Required display-text selector; also used for searching |
| `AvailableLabel`, `SelectedLabel` | `string` | `Available`, `Selected` |
| `SearchPlaceholder` | `string` | `Search...` |
| `EmptyAvailableMessage`, `EmptySelectedMessage` | `string` | Empty-list messages |
| `AddSelectedLabel`, `RemoveSelectedLabel` | `string` | Accessible labels for transfer buttons |
| Unmatched attributes | HTML attributes | Captured on the outer container |

Users can search both lists, select multiple items, use select-all for the filtered items, transfer using the buttons or double-click, and use arrow keys, Home/End, Shift selection, Space, Enter, and Ctrl/Command+A. The component uses `TItem` equality to track selection, so items should have appropriate value equality when they are reference types.

```razor
@using Kinetic_A11y.Web2.Web.Components.Forms.DualListBox

<DualListBox TItem="string"
             Items="@allRoles"
             @bind-SelectedItems="@selectedRoles"
             ItemText="@(role => role)"
             AvailableLabel="Available roles"
             SelectedLabel="Assigned roles" />

@code {
    private IEnumerable<string> allRoles = ["Reader", "Editor", "Administrator"];
    private IEnumerable<string> selectedRoles = ["Reader"];
}
```

## Layout and navigation components

The components in `Components/Layout` form the default site shell. `MainLayout` renders `Banner`, `Header`, the page body, `Footer`, and shared dialog/portal/loading/error UI. `Routes.razor` uses it as the default authorized route layout. Customize these components to change the template-wide branding or navigation.

| Component | Purpose and customization |
|---|---|
| `Banner` | State-government identity strip. For authenticated users, it shows a user menu and posts logout to `/auth/logout`; the display name is resolved from the identity name, email, name claim, or `User`. |
| `Header` | Responsive site header with logo, site name, and primary/mobile navigation. It currently includes a Home link; update the markup and its corresponding mobile navigation when adding links. |
| `Footer` | State-branded footer with Nebraska.gov, Governor, Policies, social links, and the current UTC year. Update the hard-coded link destinations and labels to suit the application. |
| `SkipToContent` | Accessible skip link. `TargetId` is required and must match a focusable content target; `LinkText` defaults to `Skip to main content`. |
| `MenuDropdown` | Policy-aware dropdown for desktop or mobile navigation. See the API below. |
| `UnauthorizedLayout` | Alternate shell using `UnauthorizedBanner` and `UnauthorizedHeader`, plus the footer and shared providers. |
| `UnauthorizedBanner`, `UnauthorizedHeader` | Reduced banner/header used by the unauthorized layout; customize these separately from the normal shell if needed. |
| `ReconnectModal` | Blazor Server connection/reconnect UI. Keep it alongside the app shell when changing layout behavior. |

### Creating dropdown menus with MenuDropdownItem

`MenuDropdown` accepts `Title`, `Items` (required `IReadOnlyList<MenuDropdownItem>`), and `IsMobile` (default `false`). Each `MenuDropdownItem` describes one link:

| Constructor argument | Required | Meaning |
|---|---|---|
| `Text` | Yes | Visible link label |
| `Href` | Yes | Destination, such as `/admin` or an external URL |
| `Target` | No | Native link target; defaults to `null` (normal same-window navigation). Use `_blank` to open a new tab. |
| `RequiredPolicy` | No | Registered authorization policy name; defaults to `null`. Null, empty, or whitespace means no menu visibility restriction. |

Define a collection in `Header.razor`'s existing `@code` block. The following example includes a public link and an administration link restricted by policy. The destinations and policy name are examples; create the pages and register the policy before using them.

```razor
@using Kinetic_A11y.Web2.Web.Components.Layout.MenuDropdown

@code {
    private readonly IReadOnlyList<MenuDropdownItem> resourceLinks =
    [
        new("Public information", "/information"),
        new("Administration", "/admin", RequiredPolicy: "Administrator")
    ];
}
```

Render the dropdown inside the existing desktop `<ul class="cio__nav-list">`, for example after the Home `<li>`:

```razor
<MenuDropdown Title="Resources" Items="@resourceLinks" />
```

Render the same collection inside the mobile `<ul class="mobile-menu-list">` within `BbResponsiveNavContent`:

```razor
<MenuDropdown Title="Resources" Items="@resourceLinks" IsMobile="true" />
```

The component renders its own `<li>`, so do not wrap it in another `<li>`. Reuse the collection for both versions to keep destinations and permissions consistent. Give each item a distinct `Href` within a dropdown, because the component uses it as the rendering key. Desktop menus use a chevron dropdown; mobile menus use expandable sections with plus/minus icons. The existing header JavaScript handles dropdown interaction.

### Applying authorization to menu items

`RequiredPolicy` is a **policy name**, not a role name. A policy can check authentication, roles, claims, or custom requirements. The component obtains the current user from `AuthenticationStateProvider`, evaluates each named policy with `IAuthorizationService`, and renders protected links through `AuthorizeView`. Unauthorized links are omitted, not merely disabled. If no links are visible, the entire dropdown is omitted.

Register the example policy in `Program.cs` before `builder.Build()`, or add the equivalent registration to `AddWebServices`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Administrator", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Administrator"));
});
```

In this example, the policy and role happen to share the name `Administrator`; they are separate concepts. Set `RequiredPolicy: "Administrator"` on every link that needs this policy. For a claim-based policy, use a requirement such as `RequireClaim("permission", "manage-users")` instead of `RequireRole`, matching claims issued by your identity provider. A referenced policy must be registered; a misspelled or missing policy is a configuration error, not a way to hide a link.

The public template does not currently configure a sign-in scheme or these example policies in `Program.cs`. Configure the application's authentication provider, authentication/authorization middleware, and login/logout endpoints as appropriate before relying on authenticated menus. `AddWebServices` already registers cascading authentication state, and `Routes.razor` supplies it to routed components. These registrations do not themselves sign users in or assign roles.

**Hiding a menu item does not protect its destination or data.** Apply the same policy to the routable Razor page:

```razor
@page "/admin"
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize(Policy = "Administrator")]

<h1>Administration</h1>
```

`Routes.razor` uses `AuthorizeRouteView` to enforce page authorization and `RedirectToUnauthorized` for denied navigation. Also enforce authorization on the server endpoints or services that expose protected operations; do not rely on menu visibility or child-component markup as an access-control boundary.

With the example collection and policy, an anonymous user or a signed-in user without the required role sees only Public information. A signed-in administrator sees both links. To make a link visible to any signed-in user, register a policy using only `RequireAuthenticatedUser()` and use that policy's name for `RequiredPolicy`.

### Skip-to-content navigation

`SkipToContent` should point to the main content target used by the layout:

```razor
<SkipToContent TargetId="main-content-target" />
<a id="main-content-target" class="visually-hidden-focusable">Main content</a>
```

## Pages and routing

`Components/Pages/NotFound.razor` is the `NotFoundPage` configured by `Routes.razor`; it provides a 404 message and a Home action. Update its copy and `NavigateHome` destination to change the not-found experience.

`Components/Pages/Unauthorized.razor` handles `/unauthorized` and `/auth/access-denied`. It uses `UnauthorizedLayout`, allows anonymous access to the error page, and can display a return action when passed a safe local `returnUrl`. The redirect behavior for users who encounter authorization failures is in `Common/Authorization/RedirectToUnauthorized`.

`Error.razor` and `Index.razor` are also in `Components/Pages`; they are the standard error and home pages and can be edited like other application pages.

## Styling and assets

The components use colocated Razor CSS where available and shared styles in `wwwroot/css` (including `custom-components.css`, `layout.css`, and the theme styles). Prefer changing those existing styles or adding component-scoped `.razor.css` styles rather than overriding component markup ad hoc. The header, banner, and footer reference assets under `wwwroot/icons`; retain or replace those assets when rebranding the shell.
