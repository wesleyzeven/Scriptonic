using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Scriptonic.Web.Site.Seeding;

public class SiteSeedComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddScoped<SiteContentService>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, SiteSeedHandler>();
        builder.AddNotificationAsyncHandler<MemberSavingNotification, MemberRelationAutoLinkHandler>();
    }
}
