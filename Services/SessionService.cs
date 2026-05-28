using System.Text.Json;
using GDriveMigrator.Models;

namespace GDriveMigrator.Services;

public class SessionService
{
    private const string SessionFile = "session.json";
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AppSession Session { get; private set; } = new();

    public SessionService()
    {
        Load();
    }

    public void Save()
    {
        File.WriteAllText(SessionFile, JsonSerializer.Serialize(Session, JsonOpts));
    }

    private void Load()
    {
        if (!File.Exists(SessionFile)) return;
        try
        {
            var json = File.ReadAllText(SessionFile);
            Session = JsonSerializer.Deserialize<AppSession>(json) ?? new AppSession();
        }
        catch { Session = new AppSession(); }
    }

    public void Reset()
    {
        Session = new AppSession();
        if (File.Exists(SessionFile)) File.Delete(SessionFile);
    }
}
