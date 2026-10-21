"""Visible, read-only native GameSir HID tester for Windows.

Uses the same 37-byte interface and decoder as the firmware captures.
Button fields follow SDL_hidapi_gamesir.c. No output/feature reports,
configuration writes, mode switches, firmware operations, or input injection.
"""
from collections import deque
import ctypes as C
import json
from pathlib import Path
import statistics
import sys
import threading
import time
import tkinter as tk
from tkinter import ttk
import traceback

HERE=Path(__file__).resolve().parent
CAPTURE=HERE
sys.path.insert(0,str(CAPTURE))
import windows_hid_read as hid
from native_report import decode_state

BUTTONS={
    'A':(0,0x01),'B':(0,0x02),'X':(0,0x08),'Y':(0,0x10),
    'LB':(0,0x40),'RB':(0,0x80),'View':(1,0x04),'Menu':(1,0x08),
    'Home':(1,0x10),'L3':(1,0x20),'R3':(1,0x40),'Capture':(1,0x80),
    'L4':(2,0x40),'R4':(2,0x80),'L5':(3,0x01),'R5':(3,0x02),
}
HATS={0:(),1:('Up',),2:('Up','Right'),3:('Right',),4:('Right','Down'),
      5:('Down',),6:('Down','Left'),7:('Left',),8:('Left','Up')}
BG='#10151c';PANEL='#19222d';FG='#e9eff5';MUTED='#a3b2c3'
GRID='#3b4c5c';ACTIVE='#68e5c1';ACTIVE_FG='#09291f'

class Reader:
    def __init__(self):
        self.lock=threading.Lock();self.stop=threading.Event()
        self.connected=False;self.status='Looking for GameSir native HID mode…'
        self.axes=(0,0,0,0);self.triggers=(0,0);self.buttons=set();self.old_axes=None
        self.arrivals=deque();self.changes=deque();self.total=0;self.invalid=0
        self.presses={name:0 for name in list(BUTTONS)+['Up','Right','Down','Left']}
        self.last_press={name:0.0 for name in self.presses}
        self.last_packet=0.0;self.device=None
        self.thread=threading.Thread(target=self.run,name='native-hid-read',daemon=True)

    def ingest(self,packet,now):
        state=decode_state(packet)
        if state is None:
            with self.lock:self.invalid+=1
            return
        fields=bytes.fromhex(state['buttons'])
        buttons={name for name,(i,mask) in BUTTONS.items() if fields[i]&mask}
        buttons.update(HATS.get(fields[2]&0x0f,()))
        with self.lock:
            self.connected=True;self.status='Connected — native HID input is live'
            self.total+=1;self.last_packet=now;self.arrivals.append(now)
            if self.old_axes is not None and state['axes']!=self.old_axes:self.changes.append(now)
            self.axes=state['axes'];self.old_axes=state['axes'];self.triggers=state['triggers']
            for name in buttons-self.buttons:
                self.presses[name]+=1;self.last_press[name]=now
            self.buttons=buttons
            for events in (self.arrivals,self.changes):
                while events and events[0]<now-1.0:events.popleft()

    def snapshot(self):
        now=time.perf_counter()
        with self.lock:
            arrival=[t for t in self.arrivals if t>=now-1]
            changed=[t for t in self.changes if t>=now-1]
            live=self.connected and now-self.last_packet<1
            rate=lambda events: round((len(events)-1)/(events[-1]-events[0]),1) if len(events)>1 and events[-1]-events[0]>=0.25 else None
            intervals=[(b-a)*1000 for a,b in zip(changed,changed[1:])]
            return dict(connected=live,status=self.status if live or not self.connected else 'Connected, waiting for native reports…',
                        axes=list(self.axes),triggers=list(self.triggers),buttons=sorted(self.buttons) if live else [],
                        presses=dict(self.presses),last_press=dict(self.last_press),report_hz=rate(arrival),
                        fresh_hz=rate(changed),median_fresh_ms=round(statistics.median(intervals),4) if len(intervals)>=3 else None,
                        total_reports=self.total,unrecognized_reports=self.invalid,device=self.device,
                        last_packet_age_seconds=round(now-self.last_packet,3) if self.last_packet else None)

    def run(self):
        while not self.stop.is_set():
            handle=None;event=None;ov=None
            try:
                infos=[hid.describe(p) for p in hid.paths() if 'vid_3537&pid_10b8' in p.lower()]
                candidates=[d for d in infos if d.get('caps',{}).get('UsagePage')==0xfff0 and d['caps'].get('Usage')==0x40 and d['caps'].get('InputReportByteLength')==37]
                if len(candidates)!=1:
                    with self.lock:
                        self.connected=False
                        self.status='Waiting for GameSir native HID mode (Home + A)…' if not candidates else 'Multiple native GameSirs found; connect one controller.'
                    self.stop.wait(1);continue
                info=candidates[0]
                handle=hid.openfile(info['path'],0x80000000,3,None,3,0x40000000,None)
                if handle==hid.INVALID:raise C.WinError(C.get_last_error())
                event=hid.event(None,True,False,None)
                if not event:raise C.WinError(C.get_last_error())
                ov=hid.OVERLAPPED(hEvent=event);buf=C.create_string_buffer(37)
                with self.lock:
                    self.device={'product':info.get('product'),'vid':'3537','pid':'10b8'}
                    self.old_axes=None;self.arrivals.clear();self.changes.clear()
                while not self.stop.is_set():
                    hid.reset(event);count=hid.W.DWORD()
                    if not hid.read(handle,buf,37,C.byref(count),C.byref(ov)):
                        error=C.get_last_error()
                        if error!=997:raise C.WinError(error)
                        result=hid.wait(event,100)
                        if result==258:
                            hid.cancel(handle,C.byref(ov));hid.getover(handle,C.byref(ov),C.byref(count),True)
                            continue
                        if result!=0:raise RuntimeError('Unexpected HID wait '+str(result))
                        if not hid.getover(handle,C.byref(ov),C.byref(count),False):raise C.WinError(C.get_last_error())
                    if count.value:self.ingest(buf.raw[:count.value],time.perf_counter())
            except Exception as error:
                with self.lock:self.connected=False;self.status='Waiting to reconnect: '+str(error)
                self.stop.wait(1)
            finally:
                if handle not in (None,hid.INVALID):
                    hid.cancel(handle,None)
                    if ov is not None:
                        count=hid.W.DWORD();hid.getover(handle,C.byref(ov),C.byref(count),True)
                    hid.close(handle)
                if event:hid.close(event)

class Window:
    def __init__(self,reader):
        self.reader=reader;self.root=tk.Tk();self.root.title('GameSir Native HID Test')
        self.root.geometry('960x800');self.root.minsize(800,780);self.root.configure(bg=BG)
        self.root.option_add('*Font','{Segoe UI} 10')
        self.root.protocol('WM_DELETE_WINDOW',self.close)
        outer=tk.Frame(self.root,bg=BG);outer.pack(fill='both',expand=True,padx=24,pady=18)
        tk.Label(outer,text='GameSir G7 Pro 8K · Native HID',font=('Segoe UI',19,'bold'),bg=BG,fg=FG).pack(anchor='w')
        self.status=tk.Label(outer,text='Connecting…',bg=BG,fg=ACTIVE);self.status.pack(anchor='w',pady=(4,12))
        metrics=tk.Frame(outer,bg=PANEL);metrics.pack(fill='x',pady=(0,10))
        self.metric_values={}
        for i,(name,label) in enumerate([('fresh','Fresh stick states / sec'),('reports','HID reports / sec'),('interval','Median fresh interval')]):
            cell=tk.Frame(metrics,bg=PANEL);cell.grid(row=0,column=i,sticky='ew',padx=18,pady=10);metrics.columnconfigure(i,weight=1)
            tk.Label(cell,text=label,bg=PANEL,fg=MUTED).pack(anchor='w')
            value=tk.Label(cell,text='—',font=('Consolas',23,'bold'),bg=PANEL,fg=FG);value.pack(anchor='w');self.metric_values[name]=value
        sticks=tk.Frame(outer,bg=BG);sticks.pack(fill='both',expand=True)
        self.plots=[]
        for i,label in enumerate(['LEFT STICK','RIGHT STICK']):
            frame=tk.Frame(sticks,bg=BG);frame.grid(row=0,column=i,sticky='nsew',padx=8);sticks.columnconfigure(i,weight=1);sticks.rowconfigure(0,weight=1)
            tk.Label(frame,text=label,bg=BG,fg=MUTED).pack()
            canvas=tk.Canvas(frame,bg=BG,highlightthickness=0,height=195);canvas.pack(fill='both',expand=True)
            values=tk.Label(frame,text='X  0     Y  0',font=('Consolas',12),bg=BG,fg=FG);values.pack()
            self.plots.append((canvas,values))
        style=ttk.Style(self.root);style.theme_use('clam')
        style.configure('Native.Horizontal.TProgressbar',troughcolor=PANEL,background=ACTIVE,bordercolor=PANEL,lightcolor=ACTIVE,darkcolor=ACTIVE)
        self.triggers=[]
        trigger_row=tk.Frame(outer,bg=BG);trigger_row.pack(fill='x',pady=(8,12))
        for i,label in enumerate(['LEFT TRIGGER','RIGHT TRIGGER']):
            frame=tk.Frame(trigger_row,bg=BG);frame.grid(row=0,column=i,sticky='ew',padx=8);trigger_row.columnconfigure(i,weight=1)
            text=tk.Label(frame,text=label+'  0 / 255',bg=BG,fg=MUTED);text.pack(anchor='w')
            bar=ttk.Progressbar(frame,maximum=255,style='Native.Horizontal.TProgressbar');bar.pack(fill='x',pady=4);self.triggers.append((text,bar,label))
        tk.Label(outer,text='INDEPENDENT EXTRA BUTTONS',bg=BG,fg=MUTED).pack(anchor='w')
        extra_row=tk.Frame(outer,bg=BG);extra_row.pack(fill='x',pady=(6,12));self.button_views={}
        for i,(name,label) in enumerate([('L4','L4 · left rear'),('R4','R4 · right rear'),('L5','L5 · left extra shoulder'),('R5','R5 · right extra shoulder')]):
            frame=tk.Frame(extra_row,bg=PANEL,padx=9,pady=10);frame.grid(row=0,column=i,sticky='ew',padx=(0,6));extra_row.columnconfigure(i,weight=1)
            text=tk.Label(frame,text=label,bg=PANEL,fg=FG);text.pack()
            count=tk.Label(frame,text='0 presses',bg=PANEL,fg=MUTED);count.pack();self.button_views[name]=(frame,text,count)
        standard=tk.Frame(outer,bg=BG);standard.pack(fill='x',pady=(0,10))
        names=['A','B','X','Y','LB','RB','L3','R3','View','Menu','Home','Capture','Up','Right','Down','Left']
        for i,name in enumerate(names):
            label=tk.Label(standard,text=name,bg=PANEL,fg=FG,padx=6,pady=7)
            label.grid(row=i//8,column=i%8,sticky='ew',padx=3,pady=3);standard.columnconfigure(i%8,weight=1)
            self.button_views[name]=(label,label,None)
        tk.Label(outer,text='Move the sticks for the fresh-state rate. Button tiles light up on press; extra-button counts remain visible.',bg=BG,fg=MUTED,wraplength=900,justify='left').pack(anchor='w')
        self.footer=tk.Label(outer,text='Read-only · direct native HID · no firmware or settings writes',bg=BG,fg=MUTED);self.footer.pack(anchor='w',pady=(5,0))
        self.last_saved=0;self.reader.thread.start();self.root.after(30,self.refresh)
        self.root.after(200,self.root.lift)

    def refresh(self):
        state=self.reader.snapshot();now=time.perf_counter()
        self.status.configure(text=state['status'],fg=ACTIVE if state['connected'] else MUTED)
        self.metric_values['reports'].configure(text=f"{state['report_hz']:,.0f}" if state['report_hz'] is not None else '—')
        self.metric_values['fresh'].configure(text=f"{state['fresh_hz']:,.0f}" if state['fresh_hz'] is not None else 'Idle')
        self.metric_values['interval'].configure(text=f"{state['median_fresh_ms']:.2f} ms" if state['median_fresh_ms'] is not None else '—')
        for i,(canvas,values) in enumerate(self.plots):
            x,y=state['axes'][2*i:2*i+2];width=canvas.winfo_width();height=canvas.winfo_height()
            cx,cy=width/2,height/2;r=max(20,min(width,height)/2-13)
            canvas.delete('all');canvas.create_oval(cx-r,cy-r,cx+r,cy+r,outline=GRID,width=2)
            canvas.create_line(cx-r,cy,cx+r,cy,fill=GRID);canvas.create_line(cx,cy-r,cx,cy+r,fill=GRID)
            # Native GameSir Y is positive upward; canvas coordinates grow down.
            px=cx+r*x/(32767 if x>=0 else 32768);py=cy-r*y/(32767 if y>=0 else 32768)
            canvas.create_line(cx,cy,px,py,fill=ACTIVE,width=2);canvas.create_oval(px-6,py-6,px+6,py+6,fill=ACTIVE,outline='')
            values.configure(text=f'X {x:+6d}     Y {y:+6d}')
        for i,(text,bar,label) in enumerate(self.triggers):
            value=state['triggers'][i];text.configure(text=f'{label}  {value} / 255');bar['value']=value
        for name,(frame,text,count) in self.button_views.items():
            active=name in state['buttons'] or now-state['last_press'].get(name,0)<0.18
            color=ACTIVE if active else PANEL;fg=ACTIVE_FG if active else FG
            frame.configure(bg=color);text.configure(bg=color,fg=fg)
            if count is not None:count.configure(text=f"{state['presses'][name]} presses",bg=color,fg=ACTIVE_FG if active else MUTED)
        self.footer.configure(text=f"Read-only · 3537:10B8 native reports · {state['total_reports']:,} received · no firmware or settings writes")
        if now-self.last_saved>=1:
            snapshot={k:v for k,v in state.items() if k!='last_press'}
            temp=HERE/'live-status.tmp';temp.write_text(json.dumps(snapshot,indent=2),encoding='utf-8');temp.replace(HERE/'live-status.json');self.last_saved=now
        self.root.after(33,self.refresh)

    def close(self):
        self.reader.stop.set();self.reader.thread.join(timeout=1.5);self.root.destroy()

if __name__=='__main__':
    try:Window(Reader()).root.mainloop()
    except Exception:
        (HERE/'tester-errors.log').write_text(traceback.format_exc(),encoding='utf-8')
        raise
