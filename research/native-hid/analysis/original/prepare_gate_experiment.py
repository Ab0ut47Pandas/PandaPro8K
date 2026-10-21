"""Prepare a NON-FLASHABLE application-only experiment on an extracted copy.

No device APIs. No original firmware writes. No UFW output.
The assembler probe independently verifies both instruction encodings.
"""
from pathlib import Path
import hashlib,json,struct,subprocess
from make_analysis_elf import make_elf,ROOT

EXPECTED='fa554844a381e8f43a63aa79c8b86de412893c93c8309c533658c30c6633502d'


def elf_text(path):
    data=path.read_bytes()
    shoff=struct.unpack_from('<I',data,32)[0]
    shentsize,shnum,shstr=struct.unpack_from('<HHH',data,46)
    headers=[struct.unpack_from('<IIIIIIIIII',data,shoff+i*shentsize) for i in range(shnum)]
    strings=headers[shstr]
    names=data[strings[4]:strings[4]+strings[5]]
    for h in headers:
        name=names[h[0]:].split(b'\0')[0]
        if name==b'.text':return data[h[4]:h[4]+h[5]]
    raise ValueError('No probe text section')


def main():
    source=ROOT.parent.parent/'outputs/gamesir-firmware-research/controller-2.2.9-app.bin'
    original=source.read_bytes()
    assert hashlib.sha256(original).hexdigest()==EXPECTED
    regions=json.loads((ROOT/'disassembly/controller-regions.json').read_text())
    region=next(r for r in regions if r['name']=='.data')
    address=0x108f0e
    offset=region['offset']+address-region['address']
    probe=elf_text(ROOT/'toolchain/threshold_probe.o')
    before=probe[:4]
    after=probe[44:48]
    assert before==bytes.fromhex('81f91314')
    assert after==bytes.fromhex('81f91304')
    assert original[offset:offset+4]==before
    candidate=bytearray(original)
    candidate[offset:offset+4]=after
    changed=[i for i,(a,b) in enumerate(zip(original,candidate)) if a!=b]
    assert changed==[offset+3] and len(candidate)==len(original)
    out=ROOT/'experiments'
    out.mkdir(exist_ok=True)
    (out/'controller-2.2.9-gate-2-ANALYSIS-ONLY.bin').write_bytes(candidate)
    make_elf(candidate,out/'controller-gate-2-analysis.elf',regions)
    tool=ROOT/'toolchain/extracted/C$/JL/pi32/bin/llvm-objdump.exe'
    dis=subprocess.run([str(tool),'-d','-mcpu=r3','-print-imm-hex','-start-address=0x108f0a','-stop-address=0x108f38',str(out/'controller-gate-2-analysis.elf')],stdout=subprocess.PIPE,stderr=subprocess.PIPE,check=True)
    text=dis.stdout.decode('utf-8','replace')
    assert 'if (r1 < 0x2) goto 0x26' in text
    (out/'controller-gate-2-disassembly.txt').write_text(text)
    plan={
      'status':'offline application experiment; package-copy validation is recorded separately in repack-validation.json; no hardware test',
      'original_app_sha256':EXPECTED,
      'candidate_app_sha256':hashlib.sha256(candidate).hexdigest(),
      'application_bytes':len(original),
      'instruction_runtime_address':hex(address),
      'instruction_application_offset':hex(offset),
      'before_instruction_hex':before.hex(),
      'after_instruction_hex':after.hex(),
      'changed_application_byte_offset':hex(changed[0]),
      'changed_byte_count':len(changed),
      'intent':'lower the minimum changed-state transmission gate from counter value 10 to 2',
      'timing_interpretation':'Counter gains 2 in a callback registered with period 2 using a timer API consistent with milliseconds. Expected changed-state ceiling rises from about 100 Hz toward about 500 Hz, subject to other scheduling and transport limits.',
      'unchanged':'100-unit idle keepalive gate, comparison/snapshot logic, packet format, USB identity, all other application bytes; no device data was read or written',
      'remaining_checks':['Current-firmware native HID baseline','Timer API identification and runtime timing confirmation','Complete update integrity and authorization requirements','Recovery and calibration preservation','Physical stability, latency, rate and button testing'],
      'source_unchanged':hashlib.sha256(source.read_bytes()).hexdigest()==EXPECTED,
    }
    (out/'gate-experiment-plan.json').write_text(json.dumps(plan,indent=2))
    print(json.dumps(plan,indent=2))


if __name__=='__main__':main()
