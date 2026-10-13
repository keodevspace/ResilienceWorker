// Isolamos a lógica em uma classe auxiliar (TestableRetryHelper) em vez de deixar tudo preso dentro do Program.Main. Isso é fundamental para conseguir testar métodos estáticos ou lógicas complexas sem depender da execução do console.
using Xunit;

public class WorkerTests
{
    [Fact]
    public async Task ExecuteWithRetryAsync_ShouldSucceed_WhenActionSucceedsOnRetry()
    {
        // Arrange
        int attempts = 0;
        // Simula uma função que falha 2 vezes e sucede na 3ª
        Func<bool> unstableAction = () =>
        {
            attempts++;
            if (attempts < 3) throw new HttpRequestException("Falha temporária");
            return true;
        };

        // Act
        // Chamamos a lógica de retry simulando um método helper ou extraindo a lógica para uma classe testável.
        // (Nota: No exemplo anterior colocamos tudo estático no Program.cs. Para testar fácil, 
        // o ideal é isolar a lógica de retry em uma classe de serviço, ex: RetryPolicyService).
        bool success = await TestableRetryHelper.ExecuteWithRetryAsync(unstableAction, maxRetries: 3);

        // Assert
        Assert.True(success);
        Assert.Equal(3, attempts);
    }
}

// Helper refatorado para facilitar o teste unitário (separando a regra de negócio do Console.WriteLine principal)
public static class TestableRetryHelper
{
    public static async Task<bool> ExecuteWithRetryAsync(Func<bool> action, int maxRetries)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return action();
            }
            catch
            {
                if (attempt == maxRetries) return false;
                await Task.Delay(10); // Delay menor para o teste rodar instantaneamente
            }
        }
        return false;
    }
}