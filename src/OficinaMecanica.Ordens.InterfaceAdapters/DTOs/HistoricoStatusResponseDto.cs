using OficinaMecanica.Ordens.Domain.Entities;

namespace OficinaMecanica.Ordens.InterfaceAdapters.DTOs;

public class HistoricoStatusResponseDto
{
    public StatusOrdemServico Status { get; set; }
    public DateTime RegistradoEm { get; set; }
}
