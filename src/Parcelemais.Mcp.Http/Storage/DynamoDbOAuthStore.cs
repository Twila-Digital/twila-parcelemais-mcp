using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Parcelemais.Mcp.Http.Storage;

/// <summary>
/// Persistência do authorization server em DynamoDB: clients registrados via DCR (sem expiração) e
/// códigos de autorização (TTL nativo do DynamoDB cuida da limpeza — sem job de limpeza manual).
/// </summary>
public sealed class DynamoDbOAuthStore(IAmazonDynamoDB dynamoDb, string tablePrefix)
{
    private const int AuthCodeLifetimeSeconds = 5 * 60;

    private string ClientsTable => $"{tablePrefix}OAuthClients";
    private string CodesTable => $"{tablePrefix}AuthCodes";

    public async Task<OAuthClientRecord> RegisterClientAsync(IReadOnlyList<string> redirectUris, string? clientName, CancellationToken cancellationToken)
    {
        var record = new OAuthClientRecord(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), redirectUris, clientName);

        await dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = ClientsTable,
            Item = new Dictionary<string, AttributeValue>
            {
                ["ClientId"] = new(record.ClientId),
                ["ClientSecret"] = new(record.ClientSecret),
                ["RedirectUris"] = new AttributeValue { SS = [.. redirectUris] },
                ["ClientName"] = clientName is null ? new AttributeValue { NULL = true } : new AttributeValue(clientName),
            },
        }, cancellationToken).ConfigureAwait(false);

        return record;
    }

    public async Task<OAuthClientRecord?> GetClientAsync(string clientId, CancellationToken cancellationToken)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = ClientsTable,
            Key = new Dictionary<string, AttributeValue> { ["ClientId"] = new(clientId) },
        }, cancellationToken).ConfigureAwait(false);

        if (!response.IsItemSet) return null;

        var item = response.Item;
        return new OAuthClientRecord(
            item["ClientId"].S,
            item["ClientSecret"].S,
            item["RedirectUris"].SS,
            item.TryGetValue("ClientName", out var name) && name.NULL != true ? name.S : null);
    }

    public async Task<string> CreateAuthCodeAsync(
        string clientId, string redirectUri, string codeChallenge, string credentialEnc,
        string? scope, string? resource, CancellationToken cancellationToken)
    {
        var code = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(AuthCodeLifetimeSeconds).ToUnixTimeSeconds();

        var item = new Dictionary<string, AttributeValue>
        {
            ["Code"] = new(code),
            ["ClientId"] = new(clientId),
            ["RedirectUri"] = new(redirectUri),
            ["CodeChallenge"] = new(codeChallenge),
            ["CredentialEnc"] = new(credentialEnc),
            ["ExpiresAt"] = new AttributeValue { N = expiresAt.ToString() },
        };
        if (scope is not null) item["Scope"] = new AttributeValue(scope);
        if (resource is not null) item["Resource"] = new AttributeValue(resource);

        await dynamoDb.PutItemAsync(new PutItemRequest { TableName = CodesTable, Item = item }, cancellationToken).ConfigureAwait(false);
        return code;
    }

    /// <summary>Consome (lê e apaga) um código de autorização atomicamente. Retorna null se ausente/expirado.</summary>
    public async Task<AuthCodeRecord?> ConsumeAuthCodeAsync(string code, CancellationToken cancellationToken)
    {
        var response = await dynamoDb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = CodesTable,
            Key = new Dictionary<string, AttributeValue> { ["Code"] = new(code) },
            ReturnValues = ReturnValue.ALL_OLD,
        }, cancellationToken).ConfigureAwait(false);

        if (response.Attributes is not { Count: > 0 } item) return null;

        var expiresAt = long.Parse(item["ExpiresAt"].N);
        if (expiresAt < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;

        return new AuthCodeRecord(
            code,
            item["ClientId"].S,
            item["RedirectUri"].S,
            item["CodeChallenge"].S,
            item["CredentialEnc"].S,
            expiresAt,
            item.TryGetValue("Scope", out var scope) ? scope.S : null,
            item.TryGetValue("Resource", out var resource) ? resource.S : null);
    }

    /// <summary>Cria as tabelas se não existirem (dev local / primeiro deploy). Em produção, prefira provisionar via IaC.</summary>
    public async Task EnsureTablesExistAsync(CancellationToken cancellationToken)
    {
        var existing = (await dynamoDb.ListTablesAsync(cancellationToken).ConfigureAwait(false)).TableNames;

        if (!existing.Contains(ClientsTable))
        {
            await dynamoDb.CreateTableAsync(new CreateTableRequest
            {
                TableName = ClientsTable,
                AttributeDefinitions = [new AttributeDefinition("ClientId", ScalarAttributeType.S)],
                KeySchema = [new KeySchemaElement("ClientId", KeyType.HASH)],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            }, cancellationToken).ConfigureAwait(false);
        }

        if (!existing.Contains(CodesTable))
        {
            await dynamoDb.CreateTableAsync(new CreateTableRequest
            {
                TableName = CodesTable,
                AttributeDefinitions = [new AttributeDefinition("Code", ScalarAttributeType.S)],
                KeySchema = [new KeySchemaElement("Code", KeyType.HASH)],
                BillingMode = BillingMode.PAY_PER_REQUEST,
            }, cancellationToken).ConfigureAwait(false);

            await dynamoDb.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
            {
                TableName = CodesTable,
                TimeToLiveSpecification = new TimeToLiveSpecification { AttributeName = "ExpiresAt", Enabled = true },
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}
