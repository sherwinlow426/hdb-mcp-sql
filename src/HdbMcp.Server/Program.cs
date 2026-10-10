using HdbMcp.Server.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdout IS the protocol channel. Every log line must go to stderr
// or the client sees malformed JSON-RPC and drops the connection.
builder.Logging.AddConsole(o =>
    o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(new HdbOptions
{
    ConnectionString = Environment.GetEnvironmentVariable("HDB_CS")
        ?? throw new InvalidOperationException("HDB_CS is not set"),
    MaxRows = 500,
    TimeoutSeconds = 15,
    EnableRawSql =
        Environment.GetEnvironmentVariable("HDB_ENABLE_RAW_SQL") == "true"
});
builder.Services.AddSingleton<HdbRepository>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

await builder.Build().RunAsync();