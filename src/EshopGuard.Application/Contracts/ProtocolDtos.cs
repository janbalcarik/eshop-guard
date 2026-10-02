using System.Text.Json;

namespace EshopGuard.Application.Contracts;

/// <summary>
/// A protocol of the checks (change 11, AD 11): <c>rendering</c> until the worker stores the PDF, then <c>ready</c>, or
/// <c>failed</c> with <see cref="ErrorCode"/>. <see cref="Summary"/> holds the numbers of the protocol (codes and counts).
/// </summary>
public sealed record ProtocolDto(
    Guid Id, string Number, DateOnly PeriodFrom, DateOnly PeriodTo, string Locale, string Status, string? ErrorCode, Guid? GeneratedBy,
    DateTimeOffset CreatedAt, JsonElement? Summary);
