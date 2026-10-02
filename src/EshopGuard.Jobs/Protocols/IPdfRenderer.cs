namespace EshopGuard.Jobs.Protocols;

/// <summary>
/// Lays a protocol out as a PDF (change 11, task 9.3). The library is K rozhodnutí 6 of change 11 (QuestPDF or
/// PdfSharp/MigraDoc, licence to be checked first); until it is decided no implementation is registered and the job
/// <c>protocol.render</c> ends <c>failed</c> with <c>pdf_renderer_unavailable</c> (the protocol says so, nothing is hidden).
/// </summary>
public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(ProtocolDocument document, CancellationToken ct);
}
