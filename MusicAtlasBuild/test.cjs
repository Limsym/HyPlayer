const fs=require('fs'),assert=require('assert/strict'),path=require('path');
const {chromium}=require('playwright');
(async()=>{const browser=await chromium.launch({headless:true,...(process.env.ATLAS_TEST_BROWSER?{executablePath:process.env.ATLAS_TEST_BROWSER}:{})});try{
 const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{window.messages=[];window.chrome.webview={postMessage:m=>messages.push(m),addEventListener:(n,f)=>window.receive=f}});
 let html=fs.readFileSync(path.join(__dirname,'../HyPlayer/Assets/MusicAtlas/index.html'),'utf8');const at=html.lastIndexOf('})();');html=html.slice(0,at)+`window.test={groupData,dimTags,editSong,resetDimension,setDimensionTags,get tracks(){return tracks},get categories(){return categories},get overrides(){return overrides},setDim:d=>{activeDim=d;render()},hide:id=>{expansion[activeDim]='custom';home.delete(id);render()}};`+html.slice(at);
 await page.route('https://atlas.test/',r=>r.fulfill({contentType:'text/html',body:html}));await page.goto('https://atlas.test/');
 await page.evaluate(()=>receive({data:{type:'playlist',playlistId:'test',name:'Fixture',tracks:[{id:'a',title:'A',artist:'',album:'',styles:['摇滚-后摇','摇滚-艺术摇滚','流行'],emotions:['热血励志','活力','KTV'],genreState:'known',emotionState:'known'},{id:'b',title:'B',artist:'',album:'',styles:['古典'],emotions:[],genreState:'known',emotionState:'known'},{id:'c',title:'C',artist:'',album:'',styles:[],emotions:[],genreState:'known',emotionState:'known'}]}}));
 assert.equal(await page.evaluate(()=>test.groupData().reduce((s,g)=>s+g.count,0)),4);
 await page.evaluate(()=>{test.hide('nc:流行');test.hide('nc:古典')});assert.equal(await page.locator('.tile[data-category="nc:摇滚"]').getAttribute('data-share'),'0.25');
 await page.evaluate(()=>test.setDim('mood'));assert.deepEqual(await page.evaluate(()=>test.groupData().map(g=>g.id).sort()),['__unclassified__','mood:活力','mood:热血励志'].sort());
 // The public corrections match exact provider IDs; identical titles are not a rule.
 const curation=JSON.parse(fs.readFileSync(path.join(__dirname,'../MusicAtlasData/mood-curation.json'),'utf8'));
 const correctedId='ncm-song-22605222';
 const rows=[{id:correctedId,title:'Creep',artist:'Radiohead',album:'Pablo Honey',styles:['摇滚'],emotions:['甜蜜','欢快','悲伤','思念','放松'],genreState:'known',emotionState:'known'},
 {id:'unrelated-cover',title:'Creep',artist:'Different recording',album:'',styles:[],emotions:['甜蜜'],genreState:'known',emotionState:'known'},
 {id:'ncm-song-31654455',title:'Victory',artist:'Two Steps From Hell',album:'Battlecry',styles:[],emotions:[],genreState:'known',emotionState:'pending'}];
 await page.evaluate(tracks=>receive({data:{type:'playlist',playlistId:'curation-test',name:'Curation',tracks}}),rows);
 const rule=curation.tracks[correctedId];
 const expected=[...new Set([...rows[0].emotions.map(s=>'mood:'+s).filter(id=>!rule.remove.includes(id)),...rule.add])].sort();
 assert.deepEqual(await page.evaluate(()=>test.dimTags(test.tracks[0],'mood').sort()),expected);
 assert(!expected.includes('mood:甜蜜'));
 assert.deepEqual(await page.evaluate(()=>test.tracks[0].emotions),rows[0].emotions);
 assert.deepEqual(await page.evaluate(()=>test.dimTags(test.tracks[1],'mood')),['mood:甜蜜']);
 assert((await page.evaluate(()=>test.dimTags(test.tracks[2],'mood'))).includes('m-energy'));
 await page.evaluate(()=>test.editSong(test.tracks[0].id));
 await page.locator('[data-editor-dim="mood"]').click();
 assert.equal(await page.locator('[data-edit-category="mood:甜蜜"]').getAttribute('data-provider'),'true');
 assert.equal(await page.locator('[data-edit-category="mood:甜蜜"]').getAttribute('aria-pressed'),'false');
 assert((await page.locator('.tag-caption').textContent()).includes('人工校正'));
 await page.evaluate(()=>{test.setDimensionTags(test.tracks[0],'genre',['nc:古典']);test.setDimensionTags(test.tracks[0],'source',[]);test.setDimensionTags(test.tracks[0],'mood',[])});
 assert.deepEqual(await page.evaluate(()=>test.dimTags(test.tracks[0],'mood')),[]);
 await page.evaluate(()=>test.setDimensionTags(test.tracks[0],'mood',['mood:甜蜜']));
 assert.deepEqual(await page.evaluate(()=>test.dimTags(test.tracks[0],'mood')),['mood:甜蜜']);
 await page.evaluate(()=>test.resetDimension(test.tracks[0],'mood'));
 assert.deepEqual(await page.evaluate(()=>test.dimTags(test.tracks[0],'mood').sort()),expected);
 assert.deepEqual(await page.evaluate(()=>test.dimTags(test.tracks[0],'genre')),['nc:古典']);
 assert.deepEqual(await page.evaluate(()=>test.overrides[test.tracks[0].id].source),[]);
 const before=await page.evaluate(()=>test.groupData().reduce((s,g)=>s+g.count,0));
 await page.evaluate(()=>test.hide('m-energy'));
 assert.equal(await page.evaluate(()=>test.groupData().reduce((s,g)=>s+g.count,0)),before);
 assert.equal(await page.evaluate(()=>test.categories.filter(c=>c.id==='m-energy').length),1);
 assert.equal(await page.evaluate(()=>test.categories.filter(c=>c.id==='m-sacred').length),1);
 assert.deepEqual(errors,[]);console.log('Passed: multi-vote conservation, genre root deduplication, mood whitelist, exact-ID curation, raw provider stars, manual/empty overrides, per-dimension reset and shared-rule deduplication.');
}finally{await browser.close()}})().catch(e=>{console.error(e);process.exitCode=1});
