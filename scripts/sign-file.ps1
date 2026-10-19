param([Parameter(Mandatory=$true)][string]$FilePath)
$ErrorActionPreference='Stop'
# Configure these in your local shell. No private keys or passwords belong in this repository.
$certificateId=$env:AION_SIGN_CERT_SHA1
$signTool=$env:AION_SIGNTOOL
$timestampUrl=$env:AION_TIMESTAMP_URL
if($certificateId -notmatch '^[A-Fa-f0-9]{40}$'){throw 'Set AION_SIGN_CERT_SHA1 to your issued code-signing certificate thumbprint.'}
if(-not $signTool -or -not (Test-Path -LiteralPath $signTool)){throw 'Set AION_SIGNTOOL to the Windows SDK signtool.exe path.'}
if(-not $timestampUrl -or $timestampUrl -notmatch '^https?://[^\s"]+$'){throw 'Set AION_TIMESTAMP_URL to the RFC3161 URL supplied by your certificate provider.'}
$target=(Resolve-Path -LiteralPath $FilePath).Path
& $signTool sign /sha1 $certificateId /s My /fd SHA256 /tr $timestampUrl /td SHA256 $target
if($LASTEXITCODE -ne 0){throw 'Signing or timestamping failed.'}
& $signTool verify /pa /all /v $target
if($LASTEXITCODE -ne 0){throw 'Authenticode verification failed.'}
$signature=Get-AuthenticodeSignature -LiteralPath $target
if($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate){throw 'Signature must be trusted and timestamped.'}
