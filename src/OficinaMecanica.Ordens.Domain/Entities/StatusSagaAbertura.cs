namespace OficinaMecanica.Ordens.Domain.Entities;

public enum StatusSagaAbertura
{
    AguardandoRegistro = 1, RegistroConfirmado, CompensacaoPendente, Compensada,
    FalhaRequerIntervencao, RegistroRecusado, EmDiagnostico, OrcamentoPendente, OrcamentoGerado
}
