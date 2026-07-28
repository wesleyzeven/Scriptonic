namespace Scriptonic.Web.Site.Boekhouden;

/// <summary>
/// In-memory sample data used when no e-Boekhouden API token is configured
/// (local dev and acceptance). Profile edits are kept for the lifetime of the
/// process so the portal's edit flow can be exercised end-to-end.
/// </summary>
public class DemoEboekhoudenStore
{
    public readonly Dictionary<long, EboekRelation> Relations = new()
    {
        [1001] = new EboekRelation
        {
            Id = 1001,
            Type = "B",
            Code = "DEMO001",
            Name = "Demo Klant B.V.",
            Contact = "Daan Demo",
            Address = "Voorbeeldstraat 12",
            PostalCode = "1234 AB",
            City = "Utrecht",
            Country = "Nederland",
            PhoneNumber = "030-1234567",
            EmailAddress = "demo@scriptonic.nl",
            Website = "https://www.voorbeeld.nl",
            VatNumber = "NL123456789B01",
        },
    };
}

public class DemoEboekhoudenClient : IEboekhoudenClient
{
    private readonly DemoEboekhoudenStore _store;
    private readonly TimeProvider _time;

    public DemoEboekhoudenClient(DemoEboekhoudenStore store, TimeProvider time)
    {
        _store = store;
        _time = time;
    }

    public bool IsDemo => true;

    public Task<IReadOnlyList<EboekInvoice>> GetInvoicesAsync(long relationId, CancellationToken ct = default)
    {
        DateTime today = _time.GetLocalNow().Date;
        IReadOnlyList<EboekInvoice> invoices =
        [
            new EboekInvoice { Id = 3, InvoiceNumber = "F2026-018", RelationId = relationId, Date = today.AddDays(-12), TermOfPayment = 30, TotalExcludingVat = 1250.00m, TotalVat = 262.50m, TotalIncludingVat = 1512.50m },
            new EboekInvoice { Id = 2, InvoiceNumber = "F2026-009", RelationId = relationId, Date = today.AddMonths(-2), TermOfPayment = 30, TotalExcludingVat = 780.00m, TotalVat = 163.80m, TotalIncludingVat = 943.80m },
            new EboekInvoice { Id = 1, InvoiceNumber = "F2025-142", RelationId = relationId, Date = today.AddMonths(-7), TermOfPayment = 14, TotalExcludingVat = 2400.00m, TotalVat = 504.00m, TotalIncludingVat = 2904.00m },
        ];
        return Task.FromResult(invoices);
    }

    public Task<IReadOnlyList<EboekOutstandingInvoice>> GetOutstandingInvoicesAsync(long relationId, CancellationToken ct = default)
    {
        DateTime today = _time.GetLocalNow().Date;
        IReadOnlyList<EboekOutstandingInvoice> outstanding =
        [
            new EboekOutstandingInvoice { Id = 3, InvoiceNumber = "F2026-018", RelationId = relationId, Date = today.AddDays(-12), Amount = 1512.50m, OutstandingAmount = 1512.50m },
        ];
        return Task.FromResult(outstanding);
    }

    public Task<EboekRelation?> GetRelationAsync(long relationId, CancellationToken ct = default)
    {
        _store.Relations.TryGetValue(relationId, out EboekRelation? relation);
        return Task.FromResult(relation);
    }

    public Task UpdateRelationAsync(long relationId, EboekRelationUpdate update, CancellationToken ct = default)
    {
        if (_store.Relations.TryGetValue(relationId, out EboekRelation? relation))
        {
            relation.Name = update.Name;
            relation.Contact = update.Contact;
            relation.Address = update.Address;
            relation.PostalCode = update.PostalCode;
            relation.City = update.City;
            relation.Country = update.Country;
            relation.PhoneNumber = update.PhoneNumber;
            relation.EmailAddress = update.EmailAddress;
            relation.Website = update.Website;
        }
        return Task.CompletedTask;
    }
}
