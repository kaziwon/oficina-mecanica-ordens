namespace OficinaMecanica.Ordens.IntegrationTests;

public sealed class SqsTheoryAttribute : TheoryAttribute
{
    public SqsTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQS_TEST_ENDPOINT")))
            Skip = "Defina SQS_TEST_ENDPOINT para executar com simulador SQS local.";
    }
}
