# GDriveMigrator

Aplicação .NET 10 que move arquivos entre duas contas do Google Drive.  
Configuração via **interface web local** + worker em background que verifica e move a cada 5 minutos.

---

## Como funciona

```
dotnet run  →  abre http://localhost:5000
```

- **Interface web** (localhost:5000): configura contas, autentica via OAuth2 e seleciona pastas navegando pelo Drive
- **Worker em background**: loop a cada 5 minutos que lista arquivos na pasta de origem e move para o destino
- **Logs**: aparecem no terminal onde o app foi iniciado

---

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- `credentials.json` do Google Cloud Console

---

## Setup do Google Cloud Console

> Feito uma única vez pelo desenvolvedor.

1. Crie um projeto em [console.cloud.google.com](https://console.cloud.google.com)
2. **APIs e Serviços → Biblioteca** → ative **Google Drive API**
3. **Credenciais → + Criar → ID do cliente OAuth** → tipo: **App para computador**
4. Baixe o JSON e renomeie para `credentials.json`
5. **Tela de consentimento → Usuários de teste**: adicione os e-mails das contas

---

## Executar

```bash
# Coloque o credentials.json na raiz do projeto, então:
dotnet run
```

Acesse **http://localhost:5000** e siga o wizard:

1. **Conta 1** — informe o e-mail e autentique
2. **Conta 2** — informe o e-mail e autentique
3. **Opções** — intervalo, deletar após mover, etc.
4. **Pasta de origem** — navegue pelas pastas da Conta 1 e selecione
5. **Pasta de destino** — navegue pelas pastas da Conta 2 e selecione

O worker inicia automaticamente após a configuração.

---

## Autenticação OAuth2

Duas opções disponíveis na interface:

**Manual (copy/paste)**
- Copia o link gerado, abre no browser
- Faz login com a conta correta
- Cola o código retornado

**Browser automático**
- Clica em "Abrir login do Google"
- Faz login e autoriza
- Redirecionado de volta automaticamente

---

## Arquivos Google Workspace

Docs, Sheets e Slides são exportados como **PDF** automaticamente.

---

## Estrutura

```
GDriveMigrator/
├── Program.cs
├── GDriveMigrator.csproj
├── Models/Models.cs
├── Services/
│   ├── AuthService.cs
│   ├── DriveOperationsService.cs
│   └── SessionService.cs
├── Workers/MigrationWorker.cs
├── Pages/
│   ├── Dashboard.cshtml
│   ├── Setup.cshtml
│   ├── Auth.cshtml
│   ├── AuthCallback.cshtml
│   └── Folders.cshtml
└── wwwroot/app.css
```

---

## Códigos de saída

| Código | Significado |
|---|---|
| `0` | Encerrado normalmente |
| Ctrl+C | Para o worker e encerra |
