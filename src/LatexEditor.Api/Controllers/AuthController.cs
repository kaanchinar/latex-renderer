using LatexEditor.Application.DTOs;
using LatexEditor.Core.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
    IAuthenticationSchemeProvider schemeProvider) : ControllerBase
{
    /// <summary>Register a new account</summary>
    /// <remarks>Signs the user in immediately on success. Returns 400 with Identity errors on failure.</remarks>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var user = new ApplicationUser { UserName = dto.Email, Email = dto.Email };
        var result = await userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(e => e.Description));
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return Ok(new { user.Id, user.Email });
    }

    /// <summary>Sign in with email and password</summary>
    /// <remarks>Returns 401 on invalid credentials.</remarks>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var result = await signInManager.PasswordSignInAsync(
            dto.Email, dto.Password, isPersistent: false, lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            return Unauthorized();
        }

        var user = await userManager.FindByEmailAsync(dto.Email);
        return Ok(new { user!.Id, user.Email });
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
