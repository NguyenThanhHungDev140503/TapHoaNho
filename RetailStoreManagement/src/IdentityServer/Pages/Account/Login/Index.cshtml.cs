using Duende.IdentityServer.Services;
using Infrastructure.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IdentityServer.Pages.Account.Login;

public class InputModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberLogin { get; set; }
    public string? ReturnUrl { get; set; }
}

public class IndexModel : PageModel
{
    private readonly IIdentityServerInteractionService _interaction;
    private readonly ApplicationDbContext _dbContext;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public IndexModel(IIdentityServerInteractionService interaction, ApplicationDbContext dbContext)
    {
        _interaction = interaction;
        _dbContext = dbContext;
    }

    public IActionResult OnGet(string? returnUrl)
    {
        Input.ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        var returnUrl = Input.ReturnUrl ?? "~/";

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == Input.Username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(Input.Password, user.Password))
        {
            ErrorMessage = "Tên đăng nhập hoặc mật khẩu không đúng";
            return Page();
        }

        var claims = new List<Claim>
        {
            new Claim("sub", user.Id.ToString()),
            new Claim("username", user.Username),
            new Claim("name", user.FullName ?? string.Empty),
            new Claim("role", user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims,
            Duende.IdentityServer.IdentityServerConstants.DefaultCookieAuthenticationScheme,
            "sub", "role");

        var principal = new ClaimsPrincipal(identity);

        var props = new AuthenticationProperties
        {
            IsPersistent = Input.RememberLogin,
            ExpiresUtc = Input.RememberLogin
                ? DateTimeOffset.UtcNow.AddDays(30)
                : DateTimeOffset.UtcNow.AddHours(1)
        };

        await HttpContext.SignInAsync(
            Duende.IdentityServer.IdentityServerConstants.DefaultCookieAuthenticationScheme,
            principal,
            props
        );

        if (Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return Redirect("~/");
    }
}
