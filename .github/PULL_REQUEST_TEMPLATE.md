## Resumo da Alteracao

Descreva de forma objetiva o que este Pull Request altera e qual problema resolve.

## Checklist Tecnico

- [ ] Codigo segue os limites entre camadas definidos em `AGENTS.md` e `ARCHITECTURE.md`
- [ ] Novos fluxos cobertos por testes automatizados xUnit com dados sinteticos (sem dados reais de contribuintes)
- [ ] `dotnet build OpenSintegra.sln -c Release` concluido sem avisos ou erros
- [ ] `dotnet test OpenSintegra.sln -c Release` 100% aprovado
- [ ] Documentacao em `docs/` e `CHANGELOG.md` atualizada quando aplicavel
