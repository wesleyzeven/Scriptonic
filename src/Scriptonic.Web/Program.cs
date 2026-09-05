using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Scriptonic.Web.Site;
using Scriptonic.Web.Site.Boekhouden;
using Scriptonic.Web.Site.Data;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Qweb/Plesk (IIS) hosting: secrets are kept in secrets.json in the site root,
// outside git and outside the Web Deploy sync. Absent everywhere else.
builder.Configuration.AddJsonFile("secrets.json", optional: true, reloadOnChange: true);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

// Site services
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection(SiteOptions.SectionName));
// Resolve |DataDirectory| ourselves: Microsoft.Data.Sqlite only expands it when
// the AppDomain "DataDirectory" slot is set, which is not guaranteed on IIS.
string umbracoDataDir = Path.Combine(builder.Environment.ContentRootPath, "umbraco", "Data");
string siteDbDsn = (builder.Configuration.GetConnectionString("siteDbDSN")
        ?? "Data Source=|DataDirectory|/Site.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True")
    .Replace("|DataDirectory|", umbracoDataDir);
builder.Services.AddDbContext<SiteDbContext>(options => options.UseSqlite(siteDbDsn));
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddSingleton(TimeProvider.System);

// Persist data-protection keys next to the SQLite databases. On IIS without a
// loaded user profile the keys would otherwise be ephemeral, logging everyone
// out (backoffice, portal, antiforgery) on every app-pool recycle. In Docker
// the folder is the umbraco-data volume, so keys survive restarts there too.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "umbraco", "Data", "DataProtection-Keys")));

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

// Make sure the SQLite folder exists before anything opens a connection.
Directory.CreateDirectory(umbracoDataDir);

WebApplication app = builder.Build();

app.UseForwardedHeaders();

await app.BootUmbracoAsync();

// Apply site schema migrations (contact messages) after Umbraco boot.
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
