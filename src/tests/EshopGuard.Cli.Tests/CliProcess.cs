using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace EshopGuard.Cli.Tests;

/// <summary>Runs the built CLI in a working folder of its own (with copies of config/ and rules/, no .env).</summary>
internal static class CliProcess
{
    /// <summary>The folder <c>src/</c> of the repository.</summary>
    public static string SourceRoot
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "EshopGuard.sln")))
                {
                    return dir.FullName;
                }
            }

            throw new DirectoryNotFoundException("EshopGuard.sln not found above the test output.");
        }
    }

    /// <summary>A fresh working folder with config/ and rules/ of the repository.</summary>
    public static string NewWorkingFolder()
    {
        var folder = Directory.CreateTempSubdirectory("eshopguard-cli-").FullName;
        foreach (var name in new[] { "config", "rules" })
        {
            var source = Path.Combine(SourceRoot, name);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(folder, name, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }

        return folder;
    }

    public static async Task<(int ExitCode, string Output)> RunAsync(string workingFolder, IDictionary<string, string?> environment, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingFolder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "eshopguard.dll"));
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, await stdout + await stderr);
    }

    /// <summary>The fixture e-shop on a free local port; the requests it served are recorded.</summary>
    public static (string Url, ConcurrentQueue<string> Requests, CancellationTokenSource Stop) StartFixture(string site = "site")
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var server = new FixtureServer(Path.Combine(SourceRoot, "tests", "EshopGuard.Core.Tests", "Fixtures", site), port);
        var requests = new ConcurrentQueue<string>();
        var stop = new CancellationTokenSource();
        _ = Task.Run(() => server.RunAsync(requests.Enqueue, stop.Token));
        return (server.Prefix, requests, stop);
    }
}
