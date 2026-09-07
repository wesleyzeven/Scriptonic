using System.Text.Json;
using Microsoft.AspNetCore.Html;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Scriptonic.Web.Site;

/// <summary>
/// Small SEO helpers shared by the layout, the content views, the sitemap and
/// the seeder: the noindex property, JSON-LD output and responsive image URLs.
/// </summary>
public static class Seo
{
    /// <summary>Toggle in the "SEO" group: keep the page out of search engines and the sitemap.</summary>
    public const string NoIndexAlias = "seoNoIndex";

    /// <summary>
    /// Fixed key of the seeded "Pagina niet gevonden" node, referenced from
    /// Umbraco:CMS:Content:Error404Collection in appsettings.json.
    /// </summary>
    public static readonly Guid NotFoundPageKey = new("7f3b1c2e-9d4a-4e0b-8c5f-2a6d1e9b4f04");

    /// <summary>
    /// Public, routable and indexable: has a template and isn't flagged noindex.
    /// Nodes without a template (portal root, offertes) have no URL at all.
    /// </summary>
    public static bool IsIndexable(IPublishedContent content)
        => content.TemplateId is > 0 && !content.Value<bool>(NoIndexAlias);

    private static readonly JsonSerializerOptions JsonLdOptions = new()
    {
        // The default encoder writes '<' and '&' as unicode escapes, so
        // page text can never close the <script> tag it is rendered in.
        WriteIndented = false,
    };

    /// <summary>
    /// The complete JSON-LD script element. Emitted as one raw string because
    /// Razor would otherwise HTML-encode the '+' in the type attribute.
    /// </summary>
    public static IHtmlContent JsonLd(object graph)
        => new HtmlString("<script type=\"application/ld+json\">" + JsonSerializer.Serialize(graph, JsonLdOptions) + "</script>");

    /// <summary>
    /// First non-blank text among the given properties, e.g. the SEO
    /// description with the intro as fallback. Umbraco returns an empty
    /// string (not null) for an untouched textarea, so ?? alone won't do.
    /// </summary>
    public static string? FirstText(IPublishedContent content, params string[] aliases)
        => aliases.Select(a => content.Value<string>(a)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    /// <summary>
    /// Meta description from the first filled property, cut at a sentence or
    /// word boundary so a long intro never overflows the snippet.
    /// </summary>
    public static string? Description(IPublishedContent content, params string[] aliases)
    {
        string? text = FirstText(content, aliases);
        if (text is null)
        {
            return null;
        }
        text = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        const int max = 160;
        if (text.Length <= max)
        {
            return text;
        }
        int sentence = text.LastIndexOfAny(['.', '!', '?'], max - 1);
        if (sentence >= max / 2)
        {
            return text[..(sentence + 1)];
        }
        int word = text.LastIndexOf(' ', max - 2);
        return text[..(word > 0 ? word : max - 1)] + "…";
    }

    /// <summary>
    /// ImageSharp crop URL at the given width and aspect ratio, as WebP. The
    /// URL is HMAC-signed by Umbraco, so it works with the secret key set.
    /// </summary>
    public static string Crop(IPublishedContent media, int width, int aspectWidth, int aspectHeight)
        => media.GetCropUrl(width: width, height: width * aspectHeight / aspectWidth, quality: 80, furtherOptions: "&format=webp")
            ?? media.Url();

    /// <summary>srcset candidates at each width, all cropped to the same aspect ratio.</summary>
    public static string Srcset(IPublishedContent media, int aspectWidth, int aspectHeight, params int[] widths)
        => string.Join(", ", widths.Select(w => $"{Crop(media, w, aspectWidth, aspectHeight)} {w}w"));
}
