# Generates PLACEHOLDER instructor voice clips with the offline Windows speech engine (no internet, no paid service).
# These are synthetic text-to-speech recordings, not professional voice acting. Replace each WAV with a recorded
# clip of the same file name and wording when real recordings are available (see Assets/SafeRoute/Audio/README.md).
#
# Usage (Windows PowerShell 5.1):  powershell -ExecutionPolicy Bypass -File Tools\Audio\generate_voice_placeholders.ps1
# Then run:  python Tools\Audio\process_audio.py voice   (trims silence and normalises loudness)

param(
    [string]$VoiceName = 'Mark',   # OneCore voices: Mark, David, Zira (en-US); Ravi, Heera (en-IN)
    [string]$Rate = '-8%'          # slightly slower than default: calm, clear emergency guidance
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$outDir = Join-Path $root 'Assets\SafeRoute\Audio\Voice'
$csv = Join-Path $PSScriptRoot 'voice_lines.csv'
New-Item -ItemType Directory -Force $outDir | Out-Null

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($operation, [Type]$resultType) {
    $task = $asTask.MakeGenericMethod($resultType).Invoke($null, @($operation))
    $task.Wait(-1) | Out-Null
    $task.Result
}

[Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media.SpeechSynthesis, ContentType = WindowsRuntime] | Out-Null
[Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime] | Out-Null
$synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
$voice = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices | Where-Object { $_.DisplayName -like "*$VoiceName*" } | Select-Object -First 1
if ($null -eq $voice) { throw "Voice '$VoiceName' is not installed." }
$synth.Voice = $voice
Write-Host "Voice: $($voice.DisplayName) ($($voice.Language))"

foreach ($line in Import-Csv $csv) {
    $text = [System.Security.SecurityElement]::Escape($line.text)
    $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='$($voice.Language)'>" +
            "<prosody rate='$Rate'>$text</prosody></speak>"
    $stream = Await ($synth.SynthesizeSsmlToStreamAsync($ssml)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
    $reader = New-Object Windows.Storage.Streams.DataReader($stream.GetInputStreamAt(0))
    $size = [uint32]$stream.Size
    Await ($reader.LoadAsync($size)) ([uint32]) | Out-Null
    $bytes = New-Object byte[] $size
    $reader.ReadBytes($bytes)
    $path = Join-Path $outDir ("instructor_" + $line.id + ".wav")
    [System.IO.File]::WriteAllBytes($path, $bytes)
    Write-Host "  $($line.id): $size bytes"
}
