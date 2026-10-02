using System.Text.Json;
using EshopGuard.Storage;
using Microsoft.AspNetCore.DataProtection;

namespace EshopGuard.Api.Http;

/// <summary>
/// Short-lived links to stored files (evidence, protocols; change 11, AD 10 and 11): a store that signs its own links gives
/// one; the file system cannot, so the API gives a link of its own, <c>/api/files/{token}</c>, whose token is protected by
/// Data Protection and valid for <see cref="ValidFor"/> (key, file name and type inside, nothing readable). The file goes out as
/// an attachment. The token is the permission: it is issued only after the check of the tenant and the role.
/// </summary>
public sealed class BlobLinks(IDataProtectionProvider protection, IBlobStore blobs, TimeProvider time)
{
    public static readonly TimeSpan ValidFor = TimeSpan.FromMinutes(5);

    public const string Path = "/api/files/";

    private ITimeLimitedDataProtector Protector => protection.CreateProtector("EshopGuard.BlobLinks.v1").ToTimeLimitedDataProtector();

    public async Task<string> LinkAsync(BlobKey key, string fileName, string contentType, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (await blobs.GetReadUrlAsync(key, ValidFor, ct).ConfigureAwait(false) is { } signed)
        {
            return signed.ToString();
        }

        var payload = JsonSerializer.Serialize(new LinkPayload(key.Value, fileName, contentType));
        return Path + Protector.Protect(payload, time.GetUtcNow() + ValidFor);
    }

    /// <summary>The file of a valid token; null when the token is forged, expired or names no file of a tenant.</summary>
    public LinkPayload? Read(string token)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<LinkPayload>(Protector.Unprotect(token, out _));
            return payload is not null && Key(payload.Key) is not null ? payload : null;
        }
        catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }

    /// <summary>A key of a tenant from its text (only under <c>tenants/{id}/</c>).</summary>
    public static BlobKey? Key(string value)
    {
        var segments = value.Split('/');
        if (segments.Length < 3 || segments[0] != "tenants" || !Guid.TryParseExact(segments[1], "D", out var tenantId))
        {
            return null;
        }

        try
        {
            return BlobKey.ForTenant(tenantId, segments[2..]);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public sealed record LinkPayload(string Key, string Name, string Type);
}

/// <summary>The download of a link of <see cref="BlobLinks"/>.</summary>
public static class FileEndpoints
{
    public static RouteGroupBuilder MapFileEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/files/{token}", async (string token, HttpContext context, BlobLinks links, IBlobStore blobs, CancellationToken ct) =>
            {
                if (links.Read(token) is not { } file || BlobLinks.Key(file.Key) is not { } key || await blobs.OpenReadAsync(key, ct) is not { } stream)
                {
                    return Results.NotFound();
                }

                context.Response.Headers.CacheControl = "private, no-store";
                return Results.File(stream, file.Type, file.Name);
            })
            .RequireAuthorization()
            .WithTags("files");
        return api;
    }
}
