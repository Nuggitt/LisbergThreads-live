using LisbergThreads.Api.Data;
using LisbergThreads.Api.Services.Auth;
using LisbergThreads.Api.Services.Email;
using LisbergThreads.Contracts.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LisbergThreads.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly JwtTokenService _tokens;
        private readonly PasswordResetEmailSender _passwordResetEmailSender;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            JwtTokenService tokens,
            PasswordResetEmailSender passwordResetEmailSender)
        {
            _userManager = userManager;
            _tokens = tokens;
            _passwordResetEmailSender = passwordResetEmailSender;
        }

        [HttpPost("register")]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var existingUser =
                await _userManager.FindByEmailAsync(request.Email);

            if (existingUser is not null)
            {
                return BadRequest(
                    new RegisterErrorResponse(["DuplicateEmail"]));
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                UserName = request.Email,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result =
                await _userManager.CreateAsync(
                    user,
                    request.Password);

            if (!result.Succeeded)
            {
                return BadRequest(new RegisterErrorResponse(
                    result.Errors.Select(error => error.Code).Distinct().ToArray()));
            }

            await _userManager.AddToRoleAsync(
                user,
                "Customer");

            return Ok();
        }

        [HttpPost("login")]
        [EnableRateLimiting("authentication")]
        public async Task<ActionResult<LoginResponse>> Login(
            LoginRequest request)
        {
            var user =
                await _userManager.FindByEmailAsync(
                    request.Email);

            if (user is null || !user.IsActive)
            {
                return Unauthorized();
            }

            var passwordValid =
                await _userManager.CheckPasswordAsync(
                    user,
                    request.Password);

            if (!passwordValid)
            {
                return Unauthorized();
            }

            return Ok(await _tokens.CreateLoginResponseAsync(user));
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<CurrentUserDto>> Me()
        {
            var user = await GetActiveUserAsync();
            if (user is null)
            {
                return Unauthorized();
            }

            return Ok(new CurrentUserDto(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email ?? string.Empty,
                user.PhoneNumber ?? string.Empty
            ));
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<ActionResult<LoginResponse>> UpdateProfile(UpdateProfileRequest request)
        {
            var user = await GetActiveUserAsync();
            if (user is null)
            {
                return Unauthorized();
            }

            var email = request.Email.Trim();
            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                user.EmailConfirmed = false;
            }
            if (user.PhoneNumber != request.PhoneNumber)
            {
                user.PhoneNumberConfirmed = false;
            }

            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.Email = email;
            user.UserName = email;
            user.PhoneNumber = request.PhoneNumber;

            IdentityResult result;
            try
            {
                // Email, user name and profile are saved together; Identity checks concurrency.
                result = await _userManager.UpdateAsync(user);
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // A concurrent profile update can win the unique user-name index.
                return BadRequest(new AccountErrorResponse(["DuplicateEmail", "DuplicateUserName"]));
            }

            if (!result.Succeeded)
            {
                return AccountErrors(result);
            }

            return Ok(await _tokens.CreateLoginResponseAsync(user));
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
        {
            var user = await GetActiveUserAsync();
            if (user is null)
            {
                return Unauthorized();
            }

            var result = await _userManager.ChangePasswordAsync(
                user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                return AccountErrors(result);
            }

            return NoContent();
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
        {
            // Check configuration before looking up any account so 503 cannot reveal existence.
            if (!_passwordResetEmailSender.IsConfigured)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new AccountErrorResponse(["PasswordResetUnavailable"]));
            }

            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user is not null && user.IsActive)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                await _passwordResetEmailSender.SendAsync(
                    user.Email!, token, HttpContext.RequestAborted);
            }

            // Unknown/inactive accounts and SMTP failures deliberately have the same response.
            return NoContent();
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting("authentication")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user is null || !user.IsActive)
            {
                return BadRequest(new AccountErrorResponse(["InvalidToken"]));
            }

            var result = await _userManager.ResetPasswordAsync(
                user, request.Token, request.NewPassword);
            if (!result.Succeeded)
            {
                return AccountErrors(result);
            }

            return NoContent();
        }

        private async Task<ApplicationUser?> GetActiveUserAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user is { IsActive: true } ? user : null;
        }

        private BadRequestObjectResult AccountErrors(IdentityResult result)
        {
            return BadRequest(new AccountErrorResponse(
                result.Errors.Select(error => error.Code).Distinct().ToArray()));
        }
    }
}
