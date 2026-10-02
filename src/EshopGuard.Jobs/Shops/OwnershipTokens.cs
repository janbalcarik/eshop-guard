namespace EshopGuard.Jobs.Shops;

/// <summary>Where the token of a verification of ownership stands (change 10, AD 9).</summary>
public static class OwnershipTokens
{
    /// <summary>The name of the meta tag in the <c>&lt;head&gt;</c> of the home page.</summary>
    public const string MetaName = "eshopguard-site-verification";

    /// <summary>The prefix of the value of the TXT record.</summary>
    public const string DnsPrefix = "eshopguard-site-verification=";

    public const string MetaNotFound = "meta_not_found";
    public const string DnsRecordNotFound = "dns_record_not_found";
    public const string TokenMismatch = "token_mismatch";
    public const string FetchFailed = "fetch_failed";

    /// <summary><c>&lt;meta name="eshopguard-site-verification" content="{token}"&gt;</c>.</summary>
    public static string MetaTag(string token) => $"<meta name=\"{MetaName}\" content=\"{token}\">";

    /// <summary>The name of the TXT record: <c>_eshopguard.{domain}</c>.</summary>
    public static string DnsName(string domain) => "_eshopguard." + domain;

    /// <summary>The value of the TXT record.</summary>
    public static string DnsValue(string token) => DnsPrefix + token;
}

/// <summary>TXT records of a name (the worker's resolver; tests replace it).</summary>
public interface IDnsTxtResolver
{
    /// <summary>The texts of the TXT records; empty when the name has none or does not exist. Network errors are thrown.</summary>
    Task<IReadOnlyList<string>> TxtAsync(string name, CancellationToken ct);
}

/// <summary>TXT records through <c>DnsClient</c> and the resolvers of the system.</summary>
public sealed class DnsClientTxtResolver : IDnsTxtResolver
{
    private readonly DnsClient.LookupClient _client = new(new DnsClient.LookupClientOptions
    {
        UseCache = false,
        Timeout = TimeSpan.FromSeconds(5),
        Retries = 1,
        ThrowDnsErrors = false,
    });

    public async Task<IReadOnlyList<string>> TxtAsync(string name, CancellationToken ct)
    {
        var answer = await _client.QueryAsync(name, DnsClient.QueryType.TXT, cancellationToken: ct).ConfigureAwait(false);
        return answer.Answers.TxtRecords().Select(r => string.Concat(r.Text)).ToList();
    }
}
