using System.IO.Compression;
using EshopGuard.Storage;

namespace EshopGuard.Billing.Invoicing;

/// <summary>
/// Writes the ZIP of invoices straight into the response (task 11.3): one PDF after another from the store, never the whole ZIP
/// in memory. The archive writes synchronously (also when an entry is closed), which the server forbids on the response, so it
/// writes into a buffer that is sent on asynchronously after every entry; the buffer holds at most one compressed PDF.
/// The files were checked by <see cref="InvoiceListService.ZipAsync"/>; a file gone since then breaks the download rather than
/// giving a ZIP that silently lacks a document.
/// </summary>
public sealed class InvoiceZipWriter(IBlobStore blobs)
{
    public async Task WriteAsync(Stream output, IReadOnlyList<InvoiceZipFile> files, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(files);
        await using var pending = new PendingStream();
        using (var archive = new ZipArchive(pending, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                await using (var source = await blobs.OpenReadAsync(file.Key, ct).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("invoice.pdf_file_missing"))
                {
                    await using var target = archive.CreateEntry(file.Name, CompressionLevel.Fastest).Open();
                    await source.CopyToAsync(target, ct).ConfigureAwait(false);
                }

                await pending.DrainAsync(output, ct).ConfigureAwait(false);
            }
        }

        await pending.DrainAsync(output, ct).ConfigureAwait(false);
    }

    /// <summary>A write-only stream that keeps what the archive writes until <see cref="DrainAsync"/> sends it on.</summary>
    private sealed class PendingStream : Stream
    {
        private readonly MemoryStream _buffer = new();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public async Task DrainAsync(Stream output, CancellationToken ct)
        {
            if (_buffer.Length == 0)
            {
                return;
            }

            await output.WriteAsync(_buffer.GetBuffer().AsMemory(0, (int)_buffer.Length), ct).ConfigureAwait(false);
            await output.FlushAsync(ct).ConfigureAwait(false);
            _buffer.SetLength(0);
        }

        public override void Write(byte[] buffer, int offset, int count) => _buffer.Write(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => _buffer.Write(buffer);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _buffer.Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _buffer.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _buffer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
