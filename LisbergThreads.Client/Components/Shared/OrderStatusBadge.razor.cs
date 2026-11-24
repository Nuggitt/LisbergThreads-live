using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Components;

namespace LisbergThreads.Client.Components.Shared;

public partial class OrderStatusBadge : ComponentBase
{
    [Parameter, EditorRequired]
    public string Status { get; set; } = string.Empty;

    private string StatusClass => Status switch
    {
        OrderStatuses.Placed => "placed",
        OrderStatuses.Confirmed => "confirmed",
        OrderStatuses.Shipped => "shipped",
        OrderStatuses.Completed => "completed",
        _ => "unknown"
    };
}
