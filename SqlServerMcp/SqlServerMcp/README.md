# SqlServerMcp

Servidor MCP read-only em C#/.NET para permitir que o Cursor explore e analise um SQL Server durante o planejamento de tarefas de desenvolvimento.

## Status

| Fase | Descrição | Status |
|------|-----------|--------|
| 1 | Projeto MCP com `test_connection` | Concluída |
| 2 | Configuração do SQL Server | Concluída |
| 3 | Segurança Read-Only | Concluída |
| 4 | Primeiras ferramentas SQL | Concluída |
| 5 | Descoberta do banco | Concluída |
| 6 | Análise dos dados | Concluída |
| 7 | Query genérica | Concluída |
| 8 | Segurança adicional | Concluída |
| 9 | Configuração do Cursor | Concluída |
| 10 | Teste completo | Concluída |
| 11 | Teste de uso real | Concluída |

## Requisitos

- .NET 9 SDK
- Windows
- SQL Server acessível via rede
- Cursor IDE com suporte a MCP

## Compilar

```powershell
dotnet build -c Release
```

Executável gerado em:

```
c:\Projetos\SqlServerMcp\SqlServerMcp\bin\Release\net9.0\SqlServerMcp.exe
```

## Configurar no Cursor (Fase 9)

### 1. Criar o arquivo de configuração

Crie o arquivo na **raiz do workspace**:

```
c:\Projetos\SqlServerMcp\.cursor\mcp.json
```

Use o modelo em [`.cursor/mcp.json.example`](.cursor/mcp.json.example) e substitua `YOUR_PASSWORD` pela senha real.

Exemplo (sem credenciais reais):

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "c:\\Projetos\\SqlServerMcp\\SqlServerMcp\\bin\\Release\\net9.0\\SqlServerMcp.exe",
      "args": [],
      "env": {
        "SQLSERVER_CONNECTION_STRING": "Server=SEU_SERVIDOR;Database=SUA_DATABASE;User Id=SEU_USUARIO;Password=SUA_SENHA;TrustServerCertificate=True",
        "SQLSERVER_COMMAND_TIMEOUT_SECONDS": "30",
        "SQLSERVER_MAX_SAMPLE_ROWS": "100",
        "SQLSERVER_MAX_SELECT_ROWS": "100",
        "SQLSERVER_MAX_RESULT_SIZE_BYTES": "524288"
      }
    }
  }
}
```

**Importante:**
- Use barras duplas `\\` no caminho do Windows
- O arquivo `.cursor/mcp.json` está no `.gitignore` — **não commite credenciais**
- Alternativa: configure `SQLSERVER_CONNECTION_STRING` como variável de ambiente do Windows e omita a senha do arquivo

### 2. Habilitar no Cursor

1. Abra **Cursor Settings** (`Ctrl + Shift + J`)
2. Vá em **MCP**
3. Confirme que o servidor `sqlserver` aparece e está **conectado**
4. Se necessário, reinicie o Cursor ou clique em **Refresh**

### 3. Validar

No chat do Agent, peça:

> Chame `test_connection` e me mostre o resultado.

## Variáveis de ambiente

| Variável | Padrão | Descrição |
|----------|--------|-----------|
| `SQLSERVER_CONNECTION_STRING` | — | Connection string do SQL Server (obrigatória) |
| `SQLSERVER_COMMAND_TIMEOUT_SECONDS` | 30 | Timeout de comandos SQL |
| `SQLSERVER_MAX_SAMPLE_ROWS` | 100 | Limite de linhas em `sample_data` |
| `SQLSERVER_MAX_SELECT_ROWS` | 100 | Limite de linhas em `execute_select` |
| `SQLSERVER_MAX_RESULT_SIZE_BYTES` | 524288 | Limite de tamanho do resultado (512 KB) |

Consulte [`.env.example`](SqlServerMcp/.env.example) para um modelo sem credenciais.

## Compilar e testar

```powershell
dotnet build -c Release
dotnet test
```

Checklist completa de validação: [CHECKLIST.md](CHECKLIST.md)

## Ferramentas disponíveis

| Tool | Descrição |
|------|-----------|
| `test_connection` | Testa MCP + conexão SQL Server |
| `list_databases` | Lista databases acessíveis |
| `list_tables` | Lista tabelas/views de um database |
| `describe_table` | Descreve colunas de uma tabela |
| `find_columns` | Busca colunas por nome parcial |
| `find_relationships` | Lista FKs reais de uma tabela |
| `sample_data` | Amostra de registros com limite |
| `row_count` | Contagem de linhas |
| `get_column_stats` | Estatísticas de uma coluna |
| `execute_select` | Executa SELECT read-only validado |

### Dica: schema das tabelas

Neste projeto Kairos, as tabelas ficam no schema `userNewPoint`. Use `userNewPoint.PESSOA` em `describe_table` quando necessário.

## Segurança Read-Only

O `QueryValidator` bloqueia operações de escrita e DDL:

- Keywords bloqueadas: `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `DROP`, `ALTER`, `TRUNCATE`, `CREATE`, `EXEC`, `EXECUTE`, `GRANT`, `REVOKE`, `DENY`, `INTO`, `OPENROWSET`, `OPENDATASOURCE`, `OPENQUERY`, `BULK`, `DBCC`, `BACKUP`, `RESTORE`, `KILL`, `RECONFIGURE`, `WAITFOR`, `SHUTDOWN`, `SP_EXECUTESQL`
- Apenas `SELECT` ou `WITH ... SELECT`
- Múltiplas instruções SQL (`;`) rejeitadas
- `CROSS JOIN` bloqueado
- Extended procedures (`xp_*`) bloqueadas
- Consultas com `FROM` exigem `TOP`, `WHERE` ou agregação
- Limites de linhas e tamanho em `execute_select` e `sample_data`

## Estrutura do projeto

```
SqlServerMcp/
├── Program.cs
├── SqlServerMcp.csproj
├── README.md
├── Tools/
│   ├── ConnectionTools.cs
│   ├── DatabaseTools.cs
│   ├── SchemaTools.cs
│   ├── DataTools.cs
│   └── QueryTools.cs
├── Services/
│   ├── SqlServerService.cs
│   ├── QueryValidator.cs
│   ├── SqlIdentifierHelper.cs
│   ├── DataAnalysisHelper.cs
│   └── QueryResultFormatter.cs
└── Models/
    ├── QueryRow.cs
    └── SelectQueryResult.cs
```

## Arquitetura

```
Cursor
  |
  | MCP / STDIO
  v
SqlServerMcp
  |
  | QueryValidator (read-only)
  | Microsoft.Data.SqlClient
  v
SQL Server
```
