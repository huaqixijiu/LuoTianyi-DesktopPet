import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import fs from 'node:fs';
import path from 'node:path';

// Read-only protocol check. No thread, model turn, credentials or MCP connection.
const child = spawn(process.argv[2] ?? 'codex', ['app-server', '--stdio'], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });
let done = false;
const timeout = setTimeout(() => finish(new Error('skills/list timed out')), 30000);
function finish(error, value) {
  if (done) return;
  done = true; clearTimeout(timeout); child.kill();
  if (error) { console.error(error.message); process.exitCode = 1; }
  else {
    fs.mkdirSync('artifacts/ui-review', {recursive:true});
    fs.writeFileSync('artifacts/ui-review/skill-discovery.json', JSON.stringify(value, null, 2));
    console.log(JSON.stringify(value));
  }
}
function send(message) { child.stdin.write(JSON.stringify(message) + '\n'); }
child.on('error', e => finish(e));
child.on('exit', () => { if (!done) finish(new Error('App server exited before discovery')); });
child.stderr.on('data', () => {}); // Do not persist unrelated local server diagnostics.
createInterface({input:child.stdout}).on('line', line => {
  try {
    const message = JSON.parse(line);
    if (message.id === 1) {
      if (message.error) return finish(new Error(message.error.message));
      send({method:'initialized'});
      send({id:2,method:'skills/list',params:{cwds:[process.cwd()],forceReload:true}});
    }
    if (message.id === 2) {
      if (message.error) return finish(new Error(message.error.message));
      const skills = (message.result?.data ?? []).flatMap(entry => entry.skills ?? []);
      const expected = path.resolve('.agents/skills/luotianyi-ui-design/SKILL.md').toLowerCase();
      const match = skills.find(skill => skill.name === 'luotianyi-ui-design' && path.resolve(skill.path).toLowerCase() === expected);
      if (!match || match.enabled === false) return finish(new Error('Project skill not discovered/enabled'));
      finish(null, {name:match.name,scope:match.scope,enabled:match.enabled,path:match.path,verifiedUtc:new Date().toISOString()});
    }
  } catch(e) { finish(e); }
});
send({id:1,method:'initialize',params:{clientInfo:{name:'luotianyi-ui-skill-check',version:'1.0.0'}}});
