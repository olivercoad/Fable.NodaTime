import { spawnSync } from 'node:child_process';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const root = resolve(import.meta.dirname, '..');
export const packages = ['Fable.NodaTime', 'Fable.NodaTime.Thoth', 'Fable.NodaTime.TypedJson'];

export function packageVersion() {
  const versions = packages.map(name => {
    const result = spawnSync('dotnet', ['msbuild', `src/${name}/${name}.fsproj`, '-nologo',
      '-target:SetVersionFromChangelog', '-getProperty:PackageVersion'],
    { cwd: root, encoding: 'utf8', timeout: 30000 });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(result.stderr || result.stdout);
    const version = result.stdout.trim();
    if (!/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/.test(version)) {
      throw new Error(`Invalid changelog package version for ${name}: ${version}`);
    }
    return version;
  });
  if (new Set(versions).size !== 1) throw new Error(`Package versions must agree: ${versions}`);
  return versions[0];
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const version = packageVersion();
  if (process.argv.includes('--check-tag') && process.env.RELEASE_TAG !== `v${version}`) {
    throw new Error(`Release tag must be v${version}; received ${process.env.RELEASE_TAG}`);
  }
  console.log(version);
}
