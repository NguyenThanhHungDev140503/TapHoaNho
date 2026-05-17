using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServer.Pages.Account.Logout;

public class IndexModel : PageModel
{
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;

    [BindProperty]
    public string? LogoutId { get; set; }

    public string? PostLogoutRedirectUri { get; set; }
    public bool ShowLogoutPrompt { get; set; } = true;

    public IndexModel(
        IIdentityServerInteractionService interaction,
        IEventService events)
    {
        _interaction = interaction;
        _events = events;
    }

    public async Task<IActionResult> OnGet(string? logoutId)
    {
        LogoutId = logoutId;

        // Duende sets ShowSignoutPrompt=false when logout is part of a valid
        // end_session request — in that case we can skip the confirm page.
        var context = await _interaction.GetLogoutContextAsync(logoutId);
        ShowLogoutPrompt = context?.ShowSignoutPrompt ?? true;

        if (!ShowLogoutPrompt)
            return await PerformLogoutAsync(logoutId);

        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        return await PerformLogoutAsync(LogoutId);
    }

    private async Task<IActionResult> PerformLogoutAsync(string? logoutId)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            logoutId ??= await _interaction.CreateLogoutContextAsync();

            await HttpContext.SignOutAsync();

            await _events.RaiseAsync(new UserLogoutSuccessEvent(
                User.GetSubjectId(),
                User.GetDisplayName()
            ));
        }

        var logout = await _interaction.GetLogoutContextAsync(logoutId);
        PostLogoutRedirectUri = logout?.PostLogoutRedirectUri;

        if (!string.IsNullOrEmpty(PostLogoutRedirectUri))
            return Redirect(PostLogoutRedirectUri);

        return Redirect("~/");
    }
}
