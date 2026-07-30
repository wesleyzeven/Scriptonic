using Scriptonic.Web.Site.Boekhouden;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace Scriptonic.Web.Site;

/// <summary>
/// When a portal member is saved with an email address but no e-Boekhouden
/// link yet, look the relation up by email and fill relationId/relationCode
/// (and the company name if empty) automatically. Manually entered values are
/// never overwritten, and a failed lookup never blocks the save.
/// </summary>
public class MemberRelationAutoLinkHandler : INotificationAsyncHandler<MemberSavingNotification>
{
    private readonly IEboekhoudenClient _boekhouden;
    private readonly ILogger<MemberRelationAutoLinkHandler> _logger;

    public MemberRelationAutoLinkHandler(IEboekhoudenClient boekhouden, ILogger<MemberRelationAutoLinkHandler> logger)
    {
        _boekhouden = boekhouden;
        _logger = logger;
    }

    public async Task HandleAsync(MemberSavingNotification notification, CancellationToken cancellationToken)
    {
        foreach (IMember member in notification.SavedEntities)
        {
            if (member.ContentType.Alias != SiteAliases.MemberType
                || string.IsNullOrWhiteSpace(member.Email)
                || member.GetValue<int>("relationId") > 0)
            {
                continue;
            }

            try
            {
                EboekRelation? relation = await _boekhouden.FindRelationByEmailAsync(member.Email, cancellationToken);
                if (relation is null)
                {
                    _logger.LogInformation("No e-Boekhouden relation found for member email {Email}; fill relationId/relationCode manually", member.Email);
                    continue;
                }

                member.SetValue("relationId", (int)relation.Id);
                if (string.IsNullOrWhiteSpace(member.GetValue<string>("relationCode")))
                {
                    member.SetValue("relationCode", relation.Code);
                }
                if (string.IsNullOrWhiteSpace(member.GetValue<string>("companyName")))
                {
                    member.SetValue("companyName", relation.Name);
                }
                _logger.LogInformation("Auto-linked member {Email} to e-Boekhouden relation {Id} ({Code}, {Name})",
                    member.Email, relation.Id, relation.Code, relation.Name);
            }
            catch (Exception ex)
            {
                // Linking is best-effort; the member save must always succeed.
                _logger.LogWarning(ex, "e-Boekhouden lookup failed while saving member {Email}", member.Email);
            }
        }
    }
}
