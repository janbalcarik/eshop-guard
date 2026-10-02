using EshopGuard.Application.Evidence;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Jobs.Evidence;

namespace EshopGuard.Application.Tests.Evidence;

/// <summary>The state of a piece of evidence at the edges of its validity (change 11, task 6.2; AD 10) and the check of files (6.1).</summary>
public sealed class EvidenceStatusCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);

    [Theory]
    [InlineData(0, "Expiring")]
    [InlineData(1, "Expiring")]
    [InlineData(30, "Expiring")]
    [InlineData(31, "Valid")]
    [InlineData(-1, "Expired")]
    public void DaysToTheEnd_DecideTheState(int days, string expected)
    {
        var (status, daysToExpiry) = EvidenceStatusCalculator.Calculate(EvidenceStatus.Valid, Today.AddDays(days), Today, 30);

        Assert.Equal(Enum.Parse<EvidenceStatus>(expected), status);
        Assert.Equal(days, daysToExpiry);
    }

    [Fact]
    public void WithoutAnEnd_IsValid_AndStatesOfQuestionsStay()
    {
        Assert.Equal((EvidenceStatus.Valid, (int?)null), EvidenceStatusCalculator.Calculate(EvidenceStatus.Expired, null, Today, 30));
        Assert.Equal(EvidenceStatus.ClaimRemoved, EvidenceStatusCalculator.Calculate(EvidenceStatus.ClaimRemoved, Today.AddDays(-5), Today, 30).Status);
        Assert.Equal(EvidenceStatus.AwaitingAnswer, EvidenceStatusCalculator.Calculate(EvidenceStatus.AwaitingAnswer, null, Today, 30).Status);
    }

    [Fact]
    public void TodayIsTheDayInBratislava()
    {
        // 30. 9. 22:30 UTC is already 1. 10. in Bratislava (CEST, UTC+2).
        Assert.Equal(new DateOnly(2026, 10, 1), EvidenceStatusCalculator.Today(new DateTimeOffset(2026, 9, 30, 22, 30, 0, TimeSpan.Zero), "Europe/Bratislava"));
        Assert.Equal(new DateOnly(2027, 9, 30), EvidenceStatusCalculator.Date(EvidenceStatusCalculator.Stored(new DateOnly(2027, 9, 30))));
    }

    [Theory]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, "application/pdf")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C }, null)]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, null)]
    public void FileType_IsByContent(byte[] header, string? expected) =>
        Assert.Equal(expected, EvidenceFileValidator.Detect(header)?.ContentType);

    [Theory]
    [InlineData("Certifikát Vegan.pdf", "Certifikat-Vegan.pdf")]
    [InlineData("../../etc/passwd", "passwd.pdf")]
    [InlineData("C:\\Users\\jana\\Doklad BDIH 2026.PDF", "Doklad-BDIH-2026.pdf")]
    [InlineData("", "doklad.pdf")]
    [InlineData("...", "doklad.pdf")]
    public void FileName_IsSafe(string name, string expected) =>
        Assert.Equal(expected, EvidenceFileValidator.SafeName(name, EvidenceFileValidator.Pdf));
}
