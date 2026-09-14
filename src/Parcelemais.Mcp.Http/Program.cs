using Amazon.DynamoDBv2;
using ModelContextProtocol.AspNetCore;
using Parcelemais.Mcp.Http;
using Parcelemais.Mcp.Http.Auth;
using Parcelemais.Mcp.Http.Storage;
using Parcelemais.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);

var tablePrefix = Environment.GetEnvironmentVariable("MCP_DYNAMODB_TABLE_PREFIX") ?? "ParcelemaisMcp";

builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
{
    var localEndpoint = Environment.GetEnvironmentVariable("MCP_DYNAMODB_LOCAL_ENDPOINT");
    return string.IsNullOrEmpty(localEndpoint)
        ? new AmazonDynamoDBClient()
        : new AmazonDynamoDBClient(new AmazonDynamoDBConfig { ServiceURL = localEndpoint });
});
builder.Services.AddSingleton(sp => new DynamoDbOAuthStore(sp.GetRequiredService<IAmazonDynamoDB>(), tablePrefix));
builder.Services.AddSingleton(_ => Crypto.FromEnvironment());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IParceleMaisClientAccessor, SessionClientAccessor>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("OAuthBrowser", policy => policy
        .AllowAnyOrigin()
        .WithMethods("GET", "POST", "OPTIONS")
        .WithHeaders("Content-Type", "Authorization"));
});

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Sem estado adicional entre chamadas além da credencial (já resolvida por requisição via
        // BearerCredentialMiddleware) — stateless permite escalar horizontalmente sem afinidade de sessão.
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithTools<OrdersTools>()
    .WithTools<SimulationsTools>()
    .WithTools<CustomersTools>()
    .WithTools<WebhooksTools>();

var app = builder.Build();

if (Environment.GetEnvironmentVariable("MCP_AUTO_CREATE_TABLES") == "1")
{
    await app.Services.GetRequiredService<DynamoDbOAuthStore>().EnsureTablesExistAsync(CancellationToken.None);
}

app.UseCors("OAuthBrowser");

var oauthStore = app.Services.GetRequiredService<DynamoDbOAuthStore>();
var crypto = app.Services.GetRequiredService<Crypto>();
app.MapOAuthEndpoints(oauthStore, crypto);

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/mcp"),
    branch => branch.UseMiddleware<BearerCredentialMiddleware>());

app.MapMcp("/mcp");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
