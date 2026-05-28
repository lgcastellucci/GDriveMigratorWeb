using GDriveMigrator.Models;
using GDriveMigrator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GDriveMigrator.Pages;

public class DashboardModel(SessionService sessionService) : PageModel
{
    public AppSession Session => sessionService.Session;
    public bool IsConfigured => sessionService.Session.IsConfigured;

    public void OnGet() { }

    public IActionResult OnPostReset()
    {
        sessionService.Reset();
        return RedirectToPage("/Setup");
    }
}
