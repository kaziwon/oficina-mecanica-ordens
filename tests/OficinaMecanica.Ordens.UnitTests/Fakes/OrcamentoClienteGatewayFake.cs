using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Inputs.Orcamentos;
using OficinaMecanica.Ordens.Application.Outputs.Orcamentos;

namespace OficinaMecanica.Ordens.UnitTests.Fakes;

public class OrcamentoClienteGatewayFake : IOrcamentoClienteGateway
{
    public int Chamadas { get; private set; }
    public VinculoOrcamentoClienteInput? Recebido { get; private set; }
    public Action<OrcamentoClienteOutput>? Alterar { get; set; }
    public Task<OrcamentoClienteOutput> Obter(VinculoOrcamentoClienteInput vinculo, CancellationToken cancellation)
        => Responder(vinculo, false, null, cancellation);
    public Task<OrcamentoClienteOutput> Responder(VinculoOrcamentoClienteInput vinculo, bool aprovado, string? motivo, CancellationToken cancellation)
    {
        Chamadas++; Recebido = vinculo;
        var output = new OrcamentoClienteOutput
        {
            Id = vinculo.OrcamentoId,
            OrdemServicoId = vinculo.OrdemServicoId,
            ExecucaoId = vinculo.ExecucaoId,
            ValorTotal = vinculo.ValorTotal,
            Diagnostico = "Revisao",
            CriadoEm = DateTime.UtcNow.AddMinutes(-1),
            Status = aprovado ? "Aprovado" : motivo is null ? "AguardandoAprovacao" : "Recusado",
            RespondidoEm = aprovado || motivo is not null ? DateTime.UtcNow : null,
            MotivoRecusa = motivo,
            Itens = [new() { Tipo = "Servico", ReferenciaId = Guid.NewGuid(), Nome = "Revisao", Quantidade = 1, PrecoUnitario = vinculo.ValorTotal, Total = vinculo.ValorTotal }]
        };
        Alterar?.Invoke(output); return Task.FromResult(output);
    }
}
