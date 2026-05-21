param(
  [Parameter(Mandatory=$true)][string]$TargetsJson,
  [string]$RunDir
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($RunDir) {
  python3 "$ScriptDir/run-corpus.py" $TargetsJson $RunDir
} else {
  python3 "$ScriptDir/run-corpus.py" $TargetsJson
}
