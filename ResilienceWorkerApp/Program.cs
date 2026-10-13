using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class Program
{
    // Contador estático para simular falhas nas primeiras tentativas
    private static int _attempts = 0;

    public static async Task Main(string[] args)
    {
        // 1. Geração do CorrelationId (Rastreabilidade de ponta a ponta)
        var correlationId = Guid.NewGuid().ToString();

        Log("INFO", "Iniciando processamento do worker com resiliência", correlationId);

        // 2. Execução da operação protegida pelo padrão de Retry
        bool success = await ExecuteWithRetryAsync(
            action: () => UnstableExternalApiCall(),
            maxRetries: 3,
            correlationId
        );

        // 3. Log do resultado final do Job
        Log("INFO", $"Resultado final do job: {(success ? "Sucesso" : "Falha definitiva")}", correlationId);
    }

    // Simula uma API ou banco instável que falha 2 vezes e sucede na 3ª
    private static bool UnstableExternalApiCall()
    {
        _attempts++;
        if (_attempts < 3)
        {
            throw new HttpRequestException("Timeout de conexão com o microsserviço de loja/inventário.");
        }
        return true;
    }

    // 4. O Coração da Resiliência: Retry com Exponential Backoff
    private static async Task<bool> ExecuteWithRetryAsync(Func<bool> action, int maxRetries, string correlationId)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Log("DEBUG", $"Tentativa {attempt} de {maxRetries} em andamento...", correlationId);

                // Executa a ação passada via delegate
                var result = action();

                Log("INFO", $"Operação bem-sucedida na tentativa {attempt}.", correlationId);
                return result;
            }
            catch (Exception ex)
            {
                Log("WARN", $"Falha na tentativa {attempt}: {ex.Message}", correlationId);

                // Se atingiu o limite de tentativas, encerra retornando falso
                if (attempt == maxRetries)
                {
                    Log("ERROR", "Número máximo de tentativas esgotado. O job falhou.", correlationId);
                    return false;
                }

                // Cálculo do Exponential Backoff: o tempo de espera dobra a cada falha (500ms, 1000ms...)
                int delayMs = (int)Math.Pow(2, attempt) * 250;

                Log("DEBUG", $"Aguardando {delayMs}ms antes da próxima tentativa...", correlationId);
                await Task.Delay(delayMs);
            }
        }
        return false;
    }

    // 5. Sistema de Log Estruturado em Formato JSON
    private static void Log(string level, string message, string correlationId)
    {
        var logEntry = new
        {
            Timestamp = DateTime.UtcNow.ToString("o"), // Formato ISO 8601 padronizado
            Level = level,
            CorrelationId = correlationId,
            ThreadId = Thread.CurrentThread.ManagedThreadId,
            Message = message
        };

        // Serializa o objeto anônimo para JSON e imprime no console
        Console.WriteLine(JsonSerializer.Serialize(logEntry));
    }
}