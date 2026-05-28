using GDriveMigrator.Models;
using GDriveMigrator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GDriveMigrator.Pages;

public class AuthModel(
    SessionService sessionService,
    AuthService authService,
    IHttpContextAccessor httpContextAccessor) : PageModel
{
    private const string CallbackPath = "/AuthCallback";

    public AppSession Session => sessionService.Session;
    public string AccountKey { get; private set; } = "account1";
    public bool UseManual { get; private set; } = true;
    public string? AuthUrl { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool AlreadyAuthenticated { get; private set; }

    private AccountConfig GetAccount(string key) =>
        key == "account1" ? Session.Account1 : Session.Account2;

    private string NextStep(string key) =>
        key == "account1" ? "/Setup?step=2" : "/Setup?step=3";

    public async Task OnGetAsync(
        [FromQuery] string accountKey = "account1",
        [FromQuery] bool manual = true)
    {
        AccountKey = accountKey;
        UseManual = manual;

        var account = GetAccount(accountKey);

        if (await authService.HasValidTokenAsync(account.UserEmail, account.TokenFolder))
        {
            account.IsAuthenticated = true;
            sessionService.Save();
            AlreadyAuthenticated = true;
            return;
        }

        // Restaura URL gerada anteriormente se houver
        AuthUrl = HttpContext.Session.GetString($"authUrl_{accountKey}");
    }

    // ── Gera link manual ───────────────────────────────────────────────────
    public async Task<IActionResult> OnPostGenerateManualUrlAsync(string accountKey)
    {
        AccountKey = accountKey;
        UseManual = true;

        var account = GetAccount(accountKey);
        var (url, _) = await authService.BuildAuthUrlAsync(account.TokenFolder);

        // Salva na session HTTP para sobreviver ao redirect
        HttpContext.Session.SetString($"authUrl_{accountKey}", url);
        AuthUrl = url;

        return Page();
    }

    // ── Valida código manual ───────────────────────────────────────────────
    public async Task<IActionResult> OnPostSubmitCodeAsync(string accountKey, string code)
    {
        AccountKey = accountKey;
        UseManual = true;
        AuthUrl = HttpContext.Session.GetString($"authUrl_{accountKey}");

        var account = GetAccount(accountKey);

        var ok = await authService.ExchangeCodeAsync(
            account.UserEmail, code.Trim(), account.TokenFolder);

        if (!ok)
        {
            ErrorMessage = "Código inválido ou expirado. Gere um novo link e tente novamente.";
            return Page();
        }

        account.IsAuthenticated = true;
        sessionService.Save();
        HttpContext.Session.Remove($"authUrl_{accountKey}");

        return Redirect(NextStep(accountKey));
    }

    // ── Inicia fluxo browser (redirect OAuth) ─────────────────────────────
    public async Task<IActionResult> OnPostStartBrowserAsync(string accountKey)
    {
        AccountKey = accountKey;
        UseManual = false;

        var account = GetAccount(accountKey);
        var request = HttpContext.Request;
        var redirectUri = $"{request.Scheme}://{request.Host}{CallbackPath}?accountKey={accountKey}";

        var (url, _) = await authService.BuildAuthUrlAsync(account.TokenFolder, redirectUri);

        return Redirect(url);
    }

    // ── Reauthenticate ─────────────────────────────────────────────────────
    public IActionResult OnPostReauthenticate(string accountKey)
    {
        var account = GetAccount(accountKey);
        account.IsAuthenticated = false;

        // Remove tokens salvos
        var tokenDir = account.TokenFolder;
        if (Directory.Exists(tokenDir))
            Directory.Delete(tokenDir, recursive: true);

        sessionService.Save();
        return RedirectToPage(new { accountKey });
    }
}
