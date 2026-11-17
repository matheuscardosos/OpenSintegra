# Contribuindo com o OpenSintegra Contabil

Agradecemos seu interesse em contribuir para tornar a auditoria e validacao fiscal mais transparente e acessivel sob codigo aberto MIT!

## Como Contribuir

1. Leia antes o guia de arquitetura em [ARCHITECTURE.md](ARCHITECTURE.md) e as regras tecnicas em [AGENTS.md](AGENTS.md).
2. Verifique as issues existentes para evitar trabalho duplicado.
3. Faca um fork do repositorio e crie uma branch a partir da `main`:
   - `feat/nome-da-funcionalidade` para novos recursos
   - `fix/descricao-do-ajuste` para correcoes
   - `docs/assunto` para melhorias de documentacao
4. Escreva ou atualize os testes unitarios em `tests/OpenSintegra.Tests/`.
5. Valide localmente antes de abrir o Pull Request:
   ```bash
   dotnet build OpenSintegra.sln -c Release
   dotnet test OpenSintegra.sln -c Release
   ```

## Convencoes de Codigo e Commits

- Utilize [Conventional Commits](https://www.conventionalcommits.org/pt-br/): `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`.
- Nao inclua arquivos reais de contribuintes (CNPJs privados, Sintegra real de clientes ou XMLs fiscais reais) nos commits ou fixtures de teste. Sempre utilize dados sinteticos com CNPJs de teste validos.
- Mantenha zero dependencias proprietarias ou com licencas incompativeis com a [Licenca MIT](LICENSE).

## Licenca das Contribuicoes

Ao enviar um Pull Request, voce concorda que suas contribuicoes serao licenciadas sob os termos da **Licenca MIT** do projeto.
