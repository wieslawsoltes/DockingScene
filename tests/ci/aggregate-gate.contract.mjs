import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

const source = readFileSync(new URL('../../.github/workflows/uno.yml', import.meta.url), 'utf8');
const aggregate = source.split('  validated:\n')[1]?.split('\n  deploy:')[0];
assert(aggregate, 'Required aggregate job is missing');
assert.match(aggregate, /^    name: build$/m, 'Required check name must remain build');
assert.match(aggregate, /^    if: \$\{\{ !cancelled\(\) \}\}$/m,
  'Failed dependencies must still run the gate without resisting workflow cancellation');
assert.match(aggregate, /^    needs: \[build, browser-tests\]$/m);
assert.match(aggregate, /BUILD_RESULT: \$\{\{ needs\.build\.result \}\}/);
assert.match(aggregate, /BROWSER_RESULT: \$\{\{ needs\.browser-tests\.result \}\}/);
const lines = aggregate.split('        run: |\n')[1];
assert(lines, 'Aggregate must execute its result checks');
const shell = lines.split('\n').filter(line => line.startsWith('          ')).map(line => line.slice(10)).join('\n');
assert(shell.trim(), 'Empty aggregate validation is not allowed');
let combinations = 0;
for (const build of ['success', 'failure', 'cancelled', 'skipped']) {
  for (const browser of ['success', 'failure', 'cancelled', 'skipped']) {
    const execution = spawnSync('bash', ['-e', '-o', 'pipefail', '-c', shell], {
      env: { ...process.env, BUILD_RESULT: build, BROWSER_RESULT: browser }, encoding: 'utf8', timeout: 5000,
    });
    assert(!execution.error, String(execution.error));
    assert.equal(execution.status === 0, build === 'success' && browser === 'success',
      `Incorrect certification for build=${build}, browser=${browser}`);
    combinations++;
  }
}
assert.match(source, /^    needs: \[build, browser-tests, validated\]$/m,
  'Deployment must retain every validation prerequisite');
for (const script of ['run.mjs', 'tab-layout.mjs', 'host-review.mjs', 'focus-investigation.mjs', 'caption-policies-runner.mjs']) {
  assert.equal(source.split('script: ' + script + '\n').length - 1, 2,
    `Pre-deployment and live matrices must both retain ${script}`);
}
console.log(`PASS aggregate gate: ${combinations} actual shell result combinations; required name, both matrices and deployment dependencies retained`);
