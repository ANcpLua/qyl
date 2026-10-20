/** Integration answer key: real OTLP ingest -> collector -> MCP -> generated validators. */
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { randomBytes } from 'node:crypto';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { Client, StreamableHTTPClientTransport } from '@modelcontextprotocol/client';
import { createMcpHandler } from '@modelcontextprotocol/server';
import { createServer } from './dist/server.js';
const fixture = JSON.parse(await readFile(process.env.QYL_ANSWER_KEY_FILE ?? new URL('missing-answer-key-use-run-answer-key', import.meta.url),'utf8'));
process.env.QYL_COLLECTOR_URL = process.env.QYL_ANSWER_KEY_COLLECTOR ?? 'http://127.0.0.1:5510';
process.env.QYL_PROJECT = fixture.project;
delete process.env.QYL_API_KEY;
delete process.env.MCP_COLLECTOR_PROJECTS;
const w=fixture.window, key=fixture.keys;
const scoped=(service)=>({ ...w, matchers:[{key:'service.name',op:'eq',value:service,scope:'resource'}] });
// The seed writes instants in microseconds and the Collector in 100 ns: one is within a millisecond of the other.
const sameInstant=(actual,expected)=>assert(Math.abs(Date.parse(actual)-Date.parse(expected))<=1,`${actual} is not ${expected}`);
async function withClient(run, project = fixture.project) {
 const previousProject=process.env.QYL_PROJECT;
 process.env.QYL_PROJECT=project;
 const handler=createMcpHandler(()=>createServer({nativeExecution:false}));
 const client=new Client({name:'qyl-answer-key',version:'1.0.0'},{versionNegotiation:{mode:{pin:'2026-07-28'}}});
 await client.connect(new StreamableHTTPClientTransport(new URL('http://qyl.test/mcp'),{fetch:(url,init)=>handler.fetch(new Request(url,init))}));
 try {return await run(async(name,args={})=>{
   const result=await client.callTool({name,arguments:args});
   assert(!result.isError, `${name}: ${JSON.stringify(result)}`);
   assert(Buffer.byteLength(JSON.stringify(result), 'utf8') <= 24_000, name+' exceeds response budget');
   if (!['annotate','update_failure_status'].includes(name)) {
     // A verification's before and after windows are its bounds; every other read reports the window it applied.
     if (name!=='verify_fix') assert(result.structuredContent.window, name+' omits absolute window');
     assert(result.structuredContent.coverage, name+' omits coverage');
   }
   return result.structuredContent;
 });} finally {await client.close();await handler.close();process.env.QYL_PROJECT=previousProject;}
}
async function failures(call) {
 return (await call('triage',{...w,service_name:'checkout',status:'all'})).failures;
}
const registrations=[];
function verify(name,run){registrations.push(name);void test(name,{concurrency:false},()=>withClient(run));}
function verifyIsolated(name,run){
 registrations.push(name);
 void test(name,{concurrency:false},async()=>{
  const project='answer-key-'+randomBytes(8).toString('hex');
  const output=join(dirname(process.env.QYL_ANSWER_KEY_FILE),project+'.json');
  const repo=fileURLToPath(new URL('../..',import.meta.url));
  await promisify(execFile)('python3',[join(repo,'tests/Qyl.Collector.Tests/Fixtures/seed-answer-key.py'),process.env.QYL_ANSWER_KEY_OTLP,output,project]);
  await withClient(run,project);
 });
}
verify('01 triage ranks F first, includes rare R and silent billing, excludes healthy sess_ok',async call=>{
 const data=await call('triage',{...w,matchers:[{key:'service.name',op:'in',values:['checkout','orders-db','payments','billing','sessions'],scope:'resource'}]});
 const F=(await failures(call)).find(f=>f.operation==='checkout'); assert(F);
 assert.equal(data.failures[0].failure_id,F.failure_id);
 assert.equal(data.failures[0].coinciding_release.version,'1.4.1');
 assert(data.failures.some(f=>f.count===2 && f.service==='payments'));
 assert(data.silent_services.some(s=>s.service==='billing'));
 const sessionFailures = data.failures.filter(f => f.service==='sessions');
 assert.equal(sessionFailures.length, 1);
 assert.equal(sessionFailures[0].count, 3, 'only sess_bad contributes failures');
 const badIds = new Set(Object.entries(key).filter(([name]) => name.startsWith('sess_bad-')).map(([, id]) => id.trace_id));
 assert(badIds.has(sessionFailures[0].example_trace_id));
 const healthyIds = new Set(Object.entries(key).filter(([name]) => name.startsWith('sess_ok-')).map(([, id]) => id.trace_id));
 assert(data.failures.every(f => !healthyIds.has(f.example_trace_id)));
 assert(!data.regressions.some(r => r.service === 'sessions'));
 assert(!data.silent_services.some(r => r.service === 'sessions'));
});
verify('02 db chain yields two stable failure ids',async call=>{
 const time=Date.parse(fixture.T)+60_000;
 const args={start_time:new Date(time-10).toISOString(),end_time:new Date(time+500).toISOString()};
 const a=await call('triage',args),b=await call('triage',args);
 assert.equal(a.failures.length,2);assert.deepEqual(a.failures.map(f=>f.failure_id),b.failures.map(f=>f.failure_id));
});
verify('03a db child is the root cause, not propagated checkout error',async call=>{
 const data=await call('get_failure_context',{...w,trace_id:key['db-root'].trace_id});
 assert.equal(data.observed.root_cause_span.span_id,key['db-timeout'].span_id);
});
verify('03b newly arrived execution remains unsettled',async call=>{
 const trace=randomBytes(16).toString('hex'),span=randomBytes(8).toString('hex'),now=BigInt(Date.now())*1_000_000n;
 const response=await fetch((process.env.QYL_ANSWER_KEY_OTLP ?? 'http://127.0.0.1:5518')+'/v1/traces',{method:'POST',headers:{'content-type':'application/json','x-qyl-project':fixture.project},body:JSON.stringify({resourceSpans:[{resource:{attributes:[{key:'qyl.project.id',value:{stringValue:fixture.project}},{key:'service.name',value:{stringValue:'arrival-test'}}]},scopeSpans:[{spans:[{traceId:trace,spanId:span,name:'arrival',kind:2,startTimeUnixNano:String(now-1_000_000n),endTimeUnixNano:String(now),status:{code:2}}]}]}]})});assert.equal(response.status,200);
 const data=await call('get_failure_context',{trace_id:trace,lookback_minutes:60});assert.equal(data.settled,false);
});
verify('04 error-group discriminators are eu and service version 2.3',async call=>{
 const data=await call('compare_groups',{...scoped('regional'),signal:'spans',group_a:{matchers:[{key:'error.type',op:'exists'}]},group_b:'rest',attribute_scope:'both'});
 assert.deepEqual(new Set(data.discriminators.slice(0,2).map(x=>`${x.key}=${x.value}`)),new Set(['cloud.region=eu','service.version=2.3']));
});
verify('05 db executions first diverge at db status',async call=>{
 const data=await call('compare_traces',{...w,a:{trace_id:key['db-root'].trace_id},b:{trace_id:key['db-success-root'].trace_id}});
 assert.equal(data.first_divergence.kind,'status');assert.equal(data.first_divergence.a_span_id,key['db-timeout'].span_id);
});
verify('06 four requests after fix need another96',async call=>{
 const F=(await failures(call)).find(f=>f.operation==='checkout');assert(F);
 const data=await call('verify_fix',{...w,failure_id:F.failure_id,since:fixture.T2});assert.equal(data.verdict,'pending');assert.equal(data.needs.requests,96);
 assert.equal(data.change.kind,'since');sameInstant(data.change.time,fixture.T2);
});
verify('06b the fix release 1.4.2 of checkout anchors F and its orders-db root cause at T2; 1.4.1 anchors at T and failed',async call=>{
 const all=(await call('triage',{...w,status:'all',matchers:[{key:'service.name',op:'in',values:['checkout','orders-db'],scope:'resource'}]})).failures;
 const F=all.find(f=>f.service==='checkout'&&f.operation==='checkout'),D=all.find(f=>f.service==='orders-db');assert(F&&D,JSON.stringify(all));
 for (const failure of [F,D]) {
  const fix=await call('verify_fix',{...w,failure_id:failure.failure_id,release:'1.4.2'});
  assert.deepEqual([fix.change.kind,fix.change.service,fix.change.version],['release','checkout','1.4.2'],failure.service);
  sameInstant(fix.change.time,fixture.T2);sameInstant(fix.after.window.start_time,fixture.T2);
  assert.equal(fix.verdict,'pending',failure.service);assert.equal(fix.after.occurrences,0,failure.service);
 }
 assert.equal((await call('verify_fix',{...w,failure_id:F.failure_id,release:'1.4.2'})).needs.requests,96);
 const introduced=await call('verify_fix',{...w,failure_id:F.failure_id,release:'1.4.1'});
 sameInstant(introduced.change.time,fixture.T);assert.equal(introduced.verdict,'failed');
});
verify('07a checkout span p95 is1840ms and cart 210ms, each with its own p50, p95 and p99',async call=>{
 const data=await call('aggregate_telemetry',{...scoped('http'),signal:'spans',group_by:['http.route'],measures:['duration_p95','duration_p50','duration_p99']});
 for (const [route,p95] of [['/checkout',1840],['/cart',210]]) {
  const measures=data.groups.find(g=>g.key['http.route']===route).measures;
  assert(Math.abs(measures.duration_p95-p95)<1,`${route}: ${JSON.stringify(measures)}`);
  assert(measures.duration_p50<measures.duration_p95&&measures.duration_p95<measures.duration_p99,`${route}: ${JSON.stringify(measures)}`);
 }
});
verify('07b MCP aggregate error rate is8/40',async call=>{
 const data=await call('aggregate_telemetry',{...w,signal:'spans',matchers:[{key:'mcp.method.name',op:'eq',value:'tools/call'}],group_by:['gen_ai.tool.name'],measures:['count','error_count','error_rate']});
 const group=data.groups.find(g=>g.key['gen_ai.tool.name']==='search_logs');assert.equal(group.measures.count,40);assert.equal(group.measures.error_rate,.2);
});
verify('08 region cardinality2 includes semantic definition',async call=>{
 const data=await call('discover_attributes',{...scoped('regional'),signal:'resources'});const region=data.attributes.find(a=>a.key==='cloud.region');assert.equal(region.cardinality,2);assert(region.semconv.brief.length>0);
});
verify('09 identical agent call and argument hashes repeated3times',async call=>{
 const data=await call('get_agent_run',{...w,session_id:'agent_run'});assert.equal(data.findings.repeated_calls[0].count,3);
});
verify('10 release1.4.1 first observed atT with F, the fix 1.4.2 atT2 without failures',async call=>{
 const F=(await failures(call)).find(f=>f.operation==='checkout');assert(F);
 const data=await call('list_releases',{...w,service_name:'checkout'});const release=data.releases.find(r=>r.version==='1.4.1');assert.equal(Date.parse(release.first_seen),Date.parse(fixture.T));assert(release.new_failure_ids.includes(F.failure_id));
 const fix=data.releases.find(r=>r.version==='1.4.2');assert(fix,JSON.stringify(data.releases));sameInstant(fix.first_seen,fixture.T2);assert.deepEqual(fix.new_failure_ids,[]);
});
verify('11 checkout calls orders-db with errors',async call=>{
 const data=await call('get_service_map',w);const edge=data.edges.find(e=>e.from==='checkout'&&e.to==='orders-db');assert(edge);assert(edge.error_rate>0);
});
verify('12 silent billing last seen2hours beforeseed',async call=>{
 const data=await call('check_telemetry_health',w);const service=data.silent_services.find(s=>s.service==='billing');assert.equal(Date.parse(service.last_seen),Date.parse(fixture.now)-7_200_000);
});
verify('13 MCP stats preserve denominator, failure class, exampleids, buckets and breakdowns',async call=>{
 assert.equal((await call('get_mcp_stats',{...w,tool_name:'search_logs'})).buckets,undefined,'buckets come only on request');
 const data=await call('get_mcp_stats',{...w,tool_name:'search_logs',buckets:true});const group=data.groups.find(g=>g.name==='search_logs');assert.equal(group.requests,40);assert.equal(group.errors,8);assert.equal(group.error_rate,.2);assert.equal(data.failure_classes.timeout,8);assert(group.example_trace_ids.length>0);
 const tool=data.tools.find(t=>t.name==='search_logs');assert.equal(tool.requests,40);assert.equal(tool.errors,8);assert.equal(tool.error_rate,.2);
 // Seeded durations vary around 20 ms, by at most 8%.
 assert(Math.abs(tool.avg_ms-20)<=1.6&&Math.abs(tool.p95_ms-20)<=1.6&&tool.avg_ms!==tool.p95_ms,JSON.stringify(tool));
 assert.equal(data.totals.requests,40);assert.equal(data.coverage.scan_complete,true);assert.equal(data.coverage.truncated,false);
 assert(data.buckets.length>0&&data.buckets.every(b=>b.requests>0),'only buckets that saw requests');
 assert.equal(data.buckets.reduce((sum,b)=>sum+b.requests,0),40);assert.equal(data.buckets.reduce((sum,b)=>sum+b.errors,0),8);
 assert.equal(data.by_method.find(m=>m.name==='tools/call').requests,40);assert.equal(data.by_transport.find(t=>t.name==='http').requests,40);
 assert.equal(data.by_server.find(s=>s.name===fixture.mcp_server_name).requests,40);
 for (const id of group.example_trace_ids) {
   const trace = await call('get_trace', {...w, trace_id:id});
   assert(trace.trace.spans.some(span => span.status === 'error'), 'example must contain an error');
 }
});
verify('14 active error filter returns only sess_bad',async call=>{
 const data=await call('list_sessions',{...w,active_only:true,errors_only:true});assert.deepEqual(data.sessions.map(s=>s.session_id),['sess_bad']);
});
verify('15 sess_bad errors returns3traces',async call=>{const data=await call('list_traces',{...w,session_id:'sess_bad',errors_only:true});assert.equal(data.traces.length,3);});
verify('16a critical path is rooted under checkout',async call=>{const data=await call('get_trace',{...w,trace_id:key['latency-/checkout-0'].trace_id,selection:'critical_path'});assert(data.trace.spans.every(s=>s.name.includes('/checkout')));assert(data.trace.spans.length>0);});
verify('16b error ancestors include the healthy root',async call=>{const data=await call('get_trace',{...w,trace_id:key['db-root'].trace_id,selection:'errors_with_ancestors'});assert.equal(data.trace.spans.find(s=>s.span_id===key['db-root'].span_id).status,'ok');});
verify('17 grouped timeout log pattern count8',async call=>{const data=await call('search_logs',{...scoped('qyl-mcp'),group_by:['pattern']});assert(data.patterns.some(p=>p.count===8&&/timeout/.test(p.template)));});
verify('18 duration histogram reports seconds and p95support',async call=>{const data=await call('list_metrics',{...w,query:'http.server.request.duration'});const metric=data.items.find(m=>m.name==='http.server.request.duration');assert.equal(metric.kind,'histogram');assert.equal(metric.unit,'s');assert(metric.supported_aggregations.includes('p95'));});
verify('19 metric route cardinality2 suggested for grouping',async call=>{const data=await call('list_metrics',{...w,name_prefix:'http.server.request.duration'});const metric=data.items.find(m=>m.name==='http.server.request.duration');assert.equal(metric.keys.find(k=>k.key==='http.route').cardinality,2);assert(metric.suggested_group_by.includes('http.route'));
 const raw=await call('query_metric',{...w,metric_name:'http.server.request.duration',raw:true,matchers:[{key:'http.route',op:'eq',value:'/checkout'}]});assert.equal(raw.points.length,1);assert.equal(raw.points[0].data.count,'100');});
verify('20a histogram p95 remains seconds and approximate',async call=>{const data=await call('query_metric',{...w,metric_name:'http.server.request.duration',aggregation:'p95',group_by:['http.route'],step_ms:3_600_000});assert.equal(data.unit,'s');assert.equal(data.approximate,true);const checkout=data.series.find(s=>s.attributes.some(a=>a.key==='http.route'&&a.value==='/checkout'));assert(checkout);const values=checkout.buckets.map(b=>b.value).filter(v=>v!==null);assert(values.some(v=>Math.abs(v-1.84)<.1),JSON.stringify(values));});
verify('20b cumulative100,150,190 started in the window increase190',async call=>{const data=await call('query_metric',{...w,metric_name:'requests.total',function:'increase',step_ms:3_600_000});assert.equal(data.series.flatMap(s=>s.buckets).reduce((sum,b)=>sum+(b.value??0),0),fixture.expected.increase);});
verify('21 CI new test failure and build3xregression',async call=>{const data=await call('ci_log',{...w,ci_run_id:'ci_current',baseline:'last_success'});assert.equal(data.ci_run_id,'ci_current');assert(data.comparison.baseline_ci_run_id);assert(data.comparison.new_failures.some(p=>p.leg==='test'));assert(data.comparison.duration_regressions.some(p=>p.leg==='build'&&p.ratio===3));});
verify('24 span discovery keeps integer attribute values whole',async call=>{
 const data=await call('discover_attributes',{...scoped('payments'),signal:'spans',key:'http.response.status_code'});
 const status=data.attributes.find(a=>a.key==='http.response.status_code'&&a.scope==='span');assert(status,JSON.stringify(data.attributes));
 assert.equal(status.type,'int');assert.deepEqual(status.top_values[0].value,{type:'int',value:'500'});assert.equal(status.top_values[0].count,2);
});
verify('22 health finds no seed defects: no semconv gaps, schema URLs, no orphans and no ingestion lag',async call=>{
 const data=await call('check_telemetry_health',w);
 assert.deepEqual(data.semconv_gaps,[],JSON.stringify(data.semconv_gaps));
 assert.deepEqual(data.schema_urls,['https://opentelemetry.io/schemas/1.44.0']);
 assert.equal(data.orphan_spans.count,0);
 assert(data.ingestion_lag_ms<1000,String(data.ingestion_lag_ms));
});
registrations.push('23 fresh ids reseed the same services and sessions without replacing the earlier run');
void test('23 fresh ids reseed the same services and sessions without replacing the earlier run',{concurrency:false},async()=>{
 const project='answer-key-'+randomBytes(8).toString('hex');
 const repo=fileURLToPath(new URL('../..',import.meta.url));
 async function seed(run){
  const output=join(dirname(process.env.QYL_ANSWER_KEY_FILE),`${project}-${run}.json`);
  await promisify(execFile)('python3',[join(repo,'tests/Qyl.Collector.Tests/Fixtures/seed-answer-key.py'),process.env.QYL_ANSWER_KEY_OTLP,output,project],{env:{...process.env,QYL_ANSWER_KEY_FRESH_IDS:'1'}});
  return JSON.parse(await readFile(output,'utf8'));
 }
 const first=await seed(1),second=await seed(2);
 assert.notEqual(first.keys['db-root'].trace_id,second.keys['db-root'].trace_id);
 assert.deepEqual(second.services,first.services);assert.deepEqual(second.sessions,first.sessions);
 await withClient(async call=>{
  const data=await call('list_traces',{...first.window,session_id:'sess_bad',errors_only:true});
  assert.equal(data.traces.length,6,'both runs keep their sess_bad failures');
 },project);
});
registrations.push('25 traffic keeps every seeded service but billing live and every planted answer in place');
void test('25 traffic keeps every seeded service but billing live and every planted answer in place',{concurrency:false},async()=>{
 const project='answer-key-'+randomBytes(8).toString('hex');
 const repo=fileURLToPath(new URL('../..',import.meta.url));
 // The seed ran 100 minutes ago and the 10-minute traffic cron ran ten times since.
 const seedNow=Math.floor(Date.now()/1000)-6000;
 async function send(mode,now){
  const output=join(dirname(process.env.QYL_ANSWER_KEY_FILE),`${project}-${now}.json`);
  await promisify(execFile)('python3',[join(repo,'tests/Qyl.Collector.Tests/Fixtures/seed-answer-key.py'),process.env.QYL_ANSWER_KEY_OTLP,output,project],{env:{...process.env,QYL_ANSWER_KEY_MODE:mode,QYL_ANSWER_KEY_NOW:String(now)}});
  return JSON.parse(await readFile(output,'utf8'));
 }
 const seeded=await send('seed',seedNow);
 for (let run=1;run<=10;run++) await send('traffic',seedNow+run*600);
 await withClient(async call=>{
  const day={lookback_minutes:1440}, scope=(service)=>({...day,matchers:[{key:'service.name',op:'eq',value:service,scope:'resource'}]});
  assert.deepEqual((await call('triage',{start_time:seeded.T,limit:50})).silent_services.map(s=>s.service),['billing']);
  const regional=await call('compare_groups',{...scope('regional'),signal:'spans',group_a:{matchers:[{key:'error.type',op:'exists'}]},group_b:'rest',attribute_scope:'both'});
  assert.deepEqual(new Set(regional.discriminators.slice(0,2).map(x=>`${x.key}=${x.value}`)),new Set(['cloud.region=eu','service.version=2.3']));
  const latency=await call('aggregate_telemetry',{...scope('http'),signal:'spans',group_by:['http.route'],measures:['duration_p95']});
  assert.deepEqual(Object.fromEntries(latency.groups.map(g=>[g.key['http.route'],g.measures.duration_p95])),{'/checkout':1840,'/cart':210});
  assert.equal((await call('get_failure_context',{...day,trace_id:seeded.keys['db-root'].trace_id})).observed.root_cause_span.span_id,seeded.keys['db-timeout'].span_id);
  const F=(await call('triage',{...day,service_name:'checkout',status:'all'})).failures.find(f=>f.operation==='checkout');assert(F);
  const fix=await call('verify_fix',{...day,failure_id:F.failure_id,since:seeded.T2});
  assert.equal(fix.verdict,'pending');assert.equal(fix.needs.requests,76,'the four fix requests and two checkouts per batch count');
  const release=await call('verify_fix',{...day,failure_id:F.failure_id,release:'1.4.2'});
  sameInstant(release.change.time,seeded.T2);assert.equal(release.needs.requests,76,'every later checkout runs the fix release');
  assert.equal((await call('get_agent_run',{...day,session_id:'agent_run'})).findings.repeated_calls[0].count,3);
  const ci=(await call('ci_log',{...day,ci_run_id:'ci_current',baseline:'last_success'})).comparison;
  assert(ci.new_failures.some(p=>p.leg==='test'));assert(ci.duration_regressions.some(p=>p.leg==='build'&&p.ratio===3));
  const search=(await call('get_mcp_stats',{...day,tool_name:'search_logs'})).tools.find(t=>t.name==='search_logs');
  assert.deepEqual([search.requests,search.errors],[40,8]);
  assert.deepEqual((await call('list_sessions',{...day,errors_only:true})).sessions.map(s=>s.session_id).sort(),['ci_current','sess_bad']);
  assert.deepEqual((await call('check_telemetry_health',day)).semconv_gaps,[]);
 },project);
});
registrations.push('26 the fix in 1.4.2 is pending, then passes once a hundred checkouts ran it');
void test('26 the fix in 1.4.2 is pending, then passes once a hundred checkouts ran it',{concurrency:false},async()=>{
 const project='answer-key-'+randomBytes(8).toString('hex');
 const repo=fileURLToPath(new URL('../..',import.meta.url));
 // The seed ran eight hours ago; the 10-minute traffic cron ran 48 times since, two checkouts each.
 const seedNow=Math.floor(Date.now()/1000)-48*600-60;
 async function send(mode,now){
  const output=join(dirname(process.env.QYL_ANSWER_KEY_FILE),`${project}-${now}.json`);
  await promisify(execFile)('python3',[join(repo,'tests/Qyl.Collector.Tests/Fixtures/seed-answer-key.py'),process.env.QYL_ANSWER_KEY_OTLP,output,project],{env:{...process.env,QYL_ANSWER_KEY_MODE:mode,QYL_ANSWER_KEY_NOW:String(now)}});
  return JSON.parse(await readFile(output,'utf8'));
 }
 const seeded=await send('seed',seedNow);
 // Each batch is independent of the others; eight run at a time.
 async function batches(from,to){for (let run=from;run<=to;run+=8) await Promise.all(Array.from({length:Math.min(8,to-run+1)},(_,index)=>send('traffic',seedNow+(run+index)*600)));}
 await withClient(async call=>{
  const F=(await call('triage',{lookback_minutes:1440,service_name:'checkout',status:'all'})).failures.find(f=>f.operation==='checkout');assert(F);
  const verify=()=>call('verify_fix',{lookback_minutes:1440,failure_id:F.failure_id,release:'1.4.2'});
  await batches(1,47);
  const pending=await verify();
  assert.equal(pending.verdict,'pending');assert.deepEqual(pending.needs,{requests:2,minutes:0});sameInstant(pending.change.time,seeded.T2);
  await batches(48,48);
  const passed=await verify();
  assert.equal(passed.verdict,'passed',JSON.stringify(passed));assert.deepEqual([passed.after.occurrences,passed.after.requests],[0,100]);
 },project);
});
verifyIsolated('27 annotation on F read back as count1',async call=>{const F=(await failures(call)).find(f=>f.operation==='checkout');assert(F);await call('annotate',{target:{kind:'failure',failure_id:F.failure_id},text:'Answer-key finding',classification:'finding',labels:['answer-key'],idempotency_key:'answer-key-finding'});assert.equal((await failures(call)).find(f=>f.failure_id===F.failure_id).annotation_count,1);const context=await call('get_failure_context',{...w,failure_id:F.failure_id});assert(context.context.annotations.some(a=>a.text==='Answer-key finding'));});
verifyIsolated('32a ignored F suppressed fromtriage',async call=>{const F=(await failures(call)).find(f=>f.operation==='checkout');assert(F);const saved=await call('update_failure_status',{failure_id:F.failure_id,etag:F.etag,status:'ignored'});assert.equal(saved.failure.status,'ignored');try { assert(!(await call('triage',w)).failures.some(f=>f.failure_id===F.failure_id)); } finally { await call('update_failure_status',{failure_id:F.failure_id,etag:saved.failure.etag,status:'open'}); }});
verifyIsolated('32b resolved F recurrence markedregressed',async call=>{
 const F=(await failures(call)).find(f=>f.operation==='checkout');assert(F);const resolved=await call('update_failure_status',{failure_id:F.failure_id,etag:F.etag,status:'resolved'});
 // The recurrence starts a millisecond before now and must still follow the resolution, which the Collector stamped
 // to 100 ns just before it answered; waiting a few milliseconds keeps a fast answer from racing it.
 await new Promise(resolve=>setTimeout(resolve,5));
 const now=BigInt(Date.now())*1_000_000n;
 const response=await fetch((process.env.QYL_ANSWER_KEY_OTLP??'http://127.0.0.1:5518')+'/v1/traces',{method:'POST',headers:{'content-type':'application/json','x-qyl-project':process.env.QYL_PROJECT},body:JSON.stringify({resourceSpans:[{resource:{attributes:[{key:'qyl.project.id',value:{stringValue:process.env.QYL_PROJECT}},{key:'service.name',value:{stringValue:'checkout'}},{key:'service.version',value:{stringValue:'1.4.1'}}]},scopeSpans:[{spans:[{traceId:randomBytes(16).toString('hex'),spanId:randomBytes(8).toString('hex'),name:'checkout',kind:2,startTimeUnixNano:String(now-1_000_000n),endTimeUnixNano:String(now),status:{code:2,message:'dependency failed'},attributes:[{key:'error.type',value:{stringValue:'downstream'}}]}]}]}]})});assert.equal(response.status,200);
 try { const data=await call('triage',{lookback_minutes:60});assert.equal(data.failures.find(f=>f.failure_id===F.failure_id).change,'regressed'); } finally { await call('update_failure_status',{failure_id:F.failure_id,etag:resolved.failure.etag,status:'open'}); }
});


// Existing projection and paging semantics stay exercised against real storage.
verify('regression get_trace removes all attribute collections without changing totals', async call=>{
 const args={...w,trace_id:key['db-root'].trace_id};
 const complete=await call('get_trace',args);
 const compact=await call('get_trace',{...args,include_attributes:false,max_spans:1});
 assert.equal(compact.trace.span_count,complete.trace.span_count);
 assert.equal(compact.trace.duration_ms,complete.trace.duration_ms);
 assert.equal(compact.trace.spans.length,1);
 assert(!JSON.stringify(compact.trace).includes('"attributes"'));
 const repeated=await call('get_trace',args);assert.deepEqual(repeated.trace,complete.trace);
});
verify('regression get_trace filters errors before capping and returns empty matches honestly', async call=>{
 const selected=await call('get_trace',{...w,trace_id:key['db-root'].trace_id,selection:'errors',max_spans:1});
 assert.equal(selected.trace.spans.length,1);assert.equal(selected.trace.spans[0].status,'error');
 const empty=await call('get_trace',{...w,trace_id:key['db-success-root'].trace_id,selection:'errors'});
 assert.deepEqual(empty.trace.spans,[]);assert(empty.trace.span_count>0);
});
verify('regression pagination filters before issuing opaque cursors', async call=>{
 const args={...w,session_id:'sess_bad',errors_only:true,limit:1};
 const first=await call('list_traces',args);assert.equal(first.traces.length,1);assert(first.has_more);assert(first.next_cursor);
 const second=await call('list_traces',{...args,cursor:first.next_cursor});
 assert.equal(second.traces.length,1);assert.notEqual(first.traces[0].trace_id,second.traces[0].trace_id);
 assert(first.traces[0].error_count>0 && second.traces[0].error_count>0);
});

console.log(`Registered ${registrations.length} answer-key Verify checks through real MCP calls.`);
