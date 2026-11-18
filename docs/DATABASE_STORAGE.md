# Persistência SQLite e Controle de Armazenamento

O projeto `OpenSintegra.Data` encapsula o armazenamento local através da classe `FiscalDatabase` utilizando `Microsoft.Data.Sqlite`.

## Localização Padrão

Por padrão, o banco SQLite é criado automaticamente em:
- Windows: `%LocalAppData%\OpenSintegra\opensintegra.db`
- Linux / macOS: `~/.local/share/OpenSintegra/opensintegra.db`

Na tela de **Empresas e Banco** é possível visualizar o caminho ativo, alterar o regime tributário da empresa e gerenciar a capacidade máxima permitida.

## Estrutura do Esquema

1. **`app_settings`**: Armazena configurações globais (`max_db_size_mb` padrão `1024` MB, `auto_prune_oldest` padrão `true`, `last_sintegra_path` e `last_xml_path`).
2. **`companies`**: Mantém o registro único por `cnpj` com Razão Social, IE, UF, Município e `tax_regime` (Simples Nacional, Lucro Presumido, Lucro Real, MEI / SIMEI).
3. **`import_history`**: Registra todas as importações Sintegra (com `sha256` único por empresa para deduplicar reaberturas do mesmo arquivo), totais de entradas/compras/saídas, quantidade de erros/avisos e estatísticas de confronto XML.
4. **`monthly_snapshots`**: Consolida valores mês a mês vinculados a cada importação, permitindo montar a evolução anual contínua de uma empresa mesmo quando os arquivos mensais são importados separadamente ao longo do ano.
5. **`audit_missing_docs`**: Registra XMLs críticos omissos detectados no momento da auditoria cruzada.

## Limite de Banco de Dados Configurável

Na barra inferior de status, o programa exibe continuamente o indicador `Base fiscal: <uso> de <limite>` acompanhado de barra de progresso.

Ao salvar configurações com **Auto-Prune** ativo:
1. O banco verifica se a soma física do `.db` principal + `-wal` excede `max_db_size_mb * 1024 * 1024`.
2. Se ultrapassar o limite, as importações mais antigas são excluídas em cascata (`monthly_snapshots` e `audit_missing_docs` acompanham a remoção).
3. Executa `PRAGMA wal_checkpoint(TRUNCATE)` e `VACUUM` para liberar imediatamente o espaço em disco para o sistema operacional.
