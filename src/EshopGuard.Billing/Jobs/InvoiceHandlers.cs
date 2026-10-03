using EshopGuard.Billing.Invoicing;
using EshopGuard.Billing.Stripe;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Processing;

namespace EshopGuard.Billing.Jobs;

/// <summary>
/// <c>billing.issue_invoice</c>: the invoice of a paid invoice of Stripe with its tax check (<see cref="InvoiceIssuer"/>). Errors of
/// Stripe that may pass later are tried again; an invoice without a known e-shop fails with an alert of operations.
/// </summary>
public sealed class IssueInvoiceHandler(InvoiceIssuer issuer) : IJobHandler
{
    public string Kind => BillingJobs.IssueInvoiceKind;

    public JobResourceClass ResourceClass => JobResourceClass.Io;

    public Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Job.TenantId is not { } tenantId)
        {
            return Task.FromResult<JobResult>(new JobResult.Fail("billing.tenant_missing"));
        }

        return RunAsync(tenantId, context.Job.ShopId, context.Job.Payload.RootElement.GetProperty("stripe_invoice_id").GetString()!, ct);
    }

    public async Task<JobResult> RunAsync(Guid tenantId, Guid? shopId, string stripeInvoiceId, CancellationToken ct)
    {
        try
        {
            var outcome = await issuer.IssueAsync(tenantId, shopId, stripeInvoiceId, ct).ConfigureAwait(false);
            return outcome is { Status: InvoiceIssueOutcome.NotCreated, Code: { } code } ? new JobResult.Fail(code) : JobResult.Done;
        }
        catch (StripeGatewayException e)
        {
            return e.Transient ? new JobResult.Retry(e.Code) : new JobResult.Fail(e.Code);
        }
    }
}
