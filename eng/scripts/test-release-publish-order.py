#!/usr/bin/env python3
"""Check runtime-before-tool publication and failure behavior without network access."""
import json,os,pathlib,re,subprocess,tempfile,unittest
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
phase() { say "$@"; }
sleep() { SECONDS=$(( SECONDS + $1 )); }
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
   assert status==0 and uploads==['qyl.linux-x64.12.0.0.nupkg','qyl.osx-arm64.12.0.0.nupkg','Qyl.Orchestration.12.0.0.nupkg','Qyl.Api.Sdk.12.0.0.nupkg','qyl.12.0.0.nupkg'],uploads
  else:
   assert status!=0 and 'qyl.12.0.0.nupkg' not in uploads and 'Qyl.Api.Sdk.12.0.0.nupkg' not in uploads,(status,uploads)
  print(mode+': PASS')


# Exercise the complete release CLI, not only extracted functions. All external commands are
# local stubs, the key is a dummy fixture, and the shell clock advances without registry waits.
MOCK = r'''#!/usr/bin/env python3
import json, os, pathlib, sys
root = pathlib.Path(os.environ['QYL_RELEASE_TEST_ROOT'])
mode = os.environ['QYL_RELEASE_TEST_MODE']
tool = pathlib.Path(sys.argv[0]).resolve().name
args = sys.argv[1:]
version = '13.0.1'
commit = '0123456789abcdef'
packages = ['qyl', 'Qyl.Api.Sdk', 'Qyl.Orchestration', 'qyl.linux-x64', 'qyl.osx-arm64']
with (root / 'events.log').open('a') as events:
    events.write(tool + ' ' + ' '.join(args) + '\n')
if tool == 'git':
    if args[0] == 'branch': print('main')
    elif args[0] == 'ls-remote':
        if (root / 'tag').exists(): print(commit + '\trefs/tags/v' + version)
    elif args[0] == 'rev-parse':
        if '-q' in args and not (root / 'tag').exists(): sys.exit(1)
        print(commit)
    elif args[0] == 'tag': (root / 'tag').touch()
    elif args[0] == 'push' and mode == 'tag-push-failure': sys.exit(43)
elif tool == 'gh':
    if args[:2] == ['release', 'view']:
        if not (root / 'github').exists(): sys.exit(1)
        if '--json' in args and args[args.index('--json') + 1] == 'isDraft':
            print('false' if (root / 'github').read_text() == 'published' else 'true')
        elif '--json' in args:
            print('\n'.join(p + '.' + version + '.nupkg' for p in packages))
    elif args[:2] in (['release', 'create'], ['release', 'upload']):
        if mode == 'github-failure': sys.exit(45)
        existed = (root / 'github').exists()
        (root / 'github').touch()
        if mode == 'github-transient' and not existed: sys.exit(45)
    elif args[:2] == ['release', 'edit']:
        if mode == 'github-publish-failure': sys.exit(48)
        (root / 'github').write_text('published')
elif tool == 'orchestration-smoke':
    if mode == 'orchestration-smoke-failure': sys.exit(47)
elif tool == 'dotnet':
    if args[0] == 'run' and mode == 'smoke-failure': sys.exit(134)
    if args[:2] == ['nuget', 'push']:
        name = pathlib.Path(args[2]).name
        if mode == 'push-failure' and name.startswith('qyl.osx-arm64.'): sys.exit(42)
        package = name.removesuffix('.' + version + '.nupkg').lower()
        (root / ('index-' + package)).touch()
elif tool == 'curl':
    url = next(arg for arg in args if arg.startswith('https://'))
    if 'create-verification-key' in url: print('{"Key":"fixture-only"}')
    elif '/verifykey/' in url: print('200')
    else:
        package = url.split('/')[-2]
        indexed = (root / ('index-' + package)).exists()
        if mode == 'stale-index' and ('Cache-Control: no-cache' not in args or '?qyl-release=' not in url): indexed = False
        if mode == 'runtime-timeout' and package == 'qyl.osx-arm64': indexed = False
        print(json.dumps({'versions': ['12.0.0', version] if indexed else ['12.0.0']}))
elif tool == 'build':
    if args[0] == 'PackRelease':
        if mode == 'pack-failure': sys.exit(41)
        output = root / 'artifacts/nuget'
        output.mkdir(parents=True, exist_ok=True)
        for package in packages: (output / (package + '.' + version + '.nupkg')).touch()
    elif args[0] == 'VerifyRelease':
        if mode == 'bad-index-status': sys.exit(0)
        state = 'timed-out' if mode == 'verify-timeout' else 'indexed'
        (root / ('artifacts/release-' + version + '-index.status')).write_text(state + ' elapsed=1s at=fixture\n')
        if mode in ('verify-failure', 'verify-timeout'): sys.exit(42)
elif tool == 'refresh' and mode == 'snapshot-failure': sys.exit(46)
'''


class ReleaseOutcomeTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='qyl release cli ')
        self.addCleanup(self.temporary.cleanup)
        self.root = pathlib.Path(self.temporary.name)
        self.env = dict(os.environ, QYL_RELEASE_TEST_ROOT=str(self.root))
        commands = self.root / 'commands'
        commands.mkdir()
        self.env['PATH'] = str(commands) + os.pathsep + self.env['PATH']
        for name in ('git', 'gh', 'curl', 'dotnet', 'orchestration-smoke', 'build', 'refresh'):
            path = commands / name
            path.write_text(MOCK)
            path.chmod(0o755)
        (self.root / 'eng').mkdir()
        (self.root / 'eng/build.sh').symlink_to(commands / 'build')
        (self.root / 'eng/scripts').mkdir()
        (self.root / 'eng/scripts/smoke-orchestration-package.sh').symlink_to(commands / 'orchestration-smoke')
        (self.root / 'site/scripts').mkdir(parents=True)
        (self.root / 'site/scripts/refresh-published-versions.sh').symlink_to(commands / 'refresh')
        self.env['QYL_AT_DIR'] = str(self.root / 'site')
        (self.root / 'packages/Qyl.Cli').mkdir(parents=True)
        (self.root / 'packages/Qyl.Cli/Qyl.Cli.csproj').write_text(
            '<QylToolRuntimeIdentifiers>linux-x64;osx-arm64</QylToolRuntimeIdentifiers>\n')
        (self.root / 'Version.props').write_text('<QylVersion>13.0.1</QylVersion>\n')
        (self.root / 'test-key').write_text('not-a-real-key')
        script = source.replace('NUGET_KEY_FILE=$HOME/.config/vitals/nuget-qyl-manual-publish-key',
                                'NUGET_KEY_FILE="$PWD/test-key"')
        script = script.replace('set -euo pipefail\n',
                                'set -euo pipefail\nsleep() { SECONDS=$(( SECONDS + $1 )); }\n', 1)
        (self.root / 'release.sh').write_text(script)

    def run_release(self, mode='success', *arguments):
        env = dict(self.env, QYL_RELEASE_TEST_MODE=mode)
        return subprocess.run(['bash', 'release.sh', *(arguments or ('13.0.1',))],
                              cwd=self.root, env=env, text=True, capture_output=True, timeout=30)

    def status(self):
        return json.loads((self.root / 'artifacts/release-13.0.1.status.json').read_text())

    def events(self):
        return (self.root / 'events.log').read_text().splitlines()

    def test_complete_release_returns_only_after_verification_and_snapshot(self):
        result = self.run_release()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual('VERIFIED', self.status()['state'])
        events = self.events()
        positions = [next(i for i, call in enumerate(events) if call.startswith(prefix))
                     for prefix in ('build PackRelease', 'dotnet run', 'dotnet nuget push artifacts/nuget/qyl.linux-x64',
                                    'dotnet nuget push artifacts/nuget/qyl.13', 'git tag', 'gh release create',
                                    'build VerifyRelease', 'refresh ')]
        self.assertEqual(sorted(positions), positions)
        status = self.run_release('success', '--status', '13.0.1')
        self.assertEqual(0, status.returncode)
        self.assertEqual('VERIFIED', json.loads(status.stdout)['state'])

    def test_failures_stop_subsequent_stages_and_preserve_exit_codes(self):
        cases = [('pack-failure', 41, 'dotnet run'), ('smoke-failure', 134, 'dotnet nuget push'),
                 ('orchestration-smoke-failure', 47, 'dotnet nuget push'),
                 ('push-failure', 42, 'git tag'), ('tag-push-failure', 43, 'gh release create'),
                 ('github-failure', 45, 'build VerifyRelease'), ('verify-failure', 42, 'refresh '),
                 ('github-publish-failure', 48, 'refresh '),
                 ('bad-index-status', 1, 'refresh '), ('snapshot-failure', 46, None)]
        for mode, code, forbidden in cases:
            with self.subTest(mode=mode):
                # Every scenario owns a clean fixture, so no previous tag or upload can hide a failure.
                case = ReleaseOutcomeTests()
                case.setUp()
                try:
                    result = case.run_release(mode)
                    self.assertEqual(code, result.returncode, result.stdout + result.stderr)
                    self.assertEqual('FAILED', case.status()['state'])
                    self.assertEqual(code, case.status()['exit_code'])
                    if forbidden:
                        self.assertFalse(any(call.startswith(forbidden) for call in case.events()))
                    recorded = case.run_release('success', '--status', '13.0.1')
                    self.assertEqual(code, recorded.returncode)
                finally:
                    case.doCleanups()

    def test_smoke_failure_cannot_be_hidden_by_a_successful_outer_echo(self):
        result = subprocess.run(['bash', '-c', 'bash release.sh 13.0.1; echo wrapper-finished'],
                                cwd=self.root, env=dict(self.env, QYL_RELEASE_TEST_MODE='smoke-failure'),
                                text=True, capture_output=True, timeout=30)
        self.assertEqual(0, result.returncode)
        recorded = self.run_release('success', '--status', '13.0.1')
        self.assertEqual(134, recorded.returncode)
        self.assertEqual('FAILED', json.loads(recorded.stdout)['state'])
        self.assertFalse(any(call.startswith('gh release create') for call in self.events()))

    def test_status_distinguishes_running_and_interrupted_attempts(self):
        status_file = self.root / 'artifacts/release-13.0.1.status.json'
        status_file.parent.mkdir()
        running = dict(state='RUNNING', exit_code=None, pid=os.getpid())
        status_file.write_text(json.dumps(running))
        result = self.run_release('success', '--status', '13.0.1')
        self.assertEqual(4, result.returncode)
        self.assertEqual('RUNNING', json.loads(result.stdout)['state'])
        duplicate = self.run_release()
        self.assertEqual(1, duplicate.returncode)
        self.assertEqual(running, self.status())
        child = subprocess.Popen(['bash', '-c', 'exit 0'])
        child.wait()
        running['pid'] = child.pid
        status_file.write_text(json.dumps(running))
        result = self.run_release('success', '--status', '13.0.1')
        self.assertEqual(1, result.returncode)
        self.assertEqual('INTERRUPTED', json.loads(result.stdout)['state'])

    def test_runtime_index_timeout_does_not_publish_pointer_or_create_tag(self):
        result = self.run_release('runtime-timeout')
        self.assertEqual(3, result.returncode, result.stdout + result.stderr)
        self.assertEqual('PENDING', self.status()['state'])
        self.assertEqual('runtime package indexing', self.status()['phase'])
        self.assertFalse(any(call.startswith(('git tag', 'dotnet nuget push artifacts/nuget/qyl.13'))
                             for call in self.events()))

    def test_pending_verification_resumes_without_republishing_or_erasing_failure(self):
        first = self.run_release('verify-timeout')
        self.assertEqual(3, first.returncode, first.stdout + first.stderr)
        failed = self.status()
        first_log = pathlib.Path(failed['log']).read_text()
        self.assertEqual('PENDING', failed['state'])
        pushes = [call for call in self.events() if call.startswith('dotnet nuget push')]
        second = self.run_release('success', '--verify', '13.0.1')
        self.assertEqual(0, second.returncode, second.stdout + second.stderr)
        self.assertEqual('VERIFIED', self.status()['state'])
        self.assertNotEqual(failed['log'], self.status()['log'])
        self.assertEqual(first_log, pathlib.Path(failed['log']).read_text())
        self.assertEqual('PENDING', json.loads(pathlib.Path(failed['log']).with_name('status.json').read_text())['state'])
        self.assertEqual(pushes, [call for call in self.events() if call.startswith('dotnet nuget push')])

    def test_manually_created_release_cannot_start_verification_for_absent_packages(self):
        (self.root / 'tag').touch()
        (self.root / 'github').touch()
        result = self.run_release('success', '--verify', '13.0.1')
        self.assertEqual(3, result.returncode, result.stdout + result.stderr)
        self.assertFalse(any(call.startswith('build VerifyRelease') for call in self.events()))

    def test_transient_github_failure_is_retried_after_publication(self):
        result = self.run_release('github-transient')
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertTrue(any(call.startswith('gh release upload') for call in self.events()))
        self.assertEqual('VERIFIED', self.status()['state'])
        self.assertEqual('published', (self.root / 'github').read_text())

    def test_stale_cached_indexes_are_bypassed(self):
        result = self.run_release('stale-index')
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual('VERIFIED', self.status()['state'])

    def test_dry_run_is_not_a_verified_release(self):
        result = self.run_release('success', '13.0.1', '--dry-run')
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual('DRY_RUN', self.status()['state'])
        self.assertFalse(any(call.startswith(('dotnet nuget push', 'git tag', 'build VerifyRelease', 'refresh '))
                             for call in self.events()))


if __name__ == '__main__':
    unittest.main()
