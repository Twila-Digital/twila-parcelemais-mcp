using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Parcelemais.Mcp.Stdio;
using Parcelemais.Mcp.Tools;
using ParceleMais.Configuration;
using ParceleMais.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

var clientId = Environment.GetEnvironmentVariable("PARCELEMAIS_CLIENT_ID");
var clientSecret = Environment.GetEnvironmentVariable("PARCELEMAIS_CLIENT_SECRET");
var environmentName = Environment.GetEnvironmentVariable("PARCELEMAIS_ENVIRONMENT") ?? "Production";

if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
{
    await Console.Error.WriteLineAsync(
        "PARCELEMAIS_CLIENT_ID e PARCELEMAIS_CLIENT_SECRET são obrigatórias. Configure-as no env do seu cliente MCP (Claude Desktop / Cursor).");
    Environment.Exit(1);
    return;
}

if (!Enum.TryParse<ParceleMaisEnvironment>(environmentName, ignoreCase: true, out var parsedEnvironment))
{
    await Console.Error.WriteLineAsync($"PARCELEMAIS_ENVIRONMENT inválida: '{environmentName}'. Use 'Staging' ou 'Production'.");
    Environment.Exit(1);
    return;
}

builder.Services.AddParceleMais(options =>
{
    options.ClientId = clientId;
    options.ClientSecret = clientSecret;
    options.Environment = parsedEnvironment;
});

builder.Services.AddSingleton<IParceleMaisClientAccessor, StaticClientAccessor>();

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<OrdersTools>()
    .WithTools<SimulationsTools>()
    .WithTools<CustomersTools>()
    .WithTools<WebhooksTools>();

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

await builder.Build().RunAsync();
