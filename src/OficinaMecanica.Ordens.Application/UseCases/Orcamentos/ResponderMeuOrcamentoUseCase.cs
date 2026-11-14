using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Outputs.Orcamentos;
using OficinaMecanica.Ordens.Application.Services;
using OficinaMecanica.Ordens.Application.Validators;

namespace OficinaMecanica.Ordens.Application.UseCases.Orcamentos;

public class ResponderMeuOrcamentoUseCase(AcessoOrcamentoCliente acesso, IOrcamentoClienteGateway orcamentos)
{
    public async Task<OrcamentoClienteOutput?> Executar(Guid clienteId, Guid ordemId, bool aprovado, string? motivo, CancellationToken cancellation)
    {
        var vinculo = acesso.Obter(clienteId, ordemId);
        if (vinculo is null) return null;
        if (aprovado ? motivo is not null : string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > 1000 || motivo.Any(char.IsControl))
            throw new ValidacaoException("Informe um motivo de recusa com ate 1000 caracteres, sem controles.");
        motivo = motivo?.Trim();
        var output = await orcamentos.Responder(vinculo, aprovado, motivo, cancellation);
        OrcamentoClienteValidator.Validar(output, vinculo);
        if (output.Status != (aprovado ? "Aprovado" : "Recusado") || output.MotivoRecusa != motivo)
            throw new IntegracaoOrcamentoException("Decisao ainda nao confirmada por Cobranca. Consulte o orcamento antes de repetir.");
        return output;
    }
}
