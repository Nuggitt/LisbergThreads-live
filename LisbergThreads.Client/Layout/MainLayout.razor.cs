using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace LisbergThreads.Client.Layout;

public partial class MainLayout
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    private CartDto? cart;

    public async Task LoadCart()
    {
        cart = await Http.GetFromJsonAsync<CartDto>("api/cart");
        StateHasChanged();
    }
}