"""Find candidate constant-address accesses; follow up in disassembly.

Linear tracking only, with simultaneous reads for vendor '#' instruction pairs.
This is an index for manual analysis, not an emulator or proof of reachability.
"""
import argparse,re
from analyze_disassembly import instructions

p=argparse.ArgumentParser()
p.add_argument('label')
p.add_argument('low',type=lambda x:int(x,0))
p.add_argument('high',type=lambda x:int(x,0))
a=p.parse_args()
rows=instructions(a.label); vals={}

def compute(expr, values):
    if expr in values: return values[expr]
    if re.fullmatch('-?0x[0-9a-f]+',expr): return int(expr,16)&0xffffffff
    m=re.fullmatch(r'(r\d+) ([+\-]|<<|>>) (r\d+|-?0x[0-9a-f]+)',expr)
    if not m:return None
    x=values.get(m[1]); y=compute(m[3],values)
    if x is None or y is None:return None
    if m[2]=='+': return (x+y)&0xffffffff
    if m[2]=='-': return (x-y)&0xffffffff
    if y>=32:return None
    return ((x<<y)&0xffffffff) if m[2]=='<<' else x>>y

i=0
while i<len(rows):
    group=[rows[i]]
    if rows[i]['asm'].endswith('#') and i+1<len(rows):group.append(rows[i+1])
    if any(r['asm'].startswith('[--sp] = ') for r in group):vals={}
    changes={}
    for row in group:
        text=row['asm'].removesuffix('#').strip()
        for m in re.finditer(r'([bh]?)\[(r\d+)\+(0x[0-9a-f]+)\]',text):
            base=vals.get(m[2]); offset=int(m[3],16)
            if base is not None and a.low<=base+offset<a.high:
                print(hex(row['address']),hex(base+offset),text)
        m=re.fullmatch(r'(r\d+) = (.*)',text)
        if m:
            changes[m[1]]=compute(m[2],vals)
        else:
            m=re.fullmatch(r'(r\d+) ([+\-])= (.*)',text)
            if m: changes[m[1]]=compute(m[1]+' '+m[2]+' '+m[3],vals)
            else:
                m=re.match(r'(r\d+) (?:=|\+=|-=|\^=|&=|\|=|<=)',text)
                if m:changes[m[1]]=None
        m=re.search(r'\[\+\+(r\d+)=(0x[0-9a-f]+)\]',text)
        if m:
            addr=compute(m[1]+' + '+m[2],vals);changes[m[1]]=addr
            if addr is not None and a.low<=addr<a.high:print(hex(row['address']),hex(addr),text)
        if text.startswith('call '):
            for reg in ('r0','r1','r2','r3'):changes[reg]=None
    for reg,value in changes.items():
        vals.pop(reg,None)
        if value is not None:vals[reg]=value
    if any(r['asm']=='rts' or r['asm'].startswith('pc = ') or r['asm'].startswith('{pc,') for r in group):vals={}
    i+=len(group)
