# Checklist de validação — SqlServerMcp

Validação executada em ambiente real com MCP conectado no Cursor.

## Pré-requisitos

- [x] .NET 9 SDK instalado
- [x] Projeto compilado: `dotnet build -c Release`
- [x] Executável existe: `SqlServerMcp\bin\Release\net9.0\SqlServerMcp.exe`
- [x] Variável `SQLSERVER_CONNECTION_STRING` configurada no `mcp.json`
- [x] MCP habilitado em **Cursor Settings → MCP**

## Funcionalidade (Fase 10)

| # | Teste | Resultado | OK |
|---|-------|-----------|-----|
| 1 | Cursor inicia o MCP | Servidor `sqlserver` conectado | x |
| 2 | `test_connection` | Conexão OK com SQL Server | x |
| 3 | `list_databases` | 15 databases listados | x |
| 4 | `list_tables` | 399 tabelas/views listadas | x |
| 5 | `describe_table` | 96 colunas de `userNewPoint.PESSOA` | x |
| 6 | `find_columns` | 3 colunas com `RAIO` encontradas | x |
| 7 | `find_relationships` | 51 FKs em `PESSOA` | x |
| 8 | `sample_data` | 3 registros retornados | x |
| 9 | `row_count` | 5180 linhas em `PESSOA` | x |
| 10 | `get_column_stats` | Stats de `RAIO_MARCACAO` OK | x |
| 11 | `execute_select` | SELECT TOP 3 com WHERE OK | x |

## Segurança read-only

| # | Teste | Resultado | OK |
|---|-------|-----------|-----|
| 12 | `INSERT` bloqueado | 71 testes unitários QueryValidator | x |
| 13 | `UPDATE` bloqueado | Testes unitários | x |
| 14 | `DELETE` bloqueado | Testes unitários | x |
| 15 | `DROP` bloqueado | Testes unitários | x |
| 16 | `ALTER` bloqueado | Testes unitários | x |
| 17 | `EXEC` bloqueado | Testes unitários | x |
| 18 | Consultas grandes limitadas | TOP/WHERE obrigatório + truncamento | x |
| 19 | Timeout configurável | `SQLSERVER_COMMAND_TIMEOUT_SECONDS=30` | x |
| 20 | Credenciais no Git | `.cursor/mcp.json` no `.gitignore` | x |

## Testes automatizados

```powershell
dotnet test
```

Resultado: **71 testes aprovados**.
