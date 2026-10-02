using System.Text.Json;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Rules;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Runs;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>The rules of errors of the steps (design of change 8, "Chyby a samooprava"; task 11.5).</summary>
public sealed class StepErrorPolicyTests
{
    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(0)]
    public void ServiceFailures_ThatMayPassLater_AreTransient(int status)
    {
        Assert.Equal(StepErrorKind.Transient, StepErrorPolicy.OfService(status, fatal: false));
        Assert.Equal(StepErrorKind.Transient, StepErrorPolicy.Of(new JevApiException("x", status, null, isFatal: false)));
        Assert.Equal(StepErrorKind.Transient, StepErrorPolicy.Of(new RewriteApiException("x", status, isFatal: false)));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(402)]
    [InlineData(403)]
    [InlineData(429)]
    public void RejectedKeyOrCredit_IsFatal(int status)
    {
        // The clients decide what is fatal (401, 403, 429 insufficient_quota of OpenAI, 402 of Jev).
        Assert.Equal(StepErrorKind.Fatal, StepErrorPolicy.Of(new JevApiException("x", status, null, isFatal: true)));
        Assert.Equal(StepErrorKind.Fatal, StepErrorPolicy.Of(new RewriteApiException("x", status, isFatal: true)));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(422)]
    public void ServiceRefusingOneItem_IsPermanentForTheItem(int status) =>
        Assert.Equal(StepErrorKind.PermanentItem, StepErrorPolicy.Of(new JevApiException("x", status, null, isFatal: false)));

    [Fact]
    public void OtherExceptions_AreSortedByWhatTheyBreak()
    {
        Assert.Equal(StepErrorKind.Transient, StepErrorPolicy.Of(new TransientStepException("jev.unavailable")));
        Assert.Equal(StepErrorKind.PermanentRun, StepErrorPolicy.Of(new RuleValidationException(["broken"])));
        Assert.Equal(StepErrorKind.Infrastructure, StepErrorPolicy.Of(new IOException("disk")));
        Assert.Equal(StepErrorKind.Unexpected, StepErrorPolicy.Of(new InvalidOperationException("bug")));
    }

    [Theory]
    [InlineData(404, null, RunUrlState.Gone)]
    [InlineData(410, null, RunUrlState.Gone)]
    [InlineData(200, "too_large", RunUrlState.TooLarge)]
    [InlineData(500, "http_500", RunUrlState.Failed)]
    [InlineData(429, "http_429", RunUrlState.Failed)]
    [InlineData(null, "timeout", RunUrlState.Failed)]
    public void FailedPage_GetsAStateByItsAnswer(int? status, string? code, RunUrlState expected) =>
        Assert.Equal(expected, StepErrorPolicy.OfShopFailure(status, code));

    [Theory]
    [InlineData(404, StepErrorKind.PermanentItem)]
    [InlineData(410, StepErrorKind.PermanentItem)]
    [InlineData(403, StepErrorKind.PermanentItem)]
    [InlineData(408, StepErrorKind.Transient)]
    [InlineData(429, StepErrorKind.Transient)]
    [InlineData(503, StepErrorKind.Transient)]
    public void ShopAnswers_AreTransientOrFinal(int status, StepErrorKind expected) =>
        Assert.Equal(expected, StepErrorPolicy.OfShop(status));

    [Fact]
    public void BlockedAddress_IsFinalForItsPage()
    {
        var page = new FetchedPage(new Uri("http://shop.test/a"), new Uri("http://shop.test/a"), FetchOutcome.Blocked, false, 1);

        Assert.Equal((RunUrlState.SsrfBlocked, "ssrf_blocked"), RunPages.Outcome(page));
    }

    [Fact]
    public void FailedPage_KeepsItsCode()
    {
        var gone = new FetchedPage(new Uri("http://shop.test/a"), new Uri("http://shop.test/a"), FetchOutcome.Failed, false, 1) { HttpStatus = 404, FailureCode = "http_404" };
        var unknown = new FetchedPage(new Uri("http://shop.test/b"), new Uri("http://shop.test/b"), FetchOutcome.Failed, false, 1);

        Assert.Equal((RunUrlState.Gone, "http_404"), RunPages.Outcome(gone));
        Assert.Equal((RunUrlState.Failed, "fetch_failed"), RunPages.Outcome(unknown));
    }

    [Theory]
    [InlineData(404, null, false, "http_404")]
    [InlineData(429, null, false, "http_429")]
    [InlineData(200, "larger than 5 bytes", true, "too_large")]
    [InlineData(0, "timeout", false, "timeout")]
    [InlineData(0, "Connection refused", false, "network_error")]
    public void FailureCode_OfAResponse(int status, string? error, bool tooLarge, string expected) =>
        Assert.Equal(expected, FetchStep.FailureCode(new FetchResponse { Url = new Uri("http://shop.test/"), StatusCode = status, Error = error, TooLarge = tooLarge }));

    [Theory]
    [InlineData(1, 6, 3, true)]
    [InlineData(5, 6, 1, true)]
    [InlineData(6, 6, 1, false)]
    [InlineData(1, 6, 0, false)]
    public void BatchWithTransientErrors_IsRepeatedUntilItsLastAttempt(int attempt, int maxAttempts, int transient, bool repeat)
    {
        var job = new ClaimedJob(1, RunJobKinds.Evaluate, JobResourceClass.Jev, JobPriority.P2, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            JsonDocument.Parse("{}"), attempt, maxAttempts, null, "w0");

        Assert.Equal(repeat, StepErrorPolicy.RepeatBatch(transient, job));
        Assert.Equal(attempt >= maxAttempts, StepErrorPolicy.IsLastAttempt(job));
    }

    [Fact]
    public void ServiceErrors_OfTheLibrary_MatchThePolicy()
    {
        Assert.True(ServiceErrors.IsTransient(new HttpRequestException("reset")));
        Assert.True(ServiceErrors.IsTransient(new JevApiException("x", 503, null, isFatal: false)));
        Assert.False(ServiceErrors.IsTransient(new JevApiException("x", 503, null, isFatal: true)));
        Assert.False(ServiceErrors.IsTransient(new RewriteApiException("x", 200, isFatal: false)));
    }
}
