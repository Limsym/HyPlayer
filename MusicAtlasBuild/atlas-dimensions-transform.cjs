const fs=require('fs'),vm=require('vm');
module.exports=function(html){
 const original=fs.readFileSync('music_atlas_v3.html','utf8');
 const expression=original.slice(original.indexOf('const definitions=')+18,original.indexOf('const palette=')).trim().replace(/;$/,'');
 const legacy=vm.runInNewContext(expression);
 const moodCuration=JSON.parse(fs.readFileSync('../MusicAtlasData/mood-curation.json','utf8'));
 const runtime=fs.readFileSync('atlas-dimensions-runtime.js','utf8');
 const providerMoods=vm.runInNewContext(runtime.match(/^const moodNames=(\[.*\]);$/m)[1]);
 if(moodCuration.schema!==1||!Array.isArray(moodCuration.categories)||!moodCuration.tracks||Array.isArray(moodCuration.tracks)||typeof moodCuration.tracks!=='object')throw Error('Invalid mood curation document');
 const categoryIds=new Set();
 for(const c of moodCuration.categories){if(!['m-energy','m-sacred'].includes(c.id)||c.dim!=='mood'||typeof c.name!=='string'||typeof c.color!=='string'||categoryIds.has(c.id))throw Error('Invalid or duplicated shared mood category');categoryIds.add(c.id)}
 const moodIds=new Set([...providerMoods.map(name=>'mood:'+name),...categoryIds]);
 for(const [id,c] of Object.entries(moodCuration.tracks)){
  if(!/^ncm-song-\d+$/.test(id)||!c||!Array.isArray(c.add)||!Array.isArray(c.remove)||!c.add.concat(c.remove).every(tag=>moodIds.has(tag))||c.add.some(tag=>c.remove.includes(tag))||typeof c.title!=='string'||typeof c.artist!=='string'||typeof c.reason!=='string'||!c.reason.trim())throw Error('Invalid shared mood correction: '+id);
 }
 const dims=[{id:'genre',name:'曲风',color:'#5888a3'},{id:'mood',name:'气质',color:'#a77cba'},{id:'source',name:'来源',color:'#b58b55'}];
 html=html.replace(/const dimensions=.*?;\r?\n/,'const dimensions='+JSON.stringify(dims)+';\n');
 const end=html.indexOf('const palette=');
 html=html.slice(0,end)+'const legacyDefinitions='+JSON.stringify(legacy)+';\ndefinitions.push(...legacyDefinitions.filter(c=>c.dim==="source"));\nconst genreCatalog='+fs.readFileSync('atlas-genre-catalog.json','utf8')+';\n'+html.slice(end);
 const legacyFn=original.slice(original.indexOf('function autoTags('),original.indexOf('function tagsOf(')).replace('function autoTags(','function legacyTags(').replace('return g.slice(0,5)','return g');
 const names=['autoTags','tagsOf','save','restore','counts','groupData','renderMap','renderChooser','renderStats','choose','editSong','render','download'];
 for(const name of names){const re=new RegExp('^function '+name+'\\(.*\\r?\\n','m');if(!re.test(html))throw Error('Missing '+name);html=html.replace(re,'');}
 const predicate="selected==='__all__'||(selected==='__unclassified__'&&!tagsOf(x).length)||tagsOf(x).includes(selected)";
 html=html.replace(predicate,'matchesView(x)');
 html=html.replaceAll("selected==='__all__'||(selected==='__unclassified__'&&!tagsOf(t).length)||tagsOf(t).includes(selected)",'matchesView(t)');
 html=html.replace(/^\$\('recommendBtn'\).*\r?\n/m,"$('recommendBtn').onclick=()=>{expansion[activeDim]='auto';render();save();msg('当前维度已按累计 90% 票数展开；不超过 10 类时全部展开')};\n");
 html=html.replace("home.add(id);$('newName')","home.add(id);expansion[dim]='custom';$('newName')");
 html=html.replace("overrides={};categories=definitions", "overrides={};expansion={genre:'auto',mood:'auto',source:'auto'};filters={genre:new Set(),mood:new Set(),source:new Set()};selected='__all__';categories=definitions");
 html=html.replace("<option value=\"manual\">手动类别</option>",'<option value="mood">气质</option><option value="source">来源</option>');
 html=html.replace('恢复网易云曲风</button>','恢复此曲全部自动标签</button>');
 html=html.replace('人工修改优先于网易云曲风，按歌曲 ID 保存在本机。','手动修改只覆盖同一维度，按歌曲 ID 保存在本机。气质与来源保留原规则初判，可逐维度修正。');
 html=html.replace('<div class="main">','<section class="panel dimension-panel"><div id="dimensionTabs" class="actions"></div><p id="mapSummary" class="hint"></p><details id="filterPanel"><summary>组合筛选 <span id="filterSummary"></span></summary><p class="hint">同维度任选其一，不同维度同时满足。筛选影响列表与播放，地图始终统计整个歌单。</p><div id="filterGroups"></div><button class="btn" id="clearFilters">清除组合筛选</button></details></section><div class="main">');
 html=html.replace('音乐地图 · 主页','音乐地图 · 整个歌单');
 html=html.replace('id="unclassifiedBtn">其它','id="unclassifiedBtn">未分类');
 html=html.replace('>主页分类</h2>','>展开类别</h2>');
 html=html.replace(/<div class="chooser-desc">[^<]*<\/div>/,'<div class="chooser-desc">按歌曲数排列 · 折叠的分类票数并入“其它类别”，其余类别面积不变</div>');
 html=html.replace('<label><input type="checkbox" id="showOther"> 展示其它方块</label><p class="hint">开启后，未分类歌曲不为 0 时显示「其它」。</p>','<label hidden><input type="checkbox" id="showOther" checked disabled> 保留完整地图</label><p class="hint">其它类别收纳折叠的分类票数；未分类收纳当前维度没有标签的歌曲，两者始终保留。</p>');
 html=html.replace('每首歌可属于多个候选类别；所有入选主页的类别各自完整计数。方块面积使用“已选类别计数之和”归一化，而非唯一歌曲总数。仅有曲目元数据，不能据此保证情绪和风格的识别准确性。','每首歌在当前维度每个不同标签各投一票；同一曲风大类的细分标签不重复投票。总票数为当前维度全部分类票数，加未分类歌曲数。组合筛选不改变地图面积。气质与来源沿用元数据规则初判，不代表经过人工确认。');
 html=html.replace('「其它」只包含没有有效分类的歌曲；取消主页勾选不会取消歌曲的分类。其它方块计入地图面积，但不会参与系统推荐选择。','“其它类别”合并折叠标签的票数，歌曲列表去重；“未分类”表示当前维度没有标签。系统推荐按已分类票数累计达到 90% 展开，有歌曲的类别不超过 10 个则全部展开。');
 html=html.replace('</style>','\n.dimension-panel{padding:16px 20px;margin-bottom:16px}.dimension-panel .actions{margin:0}.filter-row{display:flex;gap:8px;flex-wrap:wrap;margin:12px 0}.filter-row strong{min-width:45px;padding:6px 0}.dimension-tab[aria-pressed="true"]{background:var(--accent);color:var(--accent-ink)}.dimension-panel summary{padding:8px 0}.dimension-panel .hint{margin:8px 0}.editor-row .btn{font-size:11px;padding:5px 8px}\n</style>');
 const anchor="for(const id of ['importData','importAudio'])";
 html=html.replace(anchor,legacyFn+'\nconst moodCuration='+JSON.stringify(moodCuration).replaceAll('<','\\u003c')+';\n'+runtime+'\n'+fs.readFileSync('atlas-editor-runtime.js','utf8')+'\n'+anchor);
 html=html.replace("genreState:t.genreState||'pending'", "genreState:t.genreState||'pending',emotions:Array.isArray(t.emotions)?t.emotions.filter(s=>typeof s==='string'):[],emotionState:t.emotionState||'pending'");
 html=html.replace("t.genreState=u.genreState", "t.genreState=u.genreState;t.emotions=Array.isArray(u.emotions)?u.emotions:[];t.emotionState=u.emotionState||'pending'");
 html=html.replace("if(m.type==='playlist'){", "if(m.type==='configuration'){applyHostState(m.state);return}if(m.type==='playlist'){editorOnly=!!m.editor;document.body.classList.toggle('editor-only',editorOnly);");
 html=html.replace("ensureGenres();save();render();", "ensureGenres();render();save();if(editorOnly&&tracks.length)editSong(tracks[0].id);");
 html=html.replace("if($('songDetails').open&&editId)editSong(editId);", "if(($('songDetails').open||editorOnly)&&editId)editSong(editId);");
 html=html.replaceAll('其它类别','其它').replaceAll('气质与来源沿用元数据规则初判','气质采用网易云情绪标签；来源沿用元数据规则初判').replaceAll('气质与来源保留原规则初判','气质使用网易云情绪标签，来源保留规则初判');
 html=html.replace('</style>', '<style-placeholder></style>').replace('<style-placeholder>', '.editor-only .page,.editor-only .dialog-head,.editor-only .editor-head{display:none!important}.editor-only #songDetails{display:block;position:static;width:100%;max-height:none;margin:0;padding:12px;border:0;border-radius:0}.editor-only #songDetails::backdrop{display:none}.editor-only .toast{bottom:12px}.editor-only{background:var(--panel)}.tag-search-results{max-height:190px;overflow:auto;display:flex;gap:6px;flex-wrap:wrap;margin:12px 0}.tag-search-results .chip{text-align:left}.tag-tabs{display:flex;gap:8px;margin:14px 0}.selected-tags{display:flex;gap:6px;flex-wrap:wrap;margin:10px 0}.tag-caption{margin:8px 0}.tag-tools{display:flex;gap:6px;flex-wrap:wrap}.tag-create{display:flex;gap:8px;margin:8px 0;flex-wrap:wrap}.tag-create input{min-width:120px;flex:1}');
 html=html.replace("$('closeDetails').onclick=()=>$('songDetails').close()", "$('closeDetails').onclick=()=>$('songDetails').close()");
 html=html.replace('</style>', '.tag-tabs{flex-wrap:wrap;align-items:center}.tag-provenance{flex:1;min-width:150px}.tile .name{font-size:clamp(16px,1.8vw,26px);line-height:1.25;overflow-wrap:anywhere}.tile .size{font-size:clamp(13px,1.2vw,18px);letter-spacing:0}.tile.small .name{font-size:clamp(14px,1.5vw,20px)}.tile.small .size{font-size:14px}.tile.tiny .name{font-size:14px}.tile.tiny .size{display:none}</style>');
 return html;
};
