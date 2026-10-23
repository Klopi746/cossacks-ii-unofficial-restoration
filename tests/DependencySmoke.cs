using System;
using System.Linq;
using CossacksRestoration;
public static class DependencySmoke {
    public static int Main() {
        try {
            Environment.SetEnvironmentVariable("C2_RESTORATION_FORBID_GAME_LAUNCH","1");
            var files=Store.Payload(); Dependencies.Verify(files);
            foreach(var item in Dependencies.DxWndHashes) Console.WriteLine(item.Key+" "+Hashing.Bytes(files["dxwnd\\"+item.Key]));
            Console.WriteLine("PASS: embedded DxWnd / source availability and four binary hashes. No network, game or DxWnd process created.");
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }
}
