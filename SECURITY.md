# Politica de Seguranca - OpenSintegra

## Versoes Suportadas

| Versao | Suportada |
| ------ | --------- |
| 1.0.x  | Sim       |

## Privacidade e Processamento Offline

O OpenSintegra Contabil foi projetado para operar **100% offline**:

- Nenhum arquivo Sintegra, XML, SPED ou dado cadastral de empresa e transmitido pela internet.
- O armazenamento historico ocorre exclusivamente em banco SQLite local na maquina do usuario.
- O parsing de documentos XML utiliza `System.Xml.Linq.XDocument` padrao sem habilitacao de resolucao de DTD externa, prevenindo vulnerabilidades XXE (XML External Entity).

## Reportando Vulnerabilidades

Caso encontre qualquer vulnerabilidade de seguranca (como XXE, Traversal em arquivos `.zip` ou injecao SQL na camada `OpenSintegra.Data`):

1. **Nao** abra uma issue publica detalhando a falha exploravel.
2. Envie o relato de forma privada utilizando **GitHub Security Advisories** ("Report a vulnerability" na aba *Security* do repositorio) ou entre em contato direto com os mantenedores.
3. Inclua passos reproduziveis com dados sinteticos ou arquivo de exemplo anonimizado.

Responderemos com avaliacao inicial em ate 5 dias uteis e publicaremos correcao com credito ao pesquisador, caso desejado.
