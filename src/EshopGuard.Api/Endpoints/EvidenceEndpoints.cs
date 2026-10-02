using System.Text.Json;
using EshopGuard.Api.Contracts;
using EshopGuard.Api.Http;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Evidence;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace EshopGuard.Api.Endpoints;

/// <summary>The evidence of the tenant (change 11, design G): every member reads, the editor and above change it.</summary>
public static class EvidenceEndpoints
{
    public static RouteGroupBuilder MapEvidenceEndpoints(this RouteGroupBuilder tenant)
    {
        tenant.MapGet("/evidence", async (string? status, string? q, Guid? shopId, EvidenceService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(status, q, shopId, ct)))
            .WithTags("evidence")
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ValidationFailed);

        tenant.MapPost("/evidence", async (Guid tenantId, HttpContext context, EvidenceService service, IOptions<JsonOptions> json, CancellationToken ct) =>
            {
                if (!context.Request.HasFormContentType)
                {
                    throw DomainException.Validation(new ValidationResult().Add("metadata", ProblemCodes.Fields.Required));
                }

                var form = await context.Request.ReadFormAsync(ct);
                CreateEvidenceRequest? metadata;
                try
                {
                    metadata = form["metadata"].FirstOrDefault() is { Length: > 0 } text
                        ? JsonSerializer.Deserialize<CreateEvidenceRequest>(text, json.Value.SerializerOptions)
                        : null;
                }
                catch (JsonException)
                {
                    metadata = null;
                }

                if (metadata is null)
                {
                    throw DomainException.Validation(new ValidationResult().Add("metadata", ProblemCodes.Fields.Required));
                }

                var file = form.Files.GetFile("file");
                await using var content = file?.OpenReadStream();
                var created = await service.CreateAsync(tenantId, context.User.RequireUserId(),
                    new EvidenceInput(metadata.ClaimText, metadata.SubjectKind, metadata.SubjectLabel, metadata.Kind, metadata.Title, metadata.ValidFrom, metadata.ValidUntil,
                        metadata.RegistryRef),
                    file is null ? null : new EvidenceUpload(content!, file.FileName, file.Length), ct);
                context.SetETag(created.Version);
                return TypedResults.Created($"/api/t/{tenantId}/evidence/{created.Id}", created);
            })
            .WithTags("evidence")
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.EvidenceClaimRequired, ProblemCodes.EvidenceFileTypeNotAllowed, ProblemCodes.EvidenceFileTooLarge,
                ProblemCodes.EvidenceValidUntilBeforeFrom, ProblemCodes.ValidationFailed);

        tenant.MapGet("/evidence/{evidenceId:guid}", async (Guid evidenceId, HttpContext context, EvidenceService service, CancellationToken ct) =>
                Tagged(context, await service.DetailAsync(evidenceId, ct)))
            .WithTags("evidence")
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.EvidenceNotFound);

        tenant.MapPatch("/evidence/{evidenceId:guid}", async (Guid tenantId, Guid evidenceId, UpdateEvidenceRequest? body, HttpContext context, EvidenceService service,
                CancellationToken ct) =>
                Tagged(context, await service.UpdateAsync(tenantId, context.User.RequireUserId(), evidenceId,
                    new EvidencePatch(body?.ClaimText, body?.SubjectKind, body?.SubjectLabel, body?.Title, body?.ValidFrom, body?.ValidUntil), context.IfMatch(), ct)))
            .WithTags("evidence")
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.EvidenceNotFound, ProblemCodes.EvidenceClaimRequired, ProblemCodes.EvidenceValidUntilBeforeFrom,
                ProblemCodes.ConcurrencyConflict, ProblemCodes.ValidationFailed);

        tenant.MapDelete("/evidence/{evidenceId:guid}", async (Guid tenantId, Guid evidenceId, HttpContext context, EvidenceService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(tenantId, context.User.RequireUserId(), evidenceId, ct);
                return TypedResults.NoContent();
            })
            .WithTags("evidence")
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.EvidenceNotFound);

        tenant.MapGet("/evidence/{evidenceId:guid}/file", async (Guid evidenceId, EvidenceService service, BlobLinks links, CancellationToken ct) =>
            {
                var file = await service.FileAsync(evidenceId, ct);
                return TypedResults.Redirect(await links.LinkAsync(file.Key, file.FileName, file.ContentType, ct));
            })
            .WithTags("evidence")
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.EvidenceNotFound, ProblemCodes.EvidenceNoFile);

        tenant.MapPost("/evidence/{evidenceId:guid}/links", async (Guid tenantId, Guid evidenceId, EvidenceLinksRequest? body, HttpContext context, EvidenceService service,
                CancellationToken ct) =>
                Tagged(context, await service.LinkAsync(tenantId, context.User.RequireUserId(), evidenceId, body?.FindingIds, ct)))
            .WithTags("evidence")
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.EvidenceNotFound, ProblemCodes.FindingNotFound, ProblemCodes.FindingTransitionNotAllowed, ProblemCodes.ShopSampleOnly,
                ProblemCodes.ValidationFailed);

        tenant.MapDelete("/evidence/{evidenceId:guid}/links/{linkId:guid}", async (Guid tenantId, Guid evidenceId, Guid linkId, HttpContext context, EvidenceService service,
                CancellationToken ct) =>
            {
                await service.UnlinkAsync(tenantId, context.User.RequireUserId(), evidenceId, linkId, ct);
                return TypedResults.NoContent();
            })
            .WithTags("evidence")
            .RequireTenantRole(TenantRole.Editor)
            .ProducesProblemCodes(ProblemCodes.EvidenceNotFound);
        return tenant;
    }

    private static Ok<EvidenceDto> Tagged(HttpContext context, EvidenceDto evidence)
    {
        context.SetETag(evidence.Version);
        return TypedResults.Ok(evidence);
    }
}
