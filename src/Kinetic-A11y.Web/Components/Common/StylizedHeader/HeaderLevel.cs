namespace Kinetic_A11y.Web.Components.Common.StylizedHeader;

/// <summary>
/// Specifies the semantic heading level (h1-h5) for a stylized header component.
/// </summary>
/// <remarks>
/// This enum allows components to render headers with semantic HTML heading tags while decoupling
/// the visual styling from the heading level. Use the appropriate level based on document outline
/// and accessibility requirements, not visual preferences.
/// </remarks>
public enum HeaderLevel
{
    /// <summary>
    /// Top-level heading (HTML h1). Use once per page for the main topic.
    /// </summary>
    H1,

    /// <summary>
    /// Second-level heading (HTML h2). Use for major sections within the page.
    /// </summary>
    H2,

    /// <summary>
    /// Third-level heading (HTML h3). Use for subsections.
    /// </summary>
    H3,

    /// <summary>
    /// Fourth-level heading (HTML h4). Use for sub-subsections.
    /// </summary>
    H4,

    /// <summary>
    /// Fifth-level heading (HTML h5). Use for minor headings.
    /// </summary>
    H5
}
