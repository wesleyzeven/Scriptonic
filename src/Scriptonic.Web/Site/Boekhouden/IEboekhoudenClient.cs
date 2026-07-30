namespace Scriptonic.Web.Site.Boekhouden;

/// <summary>
/// Read/update access to the bookkeeping backend for the customer portal.
/// Implemented by <see cref="EboekhoudenClient"/> (live REST API) and
/// <see cref="DemoEboekhoudenClient"/> (sample data when no token is set).
/// </summary>
public interface IEboekhoudenClient
{
    /// <summary>True when this is the demo implementation (shows a banner in the portal).</summary>
    bool IsDemo { get; }

    Task<IReadOnlyList<EboekInvoice>> GetInvoicesAsync(long relationId, CancellationToken ct = default);

    Task<IReadOnlyList<EboekOutstandingInvoice>> GetOutstandingInvoicesAsync(long relationId, CancellationToken ct = default);

    Task<EboekRelation?> GetRelationAsync(long relationId, CancellationToken ct = default);

    /// <summary>Finds the relation whose (invoice) email address matches, or null.</summary>
    Task<EboekRelation?> FindRelationByEmailAsync(string email, CancellationToken ct = default);

    Task UpdateRelationAsync(long relationId, EboekRelationUpdate update, CancellationToken ct = default);
}
