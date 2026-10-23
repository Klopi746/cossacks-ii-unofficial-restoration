using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace CossacksRestoration {
    public sealed class CopyBlock {
        public int source; public int copy; public int target; public string xor; public string tail;
    }
    public sealed class BytePatch { public int offset; public string data; }
    public sealed class PatchDescriptor {
        public int format; public string originalHash; public string patchedHash;
        public int originalLength; public int patchedLength; public int sectionRaw; public int codeLength;
        public CopyBlock[] blocks; public BytePatch[] prefixPatches;
        public string afterCode; public int relocSource; public int relocLength; public int relocTarget; public string relocTail;
    }
    public static class Hashing {
        public static string Bytes(byte[] bytes) {
            using (SHA256 hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        }
        public static string FileHash(string path) {
            using (FileStream file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using (SHA256 hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "");
        }
    }
    public static class EnginePatch {
        public const string OriginalHash="DB52B8A6926C63EF2B7BBF921C01B8F95A4ECD9F2B63BDA18F0EDD18E45F7517";
        public const string PatchedHash="EC5B4E1EB1410E66223AD86C5F69B97B2646552A5B2AEDE2B4072111D85C9605";
        static void Copy(byte[] source,int sourceOffset,byte[] destination,int offset,int count) {
            if(sourceOffset<0 || offset<0 || count<0 || (long)sourceOffset+count>source.Length || (long)offset+count>destination.Length)
                throw new InvalidDataException("Invalid patch descriptor range.");
            Buffer.BlockCopy(source,sourceOffset,destination,offset,count);
        }
        static byte[] Decode(string text) { return string.IsNullOrEmpty(text)?new byte[0]:Convert.FromBase64String(text); }
        public static byte[] Apply(byte[] original,string json) {
            if(Hashing.Bytes(original)!=OriginalHash) throw new InvalidDataException("Unsupported engine.exe. The original compatible GOG file is required.");
            PatchDescriptor plan=new JavaScriptSerializer().Deserialize<PatchDescriptor>(json);
            if(plan.format!=1 || plan.originalHash!=OriginalHash || plan.patchedHash!=PatchedHash || plan.originalLength!=original.Length || plan.patchedLength!=24567808)
                throw new InvalidDataException("Unsupported patch descriptor.");
            byte[] output=new byte[plan.patchedLength];
            Copy(original,0,output,0,original.Length);
            foreach(CopyBlock block in plan.blocks) {
                int at=checked(plan.sectionRaw+block.target);
                if(block.copy>0) Copy(original,block.source,output,at,block.copy);
                byte[] mask=Decode(block.xor);
                if(mask.Length!=0 && mask.Length!=block.copy) throw new InvalidDataException("Invalid transform mask.");
                for(int i=0;i<mask.Length;i++) output[at+i]^=mask[i];
                byte[] tail=Decode(block.tail);Copy(tail,0,output,checked(at+block.copy),tail.Length);
            }
            byte[] after=Decode(plan.afterCode);Copy(after,0,output,checked(plan.sectionRaw+plan.codeLength),after.Length);
            Copy(original,plan.relocSource,output,plan.relocTarget,plan.relocLength);
            byte[] reloc=Decode(plan.relocTail);Copy(reloc,0,output,checked(plan.relocTarget+plan.relocLength),reloc.Length);
            foreach(BytePatch patch in plan.prefixPatches) {
                byte[] bytes=Decode(patch.data);Copy(bytes,0,output,patch.offset,bytes.Length);
            }
            if(Hashing.Bytes(output)!=PatchedHash) throw new InvalidDataException("Patched engine verification failed. No game file was changed.");
            return output;
        }
    }
}
