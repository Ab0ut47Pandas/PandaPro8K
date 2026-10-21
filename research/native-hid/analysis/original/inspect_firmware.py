"""Offline inspection only. Never opens a controller or modifies source firmware.

Container/cipher decoding follows the public-domain gamesir-fw implementation:
https://github.com/xsyetopz/gamesir-fw/tree/master/crates/gsfw-core/src/formats
"""
from pathlib import Path
import binascii
import argparse
import hashlib
import json
import re
import os
import struct

SOURCE = Path(os.environ.get('LOCALAPPDATA', Path.home() / 'AppData' / 'Local')) / 'Programs' / 'GameSir Connect' / 'resources' / 'dfu' / 'packages'
DEST = Path.cwd() / 'firmware-analysis'

def crc(data):
    return binascii.crc_hqx(data, 0)

def enc(data, key=65535):
    out = bytearray()
    for value in data:
        out.append(value ^ (key & 255))
        key = ((key << 1) ^ (0x1021 if key & 0x8000 else 0)) & 65535
    return bytes(out)

def sfc(data, key, base=0):
    return b''.join(enc(data[i:i+32], key ^ (((base+i) >> 2) & 65535)) for i in range(0, len(data), 32))

def name(data):
    return data.split(b'\0')[0].decode('ascii', errors='replace')

def jlfs(data, decoder):
    entries = []
    for at in range(0, min(len(data), 32768)-31, 32):
        plain = decoder(data[at:at+32], at)
        head_crc, body_crc, offset, size = struct.unpack_from('<HHII', plain)
        if crc(plain[2:]) != head_crc:
            break
        entries.append({'name': name(plain[16:32]), 'offset': offset, 'size': size, 'crc': body_crc})
    return entries

def inspect(path):
    data = path.read_bytes()
    original_hash = hashlib.sha256(data).hexdigest()
    head = enc(data[:64])
    head_crc, table_crc, file_size, count = struct.unpack_from('<HHIH', head)
    if count > 100 or file_size != len(data) or crc(head[2:]) != head_crc:
        raise ValueError('Unexpected or invalid firmware header: '+path.name)
    table = data[64:64+count*80]
    if crc(table) != table_crc:
        raise ValueError('Entry table CRC mismatch: '+path.name)
    entries = []
    for index in range(count):
        e = enc(table[index*80:(index+1)*80])
        kind, entry_index, body_crc = struct.unpack_from('<HHH', e)
        offset, size, padded = struct.unpack_from('<III', e, 8)
        if offset+size > len(data):
            raise ValueError('Entry outside source image')
        entries.append({'kind': kind, 'index': entry_index, 'crc': body_crc, 'offset': offset, 'size': size, 'padded': padded, 'name': name(e[64:80])})
    isd = next(e for e in entries if e['kind'] == 0x34)
    block = data[isd['offset']:isd['offset']+32]
    # These JL703N configs begin with newlines, unlike the AC695X example.
    # Match a known opening, then independently validate the complete config CRC.
    config_body = data[isd['offset']:isd['offset']+isd['size']]
    key = None
    prefixes = [b'\n'*n+b'[EXTRA_CFG_PARAM]' for n in (14, 9)]
    for prefix in (*prefixes, b'#'*32):
        low = block[0] ^ prefix[0]
        for high in range(256):
            block_key = (high << 8) | low
            if not enc(block, block_key).startswith(prefix):
                continue
            candidate = block_key ^ ((isd['offset'] >> 2) & 65535)
            if crc(sfc(config_body, candidate, isd['offset'])) == isd['crc']:
                key = candidate
                break
        if key is not None:
            break
    if key is None and crc(sfc(config_body, 65535, isd['offset'])) == isd['crc']:
        key = 65535
    out = DEST / path.stem
    out.mkdir(exist_ok=True)
    result = {'file': path.name, 'sha256': original_hash, 'chip_label': name(head[16:]), 'bytes': len(data), 'header_crc_valid': True, 'entry_table_crc_valid': True, 'chip_key_recovered': key is not None, 'entries': entries}
    if key is not None:
        isd_plain = sfc(data[isd['offset']:isd['offset']+isd['size']], key, isd['offset'])
        result['isd_crc_valid'] = crc(isd_plain) == isd['crc']
        if result['isd_crc_valid']:
            (out/'isd_config.ini').write_bytes(isd_plain)
            result['isd_text'] = isd_plain.decode('ascii', errors='replace')
        flash_entry = next(e for e in entries if e['kind'] == 0)
        flash = data[flash_entry['offset']:flash_entry['offset']+flash_entry['size']]
        top = jlfs(flash[32:], lambda b, at: enc(b))
        result['flash_directory'] = top
        app_dir = next((e for e in top if e['name']=='app_dir_head'), None)
        if app_dir:
            sub = jlfs(flash[app_dir['offset']:], lambda b, at: sfc(b, key, at))
            result['app_directory'] = sub
            app = next((e for e in sub if e['name']=='app.bin'), None)
            if app:
                at = app_dir['offset']+app['offset']
                if at + app['size'] > len(flash):
                    raise ValueError('Application outside stored flash image')
                plain = sfc(flash[at:at+app['size']], key, app['offset'])
                result['app_crc_valid'] = crc(plain) == app['crc']
                if result['app_crc_valid']:
                    (out/'app.bin').write_bytes(plain)
                    result['app_sha256'] = hashlib.sha256(plain).hexdigest()
                    matches = []
                    for m in re.finditer(rb'[ -~]{5,}', plain):
                        value = m.group()
                        if re.search(rb'(?i)boot|update|hid|input|mode|key|factory|test|debug|usb|gamepad|1000|8000', value):
                            matches.append({'offset': m.start(), 'text': value.decode('ascii')[:240]})
                    result['app_matching_strings'] = matches
    result['source_hash_unchanged'] = hashlib.sha256(path.read_bytes()).hexdigest() == original_hash
    return result

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=SOURCE, help='Directory containing GameSir UFW packages (read only)')
    parser.add_argument('--output', type=Path, default=DEST, help='Directory for decoded analysis files')
    args = parser.parse_args()
    SOURCE = args.source.resolve()
    DEST = args.output.resolve()
    if SOURCE == DEST or SOURCE in DEST.parents:
        parser.error('Output must be outside the original firmware package directory')
    DEST.mkdir(parents=True, exist_ok=True)
    results = [inspect(p) for p in sorted(SOURCE.glob('GS_G7_Pro_CE_*'))]
    if not results:
        parser.error('No matching firmware packages found')
    (DEST/'inspection.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
    for r in results:
        print(json.dumps({k:v for k,v in r.items() if k not in ('isd_text','app_matching_strings','entries','flash_directory','app_directory')}, indent=2))
