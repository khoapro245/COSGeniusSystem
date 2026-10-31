param([string]$Workspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path)
$ErrorActionPreference = 'Stop'
$sdkInfo = & dotnet --list-sdks | Where-Object { $_ -match '^9\.' } | Select-Object -Last 1
if (!$sdkInfo) { throw 'This offline contract checker requires the installed .NET 9 SDK/runtime.' }
$sdkVersion = ($sdkInfo -split ' ')[0]
$sdkRoot = ($sdkInfo -replace '^.*\[','' -replace '\]$','')
$dotnetRoot = Split-Path $sdkRoot -Parent
$compiler = Join-Path $sdkRoot "$sdkVersion/Roslyn/bincore"
$coreRef = Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '9.*' | Sort-Object { [version]$_.Name } | Select-Object -Last 1
$aspRef = Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.AspNetCore.App.Ref') -Directory | Where-Object Name -Like '9.*' | Sort-Object { [version]$_.Name } | Select-Object -Last 1
$outDir = Join-Path $PSScriptRoot 'obj/offline'
New-Item -ItemType Directory -Force $outDir | Out-Null
$references = @(@($coreRef, $aspRef) | ForEach-Object {
    Get-ChildItem (Join-Path $_.FullName 'ref/net9.0') -Filter *.dll | ForEach-Object { '/r:"' + $_.FullName + '"' }
})
foreach ($name in @('Microsoft.CodeAnalysis.dll', 'Microsoft.CodeAnalysis.CSharp.dll')) {
    $references += '/r:"' + (Join-Path $compiler $name) + '"'
    Copy-Item -LiteralPath (Join-Path $compiler $name) -Destination $outDir
}
$paths = @('COSGeniusSystem/DTOs/Examinations', 'COSGeniusSystem/Commons', 'COSGeniusSystem/Exceptions')
$sources = @($paths | ForEach-Object {
    Get-ChildItem (Join-Path $Workspace $_) -Filter *.cs | ForEach-Object { '"' + $_.FullName + '"' }
})
foreach ($path in @('COSGeniusSystem/DTOs/Patients/Responses/PatientResponse.cs', 'COSGeniusSystem/Common/RoleConstants.cs',
    'COSGeniusSystem/Controllers/BaseApiController.cs', 'COSGeniusSystem/Controllers/ExaminationsController.cs',
    'COSGeniusSystem/Services/Examinations/IExaminationService.cs', 'tests/ExaminationApi.ContractChecks/Program.cs')) {
    $sources += '"' + (Join-Path $Workspace $path) + '"'
}
$globalUsings = Join-Path $outDir 'GlobalUsings.cs'
Set-Content $globalUsings 'global using System; global using System.Collections.Generic; global using System.IO; global using System.Linq; global using System.Threading; global using System.Threading.Tasks;'
$sources += '"' + $globalUsings + '"'
$dll = Join-Path $outDir 'ContractChecks.dll'
$responseFile = Join-Path $outDir 'checks.rsp'
Set-Content $responseFile (@('/nologo', '/target:exe', '/nullable:enable', ('/out:"' + $dll + '"')) + $references + $sources)
& dotnet (Join-Path $compiler 'csc.dll') "@$responseFile"
if ($LASTEXITCODE -ne 0) { throw 'Contract compilation failed.' }
Set-Content (Join-Path $outDir 'ContractChecks.runtimeconfig.json') '{"runtimeOptions":{"tfm":"net9.0","frameworks":[{"name":"Microsoft.NETCore.App","version":"9.0.0"},{"name":"Microsoft.AspNetCore.App","version":"9.0.0"}]}}'
& dotnet $dll $Workspace
if ($LASTEXITCODE -ne 0) { throw 'Contract checks failed.' }
