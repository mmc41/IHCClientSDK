using AspNetCore.Proxy;
using System.Text;

// Configuration constants
const int HttpPort = 5082;
const int HttpsPort = 5083;
const string TargetUrl = "http://usb";
const string LogFileName = "capture.log";
const string LogHeadersConfigKey = "LogHeaders";
const string LogHeadersEnvVar = "LOG_HEADERS";

// Computed values
var HttpEndpoint = $"http://localhost:{HttpPort}";
var HttpsEndpoint = $"https://localhost:{HttpsPort}";

var builder = WebApplication.CreateBuilder(args);

// Create log file writer
var logFile = new StreamWriter(LogFileName, append: true) { AutoFlush = true };

// Handle Ctrl+C gracefully
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    logFile?.Dispose();
    Environment.Exit(0);
};

// Helper method to write to both console and file
void WriteLog(string message, ConsoleColor? color = null)
{
    if (color.HasValue)
        Console.ForegroundColor = color.Value;

    Console.WriteLine(message);
    logFile.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");

    if (color.HasValue)
        Console.ResetColor();
}

// Configure Kestrel to listen on both HTTP and HTTPS
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenLocalhost(HttpPort); // HTTP
    serverOptions.ListenLocalhost(HttpsPort, listenOptions => // HTTPs
    {
        listenOptions.UseHttps(); // Uses development certificate
    });
});

builder.Services.AddProxies();

var app = builder.Build();

var logHeaders = builder.Configuration.GetValue<bool>(LogHeadersConfigKey) ||
                Environment.GetEnvironmentVariable(LogHeadersEnvVar) == "true";

var correlationCounter = 0;

WriteLog($"HTTP/HTTPS Proxy Server starting...");
WriteLog($"Proxy endpoints:");
WriteLog($"  HTTP:  {HttpEndpoint}");
WriteLog($"  HTTPS: {HttpsEndpoint} (SSL termination)");
WriteLog($"Forwarding all traffic to: {TargetUrl}");
WriteLog($"Header logging: {(logHeaders ? "ENABLED" : "DISABLED")} (set {LogHeadersEnvVar}=true to enable)");
WriteLog($"Logging to file: {LogFileName}");
WriteLog($"----------------------------------------");
WriteLog($"Ready to receive requests...\n");

app.RunProxy(proxy => proxy
    .UseHttp(TargetUrl, builder => builder
        .WithBeforeSend(async (context, requestMessage) =>
        {
            var correlationId = Interlocked.Increment(ref correlationCounter);
            context.Items["CorrelationId"] = correlationId;

            WriteLog($"\n[{correlationId}] ====== REQUEST ======", ConsoleColor.Cyan);
            WriteLog($"[{correlationId}] {requestMessage.Method} {requestMessage.RequestUri}", ConsoleColor.Cyan);

            if (logHeaders && requestMessage.Headers.Any())
            {
                WriteLog($"[{correlationId}] Headers:", ConsoleColor.DarkCyan);
                foreach (var header in requestMessage.Headers)
                {
                    WriteLog($"[{correlationId}]   {header.Key}: {string.Join(", ", header.Value)}", ConsoleColor.DarkCyan);
                }
            }

            if (requestMessage.Content != null)
            {
                var requestBody = await requestMessage.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(requestBody))
                {
                    WriteLog($"[{correlationId}] Body: {requestBody}", ConsoleColor.Yellow);
                }
            }
        })
        .WithAfterReceive(async (context, responseMessage) =>
        {
            var correlationId = context.Items["CorrelationId"];

            WriteLog($"\n[{correlationId}] ====== RESPONSE ======", ConsoleColor.Green);
            WriteLog($"[{correlationId}] Status: {(int)responseMessage.StatusCode} {responseMessage.ReasonPhrase}", ConsoleColor.Green);

            if (logHeaders && responseMessage.Headers.Any())
            {
                WriteLog($"[{correlationId}] Headers:", ConsoleColor.DarkGreen);
                foreach (var header in responseMessage.Headers)
                {
                    WriteLog($"[{correlationId}]   {header.Key}: {string.Join(", ", header.Value)}", ConsoleColor.DarkGreen);
                }
            }

            var responseBody = await responseMessage.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                WriteLog($"[{correlationId}] Body: {responseBody}", ConsoleColor.Magenta);
            }
        })));

app.Run();
