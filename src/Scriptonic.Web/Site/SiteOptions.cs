namespace Scriptonic.Web.Site;

public class SiteOptions
{
    public const string SectionName = "Site";

    /// <summary>Public https base URL (canonical links, sitemap). No trailing slash.</summary>
    public string PublicBaseUrl { get; set; } = "https://scriptonic.local.io";

    public string SiteName { get; set; } = "Scriptonic";

    /// <summary>Version stamp injected by the Docker build.</summary>
    public string AppVersion { get; set; } = "dev";

    /// <summary>Key that unlocks /admin/berichten (contact form submissions).</summary>
    public string AdminKey { get; set; } = string.Empty;

    /// <summary>Optional Scriptonic Analytics tracker script URL; injected in the layout when set.</summary>
    public string AnalyticsScriptUrl { get; set; } = string.Empty;

    public EboekhoudenOptions Eboekhouden { get; set; } = new();

    public PortalOptions Portal { get; set; } = new();
}

public class EboekhoudenOptions
{
    /// <summary>Secret API token from e-Boekhouden (Beheer &gt; Instellingen &gt; API). Empty = demo mode.</summary>
    public string ApiToken { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.e-boekhouden.nl";

    /// <summary>Source code identifying this integration (max 10 chars, [\w_ ]).</summary>
    public string Source { get; set; } = "Scriptonic";
}

public class PortalOptions
{
    /// <summary>Seed a demo customer member on first boot (acceptance / local dev).</summary>
    public bool SeedDemoMember { get; set; }

    public string DemoMemberEmail { get; set; } = "demo@scriptonic.nl";

    public string DemoMemberPassword { get; set; } = "DemoKlant123!";
}
