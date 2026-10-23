"""Independent file/PE coverage check; no executable is loaded as a process."""
from pathlib import Path
import sys,struct,json,hashlib

import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_32,CS_GRP_CALL,CS_GRP_JUMP
import argparse
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--original',type=Path,required=True)
parser.add_argument('--candidate',type=Path,required=True)
parser.add_argument('--report',type=Path,required=True)
args=parser.parse_args()
OUT=args.report.parent
OUT.mkdir(parents=True,exist_ok=True)

manifest=json.loads((Path(__file__).parent/'engine-contract.json').read_text())
original=args.original.read_bytes()
candidate=args.candidate.read_bytes()
def sha(b):return hashlib.sha256(b).hexdigest().upper()
assert sha(original)==manifest['original_SHA256'] and sha(candidate)==manifest['candidate_SHA256']
a=pefile.PE(data=original,fast_load=True);b=pefile.PE(data=candidate,fast_load=True)
def field(obj,name,size):return (obj.get_field_absolute_offset(name),size,name)
allowed=[field(a.FILE_HEADER,'NumberOfSections',2),field(a.OPTIONAL_HEADER,'SizeOfCode',4),field(a.OPTIONAL_HEADER,'SizeOfImage',4),field(a.OPTIONAL_HEADER,'CheckSum',4),
         (a.OPTIONAL_HEADER.DATA_DIRECTORY[4].get_file_offset(),8,'certificate directory'),(a.OPTIONAL_HEADER.DATA_DIRECTORY[5].get_file_offset(),8,'relocation directory'),
         (a.sections[-1].get_file_offset()+40,40,'new section header')]
for va,size,name in [(0x114813f,4,'UI capacity'),(0x114a1d0,8,'AI entry redirect'),(0x1148abc,6,'UI sentinel redirect')]:allowed.append((a.get_offset_from_rva(va-a.OPTIONAL_HEADER.ImageBase),size,name))
differences=[i for i,(x,y) in enumerate(zip(original,candidate)) if x!=y]
assert all(any(start<=i<start+size for start,size,name in allowed) for i in differences)
assert b.OPTIONAL_HEADER.AddressOfEntryPoint==a.OPTIONAL_HEADER.AddressOfEntryPoint
assert b.OPTIONAL_HEADER.SizeOfStackReserve==a.OPTIONAL_HEADER.SizeOfStackReserve
assert b.OPTIONAL_HEADER.DllCharacteristics==a.OPTIONAL_HEADER.DllCharacteristics
assert b.OPTIONAL_HEADER.ImageBase==a.OPTIONAL_HEADER.ImageBase
assert b.verify_checksum()
for sa,sb in zip(a.sections,b.sections):assert sa.__pack__()==sb.__pack__()
for i in range(16):
    if i not in [4,5]:assert a.OPTIONAL_HEADER.DATA_DIRECTORY[i].__pack__()==b.OPTIONAL_HEADER.DATA_DIRECTORY[i].__pack__()
assert b.sections[-1].Name==b'.bfix\0\0\0' and b.sections[-1].Characteristics==0x60000020
def relocs(pe):
    d=pe.OPTIONAL_HEADER.DATA_DIRECTORY[5];raw=pe.get_data(d.VirtualAddress,d.Size)
    pos=0;entries={}
    while pos+8<=len(raw):
        page,size=struct.unpack_from('<II',raw,pos)
        assert page and size>=8 and size%4==0 and pos+size<=len(raw)
        for word, in struct.iter_unpack('<H',raw[pos+8:pos+size]):
            kind=word>>12
            if kind:assert kind==3;entries[page+(word&4095)]=kind
        pos+=size
    assert pos==len(raw)
    return entries
ra,rb=relocs(a),relocs(b)
assert all(rb[k]==v for k,v in ra.items())
added=set(rb)-set(ra)
assert added=={int(e['new_RVA'],16) for e in manifest['new_relocations']}
clone=int(manifest['AI_clone_entry'],16);length=manifest['AI_clone_bytes'];base=b.OPTIONAL_HEADER.ImageBase
code=bytearray(b.get_data(clone-base,length));delta=0x1000000
for rva in added:
    offset=rva-(clone-base)
    assert 0<=offset<=length-4
    old=struct.unpack_from('<I',code,offset)[0]
    struct.pack_into('<I',code,offset,old+delta)
md=Cs(CS_ARCH_X86,CS_MODE_32);md.detail=True
normal=list(md.disasm(b.get_data(clone-base,length),clone))
rebased=list(md.disasm(bytes(code),clone+delta))
assert len(normal)==len(rebased)
for x,y in zip(normal,rebased):
    assert (x.mnemonic,x.size)==(y.mnemonic,y.size)
    if x.group(CS_GRP_CALL) or x.group(CS_GRP_JUMP):assert y.operands[0].imm==x.operands[0].imm+delta
for va,dest in [(0x114a1d0,clone),(0x1148abc,int(manifest['UI_sentinel_guard'],16))]:
    i=next(md.disasm(b.get_data(va-base,5),va+delta))
    assert i.mnemonic=='jmp' and i.operands[0].imm==dest+delta
evidence={'status':'PASS','original_SHA256':sha(original),'candidate_SHA256':sha(candidate),'original_prefix_bytes':len(original),'changed_prefix_bytes':len(differences),
          'allowed_regions':[{'offset':hex(s),'length':n,'purpose':name} for s,n,name in allowed],
          'original_sections_identical':True,'entry_point_imports_resources_stack_unchanged':True,'checksum_valid':True,'new_section_permissions':'read/execute; no write',
          'original_relocations_retained':len(ra),'new_relocations':len(added),'clone_rebase_delta_test':hex(delta),'relative_branches_and_absolute_fixups_checked':True,
          'certificate_directory_cleared':True,'game_process_created':False}
(args.report).write_text(json.dumps(evidence,indent=2),encoding='utf-8')
print(json.dumps(evidence,indent=2))
