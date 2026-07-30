using LatexEditor.Application.DTOs;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Claims;
using System.Text;

namespace LatexEditor.Api.Controllers;

/// <summary>
/// Cookie-based authentication: email/password register/login/logout plus
/// external OAuth login (Google, GitHub) when provider credentials are configured.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAuthenticationSchemeProvider schemeProvider,
    IEmailSender emailSender,
    IConfiguration configuration) : ControllerBase
{
    private bool RequireConfirmedEmail =>
        configuration.GetValue("Authentication:RequireConfirmedEmail", false);

    /// <summary>Register a new account</summary>
    /// <remarks>
    /// Sends a confirmation email. When <c>Authentication:RequireConfirmedEmail</c> is off
    /// (the default), the user is also signed in immediately. Returns 400 with Identity errors on failure.
    /// </remarks>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var user = new ApplicationUser { UserName = dto.Email, Email = dto.Email };
        var result = await userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(e => e.Description));
        }

        await SendConfirmationEmailAsync(user);

        if (!RequireConfirmedEmail)
            await signInManager.SignInAsync(user, isPersistent: false);

        return Ok(new { user.Id, user.Email });
    }

    /// <summary>Confirm an email address</summary>
    /// <remarks>Validates the token from the confirmation email. Returns 400 for invalid or expired tokens.</remarks>
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return BadRequest("Invalid confirmation link.");

        var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        var result = await userManager.ConfirmEmailAsync(user, decoded);

        return result.Succeeded
            ? Ok(new { message = "Email confirmed." })
            : BadRequest(result.Errors.Select(e => e.Description));
    }

    /// <summary>Resend the confirmation email</summary>
    /// <remarks>Returns 400 if the account does not exist or is already confirmed.</remarks>
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(RegisterDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user is null || user.EmailConfirmed)
            return BadRequest("Cannot resend confirmation for this account.");

        await SendConfirmationEmailAsync(user);
        return NoContent();
    }

    /// <summary>Sign in with email and password</summary>
    /// <remarks>
    /// Returns 401 on invalid credentials. When email confirmation is required,
    /// unconfirmed accounts get 403.
    /// </remarks>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var result = await signInManager.PasswordSignInAsync(
            dto.Email, dto.Password, isPersistent: false, lockoutOnFailure: false);

        if (result.IsNotAllowed)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Email address is not confirmed.");
        }

        if (!result.Succeeded)
        {
            return Unauthorized();
        }

        var user = await userManager.FindByEmailAsync(dto.Email);
        return Ok(new { user!.Id, user.Email });
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = $"{Request.Scheme}://{Request.Host}/api/auth/confirm-email?userId={user.Id}&token={encoded}";

        await emailSender.SendAsync(user.Email!,
            "Confirm your Latex Renderer account",
            $"<p>Welcome! Confirm your email by clicking <a href=\"{link}\">this link</a>.</p>");
    }

    /// <summary>Get the current authenticated user</summary>
    /// <remarks>
    /// Returns the user's id and email from the authentication cookie claims.
    /// If the email claim is missing (common for OAuth-created users), the value
    /// is resolved from the user store.
    /// </remarks>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            var user = await userManager.FindByIdAsync(userId);
            email = user?.Email ?? string.Empty;
        }

        return Ok(new { id = userId, email });
    }

    /// <summary>Sign out</summary>
    /// <remarks>Invalidates the authentication cookie.</remarks>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    /// <summary>Initiate external OAuth login</summary>
    /// <remarks>
    /// Challenges the given provider (<c>Google</c> or <c>GitHub</c>) and redirects to its login page.
    /// Returns 400 if the provider is not a registered external scheme.
    /// </remarks>
    [HttpGet("external-login")]
    public async Task<IActionResult> ExternalLogin(string provider, string returnUrl = "/")
    {
        var schemes = await schemeProvider.GetAllSchemesAsync();
        var scheme = schemes.FirstOrDefault(s =>
            s.Name.Equals(provider, StringComparison.OrdinalIgnoreCase) &&
            s.HandlerType is not null &&
            typeof(IAuthenticationHandler).IsAssignableFrom(s.HandlerType) &&
            !s.Name.StartsWith("Identity.", StringComparison.OrdinalIgnoreCase));

        if (scheme is null)
        {
            return BadRequest($"Unknown external login provider: {provider}");
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { returnUrl })!;
        var properties = signInManager.ConfigureExternalAuthenticationProperties(scheme.Name, redirectUrl);
        return Challenge(properties, scheme.Name);
    }

    /// <summary>External OAuth login callback</summary>
    /// <remarks>
    /// Signs in if the external login is already linked to a local user,
    /// otherwise creates a new user from the provider's email claim and links the login.
    /// Redirects to <paramref name="returnUrl"/> on success.
    /// </remarks>
    [HttpGet("external-login-callback")]
    public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "/")
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return Unauthorized();
        }

        var result = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false);

        if (result.Succeeded)
        {
            return Redirect(returnUrl);
        }

        var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("External login did not provide an email.");
        }

        var user = new ApplicationUser { UserName = email, Email = email };
        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            return BadRequest(createResult.Errors.Select(e => e.Description));
        }

        await userManager.AddLoginAsync(user, info);
        await signInManager.SignInAsync(user, isPersistent: false);

        return Redirect(returnUrl);
    }
}
