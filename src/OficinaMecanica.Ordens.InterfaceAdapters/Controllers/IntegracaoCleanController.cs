using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

public class IntegracaoCleanController
{
    private readonly ProcessarRespostaAberturaUseCase _processar;
    private readonly ProcessarEventoDiagnosticoUseCase _diagnostico;
    private readonly ProcessarRespostaOrcamentoUseCase _orcamento;
    private readonly ProcessarDecisaoOrcamentoUseCase _decisao;
    private readonly ProcessarCancelamentoPorRecusaUseCase _cancelamento;
    public IntegracaoCleanController(ProcessarRespostaAberturaUseCase processar,
        ProcessarEventoDiagnosticoUseCase diagnostico, ProcessarRespostaOrcamentoUseCase orcamento,
        ProcessarDecisaoOrcamentoUseCase decisao, ProcessarCancelamentoPorRecusaUseCase cancelamento)
    { _processar = processar; _diagnostico = diagnostico; _orcamento = orcamento; _decisao = decisao; _cancelamento = cancelamento; }
    public void Processar(MensagemIntegracao mensagem)
    {
        if (mensagem.Type is "DiagnosticoIniciado" or "DiagnosticoConcluido") _diagnostico.Executar(mensagem);
        else if (mensagem.Type is "OrcamentoGerado" or "GeracaoOrcamentoRecusada") _orcamento.Executar(mensagem);
        else if (mensagem.Type is "OrcamentoAprovado" or "OrcamentoRecusado") _decisao.Executar(mensagem);
        else if (mensagem.Type is "ExecucaoCanceladaPorRecusaOrcamento" or "CancelamentoPorRecusaOrcamentoRecusado") _cancelamento.Executar(mensagem);
        else _processar.Executar(mensagem);
    }
}
