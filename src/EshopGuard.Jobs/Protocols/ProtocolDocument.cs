namespace EshopGuard.Jobs.Protocols;

/// <summary>
/// The content of a protocol of the checks (change 11, AD 11; design <c>Protocol.dc.html</c>), every text already in the
/// language of the protocol, so the renderer only lays it out. It is built when the protocol is requested (the state on the day
/// of issue) and kept next to the PDF as JSON. Legal references stay in the language of the law.
/// </summary>
public sealed record ProtocolDocument(
    string Number,
    string Locale,
    string Title,
    string Heading,
    string NumberLine,
    string IssuedLine,
    IReadOnlyList<ProtocolFact> Facts,
    IReadOnlyList<ProtocolSummaryItem> Summary,
    ProtocolTable Decisions,
    ProtocolSection Evidence,
    string Disclaimer,
    string FileName);

/// <summary>A line of the head (<c>E-shop</c>, <c>Obdobie</c>, <c>Úvodná kontrola</c>, <c>Pravidlá</c>…): label and value.</summary>
public sealed record ProtocolFact(string Code, string Label, string Value);

/// <summary>A number of the summary with its label (<c>43</c> „nálezov pri úvodnej kontrole“).</summary>
public sealed record ProtocolSummaryItem(string Code, int Value, string Label);

/// <summary>The table of decisions and fixes: headers and rows (date, pages, originally, solution, point of the law).</summary>
public sealed record ProtocolTable(string Title, IReadOnlyList<string> Headers, IReadOnlyList<ProtocolRow> Rows, string? Empty);

public sealed record ProtocolRow(string Date, string Pages, string Before, string Fix, string Reference);

/// <summary>A titled list of lines (the evidence of the operator); <see cref="Empty"/> when it has none.</summary>
public sealed record ProtocolSection(string Title, IReadOnlyList<string> Lines, string? Empty);
