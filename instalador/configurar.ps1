# Configura o estabelecimento na pasta instalada: tipo, nome, gerente e PIN pessoal (sem PIN padrao).
# Executado pelo instalador na primeira instalacao; atualizacoes preservam o banco existente.
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$banco = Join-Path $PSScriptRoot 'estabelecimento.db'
$pendente = "$banco.instalacao-pendente"

function Pausar { Read-Host 'Pressione Enter para fechar' | Out-Null }

try {
    if ((Test-Path -LiteralPath $banco) -and -not (Test-Path -LiteralPath $pendente)) {
        Write-Host 'Este PDV ja esta configurado. O banco existente foi preservado.'
        Pausar
        return
    }
    if (Test-Path -LiteralPath $pendente) {
        # A configuracao anterior parou antes de criar o gerente; o PDV nunca iniciou com esse banco.
        Write-Host 'Configuracao anterior incompleta encontrada. Ela sera refeita.'
        foreach ($arquivo in @($banco, "$banco-wal", "$banco-shm", "$banco.seed-pendente", $pendente)) {
            if (Test-Path -LiteralPath $arquivo) { Remove-Item -LiteralPath $arquivo -Force }
        }
    }

    Write-Host '=== Configuracao do PDV ==='
    $tipos = @('Churrascaria', 'Pizzaria', 'Hamburgueria', 'Bar', 'RestaurantePorQuilo', 'Restaurante')
    for ($i = 0; $i -lt $tipos.Count; $i++) { Write-Host "$($i + 1) - $($tipos[$i])" }
    do { $escolha = Read-Host "Tipo do estabelecimento (1 a $($tipos.Count))" } while ($escolha -notmatch "^[1-$($tipos.Count)]$")
    $tipo = $tipos[[int]$escolha - 1]
    do { $nome = (Read-Host 'Nome do estabelecimento').Trim() } while ($nome.Length -eq 0 -or $nome.Length -gt 120)
    do { $nomeGerente = (Read-Host 'Nome do dono/gerente').Trim() } while ($nomeGerente.Length -eq 0 -or $nomeGerente.Length -gt 100)

    $pinTexto = $null
    while ($true) {
        $pin = Read-Host 'Escolha seu PIN pessoal de 4 digitos (nao ha PIN padrao)' -AsSecureString
        $confirmacao = Read-Host 'Confirme o PIN' -AsSecureString
        $p1 = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($pin)
        $p2 = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($confirmacao)
        try {
            $a = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($p1)
            $b = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($p2)
            if ($a -notmatch '^[0-9]{4}$') { Write-Host 'O PIN deve ter exatamente 4 digitos numericos.' }
            elseif ($a -cne $b) { Write-Host 'Os PINs nao conferem.' }
            else { $pinTexto = $a; break }
        }
        finally {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($p1)
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($p2)
        }
    }

    $configuracao = [ordered]@{
        ConnectionStrings = @{ DefaultConnection = 'Data Source=estabelecimento.db' }
        Servidor = @{ Porta = 5000 }
        Instalacao = @{ Tipo = $tipo; Nome = $nome; NomeGerente = $nomeGerente }
        Impressao = @{ ModoSimulacao = $true; TimeoutMs = 3000 }
        Logging = @{ LogLevel = @{ Default = 'Information'; 'Microsoft.AspNetCore' = 'Warning'; 'Microsoft.EntityFrameworkCore.Database.Command' = 'Warning' } }
        AllowedHosts = '*'
    }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'appsettings.json'), ($configuracao | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding($false)))

    # O PIN vai apenas pela entrada padrao, nunca em argumentos, JSON ou logs.
    $pinTexto | & (Join-Path $PSScriptRoot 'ChurrascariaPDV.exe') --instalar
    $pinTexto = $null
    if ($LASTEXITCODE -ne 0) { throw 'A configuracao nao foi concluida. Abra "Configurar PDV" no menu Iniciar para tentar de novo.' }

    Write-Host ''
    Write-Host 'PDV configurado. Use o atalho "PDV - Caixa" na area de trabalho para abrir o caixa.'
    Write-Host 'Impressao em simulacao: cadastre os IPs das impressoras em Gerencia > Setores antes de habilitar.'
    Pausar
}
catch {
    Write-Host "ERRO: $($_.Exception.Message)" -ForegroundColor Red
    Pausar
    exit 1
}
