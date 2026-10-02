using System.Net;
using System.Text;

namespace EshopGuard.Cli;

/// <summary>
/// Minimal static server for the fixture e-shop. Replaces <c>{{base_url}}</c> in text files with the address the
/// request came to, so the same files work for tests and for manual runs.
/// </summary>
internal sealed class FixtureServer(string root, int port)
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".xml"] = "application/xml; charset=utf-8",
        [".txt"] = "text/plain; charset=utf-8",
        [".gz"] = "application/gzip",
    };

    public string Prefix => $"http://localhost:{port}/";

    public async Task RunAsync(Action<string> log, CancellationToken ct)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(Prefix);
        listener.Start();
        await using var registration = ct.Register(listener.Stop);

        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException && ct.IsCancellationRequested)
            {
                break;
            }

            // Read the request line first: closing the response disposes the request.
            var requestLine = $"{context.Request.HttpMethod} {context.Request.Url?.PathAndQuery}";
            try
            {
                var status = await HandleAsync(context);
                log($"{requestLine} → {status}");
            }
            catch (Exception ex) when (ex is HttpListenerException or IOException or InvalidOperationException)
            {
                // One broken request (e.g. the client hung up) must not stop the server.
                log($"{requestLine} → chyba: {ex.Message}");
            }
        }
    }

    private async Task<int> HandleAsync(HttpListenerContext context)
    {
        var response = context.Response;
        try
        {
            var requestUrl = context.Request.Url!;
            var relative = Uri.UnescapeDataString(requestUrl.AbsolutePath).TrimStart('/');
            if (relative.Length == 0 || relative.EndsWith('/'))
            {
                // A folder answers with its index.html (the Slovak version /sk/ of the fixture shops of change 7).
                relative += "index.html";
            }

            if (relative.StartsWith('_'))
            {
                // _server.json and _lang-*/ describe the test server, they are not pages.
                response.StatusCode = 404;
                return 404;
            }

            var fullRoot = Path.GetFullPath(root);
            var path = Path.GetFullPath(Path.Combine(fullRoot, relative));
            if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            {
                response.StatusCode = 404;
                return 404;
            }

            var extension = Path.GetExtension(path);
            var body = await File.ReadAllBytesAsync(path);
            if (extension is ".html" or ".xml" or ".txt")
            {
                var baseUrl = requestUrl.GetLeftPart(UriPartial.Authority);
                body = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(body).Replace("{{base_url}}", baseUrl));
            }

            response.StatusCode = 200;
            response.ContentType = ContentTypes.GetValueOrDefault(extension, "application/octet-stream");
            response.ContentLength64 = body.Length;
            if (!string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                await response.OutputStream.WriteAsync(body);
            }

            return 200;
        }
        finally
        {
            response.Close();
        }
    }
}
