# Automação Boletos PROTEST — Laranjeiras

## O que faz

Automatiza o processo completo de recebimento e repasse de boletos de condomínio:

1. Acessa o Gmail e localiza o email do PROTEST com o boleto
2. Extrai o link do boleto e a data de vencimento
3. Abre o boleto, autentica com os 4 primeiros dígitos do CPF e baixa o PDF
4. Salva o PDF localmente com o nome `Vencimento_DD-MM-YYYY.pdf`
5. Envia para impressão
6. Prepara um rascunho de email no Gmail com o PDF anexado, usando um template pré-configurado, para a Dalgiza revisar e enviar manualmente

---

## Configurações necessárias

| Campo | Valor |
| --- | --- |
| CPF (4 primeiros dígitos) | `4776` |
| Pasta de destino do PDF | `D:\DALGIZA\Condominios\1509-A` |
| Template de email no Gmail | `Boleto PROTEST ap 1509-A 336 Laranjeiras` |
| Email do destinatário | <danieloliveiracorretor222@gmail.com> |
| Apartamentos gerenciados | 1509-A, 1508-A, 1503-A, 1515-B |

---

## Pré-requisitos

- Windows 10 com Chrome instalado
- Gmail com a funcionalidade de **Templates (Modelos)** ativada
- Chrome fechado antes de rodar o programa

---

## Uso no dia a dia

1. Recebeu o email do PROTEST → abrir o programa
2. O Chrome executa tudo automaticamente
3. Ao final, revisar o rascunho no Gmail e clicar **Enviar**

---

## Problemas comuns

| Sintoma | Solução |
| --- | --- |
| CPF não reconhecido | Confirmar se são os 4 primeiros dígitos corretos |
| Template não encontrado | O nome no sistema deve ser idêntico ao salvo no Gmail |
| PDF não baixa | O programa permite salvar manualmente e continua |
| Chrome não abre corretamente | Verificar o caminho do perfil do Chrome nas configurações |
| Timeout ao buscar email | Abrir o Chrome manualmente para verificar o site |

---

## Possíveis melhorias futuras

- Criação de GUI para permitir seleção de opções para o usuário
- Fazer com que TODOS os dados sejam opções definíveis pelo usuário (tanto de modo "permanente" - salvo em configs/modelos individuais - quanto imediata, na própria GUI)
- Em caso de falha devido a fatores externos (por exemplo, PROTEST mudou o formato que apresenta datas de 'dd/MM/yyyy' para 'yyyy-MM-dd'), exibir alerta para o usuário informando sobre o problema específico, e pedindo para entrar em contato com o desenvolvedor (<r.borjovsky@gmail.com>)
- Melhorias e otimizações de código e performance
- Ampliar o programa, criando mais opções e comodidades para o usuário (pensar em como poderia ser um automatizador desse tipo, mas não especificamente para Dalgiza, e sim para o público/empresas em geral)
