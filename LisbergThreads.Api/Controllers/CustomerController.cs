using LisbergThreads.Api.Data;
using LisbergThreads.Contracts.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = "Admin")]
public class CustomersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public CustomersController(
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<CustomerSummaryDto>>> GetAllCustomers()
    {
        var customers = await _userManager.GetUsersInRoleAsync("Customer");

        var result = customers
            .OrderBy(c => c.FirstName)
            .ThenBy(c => c.LastName)
            .Select(c => new CustomerSummaryDto(
                c.Id,
                c.FirstName,
                c.LastName,
                c.Email ?? string.Empty
            ))
            .ToList();

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDetailsDto>> GetCustomer(Guid id)
    {
        var customer = await _userManager.FindByIdAsync(id.ToString());

        if (customer is null)
        {
            return NotFound("Kunden blev ikke fundet.");
        }

        var roles = await _userManager.GetRolesAsync(customer);

        if (!roles.Contains("Customer"))
        {
            return NotFound("Kunden blev ikke fundet.");
        }

        var result = new CustomerDetailsDto(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email ?? string.Empty,
            customer.PhoneNumber ?? string.Empty,
            customer.IsActive,
            customer.CreatedAt
        );

        return Ok(result);
    }
}