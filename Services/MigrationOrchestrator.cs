using Google.Apis.Drive.v3;
using GDriveMigrator.Models;
using Microsoft.Extensions.Logging;

namespace GDriveMigrator.Services;

public class MigrationOrchestrator(
    DriveOperationsService driveOps,
    ILogger<MigrationOrchestrator> logger)
{
    /// <summary>
    /// Executa a migração completa:
    /// 1. Lista arquivos da pasta de origem (Conta 1)
    /// 2. Para cada arquivo: baixa → faz upload (Conta 2) → deleta origem (se configurado)
    /// </summary>
    public async Task<MigrationResult> RunAsync(
        DriveService sourceDrive,
        DriveService destinationDrive,
        MigrationSettings settings,
        CancellationToken ct = default)
    {
        var sourceFolderId = settings.Account1.SourceFolderId
            ?? throw new InvalidOperationException("SourceFolderId não configurado.");

        var destinationFolderId = settings.Account2.DestinationFolderId
            ?? throw new InvalidOperationException("DestinationFolderId não configurado.");

        var opts = settings.Options;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new MigrationResult();

        // ── 1. Listar arquivos ──────────────────────────────────────────
        PrintSection("Listando arquivos na origem...");
        var files = await driveOps.ListFilesInFolderAsync(sourceDrive, sourceFolderId, ct);

        result.TotalFiles = files.Count;

        if (files.Count == 0)
        {
            logger.LogWarning("Nenhum arquivo encontrado na pasta de origem.");
            result.Elapsed = sw.Elapsed;
            return result;
        }

        PrintSection($"Iniciando migração de {files.Count} arquivo(s)...");
        PrintTableHeader();

        // ── 2. Processar em lotes ───────────────────────────────────────
        var batches = files.Chunk(opts.BatchSize);

        foreach (var batch in batches)
        {
            foreach (var file in batch)
            {
                ct.ThrowIfCancellationRequested();

                var fileIndex = result.Succeeded + result.Failed + result.Skipped + 1;
                var success = await ProcessFileWithRetryAsync(
                    sourceDrive,
                    destinationDrive,
                    file,
                    destinationFolderId,
                    opts,
                    result,
                    fileIndex,
                    ct);

                if (success && opts.LogProgressEveryNFiles > 0 &&
                    fileIndex % opts.LogProgressEveryNFiles == 0)
                {
                    logger.LogInformation("Progresso: {Done}/{Total}", fileIndex, files.Count);
                }
            }
        }

        sw.Stop();
        result.Elapsed = sw.Elapsed;

        PrintSummary(result);
        return result;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Processamento individual com retry
    // ──────────────────────────────────────────────────────────────────────

    private async Task<bool> ProcessFileWithRetryAsync(
        DriveService sourceDrive,
        DriveService destinationDrive,
        DriveFileInfo file,
        string destinationFolderId,
        MigrationOptions opts,
        MigrationResult result,
        int fileIndex,
        CancellationToken ct)
    {
        for (int attempt = 1; attempt <= opts.MaxRetries; attempt++)
        {
            try
            {
                await MoveFileAsync(sourceDrive, destinationDrive, file, destinationFolderId, opts, ct);

                PrintRow(fileIndex, file.Name, file.DisplaySize, "✓ OK", attempt > 1 ? $"(tentativa {attempt})" : "");
                result.Succeeded++;
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt < opts.MaxRetries)
                {
                    logger.LogWarning("Tentativa {Attempt}/{Max} falhou para '{Name}': {Msg}. Aguardando {Delay}s...",
                        attempt, opts.MaxRetries, file.Name, ex.Message, opts.RetryDelaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(opts.RetryDelaySeconds), ct);
                }
                else
                {
                    var errorMsg = $"'{file.Name}': {ex.Message}";
                    PrintRow(fileIndex, file.Name, file.DisplaySize, "✗ ERRO", ex.Message[..Math.Min(40, ex.Message.Length)]);
                    result.Failed++;
                    result.Errors.Add(errorMsg);
                    logger.LogError(ex, "Falha permanente ao migrar '{Name}'", file.Name);
                    return false;
                }
            }
        }

        return false;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Operação atômica: download → upload → delete
    // ──────────────────────────────────────────────────────────────────────

    private async Task MoveFileAsync(
        DriveService sourceDrive,
        DriveService destinationDrive,
        DriveFileInfo file,
        string destinationFolderId,
        MigrationOptions opts,
        CancellationToken ct)
    {
        // 1. Download
        var (content, fileName, mimeType) = await driveOps.DownloadFileAsync(sourceDrive, file, ct);

        await using (content)
        {
            // 2. Upload
            await driveOps.UploadFileAsync(destinationDrive, content, fileName, mimeType, destinationFolderId, ct);
        }

        // 3. Delete original (apenas após upload confirmado)
        if (opts.DeleteAfterMove)
        {
            await driveOps.DeleteFileAsync(sourceDrive, file.Id, ct);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers de output formatado
    // ──────────────────────────────────────────────────────────────────────

    private static void PrintSection(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  ▶  {title}");
        Console.ResetColor();
    }

    private static void PrintTableHeader()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {"#",-5} {"Arquivo",-45} {"Tamanho",10}  {"Status",-10}  Obs");
        Console.WriteLine($"  {new string('─', 85)}");
        Console.ResetColor();
    }

    private static void PrintRow(int idx, string name, string size, string status, string obs)
    {
        var truncName = name.Length > 43 ? name[..40] + "..." : name;
        var color = status.StartsWith("✓") ? ConsoleColor.Green : ConsoleColor.Red;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  {idx,-5}");
        Console.ResetColor();
        Console.Write($" {truncName,-45} {size,10}  ");
        Console.ForegroundColor = color;
        Console.Write($"{status,-10}");
        Console.ResetColor();
        Console.WriteLine($"  {obs}");
    }

    private static void PrintSummary(MigrationResult result)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ╔══════════════════════════════╗");
        Console.WriteLine("  ║        RESUMO FINAL          ║");
        Console.WriteLine("  ╚══════════════════════════════╝");
        Console.ResetColor();

        Console.WriteLine($"  Total de arquivos : {result.TotalFiles}");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Migrados com êxito: {result.Succeeded}");
        Console.ResetColor();

        if (result.Failed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Com falha         : {result.Failed}");
            Console.ResetColor();
        }

        if (result.Skipped > 0)
            Console.WriteLine($"  Ignorados         : {result.Skipped}");

        Console.WriteLine($"  Tempo total       : {result.Elapsed:mm\\:ss\\.fff}");

        if (result.HasErrors)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Erros registrados:");
            foreach (var err in result.Errors)
                Console.WriteLine($"    • {err}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }
}
