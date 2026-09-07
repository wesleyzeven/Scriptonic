using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Scriptonic.Web.Site;

/// <summary>Document type aliases used across seeding, views and controllers.</summary>
public static class SiteAliases
{
    public const string Home = "scriptonicHome";
    public const string Diensten = "scriptonicDiensten";
    public const string Dienst = "scriptonicDienst";
    public const string Portfolio = "scriptonicPortfolio";
    public const string Case = "scriptonicCase";
    public const string Page = "scriptonicPage";
    public const string Contact = "scriptonicContact";
    public const string PortalRoot = "scriptonicPortalRoot";
    public const string Offerte = "scriptonicOfferte";

    public const string MemberType = "portalKlant";
    public const string MemberGroup = "Portaalklanten";
}

/// <summary>
/// Read-side helper resolving site content from the published cache, usable
/// from both Umbraco-routed views and the MVC portal/contact controllers.
/// </summary>
public class SiteContentService
{
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IDocumentNavigationQueryService _navigation;

    public SiteContentService(IUmbracoContextFactory umbracoContextFactory, IDocumentNavigationQueryService navigation)
    {
        _umbracoContextFactory = umbracoContextFactory;
        _navigation = navigation;
    }

    public IPublishedContent? GetHome()
    {
        using var ctx = _umbracoContextFactory.EnsureUmbracoContext();
        if (!_navigation.TryGetRootKeys(out IEnumerable<Guid> rootKeys))
        {
            return null;
        }
        return rootKeys
            .Select(key => ctx.UmbracoContext.Content?.GetById(key))
            .FirstOrDefault(c => c?.ContentType.Alias == SiteAliases.Home);
    }

    /// <summary>Top navigation: children of home that have a template and aren't hidden.</summary>
    public IReadOnlyList<(string Name, string Url)> GetNavigation()
    {
        using var ctx = _umbracoContextFactory.EnsureUmbracoContext();
        IPublishedContent? home = GetHome();
        return home is null
            ? []
            : home.Children()
                .Where(c => c.TemplateId is > 0 && !c.Value<bool>("umbracoNaviHide"))
                .Select(c => (c.Name, c.Url()))
                .ToList();
    }

    /// <summary>
    /// E-mail address and KvK number as maintained on the contact page, for
    /// the footer, the contact page and the Organization structured data.
    /// </summary>
    public ContactInfo GetContactInfo()
    {
        using var ctx = _umbracoContextFactory.EnsureUmbracoContext();
        IPublishedContent? contact = GetHome()?.Children()
            .FirstOrDefault(c => c.ContentType.Alias == SiteAliases.Contact);
        string? Text(string alias) => contact?.Value<string>(alias) is { } v && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
        return new ContactInfo(Text("email") ?? "info@scriptonic.nl", Text("kvk"));
    }

    /// <summary>Offertes (CMS-managed quotes) for one e-Boekhouden relation code.</summary>
    public IReadOnlyList<OfferteInfo> GetOffertes(string relationCode)
    {
        if (string.IsNullOrWhiteSpace(relationCode))
        {
            return [];
        }
        using var ctx = _umbracoContextFactory.EnsureUmbracoContext();
        IPublishedContent? portalRoot = GetHome()?.Children()
            .FirstOrDefault(c => c.ContentType.Alias == SiteAliases.PortalRoot);
        return portalRoot is null
            ? []
            : portalRoot.Children()
                .Where(c => c.ContentType.Alias == SiteAliases.Offerte
                    && string.Equals(c.Value<string>("relationCode"), relationCode, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.Value<DateTime>("offerteDate"))
                .Select(c => new OfferteInfo(
                    c.Name,
                    c.Value<string>("offerteNumber") ?? "",
                    c.Value<DateTime>("offerteDate") is var d && d != default ? d : null,
                    c.Value<DateTime>("validUntil") is var v && v != default ? v : null,
                    c.Value<decimal>("amount"),
                    c.Value<string>("status") ?? "Open",
                    c.Value<string>("description") ?? "",
                    c.FirstMedia("pdf")?.Url()))
                .ToList();
    }
}

/// <summary>Company details from the contact page; KvK is null until an editor fills it in.</summary>
public record ContactInfo(string Email, string? Kvk);

/// <summary>Offerte data resolved inside an Umbraco context, safe to render anywhere.</summary>
public record OfferteInfo(
    string Name,
    string Number,
    DateTime? Date,
    DateTime? ValidUntil,
    decimal Amount,
    string Status,
    string Description,
    string? PdfUrl);
