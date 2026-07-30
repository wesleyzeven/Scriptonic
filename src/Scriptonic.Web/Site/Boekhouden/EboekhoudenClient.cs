using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Scriptonic.Web.Site.Boekhouden;

/// <summary>
/// Caches the short-lived session token from POST /v1/session so we don't
/// open a new session on every portal page view.
/// </summary>
public class EboekhoudenSessionCache
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<string> GetTokenAsync(Func<Task<(string Token, int ExpiresInSeconds)>> acquire, TimeProvider time, CancellationToken ct)
    {
        if (_token is not null && time.GetUtcNow() < _expiresAt)
        {
            return _token;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_token is null || time.GetUtcNow() >= _expiresAt)
            {
                (string token, int expiresIn) = await acquire();
                _token = token;
                // Renew a minute early so requests never race the expiry.
                _expiresAt = time.GetUtcNow().AddSeconds(Math.Max(60, expiresIn - 60));
            }
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate() => _expiresAt = DateTimeOffset.MinValue;
}

public class EboekhoudenClient : IEboekhoudenClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly EboekhoudenOptions _options;
    private readonly EboekhoudenSessionCache _session;
    private readonly TimeProvider _time;
    private readonly ILogger<EboekhoudenClient> _logger;

    public EboekhoudenClient(
        HttpClient http,
        IOptions<SiteOptions> options,
        EboekhoudenSessionCache session,
        TimeProvider time,
        ILogger<EboekhoudenClient> logger)
    {
        _http = http;
        _options = options.Value.Eboekhouden;
        _session = session;
        _time = time;
        _logger = logger;
        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    }

    public bool IsDemo => false;

    public async Task<IReadOnlyList<EboekInvoice>> GetInvoicesAsync(long relationId, CancellationToken ct = default)
    {
        var list = await GetAsync<EboekhoudenListResponse<EboekInvoice>>(
            $"v1/invoice?relationId={relationId}&limit=500", ct);
        return (list?.Items ?? []).OrderByDescending(i => i.Date).ToList();
    }

    public async Task<IReadOnlyList<EboekOutstandingInvoice>> GetOutstandingInvoicesAsync(long relationId, CancellationToken ct = default)
    {
        // credDeb=D: debiteuren (money owed to us by customers).
        var list = await GetAsync<EboekhoudenListResponse<EboekOutstandingInvoice>>(
            "v1/mutation/invoice/outstanding?credDeb=D&limit=2000", ct);
        return (list?.Items ?? []).Where(i => i.RelationId == relationId)
            .OrderByDescending(i => i.Date).ToList();
    }

    public Task<EboekRelation?> GetRelationAsync(long relationId, CancellationToken ct = default)
        => GetAsync<EboekRelation>($"v1/relation/{relationId}", ct);

    public async Task<EboekRelation?> FindRelationByEmailAsync(string email, CancellationToken ct = default)
    {
        // The list endpoint returns sparse objects (id/type/code only), so
        // fetch each relation's detail and compare email addresses there.
        var list = await GetAsync<EboekhoudenListResponse<EboekRelation>>("v1/relation?limit=500", ct);
        foreach (EboekRelation sparse in list?.Items ?? [])
        {
            ct.ThrowIfCancellationRequested();
            EboekRelation? relation = await GetRelationAsync(sparse.Id, ct);
            if (relation is null)
            {
                continue;
            }
            if (string.Equals(relation.EmailAddress, email, StringComparison.OrdinalIgnoreCase)
                || string.Equals(relation.EmailAddressInvoice, email, StringComparison.OrdinalIgnoreCase))
            {
                return relation;
            }
        }
        return null;
    }

    public async Task UpdateRelationAsync(long relationId, EboekRelationUpdate update, CancellationToken ct = default)
    {
        using HttpRequestMessage request = await BuildRequestAsync(HttpMethod.Patch, $"v1/relation/{relationId}", ct);
        request.Content = new StringContent(JsonSerializer.Serialize(update, JsonOptions), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await SendWithRetryAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("e-Boekhouden PATCH relation {RelationId} failed: {Status} {Body}", relationId, response.StatusCode, body);
            throw new InvalidOperationException($"e-Boekhouden weigerde de wijziging ({(int)response.StatusCode}).");
        }
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct) where T : class
    {
        using HttpRequestMessage request = await BuildRequestAsync(HttpMethod.Get, path, ct);
        using HttpResponseMessage response = await SendWithRetryAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(HttpMethod method, string path, CancellationToken ct)
    {
        string token = await _session.GetTokenAsync(AcquireSessionAsync, _time, ct);
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Authorization", token);
        return request;
    }

    /// <summary>Retries exactly once with a fresh session when the token is rejected.</summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response = await _http.SendAsync(request, ct);
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        _session.Invalidate();
        string token = await _session.GetTokenAsync(AcquireSessionAsync, _time, ct);
        using var retry = new HttpRequestMessage(request.Method, request.RequestUri);
        retry.Headers.TryAddWithoutValidation("Authorization", token);
        if (request.Content is not null)
        {
            string body = await request.Content.ReadAsStringAsync(ct);
            retry.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }
        return await _http.SendAsync(retry, ct);
    }

    private async Task<(string Token, int ExpiresInSeconds)> AcquireSessionAsync()
    {
        var payload = new { accessToken = _options.ApiToken, source = _options.Source };
        using HttpResponseMessage response = await _http.PostAsync("v1/session",
            new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string token = doc.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("e-Boekhouden session response had no token.");
        int expiresIn = doc.RootElement.TryGetProperty("expiresIn", out JsonElement e) ? e.GetInt32() : 600;
        _logger.LogInformation("Opened e-Boekhouden API session (expires in {Seconds}s)", expiresIn);
        return (token, expiresIn);
    }
}
