using LatexEditor.Application.DTOs;
using LatexEditor.Core.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LatexEditor.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : ControllerBase
{
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

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("external-login")]
    public IActionResult ExternalLogin(string provider, string returnUrl = "/")
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { returnUrl })!;
        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

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
