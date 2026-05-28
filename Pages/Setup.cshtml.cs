using GDriveMigrator.Models;
using GDriveMigrator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GDriveMigrator.Pages;

public class SetupModel(SessionService sessionService) : PageModel
{
    public AppSession Session => sessionService.Session;
    public int Step { get; private set; } = 1;
    public string? ErrorMessage { get; private set; }

    public void OnGet([FromQuery] int step = 1)
    {
        Step = Math.Clamp(step, 1, 3);
    }

    // ── Step 1: salva e-mail da Conta 1 ───────────────────────────────────
    public IActionResult OnPostSaveAccount1(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ErrorMessage = "E-mail obrigatório.";
            Step = 1;
            return Page();
        }

        Session.Account1.UserEmail = email.Trim();
        Session.Account1.TokenFolder = ".tokens/account1";
        sessionService.Save();

        return RedirectToPage("/Auth", new { accountKey = "account1" });
    }

    // ── Step 2: salva e-mail da Conta 2 ───────────────────────────────────
    public IActionResult OnPostSaveAccount2(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ErrorMessage = "E-mail obrigatório.";
            Step = 2;
            return Page();
        }

        Session.Account2.UserEmail = email.Trim();
        Session.Account2.TokenFolder = ".tokens/account2";
        sessionService.Save();

        return RedirectToPage("/Auth", new { accountKey = "account2" });
    }

    // ── Step 3: salva opções ───────────────────────────────────────────────
    public IActionResult OnPostSaveOptions(
        string deleteAfterMove, int intervalMinutes, int maxRetries)
    {
        Session.Options.DeleteAfterMove = deleteAfterMove == "true";
        Session.Options.WorkerIntervalMinutes = Math.Max(1, intervalMinutes);
        Session.Options.MaxRetries = Math.Clamp(maxRetries, 1, 10);
        sessionService.Save();

        // Vai para seleção de pasta da conta 1
        return RedirectToPage("/Folders", new { accountKey = "account1" });
    }
}
