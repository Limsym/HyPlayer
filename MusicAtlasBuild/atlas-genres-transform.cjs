const fs=require('fs');
module.exports=function(html){
 const catalog=JSON.parse(fs.readFileSync('atlas-genre-catalog.json','utf8'));
 const roots=[...new Set(catalog.map(s=>s.split('-')[0]))].sort((a,b)=>a.localeCompare(b,'zh-CN'));
 const colors=['#9e546a','#587a97','#b57663','#c19359','#658d7b','#7c6caa','#538e9f','#9d7361'];
 const defs=roots.map((name,i)=>({id:'nc:'+name,dim:'genre',name,color:colors[i%colors.length],defaultHome:true}));
 html=html.replace(/const dimensions=.*?;\r?\n/,"const dimensions=[{id:'genre',name:'网易云曲风',color:'#5888a3'},{id:'manual',name:'手动类别',color:'#a77cba'}];\n");
 const start=html.indexOf('const definitions=['),end=html.indexOf('const palette=',start);
 html=html.slice(0,start)+'const definitions='+JSON.stringify(defs)+';\n'+html.slice(end);
 const a=html.indexOf('function autoTags('),b=html.indexOf('function tagsOf(',a);
 html=html.slice(0,a)+`function autoTags(t){return [...new Set((t.styles||[]).map(s=>'nc:'+s.split('-')[0]))]}
function ensureGenres(){for(const t of tracks)for(const name of (t.styles||[]).map(s=>s.split('-')[0])){const id='nc:'+name;if(!categories.some(c=>c.id===id)){categories.push({id,dim:'genre',name,color:palette[categories.length%palette.length],defaultHome:true});home.add(id)}}}
function genreDetail(t){const raw=(t.styles||[]).join('、');return '网易云细分曲风：'+(raw||({known:'未提供',pending:'正在查询',failed:'查询失败，可返回后重试'}[t.genreState]||'尚未查询'))+(raw&&t.genreState==='failed'?'（更新失败，显示缓存）':'')}
`+html.slice(b);
 html=html.replace(/function save\(\)\{.*?\r?\n/,`function save(){try{localStorage.setItem(STORE+'session',JSON.stringify({schema:2,categories,home:[...home],overrides}))}catch{msg('分类保存失败')}}\n`);
 html=html.replace(/function restore\(\)\{.*?\r?\n/,`function restore(){try{const raw=localStorage.getItem(STORE+'session'),s=JSON.parse(raw||'null');if(!s||!Array.isArray(s.categories))return;overrides=s.overrides||{};if(s.schema===2){categories=s.categories;home=new Set(s.home||[])}else{if(!localStorage.getItem(STORE+'phase0-backup'))localStorage.setItem(STORE+'phase0-backup',raw);const used=new Set(Object.values(overrides).flat());const kept=s.categories.filter(c=>used.has(c.id)).map(c=>({...c,dim:'manual'}));categories=[...definitions.map(x=>({...x})),...kept];home=new Set([...definitions.map(c=>c.id),...kept.map(c=>c.id)]);save()}}catch{msg('读取分类设置失败，原设置未清除')}}\n`);
 html=html.replace("album:String(t.album)}", "album:String(t.album),styles:Array.isArray(t.styles)?t.styles.filter(s=>typeof s==='string'):[],genreState:t.genreState||'pending'}");
 html=html.replace("playlistId=m.playlistId;tracks=", "const samePlaylist=playlistId===m.playlistId;playlistId=m.playlistId;tracks=");
 html=html.replace("sourceMode='hyplayer';selected='__all__';$('search').value='';", "sourceMode='hyplayer';if(!samePlaylist){selected='__all__';$('search').value=''}");
 html=html.replace("$('playlistName').textContent=m.name;render();", "$('playlistName').textContent=m.name;ensureGenres();save();render();");
 html=html.replace("}else if(m.type==='accepted')", `}else if(m.type==='genres'&&m.playlistId===playlistId){
 const changes=new Map(m.tracks.map(t=>[String(t.id),t]));for(const t of tracks){const u=changes.get(t.id);if(u){t.styles=Array.isArray(u.styles)?u.styles.filter(s=>typeof s==='string'):[];t.genreState=u.genreState}}
 ensureGenres();save();render();if($('songDetails').open&&editId)editSong(editId);
 }else if(m.type==='accepted')`);
 html=html.replace("$('sourceBadge').textContent='当前歌单 · 标签根据歌名、歌手和专辑初判，可人工修正'", "$('sourceBadge').textContent='网易云曲风 · '+tracks.filter(t=>t.styles?.length).length+'/'+tracks.length+' 首有曲风 · 手动修改优先 · 多曲风歌曲可重复计入'");
 html=html.replace("const ed=$('tagEditor');ed.replaceChildren();", "const ed=$('tagEditor');ed.replaceChildren();const raw=document.createElement('p');raw.className='hint';raw.id='rawGenres';raw.textContent=genreDetail(t);ed.append(raw);");
 html=html.replace("'（自动建议）'", "'（网易云曲风）'");
 html=html.replaceAll('人工修改优先于自动推断','人工修改优先于网易云曲风');
 html=html.replace('categories.filter(x=>x.dim===d.id)){const b=', 'categories.filter(x=>x.dim===d.id).sort((a,b)=>(countsAll[b.id]||0)-(countsAll[a.id]||0)||a.name.localeCompare(b.name,\'zh-CN\'))){const b=');
 html=html.replace(/\$\('recommendBtn'\)\.onclick=.*?\r?\n/,`$('recommendBtn').title='按当前歌单曲目数选择前 10 个曲风，保留已选的手动类别';$('recommendBtn').onclick=()=>{const n=counts();const ranked=categories.filter(c=>c.dim==='genre'&&(n[c.id]||0)>0).sort((a,b)=>n[b.id]-n[a.id]||a.name.localeCompare(b.name,'zh-CN')).slice(0,10);const manual=categories.filter(c=>c.dim==='manual'&&home.has(c.id));home=new Set([...ranked,...manual].map(c=>c.id));save();render();msg('已选择曲目最多的 '+ranked.length+' 个曲风，保留手动类别选择')};\n`);
 html=html.replace('选择要展示的分类 · 数字为当前歌单曲数','选择要展示的分类 · 按歌曲数从多到少排列');
 html=html.replace("home=new Set(definitions.filter(x=>x.defaultHome).map(x=>x.id));save();render()", "home=new Set(definitions.filter(x=>x.defaultHome).map(x=>x.id));ensureGenres();save();render()");
 html=html.replace('重置此曲为自动标签','恢复网易云曲风');
 html=html.replace('歌曲标签 · 点击增删以校正','地图分类 · 点击增删以校正');
 // New custom categories must use one of the actual editor dimensions.
 html=html.replace('<option value="mood">气质</option>','<option value="manual">手动类别</option>').replace('<option value="source">来源</option>','');
 return html;
};
