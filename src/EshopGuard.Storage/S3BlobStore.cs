using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace EshopGuard.Storage;

/// <summary>
/// Files in a private S3 bucket (S3 at the provider, MinIO in development). Read links are pre-signed and short-lived.
/// </summary>
public sealed class S3BlobStore : IBlobStore, IBlobStoreProbe, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly TimeSpan _maxSignedUrl;
    private readonly bool _http;

    /// <summary>Creates the client from the options (keys must be present).</summary>
    public S3BlobStore(S3StorageOptions options, int maxSignedUrlMinutes)
    {
        ArgumentNullException.ThrowIfNull(options);
        _bucket = options.Bucket ?? throw new StorageConfigurationException(StorageErrorCodes.KeyMissing, "Storage:S3:Bucket");
        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region,
            // Newer SDKs send checksums some S3-compatible stores reject; only when the operation requires them.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        if (string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }
        else
        {
            config.ServiceURL = options.ServiceUrl;
            _http = options.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        _client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
        _maxSignedUrl = TimeSpan.FromMinutes(maxSignedUrlMinutes);
    }

    /// <inheritdoc />
    public async Task PutAsync(BlobKey key, Stream content, string contentType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var file = RequireFile(key);
        Stream body = content;
        MemoryStream? buffer = null;
        if (!content.CanSeek)
        {
            // S3 needs the length up front; buffer streams that cannot report it.
            buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct).ConfigureAwait(false);
            buffer.Position = 0;
            body = buffer;
        }

        try
        {
            await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                Key = file,
                InputStream = body,
                ContentType = contentType,
                AutoCloseStream = false,
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            if (buffer is not null)
            {
                await buffer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task<Stream?> OpenReadAsync(BlobKey key, CancellationToken ct = default)
    {
        var file = RequireFile(key);
        try
        {
            var response = await _client.GetObjectAsync(_bucket, file, ct).ConfigureAwait(false);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(BlobKey key, CancellationToken ct = default)
    {
        var file = RequireFile(key);
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, file, ct).ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(BlobKey key, CancellationToken ct = default) =>
        _client.DeleteObjectAsync(_bucket, RequireFile(key), ct);

    /// <inheritdoc />
    public async Task<int> DeletePrefixAsync(BlobKey prefix, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (!prefix.IsPrefix)
        {
            throw new ArgumentException("A prefix key is required (BlobKey.Prefix).", nameof(prefix));
        }

        var deleted = 0;
        var request = new ListObjectsV2Request { BucketName = _bucket, Prefix = prefix.Value, MaxKeys = 1000 };
        while (true)
        {
            // Always list from the start: the previous page was deleted, so no continuation token is needed.
            var page = await _client.ListObjectsV2Async(request, ct).ConfigureAwait(false);
            var objects = page.S3Objects ?? [];
            if (objects.Count == 0)
            {
                return deleted;
            }

            var response = await _client.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = _bucket,
                Objects = objects.Select(o => new KeyVersion { Key = o.Key }).ToList(),
                Quiet = false,
            }, ct).ConfigureAwait(false);
            if (response.DeleteErrors is { Count: > 0 })
            {
                throw new IOException($"S3 refused to delete {response.DeleteErrors.Count} objects under the prefix.");
            }

            deleted += response.DeletedObjects?.Count ?? 0;
        }
    }

    /// <inheritdoc />
    public async Task<Uri?> GetReadUrlAsync(BlobKey key, TimeSpan validFor, CancellationToken ct = default)
    {
        var file = RequireFile(key);
        if (validFor <= TimeSpan.Zero || validFor > _maxSignedUrl)
        {
            throw new ArgumentOutOfRangeException(nameof(validFor), $"A signed link may be valid for at most {_maxSignedUrl.TotalMinutes} minutes.");
        }

        var url = await _client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = file,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(validFor),
            Protocol = _http ? Protocol.HTTP : Protocol.HTTPS,
        }).ConfigureAwait(false);
        return new Uri(url);
    }

    /// <inheritdoc />
    public async Task<string?> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            await _client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket, MaxKeys = 1, Prefix = "health/" }, ct).ConfigureAwait(false);
            return null;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchBucket")
        {
            return StorageErrorCodes.BucketMissing;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            return StorageErrorCodes.AccessDenied;
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException or HttpRequestException or IOException or TimeoutException)
        {
            return StorageErrorCodes.Unreachable;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    private static string RequireFile(BlobKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.IsPrefix ? throw new ArgumentException("A file key is required, not a prefix.", nameof(key)) : key.Value;
    }
}
