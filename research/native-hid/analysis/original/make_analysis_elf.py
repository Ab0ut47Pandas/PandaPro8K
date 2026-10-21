"""Wrap extracted firmware regions in an ELF for offline disassembly only.

This does not reconstruct the vendor's original ELF, symbols, or update package.
ELF machine/flags come from a tiny object compiled with the vendor's compiler.
"""
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parent


def make_elf(data, out, regions):
    probe = (ROOT/'toolchain/probe.o').read_bytes()
    machine = struct.unpack_from('<H', probe, 18)[0]
    flags = struct.unpack_from('<I', probe, 36)[0]
    names = b'\0'
    name_offsets = []
    for region in regions:
        name_offsets.append(len(names))
        names += region['name'].encode('ascii')+b'\0'
    strings_name = len(names)
    names += b'.shstrtab\0'
    image = bytearray(b'\0'*0x100)
    sections = [b'\0'*40]
    for region, name_at in zip(regions, name_offsets):
        file_at = len(image)
        start, size = region['offset'], region['size']
        if start < 0 or size < 0 or start+size > len(data):
            raise ValueError('Region outside extracted application')
        image.extend(data[start:start+size])
        image.extend(b'\0'*(-len(image) % 4))
        sections.append(struct.pack('<IIIIIIIIII',name_at,1,region.get('flags',6),region['address'],file_at,size,0,0,2,0))
    strings_at = len(image)
    image.extend(names)
    image.extend(b'\0'*(-len(image) % 4))
    sections.append(struct.pack('<IIIIIIIIII',strings_name,3,0,0,strings_at,len(names),0,0,1,0))
    sections_at = len(image)
    image.extend(b''.join(sections))
    ident = b'\x7fELF\x01\x01\x01'+b'\0'*9
    image[:52] = struct.pack('<16sHHIIIIIHHHHHH',ident,2,machine,1,0x06000100,0,sections_at,flags,52,0,0,40,len(sections),len(sections)-1)
    out.parent.mkdir(parents=True,exist_ok=True)
    out.write_bytes(image)


if __name__ == '__main__':
    for label, file in [('controller','controller-2.2.9-app.bin'),('receiver','receiver-1.3.5-app.bin')]:
        data=(ROOT.parent.parent/'outputs/gamesir-firmware-research'/file).read_bytes()
        out=ROOT/'disassembly'/f'{label}-flat.elf'
        make_elf(data,out,[{'name':'.text','offset':0,'size':len(data),'address':0x06000100}])
        print(label,len(data),out)
        copies=[]
        for offset,section_name in [(0x28,'.data'),(0x44,'.data_code')]:
            if data[offset:offset+2] != b'\xc4\xff' or data[offset+6:offset+8] != b'\xc1\xff' or data[offset+12:offset+14] != b'\xc2\xff':
                raise ValueError('Unexpected startup copy loop')
            destination=struct.unpack_from('<I',data,offset+2)[0]
            source=struct.unpack_from('<I',data,offset+8)[0]
            size=struct.unpack_from('<I',data,offset+14)[0]
            # GameSir also places executable callbacks in the first copied
            # region. Mark it executable for analysis; it still contains data.
            copies.append({'name':section_name,'offset':source-0x06000100,'size':size,'address':destination,'flags':7 if section_name=='.data' else 6})
        regions=[{'name':'.text','offset':0,'size':copies[0]['offset'],'address':0x06000100},*copies]
        if sum(r['size'] for r in regions)!=len(data):
            raise ValueError('Startup regions do not cover the application exactly')
        make_elf(data,ROOT/'disassembly'/f'{label}-mapped.elf',regions)
        import json
        (ROOT/'disassembly'/f'{label}-regions.json').write_text(json.dumps(regions,indent=2))
        print('mapped regions:',json.dumps(regions))
