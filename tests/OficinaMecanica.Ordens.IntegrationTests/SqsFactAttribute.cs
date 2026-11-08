namespace OficinaMecanica.Ordens.IntegrationTests;

public sealed class SqsFactAttribute : FactAttribute
{
    public SqsFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQS_TEST_ENDPOINT")))
            Skip = "Defina SQS_TEST_ENDPOINT para executar com simulador SQS local.";
    }
}
