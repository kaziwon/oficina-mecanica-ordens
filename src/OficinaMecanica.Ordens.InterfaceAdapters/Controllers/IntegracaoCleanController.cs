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
    private readonly ProcessarResultadoPagamentoUseCase _pagamento;
    private readonly ProcessarRespostaLiberacaoUseCase _liberacao;
    private readonly ProcessarReparoIniciadoUseCase _reparo;
    private readonly ProcessarReparoConcluidoUseCase _conclusaoReparo;
    private readonly ProcessarCancelamentoPagoUseCase _cancelamentoPago;
    private readonly ProcessarResultadoEstornoUseCase _estorno;
    public IntegracaoCleanController(ProcessarRespostaAberturaUseCase processar,
        ProcessarEventoDiagnosticoUseCase diagnostico, ProcessarRespostaOrcamentoUseCase orcamento,
        ProcessarDecisaoOrcamentoUseCase decisao, ProcessarCancelamentoPorRecusaUseCase cancelamento,
        ProcessarResultadoPagamentoUseCase pagamento, ProcessarRespostaLiberacaoUseCase liberacao,
        ProcessarReparoIniciadoUseCase reparo, ProcessarReparoConcluidoUseCase conclusaoReparo,
        ProcessarCancelamentoPagoUseCase cancelamentoPago, ProcessarResultadoEstornoUseCase estorno)
    { _processar = processar; _diagnostico = diagnostico; _orcamento = orcamento; _decisao = decisao; _cancelamento = cancelamento; _pagamento = pagamento; _liberacao = liberacao; _reparo = reparo; _conclusaoReparo = conclusaoReparo; _cancelamentoPago = cancelamentoPago; _estorno = estorno; }
    public void Processar(MensagemIntegracao mensagem)
    {
        if (mensagem.Type is "DiagnosticoIniciado" or "DiagnosticoConcluido") _diagnostico.Executar(mensagem);
        else if (mensagem.Type is "OrcamentoGerado" or "GeracaoOrcamentoRecusada") _orcamento.Executar(mensagem);
        else if (mensagem.Type is "OrcamentoAprovado" or "OrcamentoRecusado") _decisao.Executar(mensagem);
        else if (mensagem.Type is "ExecucaoCanceladaPorRecusaOrcamento" or "CancelamentoPorRecusaOrcamentoRecusado") _cancelamento.Executar(mensagem);
        else if (mensagem.Type is "PagamentoConfirmado" or "PagamentoRecusado" or "FalhaProcessamentoPagamento") _pagamento.Executar(mensagem);
        else if (mensagem.Type is "ExecucaoLiberada" or "LiberacaoExecucaoRecusada") _liberacao.Executar(mensagem);
        else if (mensagem.Type == "ReparoIniciado") _reparo.Executar(mensagem);
        else if (mensagem.Type == "ReparoConcluido") _conclusaoReparo.Executar(mensagem);
        else if (mensagem.Type is "ReparoPagoCancelado" or "CancelamentoReparoPagoRecusado") _cancelamentoPago.Executar(mensagem);
        else if (mensagem.Type is "EstornoConfirmado" or "EstornoRecusado" or "FalhaProcessamentoEstorno") _estorno.Executar(mensagem);
        else _processar.Executar(mensagem);
    }
}
