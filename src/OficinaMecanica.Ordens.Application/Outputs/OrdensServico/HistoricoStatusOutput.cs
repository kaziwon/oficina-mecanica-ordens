using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.Application.Outputs.OrdensServico;

public class HistoricoStatusOutput
{
    public StatusOrdemServico Status { get; set; }
    public DateTime RegistradoEm { get; set; }
}
