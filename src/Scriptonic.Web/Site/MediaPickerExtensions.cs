using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Scriptonic.Web.Site;

/// <summary>
/// Reads media from a Media Picker property regardless of how the picker is
/// configured. A single-select Media Picker (the default "Media Picker" data
/// type) converts to one <see cref="IPublishedContent"/>, a multi-select one
/// to a list; asking for a list on a single picker silently yields null.
/// </summary>
public static class MediaPickerExtensions
{
    public static IReadOnlyList<IPublishedContent> MediaItems(this IPublishedContent content, string alias)
        => content.Value(alias) switch
        {
            IPublishedContent single => [single],
            IEnumerable<IPublishedContent> many => many.ToList(),
            _ => [],
        };

    public static IPublishedContent? FirstMedia(this IPublishedContent content, string alias)
        => content.MediaItems(alias).FirstOrDefault();
}
