# Estado do Projeto — boleto_protest_cs

> Arquivo de referência rápida. Mantido atualizado a cada marco importante.
> Objetivo: permitir retomar o projeto sem precisar reler o histórico completo do chat.
> **IMPORTANTE**: este arquivo precisa ser salvo em `docs/estado-projeto.md` dentro do
> repositório e commitado — ele não existe automaticamente, precisa ser adicionado
> manualmente ao projeto depois de cada atualização.

## O que o projeto faz

Automação que: busca no Gmail da Dalgiza o email MAIS RECENTE da PROTEST avisando
boleto de condomínio gerado → verifica, para CADA apartamento em
`ApartamentosPossuidos`, se já existe um PDF salvo com aquele vencimento (se sim,
ignora — já foi processado antes) → baixa e salva os PDFs que ainda faltam (um por
apartamento) → identifica o apartamento de cada PDF (via texto extraído do PDF) →
filtra e monta UM rascunho de email só com os boletos dos apartamentos que a
imobiliária gerencia (`ApartamentosGerenciados`) → cria o rascunho na conta Gmail da
Dalgiza (nunca envia — fica pra revisão humana antes de enviar pro Daniel/imobiliária).

## Stack

- C# 14 / .NET 10, 3 projetos: `BoletoProtest.Core` (models), `.Infrastructure`
  (services), `.Console` (entry point). Testes em `tests/BoletoProtest.Infrastructure.Tests`
  (xUnit v3).
- Libs: `Google.Apis.Gmail.v1` (auth OAuth2 + API Gmail), `PdfPig` (extrair texto
  de PDF), `MimeKit` (montar mensagem MIME).
- **Validado rodando ponta-a-ponta com sucesso, inclusive no PC real da Dalgiza
  (Windows 10), como `.exe` publicado.**

## Arquitetura atual (services em `Infrastructure/Services/`)

- **`GmailAuthService`** (static) — `BuscaGmailService() : Task<GmailService>`.
  OAuth2 via `GoogleWebAuthorizationBroker`, credenciais em
  `~/.config/boletoprotest/client_secret.json` (mesmo arquivo pode ser reusado
  entre usuários/máquinas — ele identifica o APP, não o usuário; cada pessoa loga
  com sua própria conta Gmail durante o fluxo de autorização). Cache de token em
  `~/.config/boletoprotest/tokens/`. **Atenção**: se o projeto Google Cloud estiver
  em modo "Testing" (OAuth consent screen), só contas cadastradas como "Test users"
  conseguem autorizar — precisa adicionar `dalgizaborges@gmail.com` lá antes dela
  rodar pela primeira vez.
- **`BoletoParserService`** (static, sem estado) — parsing puro de texto: extrai
  data de vencimento (`ExtraiVencimento`) e URLs de boleto (`ExtraiUrlsDeBoletos`)
  de dentro do corpo HTML decodificado de um email. Sem rede, 100% testável.
- **`GmailMessageService`** (instância, recebe `GmailService` no construtor):
  - `BuscaEmailMaisRecente(AppConfig) : Task<Message?>` — busca TODOS os emails que
    batem em assunto/remetente (sem filtro de data — evita cortar o email certo por
    causa de suposição errada sobre "mês anterior/atual"), baixa os `Message`s
    completos, e escolhe o mais recente comparando `InternalDate` (epoch ms)
    manualmente via `MaxBy` — NÃO confia na ordem de retorno da API (documentada
    como mais-recente-primeiro, mas há relatos de casos fora de ordem).
  - `FiltraBoletosEncontrados` — processa só o email mais recente (não itera mais
    sobre uma lista de vários emails).
  - `BaixaPdf(AppConfig) : Task<List<Boleto>>` — para cada boleto encontrado: baixa
    bytes via HTTP, extrai apartamento via `PdfService`, monta `Boleto` completo,
    **checa via `FileService.JaExiste(boleto)` se já foi processado antes** — se
    sim, `continue` (pula: não sobrescreve, não entra na lista retornada, não vai
    pro rascunho — assume-se que o ciclo already foi completado numa execução
    anterior, incluindo rascunho já criado). Só salva e adiciona à lista os boletos
    genuinamente novos.
- **`PdfService`** (static) — `FormataNumeroApartamento(byte[] pdfBytes,
  string senha) : string`. Usa PdfPig (`ParsingOptions.Password`), regex interno
  para achar "UNIDADE: BL X - AP NNNN" e reformatar para "NNNN-X". 3 catches
  irmãos (PdfDocumentFormatException, PdfDocumentEncryptedException,
  FormatException).
- **`FileService`** (instância, recebe `pastaDestino` já resolvida — com `{{apto}}`
  substituído — no construtor):
  - `CaminhoCompletoDoArquivo(Boleto) : string` — nome do arquivo é
    `{apartamento}_vencimento_{dd-MM-yyyy}.pdf` (mudou de formato — antes era só
    `Vencimento_dd-MM-yyyy.pdf`, sem o apartamento no nome, já que a pasta
    diferenciava; agora o apartamento também está no nome do arquivo).
  - `JaExiste(Boleto) : bool` — checa `File.Exists` no caminho calculado. Usado por
    `GmailMessageService.BaixaPdf` antes de baixar/sobrescrever.
  - `SalvaArquivoPdf(byte[], Boleto) : Task`.
- **`GmailDraftService`** (instância, construtor `(List<Boleto> boletosBaixados,
  AppConfig appConf, GmailService gmailService)`) — FILTRA no construtor por
  `appConf.ApartamentosGerenciados` (lança `InvalidOperationException` se nenhum
  boleto bater). `SubstituiPalavraChave` é `internal` (testável), usa `substituto
  ??= ...` (não `IsNullOrEmpty`!) para permitir string vazia como valor válido de
  substituição (ex: `{{plural}}` no singular). Monta MIME via MimeKit, serializa +
  base64url, cria `Draft`/`Message` da lib do Google, chama
  `Users.Drafts.Create(...)`.

## Models (`Core/Models/Boleto.cs`)

```csharp
public record Boleto(string Link, string Apartamento, DateTime Vencimento);
public record BoletoEncontrado(string Link, DateTime Vencimento); // estágio
  // intermediário: sabe link+data (extraídos do email) mas ainda não sabe o
  // apartamento (só descoberto depois de baixar+ler o PDF)
```

## Convenção de templates (appsettings.json)

`{{chave}}` (chaves duplas — trocado de `[[chave]]` original via grep, decisão de
estilo do usuário). Chaves em uso: `{{apto}}`, `{{dtVenc}}`, `{{plural}}`.
Exemplo real de `Corpo`:
```
"Segue em anexo o{{plural}} boleto{{plural}} do{{plural}} apto{{plural}} {{apto}}
da Rua das Laranjeiras 336, com vencimento em {{dtVenc}}..."
```
`plural` é `"s"` se `_boletos.Count > 1`, senão `""` — sempre chamado via
`SubstituiPalavraChave(texto, plural, "{{plural}}")` (nunca condicional em volta da
chamada, pra não deixar `{{plural}}` sobrando quando é 1).

**ATENÇÃO — lição aprendida**: ao migrar `[[]]` → `{{}}`, algumas ocorrências ficaram
para trás em código de PRODUÇÃO (não só em templates/JSON) — encontradas em
`GmailDraftService.cs` (3x) e `GmailMessageService.cs` (1x), causando bugs reais
(marcadores nunca sendo substituídos). Se precisar fazer troca de sintaxe parecida
no futuro: `grep -rn '\[\[' src/ --include='*.cs'` para varrer TODO o código, não só
os arquivos óbvios.

## Bugs reais já corrigidos (não repetir)

1. **`AppConfig` dessincronizado do `appsettings.json`** — `Bind()` falha
   silenciosamente se as chaves não baterem (sem erro nem warning). Checar
   sempre os dois lados ao renomear campos.
2. **Vencimento aproximado (`DateTime.Now.AddMonths(1)`)** — substituído por
   extração real do texto do email via `BoletoParserService.ExtraiVencimento`.
3. **`Regex.Match` (singular) só pegava o 1º boleto** de emails com vários
   blocos empilhados — trocado por `Regex.Matches` (plural).
4. **`&amp;` vs `&`** — o HTML real usa `&amp;` como separador de query string;
   usar `&` cru cortava a URL no meio, causando PDF corrompido/inválido.
5. **URL de descadastro capturada por engano** — `appConf.UrlBoleto` sozinho
   ("/Operacional") batia também com `/Operacional/RemoverEmail.aspx` (link de
   descadastro no rodapé de cada bloco). Regex agora exige
   `/PopUp/pCli_BoletoNovo.aspx` como parte obrigatória do padrão.
6. **URLs duplicadas** — cada link aparece 2x no HTML (dentro do `href=""` e
   como texto visível) — `.Distinct()` resolve.
7. **`{{apto}}` nunca substituído em `PastaDestino`** — corrigido: cada boleto
   tem sua própria pasta (`FileService` instanciado dentro do loop, com
   `.Replace` aplicado antes, usando o apartamento já extraído do PDF daquele
   boleto específico).
8. **`GmailDraftService` filtrava por apartamento ERRADO / não filtrava** —
   agora filtra corretamente no construtor por `ApartamentosGerenciados`, com
   guard contra lista vazia (`throw` se nenhum boleto bater — sinal de erro de
   config, não de fluxo normal).
9. **Sobras de `[[chave]]` não migradas para `{{chave}}`** — ver seção acima.
10. **`SubstituiPalavraChave` usava `string.IsNullOrEmpty(substituto)`** para
    decidir se usa o valor padrão — isso tratava `substituto = ""` (string
    vazia, valor INTENCIONAL para o caso singular de `{{plural}}`) como "não
    foi passado", substituindo pelo valor padrão errado (lista de apartamentos)
    em vez de "". Corrigido para `substituto is null`/`??=`.
11. **`CpfPrefixo` de dev era fictício ("0573")**, mas os PDFs de teste em
    `TestData/` são os REAIS, baixados de produção, protegidos com o CPF real
    da Dalgiza ("4776"). Ajustado.
12. **[SESSÃO ATUAL] Busca de emails processava TODOS os meses acumulados**,
    não só o mês atual — resultado real observado: rascunho com "1509-A"
    repetido 7x (um por mês de histórico), 7 PDFs antigos reanexados. Causa:
    `FiltraEmailsPorData` nunca de fato filtrava por data (nome do método
    mentia sobre o que fazia); tentativa de correção via `after:`/`before:`
    calculando "mês anterior/atual" também falhou, porque a PROTEST não manda
    o email num dia fixo do mês (variável, ~15-20 dias antes do vencimento) —
    testado num dia em que o cálculo de mês excluía o email real. **Solução
    definitiva**: abandonar cálculo de data relativa; buscar TODOS os emails
    (sem `after`/`before`), escolher o mais recente comparando `InternalDate`
    (epoch ms) de cada `Message`, processar só esse.
13. **[SESSÃO ATUAL] Nada impedia reprocessar/sobrescrever um boleto já
    baixado** numa execução anterior. Corrigido com `FileService.JaExiste`,
    checado por apartamento (via nome do arquivo, que já inclui apartamento +
    vencimento) antes de salvar — se já existe, `continue` (pula download
    subsequente ao HTTP já feito, não sobrescreve, não entra na lista
    retornada nem no rascunho). Decisão consciente: checagem acontece DEPOIS
    do download HTTP (não antes) — requisição de rede é barata e não tem efeito
    colateral; sobrescrever arquivo é o risco real a evitar.

## Testes automatizados

`tests/BoletoProtest.Infrastructure.Tests/` — status na última verificação: **29/29
passando, 0 warnings, 0 errors**. **ATENÇÃO**: os testes de `GmailMessageService`
(se algum dia forem criados) e o teste geral do fluxo de `BaixaPdf` provavelmente
ficaram desatualizados após a reestruturação desta sessão (`BuscaMensagensGmail` →
`BuscaEmailMaisRecente`, retorno de lista para `Message?` único, adição de
`FileService.JaExiste`) —**rodar `dotnet test` de novo antes de assumir que
continua tudo verde**.

- `PdfServiceTest.cs` — `[Theory]` com os 4 PDFs reais (1503-A, 1508-A, 1509-A,
  1515-B), senha `"4776"` (CPF real da Dalgiza — os PDFs de teste SÃO os reais).
  Teste de "senha errada" documenta comportamento REAL observado: esses PDFs não
  têm restrição de leitura (só possivelmente de edição), então PdfPig lê o
  conteúdo mesmo com senha incorreta — teste não espera exceção, propositalmente.
- `BoletoParserServiceTest.cs` — usa `TestData/ConteudoEmailReal.txt` como fixture.
- `FileServiceTest.cs` — usa `IDisposable` + pasta temporária. Usa
  `TestContext.Current.CancellationToken` nas chamadas assíncronas (resolve warning
  xUnit1051). **Provavelmente precisa de um teste novo para `JaExiste`** (ainda não
  criado nesta sessão).
- `GmailDraftServiceTest.cs` — usa um `GmailService` "vazio" (sem credenciais
  reais) já que não faz chamada de rede nos testes.
- `TestData/` contém: 4 PDFs reais (`Boleto_XXXX-Y.pdf`) + `ConteudoEmailReal.txt`.
  Esses arquivos precisam existir FISICAMENTE no repositório — não bastam os
  `.cs`/`.csproj` que descrevem os testes.
- `InternalsVisibleTo` no `.csproj` do Infrastructure, apontando pro projeto de
  testes — permite testar métodos `internal` sem torná-los `public`.
- **Não testado de propósito**: `GmailAuthService`, `BuscaEmailMaisRecente`,
  `BaixaPdf`, `CriaRascunho` — dependem de rede real/API do Google. Validação é
  manual.

## Deploy / publicação

- Comando de publish (IMPORTANTE: apontar pro projeto Console especificamente —
  rodar na raiz da solution tenta publicar Core/Infrastructure/Tests também, que
  são bibliotecas sem `Main`, causando erro `NETSDK1099`):
  ```
  dotnet publish src/BoletoProtest.Console -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
  ```
  `.exe` final em:
  `src/BoletoProtest.Console/bin/Release/net10.0/win-x64/publish/BoletoProtest.Console.exe`
  (Publicar só o projeto Console já é suficiente — ele referencia Core e
  Infrastructure via `ProjectReference`, e tudo fica embutido dentro do único
  `.exe` por causa do `PublishSingleFile`.)
- **Tamanho do `.exe`**: ~90MB — normal para self-contained (inclui o runtime
  .NET inteiro embutido, não só o código do projeto). Se precisar reduzir, opções
  futuras: `--self-contained false` (exige .NET 10 runtime já instalado na
  máquina de destino) ou trimming (`-p:PublishTrimmed=true`, mais arriscado,
  pode quebrar reflection usada por libs como MimeKit/PdfPig).
- **O que NÃO vai embutido no `.exe`** (precisa ser levado/configurado à parte):
  1. `appsettings.json` — copiado automaticamente para a pasta `publish/`, ao
     lado do `.exe` (não embutido dentro dele). Precisa ir junto na entrega.
  2. `client_secret.json` — NÃO fica na pasta `publish/`. Precisa ser colocado
     manualmente em `C:\Users\Dalgiza\.config\boletoprotest\client_secret.json`
     (mesmo arquivo do dev pode ser reusado — identifica o app, não o usuário).
  3. Pasta `tokens/` — criada automaticamente pelo programa na primeira execução
     (depois da Dalgiza autorizar via navegador com a conta dela), não precisa
     ser levada manualmente.
- **VALIDADO**: rodou com sucesso no PC real da Dalgiza (Windows 10), como `.exe`
  publicado dessa forma.
- Console não fecha automaticamente se rodado via terminal já aberto
  (PowerShell/CMD `cd` até a pasta + rodar o `.exe` de lá) — fecha sozinho se
  clicado duas vezes no Explorer. Se quiser que feche só depois de uma tecla
  (útil pra Dalgiza ver mensagem final antes de fechar), adicionar no fim do
  `Main`: `Console.WriteLine("Pressione qualquer tecla..."); Console.ReadKey();`
  (ainda não implementado, ideia registrada).

## Pendências reais (ordem sugerida)

1. **Rodar `dotnet test` de novo** — a reestruturação desta sessão
   (`GmailMessageService`) pode ter quebrado testes existentes; nenhum teste
   novo para `FileService.JaExiste` foi criado ainda.
2. **Sistema de logging estruturado** — hoje é só `Util.MensagemEx`
   (`Console.WriteLine` cru) + vários `Console.WriteLine` de debug espalhados
   (adicionados durante troubleshooting desta sessão — avaliar se devem virar
   log estruturado ou ser removidos/reduzidos antes de produção "limpa").
   Considerar `Microsoft.Extensions.Logging` ou Serilog: níveis, persistência em
   arquivo (importante pro `.exe` rodando sem terminal visível — hoje não há
   registro do que aconteceu numa execução automática/agendada).
3. **Abrir navegador automaticamente no rascunho criado** (ainda pendente,
   adiado de propósito — usar `Draft.Id` pra montar URL
   `https://mail.google.com/mail/u/0/#drafts?compose=<id>` + `Process.Start`).
4. **Revisão geral de `appConf` sendo passado inteiro em quase todo método** —
   usuário observou que isso não foi eliminado como planejado originalmente
   (muitos métodos só usam 1-2 campos de `AppConfig`, mas recebem o objeto
   inteiro). Levantamento ainda não feito nesta sessão — avaliar caso a caso
   se vale a pena reduzir para parâmetros específicos, ou se o custo de
   passar o objeto inteiro é aceitável dado o tamanho pequeno do projeto.
5. **Agendamento automático** — ainda não discutido: como/quando o `.exe` vai
   rodar periodicamente no PC da Dalgiza sem intervenção manual (Task Scheduler
   do Windows?). Não mencionado ainda nesta conversa.

## Decisões de estilo/arquitetura já fixadas (não reabrir)

- CDD (Checklist Driven Development) — comentários de plano no topo dos arquivos,
  atualizados como checkpoint.
- `static` só quando não há estado repetido entre chamadas; senão vira instância
  com campo `readonly` atribuído no construtor.
- Nunca usar `AppConfig.ApartamentosGerenciados`/similar como fonte de verdade
  quando existe dado mais concreto disponível (ex: `List<Boleto>` realmente
  baixado, ou `File.Exists` real em disco) — sempre preferir o dado
  real/empírico sobre suposição/config estática. Isso vale também para DATAS:
  nunca calcular "que dia deveria ser" — sempre extrair/comparar contra dado
  real (texto do email, `InternalDate` da mensagem, arquivo existente em disco).
- Nomes/métodos em português, seguindo convenção já estabelecida no projeto.
- Sem interfaces (YAGNI — nenhum cenário de múltipla implementação ou mock
  apareceu ainda; se aparecer, introduzir então, não antes).
- `{{chave}}` para placeholders de template.
- Requisição de rede desnecessária (barata, sem efeito colateral) é preferível a
  risco de sobrescrever/corromper dado já salvo em disco (efeito colateral sério).
