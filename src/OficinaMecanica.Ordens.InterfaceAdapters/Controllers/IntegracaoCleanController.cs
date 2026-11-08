using OficinaMecanica.Ordens.Application.Messaging;
using OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Controllers;

public class IntegracaoCleanController
{
    private readonly ProcessarRespostaAberturaUseCase _processar;
    public IntegracaoCleanController(ProcessarRespostaAberturaUseCase processar) { _processar = processar; }
    public void Processar(MensagemIntegracao mensagem) => _processar.Executar(mensagem);
}
