using GDriveMigrator.Services;
using GDriveMigrator.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<DriveOperationsService>();
builder.Services.AddHostedService<MigrationWorker>();

// Guarda o flow OAuth em memória durante o fluxo de auth (browser redirect)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromMinutes(30);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});

var app = builder.Build();

app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.MapRazorPages();

// Redireciona raiz para setup se não configurado, ou para dashboard
app.MapGet("/", (SessionService s) => Results.Redirect(s.Session.IsConfigured ? "/Dashboard" : "/Setup"));

app.Run();
