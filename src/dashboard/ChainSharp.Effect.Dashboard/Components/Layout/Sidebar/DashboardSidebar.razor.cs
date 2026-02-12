using Microsoft.AspNetCore.Components;

namespace ChainSharp.Effect.Dashboard.Components.Layout.Sidebar;

public partial class DashboardSidebar
{
    [Parameter]
    public bool Expanded { get; set; } = true;

    private bool _dataExpanded = true;
}
