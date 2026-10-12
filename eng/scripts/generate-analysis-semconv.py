#!/usr/bin/env python3
"""Extract discovery documentation from the same resolved qyl registry used by the bindings."""
import json,pathlib,sys
source=pathlib.Path(sys.argv[1]); data=json.loads(source.read_text()); out={}
def walk(value):
    if isinstance(value,dict):
        if isinstance(value.get('key'),str) and 'brief' in value and 'type' in value:
            level=value.get('requirement_level','recommended')
            if not isinstance(level,str):level=next(iter(level),'recommended')
            out[value['key']]={'brief':value['brief'].strip(),'stability':value.get('stability','development'),'requirement_level':level}
        for item in value.values():walk(item)
    elif isinstance(value,list):
        for item in value:walk(item)
walk(data['registry'])
target=pathlib.Path(__file__).resolve().parents[2]/'services/qyl.collector/Analysis/Generated/semconv-metadata.json'
target.write_text(json.dumps({'schema_url':data['schema_url'],'attributes':out,'span_requirements':[{'type':g['type'],'kind':g['kind'],'required':[a['key'] for a in g.get('attributes',[]) if a.get('requirement_level')=='required']} for g in data['registry'].get('spans',[])]},sort_keys=True,separators=(',',':'))+'\n')
print(f'{len(out)} attribute definitions generated from {data["schema_url"]}')
