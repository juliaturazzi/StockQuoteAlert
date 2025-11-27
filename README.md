# Stock Quote Alert - Monitor de Cotações de Ações

## 🎯 Visão Geral do Projeto

O **Stock Quote Alert** é um *Worker Service* desenvolvido em **.NET 8** para monitorar ativos da **B3 (Bolsa de Valores Brasileira)**.  
O sistema opera em background, consultando periodicamente a cotação de um ativo e disparando alertas via e-mail quando oportunidades de **Compra** ou **Venda** são identificadas com base em *targets* pré-definidos.

---

📸 **Preview do Alerta**

Abaixo está um exemplo do e-mail HTML enviado pelo sistema quando um gatilho é acionado:

![Preview do E-mail de Alerta](/docs/email_preview.png)

## ✨ Funcionalidades

### 1. Monitoramento de Ativos

**Consulta em Tempo Real**  
Integração com API externa para dados de mercado (B3).

**Análise de Decisão**  
Compara o preço atual (`CurrentPrice`) com os limites configurados:

- **Venda:** se `Preço Atual > Target Venda`
- **Compra:** se `Preço Atual < Target Compra`

---

### 2. Notificações Flexíveis (E-mail Opcional)

O sistema foi projetado para a configuração de notificações:

- **Modo Completo:**  
  Se as credenciais SMTP forem fornecidas no `.env`, o sistema envia e-mails em HTML com detalhes da cotação.

- **Modo "Apenas Logs":**  
  Se as configurações de e-mail não forem fornecidas, o sistema **não falha**.  
  Ele detecta automaticamente a ausência de configuração, suprime o envio de e-mail e registra o alerta apenas:
  - no **console**, e  
  - em **logs persistentes**.

---

### 3. Resiliência e Cooldown

Para evitar a inundação de e-mails (spamming) em momentos de alta volatilidade:

- **Mecanismo de Cooldown:**  
  Após enviar um alerta, o sistema "silencia" novos alertas do mesmo tipo por um período configurável (ex: `5 minutos`).

- **Reset Inteligente:**  
  Se o preço retornar à faixa neutra, o cooldown é resetado automaticamente.

---

## 🌐 Integração com API (Brapi)

O sistema utiliza a **Brapi** como fonte de dados financeiros.

### Frequência de Atualização

Dependendo do plano da sua chave de API, a frequência de atualização dos dados varia:

- **Plano Gratuito:** 30 minutos  
- **Plano Pago:** 15 minutos ou menos  

Ajuste a variável `MONITORING_CHECK_INTERVAL_MINUTES` no seu `.env` conforme seu plano.

### Autenticação e Ativos Gratuitos

O sistema gerencia a necessidade de tokens:

- **Ativos Gratuitos (Sem Token):**  
  A Brapi libera consulta sem autenticação para:
  - `PETR4`
  - `MGLU3`
  - `VALE3`
  - `ITUB4`

- **Ativos Restritos (Token Obrigatório):**  
  Para qualquer outro ativo, é necessário fornecer um `BRAPI_TOKEN`.  
  Se o token não for fornecido para um ativo restrito, o sistema bloqueia a requisição e avisa no log.

---

## 🧪 Estratégia de Testes (Quality Assurance)

A aplicação possui uma cobertura com **17 testes** utilizando **xUnit** e **Moq**.

| Camada                     | Arquivo                    | Qtd. | O que é testado?                                                                                                                                                 |
|----------------------------|----------------------------|------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Worker (Comportamento)     | `StockMonitorWorkerTests.cs` | 8    | Valida o ciclo de vida completo do Worker: alertas, Cooldown (supressão e reset), E-mail Opcional (não quebra) e tratamento de exceções/nulos da API.           |
| Domínio (Lógica)           | `StockAlertTests.cs`       | 3    | Testa a lógica pura de decisão (Preço vs Target).                                                                                                                |
| Integração (API)           | `BrapiServiceTests.cs`     | 5    | Simula respostas da Brapi. Testa leitura de JSON, erros HTTP e a lógica de validação de Token para ativos restritos vs. gratuitos.                               |
| Integração (Host)          | `HostIntegrationTests.cs`  | 1    | Verifica o Bootstrapping. Garante que a Injeção de Dependência (DI) monta todos os serviços e configurações corretamente ao iniciar.                             |
| Infraestrutura             | `SmtpEmailServiceTests.cs` | 1    | Garante que o serviço de e-mail constrói a mensagem e interage com a dependência SMTP corretamente.                                                              |

**Total:** `17` testes passando ✅

---

## 🪵 Observabilidade e Logs Persistentes

O sistema utiliza **Serilog** com saída dupla:

- Console
- Arquivo de log


Utilizamos **Volumes do Docker** para gravar logs na pasta `./logs` da sua máquina para obter um histórico auditável (`stock-alert-YYYY-MM-DD.log`) para investigar alertas passados.

---

## 🚀 Guia de Instalação e Execução

### 1. Configuração de Ambiente

O projeto utiliza um arquivo `.env.example` como guia.

**Passo Único:**  
Crie um arquivo chamado `.env` na raiz do projeto (copie o conteúdo de `.env.example`) e preencha suas informações.

> ⚠️ **Importante:**  
> O arquivo `.env` pode conter senhas. Nunca commite este arquivo no Git.

### Variáveis de Configuração

A tabela abaixo lista as variáveis que devem ser configuradas.  
As variáveis de Ticker e Preços são obrigatórias dependendo do modo de execução.

| Variável                          | Tipo      | Descrição                                                       | Status no `.env`                                   |
|-----------------------------------|-----------|-----------------------------------------------------------------|---------------------------------------------------|
| **Variáveis de Monitoramento**    |           |                                                                 |                                                   |
| `TICKER_TO_MONITOR`              | String    | O ativo da B3 que será monitorado.                              | Obrigatório (Opção B)                             |
| `PRICE_SELL_TARGET`              | Decimal   | Preço de referência para alerta de Venda.                      | Obrigatório (Opção B)                             |
| `PRICE_BUY_TARGET`               | Decimal   | Preço de referência para alerta de Compra.                     | Obrigatório (Opção B)                             |
| **Variáveis de E-mail (SMTP)**   |           |                                                                 |                                                   |
| `EMAIL_SMTP_USER`                | String    | Usuário de autenticação do servidor SMTP.                      | Opcional                                          |
| `EMAIL_SMTP_PASS`                | String    | Senha de App do servidor SMTP.                                 | Opcional                                          |
| `EMAIL_SMTP_SERVER`              | String    | Endereço do servidor de saída.                                 | Opcional                                          |
| `EMAIL_SMTP_PORT`                | Inteiro   | Porta do servidor SMTP.                                        | Opcional                                          |
| `EMAIL_SMTP_SENDER`              | String    | E-mail que aparecerá como remetente.                           | Opcional                                          |
| `EMAIL_SMTP_RECIPIENT`           | String    | E-mail de destino para recebimento dos alertas.                | Opcional                                          |
| **Variáveis de API e Monitoramento** |       |                                                                 |                                                   |
| `BRAPI_TOKEN`                    | String    | Token de autenticação da Brapi.                                | Opcional (Obrigatório para ativos restritos)      |
| `EMAIL_ALERT_COOL_DOWN`          | Booleano  | Ativa o modo de supressão de spam de e-mail.                   | Opcional (Default: `true`)                        |
| `EMAIL_ALERT_COOL_DOWN_SECONDS`  | Inteiro   | Tempo de espera após um alerta (em segundos).                  | Opcional (Default: `300`)                         |
| `MONITORING_CHECK_INTERVAL_MINUTES` | Inteiro | Frequência de checagem da API (em minutos).                    | Opcional (Default: `1`)                           |
| `MONITORING_API_BASE_URL`        | String    | Endereço base da API de cotação.                               | Opcional (Default: `https://brapi.dev/`)          |

---

## 2. Escolha como Executar

### 🟢 Opção A: Via Linha de Comando (Mais Flexível)

Neste modo, você passa o Ticker e os Preços diretamente, e o **Docker Compose** carrega as credenciais de e-mail do seu `.env`.

**Sintaxe:**

```bash
docker compose run --rm stock-alert <TICKER> <VENDA> <COMPRA>
```

### Motivo do `--rm`

O parâmetro `--rm` garante que o container seja removido automaticamente assim que a execução terminar (ou for cancelada).  
Isso evita o acúmulo de containers parados, mantendo seu ambiente limpo.

#### Exemplo prático:

```bash
docker compose run --rm stock-alert PETR4 22.67 22.59
```
**Requisito:** Nenhuma variável de Ticker/Preço é obrigatória no `.env`.  
Apenas as variáveis de **E-mail/Token** são necessárias para o funcionamento completo.

---

### 🔵 Opção B: Via Arquivo `.env` (Modo Automático / Servidor)

Neste modo, o container lê todas as informações de **Ticker** e **Preços** diretamente do arquivo `.env`.

1. **Abra o arquivo `.env`.**

2. **Defina OBRIGATORIAMENTE:**
   - `TICKER_TO_MONITOR`
   - `PRICE_SELL_TARGET`
   - `PRICE_BUY_TARGET`

3. **Execute o comando padrão:**

```bash
docker compose up --build
```

### 3. Acompanhamento

**Console:** Veja os logs em tempo real.

**Arquivos:** Verifique a pasta `logs/` na raiz do projeto.
