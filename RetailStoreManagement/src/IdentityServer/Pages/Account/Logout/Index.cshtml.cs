using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServer.Pages.Account.Logout;

public class IndexModel : PageModel
{
    private readonly IIdentityServerInteractionService _interaction;

    public string? LogoutId { get; set; }

    public IndexModel(IIdentityServerInteractionService interaction)
    {
        _interaction = interaction;
    }

    public async Task<IActionResult> OnGet(string? logoutId)
    {
        LogoutId = logoutId;
        return await ProcessLogout(logoutId);
    }

    public async Task<IActionResult> OnPost(string? logoutId)
    {
        return await ProcessLogout(logoutId);
    }

    private async Task<IActionResult> ProcessLogout(string? logoutId)
    {
        await HttpContext.SignOutAsync(
            Duende.IdentityServer.IdentityServerConstants.DefaultCookieAuthenticationScheme
        );

        var logoutRequest = await _interaction.GetLogoutContextAsync(logoutId);

        if (!string.IsNullOrEmpty(logoutRequest?.PostLogoutRedirectUri))
            return Redirect(logoutRequest.PostLogoutRedirectUri);

        return Redirect("~/");
    }
}
