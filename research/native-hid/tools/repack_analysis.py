"""Offline-only UFW roundtrip and experimental copy generation.

Recomputes the observed application, area, flash, table, and header CRCs.
It does not prove authenticity/signature acceptance or produce a tested update.
Never opens a USB device or writes an installed firmware package.
"""
from pathlib import Path
import hashlib,json,struct
import inspect_firmware as decoder

ROOT=Path(__file__).resolve().parent


def package_key(raw, entry, known_plain):
    body=raw[entry['offset']:entry['offset']+32]
    low=body[0]^known_plain[0]
    for high in range(256):
        blockkey=(high<<8)|low
        if decoder.enc(body,blockkey)==known_plain[:32]:
            key=blockkey^((entry['offset']>>2)&65535)
            plain=decoder.sfc(raw[entry['offset']:entry['offset']+entry['size']],key,entry['offset'])
            if decoder.crc(plain)==entry['crc']:return key
    raise ValueError('No validated package key')


def patch_flash(raw,key,replacement):
    out=bytearray(raw)
    top=decoder.jlfs(raw[32:],lambda b,_:decoder.enc(b))
    base=next(e['offset'] for e in top if e['name']=='app_dir_head')
    records=decoder.jlfs(raw[base:],lambda b,at:decoder.sfc(b,key,at))
    area_head=records[0]
    assert area_head['name']=='app_area_head'
    area=bytearray(decoder.sfc(raw[base:base+area_head['size']],key,0))
    assert decoder.crc(area[32:])==area_head['crc']
    app_index=next(i for i,e in enumerate(records) if e['name']=='app.bin')
    app=records[app_index]
    previous=area[app['offset']:app['offset']+app['size']]
    assert decoder.crc(previous)==app['crc']
    if replacement is not None:
        assert len(replacement)==app['size']
        area[app['offset']:app['offset']+app['size']]=replacement
    at=app_index*32
    current=area[app['offset']:app['offset']+app['size']]
    struct.pack_into('<H',area,at+2,decoder.crc(current))
    struct.pack_into('<H',area,at,decoder.crc(area[at+2:at+32]))
    struct.pack_into('<H',area,2,decoder.crc(area[32:]))
    struct.pack_into('<H',area,0,decoder.crc(area[2:32]))
    out[base:base+len(area)]=decoder.sfc(area,key,0)
    verify=decoder.sfc(out[base:base+len(area)],key,0)
    assert decoder.crc(verify[32:])==struct.unpack_from('<H',verify,2)[0]
    assert decoder.crc(verify[2:32])==struct.unpack_from('<H',verify,0)[0]
    assert decoder.crc(verify[at+2:at+32])==struct.unpack_from('<H',verify,at)[0]
    assert decoder.crc(verify[app['offset']:app['offset']+app['size']])==struct.unpack_from('<H',verify,at+2)[0]
    return bytes(out)


def repack(raw, metadata, replacement=None):
    out=bytearray(raw)
    cfg=next(e for e in metadata['entries'] if e['kind']==0x34)
    known=(ROOT/Path(metadata['file']).stem/'isd_config.ini').read_bytes()
    key=package_key(raw,cfg,known)
    for i,entry in enumerate(metadata['entries']):
        if entry['kind'] not in (0,0x20):continue
        off,size=entry['offset'],entry['size']
        old=raw[off:off+size]
        assert decoder.crc(old)==entry['crc']
        new=patch_flash(old,key,replacement)
        assert len(new)==size
        out[off:off+size]=new
        table_at=64+i*80
        record=bytearray(decoder.enc(raw[table_at:table_at+80]))
        struct.pack_into('<H',record,4,decoder.crc(new))
        out[table_at:table_at+80]=decoder.enc(record)
    header=bytearray(decoder.enc(raw[:64]))
    struct.pack_into('<H',header,2,decoder.crc(out[64:64+len(metadata['entries'])*80]))
    struct.pack_into('<H',header,0,decoder.crc(header[2:]))
    out[:64]=decoder.enc(header)
    return bytes(out)


def main():
    metadata=json.loads((ROOT/'inspection.json').read_text())
    replacement=(ROOT/'experiments/controller-2.2.9-gate-2-ANALYSIS-ONLY.bin').read_bytes()
    results=[]
    for entry in metadata:
        source=decoder.SOURCE/entry['file']; original=source.read_bytes()
        rebuilt=repack(original,entry)
        assert rebuilt==original
        result={'source':entry['file'],'unedited_roundtrip_byte_identical':True}
        if '_DV_' in entry['file']:
            candidate=repack(original,entry,replacement)
            path=ROOT/'experiments'/(source.stem+'-gate-2.ufw.research')
            path.write_bytes(candidate)
            result.update(candidate_sha256=hashlib.sha256(candidate).hexdigest(),candidate_bytes=len(candidate),changed_package_bytes=sum(a!=b for a,b in zip(original,candidate)))
            decoder.DEST=ROOT/'experiments/validation';decoder.DEST.mkdir(exist_ok=True)
            checked=decoder.inspect(path)
            assert checked['header_crc_valid'] and checked['entry_table_crc_valid'] and checked['app_crc_valid']
            assert checked['app_sha256']==hashlib.sha256(replacement).hexdigest()
            result['candidate_redecoded_app_matches_experiment']=True
            # Verify every known outer entry CRC, and preserve every non-flash body exactly.
            for old,new in zip(entry['entries'],checked['entries']):
                off,size=new['offset'],new['size']
                assert old['offset']==off and old['size']==size
                if new['kind'] in (0,0x20):assert decoder.crc(candidate[off:off+size])==new['crc']
                else:assert candidate[off:off+size]==original[off:off+size]
            result['all_non_flash_payloads_unchanged']=True
            result['known_nested_crc_checks_pass']=True
        result['installed_source_unchanged']=hashlib.sha256(source.read_bytes()).hexdigest()==entry['sha256']
        assert result['installed_source_unchanged']
        results.append(result)
    (ROOT/'experiments/repack-validation.json').write_text(json.dumps(results,indent=2))
    print(json.dumps(results,indent=2))


if __name__=='__main__':main()
