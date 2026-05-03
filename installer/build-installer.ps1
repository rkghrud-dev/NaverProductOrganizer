param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$PublishDir = Join-Path $ProjectRoot "bin\$Configuration\net10.0-windows\$Runtime\publish"
$StagingRoot = "C:\tmp\NaverProductOrganizerInstaller"
$PayloadDir = Join-Path $StagingRoot "payload"
$DistDir = Join-Path $ProjectRoot "dist"
$SedPath = Join-Path $StagingRoot "NaverProductOrganizer.sed"
$SetupPath = Join-Path $StagingRoot "NaverProductOrganizerSetup.exe"
$FinalSetupPath = Join-Path $DistDir "NaverProductOrganizerSetup.exe"

dotnet publish $ProjectRoot -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true

if (Test-Path $StagingRoot) {
    Remove-Item -LiteralPath $StagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $PayloadDir | Out-Null
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
Copy-Item -Path (Join-Path $PublishDir "NaverProductOrganizer.exe") -Destination $PayloadDir -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "install.cmd") -Destination $PayloadDir -Force

$files = Get-ChildItem -LiteralPath $PayloadDir -File | Sort-Object Name
$stringLines = New-Object System.Collections.Generic.List[string]
$sourceLines = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $files.Count; $i++) {
    $key = "FILE$i"
    $stringLines.Add("$key=`"$($files[$i].Name)`"")
    $sourceLines.Add("%$key%=")
}

$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=%AdminQuietInstCmd%
UserQuietInstCmd=%UserQuietInstCmd%
SourceFiles=SourceFiles

[SourceFiles]
SourceFiles0=$PayloadDir\

[SourceFiles0]
$($sourceLines -join "`r`n")

[Strings]
FinishMessage="Naver Product Organizer installation complete."
TargetName="$SetupPath"
FriendlyName="Naver Product Organizer"
AppLaunched="cmd /c install.cmd"
PostInstallCmd="<None>"
AdminQuietInstCmd=
UserQuietInstCmd=
$($stringLines -join "`r`n")
"@

Set-Content -LiteralPath $SedPath -Value $sed -Encoding ASCII
& iexpress.exe /N /Q $SedPath
$iexpressExitCode = $LASTEXITCODE

if (-not (Test-Path -LiteralPath $SetupPath)) {
    throw "Installer was not created: $SetupPath (IExpress exit code: $iexpressExitCode)"
}

Copy-Item -LiteralPath $SetupPath -Destination $FinalSetupPath -Force
Write-Host "Created installer: $FinalSetupPath"
