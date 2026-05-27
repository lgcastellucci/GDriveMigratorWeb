using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using GDriveMigrator.Models;
using Microsoft.Extensions.Logging;

namespace GDriveMigrator.Services;

public class AuthService(ILogger<AuthService> logger)
{
    private static readonly string[] Scopes =
    [
        DriveService.Scope.Drive,
        DriveService.Scope.DriveFile
    ];

    /// <summary>
    /// Cria um DriveService autenticado para a conta informada.
    /// Se já existe token salvo, usa direto.
    /// Se não, exibe o link no terminal e aguarda o código de autorização.
    /// </summary>
    public async Task<DriveService> CreateDriveServiceAsync(
        AccountSettings account,
        string applicationName,
        CancellationToken ct = default)
    {
        if (!File.Exists(account.CredentialsFile))
            throw new FileNotFoundException(
                $"Arquivo '{account.CredentialsFile}' não encontrado. " +
                "Coloque o credentials.json na pasta do projeto.");

        await using var stream = new FileStream(account.CredentialsFile, FileMode.Open, FileAccess.Read);
        var secrets = (await GoogleClientSecrets.FromStreamAsync(stream, ct)).Secrets;

        var tokenStore = new FileDataStore(account.TokenFolder, fullPath: false);

        // Verifica se já existe token válido salvo
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = secrets,
            Scopes = Scopes,
            DataStore = tokenStore
        });

        var existingToken = await tokenStore.GetAsync<TokenResponse>(account.UserEmail);

        UserCredential credential;

        if (existingToken != null && !string.IsNullOrEmpty(existingToken.RefreshToken))
        {
            // Token já existe — usa sem pedir autorização novamente
            logger.LogDebug("Token existente encontrado para {Email}", account.UserEmail);
            credential = new UserCredential(flow, account.UserEmail, existingToken);
        }
        else
        {
            // Sem token — gera o link e pede o código manualmente
            credential = await AuthorizeManuallyAsync(flow, secrets, account, tokenStore, ct);
        }

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = applicationName
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Fluxo manual: exibe link → aguarda código → troca por token
    // ──────────────────────────────────────────────────────────────────────

    private async Task<UserCredential> AuthorizeManuallyAsync(
        GoogleAuthorizationCodeFlow flow,
        ClientSecrets secrets,
        AccountSettings account,
        FileDataStore tokenStore,
        CancellationToken ct)
    {
        // Gera a URL de autorização
        var authUrl = flow.CreateAuthorizationCodeRequest("urn:ietf:wg:oauth:2.0:oob").Build().AbsoluteUri;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │  Copie o link abaixo e abra no seu browser:                 │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────┘");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"  {authUrl}");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Passos:");
        Console.WriteLine("    1. Abra o link acima no browser");
        Console.WriteLine($"    2. Faça login com: {account.UserEmail}");
        Console.WriteLine("    3. Autorize o acesso ao Google Drive");
        Console.WriteLine("    4. Copie o código exibido pelo Google");
        Console.WriteLine("    5. Cole o código abaixo e pressione Enter");
        Console.ResetColor();
        Console.WriteLine();

        // Aguarda o código
        string code;
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("  Código de autorização: ");
            Console.ResetColor();

            code = Console.ReadLine()?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(code))
                break;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ✗ Código não pode ser vazio. Tente novamente.");
            Console.ResetColor();
        }

        // Troca o código pelo token
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Validando código...");
        Console.ResetColor();

        TokenResponse token;
        try
        {
            token = await flow.ExchangeCodeForTokenAsync(
                account.UserEmail, code, "urn:ietf:wg:oauth:2.0:oob", ct);
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Código inválido ou expirado. Tente novamente.\nDetalhe: {ex.Message}");
        }

        // Salva o token para execuções futuras
        await tokenStore.StoreAsync(account.UserEmail, token);

        logger.LogDebug("Token salvo em {Folder} para {Email}", account.TokenFolder, account.UserEmail);

        return new UserCredential(flow, account.UserEmail, token);
    }
}
