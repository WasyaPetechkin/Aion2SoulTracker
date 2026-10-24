param([string]$DotnetPath='dotnet',[string]$IsccPath='', [string]$Version='0.3.5',[string]$SigningScript='')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$dist=Join-Path $root 'dist'
$payload=Join-Path $dist 'payload'
Add-Type -AssemblyName System.IO.Compression.FileSystem
if($SigningScript){$SigningScript=(Resolve-Path -LiteralPath $SigningScript).Path}
function Assert-Signed([string]$path) {
    $signature=Get-AuthenticodeSignature -LiteralPath $path
    if($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate){throw "A trusted, timestamped signature is required: $path"}
}

function Reset-BuildFolder([string]$path) {
    $absolute=[IO.Path]::GetFullPath($path)
    $allowed=[IO.Path]::GetFullPath($dist).TrimEnd('\')+'\'
    if(-not $absolute.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw "Unsafe build path: $absolute"}
    if(Test-Path -LiteralPath $absolute){Remove-Item -LiteralPath $absolute -Recurse -Force}
    New-Item -ItemType Directory -Path $absolute -Force | Out-Null
}
function New-Zip([string]$folder,[string]$path) {
    if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}
    $zip=[IO.Compression.ZipFile]::Open($path,[IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $folder -Recurse -File | ForEach-Object {
            $relative=$_.FullName.Substring($folder.TrimEnd('\').Length+1).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$_.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally {$zip.Dispose()}
}
if(-not $IsccPath) {
    $command=Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if($command){$IsccPath=$command.Source}
    else {
        $IsccPath=@("$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe","$env:ProgramFiles\Inno Setup 7\ISCC.exe","${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe") | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
    }
}
if(-not $IsccPath){throw 'Install Inno Setup 7 from https://jrsoftware.org/isdl.php or pass -IsccPath.'}
New-Item -ItemType Directory -Path $dist -Force | Out-Null
Reset-BuildFolder $payload
& $DotnetPath publish (Join-Path $root 'src/SoulTracker.App/SoulTracker.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:SatelliteResourceLanguages=en%3Bru -p:DebugType=none -p:DebugSymbols=false "-p:Version=$Version" -o $payload --nologo
if($LASTEXITCODE -ne 0){throw 'Publish failed'}
foreach($required in 'Aion2SoulTracker.exe','coreclr.dll','PresentationFramework.dll','maps/spawns.json','pet-catalog.json') {
    if(-not (Test-Path -LiteralPath (Join-Path $payload $required))){throw "Missing payload file: $required"}
}

$sourceStage=Join-Path $dist 'source-stage'
Reset-BuildFolder $sourceStage
foreach($item in 'src','tests','assets','licenses','installer','scripts','docs','.github','.gitignore','README.md','START-HERE.txt','CHANGELOG.md','LICENSE','build.ps1') {
    $source=Join-Path $root $item
    $files=if((Get-Item -LiteralPath $source).PSIsContainer){Get-ChildItem -LiteralPath $source -Recurse -File}else{Get-Item -LiteralPath $source}
    foreach($file in $files) {
        $relative=$file.FullName.Substring($root.Length+1)
        if($relative -match '\\(bin|obj)(\\|$)'){continue}
        if($file.Extension -in '.pcap','.pcapng','.pdb'){throw "Unexpected source file: $relative"}
        $target=Join-Path $sourceStage $relative
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}
$sourceZip=Join-Path $dist "Aion2SoulTracker-v$Version-source.zip"
New-Zip $sourceStage $sourceZip
Copy-Item -LiteralPath $sourceZip -Destination (Join-Path $payload 'source.zip')
if($SigningScript) {
    foreach($name in 'Aion2SoulTracker.exe','Aion2SoulTracker.dll','SoulTracker.Core.dll') {
        $file=Join-Path $payload $name
        & $SigningScript -FilePath $file
        if(-not $?){throw "Signing failed: $file"}
        Assert-Signed $file
    }
}
$installer=Join-Path $root 'installer/Aion2SoulTracker.iss'
$compilerArgs=@('/Qp',"/DAppVer=$Version","/DPayloadDir=$payload","/O$dist")
if($SigningScript) {
    # Inno uses $q for quotes and $f for its generated installer/uninstaller path.
    $signCommand='$q'+(Join-Path $PSHOME 'powershell.exe').Replace('$','$$')+'$q -NoProfile -File $q'+$SigningScript.Replace('$','$$')+'$q -FilePath $f'
    if(-not (Test-Path -LiteralPath (Join-Path $PSHOME 'powershell.exe'))) {
        $signCommand='$q'+(Join-Path $PSHOME 'pwsh.exe').Replace('$','$$')+'$q -NoProfile -File $q'+$SigningScript.Replace('$','$$')+'$q -FilePath $f'
    }
    $compilerArgs+=@('/DSignEnabled=1',('/Srelease='+$signCommand))
}
& $IsccPath @compilerArgs $installer
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
if($SigningScript){Assert-Signed (Join-Path $dist "Aion2SoulTracker-Setup-v$Version.exe")}
else {Write-Warning 'Unsigned test build: a trusted code-signing certificate has not been configured.'}
New-Zip $payload (Join-Path $dist "Aion2SoulTracker-v$Version-portable-win-x64.zip")
Get-ChildItem -LiteralPath $dist -File | Where-Object Extension -In '.exe','.zip' | ForEach-Object {
    $hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
} | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Get-ChildItem -LiteralPath $dist -File | Select-Object Name,Length
