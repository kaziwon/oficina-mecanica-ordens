# language: pt
Funcionalidade: Atendimento completo distribuido da oficina
  As decisoes do cliente e os eventos da Saga devem manter os tres servicos consistentes.

  Cenario: Entregar um veiculo depois de aprovar, pagar e executar o reparo
    Dado que uma OS tem diagnostico e orcamento com servico de 100 e duas pecas de 50
    Quando o proprio cliente aprova seu orcamento
    Entao a Saga confirma um pagamento de 200 e libera a execucao
    Quando o mecanico inicia e conclui o reparo
    Entao a OS fica finalizada e o estoque tem 8 pecas sem reservas
    Quando o atendente registra a entrega duas vezes
    Entao o cliente ve a OS entregue com um unico registro de entrega

  Cenario: Recusar o orcamento antes de cobrar ou reservar pecas
    Dado que uma OS tem diagnostico e orcamento com servico de 100 e duas pecas de 50
    Quando o proprio cliente recusa seu orcamento
    Entao a OS e a execucao ficam canceladas sem pagamento nem consumo de estoque

  Cenario: Compensar um cancelamento depois do pagamento e da reserva
    Dado que uma OS tem diagnostico e orcamento com servico de 100 e duas pecas de 50
    Quando o proprio cliente aprova seu orcamento
    Entao a Saga confirma um pagamento de 200 e libera a execucao
    Quando o mecanico inicia o reparo e o cliente solicita o cancelamento ao atendente
    Entao a Saga confirma um estorno de 200 e cancela a OS preservando a cobranca original
    E as pecas reservadas sao liberadas sem repor estoque consumido
