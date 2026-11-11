namespace OficinaMecanica.Ordens.Domain.Entities;

public enum StatusSagaAbertura
{
    AguardandoRegistro = 1, RegistroConfirmado, CompensacaoPendente, Compensada,
    FalhaRequerIntervencao, RegistroRecusado, EmDiagnostico, OrcamentoPendente, OrcamentoGerado,
    AguardandoPagamento, CancelamentoPorRecusaPendente, OrcamentoRecusado, PagamentoConfirmado,
    LiberacaoExecucaoPendente, ExecucaoLiberada, EmExecucao, Finalizada
}
