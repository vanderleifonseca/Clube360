# PostgreSQL no Clube360

A API pode usar o banco `postgres` do Supabase. Web e Android continuam acessando a API. O modo local em JSON é o padrão.

## Configurar no VS Code

1. Confira se a conexão salva no VS Code executa `SELECT current_database();`.
2. Pare a API com Ctrl+C. Guarde uma cópia da pasta `src/Clube360.Api/App_Data`, se ela existir.
3. Abra o terminal PowerShell na raiz do repositório. Execute o bloco abaixo. Use o servidor, usuário e senha da conexão já testada no VS Code. Digite a senha no prompt protegido.

```powershell
$clube360Conexao = [System.Data.Common.DbConnectionStringBuilder]::new()
$clube360Conexao['Host'] = Read-Host 'Servidor do Session pooler'
$clube360Conexao['Port'] = 5432
$clube360Conexao['Database'] = 'postgres'
$clube360Conexao['Username'] = Read-Host 'Usuário do Session pooler'
$clube360Conexao['SSL Mode'] = 'Require'
$clube360Senha = Read-Host 'Senha do PostgreSQL' -AsSecureString
$clube360Conexao['Password'] = [System.Net.NetworkCredential]::new('', $clube360Senha).Password

@{
    'ConnectionStrings:Clube360' = $clube360Conexao.ConnectionString
    'Dados:Provedor' = 'PostgreSQL'
} | ConvertTo-Json -Compress | dotnet user-secrets set --project src/Clube360.Api

Remove-Variable clube360Senha, clube360Conexao
```

O comando confirma que os segredos foram salvos. A configuração fica no user-secrets da API, fora do repositório. O modo `Require` exige conexão criptografada. Para validação da identidade do servidor, configure `VerifyFull` com a cadeia de confiança do servidor.

4. Inicie a API.

```powershell
dotnet run --project src/Clube360.Api --launch-profile http
```

A mensagem esperada é `Persistência do Clube360 conectada ao PostgreSQL.` Abra http://localhost:5187/api/v1/status e confira `"persistencia": "PostgreSQL"`.

## Importação e conferência

Na primeira inicialização em PostgreSQL, a API cria o esquema `clube360_privado` e grava os cadastros e as contas criadas pelo painel na mesma transação. Usa `clubes.json` e `contas.json`, ou os caminhos personalizados em `Dados:Arquivo` e `Dados:ContasArquivo`. Em uma instalação sem arquivos, inicia com os dados de demonstração e a lista de contas criadas vazia. As contas iniciais do configurador continuam em user-secrets.

No VS Code, execute a consulta abaixo para conferir a importação sem exibir dados pessoais ou hashes de senha.

```sql
SELECT chave
FROM clube360_privado.documentos
ORDER BY chave;
```

Devem aparecer duas linhas, `clubes` e `contas`. Abra o painel e entre com a conta já cadastrada para conferir os vínculos e dados. Nas inicializações seguintes, a API usa os registros do banco e não repete a importação.

Nesta versão, a estrutura atual é preservada em dois documentos JSONB no PostgreSQL. As gravações são transacionais, com bloqueio da linha alterada. O esquema privado tem acesso público revogado e segurança por linha habilitada. A aplicação usa a conexão do servidor configurada para o projeto. Mantenha esse esquema fora da lista de esquemas expostos pela API de dados do Supabase.

Os arquivos locais são preservados como cópia da situação no momento da migração. Alterações posteriores feitas no PostgreSQL não são copiadas de volta para os arquivos. Para voltar ao modo JSON durante um teste, pare a API, remova `Dados:Provedor` com `dotnet user-secrets remove 'Dados:Provedor' --project src/Clube360.Api` e inicie novamente.
