[CmdletBinding()]
param(
    [ValidateSet('Churrascaria', 'Pizzaria', 'Hamburgueria', 'Bar', 'RestaurantePorQuilo', 'Restaurante', 'Outro')]
    [string]$Tipo,
    [string]$Nome,
    [string]$Destino,
    [ValidateRange(1024, 65535)]
    [int]$Porta = 5000,
    [string]$NomeGerente,
    [Security.SecureString]$PinGerente
)

$ErrorActionPreference = 'Stop'
if (-not $Tipo) {
    $tipos = @('Churrascaria', 'Pizzaria', 'Hamburgueria', 'Bar', 'RestaurantePorQuilo')
    for ($i = 0; $i -lt $tipos.Count; $i++) { Write-Host "$($i + 1) - $($tipos[$i])" }
    $escolha = Read-Host 'Tipo do estabelecimento (1 a 5)'
    if ($escolha -notmatch '^[1-5]$') { throw 'Tipo invalido. Execute novamente e escolha de 1 a 5.' }
    $Tipo = $tipos[[int]$escolha - 1]
}
if (-not $Nome) { $Nome = Read-Host 'Nome do estabelecimento' }
if ([string]::IsNullOrWhiteSpace($Nome) -or $Nome.Trim().Length -gt 120) { throw 'Informe um nome de ate 120 caracteres.' }
if (-not $NomeGerente) { $NomeGerente = Read-Host 'Nome do dono/gerente' }
if ([string]::IsNullOrWhiteSpace($NomeGerente) -or $NomeGerente.Trim().Length -gt 100) { throw 'Informe um nome de gerente de ate 100 caracteres.' }
if (-not $Destino) { $Destino = Read-Host 'Pasta NOVA para instalar (ex.: C:\PDV\MinhaLoja)' }
if ([string]::IsNullOrWhiteSpace($Destino)) { throw 'Informe uma pasta nova para a instalacao.' }
$Destino = [IO.Path]::GetFullPath($Destino)
if (Test-Path -LiteralPath $Destino) { throw "O destino ja existe: $Destino. Nada sera sobrescrito. Escolha uma pasta nova." }

$pedirConfirmacao = $null -eq $PinGerente
if ($pedirConfirmacao) { $PinGerente = Read-Host 'Escolha seu PIN pessoal de 4 digitos (nao ha PIN padrao)' -AsSecureString }
$ponteiro = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($PinGerente)
$pinTexto = $null
try {
    $pinTexto = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ponteiro)
    if ($pinTexto -notmatch '^[0-9]{4}$') { throw 'O PIN deve ter exatamente 4 digitos numericos.' }
    if ($pedirConfirmacao) {
        $confirmacao = Read-Host 'Confirme o PIN' -AsSecureString
        $ponteiroConfirmacao = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($confirmacao)
        try {
            if ($pinTexto -cne [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ponteiroConfirmacao)) { throw 'Os PINs nao conferem.' }
        }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ponteiroConfirmacao) }
    }

    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
    $projeto = Join-Path $PSScriptRoot '..\src\ChurrascariaPDV\ChurrascariaPDV.csproj'
    & $dotnet publish $projeto --configuration Release --output $Destino --no-restore -p:UseAppHost=false
    if ($LASTEXITCODE -ne 0) { throw 'Publicacao falhou. Verifique o SDK .NET e execute dotnet restore no repositorio se faltarem dependencias.' }

    $configuracao = [ordered]@{
        ConnectionStrings = @{ DefaultConnection = 'Data Source=estabelecimento.db' }
        Servidor = @{ Porta = $Porta }
        Instalacao = @{ Tipo = $Tipo; Nome = $Nome.Trim(); NomeGerente = $NomeGerente.Trim() }
        Impressao = @{ ModoSimulacao = $true; TimeoutMs = 3000 }
        Logging = @{ LogLevel = @{ Default = 'Information'; 'Microsoft.AspNetCore' = 'Warning'; 'Microsoft.EntityFrameworkCore.Database.Command' = 'Warning' } }
        AllowedHosts = '*'
    }
    $utf8 = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText((Join-Path $Destino 'appsettings.json'), ($configuracao | ConvertTo-Json -Depth 6), $utf8)
    Push-Location -LiteralPath $Destino
    try {
        # O PIN vai apenas pela entrada padrao, nunca em argumentos, JSON ou logs.
        $pinTexto | & $dotnet (Join-Path $Destino 'ChurrascariaPDV.dll') --instalar
        if ($LASTEXITCODE -ne 0) { throw "Instalacao incompleta em $Destino. Nao inicie esta pasta; consulte o erro acima. Nenhuma pasta existente foi alterada." }
    }
    finally { Pop-Location }

    $iniciar = @'
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:ASPNETCORE_ENVIRONMENT = 'Production'
Write-Host 'Caixa: http://localhost:PORTA_PDV/caixa'
& dotnet (Join-Path $PSScriptRoot 'ChurrascariaPDV.dll')
if ($LASTEXITCODE -ne 0) { throw 'O servidor do PDV encerrou com erro.' }
'@
    [IO.File]::WriteAllText((Join-Path $Destino 'iniciar.ps1'), $iniciar.Replace('PORTA_PDV', [string]$Porta), $utf8)
    Write-Host "Instalacao pronta em $Destino"
    Write-Host "Execute .\iniciar.ps1 nessa pasta e abra http://localhost:$Porta/caixa"
    Write-Host 'Impressao em simulacao. Cadastre os IPs antes de habilitar impressao real.'
    Write-Host 'Acesso externo exige HTTPS/VPN e autenticacao reforcada; nao exponha a porta do caixa na internet.'
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ponteiro)
    $pinTexto = $null
}
