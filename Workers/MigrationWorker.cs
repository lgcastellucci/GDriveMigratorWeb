using GDriveMigrator.Models;
using GDriveMigrator.Services;

namespace GDriveMigrator.Workers;

public class MigrationWorker(
    SessionService sessionService,
    AuthService authService,
    DriveOperationsService driveOps,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("Worker iniciado. Intervalo: {Min} minuto(s).",
            sessionService.Session.Options.WorkerIntervalMinutes);

        while (!ct.IsCancellationRequested)
        {
            var session = sessionService.Session;

            if (!session.IsConfigured)
            {
                logger.LogInformation("[Worker] Configuração incompleta — aguardando setup via web.");
            }
            else
            {
                await RunCycleAsync(session, ct);
            }

            var delay = TimeSpan.FromMinutes(session.Options.WorkerIntervalMinutes);
            logger.LogInformation("[Worker] Próxima execução em {Min} minuto(s).", delay.TotalMinutes);

            await Task.Delay(delay, ct);
        }
    }

    private async Task RunCycleAsync(AppSession session, CancellationToken ct)
    {
        logger.LogInformation("[Worker] ── Iniciando ciclo [{Time}] ──", DateTime.Now.ToString("HH:mm:ss"));

        try
        {
            var src = await authService.CreateDriveServiceAsync(
                session.Account1.UserEmail, session.Account1.TokenFolder, ct);
            var dst = await authService.CreateDriveServiceAsync(
                session.Account2.UserEmail, session.Account2.TokenFolder, ct);

            if (src == null || dst == null)
            {
                logger.LogWarning("[Worker] Tokens inválidos — refaça a autenticação no painel web.");
                return;
            }

            var files = await driveOps.ListFilesAsync(src, session.Account1.SourceFolderId!, ct);

            if (files.Count == 0)
            {
                logger.LogInformation("[Worker] Nenhum arquivo encontrado. Nada a fazer.");
                return;
            }

            logger.LogInformation("[Worker] {Count} arquivo(s) encontrado(s). Iniciando migração...", files.Count);

            int ok = 0, fail = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                for (int attempt = 1; attempt <= session.Options.MaxRetries; attempt++)
                {
                    try
                    {
                        var (content, fileName, mimeType) = await driveOps.DownloadAsync(src, file, ct);
                        await using (content)
                        {
                            await driveOps.UploadAsync(
                                dst, content, fileName, mimeType,
                                session.Account2.DestinationFolderId!, ct);
                        }

                        if (session.Options.DeleteAfterMove)
                            await driveOps.DeleteAsync(src, file.Id, ct);

                        logger.LogInformation("[Worker] ✓ {Name}", file.Name);
                        ok++;
                        break;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        if (attempt < session.Options.MaxRetries)
                        {
                            logger.LogWarning("[Worker] Tentativa {A}/{M} falhou para '{Name}': {Msg}",
                                attempt, session.Options.MaxRetries, file.Name, ex.Message);
                            await Task.Delay(
                                TimeSpan.FromSeconds(session.Options.RetryDelaySeconds), ct);
                        }
                        else
                        {
                            logger.LogError("[Worker] ✗ Falha permanente em '{Name}': {Msg}",
                                file.Name, ex.Message);
                            fail++;
                        }
                    }
                }
            }

            sw.Stop();
            logger.LogInformation(
                "[Worker] Ciclo concluído em {Elapsed:mm\\:ss} — Sucesso: {Ok} | Falha: {Fail}",
                sw.Elapsed, ok, fail);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Worker] Erro inesperado no ciclo.");
        }
    }
}
