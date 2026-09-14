using System.ComponentModel;
using ModelContextProtocol.Server;
using ParceleMais.Errors;
using ParceleMais.Simulations.Models;

namespace Parcelemais.Mcp.Tools;

[McpServerToolType]
public sealed class SimulationsTools
{
    [McpServerTool(Name = "simulateInstallments"), Description("Simula as parcelas disponíveis pra um valor solicitado, sem criar nenhum pedido.")]
    public static async Task<string> SimulateInstallments(
        IParceleMaisClientAccessor accessor,
        [Description("Valor solicitado, em reais.")] decimal requestedAmount,
        [Description("Tipo de cálculo: GrossAmount (valor bruto) ou LiquidAmount (valor líquido). Padrão: GrossAmount.")] CalculationValueType calculationValueType,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var installments = await client.Simulations.SimulateInstallmentsAsync(
                new SimulateInstallmentsRequest(requestedAmount, calculationValueType), cancellationToken);

            if (installments.Count == 0) return "Nenhuma parcela disponível pra esse valor.";

            return string.Join("\n", installments.Select(i =>
                $"{i.Term}x de {ToolTextFormatting.Money(i.InstallmentAmount)} (total {ToolTextFormatting.Money(i.TotalAmount)})"));
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "simulateValues"), Description("Simula os valores (venda, desembolso, parcela) pra um valor e prazo específicos.")]
    public static async Task<string> SimulateValues(
        IParceleMaisClientAccessor accessor,
        [Description("Valor, em reais.")] decimal amount,
        [Description("Prazo em número de parcelas.")] int term,
        [Description("Tipo de cálculo: GrossAmount (valor bruto) ou LiquidAmount (valor líquido). Padrão: GrossAmount.")] CalculationValueType calculationValueType,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var values = await client.Simulations.SimulateValuesAsync(
                new SimulateValuesRequest(amount, term, calculationValueType), cancellationToken);

            return $"Valor de venda: {ToolTextFormatting.Money(values.SaleAmount)}\n" +
                   $"Valor de desembolso: {ToolTextFormatting.Money(values.DisbursementAmount)}\n" +
                   $"Valor da parcela: {ToolTextFormatting.Money(values.InstallmentAmount)}";
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }
}
