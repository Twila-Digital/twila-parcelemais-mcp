using System.ComponentModel;
using ModelContextProtocol.Server;
using ParceleMais.Errors;
using ParceleMais.Establishments.Models;

namespace Parcelemais.Mcp.Tools;

[McpServerToolType]
public sealed class EstablishmentsTools
{
    [McpServerTool(Name = "createEstablishment"), Description("Cadastra uma loja na rede do parceiro. Retorna o id da loja criada.")]
    public static async Task<string> CreateEstablishment(
        IParceleMaisClientAccessor accessor,
        [Description("CNPJ da loja, só números.")] string document,
        [Description("Razão social da loja.")] string legalName,
        [Description("Nome fantasia da loja.")] string tradeName,
        [Description("Modelo de desembolso: EstablishmentChain (a rede recebe), Establishment (a loja recebe) ou External (conta de terceiro).")] DisbursementModel disbursementModel,
        [Description("Nome do responsável pela loja.")] string ownerName,
        [Description("Email do responsável.")] string ownerEmail,
        [Description("Celular do responsável no formato E.164 (código do país + DDD + número). Ex.: +5511999998888.")] string ownerPhone,
        [Description("Código do banco (ex.: 341).")] string bankNumber,
        [Description("Número da agência, sem dígito.")] string agencyNumber,
        [Description("Número da conta, sem dígito.")] string accountNumber,
        [Description("Dígito da conta.")] string accountDigit,
        [Description("Tipo da conta: Current, Savings ou Payment.")] BankAccountType accountType,
        [Description("Dígito da agência, se houver.")] string? agencyDigit,
        [Description("Nome do titular da conta — obrigatório quando o modelo de desembolso é External.")] string? holderName,
        [Description("CPF/CNPJ do titular da conta — obrigatório quando o modelo de desembolso é External.")] string? holderDocument,
        [Description("Logradouro da loja. Informe o endereço completo ou deixe todos os campos de endereço vazios pra cadastrar sem endereço.")] string? street,
        [Description("Número do endereço.")] string? number,
        [Description("Bairro.")] string? neighborhood,
        [Description("Cidade.")] string? city,
        [Description("UF (2 letras).")] string? state,
        [Description("CEP, só números.")] string? postalCode,
        [Description("Complemento do endereço, se houver.")] string? complement,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var address = string.IsNullOrWhiteSpace(street)
                ? null
                : new EstablishmentAddress(street!, number ?? string.Empty, neighborhood ?? string.Empty, city ?? string.Empty,
                    state ?? string.Empty, postalCode ?? string.Empty, complement);

            var result = await client.Establishments.CreateAsync(new CreateEstablishmentRequest(
                Document: document,
                LegalName: legalName,
                TradeName: tradeName,
                DisbursementModel: disbursementModel,
                Owner: new EstablishmentOwner(ownerName, ownerEmail, ownerPhone),
                BankAccount: new EstablishmentBankAccount(bankNumber, agencyNumber, accountNumber, accountDigit, accountType, agencyDigit, holderName, holderDocument),
                Address: address), cancellationToken);

            return $"Loja cadastrada: {result.EstablishmentId}";
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "getEstablishment"), Description("Busca uma loja da rede do parceiro por id.")]
    public static async Task<string> GetEstablishment(
        IParceleMaisClientAccessor accessor,
        [Description("Id da loja (GUID).")] Guid establishmentId,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var establishment = await client.Establishments.GetAsync(establishmentId, cancellationToken);
            return ToolTextFormatting.Format(establishment);
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "listEstablishments"), Description("Lista as lojas da rede do parceiro, com filtro opcional por nome fantasia e por situação.")]
    public static async Task<string> ListEstablishments(
        IParceleMaisClientAccessor accessor,
        [Description("Filtra pelo nome fantasia (busca parcial).")] string? tradeName,
        [Description("Filtra pela situação: true só ativas, false só inativas. Omita pra trazer todas.")] bool? isActive,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var establishments = await client.Establishments.ListAsync(new ListEstablishmentsRequest(
                TradeName: tradeName,
                IsActive: isActive), cancellationToken);

            if (establishments.Count == 0) return "Nenhuma loja encontrada.";

            var lines = establishments.Select((e, i) => $"{i + 1}. {e.EstablishmentId} — {e.TradeName} — {e.Document} — {(e.IsActive ? "ativa" : "inativa")}");
            return string.Join("\n", lines);
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "updateEstablishment"), Description("Edita uma loja. O nome fantasia é obrigatório; os demais campos, quando omitidos, mantêm o valor atual. A razão social não pode ser alterada. Pra trocar a conta bancária use updateEstablishmentBankAccount.")]
    public static async Task<string> UpdateEstablishment(
        IParceleMaisClientAccessor accessor,
        [Description("Id da loja (GUID).")] Guid establishmentId,
        [Description("Nome fantasia da loja.")] string tradeName,
        [Description("Novo modelo de desembolso: EstablishmentChain, Establishment ou External. Omita pra manter o atual.")] DisbursementModel? disbursementModel,
        [Description("Logradouro. Informe o endereço inteiro ou deixe os campos de endereço vazios pra manter o atual.")] string? street,
        [Description("Número do endereço.")] string? number,
        [Description("Bairro.")] string? neighborhood,
        [Description("Cidade.")] string? city,
        [Description("UF (2 letras).")] string? state,
        [Description("CEP, só números.")] string? postalCode,
        [Description("Complemento do endereço, se houver.")] string? complement,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            var address = string.IsNullOrWhiteSpace(street)
                ? null
                : new EstablishmentAddress(street!, number ?? string.Empty, neighborhood ?? string.Empty, city ?? string.Empty,
                    state ?? string.Empty, postalCode ?? string.Empty, complement);

            await client.Establishments.UpdateAsync(establishmentId, new UpdateEstablishmentRequest(
                TradeName: tradeName,
                DisbursementModel: disbursementModel,
                Address: address), cancellationToken);

            return $"Loja {establishmentId} atualizada.";
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "updateEstablishmentBankAccount"), Description("Troca a conta bancária de recebimento da loja. Todos os dados da conta são obrigatórios — a conta é substituída por inteiro.")]
    public static async Task<string> UpdateEstablishmentBankAccount(
        IParceleMaisClientAccessor accessor,
        [Description("Id da loja (GUID).")] Guid establishmentId,
        [Description("Código do banco (ex.: 341).")] string bankNumber,
        [Description("Número da agência, sem dígito.")] string agencyNumber,
        [Description("Número da conta, sem dígito.")] string accountNumber,
        [Description("Dígito da conta.")] string accountDigit,
        [Description("Tipo da conta: Current, Savings ou Payment.")] BankAccountType accountType,
        [Description("Dígito da agência, se houver.")] string? agencyDigit,
        [Description("Nome do titular da conta — obrigatório quando o modelo de desembolso é External.")] string? holderName,
        [Description("CPF/CNPJ do titular da conta — obrigatório quando o modelo de desembolso é External.")] string? holderDocument,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            await client.Establishments.UpdateBankAccountAsync(establishmentId, new EstablishmentBankAccount(
                bankNumber, agencyNumber, accountNumber, accountDigit, accountType, agencyDigit, holderName, holderDocument), cancellationToken);

            return $"Conta bancária da loja {establishmentId} atualizada.";
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "deactivateEstablishment"), Description("Inativa uma loja. Uma loja inativa não aceita novos pedidos.")]
    public static async Task<string> DeactivateEstablishment(
        IParceleMaisClientAccessor accessor,
        [Description("Id da loja (GUID).")] Guid establishmentId,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            await client.Establishments.DeactivateAsync(establishmentId, cancellationToken);
            return $"Loja {establishmentId} inativada.";
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }

    [McpServerTool(Name = "activateEstablishment"), Description("Reativa uma loja inativa, voltando a aceitar pedidos.")]
    public static async Task<string> ActivateEstablishment(
        IParceleMaisClientAccessor accessor,
        [Description("Id da loja (GUID).")] Guid establishmentId,
        CancellationToken cancellationToken)
    {
        var client = await accessor.GetClientAsync(cancellationToken);
        try
        {
            await client.Establishments.ActivateAsync(establishmentId, cancellationToken);
            return $"Loja {establishmentId} ativada.";
        }
        catch (ParceleMaisException ex)
        {
            throw ex.ToMcpException();
        }
    }
}
