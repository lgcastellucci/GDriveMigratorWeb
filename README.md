# GDriveMigrator

Aplicação .NET 10 que conecta a duas contas do Google Drive e move todos os arquivos de uma pasta específica da **Conta 1** para uma pasta específica da **Conta 2**.

---

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) instalado
- Duas contas do Google (ou Google Workspace)
- Acesso ao [Google Cloud Console](https://console.cloud.google.com)

---

## 1. Configurar o Google Cloud Console

Você precisa criar **um projeto** com credenciais OAuth2 para cada conta.  
Pode usar o mesmo projeto para as duas, ou criar projetos separados.


---

## 2. Obter os IDs das pastas

O ID de uma pasta do Google Drive fica na URL quando você abre a pasta:

```
https://drive.google.com/drive/folders/1A2B3C4D5E6F7G8H9I0J
                                        ↑ este é o ID
```

---

## 3. Configurar a aplicação

Edite o arquivo `src/GDriveMigrator/appsettings.json`:

```json
{
  "Migration": {
    "Account1": {
      "TokenFolder": "token_account1",
      "UserEmail": "sua-conta1@gmail.com",
      "SourceFolderId": "ID_REAL_DA_PASTA_ORIGEM"
    },
    "Account2": {
      "TokenFolder": "token_account2",
      "UserEmail": "sua-conta2@gmail.com",
      "DestinationFolderId": "ID_REAL_DA_PASTA_DESTINO"
    },
    "Options": {
      "DeleteAfterMove": true,
      "BatchSize": 10,
      "MaxRetries": 3,
      "RetryDelaySeconds": 5,
      "LogProgressEveryNFiles": 5
    }
  }
}
```

Copie os arquivos de credenciais para a pasta do projeto:

```
src/GDriveMigrator/
├── credentials.json   ← coloque aqui
├── appsettings.json
└── ...
```

---

## 4. Executar

```bash
cd src/GDriveMigrator
dotnet run
```

### Primeira execução

Na primeira vez, o app abrirá o navegador **duas vezes** (uma para cada conta) para o fluxo OAuth2. Aceite as permissões para cada conta. Os tokens ficam salvos nas pastas `token_account1` e `token_account2` — nas próximas execuções o login é automático.

### Execuções seguintes

```bash
dotnet run
```

---

## Opções de configuração

| Opção | Padrão | Descrição |
|---|---|---|
| `DeleteAfterMove` | `true` | Remove o arquivo da Conta 1 após upload bem-sucedido |
| `BatchSize` | `10` | Arquivos processados por lote |
| `MaxRetries` | `3` | Tentativas em caso de falha de rede |
| `RetryDelaySeconds` | `5` | Segundos entre tentativas |
| `LogProgressEveryNFiles` | `5` | Frequência do log de progresso |

Para **copiar** sem deletar, defina `"DeleteAfterMove": false`.

---

## Arquivos Google Workspace

Arquivos nativos do Google (Docs, Sheets, Slides) **não podem ser baixados em formato binário** via API. O app os exporta automaticamente como PDF antes de fazer o upload na Conta 2.

---

## Publicar como executável standalone

```bash
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r osx-x64 --self-contained
```

---

## Estrutura do projeto

```
GDriveMigrator/
└── src/
    └── GDriveMigrator/
        ├── GDriveMigrator.csproj
        ├── Program.cs                          # Entry point, DI, fluxo principal
        ├── appsettings.json                    # Configurações
        ├── Models/
        │   └── Models.cs                       # DTOs e configurações tipadas
        └── Services/
            ├── AuthService.cs                  # OAuth2 por conta
            ├── DriveOperationsService.cs       # List / Download / Upload / Delete
            └── MigrationOrchestrator.cs        # Coordena o fluxo completo
```

---

## Códigos de saída

| Código | Significado |
|---|---|
| `0` | Sucesso total |
| `1` | Cancelado pelo usuário |
| `2` | Concluído com erros parciais |
| `3` | Interrompido via Ctrl+C |
| `99` | Erro fatal |
