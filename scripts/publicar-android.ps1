<#
.SYNOPSIS
  Gera o .aab assinado do Clube da Onça para enviar ao Google Play Console.

.EXAMPLE
  # Teste interno apontando para um servidor provisório (HTTPS obrigatório no Release)
  .\scripts\publicar-android.ps1 -Versao 1 -ApiUrl https://meu-tunel.exemplo.com

.EXAMPLE
  # Produção (usa o padrão https://api.clubedaonca.com.br)
  .\scripts\publicar-android.ps1 -Versao 5 -VersaoExibida 1.1

.NOTES
  -Versao é o versionCode: precisa ser MAIOR que o do último .aab enviado ao Play Console.
  A chave de upload e a senha ficam fora do repositório, em %USERPROFILE%\.chaves\clubedaonca\senha.txt
  (ou informe -ArquivoSenha). Faça backup desse arquivo e do .keystore.
#>
param(
    [Parameter(Mandatory)] [int] $Versao,
    [string] $VersaoExibida = "1.0",
    [string] $ApiUrl = "",
    [string] $ArquivoSenha = "$env:USERPROFILE\.chaves\clubedaonca\senha.txt"
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path $PSScriptRoot -Parent
$projeto = Join-Path $raiz "src\ClubeDaOnca.App\ClubeDaOnca.App.csproj"

if (-not (Test-Path $ArquivoSenha)) { throw "Arquivo da chave não encontrado: $ArquivoSenha" }
foreach ($linha in Get-Content $ArquivoSenha) {
    if ($linha -match '^\s*(CLUBE_KEYSTORE[A-Z_]*)\s*=\s*(.+?)\s*$') {
        Set-Item -Path "env:$($Matches[1])" -Value $Matches[2]
    }
}
if (-not (Test-Path $env:CLUBE_KEYSTORE)) { throw "Keystore não encontrado: $env:CLUBE_KEYSTORE" }

if ($ApiUrl -and -not $ApiUrl.StartsWith("https://")) {
    throw "No build Release o Android só aceita HTTPS. Use uma URL https:// (ex.: túnel do Cloudflare ou ngrok)."
}

$dotnetArgs = @(
    "publish", $projeto,
    "-f", "net10.0-android",
    "-c", "Release",
    "-p:ApplicationVersion=$Versao",
    "-p:ApplicationDisplayVersion=$VersaoExibida"
)
if ($ApiUrl) { $dotnetArgs += "-p:ApiUrl=$ApiUrl" }

Write-Host "Gerando versão $VersaoExibida ($Versao) — API: $(if ($ApiUrl) { $ApiUrl } else { 'https://api.clubedaonca.com.br (padrão)' })"
& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) { throw "Falha no build (código $LASTEXITCODE)." }

$aab = Get-ChildItem (Join-Path $raiz "src\ClubeDaOnca.App\bin\Release\net10.0-android\publish") -Filter "*-Signed.aab" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ""
Write-Host "Pronto: $($aab.FullName)" -ForegroundColor Green
Write-Host "Envie este arquivo em Play Console > Testar e lançar > Teste interno > Criar nova versão."
