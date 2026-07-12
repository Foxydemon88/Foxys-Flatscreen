$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$output = Join-Path $root "FlatscreenATTMod\bin\Release"
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

New-Item -ItemType Directory -Force -Path $output | Out-Null

& $compiler `
    /target:library `
    /out:"$output\FlatscreenATTMod.dll" `
    /reference:"$root\TavernLib-main\Dependencies\MelonLoader.dll" `
    /reference:"$root\TavernLib-main\Dependencies\0Harmony.dll" `
    /reference:"$root\TavernLib-main\Dependencies\UnityEngine.dll" `
    /reference:"$root\TavernLib-main\Dependencies\UnityEngine.CoreModule.dll" `
    /reference:"$root\TavernLib-main\Dependencies\UnityEngine.IMGUIModule.dll" `
    "$root\FlatscreenATTMod\Source\DesktopInput.cs" `
    "$root\FlatscreenATTMod\Source\FlatscreenMod.cs" `
    "$root\FlatscreenATTMod\Source\GameReflection.cs" `
    "$root\FlatscreenATTMod\Source\HandEmulator.cs" `
    "$root\FlatscreenATTMod\Source\LookTargeter.cs" `
    "$root\FlatscreenATTMod\Source\OpenXRInputPatches.cs" `
    "$root\FlatscreenATTMod\Source\CameraStabilizerPatches.cs" `
    "$root\FlatscreenATTMod\Source\ServerBrowserOverlay.cs"

Write-Host "Built $output\FlatscreenATTMod.dll"
