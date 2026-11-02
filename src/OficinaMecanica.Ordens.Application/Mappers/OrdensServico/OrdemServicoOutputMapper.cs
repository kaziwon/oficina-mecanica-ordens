using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;
using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.Mappers.OrdensServico;

internal static class OrdemServicoOutputMapper
{
    public static OrdemServicoOutput Mapear(OrdemServico ordem)
    {
        return new OrdemServicoOutput
        {
            Id = ordem.Id,
            ClienteId = ordem.ClienteId,
            VeiculoId = ordem.VeiculoId,
            DescricaoSolicitacao = ordem.DescricaoSolicitacao,
            Status = ordem.Status,
            CriadaEm = ordem.CriadaEm,
            Historico = ordem.Historico.OrderBy(item => item.RegistradoEm)
                .Select(item => new HistoricoStatusOutput
                {
                    Status = item.Status,
                    RegistradoEm = item.RegistradoEm
                }).ToList()
        };
    }
}
