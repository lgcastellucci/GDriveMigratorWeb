using Google.Apis.Drive.v3;
using GDriveMigrator.Models;
using GFile = Google.Apis.Drive.v3.Data.File;

namespace GDriveMigrator.Services;

public class DriveOperationsService
{
    // ── Listar subpastas ──────────────────────────────────────────────────

    public async Task<List<DriveFolder>> ListFoldersAsync(
        DriveService drive,
        string parentId = "root",
        CancellationToken ct = default)
    {
        var result = new List<DriveFolder>();
        string? pageToken = null;

        do
        {
            var req = drive.Files.List();
            req.Q = $"'{parentId}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
            req.Fields = "nextPageToken, files(id, name, parents)";
            req.PageSize = 100;
            req.PageToken = pageToken;
            req.SupportsAllDrives = true;
            req.IncludeItemsFromAllDrives = true;
            req.OrderBy = "name";

            var resp = await req.ExecuteAsync(ct);
            foreach (var f in resp.Files ?? [])
                result.Add(new DriveFolder { Id = f.Id, Name = f.Name, ParentId = parentId });

            pageToken = resp.NextPageToken;
        } while (pageToken != null);

        return result;
    }

    // ── Listar arquivos (para migração) ───────────────────────────────────

    public async Task<List<DriveFileInfo>> ListFilesAsync(
        DriveService drive,
        string folderId,
        CancellationToken ct = default)
    {
        var result = new List<DriveFileInfo>();
        string? pageToken = null;

        do
        {
            var req = drive.Files.List();
            req.Q = $"'{folderId}' in parents and mimeType != 'application/vnd.google-apps.folder' and trashed = false";
            req.Fields = "nextPageToken, files(id, name, mimeType, size)";
            req.PageSize = 100;
            req.PageToken = pageToken;
            req.SupportsAllDrives = true;
            req.IncludeItemsFromAllDrives = true;

            var resp = await req.ExecuteAsync(ct);
            foreach (var f in resp.Files ?? [])
                result.Add(new DriveFileInfo { Id = f.Id, Name = f.Name, MimeType = f.MimeType, Size = f.Size });

            pageToken = resp.NextPageToken;
        } while (pageToken != null);

        return result;
    }

    // ── Download ──────────────────────────────────────────────────────────

    public async Task<(Stream Content, string FileName, string MimeType)> DownloadAsync(
        DriveService drive, DriveFileInfo file, CancellationToken ct = default)
    {
        var ms = new MemoryStream();

        if (IsWorkspace(file.MimeType))
        {
            var mime = ExportMime(file.MimeType!);
            await drive.Files.Export(file.Id, mime).DownloadAsync(ms, ct);
            ms.Position = 0;
            return (ms, file.Name + ".pdf", mime);
        }

        var get = drive.Files.Get(file.Id);
        get.SupportsAllDrives = true;
        await get.DownloadAsync(ms, ct);
        ms.Position = 0;
        return (ms, file.Name, file.MimeType ?? "application/octet-stream");
    }

    // ── Upload ────────────────────────────────────────────────────────────

    public async Task<string> UploadAsync(
        DriveService drive, Stream content, string fileName,
        string mimeType, string folderId, CancellationToken ct = default)
    {
        var meta = new GFile { Name = fileName, Parents = [folderId] };
        var req = drive.Files.Create(meta, content, mimeType);
        req.Fields = "id";
        req.SupportsAllDrives = true;
        var upload = await req.UploadAsync(ct);

        if (upload.Status != Google.Apis.Upload.UploadStatus.Completed)
            throw new Exception($"Falha no upload de '{fileName}': {upload.Exception?.Message}");

        return req.ResponseBody?.Id ?? throw new Exception("Upload sem ID retornado.");
    }

    // ── Delete ────────────────────────────────────────────────────────────

    public async Task DeleteAsync(DriveService drive, string fileId, CancellationToken ct = default)
    {
        var req = drive.Files.Delete(fileId);
        req.SupportsAllDrives = true;
        await req.ExecuteAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static bool IsWorkspace(string? mime) =>
        mime?.StartsWith("application/vnd.google-apps.") == true &&
        mime != "application/vnd.google-apps.folder";

    private static string ExportMime(string mime) => "application/pdf";
}

public class DriveFileInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long? Size { get; set; }
}
