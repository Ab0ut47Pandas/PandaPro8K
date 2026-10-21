"""Prepare and verify a controller-only native-rate candidate on local copies.

No device APIs, installed-app writes or automatic firmware operation.
Preserves the previously tested gate-2 change and replaces the native-only
1-ms wait branch with two assembler-verified NOPs of the same total size.
"""
from pathlib import Path
import hashlib
import json
import subprocess

from analyze_disassembly import file_offset, instructions, direct_target
from make_analysis_elf import make_elf, ROOT
from prepare_gate_experiment import elf_text
import inspect_firmware as decoder
from repack_analysis import repack


def sha(data):return hashlib.sha256(data).hexdigest()


def main():
    destination=ROOT.parent.parent/'outputs/gamesir-4k-investigation/candidate-gate2-native-rate'
    if destination.exists():raise FileExistsError('Candidate already exists; refusing overwrite')
    stock_path=ROOT.parent.parent/'outputs/gamesir-firmware-research/controller-2.2.9-app.bin'
    stock=stock_path.read_bytes()
    assert sha(stock)=='fa554844a381e8f43a63aa79c8b86de412893c93c8309c533658c30c6633502d'
    gate2_path=ROOT/'experiments/controller-2.2.9-gate-2-ANALYSIS-ONLY.bin'
    gate2=gate2_path.read_bytes()
    assert sha(gate2)=='a246aa6db7027acb8eff7b1cfc6c1e1371dff1f5ab709a8b0ba7e1c0c06dfd72'
    probe=elf_text(ROOT/'toolchain/native_report_gate_probe.o')
    assert len(probe)==492 and probe[:4].hex()=='01f8f100' and probe[-4:]==b'\0'*4
    regions=json.loads((ROOT/'disassembly/controller-regions.json').read_text())
    address=0x60266ae
    offset=file_offset(regions,address)
    assert offset is not None and stock[offset:offset+4]==probe[:4] and gate2[offset:offset+4]==probe[:4]
    rows=instructions('controller')
    incoming=[hex(row['address']) for row in rows if (direct_target(row) or -1) in range(address,address+4)]
    assert not incoming, incoming
    candidate=bytearray(gate2)
    candidate[offset:offset+4]=probe[-4:]
    candidate=bytes(candidate)
    changed=[i for i,(a,b) in enumerate(zip(gate2,candidate)) if a!=b]
    assert changed==[offset,offset+1,offset+2] and len(candidate)==len(stock)
    assert candidate[0xe4e81]==4 and stock[0xe4e81]==20
    destination.mkdir()
    app_path=destination/'controller-gate2-native-rate-UNTESTED.bin'
    app_path.write_bytes(candidate)
    elf=destination/'controller-gate2-native-rate.elf'
    make_elf(candidate,elf,regions)
    tool=ROOT/'toolchain/extracted/C$/JL/pi32/bin/llvm-objdump.exe'
    dis=subprocess.run([str(tool),'-d','-mcpu=r3','-print-imm-hex','-start-address=0x602669a','-stop-address=0x60266ea',str(elf)],capture_output=True,check=True)
    listing=dis.stdout.decode('utf-8','replace')
    assert '60266ae:    00 00' in listing and '60266b0:    00 00' in listing
    assert 'if (r1 == 0x0) goto 0x1e2' not in listing
    (destination/'disassembly.txt').write_text(listing)
    metadata=json.loads((ROOT/'inspection.json').read_text())
    item=next(m for m in metadata if m['file']=='GS_G7_Pro_CE_DV_V229_Key.ufw')
    source=decoder.SOURCE/item['file'];original=source.read_bytes()
    assert sha(original)=='4d2982df76d92f0ef7564a98005c8b01c25e0c9d1f995becbe1afe72e55882c4'
    assert repack(original,item)==original
    packaged=repack(original,item,candidate)
    path=destination/'GS_G7_Pro_CE_DV_V229_Key-gate2-native-rate-UNTESTED.ufw.research'
    path.write_bytes(packaged)
    decoder.DEST=destination/'decoded-validation';decoder.DEST.mkdir()
    verified=decoder.inspect(path)
    assert verified['header_crc_valid'] and verified['entry_table_crc_valid'] and verified['app_crc_valid']
    assert verified['app_sha256']==sha(candidate)
    for old,new in zip(item['entries'],verified['entries']):
        off,size=new['offset'],new['size']
        assert old['offset']==off and old['size']==size
        if new['kind'] in (0,0x20):assert decoder.crc(packaged[off:off+size])==new['crc']
        else:assert packaged[off:off+size]==original[off:off+size]
    assert sha(source.read_bytes())==sha(original) and sha(stock_path.read_bytes())==sha(stock)
    result={'status':'offline validated candidate; not flashed or hardware tested','package_file':str(path),
        'package_sha256':sha(packaged),'package_bytes':len(packaged),'application_sha256':sha(candidate),
        'application_bytes':len(candidate),'runtime_patch_address':hex(address),'application_patch_offset':hex(offset),
        'before_hex':probe[:4].hex(),'after_hex':probe[-4:].hex(),'changed_app_bytes_vs_gate2':[hex(i) for i in changed],
        'preserves_tested_gate2_change':True,'independent_assembler_encoding_check':True,'interior_direct_branch_targets':incoming,
        'package_roundtrip_identical':True,'known_nested_crcs_valid':True,'redecoded_app_matches':True,
        'non_flash_payloads_unchanged':True,'installed_package_unchanged':True,
        'intended_behavior':'Native extended report producer follows the existing configured USB rate instead of waiting for the 1-ms context+0x210C counter.',
        'evidence':['Mode-11 extended-only branch 0x0602669A-0x060266C6 gates on context+0x210C.','The counter increment helper 0x0602DE76 has one direct caller at 0x00120502.','Timer ISR calls it after four 250-us ticks or eight 125-us ticks.','Physical moving native HID measured 1000 fresh changes/sec; same wired XInput profile measured about 4000/sec.'],
        'remaining':['Fresh profile backup and recovery identity verification','Vendor updater target and integrity checks','Post-flash wired native movement/button/rate capture','Profile equality and normal XInput regression check']}
    (destination/'validation.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result,indent=2))


if __name__=='__main__':main()
