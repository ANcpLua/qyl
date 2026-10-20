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
import { collectorHeaders } from './dist/config.js';
import { QylServiceValues } from './dist/contracts/generated/api.js';
import documents from './dist/contracts/generated/tool-schemas.json' with { type: 'json' };
const fixture = JSON.parse(await readFile(process.env.QYL_ANSWER_KEY_FILE ?? new URL('missing-answer-key-use-run-answer-key', import.meta.url),'utf8'));
process.env.QYL_COLLECTOR_URL = process.env.QYL_ANSWER_KEY_COLLECTOR ?? 'http://127.0.0.1:5510';
process.env.QYL_PROJECT = fixture.project;
delete process.env.QYL_API_KEY;
delete process.env.MCP_COLLECTOR_PROJECTS;
const w=fixture.window, key=fixture.keys, expected=fixture.expected;
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
   // A write answers with the record it changed; a verification's before and after windows are its bounds, and every
   // other read reports the window it applied.
   if (!['annotate','save_query','update_failure_status','delete_record'].includes(name)) {
     if (name!=='verify_fix') assert(result.structuredContent.window, name+' omits absolute window');
     assert(result.structuredContent.coverage, name+' omits coverage');
   }
   return result.structuredContent;
 },client);} finally {await client.close();await handler.close();process.env.QYL_PROJECT=previousProject;}
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
verify('04b group_b previous_window is group A over the window before it: the failing release discriminates',async call=>{
 // From T to T2 checkout runs the failing release; in the window of equal length before T it ran the previous one, once.
 const data=await call('compare_groups',{...scoped('checkout'),signal:'spans',group_a:{window:{start_time:fixture.T,end_time:fixture.T2}},group_b:'previous_window'});
 assert.deepEqual([data.discriminators[0].key,data.discriminators[0].value],['service.version',fixture.releases.checkout.failing],JSON.stringify(data.discriminators));
 assert.equal(data.size_b,1);
});
verify('05 db executions first diverge at db status',async call=>{
 const data=await call('compare_traces',{...w,a:{trace_id:key['db-root'].trace_id},b:{trace_id:key['db-success-root'].trace_id}});
 assert.equal(data.first_divergence.kind,'status');assert.equal(data.first_divergence.a_span_id,key['db-timeout'].span_id);
});
verify('06 four requests after fix need another96',async call=>{
 const F=(await failures(call)).find(f=>f.operation==='checkout');assert(F);
 const data=await call('verify_fix',{...w,failure_id:F.failure_id,since:fixture.T2});assert.equal(data.verdict,'pending');assert.equal(data.needs.requests,expected.fix_requests_needed);
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
 assert.equal((await call('verify_fix',{...w,failure_id:F.failure_id,release:'1.4.2'})).needs.requests,expected.fix_requests_needed);
 const introduced=await call('verify_fix',{...w,failure_id:F.failure_id,release:'1.4.1'});
 sameInstant(introduced.change.time,fixture.T);assert.equal(introduced.verdict,'failed');
});
verify('06c a verify_id repeats the verification, min_minutes sets the wait, and an after window without requests is inconclusive',async call=>{
 const F=(await failures(call)).find(f=>f.operation==='checkout');assert(F);
 const first=await call('verify_fix',{...w,failure_id:F.failure_id,since:fixture.T2});
 assert.equal(first.verdict,'pending');assert(first.needs.minutes>0,JSON.stringify(first.needs));
 const repeat=await call('verify_fix',{verify_id:first.verify_id});
 assert.deepEqual([repeat.verdict,repeat.change,repeat.before,repeat.after.requests,repeat.needs.requests],[first.verdict,first.change,first.before,first.after.requests,first.needs.requests]);
 assert.deepEqual((await call('verify_fix',{...w,failure_id:F.failure_id,since:fixture.T2,min_minutes:1})).needs,{requests:expected.fix_requests_needed,minutes:0});
 // No checkout ran after the seed: with no minimum to wait for, nothing decides.
 const unanswered=await call('verify_fix',{...w,failure_id:F.failure_id,since:fixture.now,min_requests:0,min_minutes:0});
 assert.deepEqual([unanswered.verdict,unanswered.after.requests],['inconclusive',0]);
});
verify('07a checkout span p95 is1840ms and cart 210ms, each with its own p50, p95 and p99',async call=>{
 const data=await call('aggregate_telemetry',{...scoped('http'),signal:'spans',group_by:['http.route'],measures:['duration_p95','duration_p50','duration_p99']});
 for (const [route,p95] of Object.entries(expected.p95_ms)) {
  const measures=data.groups.find(g=>g.key['http.route']===route).measures;
  assert(Math.abs(measures.duration_p95-p95)<1,`${route}: ${JSON.stringify(measures)}`);
  assert(measures.duration_p50<measures.duration_p95&&measures.duration_p95<measures.duration_p99,`${route}: ${JSON.stringify(measures)}`);
 }
});
verify('07b MCP aggregate error rate is8/40',async call=>{
 const data=await call('aggregate_telemetry',{...w,signal:'spans',matchers:[{key:'mcp.method.name',op:'eq',value:'tools/call'}],group_by:['gen_ai.tool.name'],measures:['count','error_count','error_rate']});
 const group=data.groups.find(g=>g.key['gen_ai.tool.name']==='search_logs');assert.equal(group.measures.count,expected.mcp_requests);assert.equal(group.measures.error_rate,expected.error_rate);
});
verify('08 region cardinality2 includes semantic definition',async call=>{
 const data=await call('discover_attributes',{...scoped('regional'),signal:'resources'});const region=data.attributes.find(a=>a.key==='cloud.region');assert.equal(region.cardinality,2);assert(region.semconv.brief.length>0);
});
verify('09 identical agent call and argument hashes repeated3times',async call=>{
 const data=await call('get_agent_run',{...w,session_id:'agent_run'});assert.equal(data.findings.repeated_calls[0].count,3);
});
verify('10 release1.4.1 first observed atT with F, the fix 1.4.2 atT2 without failures',async call=>{
 // Without QYL_ANSWER_KEY_FRESH_IDS the releases keep their fixed names.
 assert.deepEqual(fixture.releases,{checkout:{previous:'1.4.0',failing:'1.4.1',fix:'1.4.2'},regional:{previous:'2.2',failing:'2.3'}});
 const F=(await failures(call)).find(f=>f.operation==='checkout');assert(F);
 const data=await call('list_releases',{...w,service_name:'checkout'});assert.deepEqual(data.releases.map(r=>r.version),['1.4.2','1.4.1','1.4.0']);
 const release=data.releases.find(r=>r.version==='1.4.1');assert.equal(Date.parse(release.first_seen),Date.parse(fixture.T));assert(release.new_failure_ids.includes(F.failure_id));
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
 const data=await call('get_mcp_stats',{...w,tool_name:'search_logs',buckets:true});const group=data.groups.find(g=>g.name==='search_logs');assert.equal(group.requests,expected.mcp_requests);assert.equal(group.errors,expected.mcp_errors);assert.equal(group.error_rate,expected.error_rate);assert.equal(data.failure_classes.timeout,expected.mcp_errors);assert(group.example_trace_ids.length>0);
 const tool=data.tools.find(t=>t.name==='search_logs');assert.equal(tool.requests,expected.mcp_requests);assert.equal(tool.errors,expected.mcp_errors);assert.equal(tool.error_rate,expected.error_rate);
 // Seeded durations vary around 20 ms, by at most 8%.
 assert(Math.abs(tool.avg_ms-20)<=1.6&&Math.abs(tool.p95_ms-20)<=1.6&&tool.avg_ms!==tool.p95_ms,JSON.stringify(tool));
 assert.equal(data.totals.requests,expected.mcp_requests);assert.equal(data.coverage.scan_complete,true);assert.equal(data.coverage.truncated,false);
 assert(data.buckets.length>0&&data.buckets.every(b=>b.requests>0),'only buckets that saw requests');
 assert.equal(data.buckets.reduce((sum,b)=>sum+b.requests,0),expected.mcp_requests);assert.equal(data.buckets.reduce((sum,b)=>sum+b.errors,0),expected.mcp_errors);
 assert.equal(data.by_method.find(m=>m.name==='tools/call').requests,expected.mcp_requests);assert.equal(data.by_transport.find(t=>t.name==='http').requests,expected.mcp_requests);
 assert.equal(data.by_server.find(s=>s.name===fixture.mcp_server_name).requests,expected.mcp_requests);
 for (const id of group.example_trace_ids) {
   const trace = await call('get_trace', {...w, trace_id:id});
   assert(trace.trace.spans.some(span => span.status === 'error'), 'example must contain an error');
 }
});
verify('13b a previous_window baseline holds the MCP calls before the window',async call=>{
 // The search_logs calls start a millisecond apart from T, the first ones timing out: a window from T+20 ms to T+40 ms
 // holds the last twenty, and the window before it the first twenty with every timeout.
 const at=Date.parse(fixture.T), window={start_time:new Date(at+20).toISOString(),end_time:new Date(at+40).toISOString()};
 const data=await call('get_mcp_stats',{...window,tool_name:'search_logs',baseline:'previous_window'});
 sameInstant(data.comparison.window.start_time,fixture.T);sameInstant(data.comparison.window.end_time,window.start_time);
 assert.deepEqual([data.totals.requests,data.totals.errors],[20,0]);
 assert.deepEqual([data.comparison.totals.requests,data.comparison.totals.errors,data.comparison.failure_classes.timeout],[expected.mcp_requests-20,expected.mcp_errors,expected.mcp_errors]);
});
verify('14 active error filter returns only sess_bad',async call=>{
 const data=await call('list_sessions',{...w,active_only:true,errors_only:true});assert.deepEqual(data.sessions.map(s=>s.session_id),['sess_bad']);
});
verify('15 sess_bad errors returns3traces',async call=>{const data=await call('list_traces',{...w,session_id:'sess_bad',errors_only:true});assert.equal(data.traces.length,3);});
verify('16a critical path is rooted under checkout',async call=>{const data=await call('get_trace',{...w,trace_id:key['latency-/checkout-0'].trace_id,selection:'critical_path'});assert(data.trace.spans.every(s=>s.name.includes('/checkout')));assert(data.trace.spans.length>0);});
verify('16b error ancestors include the healthy root',async call=>{const data=await call('get_trace',{...w,trace_id:key['db-root'].trace_id,selection:'errors_with_ancestors'});assert.equal(data.trace.spans.find(s=>s.span_id===key['db-root'].span_id).status,'ok');});
verify('17 grouped timeout log pattern count8',async call=>{const data=await call('search_logs',{...scoped('qyl-mcp'),group_by:['pattern']});assert(data.patterns.some(p=>p.count===expected.mcp_errors&&/timeout/.test(p.template)));});
verify('18 duration histogram reports seconds and p95support',async call=>{const data=await call('list_metrics',{...w,query:'http.server.request.duration'});const metric=data.items.find(m=>m.name==='http.server.request.duration');assert.equal(metric.kind,'histogram');assert.equal(metric.unit,'s');assert(metric.supported_aggregations.includes('p95'));});
verify('19 metric route cardinality2 suggested for grouping',async call=>{const data=await call('list_metrics',{...w,name_prefix:'http.server.request.duration'});const metric=data.items.find(m=>m.name==='http.server.request.duration');assert.equal(metric.keys.find(k=>k.key==='http.route').cardinality,2);assert(metric.suggested_group_by.includes('http.route'));
 const raw=await call('query_metric',{...w,metric_name:'http.server.request.duration',raw:true,matchers:[{key:'http.route',op:'eq',value:'/checkout'}]});assert.equal(raw.points.length,1);assert.equal(raw.points[0].data.count,'100');});
verify('20a histogram p95 remains seconds and approximate',async call=>{const data=await call('query_metric',{...w,metric_name:'http.server.request.duration',aggregation:'p95',group_by:['http.route'],step_ms:3_600_000});assert.equal(data.unit,'s');assert.equal(data.approximate,true);const checkout=data.series.find(s=>s.attributes.some(a=>a.key==='http.route'&&a.value==='/checkout'));assert(checkout);const values=checkout.buckets.map(b=>b.value).filter(v=>v!==null);assert(values.some(v=>Math.abs(v-expected.p95_ms['/checkout']/1000)<.1),JSON.stringify(values));});
verify('20b cumulative100,150,190 started in the window increase190',async call=>{const data=await call('query_metric',{...w,metric_name:'requests.total',function:'increase',step_ms:3_600_000});assert.equal(data.series.flatMap(s=>s.buckets).reduce((sum,b)=>sum+(b.value??0),0),expected.increase);});
verify('20c a previous_window baseline holds the counter increase before the window',async call=>{
 // The counter recorded 100 at T, 150 and 190 one and two minutes later: a window from T+30 s to T+150 s holds the
 // last two points, 90 of the increase, and the window of equal length before it the first, the other 100.
 const at=Date.parse(fixture.T), window={start_time:new Date(at+30_000).toISOString(),end_time:new Date(at+150_000).toISOString()};
 const data=await call('query_metric',{...window,metric_name:'requests.total',function:'increase',baseline:'previous_window'});
 sameInstant(data.comparison.window.start_time,new Date(at-90_000).toISOString());sameInstant(data.comparison.window.end_time,window.start_time);
 assert.deepEqual([data.series[0].value,data.comparison.series[0].value],[90,expected.increase-90]);
});
verify('20d ratio_of divides each route\'s histogram sum by its count, the mean request duration',async call=>{
 const args={...w,metric_name:'http.server.request.duration',group_by:['http.route'],step_ms:3_600_000};
 const value=(data,route)=>data.series.find(s=>s.attributes.some(a=>a.key==='http.route'&&a.value===route)).value;
 const sum=await call('query_metric',{...args,aggregation:'sum'}),count=await call('query_metric',{...args,aggregation:'count'});
 const mean=await call('query_metric',{...args,aggregation:'sum',ratio_of:{metric_name:'http.server.request.duration',aggregation:'count'}});
 assert.equal(mean.unit,'1');
 for (const route of Object.keys(expected.p95_ms)) assert.equal(value(mean,route),value(sum,route)/value(count,route),route);
});
verify('21 CI new test failure and build3xregression',async call=>{const data=await call('ci_log',{...w,ci_run_id:'ci_current',baseline:'last_success'});assert.equal(data.ci_run_id,'ci_current');assert(data.comparison.baseline_ci_run_id);assert(data.comparison.new_failures.some(p=>p.leg==='test'));assert(data.comparison.duration_regressions.some(p=>p.leg==='build'&&p.ratio===3));});
verify('21b a ci_run_id baseline compares with that run, even a later failed one',async call=>{
 const data=await call('ci_log',{...w,ci_run_id:'ci_baseline',baseline:{ci_run_id:'ci_current'}});
 assert.equal(data.comparison.baseline_ci_run_id,'ci_current');
 assert(data.comparison.fixed.some(p=>p.leg==='test'),JSON.stringify(data.comparison));assert.deepEqual(data.comparison.duration_regressions,[]);
});
verify('21c a run\'s phases page 50 at a time, whatever limit pages the runs',async call=>{
 const data=await call('ci_log',{...w,ci_run_id:'ci_current',limit:1});
 assert.equal(data.runs.length,1);
 assert.deepEqual(data.phases.map(p=>p.leg).toSorted(),['build','test']);
 assert.equal(data.next_phase_cursor,undefined);
});
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
  assert.deepEqual(Object.fromEntries(latency.groups.map(g=>[g.key['http.route'],g.measures.duration_p95])),seeded.expected.p95_ms);
  assert.equal((await call('get_failure_context',{...day,trace_id:seeded.keys['db-root'].trace_id})).observed.root_cause_span.span_id,seeded.keys['db-timeout'].span_id);
  const F=(await call('triage',{...day,service_name:'checkout',status:'all'})).failures.find(f=>f.operation==='checkout');assert(F);
  const fix=await call('verify_fix',{...day,failure_id:F.failure_id,since:seeded.T2});
  assert.equal(fix.verdict,'pending');assert.equal(fix.needs.requests,seeded.expected.fix_requests_needed-2*10,'the four fix requests and two checkouts per batch count');
  const release=await call('verify_fix',{...day,failure_id:F.failure_id,release:'1.4.2'});
  sameInstant(release.change.time,seeded.T2);assert.equal(release.needs.requests,seeded.expected.fix_requests_needed-2*10,'every later checkout runs the fix release');
  assert.deepEqual((await call('list_releases',{...day,service_name:'checkout'})).releases.map(r=>r.version),['1.4.2','1.4.1','1.4.0'],'traffic runs 1.4.2 and no release of its own');
  assert.equal((await call('get_agent_run',{...day,session_id:'agent_run'})).findings.repeated_calls[0].count,3);
  const ci=(await call('ci_log',{...day,ci_run_id:'ci_current',baseline:'last_success'})).comparison;
  assert(ci.new_failures.some(p=>p.leg==='test'));assert(ci.duration_regressions.some(p=>p.leg==='build'&&p.ratio===3));
  const search=(await call('get_mcp_stats',{...day,tool_name:'search_logs'})).tools.find(t=>t.name==='search_logs');
  assert.deepEqual([search.requests,search.errors],[seeded.expected.mcp_requests,seeded.expected.mcp_errors]);
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
  assert.equal(pending.verdict,'pending');assert.deepEqual(pending.needs,{requests:seeded.expected.fix_requests_needed-2*47,minutes:0});sameInstant(pending.change.time,seeded.T2);
  await batches(48,48);
  const passed=await verify();
  assert.equal(passed.verdict,'passed',JSON.stringify(passed));assert.deepEqual([passed.after.occurrences,passed.after.requests],[0,100]);
 },project);
});
registrations.push('28 reseeds every 12 hours name their releases by the slot: any day-long window holds the latest reseed\'s failure, baseline, silent billing and fix');
void test('28 reseeds every 12 hours name their releases by the slot: any day-long window holds the latest reseed\'s failure, baseline, silent billing and fix',{concurrency:false},async()=>{
 const project='answer-key-'+randomBytes(8).toString('hex');
 const repo=fileURLToPath(new URL('../..',import.meta.url));
 // The hosted reseed runs every 12 hours, at 00:00 and 12:00 UTC. Four ran in a row, the latest a whole slot before the
 // current one, so its window ending 11 h 50 min later has passed too; four put billing's four reseeded hours into the
 // 48 hours a day-long triage weighs.
 const slot=43_200, latest=Math.floor(Date.now()/1000/slot)*slot-slot, reseeds=[3,2,1,0].map(back=>latest-back*slot);
 const named=(at,build)=>{const time=new Date(at*1000).toISOString();return `${time.slice(0,10).replaceAll('-','.')}.${time.slice(11,13)}.${build}`;};
 const hosted=at=>({checkout:{previous:named(at,0),failing:named(at,1),fix:named(at,2)},regional:{previous:named(at,2),failing:named(at,3)}});
 async function send(mode,now){
  const output=join(dirname(process.env.QYL_ANSWER_KEY_FILE),`${project}-${mode}-${now}.json`);
  await promisify(execFile)('python3',[join(repo,'tests/Qyl.Collector.Tests/Fixtures/seed-answer-key.py'),process.env.QYL_ANSWER_KEY_OTLP,output,project],{env:{...process.env,QYL_ANSWER_KEY_FRESH_IDS:'1',QYL_ANSWER_KEY_MODE:mode,QYL_ANSWER_KEY_NOW:String(now)}});
  return JSON.parse(await readFile(output,'utf8'));
 }
 // Traffic runs the releases of the slot ten minutes back: at 12:00 and 12:05 still the 00:00 reseed's, from 12:10 the
 // 12:00 one's. After the latest reseed it runs at its start, 5, 10 and 15 minutes in, 6 hours and 11 h 50 min in.
 const seeded=[], after=[0,300,600,900,21_600,42_600];
 for (const at of reseeds) {
  seeded.push(await send('seed',at));
  for (const offset of at===latest?after:[600,21_600]) {
   const batch=await send('traffic',at+offset);
   assert.deepEqual(batch.releases,hosted(Math.floor((at+offset-600)/slot)*slot),batch.now);
  }
 }
 assert.deepEqual(seeded.map(s=>s.releases),reseeds.map(hosted));
 const [previous,newest]=seeded.slice(-2), {checkout,regional}=newest.releases;
 await withClient(async call=>{
  // A reviewer's 1440 minutes ending 30 minutes, 6 hours or 11 h 50 min after the latest reseed hold it whole: the
  // checkout failure on its failing release, the baseline release the failure followed, billing silent, and the fix as
  // the latest checkout release, which verify_fix anchors on.
  for (const end of [latest+1800,latest+21_600,latest+42_600]) {
   const day={lookback_minutes:1440,end_time:new Date(end*1000).toISOString()};
   const triage=await call('triage',{...day,limit:50});
   const F=triage.failures.find(f=>f.service==='checkout'&&f.operation==='checkout');assert(F,day.end_time);
   assert.equal(F.coinciding_release?.version,checkout.failing,day.end_time);
   const baseline=triage.releases.find(r=>r.service==='checkout'&&r.version===checkout.previous);assert(baseline,day.end_time);
   sameInstant(baseline.first_seen,new Date((latest-80*60)*1000).toISOString());
   assert(triage.silent_services.some(s=>s.service==='billing'),day.end_time);
   const releases=(await call('list_releases',{...day,service_name:'checkout'})).releases;
   assert.equal(releases[0].version,checkout.fix,day.end_time);
   const fix=await call('verify_fix',{...day,failure_id:F.failure_id,release:releases[0].version});
   assert.deepEqual([fix.change.kind,fix.change.service,fix.change.version,fix.after.occurrences],['release','checkout',checkout.fix,0],day.end_time);
   sameInstant(fix.change.time,newest.T2);
  }
  const reviewer={lookback_minutes:1440,end_time:new Date((latest+42_600)*1000).toISOString()};
  const F=(await call('triage',{...reviewer,service_name:'checkout'})).failures.find(f=>f.operation==='checkout');assert(F);
  // The four fix requests and two checkouts per later traffic run count, the two runs still on the previous releases too.
  const latestFix=await call('verify_fix',{...reviewer,failure_id:F.failure_id,release:checkout.fix});
  assert.equal(latestFix.verdict,'pending',JSON.stringify(latestFix));assert.deepEqual(latestFix.needs,{requests:newest.expected.fix_requests_needed-2*after.length,minutes:0});
  // The previous slot's fix is the one the latest reseed's failures followed.
  const previousFix=await call('verify_fix',{...reviewer,failure_id:F.failure_id,release:previous.releases.checkout.fix});
  sameInstant(previousFix.change.time,previous.T2);assert.equal(previousFix.verdict,'failed');
  // Over the default week the four slots' releases stay apart, the latest first.
  const week=(await call('list_releases',{service_name:'checkout'})).releases.map(r=>r.version);
  assert.deepEqual(week,seeded.toReversed().flatMap(s=>[s.releases.checkout.fix,s.releases.checkout.failing,s.releases.checkout.previous]));
  const groups=await call('compare_groups',{...reviewer,matchers:[{key:'service.name',op:'eq',value:'regional',scope:'resource'}],signal:'spans',group_a:{matchers:[{key:'error.type',op:'exists'}]},group_b:'rest',attribute_scope:'both'});
  assert.deepEqual(new Set(groups.discriminators.slice(0,2).map(x=>`${x.key}=${x.value}`)),new Set(['cloud.region=eu',`service.version=${regional.failing}`]));
 },project);
});
verifyIsolated('27 annotation on F read back as count1; repeated, its create answers it as it is now and after a delete makes a new one',async call=>{
 const F=(await failures(call)).find(f=>f.operation==='checkout');assert(F);
 // No idempotency_key: the server derives one from the body, so every repeat below is the same create.
 const create={target:{kind:'failure',failure_id:F.failure_id},text:'Answer-key finding',classification:'finding',labels:['answer-key']};
 const first=(await call('annotate',create)).annotation;
 assert.equal((await failures(call)).find(f=>f.failure_id===F.failure_id).annotation_count,1);
 assert((await call('get_failure_context',{...w,failure_id:F.failure_id})).context.annotations.some(a=>a.text==='Answer-key finding'));
 const changed=(await call('annotate',{id:first.id,etag:first.etag,text:'Answer-key finding, confirmed'})).annotation;
 const repeated=(await call('annotate',create)).annotation;
 assert.deepEqual([repeated.id,repeated.etag,repeated.text],[first.id,changed.etag,'Answer-key finding, confirmed']);
 assert.equal((await call('delete_record',{kind:'annotation',id:first.id,etag:changed.etag})).deleted,true);
 const again=(await call('annotate',create)).annotation;
 assert.notEqual(again.id,first.id);
 assert((await call('get_failure_context',{...w,failure_id:F.failure_id})).context.annotations.some(a=>a.id===again.id));
 assert.equal((await call('delete_record',{kind:'annotation',id:again.id,etag:again.etag})).deleted,true);
});
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
verifyIsolated('29 saved queries are created, changed, listed, run and deleted; a repeated create answers one as it is now and after a delete makes a new one',async call=>{
 const tracesQuery={name:'checkout traces',filter:{signal:'traces',service_name:'checkout',lookback_minutes:60},idempotency_key:'answer-key-traces'};
 const metricsQuery={name:'requests per window',filter:{signal:'metrics',metric_name:'requests.total',lookback_minutes:60}};
 const traces=await call('save_query',tracesQuery);
 const metrics=await call('save_query',metricsQuery);
 const changed=await call('save_query',{id:traces.saved_query.id,etag:traces.saved_query.etag,description:'The planted checkout incident'});
 assert.equal(changed.saved_query.description,'The planted checkout incident');assert.notEqual(changed.saved_query.etag,traces.saved_query.etag);
 const repeated=(await call('save_query',tracesQuery)).saved_query;
 assert.deepEqual([repeated.id,repeated.etag,repeated.description],[traces.saved_query.id,changed.saved_query.etag,'The planted checkout incident']);
 const listed=await call('run_saved_query',{});
 assert.deepEqual(listed.saved_queries.map(q=>[q.name,q.etag]),[['checkout traces',changed.saved_query.etag],['requests per window',metrics.saved_query.etag]]);
 assert((await call('run_saved_query',{id:changed.saved_query.id})).traces.some(t=>t.trace_id===key['db-root'].trace_id));
 const answer=(await call('run_saved_query',{id:metrics.saved_query.id})).metric;
 assert.deepEqual([answer.name,answer.aggregation,answer.series.length],['requests.total','avg',1]);
 for (const {saved_query:query} of [changed,metrics]) assert.equal((await call('delete_record',{kind:'saved_query',id:query.id,etag:query.etag})).deleted,true);
 assert.deepEqual((await call('run_saved_query',{})).saved_queries,[]);
 // The metrics query's create took the key the server derives from its body; deleted, the same create makes a new one.
 const again=(await call('save_query',metricsQuery)).saved_query;
 assert.notEqual(again.id,metrics.saved_query.id);
 assert.equal((await call('run_saved_query',{id:again.id})).metric.name,'requests.total');
 assert.deepEqual((await call('run_saved_query',{})).saved_queries.map(q=>q.id),[again.id]);
 assert.equal((await call('delete_record',{kind:'saved_query',id:again.id,etag:again.etag})).deleted,true);
});
verifyIsolated('30 triage and health leave the qyl MCP server\'s own telemetry out unless service_name names it',async call=>{
 // The server exports its own spans into the project it reads: one call failed 20 minutes ago.
 const own=QylServiceValues.mcp, start=BigInt(Date.now()-20*60_000)*1_000_000n;
 const response=await fetch((process.env.QYL_ANSWER_KEY_OTLP??'http://127.0.0.1:5518')+'/v1/traces',{method:'POST',headers:{'content-type':'application/json','x-qyl-project':process.env.QYL_PROJECT},body:JSON.stringify({resourceSpans:[{resource:{attributes:[{key:'qyl.project.id',value:{stringValue:process.env.QYL_PROJECT}},{key:'service.name',value:{stringValue:own}}]},scopeSpans:[{spans:[{traceId:randomBytes(16).toString('hex'),spanId:randomBytes(8).toString('hex'),name:'tools/call triage',kind:2,startTimeUnixNano:String(start),endTimeUnixNano:String(start+1_000_000n),status:{code:2,message:'tool failed'},attributes:[{key:'mcp.method.name',value:{stringValue:'tools/call'}},{key:'error.type',value:{stringValue:'tool_error'}}]}]}]}]})});assert.equal(response.status,200);
 const services=items=>items.map(item=>item.service);
 const triage=await call('triage',{lookback_minutes:60});
 assert(!services(triage.failures).includes(own)&&!services(triage.silent_services).includes(own),JSON.stringify(triage));
 assert(services(triage.failures).includes('qyl-mcp'),'the seeded MCP server is a service like any other');
 const named=await call('triage',{lookback_minutes:60,service_name:own});
 assert.deepEqual([services(named.failures),services(named.silent_services)],[[own],[own]]);
 assert(!services((await call('check_telemetry_health',{lookback_minutes:60})).silent_services).includes(own));
 const health=await call('check_telemetry_health',{lookback_minutes:60,service_name:own});
 assert.deepEqual(services(health.silent_services),[own]);
 // How far back the data reaches follows the same scope: named, the server's one call.
 sameInstant(health.data_span.oldest,new Date(Number(start/1_000_000n)).toISOString());
 assert.equal((await call('get_mcp_stats',{lookback_minutes:60,server_name:own})).totals.requests,1,'get_mcp_stats keeps it');
});
// Each analysis tool's required arguments, so that one bounded argument at a time is the only one out of its range.
const analysisArguments={triage:{},get_failure_context:{},compare_groups:{signal:'spans',group_a:{failure_id:'fail_absent'},group_b:'rest'},
 compare_traces:{a:{trace_id:'a'.repeat(32)},b:{trace_id:'b'.repeat(32)}},verify_fix:{},aggregate_telemetry:{signal:'spans',measures:['count']},
 discover_attributes:{signal:'spans'},get_agent_run:{},list_releases:{},get_service_map:{},check_telemetry_health:{},get_mcp_stats:{},list_sessions:{},
 list_traces:{},get_trace:{trace_id:'a'.repeat(32)},search_logs:{},list_metrics:{},query_metric:{metric_name:'requests.total'},ci_log:{},run_saved_query:{}};
// Each bounded number of a tool's input by its path, a member of an object form that one of its arguments takes too.
const boundedNumbers=(properties,path=[])=>Object.entries(properties).flatMap(([name,schema])=>[
 ...(schema.minimum===undefined&&schema.maximum===undefined?[]:[[[...path,name],schema]]),
 ...[schema,...(schema.anyOf??[])].filter(form=>form.properties).flatMap(form=>boundedNumbers(form.properties,[...path,name]))]);
verify('31 a number outside its range is refused by the MCP and by the Collector\'s HTTP route, naming the same field',async(_call,client)=>{
 const pascal=tool=>tool.split('_').map(part=>part[0].toUpperCase()+part.slice(1)).join('');
 let refused=0, nested=0;
 for (const [tool,required] of Object.entries(analysisArguments))
  for (const [path,schema] of boundedNumbers(documents[`Mcp.Tools.${pascal(tool)}Input`].properties))
   for (const value of [schema.minimum===undefined?[]:[schema.minimum-1],schema.maximum===undefined?[]:[schema.maximum+1]].flat()) {
    const field=path.join('.'), args={...required,...path.reduceRight((inner,name)=>({[name]:inner}),value)}, at=`${tool} ${field}=${value}`;
    if (path.length>1) nested++;
    const mcp=await client.callTool({name:tool,arguments:args});
    assert.equal(mcp.isError,true,`${at}: ${JSON.stringify(mcp)}`);
    const response=await fetch(`${process.env.QYL_COLLECTOR_URL}/api/v1/analysis/${tool}`,{method:'POST',headers:{'content-type':'application/json',...collectorHeaders()},body:JSON.stringify(args)});
    const http=await response.json();
    assert.equal(response.status,400,`${at}: ${JSON.stringify(http)}`);
    assert.deepEqual([mcp.structuredContent.error.code,mcp.structuredContent.error.field,http.errors[0].field],['invalid_argument',field,field],at);
    refused++;
   }
 assert(refused>=4*Object.keys(analysisArguments).length,`every tool's lookback_minutes and limit, both ends: ${refused}`);
 assert.equal(nested,6,'baseline.offset_minutes of triage, query_metric and get_mcp_stats, both ends');
});
verify('33 an unknown argument, a value of another type and one outside its set are refused by the MCP and by the Collector\'s HTTP route, naming the same field and what it accepts',async(_call,client)=>{
 const pascal=tool=>tool.split('_').map(part=>part[0].toUpperCase()+part.slice(1)).join('');
 // A value of another JSON type than the schema's; a union, which names none, takes no number.
 const mistyped=schema=>['integer','number'].includes(schema.type)?'1':schema.type==='boolean'?'true':['array','object'].includes(schema.type)?'x':1;
 const closed=schema=>schema.enum!==undefined||schema.anyOf?.every(form=>form.enum!==undefined||form.const!==undefined||form.properties!==undefined);
 let refused=0;
 const refuse=async(tool,args,field,accepted)=>{
  const at=`${tool} ${JSON.stringify(args)}`;
  const mcp=(await client.callTool({name:tool,arguments:args})).structuredContent?.error;
  const response=await fetch(`${process.env.QYL_COLLECTOR_URL}/api/v1/analysis/${tool}`,{method:'POST',headers:{'content-type':'application/json',...collectorHeaders()},body:JSON.stringify(args)});
  const [http]=(await response.json()).errors??[];
  assert.equal(response.status,400,`${at}: ${JSON.stringify(http)}`);
  assert.deepEqual([mcp?.code,mcp?.field,http?.field],['invalid_argument',field,field],at);
  assert.deepEqual(http.allowed?.toSorted(),mcp.allowed?.toSorted(),`${at}: ${JSON.stringify([mcp,http])}`);
  if (accepted!==undefined) assert.deepEqual(mcp.allowed?.toSorted(),accepted.toSorted(),at);
  assert(typeof mcp.hint==='string'&&typeof http.hint==='string',at);
  refused++;
 };
 for (const [tool,required] of Object.entries(analysisArguments)) {
  const {properties}=documents[`Mcp.Tools.${pascal(tool)}Input`];
  await refuse(tool,{...required,bogus_argument:1},'bogus_argument',Object.keys(properties));
  for (const [field,schema] of Object.entries(properties)) {
   await refuse(tool,{...required,[field]:mistyped(schema)},field);
   if (closed(schema)) await refuse(tool,{...required,[field]:'bogus_value'},field);
  }
 }
 for (const tool of ['triage','list_traces','search_logs']) {
  await refuse(tool,{matchers:[{key:'service.name',op:'exists',bogus_member:1}]},'matchers[0].bogus_member',['key','op','value','values','scope']);
  await refuse(tool,{matchers:[{key:'service.name',op:'eq',value:{}}]},'matchers[0].value');
 }
 assert(refused>=3*Object.keys(analysisArguments).length,`every tool's unknown argument and its mistyped and closed arguments: ${refused}`);
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
