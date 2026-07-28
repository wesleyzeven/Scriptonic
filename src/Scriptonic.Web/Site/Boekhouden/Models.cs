using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scriptonic.Web.Site.Boekhouden;

/// <summary>
/// DTOs for the e-Boekhouden REST API (https://api.e-boekhouden.nl).
/// The published OpenAPI spec omits some response schemas, so these models
/// carry the documented common fields and keep everything else in
/// <see cref="Extra"/> so nothing is lost if the API returns more.
/// </summary>
public class EboekhoudenListResponse<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = [];

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class EboekInvoice
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [JsonPropertyName("relationId")]
    public long RelationId { get; set; }

    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }

    [JsonPropertyName("termOfPayment")]
    public int? TermOfPayment { get; set; }

    [JsonPropertyName("totalExcludingVat")]
    public decimal? TotalExcludingVat { get; set; }

    [JsonPropertyName("totalVat")]
    public decimal? TotalVat { get; set; }

    [JsonPropertyName("totalIncludingVat")]
    public decimal? TotalIncludingVat { get; set; }

    [JsonPropertyName("urlPdfFile")]
    public string? UrlPdfFile { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public DateTime? DueDate => Date?.AddDays(TermOfPayment ?? 30);
}

public class EboekOutstandingInvoice
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [JsonPropertyName("relationId")]
    public long RelationId { get; set; }

    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("outstandingAmount")]
    public decimal? OutstandingAmount { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public class EboekRelation
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("contact")]
    public string? Contact { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("emailAddress")]
    public string? EmailAddress { get; set; }

    [JsonPropertyName("emailAddressInvoice")]
    public string? EmailAddressInvoice { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("vatNumber")]
    public string? VatNumber { get; set; }

    [JsonPropertyName("companyRegistrationNumber")]
    public string? CompanyRegistrationNumber { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Editable subset of a relation, sent as PATCH /v1/relation/{id}.</summary>
public class EboekRelationUpdate
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("contact")]
    public string? Contact { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("emailAddress")]
    public string? EmailAddress { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }
}
