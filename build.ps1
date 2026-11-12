param(
    [string]$RailroaderManagedPath = $env:RAILROADER_MANAGED
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RailroaderManagedPath)) {
    $candidates = @(
        'D:\SteamLibrary\steamapps\common\Railroader\Railroader_Data\Managed',
        'C:\Program Files (x86)\Steam\steamapps\common\Railroader\Railroader_Data\Managed'
    )
    $RailroaderManagedPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($RailroaderManagedPath) -or -not (Test-Path -LiteralPath $RailroaderManagedPath)) {
    throw 'RailroaderManagedPath must point to Railroader_Data\Managed.'
}

$contract = Join-Path $RailroaderManagedPath 'Railroader.Modding.dll'
$game = Join-Path $RailroaderManagedPath 'Railroader.dll'
if (-not (Test-Path -LiteralPath $contract) -or -not (Test-Path -LiteralPath $game)) {
    throw "The managed directory does not contain Railroader.Modding.dll and Railroader.dll: $RailroaderManagedPath"
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$package = Join-Path $root 'dist\RailroaderCheatMenu'
$output = Join-Path $root 'dist\build\'
if (Test-Path -LiteralPath $package) {
    Remove-Item -LiteralPath $package -Recurse -Force
}
New-Item -ItemType Directory -Path $package | Out-Null

dotnet build (Join-Path $root 'src\RailroaderCheatMenu\RailroaderCheatMenu.csproj') `
    --configuration Release `
    -p:RailroaderManagedPath=$RailroaderManagedPath `
    -p:OutputPath="$output"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $root 'mod\Info.json') -Destination $package
Copy-Item -LiteralPath (Join-Path $output 'RailroaderCheatMenu.dll') -Destination $package

Write-Output "Built asset pack: $package"

