using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;
using IdentityServer.Services;
using Infrastructure.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Pages.Account.Login;

public class InputModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberLogin { get; set; }
    public string? ReturnUrl { get; set; }
}

[EnableRateLimiting("LoginPolicy")]
public class IndexModel : PageModel
{
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;
    private readonly ApplicationDbContext _dbContext;
    private readonly AccountLockoutService _lockoutService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public IndexModel(
        IIdentityServerInteractionService interaction,
        IEventService events,
        ApplicationDbContext dbContext,
        AccountLockoutService lockoutService)
    {
        _interaction = interaction;
        _events = events;
        _dbContext = dbContext;
        _lockoutService = lockoutService;
    }

    public IActionResult OnGet(string? returnUrl)
    {
        Input.ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        var returnUrl = Input.ReturnUrl ?? "~/";

        // Get the OIDC authorization context. If returnUrl is not part of a valid
        // authorize request AND not a local URL, reject to prevent open-redirect.
        var context = await _interaction.GetAuthorizationContextAsync(Input.ReturnUrl);

        if (context == null && !Url.IsLocalUrl(returnUrl))
        {
            ErrorMessage = "Invalid return URL";
            return Page();
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Vui lòng nhập đầy đủ thông tin";
            return Page();
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == Input.Username);

        if (user != null && await _lockoutService.IsLockedOutAsync(user.Id))
        {
            await _events.RaiseAsync(new UserLoginFailureEvent(
                Input.Username,
                "Account locked",
                clientId: context?.Client.ClientId
            ));
            ErrorMessage = "Tài khoản đã bị khóa. Vui lòng thử lại sau 15 phút.";
            return Page();
        }

        if (user == null || !BCrypt.Net.BCrypt.Verify(Input.Password, user.Password))
        {
            if (user != null)
            {
                var (isLockedOut, _) = await _lockoutService.RecordFailedAttemptAsync(user.Id);
                if (isLockedOut)
                {
                    ErrorMessage = "Tài khoản đã bị khóa do quá nhiều lần đăng nhập sai. Vui lòng thử lại sau 15 phút.";
                }
                else
                {
                    ErrorMessage = "Tên đăng nhập hoặc mật khẩu không đúng";
                }
            }
            else
            {
                ErrorMessage = "Tên đăng nhập hoặc mật khẩu không đúng";
            }

            await _events.RaiseAsync(new UserLoginFailureEvent(
                Input.Username,
                "Invalid credentials",
                clientId: context?.Client.ClientId
            ));
            return Page();
        }

        await _lockoutService.ResetFailedAttemptsAsync(user.Id);

        // Shape claims using JwtClaimTypes constants for forward compatibility
        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.PreferredUserName, user.Username),
            new Claim(JwtClaimTypes.Name, user.FullName ?? user.Username),
            new Claim(JwtClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            authenticationType: Duende.IdentityServer.IdentityServerConstants.DefaultCookieAuthenticationScheme,
            nameType: JwtClaimTypes.Name,
            roleType: JwtClaimTypes.Role
        );

        // Duende expects the user identifier via IdentityServerUser wrapper
        var issuer = new Duende.IdentityServer.IdentityServerUser(user.Id.ToString())
        {
            DisplayName = user.FullName ?? user.Username,
            AdditionalClaims = claims
        };

        var props = new AuthenticationProperties
        {
            IsPersistent = Input.RememberLogin,
            ExpiresUtc = Input.RememberLogin
                ? DateTimeOffset.UtcNow.AddDays(30)
                : DateTimeOffset.UtcNow.AddHours(1)
        };

        await HttpContext.SignInAsync(issuer, props);

        await _events.RaiseAsync(new UserLoginSuccessEvent(
            user.Username,
            user.Id.ToString(),
            user.FullName ?? user.Username,
            clientId: context?.Client.ClientId
        ));

        // If we came from an authorize request, just redirect — IdentityServer
        // picks up the session cookie and continues the flow.
        if (context != null)
            return Redirect(returnUrl);

        if (Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return Redirect("~/");
    }
}
