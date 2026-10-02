using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Findings;

/// <summary>
/// Search (Ctrl K, change 11, AD 15) in one e-shop under RLS: at most 10 pages by title or address and at most 10 findings by
/// text, the strictest first. The text must have at least 2 characters (<c>400 search.query_too_short</c>).
/// </summary>
public sealed class SearchService(EshopGuardDb db, ShopWorkLoader loader)
{
    public const int MaxResults = 10;

    public async Task<SearchResultDto> SearchAsync(Guid shopId, string? q, CancellationToken ct)
    {
        var text = q?.Trim() ?? "";
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var work = await loader.LoadAsync(shopId, ct).ConfigureAwait(false);
            if (text.Length < 2)
            {
                throw new DomainException(ProblemCodes.SearchQueryTooShort, 400);
            }

            var pages = db.Pages.Where(p => p.ShopId == shopId
                    && ((p.Title != null && EF.Functions.ILike(p.Title, "%" + Escape(text) + "%", "\\"))
                        || (p.Path != null && EF.Functions.ILike(p.Path, "%" + Escape(text) + "%", "\\"))))
                .OrderBy(p => p.Title).ThenBy(p => p.Id).Take(MaxResults)
                .Select(p => new PageRefDto(p.Id, p.Title, p.Path, p.Url, p.Language));
            var pageList = await pages.ToListAsync(ct).ConfigureAwait(false);
            var findings = FindingQueryService.Rows(work, new FindingFilter(null, null, null, null, null, text, null), null)
                .Take(MaxResults).Select(r => r.Item).ToList();
            return new SearchResultDto(pageList, findings);
        }, ct).ConfigureAwait(false);
    }

    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
