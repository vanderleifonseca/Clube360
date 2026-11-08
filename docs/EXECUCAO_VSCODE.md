# Execução no Visual Studio Code (azul)

Requer SDK .NET 10; global.json aceita bandas posteriores, inclusive 10.0.401. A solução principal inclui servidor, web e testes. MAUI compila separadamente.

A pasta local já clonada pode continuar chamada `FisgadaPro`. Os projetos e a solução agora usam Clube360. Veja [mudança de nome](RENOMEACAO_CLUBE360.md).

## Atualizar

Pare API e painel com Ctrl+C antes de atualizar, preservando mudanças locais e arquivos de dados.

```powershell
cd C:\maui\FisgadaPro
git status
git pull --ff-only
dotnet build Clube360.slnx
```

Se Git informar alterações locais conflitantes, não use reset para descartá-las; resolva a atualização preservando seu trabalho.

## API e painel, em terminais separados

```powershell
dotnet run --project src/Clube360.Api --launch-profile http
```

```powershell
dotnet run --project src/Clube360.Web --launch-profile http
```

Painel: http://localhost:5216/entrar. Status: http://localhost:5187/api/v1/status. Contas: [Login](LOGIN_E_ACESSOS.md).

## Verificações

```powershell
dotnet run --project tests/Clube360.Domain.Checks
dotnet run --project tests/Clube360.Access.Checks
```

Esperados: 68 e 67 verificações, respectivamente. Execute na raiz do repositório.

## Android

SDK já localizado no seu PC:

```powershell
& "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" devices
dotnet build src/Clube360.App/Clube360.App.csproj -t:Run -p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"
```

O celular deve aparecer como device. ADB funciona por cabo ou Wi-Fi; porta e IP de depuração podem mudar. Para acesso à API a partir do app, veja [conexão do celular](LOGIN_E_ACESSOS.md#conectar-o-celular).

A correção anterior dos SVGs foi preservada. A versão Android 0.3 ainda precisa ser compilada e testada no seu PC. Extensões: C#, C# Dev Kit e .NET MAUI.

## Persistência

A API cria `src/Clube360.Api/App_Data/clubes.json` e `contas.json`. Estão ignorados pelo Git. Faça backup de ambos com a API parada; não apague a pasta para atualizar. User-secrets continuam separados desses arquivos. Não use dados pessoais reais antes de preparar a instalação de produção.
