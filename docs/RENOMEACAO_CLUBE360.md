# Mudança de nome para Clube360 — 0.3.1

Nome comercial e técnico atual: **Clube360**. A alteração abrange telas web, título do navegador, API/status, MAUI, ícone e splash com C360 em contornos vetoriais, namespaces, nomes de projetos e pastas, solução, referências, tarefas do VS Code, testes e documentação.

## Atualizar sua instalação

Pare os dois terminais antigos com Ctrl+C. Faça backup da pasta App_Data da antiga API com os processos parados. Atualize mantendo sua pasta local atual:

```powershell
cd C:\maui\FisgadaPro
git pull --ff-only
dotnet build Clube360.slnx
```

Em terminais separados:

```powershell
dotnet run --project src/Clube360.Api --launch-profile http
```

```powershell
dotnet run --project src/Clube360.Web --launch-profile http
```

Abra http://localhost:5216/entrar. Os endereços e portas continuam iguais. Saia e entre novamente com o mesmo e-mail e senha, pois a sessão web anterior não é reaproveitada.

## Preservação de dados

Os arquivos padrão da antiga API ficam em `src/FisgadaPro.Api/App_Data`. Ao iniciar a nova API, se a nova pasta `src/Clube360.Api/App_Data` ainda não existir, os arquivos `clubes.json` e `contas.json` são copiados juntos. A origem permanece como backup. Se já houver pasta nova, ela é respeitada e não é sobrescrita por dados antigos.

Depois da migração use somente a API Clube360; não inicie a antiga API nem altere os arquivos do backup. Caminhos personalizados via configuração Dados continuam sendo respeitados. Backups futuros devem incluir os dois arquivos da pasta nova com a API parada. Não publique esses arquivos no Git.

O identificador de user-secrets da API permanece `FisgadaPro.Api.Local` para que as senhas locais anteriores sejam encontradas. Não execute novamente o configurador apenas para atualizar o nome. Novas instalações recebem sugestões @clube360.test, mas as contas já cadastradas conservam seus e-mails, inclusive @fisgada.test.

## Android

```powershell
dotnet build src/Clube360.App/Clube360.App.csproj -t:Run -p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"
```

O nome exibido no celular é Clube360; o ícone/splash usa C360. O identificador interno `br.com.fisgadapro.app` permanece estável, para permitir atualização do app instalado e preservação de preferências. O código de versão Android aumentou para 2. A instalação deve usar a mesma assinatura anterior; não desinstale o aplicativo como primeiro passo. Esta atualização Android ainda precisa de compilação e teste no seu computador.

## Endereço do projeto

O repositório foi renomeado para **Clube360** e está em https://github.com/vanderleifonseca/Clube360. O histórico de commits foi preservado.

Na cópia já existente no computador, ajuste o endereço remoto:

```powershell
cd C:\maui\FisgadaPro
git remote set-url origin https://github.com/vanderleifonseca/Clube360.git
git remote -v
git pull --ff-only
```

As linhas fetch e push devem mostrar o novo endereço. A pasta local `C:\maui\FisgadaPro` pode continuar com esse nome. A alteração do endereço remoto não muda cadastros, contas ou arquivos locais. Os identificadores de credenciais e pacote Android continuam preservados por compatibilidade.
