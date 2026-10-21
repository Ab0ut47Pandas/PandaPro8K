"""Offline instruction indexes and conservative investigation leads.

Constant tracking is linear and does not model control flow. Leads must be
checked against the surrounding instructions before assigning a function name.
"""
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parent


def instructions(label):
    rows=[]
    for line in (ROOT/'disassembly'/f'{label}.asm').read_text().splitlines():
        m=re.match(r'\s*([0-9a-f]+):\s+((?:[0-9a-f]{2}\s+)+)\s*(.*)',line)
        if m:
            rows.append({'address':int(m[1],16),'bytes':bytes.fromhex(m[2]),'asm':m[3].strip()})
    return rows


def direct_target(row):
    m=re.search(r'\b(call|goto) (-?0x[0-9a-f]+)\b',row['asm'])
    # Vendor objdump prints displacement from the end of the instruction.
    # Cross-checked against the SDK's ROM memset address (0x1fd60).
    return None if not m else row['address']+len(row['bytes'])+int(m[2],16)


def file_offset(regions, address):
    for region in regions:
        if region['address']<=address<region['address']+region['size']:
            return region['offset']+address-region['address']


def analyze(label):
    regions=json.loads((ROOT/'disassembly'/f'{label}-regions.json').read_text())
    filename='controller-2.2.9-app.bin' if label=='controller' else 'receiver-1.3.5-app.bin'
    data=(ROOT.parent.parent/'outputs/gamesir-firmware-research'/filename).read_bytes()
    wanted={}
    for match in re.finditer(rb'[ -~]{6,}',data):
        if re.search(rb'(?i)HS24G|HJC_TIMER|ScanStickInit|joypad task init|usb change .input mode|xinput .*endpoint|xinput init key|bt 24g .input|wire pc start .input',match[0]):
            wanted[0x6000100+match.start()]=match[0].decode('ascii')
    rows=instructions(label)
    calls=[]; leads=[]; values={}
    for row in rows:
        asm=row['asm']
        target=direct_target(row)
        if asm.startswith('call ') and target is not None:
            calls.append({'from':row['address'],'to':target})
        assignment=re.fullmatch(r'(r\d+) = (.*?)(?:\s+#)?',asm)
        if assignment:
            reg,expr=assignment.groups()
            result=None
            if re.fullmatch(r'-?0x[0-9a-f]+',expr):
                result=int(expr,16)&0xffffffff
            elif re.fullmatch(r'r\d+',expr):
                result=values.get(expr)
            else:
                m=re.fullmatch(r'(r\d+) ([+\-]|<<|>>) (r\d+|-?0x[0-9a-f]+)',expr)
                if m:
                    left=values.get(m[1]); right=values.get(m[3]) if m[3].startswith('r') else int(m[3],16)
                    if left is not None and right is not None:
                        if m[2]=='+': result=(left+right)&0xffffffff
                        elif m[2]=='-': result=(left-right)&0xffffffff
                        elif m[2]=='<<' and 0<=right<32: result=(left<<right)&0xffffffff
                        elif m[2]=='>>' and 0<=right<32: result=left>>right
            values.pop(reg,None)
            if result is not None:
                values[reg]=result
                if result in wanted:
                    leads.append({'instruction':hex(row['address']),'file_offset':hex(file_offset(regions,row['address'])),'register':reg,'string_address':hex(result),'text':wanted[result],'method':'linear constant tracking; verify control flow'})
        else:
            mutated=re.match(r'(r\d+) (?:=|\+=|-=|\^=|&=|\|=|<=)',asm)
            if mutated:
                values.pop(mutated[1],None)
        if asm.startswith('call '):
            for reg in ('r0','r1','r2','r3'): values.pop(reg,None)
        if asm=='rts' or asm.startswith('pc = ') or asm.startswith('{pc,') or asm.startswith('[--sp] = '): values.clear()
    result={'label':label,'instruction_rows':len(rows),'direct_calls':calls,'string_leads':leads,'regions':regions,'unknown_instruction_rows':sum('unknown instruction' in r['asm'] for r in rows)}
    (ROOT/'disassembly'/f'{label}-index.json').write_text(json.dumps(result,indent=2))
    print(label,'instructions',len(rows),'calls',len(calls),'unknowns (including embedded data)',result['unknown_instruction_rows'])
    print(json.dumps(leads,indent=2))
    return result


if __name__=='__main__':
    for label in ('controller','receiver'): analyze(label)
