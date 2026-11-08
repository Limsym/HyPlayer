process.chdir(__dirname);
const fs = require('node:fs');
const path = require('node:path');
let html = fs.readFileSync('music_atlas_v3.html', 'utf8');
function replace(from, to) { if (!html.includes(from)) throw Error('Missing template: '+from.slice(0,70)); html = html.replace(from,to); }
html = html.replace(/<script>const SEED_TRACKS=.*?<\/script>/s, '<script>const SEED_TRACKS=[];</script>');
replace('const STORE=\'music-atlas-v2:\';', "const STORE='hyplayer-music-atlas-v1:';let playlistId='';let playbackPending=false;");
replace("let selected='g-rock'", "let selected='__all__'");
replace('music-atlas-v3:preferences', 'hyplayer-atlas:preferences');
replace('music-atlas-v3:preferences', 'hyplayer-atlas:preferences');
replace('音乐地形图 003', 'HyPlayer 音乐地图');
replace('MUSIC ATLAS / 003', 'HYPLAYER / MUSIC ATLAS');
replace('<style>', '<style>\n[hidden]{display:none!important}');
replace('<h1>声音的地形图</h1>', '<h1 id="playlistName">正在等待歌单…</h1>');
replace('点击方块展开', '选择风格，点击播放');
replace('showOther:true,autoPlay:false', 'showOther:true,autoPlay:true');
replace("'林肯公园','许巍'", "'林肯公园','伍佰','崔健','大黒摩季','大黑摩季','许巍'");
replace('随机播放</button>', '播放此类</button>');
replace('>全库</button>', '>全部歌曲</button>');
replace("?'全库歌曲':", "?'全部歌曲':");
replace('数字为库中歌曲数', '数字为当前歌单歌曲数');
replace("const withSource=sub.filter(source);const pool=withSource.length?withSource:sub;play(pool[Math.floor(Math.random()*pool.length)])", 'play(sub[0])');
replace('点击方块时随机播放', '点击方块即播放此类');
// Keep stable provider IDs and configuration, never restore stale playlist tracks.
html = html.replace(/function save\(\)\{.*?\r?\n/, `function save(){try{localStorage.setItem(STORE+'session',JSON.stringify({categories,home:[...home],overrides}))}catch{msg('分类保存失败，请导出备份')}}\n`);
html = html.replace(/function restore\(\)\{.*?\r?\n/, `function restore(){try{const s=JSON.parse(localStorage.getItem(STORE+'session')||'null');if(s&&Array.isArray(s.categories)){categories=s.categories;home=new Set(s.home);overrides=s.overrides||{}}}catch{}}\n`);
html = html.replace(/function play\(t\)\{.*?\r?\n/, `function play(t){
if(playbackPending)return;
if(!window.chrome?.webview||!playlistId){msg('请在 HyPlayer 歌单页打开音乐地图');return}
const queue=tracks.filter(x=>selected==='__all__'||(selected==='__unclassified__'&&!tagsOf(x).length)||tagsOf(x).includes(selected));
if(!queue.some(x=>x.id===t.id))return;
playbackPending=true;setTimeout(()=>playbackPending=false,15000);
window.chrome.webview.postMessage({type:'play',playlistId,ids:queue.map(x=>x.id),startId:t.id});
msg('正在交给 HyPlayer 播放…');
}\n`);
replace("$('sourceBadge').textContent=sourceMode==='historical'?'1000 首历史样本 · 自动标签待校验':'已导入歌曲 · 标签基于元数据初判'", "$('sourceBadge').textContent='当前歌单 · 标签根据歌名、歌手和专辑初判，可人工修正'");
replace('主题与地图设置保存在当前浏览器。固定主题色用于按钮与选中状态；地图各分类保留独立配色。音频按文件名匹配歌名，刷新后需要重新关联。没有音频的歌曲仍可展开和编辑分类。', '分类修正按歌曲 ID 保存在本机，跨歌单复用。主题和主页分类也会保留。播放与队列由 HyPlayer 管理。');
replace('人工修改优先于自动推断，并保存在此浏览器。可随时导出备份，导入文件后继续编辑。', '人工修改优先于自动推断，按歌曲 ID 保存在本机。');
html = html.replace(/\$\('resetBtn'\)\.onclick=.*?\r?\n/, `$('resetBtn').onclick=()=>{if(!confirm('清除本机音乐地图的人工分类与主页选择？'))return;overrides={};categories=definitions.map(x=>({...x}));home=new Set(definitions.filter(x=>x.defaultHome).map(x=>x.id));save();render()};\n`);
replace("restore();render();choose(selected);", `
for(const id of ['importData','importAudio'])$(id).closest('label').hidden=true;
$('audio').hidden=true;document.querySelector('.player').hidden=true;
// Scroll beneath the native player, with its 130-DIP clearance plus 24px breathing room.
document.querySelector('.page').style.paddingBottom='154px';
if(window.chrome?.webview){
window.chrome.webview.addEventListener('message',event=>{
 const m=event.data;
 if(m.type==='playlist'){
  playlistId=m.playlistId;tracks=m.tracks.map(t=>({id:String(t.id),title:String(t.title),artist:String(t.artist),album:String(t.album)}));
  sourceMode='hyplayer';selected='__all__';$('search').value='';
  $('playlistName').textContent=m.name;render();
 }else if(m.type==='accepted'){playbackPending=false;msg('已交给 HyPlayer：'+m.title)}
 else if(m.type==='error'){playbackPending=false;msg('播放失败：'+m.message)}
});
window.chrome.webview.postMessage({type:'ready'});
}else $('playlistName').textContent='请从 HyPlayer 的歌单页打开';
restore();render();choose(selected);`);
replace('<head><meta charset="utf-8"/>', '<head><meta charset="utf-8"/><meta http-equiv="Content-Security-Policy" content="default-src \'none\'; script-src \'unsafe-inline\'; style-src \'unsafe-inline\'; img-src data:; base-uri \'none\'; form-action \'none\'"/>');
html = require('./atlas-genres-transform.cjs')(html);
html = require('./atlas-dimensions-transform.cjs')(html);
const out = '../HyPlayer/Assets/MusicAtlas/index.html';
fs.mkdirSync(path.dirname(out), {recursive:true});
fs.writeFileSync(out,html);
console.log(out);
