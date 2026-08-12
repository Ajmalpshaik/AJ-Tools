#region Metadata
/*
 * Tool Name     : AJ Tools Connector (page)
 * File Name     : ConnectorPage.cs
 * Purpose       : The single HTML page the connector serves. Held as a string constant so it needs
 *                 no EmbeddedResource entry in the csproj and can never be missing at runtime.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-12
 * Last Updated  : 2026-08-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : none (plain string)
 *
 * Input         : The session token, injected at serve time.
 * Output        : Complete HTML document.
 *
 * Notes         :
 * - The page hardcodes NO tools. It asks /api/tools and builds its buttons from the answer, so a
 *   tool published to the website appears here with no change to this file and no new install. That
 *   property is the entire point of the connector - keep it.
 * - There is a real empty state. A fresh install legitimately has no tools yet, and a blank screen
 *   would read as broken.
 * - Refused tools are surfaced as a count, never as a button. A tool that failed its signature check
 *   must not be presentable to a person, because a button that exists is a button someone presses.
 * - HTML attributes and JS strings use single quotes so the C# verbatim string needs almost no
 *   doubled-quote escaping.
 *
 * Changelog     :
 * v1.0.0 (2026-08-12) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJToolsConnector
{
    internal static class ConnectorPage
    {
        private const string TokenPlaceholder = "__AJ_TOKEN__";

        public static string Build(string token)
        {
            return Template.Replace(TokenPlaceholder, token ?? string.Empty);
        }

        private const string Template = @"<!doctype html>
<html lang='en'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1'>
<title>AJ Tools Connector</title>
<style>
:root{
  --ground:#EEF2F7; --surface:#FFF; --surface2:#F7F9FC; --sunken:#E4EAF2;
  --line:#D3DCE8; --ink:#0E1620; --ink2:#46566B; --ink3:#7A8AA0;
  --accent:#1F76FF; --accent-deep:#0B4CC4; --wash:#E7F0FF;
  --ok:#0E9B57; --ok-wash:#E3F6EC; --stop:#C8342B; --stop-wash:#FBE7E5;
  --warn:#B4700A; --warn-wash:#FBF0DC;
  --ui:'Segoe UI Variable Text','Segoe UI',system-ui,sans-serif;
  --mono:'Cascadia Mono',Consolas,ui-monospace,monospace;
}
@media (prefers-color-scheme:dark){
  :root{
    --ground:#0A1017; --surface:#141C26; --surface2:#1A2430; --sunken:#0F1720;
    --line:#273343; --ink:#E6EDF6; --ink2:#9FB0C4; --ink3:#6C7E94;
    --accent:#4E93FF; --accent-deep:#7FB2FF; --wash:#14243D;
    --ok:#3FCB86; --ok-wash:#10281E; --stop:#F0685E; --stop-wash:#2E1614;
    --warn:#E0A53F; --warn-wash:#2A2113;
  }
}
*{box-sizing:border-box}
body{margin:0;background:var(--ground);color:var(--ink);font-family:var(--ui);font-size:14px;line-height:1.5}
button{font:inherit;color:inherit}
:focus-visible{outline:2px solid var(--accent);outline-offset:2px;border-radius:4px}

.top{display:flex;align-items:center;gap:16px;flex-wrap:wrap;padding:12px 20px;
     background:var(--surface);border-bottom:1px solid var(--line);position:sticky;top:0;z-index:9}
.mark{width:30px;height:30px;border-radius:8px;background:linear-gradient(150deg,var(--accent),var(--accent-deep));
      color:#fff;font-size:13px;font-weight:700;display:grid;place-items:center}
.brand{display:flex;align-items:center;gap:10px}
.bname{font-size:15px;font-weight:700}
.bsub{display:block;font-size:10.5px;letter-spacing:.07em;text-transform:uppercase;color:var(--ink3)}
.ctx{display:flex;align-items:center;gap:14px;flex-wrap:wrap;margin-left:auto}
.pill{display:inline-flex;align-items:center;gap:7px;padding:5px 11px;border-radius:999px;
      font-size:12px;font-weight:600;background:var(--ok-wash);color:var(--ok)}
.pill.bad{background:var(--stop-wash);color:var(--stop)}
.dot{width:7px;height:7px;border-radius:50%;background:currentColor}
.fact{display:flex;flex-direction:column;line-height:1.25}
.fk{font-size:10px;letter-spacing:.07em;text-transform:uppercase;color:var(--ink3)}
.fv{font-size:12.5px;font-weight:600;font-family:var(--mono)}

.main{padding:18px 22px 26px;max-width:1100px;margin:0 auto}
.bar{display:flex;align-items:center;gap:10px;margin-bottom:16px;flex-wrap:wrap}
.seek{display:flex;align-items:center;flex:1;min-width:200px;padding:0 14px;background:var(--surface);
      border:1px solid var(--line);border-radius:11px}
.seek:focus-within{border-color:var(--accent)}
.seek input{flex:1;border:0;background:transparent;color:var(--ink);font:inherit;padding:11px 0}
.seek input:focus{outline:none}
.mini{padding:9px 14px;border:1px solid var(--line);border-radius:9px;background:var(--surface);
      color:var(--ink2);font-size:12.5px;cursor:pointer}
.mini:hover{border-color:var(--accent);color:var(--accent)}

.note{padding:10px 14px;border-radius:9px;font-size:12.5px;margin-bottom:16px}
.note.warn{background:var(--warn-wash);color:var(--warn)}
.note.info{background:var(--wash);color:var(--accent-deep)}

.gname{font-size:12px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;color:var(--ink2);margin:0 0 10px}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(210px,1fr));gap:10px;margin-bottom:24px}
.tool{display:flex;flex-direction:column;gap:6px;padding:12px 13px;text-align:left;background:var(--surface);
      border:1px solid var(--line);border-radius:11px;cursor:pointer;transition:transform .13s,border-color .13s}
.tool:hover:not(:disabled){transform:translateY(-2px);border-color:var(--accent)}
.tool:disabled{opacity:.55;cursor:wait}
.tname{font-size:13.5px;font-weight:600}
.tdesc{font-size:11.5px;color:var(--ink3)}

.empty{padding:44px 20px;text-align:center;background:var(--surface);border:1px dashed var(--line);border-radius:14px}
.empty h2{margin:0 0 8px;font-size:17px}
.empty p{margin:0 auto;max-width:44ch;color:var(--ink2);font-size:13px}

.console{position:sticky;bottom:0;background:var(--surface);border-top:1px solid var(--line)}
.cbar{display:flex;align-items:center;gap:12px;padding:8px 20px;border-bottom:1px solid var(--line)}
.ctitle{font-size:11px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;color:var(--ink2)}
.cnote{font-size:11.5px;color:var(--ink3);margin-right:auto}
.log{margin:0;padding:10px 20px 14px;max-height:170px;overflow-y:auto;background:var(--sunken);
     font-family:var(--mono);font-size:12px;line-height:1.65}
.line{display:flex;gap:12px;padding:2px 0}
.lt{color:var(--ink3);flex:none}
.ln{font-weight:700}
.ok{color:var(--ok)} .bad{color:var(--stop)} .run{color:var(--accent)}
.lb{color:var(--ink2);white-space:pre-wrap}
.idle{color:var(--ink3);font-style:italic}

.credit{padding:14px 20px 20px;text-align:center;font-size:11.5px;color:var(--ink3)}
.credit strong{color:var(--ink2)}
@media (prefers-reduced-motion:reduce){*{transition:none!important}}
</style>
</head>
<body>

<header class='top'>
  <div class='brand'>
    <div class='mark'>AJ</div>
    <div><div class='bname'>AJ Tools</div><span class='bsub'>Connector</span></div>
  </div>
  <div class='ctx'>
    <span class='pill' id='pill'><span class='dot'></span><span id='pillText'>Checking...</span></span>
    <div class='fact'><span class='fk'>Revit</span><span class='fv' id='ver'>-</span></div>
    <div class='fact'><span class='fk'>Model</span><span class='fv' id='model'>-</span></div>
    <div class='fact'><span class='fk'>Active view</span><span class='fv' id='view'>-</span></div>
  </div>
</header>

<main class='main'>
  <div class='bar'>
    <div class='seek'><input id='seek' type='search' placeholder='Search a tool' aria-label='Search tools'></div>
    <button class='mini' id='refresh' type='button'>Check for new tools</button>
  </div>

  <div id='notice'></div>
  <div id='groups'></div>
</main>

<section class='console'>
  <div class='cbar'>
    <span class='ctitle'>Output</span>
    <span class='cnote'>Results appear here, not as a popup inside Revit</span>
    <button class='mini' id='clear' type='button'>Clear</button>
  </div>
  <div class='log' id='log' role='log' aria-live='polite'></div>
</section>

<footer class='credit'>Created &amp; All Rights Reserved @ <strong>Ajmal P.S.</strong></footer>

<script>
const TOKEN = '__AJ_TOKEN__';
const logEl = document.getElementById('log');
const groupsEl = document.getElementById('groups');
const noticeEl = document.getElementById('notice');
const seekEl = document.getElementById('seek');
let TOOLS = [];

function api(path, body){
  return fetch(path, {
    method: body ? 'POST' : 'GET',
    headers: {'X-AJ-Token': TOKEN, 'Content-Type': 'application/json'},
    body: body ? JSON.stringify(body) : undefined
  }).then(r => r.json());
}

function now(){
  const d = new Date();
  return [d.getHours(), d.getMinutes(), d.getSeconds()].map(v => String(v).padStart(2,'0')).join(':');
}

function write(name, kind, body){
  const idle = logEl.querySelector('.idle');
  if (idle) idle.remove();
  const mark = kind === 'run' ? '. running' : kind === 'ok' ? 'OK' : 'X';
  const row = document.createElement('div');
  row.className = 'line';
  row.innerHTML = '<span class=lt>' + now() + '</span><span><span class=\'ln ' + kind + '\'>'
                + mark + ' ' + name + '</span>' + (body ? '<div class=lb></div>' : '') + '</span>';
  if (body) row.querySelector('.lb').textContent = body;
  logEl.appendChild(row);
  logEl.scrollTop = logEl.scrollHeight;
  return row;
}

function setStatus(good, text){
  const pill = document.getElementById('pill');
  pill.className = good ? 'pill' : 'pill bad';
  document.getElementById('pillText').textContent = text;
}

function refreshContext(){
  return api('/api/context', {}).then(r => {
    if (!r.success){ setStatus(false, 'No model open'); return; }
    const bits = (r.message || '').split('|');
    document.getElementById('ver').textContent = bits[0] || '-';
    document.getElementById('model').textContent = bits[1] || '-';
    document.getElementById('view').textContent = bits[2] || '-';
    setStatus(true, 'Connected');
  }).catch(() => setStatus(false, 'Revit not reachable'));
}

function showNotice(html, kind){
  noticeEl.innerHTML = html ? '<div class=\'note ' + kind + '\'>' + html + '</div>' : '';
}

function render(){
  const q = seekEl.value.trim().toLowerCase();
  const hits = TOOLS.filter(t => !q || t.name.toLowerCase().includes(q)
                              || (t.description||'').toLowerCase().includes(q));
  groupsEl.innerHTML = '';

  if (!TOOLS.length){
    groupsEl.innerHTML = '<div class=empty><h2>No tools published yet</h2>'
      + '<p>This connector is installed and talking to Revit, but no signed tools have been '
      + 'published to it yet. When one is added, press Check for new tools and it will appear here.</p></div>';
    return;
  }

  const panels = [...new Set(hits.map(t => t.panel))];
  for (const p of panels){
    const h = document.createElement('h2');
    h.className = 'gname';
    h.textContent = p;
    groupsEl.appendChild(h);

    const grid = document.createElement('div');
    grid.className = 'grid';
    for (const t of hits.filter(x => x.panel === p)){
      const b = document.createElement('button');
      b.type = 'button';
      b.className = 'tool';
      const n = document.createElement('span'); n.className = 'tname'; n.textContent = t.name;
      const d = document.createElement('span'); d.className = 'tdesc'; d.textContent = t.description || '';
      b.appendChild(n); b.appendChild(d);
      b.addEventListener('click', () => run(t, b));
      grid.appendChild(b);
    }
    groupsEl.appendChild(grid);
  }
}

function loadTools(){
  return api('/api/tools').then(r => { TOOLS = r.tools || []; render(); });
}

function run(tool, btn){
  if (btn.disabled) return;
  btn.disabled = true;
  const pending = write(tool.name, 'run', '');

  api('/api/run', {toolId: tool.id}).then(r => {
    pending.remove();
    write(tool.name, r.success ? 'ok' : 'bad', r.message || '');
    refreshContext();
  }).catch(() => {
    pending.remove();
    write(tool.name, 'bad', 'Lost contact with Revit. Is it still open?');
  }).then(() => { btn.disabled = false; });
}

document.getElementById('refresh').addEventListener('click', () => {
  write('Check for new tools', 'run', '');
  api('/api/refresh', {}).then(r => {
    write('Check for new tools', r.success ? 'ok' : 'bad', r.message || '');
    if (r.refused > 0){
      showNotice('<b>' + r.refused + ' tool(s) were refused.</b> They were not signed by Ajmal, or were '
               + 'changed after publishing. They are not shown and cannot be run.', 'warn');
    } else {
      showNotice('', 'info');
    }
    loadTools();
  }).catch(() => write('Check for new tools', 'bad', 'Could not reach the connector.'));
});

seekEl.addEventListener('input', render);
document.getElementById('clear').addEventListener('click', () => {
  logEl.innerHTML = '<div class=\'line idle\'>Waiting - click a tool above.</div>';
});

logEl.innerHTML = '<div class=\'line idle\'>Waiting - click a tool above.</div>';
loadTools();
refreshContext();
setInterval(refreshContext, 15000);
</script>
</body>
</html>";
    }
}
