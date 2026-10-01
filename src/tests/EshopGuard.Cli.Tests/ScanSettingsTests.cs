using EshopGuard.Cli.Commands;
using EshopGuard.Core.Crawl;

namespace EshopGuard.Cli.Tests;

public sealed class ScanSettingsTests
{
    [Fact]
    public void RecordAndReplayTogether_AreRefused()
    {
        var result = new ScanSettings { Url = "https://shop.test/", Record = "snapshots/a", Replay = "snapshots/b" }.Validate();

        Assert.False(result.Successful);
        Assert.Contains("--record a --replay", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayWithoutRecording_IsRefused()
    {
        var empty = Directory.CreateTempSubdirectory("eshopguard-replay-").FullName;

        var result = new ScanSettings { Url = "https://shop.test/", Replay = empty }.Validate();

        Assert.False(result.Successful);
        Assert.Contains(PageRecording.IndexFile, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayWithRecording_IsAccepted()
    {
        var folder = Directory.CreateTempSubdirectory("eshopguard-replay-").FullName;
        File.WriteAllText(Path.Combine(folder, PageRecording.IndexFile), "");

        Assert.True(new ScanSettings { Url = "https://shop.test/", Replay = folder }.Validate().Successful);
        Assert.True(new ScanSettings { Url = "https://shop.test/", Record = folder }.Validate().Successful);
    }
}
