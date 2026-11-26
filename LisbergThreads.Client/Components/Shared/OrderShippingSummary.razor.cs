using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Components;

namespace LisbergThreads.Client.Components.Shared;

public partial class OrderShippingSummary : ComponentBase
{
    [Parameter, EditorRequired] public OrderDetailsDto Order { get; set; } = null!;
}
