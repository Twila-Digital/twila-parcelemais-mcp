using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ParceleMais.Errors;
using ParceleMais.Orders.Models;

namespace Parcelemais.Mcp.Tools;

[McpServerToolType]
public sealed class OrdersTools
{
    [McpServerTool(Name = "createOrder"), Description("Cria um pedido de crédito/parcelamento (CDC) pra um cliente. Retorna o id do pedido criado.")]
    public static async Task<string> CreateOrder(
        IParceleMaisClientAccessor accessor,
        [Description("CPF do cliente, só números.")] string cpf,
        [Description("Celular do cliente.")] string phoneNumber,
        [Description("CNPJ do estabelecimento, só números.")] string establishmentDocument,
        [Description("Valor solicitado, em reais (ex.: 1500.00).")] decimal requestedAmount,
        [Description("Nome completo do cliente.")] string name,
        [Description("Email do cliente.")] string email,
        [Description("Data de nascimento do cliente (AAAA-MM-DD).")] DateTimeOffset dateOfBirth,
        [Description("Logradouro.")] string street,
        [Description("Número do endereço.")] string number,
        [Description("Bairro.")] string neighborhood,
        [Description("Cidade.")] string city,
        [Description("UF (2 letras).")] string state,
        [Description("CEP.")] string postalCode,
        [Description("Complemento do endereço, se houver.")] string? complement,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var orderId = await client.Orders.CreateAsync(new CreateOrderRequest(
                Cpf: cpf,
                PhoneNumber: phoneNumber,
                EstablishmentDocument: establishmentDocument,
                RequestedAmount: requestedAmount,
                Name: name,
                Email: email,
                DateOfBirth: dateOfBirth,
                Address: new Address(street, number, neighborhood, city, state, postalCode, complement)), cancellationToken);
            return $"Pedido criado: {orderId}";
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "getOrder"), Description("Busca um pedido por id.")]
    public static async Task<string> GetOrder(
        IParceleMaisClientAccessor accessor,
        [Description("Id do pedido (GUID).")] Guid orderId,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var order = await client.Orders.GetAsync(orderId, cancellationToken);
            return ToolTextFormatting.Format(order);
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "listOrders"), Description("Lista pedidos com filtros e paginação.")]
    public static async Task<string> ListOrders(
        IParceleMaisClientAccessor accessor,
        [Description("Filtra por status do pedido.")] OrderStatus? status,
        [Description("Filtra por CPF do cliente.")] string? customerDocument,
        [Description("Filtra por CNPJ do estabelecimento.")] string? establishmentDocument,
        [Description("Data inicial do filtro por período.")] DateTimeOffset? startDate,
        [Description("Data final do filtro por período.")] DateTimeOffset? endDate,
        [Description("Número do pedido.")] long? number,
        [Description("Página (a partir de 1).")] int page,
        [Description("Tamanho da página (padrão 10, máximo 100).")] int pageSize,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var result = await client.Orders.ListAsync(new ListOrdersRequest(
                Status: status,
                CustomerDocument: customerDocument,
                StartDate: startDate,
                EndDate: endDate,
                Number: number,
                EstablishmentDocument: establishmentDocument,
                Page: page <= 0 ? 1 : page,
                PageSize: pageSize <= 0 ? 10 : pageSize), cancellationToken);

            if (result.Items.Count == 0) return "Nenhum pedido encontrado.";

            var lines = result.Items.Select((o, i) => $"{i + 1}. {o.Id} — {o.Status} — {ToolTextFormatting.Money(o.Total ?? o.RequestedAmount ?? 0)}");
            var pagina = $"\n\nPágina {result.PageNumber} de {Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize))} ({result.TotalCount} no total, {(result.HasNext ? "há mais páginas" : "última página")}).";
            return string.Join("\n", lines) + pagina;
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "startCdcSale"), Description("Inicia a venda CDC de um pedido aprovado e retorna o link de pagamento hospedado.")]
    public static async Task<string> StartCdcSale(
        IParceleMaisClientAccessor accessor,
        [Description("Id do pedido (GUID).")] Guid orderId,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var link = await client.Orders.StartCdcSaleAsync(orderId, cancellationToken);
            return link.Url is { } url ? $"Link de pagamento: {url}" : "Pedido não retornou um link de pagamento (verifique se está aprovado).";
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "importOrderInvoice"), Description("Anexa uma nota fiscal (base64) a um pedido.")]
    public static async Task<string> ImportOrderInvoice(
        IParceleMaisClientAccessor accessor,
        [Description("Id do pedido (GUID).")] Guid orderId,
        [Description("Conteúdo do arquivo em base64.")] string base64Content,
        [Description("Nome do arquivo (ex.: nota.pdf).")] string fileName,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var bytes = Convert.FromBase64String(base64Content);
            await client.Orders.ImportInvoiceAsync(orderId, InvoiceFile.FromBytes(bytes, fileName), cancellationToken);
            return $"Nota fiscal '{fileName}' anexada ao pedido {orderId}.";
        }
        catch (FormatException)
        {
            throw new McpException("base64Content não é um base64 válido.");
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }
}
