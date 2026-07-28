using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Scriptonic.Web.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Scriptonic.Web.Controllers;

/// <summary>Sitemap and robots endpoints for search engines.</summary>
public class SeoController : Controller
{
    private readonly SiteContentService _siteContent;
    private readonly SiteOptions _options;

    public SeoController(SiteContentService siteContent, IOptions<SiteOptions> options)
    {
        _siteContent = siteContent;
        _options = options.Value;
    }

    [HttpGet("/sitemap.xml")]
    public IActionResult Sitemap()
    {
        string baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        IPublishedContent? home = _siteContent.GetHome();
        if (home is not null)
        {
            foreach (IPublishedContent page in Walk(home))
            {
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{baseUrl}{page.Url()}</loc>");
                sb.AppendLine($"    <lastmod>{page.UpdateDate:yyyy-MM-dd}</lastmod>");
                sb.AppendLine("  </url>");
            }
        }

        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    [HttpGet("/robots.txt")]
    public IActionResult Robots()
    {
        string baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        string robots = $"""
            User-agent: *
            Disallow: /portaal/
            Disallow: /umbraco/
            Allow: /

            Sitemap: {baseUrl}/sitemap.xml
            """;
        return Content(robots, "text/plain", Encoding.UTF8);
    }

    /// <summary>Public, routable pages only: skips the portal root and hidden nodes.</summary>
    private static IEnumerable<IPublishedContent> Walk(IPublishedContent node)
    {
        if (node.TemplateId is not > 0 || node.Value<bool>("umbracoNaviHide"))
        {
            yield break;
        }
        yield return node;
        foreach (IPublishedContent child in node.Children())
        {
            foreach (IPublishedContent descendant in Walk(child))
            {
                yield return descendant;
            }
        }
    }
}
