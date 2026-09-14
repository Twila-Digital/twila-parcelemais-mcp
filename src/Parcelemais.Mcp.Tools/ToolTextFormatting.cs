using System.Globalization;
using ModelContextProtocol;
using ParceleMais.Customers.Models;
using ParceleMais.Errors;
using ParceleMais.Orders.Models;
using ParceleMais.Webhooks.Models;

namespace Parcelemais.Mcp.Tools;

internal static class ToolTextFormatting
{
    public static string Money(decimal amount) =>
        amount.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));

    public static string Format(Order order)
    {
        var lines = new List<string>
        {
            $"Pedido {order.Id} (nº {order.Number})",
            $"Status: {order.Status} ({order.StatusDescription})",
            $"Cliente: {order.CustomerName ?? "—"} ({order.CustomerDocument})",
            $"Estabelecimento: {order.EstablishmentLegalName} ({order.EstablishmentDocument})",
            $"Criado em: {order.CreatedAt}",
        };
        if (order.RequestedAmount is { } requested) lines.Add($"Valor solicitado: {Money(requested)}");
        if (order.ApprovedAmount is { } approved) lines.Add($"Valor aprovado: {Money(approved)}");
        if (order.Total is { } total) lines.Add($"Total: {Money(total)}");
        if (order.Term is { } term) lines.Add($"Prazo: {term}x");
        if (order.Disbursed is { } disbursed) lines.Add($"Desembolsado: {(disbursed ? "sim" : "não")}" + (order.DisbursedAt is { } at ? $" em {at}" : ""));
        return string.Join("\n", lines);
    }

    public static string Format(Customer customer)
    {
        var lines = new List<string>
        {
            $"Cliente {customer.Id}",
            $"Nome: {customer.Name}",
            $"Documento: {customer.Document}",
            $"Data de nascimento: {customer.DateOfBirth}",
        };
        if (customer.Email is { } email) lines.Add($"Email: {email}");
        if (customer.PhoneNumber is { } phone) lines.Add($"Celular: {phone}");
        return string.Join("\n", lines);
    }

    public static string Format(Webhook webhook) =>
        $"{webhook.Type} → {webhook.Url} (autenticação: {webhook.AuthenticationType})";

    /// <summary>
    /// Converte qualquer exceção do SDK numa <see cref="McpException"/> com mensagem legível,
    /// pra que o cliente MCP veja um erro de ferramenta normal em vez de uma falha não tratada.
    /// </summary>
    public static McpException ToMcpException(this ParceleMaisException ex) => ex switch
    {
        ParceleMaisValidationException validation => new McpException(
            $"Erro de validação: {validation.Message}" +
            (validation.Errors is { Count: > 0 }
                ? "\n" + string.Join("\n", validation.Errors.Select(kv => $"- {kv.Key}: {string.Join("; ", kv.Value)}"))
                : "")),
        ParceleMaisRateLimitException rateLimit => new McpException(
            $"Limite de requisições excedido.{(rateLimit.RetryAfter is { } retryAfter ? $" Tente novamente em {retryAfter.TotalSeconds:0}s." : "")}"),
        ParceleMaisApiException api => new McpException(
            $"API do Parcele+ retornou {(int)api.StatusCode}: {api.Message}" +
            (api.CorrelationId is { } cid ? $" (correlationId: {cid})" : "")),
        ParceleMaisAuthenticationException auth => new McpException($"Falha de autenticação: {auth.Message}"),
        ParceleMaisTimeoutException timeout => new McpException($"Timeout: {timeout.Message}"),
        ParceleMaisConfigurationException config => new McpException($"Configuração inválida: {config.Message}"),
        _ => new McpException(ex.Message),
    };
}
