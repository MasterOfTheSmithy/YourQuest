"""Give the model the same schema that constrains decoding; no answer examples or facts are added."""
import copy
import json


def with_visible_schema(case):
    # note: Decoder grammars are not prompt instructions in llama.cpp. Preserve inputs and expose the exact contract.
    result=copy.deepcopy(case)
    schema=result.get('schema')
    if schema:
        declaration='\nOUTPUT_SCHEMA_JSON\n'+json.dumps(schema,ensure_ascii=False,separators=(',',':'))
        for message in reversed(result['messages']):
            if message['role']=='user':
                marker='\nREVIEW_DATA_JSON\n'
                if marker in message['content']:
                    message['content']=message['content'].replace(marker,declaration+marker,1)
                else:message['content']+=declaration
                break
        else:raise ValueError('A visible schema requires a user task message')
    return result
