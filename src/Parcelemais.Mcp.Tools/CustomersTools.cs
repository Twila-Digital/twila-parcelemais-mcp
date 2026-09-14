using System.ComponentModel;
using ModelContextProtocol.Server;
using ParceleMais.Customers.Models;
using ParceleMais.Errors;

namespace Parcelemais.Mcp.Tools;

[McpServerToolType]
public sealed class CustomersTools
{
    [McpServerTool(Name = "getCustomer"), Description("Busca um cliente por id.")]
    public static async Task<string> GetCustomer(
        IParceleMaisClientAccessor accessor,
        [Description("Id do cliente (GUID).")] Guid customerId,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var customer = await client.Customers.GetAsync(customerId, cancellationToken);
            return ToolTextFormatting.Format(customer);
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "listCustomers"), Description("Lista clientes com filtros e paginação.")]
    public static async Task<string> ListCustomers(
        IParceleMaisClientAccessor accessor,
        [Description("Filtra por nome (busca parcial).")] string? name,
        [Description("Filtra por CPF.")] string? document,
        [Description("Página (a partir de 1).")] int page,
        [Description("Tamanho da página (padrão 10, máximo 100).")] int pageSize,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var result = await client.Customers.ListAsync(new ListCustomersRequest(
                Name: name,
                Document: document,
                Page: page <= 0 ? 1 : page,
                PageSize: pageSize <= 0 ? 10 : pageSize), cancellationToken);

            if (result.Items.Count == 0) return "Nenhum cliente encontrado.";

            var lines = result.Items.Select((c, i) => $"{i + 1}. {c.Id} — {c.Name} — {c.Document}");
            var pagina = $"\n\nPágina {result.PageNumber} de {Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize))} ({result.TotalCount} no total, {(result.HasNext ? "há mais páginas" : "última página")}).";
            return string.Join("\n", lines) + pagina;
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }
}
