using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;

public class ExternalPluginShim : IAsyncPlugin, IDisposable
{
    private string _exePath;
    private Process _process;
    private int _messageId = 0;
    private ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingResponses = new();
    private IPublicAPI _flowApi;

    public ExternalPluginShim(string exePath)
    {
        _exePath = exePath;
    }

    public async Task InitAsync(PluginInitContext context)
    {
        _flowApi = context.API;
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _exePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };
        _process.Start();

        // Start reading output
        _ = Task.Run(ReadOutputAsync);

        var initMessage = new
        {
            Id = ++_messageId,
            Method = "Init",
            Params = new
            {
                context.CurrentPluginMetadata
            }
        };

        await SendMessageAsync(initMessage);
    }

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        var queryMessage = new
        {
            Id = ++_messageId,
            Method = "Query",
            Params = query
        };

        var response = await SendMessageAsync(queryMessage);
        return JsonSerializer.Deserialize<List<Result>>(response.GetProperty("Result"));
    }

    private async Task<JsonElement> SendMessageAsync(object message)
    {
        var json = JsonSerializer.Serialize(message);
        var tcs = new TaskCompletionSource<JsonElement>();
        _pendingResponses[((dynamic)message).Id] = tcs;
        await _process.StandardInput.WriteLineAsync(json);
        return await tcs.Task;
    }

    private async Task ReadOutputAsync()
    {
        while (!_process.HasExited)
        {
            var line = await _process.StandardOutput.ReadLineAsync();
            if (line == null) break;

            var message = JsonSerializer.Deserialize<JsonElement>(line);

            if (message.TryGetProperty("Method", out var _))
            {
                // This is a method call from the external process
                HandleMethodCall(message);
            }
            else if (message.TryGetProperty("Id", out var idElement))
            {
                // This is a response to a previous call
                int id = idElement.GetInt32();
                if (_pendingResponses.TryRemove(id, out var tcs))
                {
                    tcs.SetResult(message);
                }
            }
        }
    }

    private void HandleMethodCall(JsonElement message)
    {
        var method = message.GetProperty("Method").GetString();
        var id = message.GetProperty("Id").GetInt32();

        switch (method)
        {
            case "ChangeQuery":
                var query = message.GetProperty("Params").GetProperty("query").GetString();
                _flowApi.ChangeQuery(query);
                break;
            // Add more method handlers here
            default:
                Console.WriteLine($"Unknown method call: {method}");
                break;
        }

        // Send a response
        var response = new
        {
            Id = id,
            Result = "OK"
        };
        var json = JsonSerializer.Serialize(response);
        _process.StandardInput.WriteLineAsync(json);
    }

    public void Dispose()
    {
        _process?.Kill();
        _process?.Dispose();
    }
}
