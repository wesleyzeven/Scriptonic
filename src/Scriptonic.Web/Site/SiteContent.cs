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

    /// <summary>Offertes (CMS-managed quotes) for one e-Boekhouden relation code.</summary>
    public IReadOnlyList<IPublishedContent> GetOffertes(string relationCode)
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
                .ToList();
    }
}
