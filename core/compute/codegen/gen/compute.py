from pprint import pprint as pp

def gen( opcodes, ignore ):

    filtered    = [f for f in opcodes if not f['system_opcode'] and f['nop'] > 0 and f['opcode'] not in ignore]
    fname       = [dict(f.items()+{'fname': f['opcode'].lower().replace('cphvb_', '')}.items()) for f in filtered]

    data = []
    for f in fname:

        for t in f['types']:
            
            op = dict(f.items())
            op['op1'] = t[0]
            op['op2'] = t[1]
            if op['nop']>2:
                op['op3'] = t[2]
            op['ftypes'] = ','.join(t).lower()
            del(op['code'])
            del(op['doc'])
            del(op['system_opcode'])
            del(op['types'])
            data.append(op)
        
    pp(data)
    return data
