namespace EshopGuard.Storage.Tests;

public sealed class FileSystemBlobStoreTests : BlobStoreContractTests, IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("eshopguard-blobs-");
    private readonly FileSystemBlobStore _store;

    public FileSystemBlobStoreTests() => _store = new FileSystemBlobStore(_root.FullName);

    protected override IBlobStore Store => _store;

    [Fact]
    public async Task CancelledWrite_LeavesNoFileAndNoTemporaryFile()
    {
        var key = BlobKey.ForTenant(Guid.CreateVersion7(), "big.bin");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.PutAsync(key, new FailingStream(), "application/octet-stream", Ct));
        Assert.Null(await _store.OpenReadAsync(key, Ct));
        Assert.Empty(Directory.EnumerateFiles(_root.FullName, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task FailedOverwrite_KeepsPreviousContent()
    {
        var key = BlobKey.ForTenant(Guid.CreateVersion7(), "keep.bin");
        await _store.PutAsync(key, new MemoryStream([1, 2, 3]), "application/octet-stream", Ct);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.PutAsync(key, new FailingStream(), "application/octet-stream", Ct));
        Assert.Equal([1, 2, 3], await ReadAllAsync(_store, key));
    }

    [Fact]
    public async Task ReadUrl_IsNotSupported()
    {
        Assert.Null(await _store.GetReadUrlAsync(BlobKey.ForTenant(Guid.CreateVersion7(), "a"), TimeSpan.FromMinutes(1), Ct));
    }

    [Fact]
    public async Task Files_StayUnderRoot()
    {
        var key = BlobKey.ForShop(Guid.CreateVersion7(), Guid.CreateVersion7(), "pages", "a.html");
        await _store.PutAsync(key, new MemoryStream([1]), "text/html", Ct);
        var file = Assert.Single(Directory.EnumerateFiles(_root.FullName, "*", SearchOption.AllDirectories));
        Assert.StartsWith(_root.FullName, file, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("pages", "a.html"), file, StringComparison.Ordinal);
    }

    public void Dispose() => _root.Delete(recursive: true);

    /// <summary>Delivers some bytes, then fails as a cancelled download would.</summary>
    private sealed class FailingStream : Stream
    {
        private int _reads;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_reads++ > 0)
            {
                throw new OperationCanceledException();
            }

            buffer.Span[..100].Fill(42);
            return ValueTask.FromResult(100);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
