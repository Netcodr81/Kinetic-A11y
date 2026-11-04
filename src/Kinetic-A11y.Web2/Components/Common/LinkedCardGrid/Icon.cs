namespace Kinetic_A11y.Web2.Web.Components.Common.LinkedCardGrid;

/// <summary>
/// Represents the configuration for an SVG icon rendered inside a card or component.
/// </summary>
/// <remarks>
/// This model is used to configure icons with customizable appearance properties such as color, size, and stroke width.
/// Icons are typically rendered using the LucideIcon component or similar SVG rendering library.
/// </remarks>
public class Icon
{
    /// <summary>
    /// Gets or sets the name of the icon to render. This identifies the icon within the icon library.
    /// </summary>
    public required string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the color of the icon. Defaults to "currentColor" to inherit from parent element.
    /// </summary>
    public string Color { get; init; } = "currentColor";

    /// <summary>
    /// Gets or sets the size of the icon in pixels. Defaults to 24.
    /// </summary>
    public int Size { get; init; } = 24;

    /// <summary>
    /// Gets or sets the stroke width for the icon outline. Defaults to 2.0.
    /// </summary>
    public double StrokeWidth { get; init; } = 2.0;

    /// <summary>
    /// Gets or sets the fill style for the icon. Defaults to "none" for outlined icons.
    /// </summary>
    public string Fill { get; init; } = "none";

    /// <summary>
    /// Gets or sets optional CSS classes applied to the icon element.
    /// </summary>
    public string? Class { get; init; }

    /// <summary>
    /// Gets or sets the accessibility label for the icon, used for screen reader compatibility.
    /// </summary>
    public string? AriaLabel { get; init; }

    /// <summary>
    /// Gets or sets additional HTML attributes to apply to the icon element.
    /// </summary>
    public Dictionary<string, object>? AdditionalAttributes { get; set; }
}
