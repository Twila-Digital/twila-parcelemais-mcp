using Parcelemais.Mcp.Tools;
using ParceleMais.Webhooks.Models;
using Xunit;

namespace Parcelemais.Mcp.Tests;

public class ToolTextFormattingTests
{
    [Fact]
    public void Money_FormatsAsBrazilianCurrency()
    {
        var formatted = ToolTextFormatting.Money(1500.5m);
        Assert.Contains("1.500,50", formatted);
    }

    [Fact]
    public void Format_WebhookAudit_ShowsIdTypeStatusDateAndBodies()
    {
        var id = Guid.NewGuid();
        var audit = new WebhookAudit(id, WebHookType.Order, "{\"orderId\":1}", "{\"ok\":false}", 500, new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero));

        var formatted = ToolTextFormatting.Format(audit);

        Assert.Contains($"Envio {id}", formatted);
        Assert.Contains("Tipo: Order", formatted);
        Assert.Contains("Status HTTP: 500", formatted);
        Assert.Contains("Requisição: {\"orderId\":1}", formatted);
        Assert.Contains("Resposta: {\"ok\":false}", formatted);
    }

    [Fact]
    public void Format_WebhookAudit_EmptyBodiesShowDash()
    {
        var audit = new WebhookAudit(Guid.NewGuid(), WebHookType.Customer, "", "  ", 200, DateTimeOffset.UnixEpoch);

        var formatted = ToolTextFormatting.Format(audit);

        Assert.Contains("Requisição: —", formatted);
        Assert.Contains("Resposta: —", formatted);
    }
}
