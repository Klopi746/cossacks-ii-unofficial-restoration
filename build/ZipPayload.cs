// Minimal deterministic ZIP STORE writer. No runtime-dependent deflate stream.
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

static class ZipPayload {
    sealed class Entry { public byte[] name; public uint crc,size,offset; }
    static readonly uint[] Table=CreateTable();
    const ushort Date=((2026-1980)<<9)|(10<<5)|23;
    static uint[] CreateTable() {
        uint[] table=new uint[256];
        for(uint i=0;i<256;i++) { uint n=i; for(int j=0;j<8;j++) n=(n&1)!=0?0xedb88320U^(n>>1):n>>1; table[i]=n; }
        return table;
    }
    static uint Crc(byte[] data) { uint crc=0xffffffff; foreach(byte b in data) crc=Table[(crc^b)&255]^(crc>>8); return crc^0xffffffff; }
    static int Main(string[] args) {
        using(BinaryWriter writer=new BinaryWriter(File.Create(args[0]))) {
            List<Entry> entries=new List<Entry>();
            for(int i=1;i<args.Length;i++) {
                int separator=args[i].IndexOf('=');
                if(separator<1) throw new ArgumentException("Expected entry=path.");
                string name=args[i].Substring(0,separator);
                foreach(char ch in name) if(ch>127 || ch=='\\') throw new ArgumentException("ZIP entry names must be ASCII with forward slashes.");
                byte[] bytes=File.ReadAllBytes(args[i].Substring(separator+1));
                Entry entry=new Entry {name=Encoding.ASCII.GetBytes(name),crc=Crc(bytes),size=(uint)bytes.Length,offset=checked((uint)writer.BaseStream.Position)};
                writer.Write(0x04034b50U); writer.Write((ushort)20); writer.Write((ushort)0); writer.Write((ushort)0); writer.Write((ushort)0); writer.Write(Date);
                writer.Write(entry.crc); writer.Write(entry.size); writer.Write(entry.size); writer.Write((ushort)entry.name.Length); writer.Write((ushort)0); writer.Write(entry.name); writer.Write(bytes); entries.Add(entry);
            }
            uint central=checked((uint)writer.BaseStream.Position);
            foreach(Entry entry in entries) {
                writer.Write(0x02014b50U); writer.Write((ushort)20); writer.Write((ushort)20); writer.Write((ushort)0); writer.Write((ushort)0); writer.Write((ushort)0); writer.Write(Date);
                writer.Write(entry.crc); writer.Write(entry.size); writer.Write(entry.size); writer.Write((ushort)entry.name.Length); writer.Write((ushort)0); writer.Write((ushort)0); writer.Write((ushort)0); writer.Write((ushort)0); writer.Write(0U); writer.Write(entry.offset); writer.Write(entry.name);
            }
            uint size=checked((uint)writer.BaseStream.Position-central);
            writer.Write(0x06054b50U); writer.Write((ushort)0); writer.Write((ushort)0); writer.Write((ushort)entries.Count); writer.Write((ushort)entries.Count); writer.Write(size); writer.Write(central); writer.Write((ushort)0);
        }
        return 0;
    }
}
