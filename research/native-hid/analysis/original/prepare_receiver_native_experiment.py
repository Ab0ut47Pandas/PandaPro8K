"""Offline receiver-only native fast-axis/rate experiment; no device APIs.

Preserves controller firmware and receiver packet layout/extra controls.
Checks native control flow before reusing an otherwise redundant pointer load.
"""
from collections import deque
import hashlib
import itertools
import json
from pathlib import Path
import random
import re
import struct
import subprocess

from analyze_disassembly import instructions, direct_target, file_offset
from make_analysis_elf import make_elf, ROOT
from prepare_gate_experiment import elf_text
from repack_analysis import repack
import inspect_firmware as decoder


def sha(data):return hashlib.sha256(data).hexdigest()


def main():
    destination=ROOT.parent.parent/'outputs/gamesir-wireless-4k-investigation/candidate-receiver-fast-native'
    if destination.exists():raise FileExistsError('Refusing to overwrite an experiment')
    original_path=ROOT.parent.parent/'outputs/gamesir-firmware-research/receiver-1.3.5-app.bin'
    original=original_path.read_bytes()
    assert sha(original)=='bf2b2b0965d6bfe11a4059be81a0acea1436dc662b0fd2f580bbf10634a66cca'
    regions=json.loads((ROOT/'disassembly/receiver-regions.json').read_text())
    rows=instructions('receiver');by_address={r['address']:r for r in rows}
    def table(base,n):
        off=file_offset(regions,base)
        return [base+2*struct.unpack_from('<H',original,off+2*i)[0] for i in range(n)]
    hats=table(0x6004bc8,15)
    assert all(t==0x6004be6 or 0x6004d58<=t<=0x6004d78 for t in hats)
    modes=table(0x600457c,17);assert modes[11]==0x60048f4
    # Over-approximate straight-line and conditional paths from the native
    # extended builder. The intervening PS4 builder exits separately.
    pending=deque([0x6004b42]);seen=set()
    while pending:
        at=pending.popleft()
        if at in seen or at==0x6004dda:continue
        assert at in by_address,hex(at)
        row=by_address[at];asm=row['asm'];seen.add(at)
        assert 'unknown instruction' not in asm, (hex(at),asm)
        if asm.startswith('tbh '):
            assert at==0x6004bc6
            pending.extend(hats);continue
        dst=direct_target(row)
        if asm.startswith('goto '):
            assert dst is not None;pending.append(dst);continue
        if asm.startswith('if ') or asm.startswith('ifs '):
            if dst is not None:pending.append(dst)
        if asm.startswith('call '):assert dst==0x1fd60, (hex(at),asm)
        assert not (asm.startswith('{pc,') or asm.startswith('pc = ') or asm=='rts'), (hex(at),asm)
        pending.append(at+len(row['bytes']))
    writes=[r for r in rows if r['address'] in seen and re.match(r'r5\s*(?:=|\+=|-=|\|=|&=|<=)',r['asm'])]
    assert [r['address'] for r in writes]==[0x6004b42]
    assert 0x6004c6a not in seen
    incoming=[hex(r['address']) for r in rows if (direct_target(r) or -1) in range(0x6004ddc,0x6004e02)]
    assert not incoming, incoming
    probe=elf_text(ROOT/'toolchain/receiver_native_rate_probe.o')
    assert len(probe)==44 and probe[:4].hex()=='40214020'
    clamp_at=file_offset(regions,0x600418c)
    axes_at=file_offset(regions,0x6004dda)
    assert original[clamp_at:clamp_at+2]==probe[:2]
    assert original[axes_at:axes_at+40].hex()=='01e11f6205f128651a43da481a42da471a45da4a1a44da491a47da4c1a46da4b1a49da4e1a48da4d'
    candidate=bytearray(original)
    candidate[clamp_at:clamp_at+2]=probe[2:4]
    candidate[axes_at:axes_at+40]=probe[4:]
    candidate=bytes(candidate)
    assert len(candidate)==len(original)
    destination.mkdir(parents=True)
    app=destination/'receiver-fast-native-UNTESTED.bin';app.write_bytes(candidate)
    elf=destination/'receiver-fast-native.elf';make_elf(candidate,elf,regions)
    tool=ROOT/'toolchain/extracted/C$/JL/pi32/bin/llvm-objdump.exe'
    def disassemble(start,end,name):
        run=subprocess.run([str(tool),'-d','-mcpu=r3','-print-imm-hex',f'-start-address={hex(start)}',f'-stop-address={hex(end)}',str(elf)],capture_output=True,check=True)
        text=run.stdout.decode('utf-8','replace');(destination/name).write_text(text)
        return text
    listing=disassemble(0x6004dda,0x6004e02,'fast-axis-disassembly.txt')
    disassemble(0x600416e,0x60041be,'rate-clamp-disassembly.txt')
    assert 'r3 = r6 + 0x50' in listing and listing.count('r2 = h[r3+')==4
    assert '#' not in listing and 'r5 = ' not in listing
    # Execute the replacement's actual disassembly over edge-case and random
    # inputs. Verify byte order, every other output byte, and trigger pointers.
    program=[]
    for line in listing.splitlines():
        m=re.match(r'\s*([0-9a-f]+):\s+(?:[0-9a-f]{2}\s+)+\s*(.*)',line)
        if m:program.append(m[2].strip())
    assert len(program)==18
    rng=random.Random(0x135)
    tests=list(itertools.product((-32768,-1,0,1,32767),repeat=4))
    tests.extend(tuple(rng.randrange(-32768,32768) for _ in range(4)) for _ in range(1000))
    for axes in tests:
        regs={'r0':0xa55a,'r5':0x2000,'r6':0x1000};memory=bytearray(b'\xa5'*0x3000)
        memory[0x1050:0x1058]=struct.pack('<hhhh',*axes)
        for text in program:
            m=re.fullmatch(r'(r\d+) = (r\d+) \+ (0x[0-9a-f]+)',text)
            if m:regs[m[1]]=regs[m[2]]+int(m[3],16);continue
            m=re.fullmatch(r'(r\d+) = h\[(r\d+)\+(0x[0-9a-f]+)\] \(u\)',text)
            if m:regs[m[1]]=struct.unpack_from('<H',memory,regs[m[2]]+int(m[3],16))[0];continue
            m=re.fullmatch(r'b\[(r\d+)\+(0x[0-9a-f]+)\] = (r\d+)',text)
            if m:memory[regs[m[1]]+int(m[2],16)]=regs[m[3]]&255;continue
            m=re.fullmatch(r'(r\d+) = (r\d+) >> (0x[0-9a-f]+)',text)
            if m:regs[m[1]]=regs[m[2]]>>int(m[3],16);continue
            raise AssertionError(text)
        assert memory[0x2007:0x200f]==struct.pack('>hhhh',*axes)
        assert memory[0x2000:0x2007]==b'\xa5'*7 and memory[0x200f:0x2025]==b'\xa5'*22
        assert regs['r0']==0xa55a and regs['r1']==0x121f and regs['r5']==0x2000 and regs['r6']==0x1000
    metadata=json.loads((ROOT/'inspection.json').read_text())
    item=next(e for e in metadata if e['file']=='GS_G7_Pro_CE_DG_V135_Key.ufw')
    source=decoder.SOURCE/item['file'];stock=source.read_bytes()
    assert sha(stock)=='439e185b5e4dc3cf62e359aca82775a878bae9af7a05ce0c904588561c6d262a'
    assert repack(stock,item)==stock
    package=repack(stock,item,candidate)
    target=destination/'GS_G7_Pro_CE_DG_V135_Key-fast-native-UNTESTED.ufw.research';target.write_bytes(package)
    restore=destination.parent/'GS_G7_Pro_CE_DG_V135_Key-STOCK-RESTORE.ufw'
    with restore.open('xb') as f:f.write(stock)
    assert sha(restore.read_bytes())==sha(stock)
    decoder.DEST=destination/'decoded-validation';decoder.DEST.mkdir()
    checked=decoder.inspect(target)
    assert checked['header_crc_valid'] and checked['entry_table_crc_valid'] and checked['app_crc_valid']
    assert checked['app_sha256']==sha(candidate)
    for old,new in zip(item['entries'],checked['entries']):
        off,size=new['offset'],new['size'];assert old['offset']==off and old['size']==size
        if new['kind'] in (0,0x20):assert decoder.crc(package[off:off+size])==new['crc']
        else:assert package[off:off+size]==stock[off:off+size]
    assert sha(source.read_bytes())==sha(stock) and sha(original_path.read_bytes())==sha(original)
    changes=[hex(i) for i,(a,b) in enumerate(zip(original,candidate)) if a!=b]
    result={'status':'offline validated receiver candidate; not flashed','receiver_version':'1.3.5',
        'package':str(target),'package_sha256':sha(package),'application_sha256':sha(candidate),
        'package_bytes':len(package),'application_bytes':len(candidate),'changed_app_bytes':changes,
        'patches':[{'address':'0x0600418c','purpose':'Disable the extended-native forced minimum divisor 8; retain configured rate and existing mode-C/global clamps.'},
                   {'address':'0x06004dda','length':40,'purpose':'Use the existing fast XInput signed-16-bit axis buffer at context+0x50; retain native big-endian report encoding and legacy trigger pointer.'}],
        'native_mode_table_case':11,'native_hat_table_targets':[hex(t) for t in hats],
        'native_reachable_instruction_count_before_axes':len(seen),'r5_invariant_write':'0x06004b42',
        'ps4_r5_clobber_not_reachable':True,'axis_vector_checks_passed':len(tests),
        'axis_block_size_preserved':True,'all_non_axis_report_bytes_preserved_in_vector_checks':True,
        'existing_high_speed_mode_whitelist_unchanged':True,'stock_roundtrip_identical':True,
        'known_nested_crcs_valid':True,'non_flash_payloads_unchanged':True,'installed_package_unchanged':True,
        'controller_firmware_untouched':True,'restore_sha256':sha(stock),
        'limits':['Receiver recovery identity and vendor file acceptance still require verification.','Fast radio data availability and high-speed native operation must be physically tested.','No pairing/NVM dump or firmware readback has been made.']}
    (destination/'validation.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result,indent=2))


if __name__=='__main__':main()
