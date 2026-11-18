# Política de Segurança - OpenSintegra

## Versões Suportadas

| Versão | Suportada |
| ------ | --------- |
| 1.0.x  | Sim       |

## Privacidade e Processamento Offline

O OpenSintegra Contábil foi projetado para operar **100% offline**:

- Nenhum arquivo Sintegra, XML, SPED ou dado cadastral de empresa é transmitido pela internet.
- O armazenamento histórico ocorre exclusivamente em banco SQLite local na máquina do usuário.
- O parsing de documentos XML utiliza `System.Xml.Linq.XDocument` padrão sem habilitação de resolução de DTD externa, prevenindo vulnerabilidades XXE (XML External Entity).

## Reportando Vulnerabilidades

Caso encontre qualquer vulnerabilidade de segurança (como XXE, Traversal em arquivos `.zip` ou injeção SQL na camada `OpenSintegra.Data`):

1. **Não** abra uma issue pública detalhando a falha explorável.
2. Envie o relato de forma privada utilizando **GitHub Security Advisories** ("Report a vulnerability" na aba *Security* do repositório) ou entre em contato direto com os mantenedores.
3. Inclua passos reproduzíveis com dados sintéticos ou arquivo de exemplo anonimizado.

Responderemos com avaliação inicial em até 5 dias úteis e publicaremos correção com crédito ao pesquisador, caso desejado.
