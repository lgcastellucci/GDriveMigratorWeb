using GDriveMigrator.Models;
using GDriveMigrator.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ─────────────────────────────────────────────────────────────────────────────
// Banner
// ─────────────────────────────────────────────────────────────────────────────
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("""

  ╔══════════════════════════════════════════════════╗
  ║            G D r i v e M i g r a t o r           ║
  ║    Move arquivos entre duas contas do Drive      ║
  ╚══════════════════════════════════════════════════╝

""");
Console.ResetColor();

// ─────────────────────────────────────────────────────────────────────────────
// Dependency Injection
// ─────────────────────────────────────────────────────────────────────────────
var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Warning); // silencia logs internos do Google SDK
    builder.AddSimpleConsole(opts =>
    {
        opts.SingleLine = true;
        opts.TimestampFormat = "HH:mm:ss ";
    });
});

services.AddSingleton<SetupService>();
services.AddSingleton<AuthService>();
services.AddSingleton<DriveOperationsService>();
services.AddSingleton<MigrationOrchestrator>();

var sp = services.BuildServiceProvider();

var setupService = sp.GetRequiredService<SetupService>();
var authService = sp.GetRequiredService<AuthService>();
var orchestrator = sp.GetRequiredService<MigrationOrchestrator>();
var logger = sp.GetRequiredService<ILogger<Program>>();

// ─────────────────────────────────────────────────────────────────────────────
// Ctrl+C
// ─────────────────────────────────────────────────────────────────────────────
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n  Cancelando...");
    Console.ResetColor();
    cts.Cancel();
};

try
{
    // ── 1. Wizard de configuração ─────────────────────────────────────────
    var settings = await setupService.LoadOrSetupAsync(cts.Token);

    // ── 2. Autenticação Conta 1 ───────────────────────────────────────────
    PrintStep("1/4", "Autenticando Conta 1");
    PrintInfo($"     Abrindo browser para: {settings.Account1.UserEmail}");
    PrintInfo("     Faça login, autorize o acesso e volte aqui.");
    Console.WriteLine();

    var sourceDrive = await authService.CreateDriveServiceAsync(settings.Account1, "GDriveMigrator", cts.Token);

    PrintOk("Conta 1 autenticada com sucesso.");

    // ── 3. Autenticação Conta 2 ───────────────────────────────────────────
    Console.WriteLine();
    PrintStep("2/4", "Autenticando Conta 2");
    PrintInfo($"     Abrindo browser para: {settings.Account2.UserEmail}");
    PrintInfo("     Faça login com a segunda conta, autorize e volte aqui.");
    Console.WriteLine();

    var destinationDrive = await authService.CreateDriveServiceAsync(settings.Account2, "GDriveMigrator", cts.Token);

    PrintOk("Conta 2 autenticada com sucesso.");

    // ── 4. Confirmação final ──────────────────────────────────────────────
    Console.WriteLine();
    PrintStep("3/4", "Confirmação");
    Console.WriteLine($"  Mover arquivos de:");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"    {settings.Account1.UserEmail}  →  pasta [{settings.Account1.SourceFolderId}]");
    Console.WriteLine($"        ↓");
    Console.WriteLine($"    {settings.Account2.UserEmail}  →  pasta [{settings.Account2.DestinationFolderId}]");
    Console.ResetColor();
    Console.WriteLine($"  Deletar originais: {(settings.Options.DeleteAfterMove ? "Sim" : "Não")}");
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("  Iniciar migração? (S/n): ");
    Console.ResetColor();

    var confirm = Console.ReadLine()?.Trim().ToLower();
    if (confirm is "n" or "não" or "nao" or "no")
    {
        Console.WriteLine("  Operação cancelada.");
        return 1;
    }

    // ── 5. Migração ───────────────────────────────────────────────────────
    Console.WriteLine();
    PrintStep("4/4", "Migrando arquivos...");

    var result = await orchestrator.RunAsync(sourceDrive, destinationDrive, settings, cts.Token);

    //Aguardar 10 segundos para fechar, assim é possivel ver o resultado final
    Console.WriteLine();
    PrintInfo($"Migração concluída em {result.Elapsed.TotalSeconds:F1} segundos."); 
    Console.WriteLine();

    return result.HasErrors ? 2 : 0;
}
catch (OperationCanceledException)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  Migração interrompida.");
    Console.ResetColor();
    return 3;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n  Erro: {ex.Message}");
    Console.ResetColor();
    logger.LogError(ex, "Erro não tratado");
    return 99;
}

// ─────────────────────────────────────────────────────────────────────────────
// Helpers de output
// ─────────────────────────────────────────────────────────────────────────────
static void PrintStep(string step, string title)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write($"  [{step}] ");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine(title);
    Console.ResetColor();
}

static void PrintInfo(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(text);
    Console.ResetColor();
}

static void PrintOk(string text)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  ✓ {text}");
    Console.ResetColor();
}
