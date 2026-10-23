"""Offline x86 emulation of selected game routines with synthetic fixtures.

No PE process is created. OS/game services are replaced by local Python hooks;
this is memory-contract evidence, not a gameplay test.
"""
from pathlib import Path
import sys, struct, json, hashlib, time, gc

import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE, UC_HOOK_MEM_READ
from unicorn.x86_const import *
import argparse
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--original',type=Path,required=True)
parser.add_argument('--candidate',type=Path,required=True)
parser.add_argument('--report',type=Path,required=True)
args=parser.parse_args()
OUT=args.report.parent
OUT.mkdir(parents=True,exist_ok=True)

MAN=json.loads((Path(__file__).parent/'engine-contract.json').read_text())
ORIGINAL=args.original
CANDIDATE=args.candidate
STACK,HEAP,FIXTURE,STOP=0x10000000,0x20000000,0x30000000,0x100ff000
REGS=[UC_X86_REG_EBX,UC_X86_REG_ESI,UC_X86_REG_EDI,UC_X86_REG_EBP]
SEEDS=[0x12345678,0x87654321,0x31415926,0x27182818]
def u32(b): return struct.unpack('<I',b)[0]
def p32(x): return struct.pack('<I',x&0xffffffff)
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()

class Rig:
    def __init__(self,path):
        pe=pefile.PE(str(path),fast_load=True)
        self.base=pe.OPTIONAL_HEADER.ImageBase
        self.uc=Uc(UC_ARCH_X86,UC_MODE_32)
        self.uc.mem_map(self.base,(pe.OPTIONAL_HEADER.SizeOfImage+4095)&~4095)
        self.uc.mem_write(self.base,pe.get_memory_mapped_image())
        for addr,size in [(STACK,0x100000),(HEAP,0x400000),(FIXTURE,0x100000)]: self.uc.mem_map(addr,size)
        self.uc.mem_write(STOP,b'\x90')
        self.next=HEAP+0x1000;self.allocs=[];self.freed=[];self.calls=[];self.hooks=[]
        self.uc.reg_write(UC_X86_REG_ESP,STACK+0xe0000)
        for r,v in zip(REGS,SEEDS): self.uc.reg_write(r,v)
        self.write32(0x2185100,0xaabbccdd)
        self.uc.hook_add(UC_HOOK_CODE,self.unexpected_begin,begin=STOP,end=STOP)
    def unexpected_begin(self,uc,a,size,data): raise AssertionError('STOP executed instead of ending emulation')
    def read32(self,a): return u32(self.uc.mem_read(a,4))
    def write32(self,a,v): self.uc.mem_write(a,p32(v))
    def write16(self,a,v): self.uc.mem_write(a,struct.pack('<H',v))
    def args(self,n): return [self.read32(self.uc.reg_read(UC_X86_REG_ESP)+4+4*i) for i in range(n)]
    def ret(self,value=None,pop=0):
        sp=self.uc.reg_read(UC_X86_REG_ESP)
        self.uc.reg_write(UC_X86_REG_EIP,self.read32(sp))
        self.uc.reg_write(UC_X86_REG_ESP,sp+4+pop)
        if value is not None: self.uc.reg_write(UC_X86_REG_EAX,value&0xffffffff)
    def hook(self,a,callback):
        def handler(uc,address,size,unused): callback()
        self.hooks.append(self.uc.hook_add(UC_HOOK_CODE,handler,begin=a,end=a))
    def malloc(self,size):
        at=(self.next+31)&~31;self.next=at+size+64
        assert self.next<HEAP+0x400000
        self.uc.mem_write(at-32,b'\x5a'*32)
        self.uc.mem_write(at,b'\xa5'*size)
        self.uc.mem_write(at+size,b'\x5a'*32)
        self.allocs.append((at,size));return at
    def check_canaries(self):
        for a,n in self.allocs:
            assert bytes(self.uc.mem_read(a-32,32))==b'\x5a'*32
            assert bytes(self.uc.mem_read(a+n,32))==b'\x5a'*32
    def run(self,start,end=STOP,count=30000000):
        self.uc.emu_start(start,end,count=count)
        assert self.uc.reg_read(UC_X86_REG_EIP)==end,hex(self.uc.reg_read(UC_X86_REG_EIP))
    def closed(self):
        del self.uc

def fixtures(r,ni,centers):
    nation=0x30d6105+ni*0x1e94b
    r.uc.mem_write(0x30f4862+ni*0x1e94b,b'\x08')
    r.write16(0x4b99710,8192)
    quota=FIXTURE+0x90000;r.write16(quota,10)
    # Officer/support GeneralObject layout used by the machine code.
    for i in range(1000):
        go=FIXTURE+0x10000+i*0x40
        record=FIXTURE+0x30000+i*0x40
        r.uc.mem_write(go+4,b'\x02')
        r.write32(go+0xb,record);r.write32(record+9,quota)
        r.write32(nation+i*4,go)
        r.write16(0x527d278+i*2,1)
    for c in range(centers):
        ob=FIXTURE+0x70000+c*0x200
        r.write32(0x337f7b0+(c+1)*4,ob)
        r.write16(ob+0x1f,1);r.uc.mem_write(ob+0x9b,bytes([ni]))
    return nation

def fill_tables(r,args,types,centers,target,amount):
    ni,mc,cid,mo,index,amounts,mt,uo,ud,uf=args
    mc&=255;mo&=255;mt&=255
    sizes=[2*mc,2*mo*mt,2*mc*mo*mt,mc*mo,mc*mo,mc*mo]
    addresses=[cid,index,amounts,uo,ud,uf]
    intervals=sorted(zip(addresses,sizes))
    assert all(a+n<=b for (a,n),(b,m) in zip(intervals,intervals[1:])),intervals
    for a,n in zip(addresses,sizes):
        if not any(lo<=a and a+n<=lo+length for lo,length in r.allocs):
            # Original caller used a real stack frame.
            assert STACK<=a and a+n<=STACK+0xe0000,(hex(a),n,r.allocs)
    for a,n,v in zip(addresses,sizes,[255,255,0,0,0,0]):r.uc.mem_write(a,bytes([v])*n)
    for c in range(centers): r.write16(cid+2*c,c+1)
    for row in range(min(types,mo)):
        for t,v in enumerate([row,300+row,500+row,700+row]): r.write16(index+2*(row*mt+t),v)
    if target:
        c,row=target
        if row<mo:
            r.write16(amounts+2*(mt*(mo*c+row)+3),amount)
            for a in [uo,ud,uf]:r.uc.mem_write(a+mo*c+row,b'\x07')
    r.table_args=[x&255 if i in [0,1,3,6] else x for i,x in enumerate(args)]
    r.table_intervals=intervals

def ai_case(path,types,centers=1,target=None,amount=20,resource=0,ni=0,enabled=True,oom=False):
    r=Rig(path);fixtures(r,ni,centers)
    if not enabled:r.uc.mem_write(0x30f4862+ni*0x1e94b,b'\0')
    initial_sp=r.uc.reg_read(UC_X86_REG_ESP)
    r.write32(initial_sp,STOP);r.write32(initial_sp+4,ni)
    def alloc():r.ret(0 if oom else r.malloc(r.args(1)[0]))
    r.hook(0xce2ba9,alloc)
    def free():
        address=r.args(1)[0];assert address in [a for a,n in r.allocs] and address not in r.freed
        r.freed.append(address);r.check_canaries();r.ret(0xdddddddd)
    r.hook(0xcfb078,free)
    def probe():
        n=r.uc.reg_read(UC_X86_REG_EAX);sp=r.uc.reg_read(UC_X86_REG_ESP);ret=r.read32(sp)
        r.uc.reg_write(UC_X86_REG_ESP,sp+4-n);r.uc.reg_write(UC_X86_REG_EIP,ret)
    r.hook(0xcf23a6,probe)
    flag_reads=[];queue_queries=[]
    def collector():
        fill_tables(r,r.args(10),types,centers,target,amount)
        for a in r.table_args[7:10]:
            n=r.table_args[1]*r.table_args[3]
            def flag_read(uc,access,address,size,value,unused):flag_reads.append((address,size))
            r.uc.hook_add(UC_HOOK_MEM_READ,flag_read,begin=a,end=a+n-1)
        r.ret(min(types,r.table_args[3]))
    r.hook(0xcc969f,collector)
    r.hook(0xcfb253,lambda:r.ret(resource))
    def queue():queue_queries.append(r.args(1)[0]&65535);r.ret(-1,pop=4)
    r.hook(0xcc93c0,queue)
    def create():r.calls.append(tuple(a&65535 for a in r.args(2)));r.ret(1)
    r.hook(0xcc787c,create)
    def enqueue():raise AssertionError('Unexpected queue mutation in support-ready fixture')
    r.hook(0xcc5919,enqueue)
    def cookie():assert r.uc.reg_read(UC_X86_REG_ECX)==0xaabbccdd;r.ret()
    r.hook(0xcda18e,cookie)
    # Empty padded rows must not read GO[0xffff].
    invalid=[]
    def bad_read(uc,access,address,size,value,unused):invalid.append(address)
    r.uc.hook_add(UC_HOOK_MEM_READ,bad_read,begin=0x30d6105+ni*0x1e94b+0xffff*4,end=0x30d6105+ni*0x1e94b+0xffff*4+3)
    r.run(0x114a1d0)
    capacity=getattr(r,'table_args',[None,None,None,None])[3]
    result=r.uc.reg_read(UC_X86_REG_EAX)&255
    assert r.uc.reg_read(UC_X86_REG_ESP)==initial_sp+4
    assert [r.uc.reg_read(reg) for reg in REGS]==SEEDS
    r.check_canaries()
    is_candidate=path==CANDIDATE
    if is_candidate:
        assert not invalid
        assert (len(r.allocs),len(r.freed))==((0,0) if oom else (1,1))
        if not oom: assert r.allocs[0][1]==MAN['AI_heap_frame_bytes']
    expect=bool(target and target[1]<min(types,capacity or 0) and amount>=10 and enabled and not oom)
    assert result==int(expect),(target,result,expect)
    assert r.calls==([(target[0]+1,700+target[1])] if expect else [])
    if resource>3000 and expect:
        expected=[a+target[0]*capacity+target[1] for a in r.table_args[7:10]]
        assert [a for a,n in flag_reads]==expected,(flag_reads,expected)
        assert len(queue_queries)==3
    evidence={'types':types,'capacity':capacity,'centers':centers,'target':target,'resource':resource,'NI':ni,'enabled':enabled,'allocation_failure':oom,
              'result':result,'formation_calls':r.calls,'support_flag_reads':len(flag_reads),'allocations':len(r.allocs),'frees':len(r.freed),
              'empty_row_GO_reads':len(invalid),'nonvolatile_registers_preserved':True,'stack_balanced':True,'canaries_intact':True}
    r.closed();return evidence

def ui_case(types,centers,target):
    r=Rig(CANDIDATE);nation=fixtures(r,0,centers)
    bp=STACK+0xd0000;r.uc.reg_write(UC_X86_REG_EBP,bp);r.uc.reg_write(UC_X86_REG_ESP,bp-0x300)
    r.hook(0xce2ba9,lambda:r.ret(r.malloc(r.args(1)[0])))
    r.hook(0xcc969f,lambda:(fill_tables(r,r.args(10),types,centers,target,20),r.ret()))
    r.run(0x114813b,0x1148328)
    assert [n for a,n in r.allocs]==[32,32768,524288,2048,2048,2048]
    first=[a for a,n in r.allocs]
    r.run(0x114813b,0x1148328)
    assert [a for a,n in r.allocs]==first
    r.write32(bp-0x60,nation);r.write32(bp-0x1a8,0);r.write32(bp-0x58,0);r.write16(bp-0x20,0xffff)
    invalid=[]
    def bad_read(uc,access,address,size,value,unused):invalid.append(address)
    r.uc.hook_add(UC_HOOK_MEM_READ,bad_read,begin=nation+0xffff*4,end=nation+0xffff*4+3)
    r.run(0x1148a26,0x1148c4d)
    selected=r.read32(bp-0x58)
    assert not invalid
    if target:
        assert selected==2
        assert (r.read32(bp-0x4c),r.read32(bp-0x5c),r.read32(bp-0x14))==(target[0],target[1],3)
    else:assert selected==0
    r.check_canaries()
    result={'types':types,'centers':centers,'target':target,'allocation_sizes':[n for a,n in r.allocs],'allocation_reuse':True,'selected_formations':selected,'empty_row_GO_reads':0,'canaries_intact':True}
    r.closed();return result

def catalogue_case(capacity,types):
    r=Rig(ORIGINAL)
    bp=STACK+0xd0000;r.uc.reg_write(UC_X86_REG_EBP,bp);r.uc.reg_write(UC_X86_REG_ESP,bp-0x200)
    table=r.malloc(capacity*128*2);r.uc.mem_write(table,b'\xff'*(capacity*128*2))
    r.write32(bp+8,0);r.write32(bp+0x14,capacity);r.write32(bp+0x18,table);r.write32(bp+0x20,128)
    r.write32(0x30d6100,types*2)
    for i in range(types*2):
        go=FIXTURE+0x10000+i*0x40;off=FIXTURE+0x30000+i*0x40;units=FIXTURE+0x80000
        r.write32(0x30d6105+i*4,go)
        r.write32(go+0xb,off if i%2 else 0)
        r.write16(off,300);r.write16(off+2,500);r.write16(off+0x11,1);r.write32(off+0x13,units);r.write16(units,700)
    warnings=[]
    def warning():
        assert r.args(2)[1]==0x1f38c0c
        warnings.append(r.args(2)[1]);r.ret()
    r.hook(0xcbd4b2,warning)
    r.run(0xfe379a,0xfe38d4)
    count=bytes(r.uc.mem_read(bp-0x15,1))[0]
    assert count==min(types,capacity)
    assert len(warnings)==max(0,types-capacity)
    for row in range(count):assert struct.unpack('<4H',r.uc.mem_read(table+row*128*2,8))==(row*2+1,300,500,700)
    if count<capacity:assert bytes(r.uc.mem_read(table+count*128*2,(capacity-count)*128*2))==b'\xff'*((capacity-count)*128*2)
    r.check_canaries();r.closed()
    return {'capacity':capacity,'officer_definitions':types,'rows_written':count,'warning_calls':len(warnings),'canaries_intact':True}

def main():
    start=time.time()
    assert sha(ORIGINAL)==MAN['original_SHA256'] and sha(CANDIDATE)==MAN['candidate_SHA256']
    results={'scope':'Selected actual x86 routines emulated with synthetic state and stubbed game services. No game process, OS API, display or save/load execution.',
             'original_SHA256':sha(ORIGINAL),'candidate_SHA256':sha(CANDIDATE),'catalogue':[],'ai_original_control':[],'ai_candidate':[],'ui_candidate':[]}
    for capacity in [64,128]:
        for types in [0,64,92,128,129]:
            results['catalogue'].append(catalogue_case(capacity,types));gc.collect()
    print('Catalogue bounds: 10 cases passed',flush=True)
    controls=[dict(types=64,target=(0,63)),dict(types=64,centers=2,target=(1,63),resource=4000),dict(types=64,target=(0,63),amount=5),dict(types=64,target=(0,63),enabled=False)]
    for case in controls:
        before=ai_case(ORIGINAL,**case);after=ai_case(CANDIDATE,**case)
        assert (before['result'],before['formation_calls'])==(after['result'],after['formation_calls'])
        results['ai_original_control'].append(before);results['ai_candidate'].append(after);gc.collect()
    print('AI original behavior controls: 4 comparisons passed',flush=True)
    cases=[dict(types=0),dict(types=92,target=(0,91)),dict(types=128,target=(0,127)),dict(types=92,centers=16,target=(15,91),resource=4000,ni=7),
           dict(types=128,centers=16,target=(15,127),resource=4000),dict(types=92,target=(0,91),oom=True),dict(types=92,centers=0)]
    for case in cases:results['ai_candidate'].append(ai_case(CANDIDATE,**case));gc.collect()
    print('AI expanded table, support strides, all exits and OOM: 7 further cases passed',flush=True)
    for types,centers,target in [(0,1,None),(92,1,(0,91)),(128,1,(0,127)),(92,16,(15,91)),(128,16,(15,127))]:
        results['ui_candidate'].append(ui_case(types,centers,target));gc.collect()
    print('UI allocations, sentinel guard, last center/type: 5 cases passed',flush=True)
    results['status']='PASS';results['duration_seconds']=round(time.time()-start,3)
    results['gameplay_status']='NOT TESTED: user will launch manually'
    (args.report).write_text(json.dumps(results,indent=2),encoding='utf-8')
    print(json.dumps({'status':results['status'],'duration_seconds':results['duration_seconds'],'report':str(args.report)},indent=2))

if __name__=='__main__':main()
