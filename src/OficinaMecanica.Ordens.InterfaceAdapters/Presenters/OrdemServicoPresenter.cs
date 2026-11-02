using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;
using OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

namespace OficinaMecanica.Ordens.InterfaceAdapters.Presenters;

public static class OrdemServicoPresenter
{
    public static OrdemServicoResponseDto Apresentar(OrdemServicoOutput output)
    {
        return new OrdemServicoResponseDto
        {
            Id = output.Id,
            ClienteId = output.ClienteId,
            VeiculoId = output.VeiculoId,
            DescricaoSolicitacao = output.DescricaoSolicitacao,
            Status = output.Status,
            CriadaEm = output.CriadaEm,
            Historico = output.Historico.Select(item => new HistoricoStatusResponseDto
            {
                Status = item.Status,
                RegistradoEm = item.RegistradoEm
            }).ToList()
        };
    }
}
