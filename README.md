# GDriveMigrator

Aplicação .NET 10 que conecta a duas contas do Google Drive e move todos os arquivos de uma pasta específica da **Conta 1** para uma pasta específica da **Conta 2**.

---

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- O arquivo `credentials.json` do Google Cloud Console (fornecido pelo desenvolvedor)

---

## 1. Configurar o Google Cloud Console

> **Feito uma única vez pelo desenvolvedor — não pelo usuário final.**

1. Acesse [console.cloud.google.com](https://console.cloud.google.com) e crie um projeto
2. Vá em **APIs e Serviços → Biblioteca**, pesquise e ative a **Google Drive API**
3. Vá em **APIs e Serviços → Credenciais → + Criar Credenciais → ID do cliente OAuth**
4. Tipo: **App para computador** → clique em Criar → baixe o JSON e renomeie para `credentials.json`
5. Em **Tela de consentimento OAuth → Usuários de teste**, adicione os e-mails das contas que usarão o app

---

## 2. Configurar a aplicação

Coloque o `credentials.json` na raiz do projeto:

```
GDriveMigrator/
├── credentials.json   ← aqui
├── GDriveMigrator.csproj
├── Program.cs
└── ...
```

---

## 3. Executar

```bash
dotnet run
```

O app guia tudo pelo terminal — não é necessário editar nenhum arquivo de configuração.

### Primeira execução

O assistente pergunta passo a passo:

```
  ┌─ Conta 1 — Origem
  E-mail da Conta 1: conta1@gmail.com

  ┌─ Pasta de origem (Conta 1)
  Pode colar a URL completa ou só o ID:
    URL : https://drive.google.com/drive/folders/1A2B3C4D5E6
    ID  : 1A2B3C4D5E6
  URL ou ID da pasta de origem: https://drive.google.com/drive/folders/1A2B3C4D5E6
  → ID extraído: 1A2B3C4D5E6

  ┌─ Conta 2 — Destino
  E-mail da Conta 2: conta2@gmail.com

  ┌─ Pasta de destino (Conta 2)
  URL ou ID da pasta de destino: 9Z8Y7X6W5V4

  Deletar arquivos da Conta 1 após mover? (S/n): s
```

Em seguida, o app exibe um link de autorização para cada conta:

```
  [1/4] Autenticando Conta 1

  ┌─────────────────────────────────────────────────────────────┐
  │  Copie o link abaixo e abra no seu browser:                 │
  └─────────────────────────────────────────────────────────────┘

  https://accounts.google.com/o/oauth2/auth?client_id=...

  Passos:
    1. Abra o link acima no browser
    2. Faça login com: conta1@gmail.com
    3. Autorize o acesso ao Google Drive
    4. Copie o código exibido pelo Google
    5. Cole o código abaixo e pressione Enter

  Código de autorização: ____
```

### Execuções seguintes

A configuração fica salva em `session.json` e os tokens em `.tokens/`. O app pergunta se quer reutilizar:

```
  ┌─ Sessão anterior encontrada
  Conta 1 : conta1@gmail.com
  Conta 2 : conta2@gmail.com
  Origem  : 1A2B3C4D5E6
  Destino : 9Z8Y7X6W5V4

  Usar essa configuração? (S/n):
```

Nenhum código de autorização é pedido novamente enquanto os tokens forem válidos.

---

## Opções de migração

| Opção | Padrão | Descrição |
|---|---|---|
| `DeleteAfterMove` | `true` | Remove o arquivo da Conta 1 após upload bem-sucedido |
| `BatchSize` | `10` | Arquivos processados por lote |
| `MaxRetries` | `3` | Tentativas em caso de falha de rede |
| `RetryDelaySeconds` | `5` | Segundos entre tentativas |
| `LogProgressEveryNFiles` | `5` | Frequência do log de progresso |

Para **copiar** sem deletar, responda `n` quando o wizard perguntar sobre deletar — ou edite diretamente o `session.json`.

---

## Onde encontrar o ID de uma pasta

Abra a pasta no Google Drive. O ID é a parte final da URL:

```
https://drive.google.com/drive/folders/1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgVE2upms
                                        ↑ este é o ID
```

O app aceita tanto a URL completa quanto o ID puro.

---

## Arquivos Google Workspace

Arquivos nativos do Google (Docs, Sheets, Slides) são exportados automaticamente como **PDF** antes do upload na Conta 2, pois não possuem formato binário para download direto.

---

## Estrutura do projeto

```
GDriveMigrator/
├── GDriveMigrator.csproj
├── GDriveMigrator.sln
├── Program.cs                        # Entry point, DI, fluxo principal
├── appsettings.json                  # Opções padrão
├── Models/
│   └── Models.cs                     # DTOs e configurações tipadas
└── Services/
    ├── AuthService.cs                # OAuth2 manual por conta (link no terminal)
    ├── DriveOperationsService.cs     # List / Download / Upload / Delete
    ├── MigrationOrchestrator.cs      # Coordena o fluxo completo
    └── SetupService.cs               # Wizard de configuração via terminal
```

---

## Publicar como executável standalone

```bash
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r osx-x64 --self-contained
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
