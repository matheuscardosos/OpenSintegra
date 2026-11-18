# Contribuindo com o OpenSintegra Contábil

Agradecemos seu interesse em contribuir para tornar a auditoria e validação fiscal mais transparente e acessível sob código aberto MIT!

## Como Contribuir

1. Leia antes o guia de arquitetura em [ARCHITECTURE.md](ARCHITECTURE.md) e as regras técnicas em [AGENTS.md](AGENTS.md).
2. Verifique as issues existentes para evitar trabalho duplicado.
3. Faça um fork do repositório e crie uma branch a partir da `main`:
   - `feat/nome-da-funcionalidade` para novos recursos
   - `fix/descricao-do-ajuste` para correções
   - `docs/assunto` para melhorias de documentação
4. Escreva ou atualize os testes unitários em `tests/OpenSintegra.Tests/`.
5. Valide localmente antes de abrir o Pull Request:
   ```bash
   dotnet build OpenSintegra.sln -c Release
   dotnet test OpenSintegra.sln -c Release
   ```

## Convenções de Código e Commits

- Utilize [Conventional Commits](https://www.conventionalcommits.org/pt-br/): `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`.
- Não inclua arquivos reais de contribuintes (CNPJs privados, Sintegra real de clientes ou XMLs fiscais reais) nos commits ou fixtures de teste. Sempre utilize dados sintéticos com CNPJs de teste válidos.
- Mantenha zero dependências proprietárias ou com licenças incompatíveis com a [Licença MIT](LICENSE).

## Licença das Contribuições

Ao enviar um Pull Request, você concorda que suas contribuições serão licenciadas sob os termos da **Licença MIT** do projeto.
