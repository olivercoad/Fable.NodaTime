import { spawnSync } from 'node:child_process';
import { rmSync } from 'node:fs';
import { resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');
const noRestore = process.argv.includes('--no-restore');
const [project = 'tests/Serialization', jsProject = 'tests/SerializationJavaScript', outDir = 'build/serialization-js',
  entry = 'Serialization/Tests.js', fixtures = 'artifacts/serialization'] =
  process.argv.slice(2).filter(arg => arg !== '--no-restore');
rmSync(resolve(root, fixtures), { recursive: true, force: true });
function run(command, args) {
  const result = spawnSync(command, args, { cwd: root, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
}

// Exchange actual serializer output rather than maintaining hand-written fixtures.
run('dotnet', ['run', '--project', project, ...(noRestore ? ['--no-restore'] : [])]);
run('dotnet', ['fable', jsProject, '--outDir', outDir, ...(noRestore ? ['--noRestore', '--noCache'] : [])]);
run('node', ['--import', 'temporal-polyfill/global', resolve(root, outDir, entry)]);
run('dotnet', ['run', '--project', project, '--no-build', '--', '--verify-client']);
