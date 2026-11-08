const fs=require('fs'),assert=require('assert/strict'),path=require('path');
const {chromium}=require('playwright');
(async()=>{const browser=await chromium.launch({headless:true,...(process.env.ATLAS_TEST_BROWSER?{executablePath:process.env.ATLAS_TEST_BROWSER}:{})});try{
 const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{window.messages=[];window.chrome.webview={postMessage:m=>messages.push(m),addEventListener:(n,f)=>window.receive=f}});
 let html=fs.readFileSync(path.join(__dirname,'../HyPlayer/Assets/MusicAtlas/index.html'),'utf8');const at=html.lastIndexOf('})();');html=html.slice(0,at)+`window.test={groupData,dimTags,setDim:d=>{activeDim=d;render()},hide:id=>{expansion[activeDim]='custom';home.delete(id);render()}};`+html.slice(at);
 await page.route('https://atlas.test/',r=>r.fulfill({contentType:'text/html',body:html}));await page.goto('https://atlas.test/');
 await page.evaluate(()=>receive({data:{type:'playlist',playlistId:'test',name:'Fixture',tracks:[{id:'a',title:'A',artist:'',album:'',styles:['摇滚-后摇','摇滚-艺术摇滚','流行'],emotions:['热血励志','活力','KTV'],genreState:'known',emotionState:'known'},{id:'b',title:'B',artist:'',album:'',styles:['古典'],emotions:[],genreState:'known',emotionState:'known'},{id:'c',title:'C',artist:'',album:'',styles:[],emotions:[],genreState:'known',emotionState:'known'}]}}));
 assert.equal(await page.evaluate(()=>test.groupData().reduce((s,g)=>s+g.count,0)),4);
 await page.evaluate(()=>{test.hide('nc:流行');test.hide('nc:古典')});assert.equal(await page.locator('.tile[data-category="nc:摇滚"]').getAttribute('data-share'),'0.25');
 await page.evaluate(()=>test.setDim('mood'));assert.deepEqual(await page.evaluate(()=>test.groupData().map(g=>g.id).sort()),['__unclassified__','mood:活力','mood:热血励志'].sort());
 assert.deepEqual(errors,[]);console.log('Passed: multi-vote conservation, genre root deduplication, distinct moods and whitelist filtering.');
}finally{await browser.close()}})().catch(e=>{console.error(e);process.exitCode=1});
