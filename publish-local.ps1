#Requires -Version 7
$ErrorActionPreference = 'Stop'

$artifacts  = Join-Path $PSScriptRoot 'artifacts'
$configFile = Join-Path $PSScriptRoot 'nuget.config'
$apiKey     = $env:BAGET_API_KEY ?? 'BAGET'

Write-Host "Cleaning artifacts..."
if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }
New-Item $artifacts -ItemType Directory | Out-Null

Write-Host "Building..."
dotnet build src/RetailForge.Generators.slnx -c Release --nologo

Write-Host "Packing..."
dotnet pack src/RetailForge.Generators.Abstractions/RetailForge.Generators.Abstractions.csproj -c Release -o $artifacts --no-build --nologo
dotnet pack src/RetailForge.Generators/RetailForge.Generators.csproj                           -c Release -o $artifacts --no-build --nologo

Write-Host "Pushing to BaGet..."
Get-ChildItem $artifacts -Filter '*.nupkg' | ForEach-Object {
    Write-Host "  -> $($_.Name)"
    dotnet nuget push $_.FullName --source retailforge --configfile $configFile --api-key $apiKey --skip-duplicate
}

Write-Host "Done."
