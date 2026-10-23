using System;
using System.IO;
using System.Collections.Generic;

namespace CossacksRestoration {
    public static class Dependencies {
        public static readonly Dictionary<string,string> DxWndHashes=new Dictionary<string,string> {
            {"dxwnd.exe","D8D2F25BE8EC746B6DF31754A2E6C91B562B6C65AB1475D673FA95BF1B34CB82"},
            {"dxwnd.dll","73B3F9A9E8EF0C3BEB220753320C7F3E28D6E4BE8A8459D18DA7F2E3A9D09ABE"},
            {"Disasm.dll","F413A207787DA88281500AAEAE07FB98D7D438ECB4D67F8FCDF0AFF2F988717F"},
            {"MinHook.x86.dll","32667DBD34787133D0CDA2CBB1AEBC543DAEF4ABF435F346527ED6AA5D47B313"}
        };
        public static void Verify(Dictionary<string,byte[]> payload) {
            foreach(KeyValuePair<string,string> file in DxWndHashes) {
                byte[] bytes;
                if(!payload.TryGetValue("dxwnd\\"+file.Key,out bytes) || Hashing.Bytes(bytes)!=file.Value)
                    throw new InvalidDataException("Bundled DxWnd component verification failed: "+file.Key);
            }
            if(!payload.ContainsKey("sources\\DxWnd-2.06.16-corresponding-source.zip"))
                throw new InvalidDataException("Bundled corresponding source is missing.");
        }
    }
}
