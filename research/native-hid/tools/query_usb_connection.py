"""Read USB speed and descriptors for one exact GameSir connection.

Only GET_NODE_CONNECTION_INFORMATION_EX and GET_DESCRIPTOR_FROM_NODE_CONNECTION
IOCTLs are allowed. No reset, port changes, driver changes, firmware writes,
configuration writes, or HID output reports.
Structures and constants follow Microsoft's win32metadata USB headers.
"""
import argparse
import ctypes as C
from ctypes import wintypes as W
import json
from pathlib import Path
import struct
import subprocess
import sys
import uuid

import windows_hid_read as hid

def hub_paths():
    guid=hid.GUID.from_buffer_copy(uuid.UUID('f18a0e88-c30c-11d0-8815-00a0c906bed8').bytes_le)
    devices=hid.getdevs(C.byref(guid),None,None,0x12)
    try:
        index=0
        while True:
            iface=hid.IFACE(cbSize=C.sizeof(hid.IFACE))
            if not hid.enumif(devices,None,C.byref(guid),index,C.byref(iface)):break
            index+=1;needed=W.DWORD()
            hid.detail(devices,C.byref(iface),None,0,C.byref(needed),None)
            buffer=C.create_string_buffer(needed.value)
            W.DWORD.from_buffer(buffer).value=8 if C.sizeof(C.c_void_p)==8 else 6
            if hid.detail(devices,C.byref(iface),buffer,len(buffer),C.byref(needed),None):
                yield C.wstring_at(C.addressof(buffer)+4)
    finally:hid.destroy(devices)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--pid',default='10b8',choices=['10b8','10c6','10c5'])
    parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    if args.output.exists():raise FileExistsError('Refusing to overwrite USB evidence')
    script="""
$d=@(Get-PnpDevice -PresentOnly | Where-Object {$_.InstanceId -like 'USB\\VID_3537&PID_PIDVALUE\\*'});
if($d.Count -ne 1){throw 'Expected one exact GameSir USB device'};
$p=Get-PnpDeviceProperty -InstanceId $d[0].InstanceId -KeyName DEVPKEY_Device_Parent;
$a=Get-PnpDeviceProperty -InstanceId $d[0].InstanceId -KeyName DEVPKEY_Device_Address;
[pscustomobject]@{device=$d[0].InstanceId;parent=$p.Data;port=$a.Data} | ConvertTo-Json -Compress
""".replace('PIDVALUE',args.pid)
    check=subprocess.run(['powershell','-NoProfile','-Command',script],capture_output=True,text=True,timeout=15)
    if check.returncode:raise RuntimeError(check.stderr)
    identity=json.loads(check.stdout);port=int(identity['port'])
    if not 1<=port<=255:raise RuntimeError('Unexpected USB port')
    token=identity['parent'].lower().replace('\\','#')
    selected=[p for p in hub_paths() if token in p.lower()]
    if len(selected)!=1:raise RuntimeError('Could not select exact parent hub: '+repr(selected))
    handle=hid.openfile(selected[0],0,3,None,3,0,None)
    if handle==hid.INVALID:raise C.WinError(C.get_last_error())
    ioctl=hid.proto(hid.K,'DeviceIoControl',W.BOOL,[W.HANDLE,W.DWORD,C.c_void_p,W.DWORD,C.c_void_p,W.DWORD,C.POINTER(W.DWORD),C.c_void_p])
    def query(code,buffer):
        if code not in (0x220448,0x220410):raise ValueError('IOCTL not permitted')
        returned=W.DWORD()
        if not ioctl(handle,code,buffer,len(buffer),buffer,len(buffer),C.byref(returned),None):raise C.WinError(C.get_last_error())
        return buffer.raw[:returned.value]
    try:
        buffer=C.create_string_buffer(8192);struct.pack_into('<I',buffer,0,port)
        info=query(0x220448,buffer)
        if len(info)<35:raise RuntimeError('Truncated connection response')
        descriptor=info[4:22];vid,pid=struct.unpack_from('<HH',descriptor,8)
        if vid!=0x3537 or pid!=int(args.pid,16):raise RuntimeError('Live hub port identity mismatch')
        speed=info[23];n_pipes=struct.unpack_from('<I',info,27)[0]
        pipes=[]
        for i in range(n_pipes):
            at=35+11*i
            if at+11>len(info):raise RuntimeError('Truncated pipe array')
            endpoint=info[at:at+7]
            pipes.append({'address':hex(endpoint[2]),'type':endpoint[3]&3,'max_packet':struct.unpack_from('<H',endpoint,4)[0],'bInterval':endpoint[6],'raw':endpoint.hex()})
        buffer=C.create_string_buffer(4108)
        struct.pack_into('<IBBHHH',buffer,0,port,0x80,6,0x0200,0,4096)
        cfg=query(0x220410,buffer)[12:]
        if len(cfg)<9 or cfg[:2]!=bytes([9,2]):raise RuntimeError('Invalid configuration descriptor')
        total=struct.unpack_from('<H',cfg,2)[0];cfg=cfg[:total]
        if len(cfg)!=total:raise RuntimeError('Truncated configuration descriptor')
        entries=[];at=0;interface=None
        while at<len(cfg):
            length,kind=cfg[at:at+2]
            if length<2 or at+length>len(cfg):raise RuntimeError('Malformed descriptor chain')
            d=cfg[at:at+length];entry={'offset':at,'type':kind,'raw':d.hex()}
            if kind==4 and length>=9:
                interface=d[2];entry.update(interface=interface,alternate=d[3],class_code=d[5],subclass=d[6],protocol=d[7])
            if kind==5 and length>=7:
                interval=(2**(d[6]-1)*0.125 if speed==2 and 1<=d[6]<=16 else d[6] if speed==1 else None)
                entry.update(interface=interface,address=hex(d[2]),transfer_type=d[3]&3,max_packet=struct.unpack_from('<H',d,4)[0],bInterval=d[6],interval_ms=interval)
            entries.append(entry);at+=length
        result={'vid_pid':'3537:'+args.pid,'usb_speed_code':speed,'usb_speed':{0:'low',1:'full',2:'high',3:'super'}.get(speed,'unknown'),'device_descriptor':descriptor.hex(),'bcd_usb':hex(struct.unpack_from('<H',descriptor,2)[0]),'open_pipes':pipes,'configuration_bytes':len(cfg),'configuration':entries,'read_only':True}
        args.output.parent.mkdir(parents=True,exist_ok=True)
        with args.output.open('x',encoding='utf-8') as f:json.dump(result,f,indent=2)
        print(json.dumps(result,indent=2))
    finally:hid.close(handle)

if __name__=='__main__':main()
