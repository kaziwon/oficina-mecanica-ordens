using OficinaMecanica.Ordens.Application.Inputs.Clientes;

namespace OficinaMecanica.Ordens.Application.Inputs.OrdensServico;

public class CriarOrdemServicoInput
{
    public CriarOrdemServicoInput(
        string? cpfCnpj,
        Guid? veiculoId,
        CriarClienteInput? cliente,
        CriarVeiculoDaOrdemInput? veiculo,
        string descricaoSolicitacao)
    {
        CpfCnpj = cpfCnpj;
        VeiculoId = veiculoId;
        Cliente = cliente;
        Veiculo = veiculo;
        DescricaoSolicitacao = descricaoSolicitacao;
    }

    public string? CpfCnpj { get; }
    public Guid? VeiculoId { get; }
    public CriarClienteInput? Cliente { get; }
    public CriarVeiculoDaOrdemInput? Veiculo { get; }
    public string DescricaoSolicitacao { get; }
    public string? CorrelationId { get; set; }
}
