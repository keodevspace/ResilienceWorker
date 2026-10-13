Decisões tomadas no código

- O cenário aqui é um **Worker Service** em background (muito comum para processar filas, inventário de loja, entrega de itens de jogos) que chama um serviço externo instável. Se a chamada falhar (por timeout ou queda de rede), não podemos simplesmente desistir ou crashar o worker: precisamos aplicar **Retry com Exponential Backoff** e registrar tudo em logs estruturados que permitam rastrear a execução.
- console o fluxo rodando: ele vai tentar, falhar nas duas primeiras com nível WARN, esperar o tempo do backoff e suceder na terceira tentativa, imprimindo cada passo em um JSON estruturado!

-------------------

A. O CorrelationId (Guid.NewGuid().ToString())
Linha: var correlationId = Guid.NewGuid().ToString();

Por que foi feita: Numa arquitetura de microsserviços e workers (como os que sustentam loja, ranking e launcher), uma única ação do jogador gera dezenas de logs espalhados por vários serviços. O CorrelationId funciona como um "número de rastreio de encomenda".

OpenTelemetry — porque debugar produção com achismo é caro. O conceito por trás do OpenTelemetry/Tracing começa exatamente aqui: correlacionar eventos de ponta a ponta.

-------------------

B. A Estratégia de Delegação (Func<bool> action)
Linha: private static async Task<bool> ExecuteWithRetryAsync(Func<bool> action, ...)

Por que foi feita: Em vez de misturar a lógica de resiliência (o loop de erro) com a lógica de negócio (chamar a API), usamos um Func<bool> (um ponteiro para função/método). Isso garante o Princípio da Responsabilidade Única (SOLID): a função de retry cuida apenas de tentar de novo, sem saber o que a API faz de verdade.

-------------------

C. O Cálculo do Exponential Backoff
Linha: int delayMs = (int)Math.Pow(2, attempt) * 250; e await Task.Delay(delayMs);

Por que foi feita: Se o banco de dados ou a API externa engasgar sob pico de carga (ex: lançamento de evento no jogo com milhares de jogadores comprando ao mesmo tempo), se sua aplicação tentar reconectar instantaneamente a cada 10 milissegundos (Retry Linear), ela vai agir como um ataque de negação de serviço (DDoS) contra o próprio banco/serviço, derrubando-o de vez.

O Exponential Backoff faz a aplicação esperar um tempo progressivamente maior a cada tentativa (Tentativa 1: espera 500ms; Tentativa 2: espera 1000ms; Tentativa 3: espera 2000ms), dando tempo para o servidor se recuperar.

-------------------

D. Logs Estruturados em JSON (JsonSerializer.Serialize(logEntry))
Linha: Console.WriteLine(JsonSerializer.Serialize(logEntry));

Por que foi feita: Logs em texto puro (Console.WriteLine("Erro ao conectar")) são impossíveis de filtrar em ferramentas de observabilidade como Datadog, AWS CloudWatch ou Kibana. Ao transformar o log em um objeto JSON estruturado contendo Timestamp, Level, CorrelationId e Message, você permite que a ferramenta crie filtros automáticos, gráficos de erros e alertas inteligentes (ex: "disparar alerta se o nível ERROR com o mesmo CorrelationId acontecer mais de 5 vezes").
