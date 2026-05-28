using GDriveMigrator.Models;
using GDriveMigrator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GDriveMigrator.Pages;

public class FoldersModel(
    SessionService sessionService,
    AuthService authService,
    DriveOperationsService driveOps) : PageModel
{
    public AppSession Session => sessionService.Session;
    public string AccountKey { get; private set; } = "account1";
    public string CurrentParentId { get; private set; } = "root";
    public List<DriveFolder> Folders { get; private set; } = [];
    public List<DriveFolder> Breadcrumbs { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    private AccountConfig GetAccount(string key) => key == "account1" ? Session.Account1 : Session.Account2;

    public async Task OnGetAsync([FromQuery] string accountKey = "account1", [FromQuery] string parentId = "root", [FromQuery] string? breadcrumbs = null)
    {
        AccountKey = accountKey;
        CurrentParentId = parentId;
        Breadcrumbs = ParseBreadcrumbs(breadcrumbs);

        var account = GetAccount(accountKey);

        try
        {
            var drive = await authService.CreateDriveServiceAsync(
                account.UserEmail, account.TokenFolder);

            if (drive == null)
            {
                ErrorMessage = "Token inválido. Reautentique a conta.";
                return;
            }

            Folders = await driveOps.ListFoldersAsync(drive, parentId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erro ao listar pastas: {ex.Message}";
        }
    }

    // ── Confirma seleção de pasta ──────────────────────────────────────────
    public IActionResult OnPostSelect(string accountKey, string folderId, string folderName)
    {
        var account = GetAccount(accountKey);

        if (accountKey == "account1")
        {
            account.SourceFolderId = folderId;
            account.SourceFolderName = folderName;
        }
        else
        {
            account.DestinationFolderId = folderId;
            account.DestinationFolderName = folderName;
        }

        sessionService.Save();

        // Conta 1 selecionada → vai selecionar pasta da conta 2
        if (accountKey == "account1")
            return RedirectToPage(new { accountKey = "account2" });

        // Conta 2 selecionada → configuração concluída
        return RedirectToPage("/Dashboard");
    }

    // ── Helpers de breadcrumb ──────────────────────────────────────────────

    /// <summary>Constrói query string com breadcrumb atualizado ao clicar em uma pasta.</summary>
    public string BuildBreadcrumbQuery(DriveFolder folder)
    {
        var list = new List<DriveFolder>(Breadcrumbs) { folder };
        return $"breadcrumbs={Uri.EscapeDataString(SerializeBreadcrumbs(list))}";
    }

    private static List<DriveFolder> ParseBreadcrumbs(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        try
        {
            return raw.Split('|')
                .Select(p => p.Split(':'))
                .Where(p => p.Length == 2)
                .Select(p => new DriveFolder { Id = p[0], Name = Uri.UnescapeDataString(p[1]) })
                .ToList();
        }
        catch { return []; }
    }

    private static string SerializeBreadcrumbs(List<DriveFolder> crumbs) => string.Join("|", crumbs.Select(c => $"{c.Id}:{Uri.EscapeDataString(c.Name)}"));

}
