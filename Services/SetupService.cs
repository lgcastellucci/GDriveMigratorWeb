using System.Text.Json;
using GDriveMigrator.Models;
using Microsoft.Extensions.Logging;

namespace GDriveMigrator.Services;

public class SetupService(ILogger<SetupService> logger)
{
    private const string ConfigFile = "session.json";

    // ──────────────────────────────────────────────────────────────────────
    // Ponto de entrada: retorna settings prontos (carregados ou coletados)
    // ──────────────────────────────────────────────────────────────────────

    public async Task<MigrationSettings> LoadOrSetupAsync(CancellationToken ct = default)
    {
        // Se já existe uma sessão salva, pergunta se quer reutilizar
        if (File.Exists(ConfigFile))
        {
            var saved = TryLoadSaved();
            if (saved != null)
            {
                PrintSection("Sessão anterior encontrada");
                Console.WriteLine($"  Conta 1 : {saved.Account1.UserEmail}");
                Console.WriteLine($"  Conta 2 : {saved.Account2.UserEmail}");
                Console.WriteLine($"  Origem  : {saved.Account1.SourceFolderId}");
                Console.WriteLine($"  Destino : {saved.Account2.DestinationFolderId}");
                Console.WriteLine();

                if (Ask("  Usar essa configuração? (S/n): ", defaultYes: true))
                    return saved;

                Console.WriteLine();
            }
        }

        return await RunSetupWizardAsync(ct);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Wizard passo a passo
    // ──────────────────────────────────────────────────────────────────────

    private async Task<MigrationSettings> RunSetupWizardAsync(CancellationToken ct)
    {
        var settings = new MigrationSettings();

        // ── Conta 1 ────────────────────────────────────────────────────────
        PrintSection("Conta 1 — Origem (de onde os arquivos serão movidos)");

        settings.Account1.UserEmail = ReadRequired("  E-mail da Conta 1: ");
        settings.Account1.CredentialsFile = "credentials.json";
        settings.Account1.TokenFolder = "token_account1";

        PrintHint("O browser será aberto para você fazer login e autorizar o acesso.");
        Console.WriteLine();

        // ── Pasta de origem ────────────────────────────────────────────────
        PrintSection("Pasta de origem (Conta 1)");
        PrintHint("Abra a pasta no Google Drive e copie o ID da URL:");
        PrintHint("  https://drive.google.com/drive/folders/ >>>1A2B3C4D5E6<<<");
        Console.WriteLine();

        settings.Account1.SourceFolderId = ReadRequired("  ID da pasta de origem: ");

        // ── Conta 2 ────────────────────────────────────────────────────────
        PrintSection("Conta 2 — Destino (para onde os arquivos serão enviados)");

        settings.Account2.UserEmail = ReadRequired("  E-mail da Conta 2: ");
        settings.Account2.CredentialsFile = "credentials.json";
        settings.Account2.TokenFolder = "token_account2";

        PrintHint("O browser será aberto novamente para você autorizar a segunda conta.");
        Console.WriteLine();

        // ── Pasta de destino ───────────────────────────────────────────────
        PrintSection("Pasta de destino (Conta 2)");
        PrintHint("Abra a pasta no Google Drive e copie o ID da URL:");
        PrintHint("  https://drive.google.com/drive/folders/ >>>1A2B3C4D5E6<<<");
        Console.WriteLine();

        settings.Account2.DestinationFolderId = ReadRequired("  ID da pasta de destino: ");

        // ── Opções ─────────────────────────────────────────────────────────
        PrintSection("Opções de migração");

        settings.Options.DeleteAfterMove = Ask("  Deletar arquivos da Conta 1 após mover? (S/n): ", defaultYes: true);

        Console.WriteLine();

        // ── Salvar sessão ──────────────────────────────────────────────────
        SaveSession(settings);
        PrintHint("Configuração salva em session.json — próxima execução não precisará digitar novamente.");
        Console.WriteLine();

        await Task.CompletedTask; // async para futuras expansões
        return settings;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Persistência
    // ──────────────────────────────────────────────────────────────────────

    private void SaveSession(MigrationSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
            logger.LogDebug("Sessão salva em {File}", ConfigFile);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Não foi possível salvar a sessão: {Msg}", ex.Message);
        }
    }

    private MigrationSettings? TryLoadSaved()
    {
        try
        {
            var json = File.ReadAllText(ConfigFile);
            return JsonSerializer.Deserialize<MigrationSettings>(json);
        }
        catch
        {
            return null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers de leitura
    // ──────────────────────────────────────────────────────────────────────

    private static string ReadRequired(string prompt)
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(prompt);
            Console.ResetColor();

            var value = Console.ReadLine()?.Trim();

            if (!string.IsNullOrWhiteSpace(value))
                return value;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ✗ Campo obrigatório. Tente novamente.");
            Console.ResetColor();
        }
    }

    private static bool Ask(string prompt, bool defaultYes)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(prompt);
        Console.ResetColor();

        var input = Console.ReadLine()?.Trim().ToLower();

        if (string.IsNullOrWhiteSpace(input))
            return defaultYes;

        return input is "s" or "sim" or "y" or "yes";
    }

    private static void PrintSection(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  ┌─ {title}");
        Console.ResetColor();
    }

    private static void PrintHint(string text)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {text}");
        Console.ResetColor();
    }
}
