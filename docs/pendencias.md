# Pendências e Revisões Futuras

## Error Handling — Revisão geral de `throw`

Revisar **todos** os métodos do projeto para garantir que nenhum `catch` engole erros silenciosamente quando a operação é obrigatória.

**Regra:** só engolir exceção (sem `throw`) se o programa puder continuar corretamente sem o resultado daquela operação.

**Arquivos já identificados com problema:**
- `FileService.cs` — `SalvaArquivoPDF`: `catch` loga mas não relança
- Outros: verificar todos os demais serviços
