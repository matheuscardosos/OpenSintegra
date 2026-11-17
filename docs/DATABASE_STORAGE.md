# Persistencia SQLite e Controle de Armazenamento

O projeto `OpenSintegra.Data` encapsula o armazenamento local atraves da classe `FiscalDatabase` utilizando `Microsoft.Data.Sqlite`.

## Localizacao Padrao

Por padrao, o banco SQLite e criado automaticamente em:
- Windows: `%LocalAppData%\OpenSintegra\opensintegra.db`
- Linux / macOS: `~/.local/share/OpenSintegra/opensintegra.db`

Na tela de **Configuracoes** e possivel visualizar o caminho ativo e gerenciar a capacidade maxima permitida.

## Estrutura do Esquema

1. **`app_settings`**: Armazena configuracoes (`max_db_size_mb` padrao `1024` MB e `auto_prune_oldest` padrao `true`).
2. **`companies`**: Mantem o registro unico por `cnpj` com Razao Social, IE, UF, Municipio e `tax_regime` (Simples Nacional, Lucro Presumido, Lucro Real, MEI).
3. **`import_history`**: Registra todas as importacoes Sintegra (com `sha256` unico por empresa para deduplicar reaberturas do mesmo arquivo), totais de entradas/compras/saidas, quantidade de erros/avisos e estatisticas de confronto XML.
4. **`monthly_snapshots`**: Consolida valores mes a mes vinculados a cada importacao, permitindo montar a evolucao anual continua de uma empresa mesmo quando os arquivos mensais sao importados separadamente ao longo do ano.
5. **`audit_missing_docs`**: Registra XMLs criticos omissos detectados no momento da auditoria cruzada.

## Limite de Banco de Dados Configuravel

Na barra inferior de status, o programa exibe continuamente o indicador `Base fiscal: <uso> de <limite>` acompanhado de barra de progresso.

Ao salvar configuracoes com **Auto-Prune** ativo:
1. O banco verifica se a soma fisica do `.db` principal + `-wal` excede `max_db_size_mb * 1024 * 1024`.
2. Se ultrapassar o limite, as importacoes mais antigas sao excluidas em cascata (`monthly_snapshots` e `audit_missing_docs` acompanham a remocao).
3. Executa `PRAGMA wal_checkpoint(TRUNCATE)` e `VACUUM` para liberar imediatamente o espaco em disco para o sistema operacional.
