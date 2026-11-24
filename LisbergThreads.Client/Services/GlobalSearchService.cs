using LisbergThreads.Contracts.Customers;
using LisbergThreads.Contracts.Orders;
using LisbergThreads.Contracts.Products;
using System.Net.Http.Json;

namespace LisbergThreads.Client.Services;

public class GlobalSearchService
{
    private readonly HttpClient _http;

    private string _searchText = string.Empty;
    private SearchType _searchType = SearchType.Products;

    private List<ProductDto> _products = new();
    private List<AdminOrderSummaryDto> _orders = new();
    private List<CustomerSummaryDto> _customers = new();

    public GlobalSearchService(HttpClient http)
    {
        _http = http;
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            SearchChanged?.Invoke();
        }
    }

    public SearchType SearchType
    {
        get => _searchType;
        set
        {
            if (_searchType == value)
            {
                return;
            }

            _searchType = value;
            SearchChanged?.Invoke();
        }
    }

    public IEnumerable<ProductDto> ProductSuggestions
    {
        get
        {
            if (SearchType != SearchType.Products ||
                string.IsNullOrWhiteSpace(SearchText))
            {
                return Enumerable.Empty<ProductDto>();
            }

            return _products
                .Where(p =>
                    p.Name.Contains(
                        SearchText,
                        StringComparison.OrdinalIgnoreCase))
                .Take(5);
        }
    }

    public IEnumerable<CustomerSummaryDto> CustomerSuggestions
    {
        get
        {
            if (SearchType != SearchType.Admin ||
                string.IsNullOrWhiteSpace(SearchText))
            {
                return Enumerable.Empty<CustomerSummaryDto>();
            }

            var searchText = SearchText.Trim();

            return _customers
                .Where(c =>
                    c.FirstName.Contains(
                        searchText,
                        StringComparison.OrdinalIgnoreCase) ||

                    c.LastName.Contains(
                        searchText,
                        StringComparison.OrdinalIgnoreCase) ||

                    $"{c.FirstName} {c.LastName}".Contains(
                        searchText,
                        StringComparison.OrdinalIgnoreCase) ||

                    c.Email.Contains(
                        searchText,
                        StringComparison.OrdinalIgnoreCase))
                .Take(5);
        }
    }

    public IEnumerable<AdminOrderSummaryDto> OrderSuggestions
    {
        get
        {
            if (SearchType != SearchType.Admin ||
                string.IsNullOrWhiteSpace(SearchText))
            {
                return Enumerable.Empty<AdminOrderSummaryDto>();
            }

            var searchText = SearchText.Trim();
            var orderNumber = searchText
                .TrimStart('#')
                .Trim()
                .Replace("-", string.Empty);

            return _orders
                .Where(order =>
                    (!string.IsNullOrWhiteSpace(orderNumber) &&
                     order.OrderId.ToString("N").Contains(
                         orderNumber, StringComparison.OrdinalIgnoreCase)) ||
                    order.FirstName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    order.LastName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    $"{order.FirstName} {order.LastName}".Contains(
                        searchText, StringComparison.OrdinalIgnoreCase) ||
                    order.Email.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .Take(5);
        }
    }

    public async Task LoadProductsAsync()
    {
        if (_products.Count > 0)
        {
            return;
        }

        var response =
            await _http.GetFromJsonAsync<List<ProductDto>>(
                "api/products");

        _products = response ?? new List<ProductDto>();
    }

    public async Task LoadAdminResultsAsync(CancellationToken cancellationToken = default)
    {
        ClearAdminResults();

        var customersTask = _http.GetFromJsonAsync<List<CustomerSummaryDto>>(
            "api/customers", cancellationToken);
        var ordersTask = _http.GetFromJsonAsync<List<AdminOrderSummaryDto>>(
            "api/orders", cancellationToken);

        await Task.WhenAll(customersTask, ordersTask);
        cancellationToken.ThrowIfCancellationRequested();

        _customers = await customersTask ?? new List<CustomerSummaryDto>();
        _orders = await ordersTask ?? new List<AdminOrderSummaryDto>();
        SearchChanged?.Invoke();
    }

    public void ClearAdminResults()
    {
        _customers.Clear();
        _orders.Clear();
        SearchChanged?.Invoke();
    }

    public event Action? SearchChanged;
}
