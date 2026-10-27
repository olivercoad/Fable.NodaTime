import { spawnSync } from 'node:child_process';
import { mkdirSync, rmSync } from 'node:fs';
import { resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');
const feed = resolve(root, 'artifacts/packages');
const env = { ...process.env, NUGET_PACKAGES: resolve(root, 'artifacts/package-cache') };
rmSync(feed, { recursive: true, force: true });
mkdirSync(feed, { recursive: true });
rmSync(env.NUGET_PACKAGES, { recursive: true, force: true });
rmSync(resolve(root, 'build/package-js'), { recursive: true, force: true });
function run(command, args) {
  const result = spawnSync(command, args, { cwd: root, env, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
}
run('dotnet', ['pack', 'src/Fable.NodaTime', '-c', 'Release', '-o', feed]);
run('dotnet', ['restore', 'tests/PackageSmoke', '--source', feed, '--source', 'https://api.nuget.org/v3/index.json', '--force']);
run('dotnet', ['run', '--project', 'tests/PackageSmoke', '--no-restore']);
run('dotnet', ['fable', 'tests/PackageSmoke', '--outDir', 'build/package-js', '--noRestore', '--noCache']);
run('node', ['--import', 'temporal-polyfill/global', 'build/package-js/Fable.NodaTime.Tests/Tests.js']);
