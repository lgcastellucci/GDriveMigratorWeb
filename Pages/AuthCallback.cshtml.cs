using GDriveMigrator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GDriveMigrator.Pages;

public class AuthCallbackModel(
    SessionService sessionService,
    AuthService authService) : PageModel
{
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery] string accountKey,
        [FromQuery] string? code,
        [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            ErrorMessage = $"Autorização negada: {error}";
            return Page();
        }

        if (string.IsNullOrEmpty(code))
        {
            ErrorMessage = "Código não recebido do Google.";
            return Page();
        }

        var account = accountKey == "account1"
            ? sessionService.Session.Account1
            : sessionService.Session.Account2;

        var request = HttpContext.Request;
        var redirectUri = $"{request.Scheme}://{request.Host}/AuthCallback?accountKey={accountKey}";

        var ok = await authService.ExchangeCodeAsync(
            account.UserEmail, code, account.TokenFolder, redirectUri);

        if (!ok)
        {
            ErrorMessage = "Falha ao trocar código por token. Tente novamente.";
            return Page();
        }

        account.IsAuthenticated = true;
        sessionService.Save();

        var nextStep = accountKey == "account1" ? "/Setup?step=2" : "/Setup?step=3";
        return Redirect(nextStep);
    }
}
