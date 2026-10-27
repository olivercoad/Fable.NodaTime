// Check the actual NuGet archives before running package consumers or publishing.
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { strFromU8, unzipSync } from 'fflate';
import { XMLParser } from 'fast-xml-parser';
import { packages, root } from './package-version.mjs';

const [feed, version] = process.argv.slice(2);
assert(feed && version, 'Usage: node scripts/check-packages.mjs <feed> <version>');
const parser = new XMLParser({
  ignoreAttributes: false,
  removeNSPrefix: true,
  parseTagValue: false,
  isArray: tag => tag === 'group' || tag === 'dependency',
});
const guides = readdirSync(resolve(root, 'docs')).filter(file => file.endsWith('.md'));

for (const name of packages) {
  const archive = unzipSync(readFileSync(resolve(feed, `${name}.${version}.nupkg`)));
  const files = new Set(Object.keys(archive));
  assert(files.has(`${name}.nuspec`), `${name}: missing nuspec`);
  const metadata = parser.parse(strFromU8(archive[`${name}.nuspec`]), true).package.metadata;
  assert.equal(metadata.id, name);
  assert.equal(metadata.version, version, `${name}: version`);
  assert.equal(metadata.authors, 'Oliver Coad', `${name}: authors`);
  assert.equal(metadata.license['#text'], 'MIT', `${name}: license`);
  assert.equal(metadata.license['@_type'], 'expression', `${name}: license type`);
  assert(metadata.releaseNotes?.trim(), `${name}: missing release notes`);
  assert.equal(metadata.repository['@_url'], 'https://github.com/olivercoad/Fable.NodaTime');
  const tags = new Set(metadata.tags.split(/\s+/));
  assert(tags.has('fable-dotnet') && tags.has('fable-javascript'), `${name}: target tags`);
  assert(files.has(metadata.readme), `${name}: missing readme`);
  for (const file of ['LICENSE', 'CHANGELOG.md', `lib/net10.0/${name}.dll`,
    `lib/net10.0/${name}.xml`, `fable/${name}.fsproj`, ...guides.map(file => `docs/${file}`)]) {
    assert(files.has(file), `${name}: missing ${file}`);
  }
  assert([...files].some(file => file.startsWith('fable/') && file.endsWith('.fs')),
    `${name}: missing Fable sources`);
  const dependencies = [
    ...(metadata.dependencies?.dependency ?? []),
    ...(metadata.dependencies?.group ?? []).flatMap(group => group.dependency ?? []),
  ];
  for (const dependency of dependencies) {
    assert(!['Fable.Package.SDK', 'Ionide.KeepAChangelog.Tasks'].includes(dependency['@_id']),
      `${name}: build dependency ${dependency['@_id']} must remain private`);
  }
  if (name !== 'Fable.NodaTime') {
    const core = dependencies.filter(dependency => dependency['@_id'] === 'Fable.NodaTime');
    assert(core.length > 0, `${name}: missing core dependency`);
    for (const dependency of core) assert.equal(dependency['@_version'], version);
  }
  console.log(`Validated ${name} ${version}: metadata, dependencies, documentation and Fable sources`);
}
