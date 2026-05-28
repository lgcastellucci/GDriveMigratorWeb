using System.Text.Json.Serialization;

namespace GDriveMigrator.Models;

public class AppSession
{
    public AccountConfig Account1 { get; set; } = new();
    public AccountConfig Account2 { get; set; } = new();
    public MigrationOptions Options { get; set; } = new();

    [JsonIgnore]
    public bool IsConfigured =>
        Account1.IsAuthenticated &&
        Account2.IsAuthenticated &&
        !string.IsNullOrWhiteSpace(Account1.SourceFolderId) &&
        !string.IsNullOrWhiteSpace(Account2.DestinationFolderId);
}

public class AccountConfig
{
    public string UserEmail { get; set; } = string.Empty;
    public string TokenFolder { get; set; } = string.Empty;
    public string? SourceFolderId { get; set; }
    public string? SourceFolderName { get; set; }
    public string? DestinationFolderId { get; set; }
    public string? DestinationFolderName { get; set; }
    public bool IsAuthenticated { get; set; }

    [JsonIgnore]
    public bool IsAccount1 => SourceFolderId != null || (!IsAuthenticated && string.IsNullOrEmpty(DestinationFolderId));
}

public class MigrationOptions
{
    public bool DeleteAfterMove { get; set; } = true;
    public int BatchSize { get; set; } = 10;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
    public int WorkerIntervalMinutes { get; set; } = 5;
}

public class DriveFolder
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
}

public class MigrationResult
{
    public DateTime RunAt { get; set; } = DateTime.Now;
    public int TotalFiles { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public TimeSpan Elapsed { get; set; }
    public List<string> Errors { get; set; } = [];
    public bool HasErrors => Errors.Count > 0;
    public bool NothingToDo => TotalFiles == 0;
}

public class AuthFlowState
{
    public string AccountKey { get; set; } = string.Empty; // "account1" | "account2"
    public string UserEmail { get; set; } = string.Empty;
    public string AuthUrl { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool UseManual { get; set; } = true;
}
