namespace Kinetic_A11y.Web.Common.Services;

public sealed class LoadingService
{
    public event Action? StateChanged;

    public bool IsVisible { get; private set; }

    public string Message { get; private set; } = "Loading...";

    public void Show(string? message = null)
    {
        Message = message ?? "Loading...";
        IsVisible = true;
        StateChanged?.Invoke();
    }

    public void Hide()
    {
        IsVisible = false;
        Message = "Loading...";
        StateChanged?.Invoke();
    }
}
