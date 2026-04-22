# Automação Boletos PROTEST — Laranjeiras

## Dalgiza Borges | Ap 1509-A | Parque Residencial Laranjeiras, 336

---

## O que o programa faz

1. Abre o Gmail (Chrome já logado, sem pedir senha)
2. Busca o email do PROTEST com assunto "Aviso De Boleto Gerado"
3. Extrai o link do boleto e a data de vencimento do email
4. Abre o link → digita os 4 primeiros dígitos do CPF (4776) → PDF aparece
5. Salva o PDF em `D:\DALGIZA\Condominios\1509-A\Vencimento_DD-MM-YYYY.pdf`
6. Envia para impressão
7. Volta ao Gmail, usa o template "Boleto PROTEST ap 1509-A 336 Laranjeiras", anexa o PDF e deixa o **rascunho aberto** para a Dalgiza revisar e clicar Enviar

---

## Arquivos do projeto

``` bash
boleto_protest/
  boleto_automation.py   ← código principal
  config.json            ← configurações (checar antes de usar)
  requirements.txt       ← dependências Python
  setup_e_build.bat      ← instala tudo e gera o .exe (rodar no Windows)
  README.md              ← este arquivo
```

---

## Configurações a verificar no config.json

| Campo | Valor atual | O que é |
| --- | --- | --- |
| `cpf_4_digitos` | `4776` | Primeiros 4 dígitos do CPF da Dalgiza |
| `pasta_download` | `D:\DALGIZA\Condominios\1509-A` | Onde salvar o boleto |
| `gmail_template` | `Boleto PROTEST ap 1509-A 336 Laranjeiras` | Nome **exato** do template no Gmail |
| `gmail_destinatario` | *(preencher com email do Daniel)* | Email da imobiliária Henrique |
| `caminho_perfil_chrome` | `C:\Users\DALGIZA\AppData\...` | Verificar se o nome do usuário Windows bate |

### Perfis por sistema operacional (Windows/Fedora)

Agora o `config.json` suporta múltiplos perfis em `perfis`:

- `dalgiza_windows`: perfil real para uso no Windows 10 dela
- `meu_fedora`: perfil de testes no Fedora

Seleção de perfil:

1. `perfil_ativo: "auto"` usa `perfil_por_plataforma` (`win32`/`linux`)
2. `BOLETO_PERFIL` força um perfil específico

Exemplos:

```bash
# Fedora: força perfil de testes
BOLETO_PERFIL=meu_fedora python boleto_automation.py
```

```bat
:: Windows: força perfil real
set BOLETO_PERFIL=dalgiza_windows && python boleto_automation.py
```

No log de execução, o programa mostra qual perfil foi ativado.

### Campo novo no navegador: `canal`

- `"canal": "chrome"` -> usa Chrome instalado no sistema (ideal no Windows dela)
- `"canal": null` -> usa Chromium do Playwright (útil para testes no Fedora)

### Formato recomendado para `apartamentos` (menos repetição)

Use um objeto com `identificadores` e placeholders `{identificador}`:

```json
"apartamentos": {
   "identificadores": ["1509-A", "1508-A", "1503-A", "1515-B"],
   "pasta_download": "D:\\DALGIZA\\Condominios\\{identificador}",
   "nome_arquivo": "Vencimento_{data_vencimento}.pdf",
   "gmail_template": "Boleto PROTEST ap {identificador} 336 Laranjeiras",
   "gmail_destinatario": "danieloliveiracorretor222@gmail.com",
   "gmail_cc": ""
}
```

O programa substitui `{identificador}` automaticamente para cada apartamento.
Ele também continua aceitando o formato antigo (lista de objetos), para compatibilidade.

**Para descobrir o nome certo do usuário Windows:**
Abre o Explorador de Arquivos → barra de endereço → digita `%LOCALAPPDATA%\Google\Chrome\User Data` → se abrir, o caminho está certo.

---

## Instalação — fazer só uma vez no computador dela

### Pré-requisitos

- Windows 10
- Chrome instalado
- Conexão com internet

### Passos

1. Baixe e instale o Python: <https://www.python.org/downloads/>
   - ⚠️ **Marque "Add Python to PATH"** durante a instalação — obrigatório

2. Copie a pasta `boleto_protest` para o computador (ex: `C:\boleto_protest\`)

3. Clique com botão direito em `setup_e_build.bat` → **Executar como administrador**

4. Aguarda — instala Python, Playwright, Chrome headless e gera o `.exe`

5. O executável final fica em `dist\Boletos Laranjeiras.exe`
   - Copie para a Área de Trabalho para facilitar o acesso

---

## Ativar Templates no Gmail (fazer só uma vez)

1. Abre o Gmail → ⚙️ → **Ver todas as configurações**
2. Aba **Avançado**
3. Em **Modelos** → **Ativar**
4. Salva as alterações

---

## Uso no dia a dia

1. Recebeu email do PROTEST? Clica duas vezes em **"Boletos Laranjeiras.exe"**
2. O Chrome abre automaticamente e faz tudo
3. No final, aparece o rascunho do email no Gmail
4. Revisa e clica **Enviar**

> ⚠️ **Feche o Chrome antes de rodar o programa**
> O Playwright precisa abrir o Chrome com o perfil dela — se o Chrome já estiver aberto, pode dar conflito.

---

## Se algo der errado

| Problema | O que fazer |
| --- | --- |
| "cpf_4_digitos" não funciona | Verifica se são realmente os 4 primeiros dígitos do CPF da Dalgiza |
| Template não encontrado | Nome no config.json deve ser **idêntico** ao nome salvo no Gmail (maiúsculas, espaços, tudo) |
| PDF não baixa automaticamente | O programa pede para salvar manualmente e continua |
| Chrome não abre com perfil certo | Verificar o caminho em `caminho_perfil_chrome` no config.json |
| Timeout na busca do email | O site pode estar lento — abra o Chrome manualmente e veja o que aparece |

---

## Desenvolvimento (no Fedora)

```bash
pip install -r requirements.txt
python -m playwright install chromium
python boleto_automation.py
```

> O PyInstaller precisa rodar **no Windows** para gerar o .exe.
> Desenvolva/teste no Fedora, gere o executável final no Windows dela.
