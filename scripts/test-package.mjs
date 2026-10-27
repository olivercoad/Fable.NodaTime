import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { packageVersion } from './package-version.mjs';
import { resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');
const feed = resolve(root, 'artifacts/packages');
const env = { ...process.env, NUGET_PACKAGES: resolve(root, 'artifacts/package-cache') };
rmSync(feed, { recursive: true, force: true });
mkdirSync(feed, { recursive: true });
rmSync(env.NUGET_PACKAGES, { recursive: true, force: true });
rmSync(resolve(root, 'build/package-js'), { recursive: true, force: true });
rmSync(resolve(root, 'build/serialization-package-js'), { recursive: true, force: true });
rmSync(resolve(root, 'build/typed-json-package-js'), { recursive: true, force: true });
function run(command, args) {
  const result = spawnSync(command, args, { cwd: root, env, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
}
run('dotnet', ['pack', 'src/Fable.NodaTime', '-c', 'Release', '-o', feed]);
run('dotnet', ['pack', 'src/Fable.NodaTime.Thoth', '-c', 'Release', '-o', feed]);
run('dotnet', ['pack', 'src/Fable.NodaTime.TypedJson', '-c', 'Release', '-o', feed]);
const version = packageVersion();
env.PackageUnderTestVersion = version;
run(process.execPath, ['scripts/check-packages.mjs', feed, version]);
// Pin our package IDs to the local feed even after they are published on NuGet.
const config = resolve(root, 'artifacts/package-consumer.NuGet.Config');
writeFileSync(config, `<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="${feed}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="Fable.NodaTime" /><package pattern="Fable.NodaTime.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
`);
// The integration supplies shared codecs; applications choose the runtime backend.
const thothDependencies = JSON.parse(readFileSync(resolve(root,
  'src/Fable.NodaTime.Thoth/bin/Release/net10.0/Fable.NodaTime.Thoth.deps.json'), 'utf8')).libraries;
const forbidden = new Set(['Thoth.Json', 'Thoth.Json.Net', 'Newtonsoft.Json',
  'Thoth.Json.JavaScript', 'Thoth.Json.Python', 'Thoth.Json.System.Text.Json', 'Thoth.Json.Newtonsoft']);
for (const library of Object.keys(thothDependencies)) {
  if (forbidden.has(library.split('/')[0])) {
    throw new Error(`The Thoth integration must not depend on ${library}`);
  }
}
const typedJsonDependencies = JSON.parse(readFileSync(resolve(root,
  'src/Fable.NodaTime.TypedJson/bin/Release/net10.0/Fable.NodaTime.TypedJson.deps.json'), 'utf8')).libraries;
for (const library of Object.keys(typedJsonDependencies)) {
  const name = library.split('/')[0];
  if (name.startsWith('Fable.TypedJson.') || name.startsWith('Thoth.') || name === 'Newtonsoft.Json') {
    throw new Error(`The TypedJson integration must not depend on ${library}`);
  }
}
run('dotnet', ['restore', 'tests/PackageSmoke', '--configfile', config, '--force']);
run('dotnet', ['run', '--project', 'tests/PackageSmoke', '--no-restore']);
run('dotnet', ['fable', 'tests/PackageSmoke', '--outDir', 'build/package-js', '--noRestore', '--noCache']);
run('node', ['--import', 'temporal-polyfill/global', 'build/package-js/Fable.NodaTime.Tests/Tests.js']);
run('dotnet', ['restore', 'tests/SerializationPackageSmoke', '--configfile', config, '--force']);
run('dotnet', ['restore', 'tests/SerializationJavaScriptPackageSmoke', '--configfile', config, '--force']);
run('node', ['scripts/test-serialization.mjs', 'tests/SerializationPackageSmoke', 'tests/SerializationJavaScriptPackageSmoke', 'build/serialization-package-js', '--no-restore']);
run('dotnet', ['restore', 'tests/TypedJsonPackageSmoke', '--configfile', config, '--force']);
run('dotnet', ['restore', 'tests/TypedJsonJavaScriptPackageSmoke', '--configfile', config, '--force']);
run('node', ['scripts/test-serialization.mjs', 'tests/TypedJsonPackageSmoke', 'tests/TypedJsonJavaScriptPackageSmoke', 'build/typed-json-package-js', 'TypedJson/Tests.js', 'artifacts/typed-json', '--no-restore']);
