from pathlib import Path
import argparse,json,re
from analyze_disassembly import instructions,direct_target,file_offset,ROOT

parser=argparse.ArgumentParser()
parser.add_argument('label',choices=['controller','receiver'])
parser.add_argument('start',type=lambda s:int(s,0))
parser.add_argument('end',type=lambda s:int(s,0))
args=parser.parse_args()
regions=json.loads((ROOT/'disassembly'/f'{args.label}-regions.json').read_text())
filename='controller-2.2.9-app.bin' if args.label=='controller' else 'receiver-1.3.5-app.bin'
data=(ROOT.parent.parent/'outputs/gamesir-firmware-research'/filename).read_bytes()
for row in instructions(args.label):
    if not args.start<=row['address']<args.end: continue
    asm=row['asm']; notes=[]
    target=direct_target(row)
    if target is not None: notes.append('target '+hex(target))
    m=re.fullmatch(r'r\d+ = (0x[0-9a-f]+)',asm)
    if m:
        off=file_offset(regions,int(m[1],16))
        if off is not None:
            end=data.find(b'\0',off,min(len(data),off+180))
            if end>off+4 and re.fullmatch(rb'[\t\r\n -~]+',data[off:end]):
                notes.append(repr(data[off:end].decode('ascii')))
    suffix=' ; '+', '.join(notes) if notes else ''
    print(f"{row['address']:08x} {asm}{suffix}")
