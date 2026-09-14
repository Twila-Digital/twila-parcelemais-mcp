using System.ComponentModel;
using ModelContextProtocol.Server;
using ParceleMais.Errors;
using ParceleMais.Webhooks.Models;

namespace Parcelemais.Mcp.Tools;

[McpServerToolType]
public sealed class WebhooksTools
{
    [McpServerTool(Name = "createWebhook"), Description("Cadastra um webhook. Retorna a chave de assinatura (mostrada só uma vez) — guarde-a pra validar os eventos recebidos.")]
    public static async Task<string> CreateWebhook(
        IParceleMaisClientAccessor accessor,
        [Description("Tipo do evento: Customer, Simulation ou Order.")] WebHookType type,
        [Description("URL que vai receber os eventos.")] string url,
        [Description("Tipo de autenticação do endpoint: None, Basic ou Jwt.")] WebHookAuthenticationType authenticationType,
        [Description("Credencial (usuário:senha pra Basic, ou token pra Jwt) — obrigatório se authenticationType não for None.")] string? credential,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var result = await client.Webhooks.CreateAsync(new CreateWebhookRequest(type, url, authenticationType, credential), cancellationToken);
            return $"Webhook criado.\nChave de assinatura (guarde com segurança, não será mostrada de novo): {result.SigningSecret}";
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "listWebhooks"), Description("Lista os webhooks cadastrados.")]
    public static async Task<string> ListWebhooks(IParceleMaisClientAccessor accessor, CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var webhooks = await client.Webhooks.ListAsync(cancellationToken);
            return webhooks.Count == 0
                ? "Nenhum webhook cadastrado."
                : string.Join("\n", webhooks.Select(ToolTextFormatting.Format));
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "updateWebhook"), Description("Atualiza a URL/autenticação do webhook de um tipo específico (um webhook por tipo).")]
    public static async Task<string> UpdateWebhook(
        IParceleMaisClientAccessor accessor,
        [Description("Tipo do webhook a atualizar: Customer, Simulation ou Order.")] WebHookType type,
        [Description("Nova URL.")] string url,
        [Description("Tipo de autenticação do endpoint: None, Basic ou Jwt.")] WebHookAuthenticationType authenticationType,
        [Description("Credencial, se aplicável.")] string? credential,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            await client.Webhooks.UpdateAsync(type, new UpdateWebhookRequest(url, authenticationType, credential), cancellationToken);
            return $"Webhook do tipo {type} atualizado.";
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "deleteWebhook"), Description("Remove o webhook cadastrado pra um tipo (irreversível).")]
    public static async Task<string> DeleteWebhook(
        IParceleMaisClientAccessor accessor,
        [Description("Tipo do webhook a remover: Customer, Simulation ou Order.")] WebHookType type,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            await client.Webhooks.DeleteAsync(type, cancellationToken);
            return $"Webhook do tipo {type} removido.";
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }
}
