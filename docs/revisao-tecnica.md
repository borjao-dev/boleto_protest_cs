# Revisão Técnica — Itens para Pós-Conclusão

Problemas conhecidos e sugestões de melhoria identificados durante o desenvolvimento.
Não bloqueia o funcionamento — refatorar depois que tudo estiver funcionando.

---

## 🔴 Crítico — Pode causar falha em produção

### 1. Emails HTML têm estrutura multipart (GmailMessages.cs + GmailMessageService.cs)

**Problema:** A API do Gmail retorna emails simples com `payload.body.data`, mas emails HTML
(que é o caso da PROTEST) usam estrutura **multipart**:

```csharp
payload.body.data = ""           ← VAZIO em emails multipart
payload.parts[0].mimeType = "text/plain"
payload.parts[0].body.data = "..."
payload.parts[1].mimeType = "text/html"
payload.parts[1].body.data = "..."  ← O HTML está aqui
```

Partes podem ser recursivas (`parts[i].parts[j]...`) quando há anexos ou nested multipart.

**Impacto:** `mensagem.Payload.Body.Data` retorna string vazia. O Regex nunca encontra a URL.
Nenhum boleto é baixado, sem mensagem de erro.

**O que mudar:**

1. **`GmailMessages.cs`** — adicionar `Parts` ao `GmailMessagesPayload` e criar `GmailMessagesPart`:

```csharp
public class GmailMessagesPayload {
    [JsonPropertyName("headers")] public List<GmailMessagesHeaders> Headers { get; set; } = [];
    [JsonPropertyName("body")] public GmailMessagesBody Body { get; set; } = new();
    [JsonPropertyName("parts")] public List<GmailMessagesPart> Parts { get; set; } = []; // NOVO
}

public class GmailMessagesPart {
    [JsonPropertyName("mimeType")] public string MimeType { get; set; } = "";
    [JsonPropertyName("body")] public GmailMessagesBody Body { get; set; } = new();
    [JsonPropertyName("parts")] public List<GmailMessagesPart> Parts { get; set; } = []; // recursivo
}
```

1. **`GmailMessageService.cs`** — em `FiltraEmailsPorData`, substituir a leitura direta de
`mensagem.Payload.Body.Data` por uma função auxiliar que procura recursivamente pelo HTML:

```csharp
// Busca o corpo HTML em emails simples ou multipart (recursivo)
private static string ExtraiCorpoHtml(GmailMessagesPayload payload)
{
    // Email simples: corpo direto
    if (payload.Body.Data != "") return payload.Body.Data;
    
    // Email multipart: buscar na lista de partes
    foreach (GmailMessagesPart parte in payload.Parts)
    {
        if (parte.MimeType == "text/html" && parte.Body.Data != "")
            return parte.Body.Data;
        
        // Recursivo: partes podem ter sub-partes
        foreach (GmailMessagesPart subParte in parte.Parts)
        {
            if (subParte.MimeType == "text/html" && subParte.Body.Data != "")
                return subParte.Body.Data;
        }
    }
    
    return ""; // não encontrou HTML
}
```

---

### 2. Round-trip desnecessário DateTime → string → DateTime (GmailMessageService.cs)

**Problema:** Em `BaixaPDF`:

```csharp
DateTime mesQueVem = DateTime.Now.AddMonths(1);
string vencimento = mesQueVem.ToString("dd/MM/yyyy");  // vira string
...
Boleto boleto = new(url, "apartamento_aqui", DateTime.Parse(vencimento));  // volta a DateTime
```

`DateTime.Parse` é sensível ao locale do sistema — pode falhar em ambientes com locale en-US.

**Correção simples:** usar `mesQueVem` diretamente, sem round-trip:

```csharp
Boleto boleto = new(url, "apartamento_aqui", mesQueVem);
```

---

## 🟡 Importante — Qualidade e manutenibilidade

### 3. Apartamento hardcoded "apartamento_aqui" (GmailMessageService.cs)

**Problema:** O campo `Apartamento` do record `Boleto` é sempre `"apartamento_aqui"`.

**O que investigar:** O email da PROTEST provavelmente contém o número do apartamento no corpo
ou no assunto. Depois de confirmar o formato real do email, extrair via Regex junto com a URL.

Alternativa: extrair da URL do boleto (se ela contiver o identificador do apartamento).

---

### 4. Caminho do token calculado em dois lugares (GmailAuthService.cs)

**Problema:** O caminho `~/.config/boletoprotest/token.json` é construído tanto em
`BuscaTokenDeAcessoAsync` quanto em `CarregarTokenDoDisco` — duplicação que pode criar
inconsistências se um dos dois for alterado.

**Correção:** extrair para uma constante ou propriedade privada:

```csharp
private static readonly string CaminhoToken = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".config", "boletoprotest", "token.json"
);
```

---

### 5. `ConfigurationService.AppConfiguration()` deveria ser `static`

**Problema:** O método não usa nenhum estado de instância (`this`). Criar uma instância de
`ConfigurationService` só para chamar esse método é desnecessário.

**Correção:** `public static AppConfig AppConfiguration() { ... }`

**Impacto em `Program.cs`:**

```csharp
// Antes:
ConfigurationService confServ = new();
AppConfig appConf = confServ.AppConfiguration();

// Depois:
AppConfig appConf = ConfigurationService.AppConfiguration();
```

---

### 6. `HttpClient` local em `BaixaPDF` não é descartado (GmailMessageService.cs)

**Problema:** `HttpClient clienteGet = new()` é criado dentro do método mas nunca descartado.
Para um console app de curta duração não é crítico, mas é uma prática ruim.

**Opções:**

- Adicionar `using HttpClient clienteGet = new();` (descarta automaticamente ao sair do escopo)
- Ou extrair para `static readonly` como os outros, com nome diferente (`_clienteGetBoleto`)

---

### 7. `[JsonPropertyName("is_expired")]` em propriedade computada (GmailOAuthToken.cs)

**Problema:** `IsExpired` é uma propriedade calculada (sem setter). O atributo
`[JsonPropertyName]` fará com que ela seja **serializada** para o arquivo `token.json`, mas
**nunca desserializada** (não há setter). Isso polui o JSON salvo em disco sem utilidade.

**Correção:** remover o `[JsonPropertyName("is_expired")]` de `IsExpired`.

---

## 🔵 Arquitetura — Para refatoração futura

### 8. Todos os serviços são `static` — impossibilita testes unitários adequados

**Problema:** Métodos `static` não implementam interfaces e não podem ser substituídos por mocks.
Isso torna os testes unitários muito difíceis (não dá para testar `GmailMessageService` sem
realmente chamar a API do Gmail).

**Solução:** Converter para classes com interfaces injetáveis:

```csharp
public interface IGmailAuthService { Task<string> BuscaTokenDeAcessoAsync(); }
public class GmailAuthService : IGmailAuthService { ... }
```

Depois injetar via construtor (Dependency Injection). Isso permite criar mocks nos testes:

```csharp
// Teste sem chamar API real:
var mockAuth = new Mock<IGmailAuthService>();
mockAuth.Setup(s => s.BuscaTokenDeAcessoAsync()).ReturnsAsync("fake-token");
```

Esta é a maior refatoração, mas a mais importante para testabilidade.

---

### 9. `GmailMessages.cs` — múltiplas classes num só arquivo

Convenção C#: uma classe pública por arquivo. `GmailMessages`, `GmailMessagesPayload`,
`GmailMessagesHeaders`, `GmailMessagesBody` estão todas em `GmailMessages.cs`.

Isso é aceitável para classes fortemente relacionadas (como as partes de um modelo), mas
vale avaliar separar em uma pasta `Models/Gmail/` com arquivos individuais.

---

## ✅ Já resolvido durante o desenvolvimento

- `code is null` em `GmailAuthService.AutorizaAsync` → `if (code is null) throw` adicionado
- Regex com URL hardcoded → substituído por `Regex.Escape(appConf.UrlBoleto)`
- `static readonly` para `MesQueVem`/`Vencimento` → movidos para dentro dos métodos
- `_clienteGet` compartilhado com autenticação → separado em variável local `clienteGet` em `BaixaPDF`
- `DateTime.Parse` em formato incompleto `"MM/yyyy"` → corrigido para usar `DateTime` diretamente
