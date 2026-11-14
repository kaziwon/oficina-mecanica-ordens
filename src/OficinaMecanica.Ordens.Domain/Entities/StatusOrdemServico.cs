namespace OficinaMecanica.Ordens.Domain.Entities;

public enum StatusOrdemServico
{
    Recebida = 1,
    EmDiagnostico = 2,
    AguardandoAprovacao = 3,
    EmExecucao = 4,
    Finalizada = 5,
    Entregue = 6,
    Cancelada = 7,
    AguardandoPagamento = 8,
    PagamentoConfirmado = 9,
    AguardandoExecucao = 10,
    CancelamentoPendente = 11
}
