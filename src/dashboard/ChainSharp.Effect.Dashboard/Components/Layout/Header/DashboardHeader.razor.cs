using Microsoft.AspNetCore.Components;

namespace ChainSharp.Effect.Dashboard.Components.Layout.Header;

public partial class DashboardHeader
{
    [Parameter]
    public EventCallback OnToggleSidebar { get; set; }

    [Parameter]
    public EventCallback OnToggleTheme { get; set; }

    [Parameter]
    public bool IsDarkMode { get; set; }

    [Parameter]
    public string Title { get; set; } = "ChainSharp Dashboard";
}
