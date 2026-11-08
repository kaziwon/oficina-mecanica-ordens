using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

public class IntegracaoCleanController
{
    private readonly ProcessarRespostaAberturaUseCase _processar;
    private readonly ProcessarEventoDiagnosticoUseCase _diagnostico;
    private readonly ProcessarRespostaOrcamentoUseCase _orcamento;
    public IntegracaoCleanController(ProcessarRespostaAberturaUseCase processar,
        ProcessarEventoDiagnosticoUseCase diagnostico, ProcessarRespostaOrcamentoUseCase orcamento)
    { _processar = processar; _diagnostico = diagnostico; _orcamento = orcamento; }
    public void Processar(MensagemIntegracao mensagem)
    {
        if (mensagem.Type is "DiagnosticoIniciado" or "DiagnosticoConcluido") _diagnostico.Executar(mensagem);
        else if (mensagem.Type is "OrcamentoGerado" or "GeracaoOrcamentoRecusada") _orcamento.Executar(mensagem);
        else _processar.Executar(mensagem);
    }
}
