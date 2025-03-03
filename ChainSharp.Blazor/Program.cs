using ChainSharp.Blazor.Components;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Extensions;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder
    .Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 10 * 1024 * 1024);

builder.Services.AddControllers();
builder.Services.AddRadzenComponents();

builder.Services.AddRadzenCookieThemeService(options =>
{
    options.Name = "ChainSharp.BlazorTheme";
    options.Duration = TimeSpan.FromDays(365);
});
builder.Services.AddHttpClient();

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();
var connectionString = configuration.GetRequiredSection("Configuration")[
    "DatabaseConnectionString"
]!;

builder.Services.AddChainSharpEffects(options => options.AddPostgresEffect(connectionString));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.MapControllers();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
