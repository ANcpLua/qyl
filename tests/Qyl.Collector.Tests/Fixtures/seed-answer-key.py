#!/usr/bin/env python3
"""Seed the MCP answer key through real OTLP; no direct database writes.
Usage: python3 seed-answer-key.py http://127.0.0.1:5518 /tmp/qyl-answer-key.json [project]
The JSON records the absolute windows, ids, release names and expected values the answer key checks.
Every span carries the attributes its OpenTelemetry semantic convention requires and every resource and
scope its schema URL, so check_telemetry_health finds only the planted incidents.
Set QYL_ANSWER_KEY_RUN_ID for isolated ids, services and sessions in a shared project.
Set QYL_ANSWER_KEY_FRESH_IDS=1, the hosted mode, to reseed a project every day at 00:00 UTC, as the hosted reviewer's
is: new trace and span ids under the same services and sessions, and releases named by the run's UTC date, so
verify_fix and list_releases never weigh one day's releases against another's. A version keeps its last number and the
date replaces the rest: checkout runs YYYY.MM.DD.0, fails from YYYY.MM.DD.1 and is fixed in YYYY.MM.DD.2, and regional
fails in eu on YYYY.MM.DD.3 beside YYYY.MM.DD.2. Without it they are 1.4.0, 1.4.1, 1.4.2, 2.2 and 2.3.
Set QYL_ANSWER_KEY_MODE=traffic to send, instead of the seed, one small batch of healthy requests at now, with fresh
ids, for every seeded service except billing; run every 10 minutes, it keeps those services live between reseeds
while every planted answer holds. Its checkouts run the fix release, so verify_fix of the checkout failure with that
release is pending until a hundred of them ran it, then passed. Hosted traffic sets QYL_ANSWER_KEY_FRESH_IDS=1 as well
and runs the releases of the reseed ten minutes before now, the latest 00:00 one, never one no reseed has planted.
QYL_ANSWER_KEY_NOW (Unix seconds) replaces the current time in either mode. Seeded durations vary around their planned
values, so no two percentiles agree.
QYL_API_KEY supplies an existing ingest credential without placing it on the command line.
"""
import datetime, hashlib, json, os, random, sys, time, urllib.error, urllib.request
base=sys.argv[1].rstrip('/'); out=sys.argv[2] if len(sys.argv)>2 else '/tmp/qyl-answer-key.json'
project=sys.argv[3] if len(sys.argv)>3 else 'answer-key'
run_id=os.environ.get('QYL_ANSWER_KEY_RUN_ID','')
mode=os.environ.get('QYL_ANSWER_KEY_MODE') or 'seed'
if mode not in ('seed','traffic'):sys.exit(f'QYL_ANSWER_KEY_MODE is seed or traffic, not {mode}.')
hosted=os.environ.get('QYL_ANSWER_KEY_FRESH_IDS','') not in ('','0')
id_salt=os.urandom(16).hex() if mode=='traffic' or hosted else ''
schema='https://opentelemetry.io/schemas/1.44.0'
scope={'name':'qyl-answer-key','version':'1.0.0'}
metric_run_labels={'test.run.id':run_id} if run_id else {}
def scoped(value):return f'{value}-{run_id}' if run_id else value
# Whole milliseconds, so the RFC 3339 instants the fixture records name T and the fix point T2 exactly.
now=int(float(os.environ.get('QYL_ANSWER_KEY_NOW') or time.time())*1e3)*1_000_000; minute=60_000_000_000; T=now-20*minute; T2=now-5*minute
# A hosted run names each release by the UTC date of the reseed it belongs to, keeping the version's last number. The
# reseed runs at 00:00 UTC and traffic belongs to the one ten minutes back: the traffic at midnight still runs the day
# before's releases, which that day's reseed planted, and later traffic those of the day's own reseed.
def release(version):
 if not hosted:return version
 day=datetime.datetime.fromtimestamp((now-(10*minute if mode=='traffic' else 0))/1e9,datetime.timezone.utc)
 return f"{day:%Y.%m.%d}.{version.rsplit('.',1)[1]}"
releases={'checkout':{'previous':release('1.4.0'),'failing':release('1.4.1'),'fix':release('1.4.2')},'regional':{'previous':release('2.2'),'failing':release('2.3')}}
spans=[]; logs=[]; keys={}
def ident(text,n):return hashlib.sha256((id_salt+(f'{project}|{run_id}|{text}' if run_id else text)).encode()).hexdigest()[:n]
def attr(k,v):
 t='boolValue' if isinstance(v,bool) else 'intValue' if isinstance(v,int) else 'doubleValue' if isinstance(v,float) else 'stringValue'
 return {'key':k,'value':{t:v if t!='intValue' else str(v)}}
def attrs(d):return [attr(k,v) for k,v in d.items()]
def resource(service,r=None):return {'attributes':attrs({'service.name':scoped(service),**(r or {})})}
# A seeded duration varies by up to 8% around its planned value, the same for the same key in every run, as measured
# requests do; traffic draws its own, and a duration a planted answer is exact in is passed with exact=True.
def vary(key,ms,spread=.08):return ms*(1+spread*(2*int(hashlib.sha256(('duration|'+key).encode()).hexdigest()[:8],16)/0xffffffff-1))
# One route's requests spread evenly from 80% of its p95 to just above it, in an order their keys scramble: the 95th
# percentile, interpolated between neighbours as qyl computes it, is exactly the p95, and the median sits lower.
LOW,TOP=.8,(1-.05*.8)/.95
def spread(keys,p95):
 order=sorted(keys,key=lambda key:hashlib.sha256(('order|'+key).encode()).hexdigest())
 return {key:p95*(LOW+(TOP-LOW)*i/(len(order)-1)) for i,key in enumerate(order)}
def span(key,service,name,at,duration=20,error=False,session=None,parent=None,trace=None,a=None,r=None,message=None,kind=2,exact=False):
 trace=trace or ident(key,32); sid=ident(key,16); data=dict(a or {})
 if session:data['session.id']=scoped(session)
 if mode=='seed' and not exact:duration=vary(key,duration)
 s={'traceId':trace,'spanId':sid,'name':name,'kind':kind,'startTimeUnixNano':str(at),'endTimeUnixNano':str(at+round(duration*1e6)),'status':{'code':2 if error else 1},'attributes':attrs(data)}
 if parent:s['parentSpanId']=parent
 if message:s['status']['message']=message
 spans.append({'resource':resource(service,r),'schemaUrl':schema,'scopeSpans':[{'scope':scope,'schemaUrl':schema,'spans':[s]}]})
 keys[key]={'trace_id':trace,'span_id':sid};return trace,sid
def log(service,at,severity,body,trace,sid,a=None):
 logs.append({'resource':resource(service),'schemaUrl':schema,'scopeLogs':[{'scope':scope,'schemaUrl':schema,'logRecords':[{'timeUnixNano':str(at),'severityNumber':severity,'severityText':{9:'INFO',17:'ERROR'}[severity],'body':{'stringValue':body},'traceId':trace,'spanId':sid,'attributes':attrs(a or {})}]}]})
# An HTTP server span's required attributes; its name is the method and route.
def http(method,route,status):return {'http.request.method':method,'http.route':route,'url.path':route,'url.scheme':'https','http.response.status_code':status}
# A CI run is a pipeline run span with its build and test task spans; a failing run's test task fails. The run ends
# with its test task, a second after it started; the build takes exactly `build` ms, the duration CI regressions compare.
BUILD_MS=round(vary('ci-build',100))
def ci(label,at,build,error):
 run=scoped('provider-'+label); url=f'https://ci.example.com/qyl/runs/{run}'; pipeline={'cicd.pipeline.name':'qyl-ci','cicd.pipeline.run.id':run}
 test=vary(f'ci-{label}-test',20)
 trace_,root_=span(f'ci-{label}','qyl-ci-test','RUN qyl-ci',at,duration=1000+test,exact=True,error=error,session='ci_'+label,
  a={**pipeline,'cicd.pipeline.action.name':'RUN','cicd.pipeline.run.url.full':url,'cicd.pipeline.result':'failure' if error else 'success',**({'error.type':'test_failure'} if error else {})})
 for phase in ['build','test']:
  failed=error and phase=='test'
  span(f'ci-{label}-{phase}','qyl-ci-test',phase,at+(0 if phase=='build' else 1_000_000_000),duration=build if phase=='build' else test,exact=True,error=failed,session='ci_'+label,parent=root_,trace=trace_,kind=1,
   a={**pipeline,'cicd.pipeline.task.type':phase,'cicd.pipeline.task.name':phase,'cicd.pipeline.task.run.id':f'{run}-{phase}','cicd.pipeline.task.run.url.full':f'{url}/tasks/{phase}','cicd.pipeline.task.run.result':'failure' if failed else 'success',**({'error.type':'test_failure'} if failed else {})})
def post(path,data):
 for batch in next(iter(data.values())):
  # The seed replays records that happened earlier and observes them as it sends them.
  for scoped_logs in batch.get('scopeLogs',[]):
   for record in scoped_logs['logRecords']:record['observedTimeUnixNano']=str(time.time_ns())
  batch['resource']['attributes'].append(attr('qyl.project.id',project))
  if run_id:batch['resource']['attributes'].append(attr('test.run.id',run_id))
 headers={'Content-Type':'application/json','X-Qyl-Project':project}
 if os.environ.get('QYL_API_KEY'):headers['X-Otlp-Api-Key']=os.environ['QYL_API_KEY']
 req=urllib.request.Request(base+path,json.dumps(data).encode(),headers,method='POST')
 # A refused or partly rejected export exits naming its path, status and the Collector's answer, which holds the reason.
 try:
  with urllib.request.urlopen(req,timeout=60) as res:status,body=res.status,res.read()
 except urllib.error.HTTPError as error:status,body=error.code,error.read()
 partial=json.loads(body).get('partialSuccess',{}) if status==200 else {}
 if status!=200 or any(int(value) for key,value in partial.items() if key.startswith('rejected')):
  sys.exit(f'POST {path} answered {status}: {body.decode("utf-8","replace")}')
def iso(n):return datetime.datetime.fromtimestamp(n/1e9,datetime.timezone.utc).isoformat().replace('+00:00','Z')
if mode=='traffic':
 # Healthy requests under new sessions. Each operation sends one request at a fixed p95, the seeded one where the seed
 # has it, and draws the rest below, so no p95 rises and the routes' p95s stay exact; regional never runs eu on 2.3,
 # qyl-mcp and the agent never call search_logs, checkout runs the fix release, and the CI run repeats the baseline
 # run's build, so every planted answer holds however many batches accumulate.
 tag=id_salt[:8]
 def draw(p95,low,n):return [p95]+[random.uniform(low,p95) for _ in range(n-1)]
 def start(ms):return now-int(random.uniform(1,30)*1e9)-int(ms*1e6)
 for i,(ms,db_ms) in enumerate(zip(draw(20,13,2),draw(10,3,2))):
  at=start(ms); tr,root=span(f'checkout-{i}','checkout','checkout',at,ms,r={'service.version':release('1.4.2')})
  span(f'orders-db-{i}','orders-db','db',at+1_000_000,db_ms,parent=root,trace=tr,a={'db.system.name':'postgresql'})
  if i==0:log('checkout',at+int(ms*1e6),9,f'order {ident("order",8)} placed',tr,root)
 for i,ms in enumerate(draw(120,40,2)):
  at=start(ms); tr,sid=span(f'pay-{i}','payments','POST /pay',at,ms,a=http('POST','/pay',200))
  if i==0:log('payments',at+int(ms*1e6),9,'payment captured',tr,sid)
 for route,p95,low in [('/checkout',1840,1100),('/cart',210,120)]:
  for i,ms in enumerate(draw(p95,low,2)):span(f'latency-{route}-{i}','http','GET '+route,start(ms),ms,a=http('GET',route,200))
 for (region,version),ms in zip([('us','2.3'),('eu','2.2'),('us','2.2')],random.sample(draw(20,13,3),3)):
  span(f'regional-{region}-{version}','regional','request',start(ms),ms,r={'cloud.region':region,'service.version':release(version)})
 for i,ms in enumerate(draw(20,8,2)):span(f'session-{i}','sessions','request',start(ms),ms,session='sess_ok-'+tag)
 for i,(tool,ms) in enumerate(zip(random.sample(['triage','list_failures','get_failure_context','list_traces','get_trace'],2),draw(60,15,2))):
  at=start(ms); tr,sid=span(f'mcp-{i}','qyl-mcp','tools/call '+tool,at,ms,a={'mcp.method.name':'tools/call','gen_ai.tool.name':tool,'mcp.server.name':scoped('qyl'),'mcp.transport':'http'})
  if i==0:log('qyl-mcp',at+int(ms*1e6),9,f'{tool} answered in {round(ms)} ms',tr,sid)
 for i,(tool,ms) in enumerate(zip(random.sample(['list_failures','list_traces','get_trace'],2),draw(20,8,2))):
  span(f'agent-{i}','agent','execute_tool '+tool,start(ms),ms,session='agent_ok-'+tag,kind=1,a={'gen_ai.operation.name':'execute_tool','gen_ai.tool.name':tool,'qyl.gen_ai.tool.call.arguments_hash':ident(f'arguments-{i}',16),'gen_ai.agent.name':'researcher','gen_ai.usage.input_tokens':random.randint(80,140),'gen_ai.usage.output_tokens':random.randint(10,40)})
 ci('ok-'+tag,start(1020),BUILD_MS,False)
 post('/v1/traces',{'resourceSpans':spans});post('/v1/logs',{'resourceLogs':logs})
 open(out,'w').write(json.dumps({'project':project,'run_id':run_id,'mode':mode,'now':iso(now),'spans':len(spans),'logs':len(logs),'sessions':[scoped(name+tag) for name in ['sess_ok-','agent_ok-','ci_ok-']],'releases':releases,'keys':keys},indent=2)+'\n');print(out)
 sys.exit()
for sess,count,errs in [('sess_ok',12,0),('sess_bad',9,3)]:
 for i in range(count):span(f'{sess}-{i}','sessions','request',now-minute+i*1_000_000,error=i<errs,session=sess,a={'error.type':'test'} if i<errs else {})
# Five checkout symptoms and the timed-out db child share the one failing execution.
tr,root=span('db-root','checkout','checkout',T+minute,200,error=False,r={'service.version':release('1.4.1')})
_,db=span('db-timeout','orders-db','db',T+minute+1_000_000,100,error=True,parent=root,trace=tr,a={'error.type':'timeout','db.system.name':'postgresql'},message='database timeout')
for i in range(5):span(f'checkout-child-{i}','checkout','checkout',T+minute+110_000_000+i*1_000_000,5,error=True,parent=root,trace=tr,a={'error.type':'downstream'},r={'service.version':release('1.4.1')},message='dependency failed')
success,sroot=span('db-success-root','checkout','checkout',T-minute,180,r={'service.version':release('1.4.0')})
span('db-success','orders-db','db',T-minute+1_000_000,90,parent=sroot,trace=success,a={'db.system.name':'postgresql'})
for i in range(30):span(f'baseline-{i}','checkout','checkout',now-80*minute+i*1_000_000,r={'service.version':release('1.4.0')})
span('release-first','checkout','checkout',T,error=True,a={'error.type':'downstream'},r={'service.version':release('1.4.1')},message='dependency failed')
# After the last orders-db timeout checkout ships the fix, 1.4.2, at T2; every later checkout runs it.
for i in range(4):span(f'fix-request-{i}','checkout','checkout',T2+i*1_000_000,r={'service.version':release('1.4.2')})
for i in range(2):span(f'rare-{i}','payments','POST /pay',T+i*1_000_000,error=True,a={**http('POST','/pay',500),'error.type':'payment'},message='payment error')
for region,version,error in [('eu','2.3',True),('us','2.3',False),('eu','2.2',False),('us','2.2',False)]:
 for i in range(10):span(f'discrim-{region}-{version}-{i}','regional','request',T+i*1_000_000,error=error,a={'error.type':'regional'} if error else {},r={'cloud.region':region,'service.version':release(version)})
for route,ms in [('/checkout',1840),('/cart',210)]:
 durations=spread([f'latency-{route}-{i}' for i in range(20)],ms)
 for i in range(20):span(f'latency-{route}-{i}','http','GET '+route,T+i*1_000_000,duration=durations[f'latency-{route}-{i}'],exact=True,a=http('GET',route,200))
for i in range(40):
 trm,sm=span(f'mcp-{i}','qyl-mcp','tools/call search_logs',T+i*1_000_000,error=i<8,a={'mcp.method.name':'tools/call','gen_ai.tool.name':'search_logs','mcp.server.name':scoped('qyl'),'mcp.transport':'http',**({'error.type':'timeout'} if i<8 else {})})
 if i<8:log('qyl-mcp',T+i*1_000_000,17,f'tool timeout after {i+1} seconds',trm,sm,{'error.type':'timeout'})
for i in range(3):span(f'agent-{i}','agent','execute_tool search_logs',T+i*1_000_000,session='agent_run',kind=1,a={'gen_ai.operation.name':'execute_tool','gen_ai.tool.name':'search_logs','qyl.gen_ai.tool.call.arguments_hash':'same-arguments-hash','gen_ai.agent.name':'researcher','gen_ai.usage.input_tokens':100,'gen_ai.usage.output_tokens':20})
# The current CI run's build takes three times the baseline run's and its test task fails.
for label,at,build,error in [('baseline',now-40*minute,BUILD_MS,False),('current',now-10*minute,3*BUILD_MS,True)]:ci(label,at,build,error)
# Billing reported every minute for an hour and stopped two hours ago. A daily reseed adds another such hour, so its
# usual interval stays near a minute; one span a day would make billing a daily reporter that is never silent.
for i in range(61):span(f'silent-{i}','billing','charge',now-180*minute+i*minute)
metrics=[]
# The routes' durations as a histogram spread like their spans: 95 of 100 requests from 80% of the p95 up to it, five
# just above, so the bucket estimate of the p95 is the route's p95.
for route,value in [('/checkout',1.84),('/cart',.21)]:
 metrics.append({'resource':resource('http'),'schemaUrl':schema,'scopeMetrics':[{'scope':scope,'schemaUrl':schema,'metrics':[{'name':'http.server.request.duration','description':'HTTP duration','unit':'s','histogram':{'aggregationTemporality':1,'dataPoints':[{'attributes':attrs({'http.request.method':'GET','url.scheme':'https','http.route':route,**metric_run_labels}),'startTimeUnixNano':str(T-minute),'timeUnixNano':str(T),'count':'100','sum':value*100*(LOW+TOP)/2,'min':value*LOW,'max':value*TOP,'explicitBounds':[value/2,value,value*1.5],'bucketCounts':['0','95','5','0']}]}}]}]})
# The counter starts one minute before its first point, inside the window: all 190 requests are the window's increase.
metrics.append({'resource':resource('counter'),'schemaUrl':schema,'scopeMetrics':[{'scope':scope,'schemaUrl':schema,'metrics':[{'name':'requests.total','unit':'{request}','sum':{'aggregationTemporality':2,'isMonotonic':True,'dataPoints':[{'attributes':attrs(metric_run_labels),'startTimeUnixNano':str(T-minute),'timeUnixNano':str(T+i*minute),'asInt':str(v)} for i,v in enumerate([100,150,190])]}}]}]})
post('/v1/traces',{'resourceSpans':spans});post('/v1/logs',{'resourceLogs':logs});post('/v1/metrics',{'resourceMetrics':metrics})
metadata={'project':project,'run_id':run_id,'mcp_server_name':scoped('qyl'),'services':{name:scoped(name) for name in ['checkout','orders-db','payments','regional','http','counter','qyl-mcp','agent','qyl-ci-test','billing','sessions']},'sessions':{name:scoped(name) for name in ['agent_run','ci_current','ci_baseline','sess_ok','sess_bad']},'now':iso(now),'T':iso(T),'T2':iso(T2),'releases':releases,'window':{'start_time':iso(now-60*minute),'end_time':iso(now+minute)},'keys':keys,'expected':{'mcp_requests':40,'mcp_errors':8,'error_rate':.2,'increase':190,'checkout_p95_ms':1840,'fix_requests_needed':96},'baseline_window':{'start_time':iso(now-120*minute),'end_time':iso(now-60*minute)}}
open(out,'w').write(json.dumps(metadata,indent=2)+'\n');print(out)
