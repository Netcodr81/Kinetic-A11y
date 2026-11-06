namespace Kinetic_A11y.Web.Components.Common.LinkedCardGrid;

/// <summary>
/// Represents the data required to render a linked card inside a <see cref="LinkedCardGrid"/>.
/// </summary>
public sealed record LinkedCardGridItem
{
    /// <summary>
    /// Gets or sets the card heading text.
    /// </summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional supporting text for the card.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets or sets the destination URL for the card.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Gets or sets the link target for the card, such as <c>_blank</c>.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the card should render in a disabled state.
    /// </summary>
    public bool Disabled { get; init; }

    /// <summary>
    /// Gets or sets the optional icon content rendered inside the card.
    /// </summary>
    public Icon? IconContent { get; init; }

    /// <summary>
    /// Gets or sets the background image URL for the card.
    /// </summary>
    public string? BackgroundImageUrl { get; init; }

    /// <summary>
    /// Gets or sets the card heading color.
    /// </summary>
    public string? HeadingColor { get; init; }

    /// <summary>
    /// Gets or sets the card heading hover color.
    /// </summary>
    public string? HeadingHoverColor { get; init; }

    /// <summary>
    /// Gets or sets the card text color.
    /// </summary>
    public string? TextColor { get; init; }

    /// <summary>
    /// Gets or sets the card text hover color.
    /// </summary>
    public string? TextHoverColor { get; init; }

    /// <summary>
    /// Gets or sets the card background color.
    /// </summary>
    public string? BackgroundColor { get; init; }

    /// <summary>
    /// Gets or sets the card icon color.
    /// </summary>
    public string? IconColor { get; init; }

    /// <summary>
    /// Gets or sets the card icon hover color.
    /// </summary>
    public string? IconHoverColor { get; init; }

    /// <summary>
    /// Gets or sets the card border color.
    /// </summary>
    public string? BorderColor { get; init; }
}

