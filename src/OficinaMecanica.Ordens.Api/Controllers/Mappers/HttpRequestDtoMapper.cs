using CleanDtos = OficinaMecanica.Ordens.InterfaceAdapters.DTOs;
using HttpDtos = OficinaMecanica.Ordens.Controllers.DTOs;

namespace OficinaMecanica.Ordens.Controllers.Mappers;

public static class HttpRequestDtoMapper
{
    public static CleanDtos.LoginRequestDto ParaCleanDto(this HttpDtos.LoginRequestDto dto)
        => new() { Username = dto.Username, Password = dto.Password };

    public static CleanDtos.ClienteRequestDto ParaCleanDto(this HttpDtos.ClienteRequestDto dto)
        => new()
        {
            Nome = dto.Nome,
            CpfCnpj = dto.CpfCnpj,
            Email = dto.Email,
            Telefone = dto.Telefone
        };

    public static CleanDtos.VeiculoRequestDto ParaCleanDto(this HttpDtos.VeiculoRequestDto dto)
        => new()
        {
            ClienteId = dto.ClienteId,
            Placa = dto.Placa,
            Marca = dto.Marca,
            Modelo = dto.Modelo,
            Ano = dto.Ano
        };

    public static CleanDtos.OrdemServicoRequestDto ParaCleanDto(this HttpDtos.OrdemServicoRequestDto dto)
        => new()
        {
            CpfCnpj = dto.CpfCnpj,
            VeiculoId = dto.VeiculoId,
            DescricaoSolicitacao = dto.DescricaoSolicitacao,
            Cliente = dto.Cliente?.ParaCleanDto(),
            Veiculo = dto.Veiculo is null ? null : new CleanDtos.OrdemServicoVeiculoRequestDto
            {
                Placa = dto.Veiculo.Placa,
                Marca = dto.Veiculo.Marca,
                Modelo = dto.Veiculo.Modelo,
                Ano = dto.Veiculo.Ano
            }
        };
}
