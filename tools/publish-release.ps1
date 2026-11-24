<#
.SYNOPSIS
    Собирает выпуск: архив со сборкой и манифест latest.json для автообновления.

.DESCRIPTION
    Поднимает версию в Wpf.csproj, собирает Release, кладёт сборку в zip,
    считает SHA-256 и пишет манифест, который читает кнопка «Проверить
    обновление» у клиента.

    Из архива намеренно исключены appsettings.json и app.db: первый содержит
    пароль приложения от почты, второй — рабочую базу. Публиковать их нельзя,
    а у клиента они и так останутся на месте — скрипт обновления их не трогает.

.PARAMETER Version
    Версия выпуска, например 1.1.0.

.PARAMETER PackageUrl
    Где будет лежать архив. Подставляется в манифест как есть.

.EXAMPLE
    .\tools\publish-release.ps1 -Version 1.1.0 `
        -PackageUrl https://github.com/TashievEmir/MyWarehouse/releases/download/v1.1.0/MyWarehouse-1.1.0.zip `
        -Notes "Штрихкоды и этикетки"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^\d+\.\d+\.\d+$')] [string] $Version,
    [Parameter(Mandatory)] [string] $PackageUrl,
    [string] $Notes = '',
    [string] $OutputDirectory = 'release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$csproj = Join-Path $root 'Wpf\Wpf.csproj'
$output = Join-Path $root $OutputDirectory
$build = Join-Path $output "build-$Version"

Write-Host "Проект:  $root"
Write-Host "Версия:  $Version"
Write-Host ''

# ---------- 1. Версия в проекте ----------
# Клиент сравнивает её с манифестом, поэтому она должна совпадать

$xml = [xml](Get-Content $csproj -Raw)

# Ищем узел явно: когда PropertyGroup в проекте один, PowerShell отдаёт
# не массив, и обращение по индексу уводит к дочернему элементу
$node = $xml.SelectSingleNode('/Project/PropertyGroup/Version')

if (-not $node) {
    throw "В $csproj нет элемента <Version> — добавьте его в PropertyGroup"
}

$node.InnerText = $Version
$xml.Save($csproj)

Write-Host "Версия в Wpf.csproj выставлена: $Version"

# ---------- 2. Сборка ----------

if (Test-Path $build) { Remove-Item $build -Recurse -Force }
New-Item -ItemType Directory -Force -Path $build | Out-Null

Write-Host 'Собираем Release...'

dotnet publish $csproj -c Release -o $build --nologo -v quiet

if ($LASTEXITCODE -ne 0) { throw 'Сборка не удалась' }

# ---------- 3. Чистим архив от чужого ----------

$exclude = @('appsettings.json', 'app.db', 'app.db-wal', 'app.db-shm')

foreach ($name in $exclude) {
    $path = Join-Path $build $name
    if (Test-Path $path) {
        Remove-Item $path -Force
        Write-Host "Исключено из архива: $name"
    }
}

Get-ChildItem $build -Filter *.pdb -Recurse | Remove-Item -Force

if (-not (Test-Path (Join-Path $build 'Wpf.exe'))) {
    throw 'В сборке нет Wpf.exe — клиент такой архив отвергнет'
}

# ---------- 4. Архив ----------

$zipName = "MyWarehouse-$Version.zip"
$zip = Join-Path $output $zipName

if (Test-Path $zip) { Remove-Item $zip -Force }

Compress-Archive -Path (Join-Path $build '*') -DestinationPath $zip -CompressionLevel Optimal

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
$size = (Get-Item $zip).Length

Write-Host ''
Write-Host "Архив:   $zip"
Write-Host "Размер:  $([math]::Round($size / 1MB, 2)) МБ"
Write-Host "SHA-256: $hash"

# ---------- 5. Манифест ----------

$manifest = [ordered]@{
    version    = $Version
    releasedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    notes      = $Notes
    packageUrl = $PackageUrl
    sha256     = $hash
    sizeBytes  = $size
}

# Манифест кладём в docs: папка release/ в гитигноре, а манифест надо
# закоммитить — GitHub Pages умеет публиковать прямо из docs/ ветки main
$docs = Join-Path $root 'docs'
New-Item -ItemType Directory -Force -Path $docs | Out-Null

$manifestPath = Join-Path $docs 'latest.json'

$manifest | ConvertTo-Json -Depth 3 | Set-Content $manifestPath -Encoding utf8

Remove-Item $build -Recurse -Force

Write-Host ''
Write-Host "Манифест: $manifestPath"
Write-Host ''
Write-Host 'Осталось сделать руками:'
Write-Host "  1. Выложить $zipName по адресу, указанному в PackageUrl"
Write-Host '     (проще всего — приложить файлом к релизу на GitHub)'
Write-Host '  2. Залить latest.json на GitHub Pages — туда, куда смотрит'
Write-Host '     Updates:ManifestUrl в appsettings.json клиента'
Write-Host '  3. Закоммитить поднятую версию в Wpf.csproj'
