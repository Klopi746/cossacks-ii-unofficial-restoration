[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$vendor=Join-Path $projectRoot 'vendor'
$output=Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Path $vendor,$output -Force | Out-Null
[Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
function Fetch-Verified([string]$Url,[string]$Path,[string]$Expected) {
    if(!(Test-Path -LiteralPath $Path) -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected) {
        Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile ($Path+'.download')
        if((Get-FileHash -LiteralPath ($Path+'.download')).Hash -ne $Expected) { throw "Checksum mismatch: $Url" }
        Move-Item -LiteralPath ($Path+'.download') -Destination $Path -Force
    }
}
$compilerPackage=Join-Path $vendor 'microsoft.net.compilers.toolset.4.8.0.nupkg'
Fetch-Verified 'https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/4.8.0/microsoft.net.compilers.toolset.4.8.0.nupkg' $compilerPackage '37333F4F1E2CE55E621355D6DA651DC23D4CB5F94A8F76B9478816E87F110AD9'
$dxvkArchive=Join-Path $vendor 'dxvk-3.1.1.tar.gz'
Fetch-Verified 'https://github.com/doitsujin/dxvk/releases/download/v3.1.1/dxvk-3.1.1.tar.gz' $dxvkArchive '40565B4A724AADC4433FA4E010B4B23916D9B1F1BAEEE64E17186DB94F54E608'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$compilerFolder=Join-Path $vendor 'compiler-4.8.0'
if(!(Test-Path -LiteralPath (Join-Path $compilerFolder 'tasks\net472\csc.exe'))) { [IO.Compression.ZipFile]::ExtractToDirectory($compilerPackage,$compilerFolder) }
$dxvkFolder=Join-Path $vendor 'dxvk-3.1.1'
if(!(Test-Path -LiteralPath (Join-Path $dxvkFolder 'x32\d3d8.dll'))) {
    & (Join-Path $env:WINDIR 'System32\tar.exe') -xzf $dxvkArchive -C $vendor
    if($LASTEXITCODE -ne 0) { throw 'DXVK extraction failed.' }
}
$payloadFiles=@{}
foreach($item in @(@('d3d8.dll','9152BA43386E756790A66F61CDCEE01B3287D156D4161C7456D816FAA82FC364'),@('d3d9.dll','265888C31CA78DFFA290C39CB7E50BFB02762590E41927906E46FB32F01497FA'))) {
    $path=Join-Path $dxvkFolder ('x32\'+$item[0])
    if((Get-FileHash -LiteralPath $path).Hash -ne $item[1]) { throw ('Wrong DXVK DLL: '+$item[0]) }
    $payloadFiles['dxvk/'+$item[0]]=$path
}
$payloadFiles['profile-template.ini']=Join-Path $projectRoot 'src\profile-template.ini'
$payloadFiles['LICENSE']=Join-Path $projectRoot 'LICENSE'
$payloadFiles['THIRD_PARTY.md']=Join-Path $projectRoot 'THIRD_PARTY.md'
$thirdParty=Join-Path $projectRoot 'third_party'
$provenance=Get-Content -LiteralPath (Join-Path $thirdParty 'DxWnd-2.06.16-provenance.json') -Raw | ConvertFrom-Json
foreach($file in $provenance.binaries.PSObject.Properties) {
    $path=Join-Path $thirdParty ('DxWnd-2.06.16\'+$file.Name)
    if((Get-FileHash -LiteralPath $path).Hash -ne $file.Value) { throw ('Wrong DxWnd binary: '+$file.Name) }
    $payloadFiles['dxwnd/'+$file.Name]=$path
}
$sourceBundle=Join-Path $thirdParty 'DxWnd-2.06.16-corresponding-source.zip'
if((Get-FileHash -LiteralPath $sourceBundle).Hash -ne $provenance.corresponding_source_sha256) { throw 'Corresponding-source bundle hash mismatch.' }
$payloadFiles['sources/DxWnd-2.06.16-corresponding-source.zip']=$sourceBundle
$payloadFiles['sources/DxWnd-2.06.16-provenance.json']=Join-Path $thirdParty 'DxWnd-2.06.16-provenance.json'
foreach($file in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'licenses') -File) { $payloadFiles['licenses/'+$file.Name]=$file.FullName }
$payload=Join-Path $output 'payload.zip'
$compiler=Join-Path $compilerFolder 'tasks\net472\csc.exe'
$zipWriter=Join-Path $output 'build-zip.exe'
& $compiler '/nologo' '/deterministic+' '/optimize+' '/target:exe' ('/out:'+$zipWriter) ('/pathmap:'+$projectRoot+'=/src') (Join-Path $projectRoot 'build\ZipPayload.cs')
if($LASTEXITCODE -ne 0) { throw 'ZIP helper compilation failed.' }
$zipArguments=@($payload)+@($payloadFiles.Keys | Sort-Object | ForEach-Object { $_+'='+$payloadFiles[$_] })
& $zipWriter $zipArguments
if($LASTEXITCODE -ne 0) { throw 'Payload creation failed.' }
$setup=Join-Path $output 'CossacksII-Unofficial-Restoration-0.1.1-preview-setup.exe'
$sources=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
$arguments=@('/nologo','/deterministic+','/optimize+','/debug-','/target:winexe','/platform:anycpu',('/pathmap:'+$projectRoot+'=/src'),('/out:'+$setup),('/win32manifest:'+(Join-Path $projectRoot 'src\app.manifest')),('/resource:'+$payload+',payload.zip'),('/resource:'+(Join-Path $projectRoot 'src\patch-plan.json')+',patch-plan.json'),'/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.IO.Compression.dll','/r:Microsoft.CSharp.dll')+$sources
& $compiler $arguments
if($LASTEXITCODE -ne 0) { throw 'C# compilation failed.' }
$hash=(Get-FileHash -LiteralPath $setup).Hash
Copy-Item -LiteralPath $sourceBundle -Destination (Join-Path $output ([IO.Path]::GetFileName($sourceBundle))) -Force
$checksumText=$hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($setup)+"`n"+$provenance.corresponding_source_sha256.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($sourceBundle)+"`n"
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'),$checksumText,(New-Object Text.UTF8Encoding($false)))
[pscustomobject]@{Version='0.1.1-preview';SHA256=$hash;Bytes=(Get-Item -LiteralPath $setup).Length;Compiler='Microsoft.Net.Compilers.Toolset 4.8.0';Deterministic=$true;Bundled='Own code / recipe / DXVK x86 / DxWnd 2.06.16 / corresponding source / notices';InstallNetworkRequired=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build-manifest.json') -Encoding UTF8
Write-Output "Built $setup"
Write-Output "SHA-256 $hash"
