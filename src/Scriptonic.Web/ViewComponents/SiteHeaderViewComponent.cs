using Microsoft.AspNetCore.Mvc;
using Scriptonic.Web.Site;
using Umbraco.Cms.Core.Security;

namespace Scriptonic.Web.ViewComponents;

public record SiteNavLink(string Name, string Url);

public record SiteHeaderModel(IReadOnlyList<SiteNavLink> Links, bool IsLoggedIn);

/// <summary>
/// Header/nav data resolved outside the Umbraco request pipeline so the same
/// layout works for content pages and the MVC portal/contact pages.
/// </summary>
public class SiteHeaderViewComponent : ViewComponent
{
    private readonly SiteContentService _siteContent;
    private readonly IMemberManager _memberManager;

    public SiteHeaderViewComponent(SiteContentService siteContent, IMemberManager memberManager)
    {
        _siteContent = siteContent;
        _memberManager = memberManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var links = _siteContent.GetNavigation()
            .Select(l => new SiteNavLink(l.Name, l.Url))
            .ToList();
        bool loggedIn = await _memberManager.GetCurrentMemberAsync() is not null;
        return View("Default", new SiteHeaderModel(links, loggedIn));
    }
}
