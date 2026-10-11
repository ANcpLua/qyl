#!/usr/bin/env python3
"""Check runtime-before-tool publication and failure behavior without network access."""
import pathlib,re,subprocess,tempfile
source=(pathlib.Path(__file__).resolve().parents[2]/'release.sh').read_text()
functions='\n'.join(re.search(r'^'+name+r'\(\) \{\n.*?^\}',source,re.M|re.S).group(0) for name in ['wait_for_indexed','publish_release_set'])
harness='''set -eu
scratch=$1
mode=$2
OUT=$scratch
NUGET_KEY_FILE=$scratch/key
NUGET_SOURCE=unused
version=12.0.0
rids='linux-x64 osx-arm64'
printf dummy > "$NUGET_KEY_FILE"
say() { :; }
sleep() { :; }
indexed() { test -f "$scratch/index-$1"; }
dotnet() {
  local filename id
  filename=$(basename "$3")
  printf '%s\\n' "$filename" >> "$scratch/uploads"
  if [[ $mode == upload-failure && $filename == qyl.osx-arm64.* ]]; then return 42; fi
  id=${filename%.12.0.0.nupkg}
  if [[ $mode == pending-index && $id == qyl.osx-arm64 ]]; then return 0; fi
  touch "$scratch/index-$id"
}
'''+functions+'''
if publish_release_set; then result=0; else result=$?; fi
printf '%s\\n' "$result" > "$scratch/result"
'''
with tempfile.TemporaryDirectory(prefix='qyl-release-order-test-') as root:
 p=pathlib.Path(root); script=p/'test.sh'; script.write_text(harness)
 for mode in ['success','pending-index','upload-failure']:
  case=p/mode; case.mkdir(); subprocess.run(['bash',str(script),str(case),mode],check=True)
  uploads=(case/'uploads').read_text().splitlines(); status=int((case/'result').read_text())
  if mode=='success':
   assert status==0 and uploads==['qyl.linux-x64.12.0.0.nupkg','qyl.osx-arm64.12.0.0.nupkg','Qyl.Api.Sdk.12.0.0.nupkg','qyl.12.0.0.nupkg'],uploads
  else:
   assert status!=0 and 'qyl.12.0.0.nupkg' not in uploads and 'Qyl.Api.Sdk.12.0.0.nupkg' not in uploads,(status,uploads)
  print(mode+': PASS')
