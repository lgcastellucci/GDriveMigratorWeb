using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace GDriveMigrator.Services;

public class AuthService
{
    private const string CredentialsFile = "credentials.json";
    private const string AppName = "GDriveMigrator";

    private static readonly string[] Scopes =
    [
        DriveService.Scope.Drive, // Ver, editar, criar e excluir todos os seus arquivos do Google Drive
        DriveService.Scope.DriveFile // Ver, editar, criar e excluir apenas os arquivos do Google Drive que você usa com este app.
    ];

    // ── Gera URL de autorização (manual ou redirect) ───────────────────────

    public async Task<(string AuthUrl, GoogleAuthorizationCodeFlow Flow)> BuildAuthUrlAsync(
        string tokenFolder,
        string? redirectUri = null)
    {
        var flow = await CreateFlowAsync(tokenFolder);
        var req = flow.CreateAuthorizationCodeRequest(redirectUri ?? "urn:ietf:wg:oauth:2.0:oob");
        var url = req.Build().AbsoluteUri;
        return (url, flow);
    }

    // ── Troca código por token (fluxo manual) ─────────────────────────────

    public async Task<bool> ExchangeCodeAsync(string userEmail, string code, string tokenFolder, string? redirectUri = null, CancellationToken ct = default)
    {
        var flow = await CreateFlowAsync(tokenFolder);
        try
        {
            var token = await flow.ExchangeCodeForTokenAsync(userEmail, code, redirectUri ?? "urn:ietf:wg:oauth:2.0:oob", ct);
            await new FileDataStore(tokenFolder, false).StoreAsync(userEmail, token);
            return true;
        }
        catch { return false; }
    }

    // ── Cria DriveService a partir de token salvo ─────────────────────────

    public async Task<DriveService?> CreateDriveServiceAsync(string userEmail, string tokenFolder, CancellationToken ct = default)
    {
        var flow = await CreateFlowAsync(tokenFolder);
        var store = new FileDataStore(tokenFolder, false);
        var token = await store.GetAsync<TokenResponse>(userEmail);
        if (token == null) return null;

        var credential = new UserCredential(flow, userEmail, token);
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = AppName
        });
    }

    // ── Verifica se já existe token válido salvo ───────────────────────────

    public async Task<bool> HasValidTokenAsync(string userEmail, string tokenFolder)
    {
        try
        {
            var store = new FileDataStore(tokenFolder, false);
            var token = await store.GetAsync<TokenResponse>(userEmail);
            return token != null && !string.IsNullOrEmpty(token.RefreshToken);
        }
        catch { return false; }
    }

    // ── Flow factory ──────────────────────────────────────────────────────

    private static async Task<GoogleAuthorizationCodeFlow> CreateFlowAsync(string tokenFolder)
    {
        if (!File.Exists(CredentialsFile))
            throw new FileNotFoundException($"Arquivo '{CredentialsFile}' não encontrado na raiz do projeto.");

        await using var stream = new FileStream(CredentialsFile, FileMode.Open, FileAccess.Read);
        var secrets = (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets;

        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = secrets,
            Scopes = Scopes,
            DataStore = new FileDataStore(tokenFolder, false)
        });
    }
}
