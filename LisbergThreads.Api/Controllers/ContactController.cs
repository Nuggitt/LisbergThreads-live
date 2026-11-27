using System.Net.Mail;
using LisbergThreads.Api.Services.Email;
using LisbergThreads.Contracts.Contact;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/contact")]
public class ContactController(ContactEmailSender sender, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public ContactInfoDto GetInfo() => new(sender.IsConfigured, configuration["Contact:ResponseTime"]);

    [HttpPost]
    [EnableRateLimiting("contact")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Send(ContactRequestDto request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Website)) return BadRequest();
        if (!MailAddress.TryCreate(request.Email.Trim(), out var replyTo)
            || replyTo.Address != request.Email.Trim()) return BadRequest();
        if (!sender.IsConfigured) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (!await sender.SendAsync(request, cancellationToken))
            return StatusCode(StatusCodes.Status502BadGateway);
        return NoContent();
    }
}
