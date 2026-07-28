using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Scriptonic.Web.Site;
using Scriptonic.Web.Site.Boekhouden;
using Scriptonic.Web.Site.Data;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

// Site services
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection(SiteOptions.SectionName));
builder.Services.AddDbContext<SiteDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("siteDbDSN")
        ?? "Data Source=|DataDirectory|/Site.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True"));
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddSingleton(TimeProvider.System);

// e-Boekhouden: live client when an API token is configured, otherwise a demo
// client with sample data (used on acceptance / local dev).
string? eboekToken = builder.Configuration["Site:Eboekhouden:ApiToken"];
if (!string.IsNullOrWhiteSpace(eboekToken))
{
    builder.Services.AddSingleton<EboekhoudenSessionCache>();
    builder.Services.AddScoped<IEboekhoudenClient, EboekhoudenClient>();
}
else
{
    builder.Services.AddSingleton<DemoEboekhoudenStore>();
    builder.Services.AddScoped<IEboekhoudenClient, DemoEboekhoudenClient>();
}

// Behind nginx-proxy-manager: honor X-Forwarded-Proto/For so absolute URLs
// (canonical links, backoffice) use the public https hostname.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

WebApplication app = builder.Build();

app.UseForwardedHeaders();

await app.BootUmbracoAsync();

// Apply site schema migrations (contact messages) after Umbraco boot so the
// SQLite |DataDirectory| (umbraco/Data) is resolved and exists on first run.
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "umbraco", "Data"));
using (IServiceScope scope = app.Services.CreateScope())
{
    SiteDbContext siteDb = scope.ServiceProvider.GetRequiredService<SiteDbContext>();
    siteDb.Database.Migrate();
}

string appVersion = builder.Configuration["Site:AppVersion"] ?? "dev";
app.MapGet("/health", () => Results.Ok(new { status = "ok", version = appVersion }));

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

// Attribute-routed controllers (/portaal, /contact, /sitemap.xml, /admin/berichten).
app.MapControllers();

await app.RunAsync();
