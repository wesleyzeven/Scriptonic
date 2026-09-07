using System.Security;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Scriptonic.Web.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Scriptonic.Web.Controllers;

/// <summary>Sitemap and robots endpoints for search engines.</summary>
public class SeoController : Controller
{
    private readonly SiteContentService _siteContent;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly SiteOptions _options;

    public SeoController(SiteContentService siteContent, IUmbracoContextFactory umbracoContextFactory, IOptions<SiteOptions> options)
    {
        _siteContent = siteContent;
        _umbracoContextFactory = umbracoContextFactory;
        _options = options.Value;
    }

    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public IActionResult Sitemap()
    {
        string baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:image=\"http://www.google.com/schemas/sitemap-image/1.1\">");

        // Url() needs an ambient UmbracoContext; this is a plain MVC route.
        using var ctx = _umbracoContextFactory.EnsureUmbracoContext();
        IPublishedContent? home = _siteContent.GetHome();
        if (home is not null)
        {
            foreach (IPublishedContent page in Walk(home))
            {
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{Escape(baseUrl + page.Url())}</loc>");
                sb.AppendLine($"    <lastmod>{page.UpdateDate:yyyy-MM-dd}</lastmod>");
                // Portfolio screenshots: lets image search pick them up with the case title.
                if (page.ContentType.Alias == SiteAliases.Case && page.FirstMedia("previewImage") is { } preview)
                {
                    sb.AppendLine("    <image:image>");
                    sb.AppendLine($"      <image:loc>{Escape(baseUrl + Seo.Crop(preview, 1280, 16, 10))}</image:loc>");
                    sb.AppendLine($"      <image:title>{Escape(page.Name)}</image:title>");
                    sb.AppendLine("    </image:image>");
                }
                sb.AppendLine("  </url>");
            }
        }

        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    [HttpGet("/robots.txt")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public IActionResult Robots()
    {
        string baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        // Prefix rules, so "/portaal" also covers "/portaal/" and everything below.
        string robots = $"""
            User-agent: *
            Disallow: /portaal
            Disallow: /admin
            Disallow: /umbraco
            Disallow: /contact-form
            Allow: /

            Sitemap: {baseUrl}/sitemap.xml
            """;
        return Content(robots, "text/plain", Encoding.UTF8);
    }

    /// <summary>
    /// Indexable pages only: nodes with a template that aren't flagged noindex
    /// (the portal root and offertes have no template). Hidden-in-navigation
    /// is a menu concern and does not exclude a page here. Children are always
    /// walked, so a noindex parent can still have indexable pages below it.
    /// </summary>
    private static IEnumerable<IPublishedContent> Walk(IPublishedContent node)
    {
        if (Seo.IsIndexable(node))
        {
            yield return node;
        }
        foreach (IPublishedContent child in node.Children())
        {
            foreach (IPublishedContent descendant in Walk(child))
            {
                yield return descendant;
            }
        }
    }

    private static string Escape(string value) => SecurityElement.Escape(value);
}
