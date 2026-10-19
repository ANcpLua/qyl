#!/usr/bin/env python3
"""Seed the shared 32-tool answer key through real OTLP; no direct database writes.
Usage: python3 seed-answer-key.py http://127.0.0.1:5518 /tmp/qyl-answer-key.json [project]
The JSON records absolute windows, ids and Verify requests for both C# and MCP tests.
Every span carries the attributes its OpenTelemetry semantic convention requires and every resource and
scope its schema URL, so check_telemetry_health finds only the planted incidents.
Set QYL_ANSWER_KEY_RUN_ID for isolated ids, services and sessions in a shared project;
set QYL_ANSWER_KEY_FRESH_IDS=1 for new trace and span ids under the same services and sessions, to reseed
a project such as the hosted reviewer's every day.
QYL_API_KEY supplies an existing ingest credential without placing it on the command line.
"""
import datetime, hashlib, json, os, sys, time, urllib.request
base=sys.argv[1].rstrip('/'); out=sys.argv[2] if len(sys.argv)>2 else '/tmp/qyl-answer-key.json'
project=sys.argv[3] if len(sys.argv)>3 else 'answer-key'
run_id=os.environ.get('QYL_ANSWER_KEY_RUN_ID','')
id_salt=os.urandom(16).hex() if os.environ.get('QYL_ANSWER_KEY_FRESH_IDS','') not in ('','0') else ''
schema='https://opentelemetry.io/schemas/1.44.0'
scope={'name':'qyl-answer-key','version':'1.0.0'}
metric_run_labels={'test.run.id':run_id} if run_id else {}
def scoped(value):return f'{value}-{run_id}' if run_id else value
now=int(time.time()*1e9); minute=60_000_000_000; T=now-20*minute; T2=now-5*minute
spans=[]; logs=[]; keys={}
def ident(text,n):return hashlib.sha256((id_salt+(f'{project}|{run_id}|{text}' if run_id else text)).encode()).hexdigest()[:n]
def attr(k,v):
 t='boolValue' if isinstance(v,bool) else 'intValue' if isinstance(v,int) else 'doubleValue' if isinstance(v,float) else 'stringValue'
 return {'key':k,'value':{t:v if t!='intValue' else str(v)}}
def attrs(d):return [attr(k,v) for k,v in d.items()]
def resource(service,r=None):return {'attributes':attrs({'service.name':scoped(service),**(r or {})})}
def span(key,service,name,at,duration=20,error=False,session=None,parent=None,trace=None,a=None,r=None,message=None,kind=2):
 trace=trace or ident(key,32); sid=ident(key,16); data=dict(a or {})
 if session:data['session.id']=scoped(session)
 s={'traceId':trace,'spanId':sid,'name':name,'kind':kind,'startTimeUnixNano':str(at),'endTimeUnixNano':str(at+int(duration*1e6)),'status':{'code':2 if error else 1},'attributes':attrs(data)}
 if parent:s['parentSpanId']=parent
 if message:s['status']['message']=message
 spans.append({'resource':resource(service,r),'schemaUrl':schema,'scopeSpans':[{'scope':scope,'schemaUrl':schema,'spans':[s]}]})
 keys[key]={'trace_id':trace,'span_id':sid};return trace,sid
# An HTTP server span's required attributes; its name is the method and route.
def http(method,route,status):return {'http.request.method':method,'http.route':route,'url.path':route,'url.scheme':'https','http.response.status_code':status}
for sess,count,errs in [('sess_ok',12,0),('sess_bad',9,3)]:
 for i in range(count):span(f'{sess}-{i}','sessions','request',now-minute+i*1_000_000,error=i<errs,session=sess,a={'error.type':'test'} if i<errs else {})
# Five checkout symptoms and the timed-out db child share the one failing execution.
tr,root=span('db-root','checkout','checkout',T+minute,200,error=False,r={'service.version':'1.4.1'})
_,db=span('db-timeout','orders-db','db',T+minute+1_000_000,100,error=True,parent=root,trace=tr,a={'error.type':'timeout','db.system.name':'postgresql'},message='database timeout')
for i in range(5):span(f'checkout-child-{i}','checkout','checkout',T+minute+110_000_000+i*1_000_000,5,error=True,parent=root,trace=tr,a={'error.type':'downstream'},r={'service.version':'1.4.1'},message='dependency failed')
success,sroot=span('db-success-root','checkout','checkout',T-minute,180,r={'service.version':'1.4.0'})
span('db-success','orders-db','db',T-minute+1_000_000,90,parent=sroot,trace=success,a={'db.system.name':'postgresql'})
for i in range(30):span(f'baseline-{i}','checkout','checkout',now-80*minute+i*1_000_000,r={'service.version':'1.4.0'})
span('release-first','checkout','checkout',T,error=True,a={'error.type':'downstream'},r={'service.version':'1.4.1'},message='dependency failed')
for i in range(4):span(f'fix-request-{i}','checkout','checkout',T2+minute+i*1_000_000,r={'service.version':'1.4.1'})
for i in range(2):span(f'rare-{i}','payments','POST /pay',T+i*1_000_000,error=True,a={**http('POST','/pay',500),'error.type':'payment'},message='payment error')
for region,version,error in [('eu','2.3',True),('us','2.3',False),('eu','2.2',False),('us','2.2',False)]:
 for i in range(10):span(f'discrim-{region}-{version}-{i}','regional','request',T+i*1_000_000,error=error,a={'error.type':'regional'} if error else {},r={'cloud.region':region,'service.version':version})
for route,ms in [('/checkout',1840),('/cart',210)]:
 for i in range(20):span(f'latency-{route}-{i}','http','GET '+route,T+i*1_000_000,duration=ms,a=http('GET',route,200))
for i in range(40):
 trm,sm=span(f'mcp-{i}','qyl-mcp','tools/call search_logs',T+i*1_000_000,error=i<8,a={'mcp.method.name':'tools/call','gen_ai.tool.name':'search_logs','mcp.server.name':scoped('qyl'),'mcp.transport':'http',**({'error.type':'timeout'} if i<8 else {})})
 if i<8:logs.append({'resource':resource('qyl-mcp'),'schemaUrl':schema,'scopeLogs':[{'scope':scope,'schemaUrl':schema,'logRecords':[{'timeUnixNano':str(T+i*1_000_000),'severityNumber':17,'severityText':'ERROR','body':{'stringValue':f'tool timeout after {i+1} seconds'},'traceId':trm,'spanId':sm,'attributes':attrs({'error.type':'timeout'})}]}]})
for i in range(3):span(f'agent-{i}','agent','execute_tool search_logs',T+i*1_000_000,session='agent_run',kind=1,a={'gen_ai.operation.name':'execute_tool','gen_ai.tool.name':'search_logs','qyl.gen_ai.tool.call.arguments_hash':'same-arguments-hash','gen_ai.agent.name':'researcher','gen_ai.usage.input_tokens':100,'gen_ai.usage.output_tokens':20})
# Each CI run is a pipeline run span with its build and test task spans; the current run's test task fails.
for label,at,build,error in [('baseline',now-40*minute,100,False),('current',now-10*minute,300,True)]:
 run=scoped('provider-'+label); url=f'https://ci.example.com/qyl/runs/{run}'; pipeline={'cicd.pipeline.name':'qyl-ci','cicd.pipeline.run.id':run}
 trace_,root_=span(f'ci-{label}','qyl-ci-test','RUN qyl-ci',at,duration=1020,error=error,session='ci_'+label,
  a={**pipeline,'cicd.pipeline.action.name':'RUN','cicd.pipeline.run.url.full':url,'cicd.pipeline.result':'failure' if error else 'success',**({'error.type':'test_failure'} if error else {})})
 for phase in ['build','test']:
  failed=error and phase=='test'
  span(f'ci-{label}-{phase}','qyl-ci-test',phase,at+(0 if phase=='build' else 1_000_000_000),duration=build if phase=='build' else 20,error=failed,session='ci_'+label,parent=root_,trace=trace_,kind=1,
   a={**pipeline,'cicd.pipeline.task.type':phase,'cicd.pipeline.task.name':phase,'cicd.pipeline.task.run.id':f'{run}-{phase}','cicd.pipeline.task.run.url.full':f'{url}/tasks/{phase}','cicd.pipeline.task.run.result':'failure' if failed else 'success',**({'error.type':'test_failure'} if failed else {})})
span('silent','billing','charge',now-120*minute)
metrics=[]
for route,value in [('/checkout',1.84),('/cart',.21)]:
 metrics.append({'resource':resource('http'),'schemaUrl':schema,'scopeMetrics':[{'scope':scope,'schemaUrl':schema,'metrics':[{'name':'http.server.request.duration','description':'HTTP duration','unit':'s','histogram':{'aggregationTemporality':1,'dataPoints':[{'attributes':attrs({'http.request.method':'GET','url.scheme':'https','http.route':route,**metric_run_labels}),'startTimeUnixNano':str(T-minute),'timeUnixNano':str(T),'count':'100','sum':value*100,'min':value,'max':value,'explicitBounds':[value/2,value,value*1.5],'bucketCounts':['0','100','0','0']}]}}]}]})
# The counter starts one minute before its first point, inside the window: all 190 requests are the window's increase.
metrics.append({'resource':resource('counter'),'schemaUrl':schema,'scopeMetrics':[{'scope':scope,'schemaUrl':schema,'metrics':[{'name':'requests.total','unit':'{request}','sum':{'aggregationTemporality':2,'isMonotonic':True,'dataPoints':[{'attributes':attrs(metric_run_labels),'startTimeUnixNano':str(T-minute),'timeUnixNano':str(T+i*minute),'asInt':str(v)} for i,v in enumerate([100,150,190])]}}]}]})
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
 with urllib.request.urlopen(req,timeout=60) as res:
  assert res.status==200
  response=json.load(res)
  partial=response.get('partialSuccess',response.get('partial_success',{}))
  assert not any(int(value) for key,value in partial.items() if key.startswith('rejected')), partial
post('/v1/traces',{'resourceSpans':spans});post('/v1/logs',{'resourceLogs':logs});post('/v1/metrics',{'resourceMetrics':metrics})
def iso(n):return datetime.datetime.fromtimestamp(n/1e9,datetime.timezone.utc).isoformat().replace('+00:00','Z')
metadata={'project':project,'run_id':run_id,'mcp_server_name':scoped('qyl'),'services':{name:scoped(name) for name in ['checkout','orders-db','payments','regional','http','counter','qyl-mcp','agent','qyl-ci-test','billing','sessions']},'sessions':{name:scoped(name) for name in ['agent_run','ci_current','ci_baseline','sess_ok','sess_bad']},'now':iso(now),'T':iso(T),'T2':iso(T2),'window':{'start_time':iso(now-60*minute),'end_time':iso(now+minute)},'keys':keys,'expected':{'mcp_requests':40,'mcp_errors':8,'error_rate':.2,'increase':190,'checkout_p95_ms':1840,'fix_requests_needed':96},'baseline_window':{'start_time':iso(now-120*minute),'end_time':iso(now-60*minute)}}
open(out,'w').write(json.dumps(metadata,indent=2)+'\n');print(out)
