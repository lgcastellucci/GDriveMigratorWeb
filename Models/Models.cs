namespace GDriveMigrator.Models;

public class MigrationSettings
{
    public AccountSettings Account1 { get; set; } = new();
    public AccountSettings Account2 { get; set; } = new();
    public MigrationOptions Options { get; set; } = new();
}

public class AccountSettings
{
    public string CredentialsFile { get; set; } = string.Empty;
    public string TokenFolder { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;

    // Conta 1: pasta de origem
    public string? SourceFolderId { get; set; }

    // Conta 2: pasta de destino
    public string? DestinationFolderId { get; set; }
}

public class MigrationOptions
{
    /// <summary>Deleta o arquivo da Conta 1 após upload bem-sucedido na Conta 2.</summary>
    public bool DeleteAfterMove { get; set; } = true;

    /// <summary>Quantidade de arquivos processados por vez.</summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>Tentativas em caso de falha de rede.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Segundos de espera entre tentativas.</summary>
    public int RetryDelaySeconds { get; set; } = 5;

    /// <summary>Log de progresso a cada N arquivos.</summary>
    public int LogProgressEveryNFiles { get; set; } = 5;
}

public class DriveFileInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long? Size { get; set; }

    public string DisplaySize => Size.HasValue
        ? Size.Value switch
        {
            < 1024 => $"{Size.Value} B",
            < 1024 * 1024 => $"{Size.Value / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{Size.Value / (1024.0 * 1024):F1} MB",
            _ => $"{Size.Value / (1024.0 * 1024 * 1024):F2} GB"
        }
        : "–";
}

public class MigrationResult
{
    public int TotalFiles { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public TimeSpan Elapsed { get; set; }
    public List<string> Errors { get; set; } = [];

    public bool HasErrors => Errors.Count > 0;
}
