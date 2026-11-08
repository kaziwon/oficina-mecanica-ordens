using OficinaMecanica.Ordens.Application.Exceptions;
using OficinaMecanica.Ordens.Application.Gateways;
using OficinaMecanica.Ordens.Application.Inputs.OrdensServico;
using OficinaMecanica.Ordens.Application.Mappers.OrdensServico;
using OficinaMecanica.Ordens.Application.Outputs.OrdensServico;
using OficinaMecanica.Ordens.Domain.Entities;
using OficinaMecanica.Ordens.Application.Messaging;

namespace OficinaMecanica.Ordens.Application.UseCases.OrdensServico;

public class CriarOrdemServicoUseCase
{
    private readonly IOrdemServicoGateway _ordens;
    private readonly IClienteGateway _clientes;
    private readonly IVeiculoGateway _veiculos;
    private readonly ICpfCnpjValidatorGateway _documentos;
    private readonly IPlacaVeiculoValidatorGateway _placas;

    public CriarOrdemServicoUseCase(
        IOrdemServicoGateway ordens,
        IClienteGateway clientes,
        IVeiculoGateway veiculos,
        ICpfCnpjValidatorGateway documentos,
        IPlacaVeiculoValidatorGateway placas)
    {
        _ordens = ordens;
        _clientes = clientes;
        _veiculos = veiculos;
        _documentos = documentos;
        _placas = placas;
    }

    public OrdemServicoOutput Executar(CriarOrdemServicoInput input)
    {
        if (string.IsNullOrWhiteSpace(input.DescricaoSolicitacao) || input.DescricaoSolicitacao.Trim().Length > 1000)
        {
            throw new ValidacaoException("Informe uma descricao da solicitacao com ate 1000 caracteres.");
        }

        var usarExistentes = !string.IsNullOrWhiteSpace(input.CpfCnpj) && input.VeiculoId.HasValue
            && input.Cliente is null && input.Veiculo is null;
        var criarNovos = string.IsNullOrWhiteSpace(input.CpfCnpj) && !input.VeiculoId.HasValue
            && input.Cliente is not null && input.Veiculo is not null;

        if (!usarExistentes && !criarNovos)
        {
            throw new ValidacaoException("Informe CpfCnpj e VeiculoId, ou os dados de Cliente e Veiculo, sem misturar as opcoes.");
        }

        var agora = DateTime.UtcNow;
        var ordem = new OrdemServico
        {
            Id = Guid.NewGuid(),
            DescricaoSolicitacao = input.DescricaoSolicitacao.Trim(),
            Status = StatusOrdemServico.Recebida,
            CriadaEm = agora
        };

        if (usarExistentes)
        {
            VincularCadastrosExistentes(ordem, input);
        }
        else
        {
            PrepararNovosCadastros(ordem, input);
        }

        ordem.Historico.Add(new HistoricoStatusOrdemServico
        {
            Id = Guid.NewGuid(),
            OrdemServicoId = ordem.Id,
            Status = ordem.Status,
            RegistradoEm = agora
        });

        var saga = SagaAbertura.Iniciar(ordem.Id, input.CorrelationId);
        var mensagem = MensagensAbertura.Registrar(saga, ordem.DescricaoSolicitacao);
        MensagemValidator.Validar(mensagem, "RegistrarExecucao");
        return OrdemServicoOutputMapper.Mapear(_ordens.Adicionar(ordem, saga, mensagem));
    }

    private void VincularCadastrosExistentes(OrdemServico ordem, CriarOrdemServicoInput input)
    {
        var cpf = ValidarDocumento(input.CpfCnpj!);
        var cliente = _clientes.ObterPorCpfCnpj(cpf)
            ?? throw new InvalidOperationException("Cliente ativo nao encontrado.");
        var veiculo = _veiculos.ObterPorId(input.VeiculoId!.Value)
            ?? throw new InvalidOperationException("Veiculo ativo nao encontrado.");

        if (veiculo.ClienteId != cliente.Id)
        {
            throw new InvalidOperationException("O veiculo informado nao pertence ao cliente.");
        }

        ordem.ClienteId = cliente.Id;
        ordem.VeiculoId = veiculo.Id;
    }

    private void PrepararNovosCadastros(OrdemServico ordem, CriarOrdemServicoInput input)
    {
        var clienteInput = input.Cliente!;
        var veiculoInput = input.Veiculo!;
        var cpf = ValidarDocumento(clienteInput.CpfCnpj);

        if (!_placas.EhValida(veiculoInput.Placa))
        {
            throw new ValidacaoException("A placa informada e invalida.");
        }

        var placa = _placas.Normalizar(veiculoInput.Placa);
        if (_clientes.ExistePorCpfCnpj(cpf))
        {
            throw new InvalidOperationException("Ja existe um cliente com este CPF/CNPJ.");
        }

        if (_veiculos.ExistePorPlaca(placa))
        {
            throw new InvalidOperationException("Ja existe um veiculo com esta placa.");
        }

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            Nome = clienteInput.Nome,
            CpfCnpj = cpf,
            Email = clienteInput.Email,
            Telefone = clienteInput.Telefone
        };
        var veiculo = new Veiculo
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Placa = placa,
            Marca = veiculoInput.Marca,
            Modelo = veiculoInput.Modelo,
            Ano = veiculoInput.Ano
        };

        // O gateway persiste este cadastro completo e a OS na mesma transacao local.
        ordem.ClienteId = cliente.Id;
        ordem.VeiculoId = veiculo.Id;
        ordem.Cliente = cliente;
        ordem.Veiculo = veiculo;
    }

    private string ValidarDocumento(string documento)
    {
        if (!_documentos.EhValido(documento))
        {
            throw new ValidacaoException("O CPF/CNPJ informado e invalido.");
        }

        return _documentos.Normalizar(documento);
    }
}
