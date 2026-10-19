#!/usr/bin/env python3
"""Seed the shared 32-tool answer key through real OTLP; no direct database writes.
Usage: python3 seed-answer-key.py http://127.0.0.1:5518 /tmp/qyl-answer-key.json [project]
The JSON records absolute windows, ids and Verify requests for both C# and MCP tests.
Set QYL_ANSWER_KEY_RUN_ID for isolated ids, services and sessions in a shared project;
QYL_API_KEY supplies an existing ingest credential without placing it on the command line.
"""
import datetime, hashlib, json, os, sys, time, urllib.request
base=sys.argv[1].rstrip('/'); out=sys.argv[2] if len(sys.argv)>2 else '/tmp/qyl-answer-key.json'
project=sys.argv[3] if len(sys.argv)>3 else 'answer-key'
run_id=os.environ.get('QYL_ANSWER_KEY_RUN_ID','')
metric_run_labels={'test.run.id':run_id} if run_id else {}
def scoped(value):return f'{value}-{run_id}' if run_id else value
now=int(time.time()*1e9); minute=60_000_000_000; T=now-20*minute; T2=now-5*minute
spans=[]; logs=[]; keys={}
def ident(text,n):return hashlib.sha256((f'{project}|{run_id}|{text}' if run_id else text).encode()).hexdigest()[:n]
def attr(k,v):
 t='boolValue' if isinstance(v,bool) else 'intValue' if isinstance(v,int) else 'doubleValue' if isinstance(v,float) else 'stringValue'
 return {'key':k,'value':{t:v if t!='intValue' else str(v)}}
def attrs(d):return [attr(k,v) for k,v in d.items()]
def span(key,service,name,at,duration=20,error=False,session=None,parent=None,trace=None,a=None,r=None,message=None):
 trace=trace or ident(key,32); sid=ident(key,16); data=dict(a or {})
 if session:data['session.id']=scoped(session)
 s={'traceId':trace,'spanId':sid,'name':name,'kind':2,'startTimeUnixNano':str(at),'endTimeUnixNano':str(at+int(duration*1e6)),'status':{'code':2 if error else 1},'attributes':attrs(data)}
 if parent:s['parentSpanId']=parent
 if message:s['status']['message']=message
 spans.append({'resource':{'attributes':attrs({'service.name':scoped(service),**(r or {})})},'scopeSpans':[{'scope':{'name':'qyl-answer-key'},'spans':[s]}]})
 keys[key]={'trace_id':trace,'span_id':sid};return trace,sid
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
for i in range(2):span(f'rare-{i}','payments','/pay',T+i*1_000_000,error=True,a={'http.route':'/pay','http.response.status_code':500,'error.type':'payment'},message='payment error')
for region,version,error in [('eu','2.3',True),('us','2.3',False),('eu','2.2',False),('us','2.2',False)]:
 for i in range(10):span(f'discrim-{region}-{version}-{i}','regional','request',T+i*1_000_000,error=error,a={'error.type':'regional'} if error else {},r={'cloud.region':region,'service.version':version})
for route,ms in [('/checkout',1840),('/cart',210)]:
 for i in range(20):span(f'latency-{route}-{i}','http','GET '+route,T+i*1_000_000,duration=ms,a={'http.route':route})
for i in range(40):
 trm,sm=span(f'mcp-{i}','qyl-mcp','tools/call search_logs',T+i*1_000_000,error=i<8,a={'mcp.method.name':'tools/call','gen_ai.tool.name':'search_logs','mcp.server.name':scoped('qyl'),'mcp.transport':'http',**({'error.type':'timeout'} if i<8 else {})})
 if i<8:logs.append({'resource':{'attributes':attrs({'service.name':scoped('qyl-mcp')})},'scopeLogs':[{'scope':{'name':'qyl-answer-key'},'logRecords':[{'timeUnixNano':str(T+i*1_000_000),'severityNumber':17,'severityText':'ERROR','body':{'stringValue':f'tool timeout after {i+1} seconds'},'traceId':trm,'spanId':sm,'attributes':attrs({'error.type':'timeout'})}]}]})
for i in range(3):span(f'agent-{i}','agent','search_logs',T+i*1_000_000,session='agent_run',a={'gen_ai.operation.name':'execute_tool','gen_ai.tool.name':'search_logs','qyl.gen_ai.tool.call.arguments_hash':'same-arguments-hash','gen_ai.agent.name':'researcher','gen_ai.usage.input_tokens':100,'gen_ai.usage.output_tokens':20})
for label,at,build,error in [('baseline',now-40*minute,100,False),('current',now-10*minute,300,True)]:
 for phase in ['build','test']:span(f'ci-{label}-{phase}','qyl-ci-test',phase,at+(0 if phase=='build' else 1_000_000_000),duration=build if phase=='build' else 20,error=error and phase=='test',session='ci_'+label,a={'cicd.pipeline.task.type':phase,'cicd.pipeline.task.name':phase,'cicd.pipeline.run.id':scoped('provider-'+label)})
span('silent','billing','charge',now-120*minute)
metrics=[]
for route,value in [('/checkout',1.84),('/cart',.21)]:
 metrics.append({'resource':{'attributes':attrs({'service.name':scoped('http')})},'scopeMetrics':[{'scope':{'name':'qyl-answer-key'},'metrics':[{'name':'http.server.request.duration','description':'HTTP duration','unit':'s','histogram':{'aggregationTemporality':1,'dataPoints':[{'attributes':attrs({'http.route':route,**metric_run_labels}),'startTimeUnixNano':str(T-minute),'timeUnixNano':str(T),'count':'100','sum':value*100,'min':value,'max':value,'explicitBounds':[value/2,value,value*1.5],'bucketCounts':['0','100','0','0']}]}}]}]})
metrics.append({'resource':{'attributes':attrs({'service.name':scoped('counter')})},'scopeMetrics':[{'scope':{'name':'qyl-answer-key'},'metrics':[{'name':'requests.total','unit':'{request}','sum':{'aggregationTemporality':2,'isMonotonic':True,'dataPoints':[{'attributes':attrs(metric_run_labels),'startTimeUnixNano':str(T-minute),'timeUnixNano':str(T+i*minute),'asInt':str(v)} for i,v in enumerate([100,150,190])]}}]}]})
def post(path,data):
 for batch in next(iter(data.values())):
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
metadata={'project':project,'run_id':run_id,'mcp_server_name':scoped('qyl'),'services':{name:scoped(name) for name in ['checkout','orders-db','payments','regional','http','counter','qyl-mcp','agent','qyl-ci-test','billing','sessions']},'sessions':{name:scoped(name) for name in ['agent_run','ci_current','ci_baseline','sess_ok','sess_bad']},'now':iso(now),'T':iso(T),'T2':iso(T2),'window':{'start_time':iso(now-60*minute),'end_time':iso(now+minute)},'keys':keys,'expected':{'mcp_requests':40,'mcp_errors':8,'error_rate':.2,'increase':90,'checkout_p95_ms':1840,'fix_requests_needed':96},'baseline_window':{'start_time':iso(now-120*minute),'end_time':iso(now-60*minute)}}
open(out,'w').write(json.dumps(metadata,indent=2)+'\n');print(out)
