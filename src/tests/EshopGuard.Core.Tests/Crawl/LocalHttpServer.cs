using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EshopGuard.Core.Tests;

/// <summary>
/// A minimal HTTP/1.1 server on 127.0.0.1 for tests of the real fetcher: every request gets the answer of the handler,
/// the request line and headers are recorded.
/// </summary>
internal sealed class LocalHttpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly Func<Request, Answer> _handler;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    public LocalHttpServer(Func<Request, Answer> handler)
    {
        _handler = handler;
        _listener.Start();
        _loop = Task.Run(AcceptAsync);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public ConcurrentQueue<Request> Requests { get; } = new();

    public sealed record Request(string Method, string Path, IReadOnlyDictionary<string, string> Headers);

    public sealed record Answer(int Status, string Body = "", string ContentType = "text/html; charset=utf-8", IReadOnlyDictionary<string, string>? Headers = null);

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            var buffer = new byte[16384];
            var received = new StringBuilder();
            while (!received.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                var read = await stream.ReadAsync(buffer, _stop.Token);
                if (read == 0)
                {
                    return;
                }

                received.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            var lines = received.ToString().Split("\r\n");
            var parts = lines[0].Split(' ');
            var headers = lines.Skip(1).TakeWhile(l => l.Length > 0)
                .Select(l => l.Split(':', 2))
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);
            var request = new Request(parts[0], parts[1], headers);
            Requests.Enqueue(request);
            var answer = _handler(request);
            var body = Encoding.UTF8.GetBytes(answer.Body);
            var head = new StringBuilder($"HTTP/1.1 {answer.Status} X\r\nConnection: close\r\nContent-Length: {body.Length}\r\n");
            if (answer.Status != 304)
            {
                head.Append($"Content-Type: {answer.ContentType}\r\n");
            }

            foreach (var (name, value) in answer.Headers ?? new Dictionary<string, string>())
            {
                head.Append($"{name}: {value}\r\n");
            }

            head.Append("\r\n");
            await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString()), _stop.Token);
            if (answer.Status != 304)
            {
                await stream.WriteAsync(body, _stop.Token);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
        }

        _stop.Dispose();
    }
}
