const test = require('node:test');
const assert = require('node:assert/strict');
const C = require('./core.js');
const assets = new Map([['tall.svg', {width: 100, height: 200}], ['wide.svg', {width: 200, height: 100}]]);
const group = {id: 'one', name: 'One', width: 620, padding: 10, gap: 20, layout: 'vertical', panels: [{asset: 'tall.svg'}, {asset: 'wide.svg'}]};
const template = {version: 1, name: 'Demo', groups: [group]};

test('vertical assembly fits the chosen width and leaves transparent margins', () => {
  const result = C.layoutGroup(group, assets);
  assert.equal(result.width, 620); assert.equal(result.height, 1540);
  assert.deepEqual(result.placements.map(({x, y, width, height}) => ({x, y, width, height})), [{x:10,y:10,width:600,height:1200},{x:10,y:1230,width:600,height:300}]);
});
test('horizontal assembly preserves each aspect ratio and aligns at the top', () => {
  const result = C.layoutGroup({...group, layout: 'horizontal'}, assets);
  assert.equal(result.height, 600);
  assert.deepEqual(result.placements.map(({x, y, width, height}) => ({x, y, width, height})), [{x:10,y:10,width:290,height:580},{x:320,y:10,width:290,height:145}]);
});
test('an empty assembly has no artificial output height', () => assert.equal(C.layoutGroup({...group, panels: []}, assets).height, 0));
test('crowded horizontal layouts fail instead of silently clipping', () => assert.throws(() => C.layoutGroup({...group, layout:'horizontal', gap:700}, assets), /too crowded/));
test('templates normalize shorthand and maintain independent colour overrides', () => {
  const result = C.validateTemplate({...template, theme: {text:'#ffffff',lineScope:'all'}, groups:[{...group, panels:['tall.svg',{asset:'wide.svg',theme:{text:null},elements:{'node-0':{fill:'#ff0000'}}}]}]}, assets);
  assert.equal(result.groups[0].panels[0].asset, 'tall.svg');
  assert.equal(result.groups[0].panels[1].theme.text, null);
  assert.equal(result.groups[0].panels[1].elements['node-0'].fill, '#ff0000');
  assert.equal(result.scale, 1); assert.equal(result.theme.lineScope, 'all');
});
test('bad template colours, missing files, duplicate groups and bad spacing are rejected', () => {
  assert.throws(() => C.validateTemplate({...template,theme:{text:'red'}},assets), /six-digit/);
  assert.throws(() => C.validateTemplate({...template,groups:[{...group,panels:['missing.svg']}]},assets), /Missing SVG/);
  assert.throws(() => C.validateTemplate({...template,groups:[group,group]},assets), /unique ID/);
  assert.throws(() => C.validateTemplate({...template,groups:[{...group,padding:310}]},assets), /leave room/);
  assert.throws(() => C.validateTemplate({...template,groups:[{...group,gap:-1}]},assets), /spacing/);
  assert.throws(() => C.validateTemplate({...template,groups:[{...group,panels:[{asset:'tall.svg',elements:{'unknown':{fill:'#ffffff'}}}]}]},assets), /invalid key/);
});
test('old version 1 templates receive stable placement IDs and poster defaults', () => {
  const result = C.validateTemplate({...template,groups:[{...group,panels:['tall.svg',{asset:'wide.svg',id:'saved-panel'}]}]},assets);
  assert.deepEqual(result.groups[0].panels.map(panel=>panel.id),['one-panel-0','saved-panel']);
  assert.equal(result.groups[0].minHeight,900); assert.equal(result.groups[0].snap,true);
  assert.deepEqual(result.colourGroups,[]);
  assert.deepEqual(C.validateTemplate(result,assets),result);
  assert.deepEqual(C.layoutGroup(result.groups[0],assets),C.layoutGroup({...group,panels:result.groups[0].panels},assets));
});
test('poster placements retain independent coordinates, widths and aspect ratios', () => {
  const poster = {...group,layout:'poster',minHeight:900,panels:[{asset:'tall.svg',x:25,y:80,width:120},{asset:'wide.svg',x:240,y:780,width:200}]};
  const result = C.layoutGroup(poster,assets);
  assert.equal(result.width,620); assert.equal(result.height,900);
  assert.deepEqual(result.placements.map(({x,y,width,height})=>({x,y,width,height})),[{x:25,y:80,width:120,height:240},{x:240,y:780,width:200,height:100}]);
  assert.equal(C.layoutGroup({...poster,minHeight:100},assets).height,890);
  assert.equal(C.layoutGroup({...poster,panels:[{asset:'wide.svg',x:10,y:100,width:101.5}],minHeight:0},assets).height,160.75);
  const normalized = C.validateTemplate({...template,groups:[{...poster,snap:false}]},assets).groups[0];
  assert.equal(normalized.snap,false); assert.equal(normalized.panels[1].x,240);
});
test('poster defaults produce a valid stack and keep an empty canvas at its minimum height', () => {
  const poster = {...group,layout:'poster'};
  const result = C.layoutGroup(poster,assets);
  assert.deepEqual(result.placements.map(({x,y,width,height})=>({x,y,width,height})),[{x:10,y:10,width:600,height:1200},{x:10,y:1230,width:600,height:300}]);
  assert.equal(result.height,1540);
  assert.equal(C.layoutGroup({...poster,panels:[]},assets).height,900);
});
test('posters grow to fit three or four columns and panels wider than their minimum canvas', () => {
  const columns=[10,230,450,670].map(x=>({asset:'wide.svg',x,y:10,width:200}));
  const three=C.validateTemplate({...template,groups:[{...group,layout:'poster',panels:columns.slice(0,3)}]},assets).groups[0];
  assert.equal(three.width,620); assert.equal(C.layoutGroup(three,assets).width,660);
  const four=C.validateTemplate({...template,groups:[{...group,layout:'poster',panels:columns}]},assets).groups[0];
  assert.equal(four.width,620); assert.equal(C.layoutGroup(four,assets).width,880);
  assert.deepEqual(C.layoutGroup(four,assets).placements.map(({x,width})=>({x,width})),columns.map(({x,width})=>({x,width})));
  const wide=C.validateTemplate({...template,groups:[{...group,layout:'poster',panels:[{asset:'wide.svg',x:510,y:10,width:1200}]}]},assets).groups[0];
  assert.equal(C.layoutGroup(wide,assets).width,1720); assert.equal(C.layoutGroup(wide,assets).placements[0].height,600);
  assert.equal(C.layoutGroup({...four,width:1500},assets).width,1500);
});
test('poster minimum bounds and non-finite placement geometry fail validation', () => {
  for (const panel of [{x:9},{y:9},{width:0},{width:-1},{x:Infinity},{x:NaN},{y:NaN},{width:Infinity},{width:'100'}]) {
    assert.throws(()=>C.validateTemplate({...template,groups:[{...group,layout:'poster',panels:[{asset:'tall.svg',...panel}]}]},assets),/Panel|Poster panel/);
  }
  assert.throws(()=>C.validateTemplate({...template,groups:[{...group,layout:'poster',minHeight:Infinity}]},assets),/minimum height/);
  assert.throws(()=>C.validateTemplate({...template,groups:[{...group,snap:'true'}]},assets),/snapping/);
  const result=C.validateTemplate({...template,groups:[{...group,panels:[{asset:'wide.svg',x:-5,y:-10,width:2000}]}]},assets);
  assert.equal(result.groups[0].panels[0].x,-5);
  assert.equal(C.layoutGroup(result.groups[0],assets).placements[0].width,600);
});
test('reordering preserves placement identity, IDs and poster positions without mutating the array', () => {
  const original=[{id:'a',x:10},{id:'b',x:20},{id:'c',x:30}];
  const moved=C.movePanel(original,0,2);
  assert.deepEqual(moved.map(panel=>panel.id),['b','c','a']);
  assert.deepEqual(original.map(panel=>panel.id),['a','b','c']);
  assert.equal(moved[2],original[0]); assert.equal(moved[2].x,10);
  assert.deepEqual(C.movePanel(moved,2,0),original);
  assert.throws(()=>C.movePanel(original,0,3),/zero-based/);
  assert.throws(()=>C.movePanel(original,0.5,1),/zero-based/);
});
test('placement and group identities reject duplicates and invalid explicit values', () => {
  assert.throws(()=>C.validateTemplate({...template,groups:[{...group,panels:[{asset:'tall.svg',id:'same'},{asset:'wide.svg',id:'same'}]}]},assets),/unique ID/);
  assert.throws(()=>C.validateTemplate({...template,groups:[{...group,panels:[{asset:'tall.svg',id:'same'}]},{...group,id:'two',panels:[{asset:'wide.svg',id:'same'}]}]},assets),/unique ID/);
  assert.throws(()=>C.validateTemplate({...template,groups:[{...group,panels:[{asset:'tall.svg',id:''}]}]},assets),/non-empty string/);
  assert.throws(()=>C.validateTemplate({...template,groups:[{...group,id:7}]},assets),/non-empty string/);
});
test('snapping selects the closest shared or adjacent edge independently in each axis', () => {
  const peers=[{x:100,y:100,width:80,height:60}];
  assert.deepEqual(C.snapPosition({x:103,y:96,width:50,height:40},peers),{x:100,y:100,guides:[{axis:'x',value:100},{axis:'y',value:100}]});
  assert.deepEqual(C.snapPosition({x:53,y:164,width:50,height:40},peers),{x:50,y:160,guides:[{axis:'x',value:100},{axis:'y',value:160}]});
  assert.deepEqual(C.snapPosition({x:178,y:120,width:50,height:40},peers),{x:180,y:120,guides:[{axis:'x',value:180},{axis:'y',value:160}]});
  assert.equal(C.snapPosition({x:103,y:500,width:50,height:40},[{x:110,y:100,width:80,height:60},{x:100,y:100,width:80,height:60}]).x,100);
  assert.deepEqual(C.snapPosition({x:115,y:300,width:50,height:40},peers),{x:115,y:300,guides:[]});
});
test('disabled snapping still clamps the rectangle and enabled snaps respect canvas bounds', () => {
  const peers=[{x:100,y:100,width:80,height:60}];
  assert.deepEqual(C.snapPosition({x:103,y:96,width:50,height:40},peers,{enabled:false}),{x:103,y:96,guides:[]});
  assert.deepEqual(C.snapPosition({x:900,y:-3,width:50,height:40},peers,{enabled:false,minX:10,minY:10,maxX:300,maxY:250}),{x:250,y:10,guides:[]});
  assert.deepEqual(C.snapPosition({x:249,y:90,width:50,height:40},[{x:255,y:500,width:80,height:60}],{minX:10,minY:10,maxX:300}),{x:249,y:90,guides:[]});
  assert.deepEqual(C.snapPosition({x:102,y:90,width:50,height:40},peers,{tolerance:1}),{x:102,y:90,guides:[]});
});
const colourMember={group:'one',panel:'one-panel-0',element:'node-0',paint:'fill'};
const colourGroup={id:'titles',name:'Titles',colour:'#aabbcc',members:[colourMember]};
test('named colour groups normalize membership references and preserve original panel overrides', () => {
  const result=C.validateTemplate({...template,colourGroups:[colourGroup]},assets);
  assert.deepEqual(result.colourGroups,[colourGroup]);
  assert.notEqual(result.colourGroups[0],colourGroup); assert.notEqual(result.colourGroups[0].members[0],colourMember);
  assert.deepEqual(C.validateTemplate(result,assets),result);
  const both=C.validateTemplate({...template,colourGroups:[colourGroup,{...colourGroup,id:'rules',name:'Rules',members:[{...colourMember,paint:'stroke'}]}]},assets);
  assert.equal(both.colourGroups.length,2);
});
test('original element-paint sentinels validate and survive template roundtrips',()=>{
  const input={...template,theme:{text:'#ffffff'},groups:[{...group,panels:[{asset:'tall.svg',elements:{'node-0':{fill:'original',stroke:'original'},'node-1':{fill:'#abcdef'}}}]}]};
  const normalized=C.validateTemplate(input,assets);
  assert.deepEqual(normalized.groups[0].panels[0].elements, input.groups[0].panels[0].elements);
  assert.deepEqual(C.validateTemplate(JSON.parse(JSON.stringify(normalized)),assets),normalized);
  assert.throws(()=>C.validateTemplate({...template,groups:[{...group,panels:[{asset:'tall.svg',elements:{'node-0':{fill:'Original'}}}]}]},assets),/six-digit hex colours or original/);
});
test('named colour groups reject invalid names, colours, identities, paints, refs and duplicate memberships', () => {
  for (const patch of [{name:''},{name:'x'.repeat(201)},{id:''},{colour:'red'},{members:null},{members:[{...colourMember,paint:'opacity'}]},{members:[{...colourMember,element:'bad'}]},{members:[{...colourMember,group:'missing'}]},{members:[{...colourMember,panel:'missing'}]}]) {
    assert.throws(()=>C.validateTemplate({...template,colourGroups:[{...colourGroup,...patch}]},assets),/colour group|Colour group/);
  }
  assert.throws(()=>C.validateTemplate({...template,colourGroups:[colourGroup,{...colourGroup,name:'Other'}]},assets),/unique ID/);
  assert.throws(()=>C.validateTemplate({...template,colourGroups:[colourGroup,{...colourGroup,id:'other',name:' titles '}]},assets),/unique name/);
  assert.throws(()=>C.validateTemplate({...template,colourGroups:[{...colourGroup,members:[colourMember,colourMember]}]},assets),/duplicate memberships/);
  assert.throws(()=>C.validateTemplate({...template,colourGroups:[colourGroup,{...colourGroup,id:'other',name:'Other'}]},assets),/duplicate memberships/);
  const keyedAssets=new Map(assets); keyedAssets.set('tall.svg',{width:100,height:200,svg:'<svg><text data-edit-key="node-0"/></svg>'});
  assert.throws(()=>C.validateTemplate({...template,colourGroups:[{...colourGroup,members:[{...colourMember,element:'node-99'}]}]},keyedAssets),/missing SVG element/);
});

// A small DOM test fixture keeps these tests dependency-free; browser XML parsing is
// covered by the assembler's browser smoke check. These nodes exercise core edits.
class FixtureNode {
  constructor(name,attrs={},children=[]) {
    this.localName=name; this.attrs={...attrs}; this.children=[]; this.parentElement=null;
    this.style={setProperty(key,value){this[key]=value;}}; this.textContent='';
    this.append(...children);
  }
  get attributes(){return Object.entries(this.attrs).map(([name,value])=>({name,value}));}
  get id(){return this.getAttribute('id')||'';}
  set id(value){this.setAttribute('id',value);}
  setAttribute(key,value){this.attrs[key]=String(value);}
  getAttribute(key){return Object.hasOwn(this.attrs,key)?this.attrs[key]:null;}
  hasAttribute(key){return Object.hasOwn(this.attrs,key);}
  removeAttribute(key){delete this.attrs[key];}
  append(...nodes){for(const node of nodes){node.parentElement=this;this.children.push(node);}}
  remove(){if(this.parentElement)this.parentElement.children=this.parentElement.children.filter(node=>node!==this);}
  matches(selector){
    if(selector==='*')return true;
    if(selector==='text tspan')return this.localName==='tspan'&&this.parentElement?.localName==='text';
    if(selector==='g[id^="artwork-"]')return this.localName==='g'&&this.id.startsWith('artwork-');
    if(/^\[[\w-]+\]$/.test(selector))return this.hasAttribute(selector.slice(1,-1));
    return this.localName===selector;
  }
  querySelectorAll(selector){
    const selectors=selector.split(',').map(value=>value.trim()),result=[];
    const visit=node=>{for(const child of node.children){if(selectors.some(value=>child.matches(value)))result.push(child);visit(child);}};
    visit(this);return result;
  }
  closest(selector){const selectors=selector.split(',').map(value=>value.trim());for(let node=this;node;node=node.parentElement)if(selectors.some(value=>node.matches(value)))return node;return null;}
}
const svgFixture=children=>new FixtureNode('svg',{viewBox:'0 0 100 200'},children);
function withFixtureDOM(factories,run){
  const previousParser=global.DOMParser,previousDocument=global.document;
  global.DOMParser=class{parseFromString(source){return{documentElement:factories[source](),querySelector(){return null;}};}};
  global.document={createElementNS(namespace,name){assert.equal(namespace,C.NS);return new FixtureNode(name);}};
  try{return run();}finally{if(previousParser===undefined)delete global.DOMParser;else global.DOMParser=previousParser;if(previousDocument===undefined)delete global.document;else global.document=previousDocument;}
}
test('source paints resolve inline precedence, inherited attributes and SVG defaults',()=>{
  const root=svgFixture([]),parent=new FixtureNode('text',{fill:'#e9b34c',stroke:'#aabbcc'}),span=new FixtureNode('tspan');root.append(parent);parent.append(span);
  assert.equal(C.sourcePaint(span,'fill'),'#e9b34c');assert.equal(C.sourcePaint(span,'stroke'),'#aabbcc');
  parent.style.fill='#123456';assert.equal(C.sourcePaint(span,'fill'),'#123456');
  span.setAttribute('fill','#654321');span.style.fill='inherit';assert.equal(C.sourcePaint(span,'fill'),'#123456');
  span.style.fill='unset';assert.equal(C.sourcePaint(span,'fill'),'#123456');
  span.style.fill='#abcdef';assert.equal(C.sourcePaint(span,'fill'),'#abcdef');
  span.style.stroke='none';assert.equal(C.sourcePaint(span,'stroke'),'none');
  span.style.fill='initial';assert.equal(C.sourcePaint(span,'fill'),'#000000');
  assert.equal(C.sourcePaint(root,'fill'),'#000000');assert.equal(C.sourcePaint(root,'stroke'),'none');
  assert.throws(()=>C.sourcePaint(span,'opacity'),/fill or stroke/);
});
test('additional editable fill shapes leave legacy text and stroke keys unchanged',()=>withFixtureDOM({panel:()=>svgFixture([
  new FixtureNode('rect',{id:'fill',fill:'#ffffff'}),new FixtureNode('text',{id:'text'},[new FixtureNode('tspan')]),new FixtureNode('line',{id:'line',stroke:'#ffffff'}),
  new FixtureNode('defs',{},[new FixtureNode('path',{id:'definition',fill:'#ffffff'})]),new FixtureNode('clipPath',{},[new FixtureNode('rect',{id:'clip'})]),
  new FixtureNode('g',{display:'none'},[new FixtureNode('circle',{id:'hidden'})]),new FixtureNode('path',{id:'visible',fill:'#cccccc'}),
])},()=>{
  const root=C.parseSVG('panel').root,byID=id=>root.querySelectorAll('[id]').find(node=>node.id===id);
  assert.equal(byID('text').getAttribute('data-edit-key'),'node-0');assert.equal(byID('line').getAttribute('data-edit-key'),'node-1');
  assert.equal(byID('fill').getAttribute('data-edit-key'),'node-2');assert.equal(byID('visible').getAttribute('data-edit-key'),'node-3');
  for(const id of ['definition','clip','hidden'])assert.equal(byID(id).getAttribute('data-edit-key'),null);
}));
test('unique imported edit keys survive reparsing and duplicated assembly keys regenerate',()=>withFixtureDOM({
  unique:()=>svgFixture([new FixtureNode('rect',{'data-edit-key':'node-9'}),new FixtureNode('text',{'data-edit-key':'node-4'}),new FixtureNode('circle')]),
  assembled:()=>svgFixture([new FixtureNode('svg',{'data-panel-instance':'8','data-panel-id':'stale','data-ui-layer':'selection'},[
    new FixtureNode('rect',{'data-edit-key':'node-0'}),new FixtureNode('text',{'data-edit-key':'node-0'}),new FixtureNode('line',{stroke:'#ffffff'}),
  ])]),
},()=>{
  assert.deepEqual(C.parseSVG('unique').root.querySelectorAll('[data-edit-key]').map(node=>node.getAttribute('data-edit-key')),['node-9','node-4','node-10']);
  const root=C.parseSVG('assembled').root;
  assert.deepEqual(root.querySelectorAll('[data-edit-key]').map(node=>node.getAttribute('data-edit-key')),['node-2','node-0','node-1']);
  for(const attr of ['data-panel-instance','data-panel-id','data-ui-layer'])assert.equal(root.querySelectorAll(`[${attr}]`).length,0);
}));
test('named paints take precedence over themes and element overrides, including text spans',()=>withFixtureDOM({panel:()=>svgFixture([
  new FixtureNode('text',{fill:'#111111'},[new FixtureNode('tspan',{fill:'#222222'})]),new FixtureNode('line',{stroke:'#333333'}),new FixtureNode('rect',{fill:'#444444'}),
])},()=>{
  const panelAssets=new Map([['panel',{width:100,height:200,svg:'panel'}]]),panelGroup={...group,theme:{text:'#555555'},panels:[{id:'p',asset:'panel',theme:{text:'#666666'},elements:{'node-0':{fill:'#777777'},'node-1':{stroke:'#888888'}}}]};
  const colours=[{id:'ink',name:'Ink',colour:'#abcdef',members:[{group:'one',panel:'p',element:'node-0',paint:'fill'},{group:'one',panel:'p',element:'node-1',paint:'stroke'}]}];
  const result=C.composeGroup(panelGroup,panelAssets,{text:'#999999',line:'#999999'},'','test',colours),text=result.root.querySelectorAll('text')[0],span=text.querySelectorAll('tspan')[0];
  assert.equal(text.getAttribute('fill'),'#abcdef');assert.equal(text.style.fill,'#abcdef');assert.equal(span.getAttribute('fill'),'#abcdef');assert.equal(span.style.fill,'#abcdef');
  assert.equal(result.root.querySelectorAll('line')[0].getAttribute('stroke'),'#abcdef');
  assert.equal(result.root.querySelectorAll('rect')[0].getAttribute('fill'),'#444444');
  assert.equal(result.root.children[0].getAttribute('data-panel-id'),'p');
  assert.equal(C.composeAll({groups:[panelGroup],theme:{},colourGroups:colours},panelAssets).root.querySelectorAll('text')[0].getAttribute('fill'),'#abcdef');
  const root=C.parseSVG('panel').root;C.applyColourGroups(root,'other','p',colours);assert.equal(root.querySelectorAll('text')[0].getAttribute('fill'),'#111111');
}));
test('poster growth reaches single-group and combined SVG exports',()=>withFixtureDOM({panel:()=>svgFixture([new FixtureNode('rect',{fill:'#444444'})])},()=>{
  const panelAssets=new Map([['panel',{width:100,height:200,svg:'panel'}]]),poster={...group,layout:'poster',panels:[10,230,450,670].map((x,index)=>({id:`p-${index}`,asset:'panel',x,y:10,width:200}))};
  const single=C.composeGroup(poster,panelAssets),combined=C.composeAll({groups:[{...poster,id:'first'},{...poster,id:'second',width:1100}],theme:{}},panelAssets);
  assert.equal(single.width,880); assert.equal(single.root.getAttribute('width'),'880'); assert.equal(single.root.getAttribute('viewBox'),'0 0 880 900');
  assert.equal(combined.width,1100); assert.equal(combined.root.getAttribute('width'),'1100'); assert.equal(combined.root.children[0].getAttribute('width'),'880');
  assert.equal(combined.root.children[0].getAttribute('x'),'110');
}));
test('leaf text spans receive distinct keys and named colours preserve sibling paints',()=>withFixtureDOM({mixed:()=>{
  const first=new FixtureNode('tspan',{fill:'#112233','font-weight':'bold'}),second=new FixtureNode('tspan',{fill:'#334455'}),empty=new FixtureNode('tspan');
  first.textContent='Bold words';second.textContent=' ordinary words';empty.textContent='  ';
  return svgFixture([new FixtureNode('text',{fill:'#778899'},[first,second,empty]),new FixtureNode('line',{stroke:'#ffffff'})]);
}},()=>{
  const panelAssets=new Map([['mixed',{width:100,height:200,svg:'mixed'}]]),panelGroup={...group,panels:[{id:'mixed-placement',asset:'mixed',theme:{},elements:{}}]};
  const root=C.parseSVG('mixed').root,spans=root.querySelectorAll('tspan');
  assert.equal(root.querySelectorAll('text')[0].getAttribute('data-edit-key'),'node-0');
  assert.equal(root.querySelectorAll('line')[0].getAttribute('data-edit-key'),'node-1');
  assert.deepEqual(spans.map(node=>node.getAttribute('data-edit-key')),['node-2','node-3',null]);
  const colours=[{id:'emphasis',name:'Emphasis',colour:'#abcdef',members:[{group:'one',panel:'mixed-placement',element:'node-2',paint:'fill'}]}];
  const rendered=C.composeGroup(panelGroup,panelAssets,{},'','mixed-test',colours),painted=rendered.root.querySelectorAll('tspan');
  assert.equal(painted[0].getAttribute('fill'),'#abcdef');assert.equal(painted[0].style.fill,'#abcdef');
  assert.equal(painted[1].getAttribute('fill'),'#334455');assert.equal(rendered.root.querySelectorAll('text')[0].getAttribute('fill'),'#778899');
  C.applyTheme(root,{}, {'node-0':{fill:'#123456'}});C.applyColourGroups(root,'one','mixed-placement',colours);
  assert.equal(spans[0].getAttribute('fill'),'#abcdef');assert.equal(spans[1].getAttribute('fill'),'#123456');
}));
test('a preceding leaf tspan does not shift previously generated fill-shape keys',()=>withFixtureDOM({
  withoutSpan:()=>svgFixture([new FixtureNode('text'),new FixtureNode('rect',{fill:'#334455'})]),
  withSpan:()=>{
    const span=new FixtureNode('tspan',{fill:'#112233'});span.textContent='Inline emphasis';
    return svgFixture([new FixtureNode('text',{},[span]),new FixtureNode('rect',{fill:'#334455'})]);
  },
},()=>{
  const previous=C.parseSVG('withoutSpan').root,current=C.parseSVG('withSpan').root;
  assert.equal(previous.querySelectorAll('rect')[0].getAttribute('data-edit-key'),'node-1');
  assert.equal(current.querySelectorAll('rect')[0].getAttribute('data-edit-key'),previous.querySelectorAll('rect')[0].getAttribute('data-edit-key'));
  assert.equal(current.querySelectorAll('tspan')[0].getAttribute('data-edit-key'),'node-2');
}));
test('removing a named colour restores source amber despite a white text theme',()=>withFixtureDOM({numbers:()=>{
  const number=new FixtureNode('tspan'),heading=new FixtureNode('tspan');number.textContent='1';heading.textContent='Heading';
  return svgFixture([new FixtureNode('text',{fill:'#e9b34c'},[number]),new FixtureNode('text',{fill:'#f8faf9'},[heading])]);
}},()=>{
  const panelAssets=new Map([['numbers',{width:100,height:200,svg:'numbers'}]]),panelGroup={...group,panels:[{id:'numbers-placement',asset:'numbers',theme:{},elements:{'node-2':{fill:'original'}}}]};
  const named=[{id:'numbers',name:'Numbers',colour:'#0000ff',members:[{group:'one',panel:'numbers-placement',element:'node-2',paint:'fill'}]}];
  const namedImage=C.composeGroup(panelGroup,panelAssets,{text:'#ffffff'},'','named',named);
  assert.equal(namedImage.root.querySelectorAll('tspan')[0].getAttribute('fill'),'#0000ff');
  const restored=C.composeGroup(panelGroup,panelAssets,{text:'#ffffff'},'','removed',[]),spans=restored.root.querySelectorAll('tspan');
  assert.equal(spans[0].getAttribute('fill'),'#e9b34c');assert.equal(spans[0].style.fill,'#e9b34c');
  assert.equal(spans[1].getAttribute('fill'),'#ffffff');assert.equal(restored.root.querySelectorAll('text')[1].getAttribute('fill'),'#ffffff');
}));
test('original parent text fill restores each mixed descendant source paint separately',()=>withFixtureDOM({mixedOriginal:()=>{
  const amber=new FixtureNode('tspan',{fill:'#e9b34c'}),blue=new FixtureNode('tspan',{fill:'#aabbcc'}),inherited=new FixtureNode('tspan');
  amber.textContent='Amber';blue.textContent='Blue';inherited.textContent='Inherited';
  return svgFixture([new FixtureNode('text',{fill:'#334455'},[amber,blue,inherited])]);
}},()=>{
  const root=C.parseSVG('mixedOriginal').root;C.applyTheme(root,{text:'#ffffff'},{'node-0':{fill:'original'}});
  assert.equal(root.querySelectorAll('text')[0].getAttribute('fill'),'#334455');
  assert.deepEqual(root.querySelectorAll('tspan').map(node=>node.getAttribute('fill')),['#e9b34c','#aabbcc','#334455']);
  assert.deepEqual(root.querySelectorAll('tspan').map(node=>node.style.fill),['#e9b34c','#aabbcc','#334455']);
}));
test('raster size limits account for export scale', () => {
  assert.deepEqual(C.pixelSize(620,1540,2), {width:1240,height:3080});
  assert.throws(() => C.pixelSize(620,20000,2), /too large/);
  assert.throws(() => C.pixelSize(8000,16000,1), /too large/);
  assert.throws(() => C.pixelSize(32761,1,1), /too large/);
});
test('the generated ZIP has valid CRCs, central offsets, UTF-8 names and contents', async () => {
  const files = [{name:'first.png',data:new Uint8Array([1,2,3,4])},{name:'second.svg',data:new TextEncoder().encode('<svg/>')}];
  const bytes = new Uint8Array(await C.zipFiles(files).arrayBuffer()), view = new DataView(bytes.buffer);
  let offset = 0;
  for (const file of files) {
    assert.equal(view.getUint32(offset,true),0x04034b50);
    assert.equal(view.getUint16(offset+6,true),0x800);
    assert.equal(view.getUint32(offset+14,true),C.crc32(file.data));
    const nameSize = view.getUint16(offset+26,true);
    assert.equal(new TextDecoder().decode(bytes.slice(offset+30,offset+30+nameSize)),file.name);
    assert.deepEqual(bytes.slice(offset+30+nameSize,offset+30+nameSize+file.data.length),file.data);
    offset += 30+nameSize+file.data.length;
  }
  const centralOffset = offset;
  for (const file of files) {
    assert.equal(view.getUint32(offset,true),0x02014b50);
    const nameSize = view.getUint16(offset+28,true);
    const local = view.getUint32(offset+42,true);
    assert.equal(view.getUint32(local,true),0x04034b50);
    assert.equal(view.getUint32(offset+16,true),C.crc32(file.data));
    offset += 46+nameSize;
  }
  assert.equal(view.getUint32(offset,true),0x06054b50);
  assert.equal(view.getUint16(offset+10,true),2);
  assert.equal(view.getUint32(offset+16,true),centralOffset);
});
