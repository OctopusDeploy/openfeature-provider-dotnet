using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Octopus.OpenFeature.Provider;
using OpenFeature;
using OpenFeature.Model;

// Serves a recorded Octopus response on loopback, points the provider at it, and checks every evaluation.
// The response covers a server-resolved flag in each state and each client-side condition type, so the
// provider deserializes every shape it supports.
const string Evaluations = """
    [
      { "slug": "enabled-flag", "value": true, "reason": "The flag is enabled for this environment." },
      { "slug": "disabled-flag", "value": false, "reason": "The flag is disabled for this environment." },
      { "slug": "eu-only", "evaluationKey": "evaluation-key",
        "rules": [ { "name": "EU", "conditions": [ { "type": "context-attribute-is-one-of", "key": "region", "values": [ "eu" ] } ] } ] },
      { "slug": "outside-eu", "evaluationKey": "evaluation-key",
        "rules": [ { "name": "Outside EU", "conditions": [ { "type": "context-attribute-is-not-one-of", "key": "region", "values": [ "eu" ] } ] } ] },
      { "slug": "everyone-by-percentage", "evaluationKey": "evaluation-key",
        "rules": [ { "name": "Everyone", "conditions": [ { "type": "percentage-by-context", "percentage": 100 } ] } ] }
    ]
    """;
const string Check = """{ "contentHash": "AQ==" }""";

using var server = RecordedOctopusServer.Start(Evaluations, Check);
Environment.SetEnvironmentVariable("OctoToggle__Url", server.Url);

var logger = new ConsoleErrorLoggerFactory();
var configuration = new OctopusFeatureConfiguration("client-identifier", new ProductMetadata("aot-smoke-test")) { LoggerFactory = logger };
await Api.Instance.SetProviderAsync(new OctopusFeatureProvider(configuration));
var client = Api.Instance.GetClient();

var europe = EvaluationContext.Builder().Set("region", "eu").SetTargetingKey("user-1").Build();
var america = EvaluationContext.Builder().Set("region", "us").SetTargetingKey("user-1").Build();

var failures = 0;
failures += await Expect("enabled-flag", null, true);
failures += await Expect("disabled-flag", null, false);
failures += await Expect("eu-only", europe, true);
failures += await Expect("eu-only", america, false);
failures += await Expect("outside-eu", america, true);
failures += await Expect("everyone-by-percentage", europe, true);

await Api.Instance.ShutdownAsync();

failures += logger.ErrorCount;
Console.WriteLine(failures == 0 ? "AOT smoke test passed." : $"AOT smoke test failed: {failures} problem(s).");
return failures == 0 ? 0 : 1;

async Task<int> Expect(string slug, EvaluationContext? context, bool expected)
{
    // The default is the opposite of the expected value, so falling back to it fails the check.
    var details = await client.GetBooleanDetailsAsync(slug, !expected, context);
    var passed = details.Value == expected && details.ErrorType == OpenFeature.Constant.ErrorType.None;
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {slug} = {details.Value} (expected {expected}; reason: {details.Reason}; error: {details.ErrorType} {details.ErrorMessage})");
    return passed ? 0 : 1;
}

/// <summary>Answers the two Octopus endpoints the provider calls with recorded JSON.</summary>
sealed class RecordedOctopusServer : IDisposable
{
    readonly HttpListener listener = new();
    readonly string evaluations;
    readonly string check;

    RecordedOctopusServer(string url, string evaluations, string check)
    {
        Url = url;
        this.evaluations = evaluations;
        this.check = check;
        listener.Prefixes.Add(url);
    }

    public string Url { get; }

    public static RecordedOctopusServer Start(string evaluations, string check)
    {
        var server = new RecordedOctopusServer($"http://127.0.0.1:{FreePort()}/", evaluations, check);
        server.listener.Start();
        _ = Task.Run(server.Serve);
        return server;
    }

    static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    async Task Serve()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (!listener.IsListening)
            {
                return;
            }

            var isEvaluations = context.Request.Url!.AbsolutePath.EndsWith("/evaluations/v4/", StringComparison.Ordinal);
            if (isEvaluations)
            {
                context.Response.Headers.Add("ContentHash", "AQ==");
            }

            var body = isEvaluations ? evaluations : check;

            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
    }

    public void Dispose() => listener.Close();
}

/// <summary>Writes warnings and errors to standard error and counts the errors.</summary>
sealed class ConsoleErrorLoggerFactory : ILoggerFactory, ILogger
{
    public int ErrorCount { get; private set; }

    public ILogger CreateLogger(string categoryName) => this;

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        if (logLevel >= LogLevel.Error)
        {
            ErrorCount++;
        }

        var cause = exception is null ? string.Empty : $" {exception.GetType().Name}: {exception.Message}";
        Console.Error.WriteLine($"{logLevel}: {formatter(state, exception)}{cause}");
    }

    public void Dispose()
    {
    }
}
