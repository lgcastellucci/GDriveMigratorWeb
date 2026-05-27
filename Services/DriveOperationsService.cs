using Google.Apis.Drive.v3;
using GDriveMigrator.Models;
using Microsoft.Extensions.Logging;
using GFile = Google.Apis.Drive.v3.Data.File;

namespace GDriveMigrator.Services;

public class DriveOperationsService(ILogger<DriveOperationsService> logger)
{
    private const int PageSize = 100;

    // ──────────────────────────────────────────────
    // Listagem
    // ──────────────────────────────────────────────

    /// <summary>Lista todos os arquivos (não pastas) de uma pasta do Drive.</summary>
    public async Task<List<DriveFileInfo>> ListFilesInFolderAsync(DriveService drive, string folderId, CancellationToken ct = default)
    {
        logger.LogInformation("Listando arquivos na pasta: {FolderId}", folderId);

        var result = new List<DriveFileInfo>();
        string? pageToken = null;

        do
        {
            var req = drive.Files.List();
            req.Q = $"'{folderId}' in parents and mimeType != 'application/vnd.google-apps.folder' and trashed = false";
            req.Fields = "nextPageToken, files(id, name, mimeType, size)";
            req.PageSize = PageSize;
            req.PageToken = pageToken;
            req.IncludeItemsFromAllDrives = true;
            req.SupportsAllDrives = true;

            try
            {
                var response = await req.ExecuteAsync(ct);

                foreach (var f in response.Files ?? [])
                {
                    result.Add(new DriveFileInfo
                    {
                        Id = f.Id,
                        Name = f.Name,
                        MimeType = f.MimeType,
                        Size = f.Size
                    });
                }

                pageToken = response.NextPageToken;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao listar arquivos na pasta {FolderId}: {Message}", folderId, ex.Message);
                throw;
            }



        } while (pageToken != null);

        logger.LogInformation("Total de arquivos encontrados: {Count}", result.Count);
        return result;
    }

    // ──────────────────────────────────────────────
    // Download
    // ──────────────────────────────────────────────

    /// <summary>
    /// Faz download do arquivo para um MemoryStream.
    /// Arquivos do Google Workspace (Docs, Sheets, Slides) são exportados como PDF.
    /// Arquivos binários comuns são baixados diretamente.
    /// </summary>
    public async Task<(Stream Content, string FileName, string MimeType)> DownloadFileAsync(
        DriveService drive,
        DriveFileInfo file,
        CancellationToken ct = default)
    {
        var ms = new MemoryStream();

        if (IsGoogleWorkspaceFile(file.MimeType))
        {
            // Exporta como PDF
            var exportMime = GetExportMimeType(file.MimeType!);
            var exportFileName = $"{file.Name}.pdf";

            logger.LogDebug("Exportando arquivo Google Workspace: {Name} → PDF", file.Name);

            var exportReq = drive.Files.Export(file.Id, exportMime);
            await exportReq.DownloadAsync(ms, ct);

            ms.Position = 0;
            return (ms, exportFileName, exportMime);
        }
        else
        {
            logger.LogDebug("Baixando arquivo: {Name}", file.Name);

            var getReq = drive.Files.Get(file.Id);
            getReq.SupportsAllDrives = true;
            await getReq.DownloadAsync(ms, ct);

            ms.Position = 0;
            return (ms, file.Name, file.MimeType ?? "application/octet-stream");
        }
    }

    // ──────────────────────────────────────────────
    // Upload
    // ──────────────────────────────────────────────

    /// <summary>Faz upload de um arquivo para a pasta de destino.</summary>
    public async Task<string> UploadFileAsync(
        DriveService drive,
        Stream content,
        string fileName,
        string mimeType,
        string destinationFolderId,
        CancellationToken ct = default)
    {
        logger.LogDebug("Enviando arquivo: {Name} para pasta {FolderId}", fileName, destinationFolderId);

        var fileMetadata = new GFile
        {
            Name = fileName,
            Parents = [destinationFolderId]
        };

        var uploadReq = drive.Files.Create(fileMetadata, content, mimeType);
        uploadReq.Fields = "id, name";
        uploadReq.SupportsAllDrives = true;

        var uploadResult = await uploadReq.UploadAsync(ct);

        if (uploadResult.Status != Google.Apis.Upload.UploadStatus.Completed)
            throw new Exception($"Falha no upload de '{fileName}': {uploadResult.Exception?.Message}");

        return uploadReq.ResponseBody?.Id
            ?? throw new Exception($"Upload de '{fileName}' concluído mas sem ID retornado.");
    }

    // ──────────────────────────────────────────────
    // Deleção
    // ──────────────────────────────────────────────

    /// <summary>Move o arquivo para a lixeira (soft delete) na conta de origem.</summary>
    public async Task DeleteFileAsync(
        DriveService drive,
        string fileId,
        CancellationToken ct = default)
    {
        logger.LogDebug("Deletando arquivo: {FileId}", fileId);

        var deleteReq = drive.Files.Delete(fileId);
        deleteReq.SupportsAllDrives = true;
        await deleteReq.ExecuteAsync(ct);
    }

    // ──────────────────────────────────────────────
    // Helpers Google Workspace
    // ──────────────────────────────────────────────

    private static bool IsGoogleWorkspaceFile(string? mimeType) =>
        mimeType?.StartsWith("application/vnd.google-apps.") == true &&
        mimeType != "application/vnd.google-apps.folder";

    private static string GetExportMimeType(string googleMime) => googleMime switch
    {
        "application/vnd.google-apps.document" => "application/pdf",
        "application/vnd.google-apps.spreadsheet" => "application/pdf",
        "application/vnd.google-apps.presentation" => "application/pdf",
        "application/vnd.google-apps.drawing" => "application/pdf",
        _ => "application/pdf"
    };
}
