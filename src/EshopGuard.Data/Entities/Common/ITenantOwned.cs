namespace EshopGuard.Data.Entities.Common;

/// <summary>
/// Row of a tenant: the table has a <c>tenant_id</c> column (mapped property <c>TenantId</c>) and Row-Level Security
/// with the policy <c>tenant_isolation</c>. The EF filter <c>"Tenant"</c> and the save interceptor apply to these types.
/// </summary>
/// <remarks>
/// A marker, because <c>ops.audit_log</c> has a nullable <c>tenant_id</c> (system actions); everything else reads the
/// property <c>TenantId</c> through EF metadata. Global tables that carry a <c>tenant_id</c> (e.g. <c>ops.jobs</c>)
/// do not implement this interface.
/// </remarks>
public interface ITenantOwned;
