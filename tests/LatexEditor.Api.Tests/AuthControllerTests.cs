using System.Security.Claims;
using LatexEditor.Api.Controllers;
using LatexEditor.Application.DTOs;
using LatexEditor.Core.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NSubstitute;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace LatexEditor.Api.Tests;

public class AuthControllerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);

        _signInManager = Substitute.For<SignInManager<ApplicationUser>>(
            _userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            Substitute.For<Microsoft.Extensions.Options.IOptions<IdentityOptions>>(),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<SignInManager<ApplicationUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<ApplicationUser>>());

        _schemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        _schemeProvider.GetAllSchemesAsync().Returns(
        [
            new AuthenticationScheme("Google", "Google", typeof(DummyAuthHandler)),
            new AuthenticationScheme("GitHub", "GitHub", typeof(DummyAuthHandler)),
            new AuthenticationScheme("Identity.Application", null, typeof(DummyAuthHandler))
        ]);

        _controller = new AuthController(_userManager, _signInManager, _schemeProvider);

        var urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.Action(Arg.Any<UrlActionContext>()).Returns("http://localhost/callback");
        _controller.Url = urlHelper;
    }

    private sealed class DummyAuthHandler : IAuthenticationHandler
    {
        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => Task.CompletedTask;
        public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(AuthenticationProperties? properties) => Task.CompletedTask;
    }

    [Fact]
    public async Task ExternalLogin_UnknownProvider_ReturnsBadRequest()
    {
        var result = await _controller.ExternalLogin("twitter");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ExternalLogin_IdentityScheme_ReturnsBadRequest()
    {
        var result = await _controller.ExternalLogin("Identity.Application");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData("google", "Google")]
    [InlineData("GITHUB", "GitHub")]
    [InlineData("Google", "Google")]
    public async Task ExternalLogin_KnownProviderAnyCase_ChallengesRegisteredScheme(string provider, string expectedScheme)
    {
        _signInManager.ConfigureExternalAuthenticationProperties(expectedScheme, Arg.Any<string>())
            .Returns(new AuthenticationProperties());

        var result = await _controller.ExternalLogin(provider);

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Contains(expectedScheme, challenge.AuthenticationSchemes);
    }

    [Fact]
    public async Task Register_Success_SignsInAndReturnsUser()
    {
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        var result = await _controller.Register(new RegisterDto { Email = "a@b.c", Password = "pw" });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        await _signInManager.Received(1).SignInAsync(
            Arg.Is<ApplicationUser>(u => u.Email == "a@b.c"), false);
    }

    [Fact]
    public async Task Register_Failure_ReturnsBadRequestWithErrors()
    {
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        var result = await _controller.Register(new RegisterDto { Email = "a@b.c", Password = "pw" });

        Assert.IsType<BadRequestObjectResult>(result);
        await _signInManager.DidNotReceive().SignInAsync(Arg.Any<ApplicationUser>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsUser()
    {
        _signInManager.PasswordSignInAsync("a@b.c", "pw", false, false)
            .Returns(SignInResult.Success);
        _userManager.FindByEmailAsync("a@b.c")
            .Returns(new ApplicationUser { Id = "id-1", Email = "a@b.c" });

        var result = await _controller.Login(new LoginDto { Email = "a@b.c", Password = "pw" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        _signInManager.PasswordSignInAsync(Arg.Any<string>(), Arg.Any<string>(), false, false)
            .Returns(SignInResult.Failed);

        var result = await _controller.Login(new LoginDto { Email = "a@b.c", Password = "wrong" });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Logout_SignsOut()
    {
        var result = await _controller.Logout();

        Assert.IsType<NoContentResult>(result);
        await _signInManager.Received(1).SignOutAsync();
    }

    [Fact]
    public async Task Callback_NoExternalLoginInfo_ReturnsUnauthorized()
    {
        _signInManager.GetExternalLoginInfoAsync().Returns((ExternalLoginInfo?)null);

        var result = await _controller.ExternalLoginCallback();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Callback_ExistingLinkedLogin_Redirects()
    {
        var info = CreateExternalLoginInfo("user@example.com");
        _signInManager.GetExternalLoginInfoAsync().Returns(info);
        _signInManager.ExternalLoginSignInAsync("GitHub", "key-1", false)
            .Returns(SignInResult.Success);

        var result = await _controller.ExternalLoginCallback("/home");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/home", redirect.Url);
    }

    [Fact]
    public async Task Callback_NoEmailClaim_ReturnsBadRequest()
    {
        var info = CreateExternalLoginInfo(email: null);
        _signInManager.GetExternalLoginInfoAsync().Returns(info);
        _signInManager.ExternalLoginSignInAsync("GitHub", "key-1", false)
            .Returns(SignInResult.Failed);

        var result = await _controller.ExternalLoginCallback();

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Callback_NewUser_CreatesLinksAndSignsIn()
    {
        var info = CreateExternalLoginInfo("new@example.com");
        _signInManager.GetExternalLoginInfoAsync().Returns(info);
        _signInManager.ExternalLoginSignInAsync("GitHub", "key-1", false)
            .Returns(SignInResult.Failed);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);

        var result = await _controller.ExternalLoginCallback("/home");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/home", redirect.Url);
        await _userManager.Received(1).AddLoginAsync(
            Arg.Is<ApplicationUser>(u => u.Email == "new@example.com"), info);
        await _signInManager.Received(1).SignInAsync(
            Arg.Is<ApplicationUser>(u => u.Email == "new@example.com"), false);
    }

    private static ExternalLoginInfo CreateExternalLoginInfo(string? email)
    {
        var claims = email is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.Email, email) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        return new ExternalLoginInfo(principal, "GitHub", "key-1", "GitHub");
    }
}
