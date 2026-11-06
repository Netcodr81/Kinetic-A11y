namespace Kinetic_A11y.Web.Components.Forms.Button;

/// <summary>
/// Specifies the HTML button type that controls the button's behavior within forms.
/// </summary>
/// <remarks>
/// The button type determines how the button interacts with form submission and reset.
/// Defaults should align with accessibility and user expectation, typically Submit for form buttons.
/// </remarks>
public enum ButtonType
{
    /// <summary>
    /// A standard button that performs actions without submitting or resetting a form.
    /// </summary>
    Button,

    /// <summary>
    /// A button that submits the containing form's data to the server.
    /// </summary>
    Submit,

    /// <summary>
    /// A button that resets all form fields to their default values.
    /// </summary>
    Reset
}
