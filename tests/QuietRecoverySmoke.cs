using System;
using System.IO;
using CossacksRestoration;

public static class QuietRecoverySmoke {
    static int checks;
    static void Check(bool pass,string message) { if(!pass) throw new Exception(message); checks++; }
    static string Case(string root,string name,bool existed,int originalAttributes,string text) {
        string game=Path.Combine(root,name); Directory.CreateDirectory(Path.Combine(game,Store.Folder));
        if(text!=null) File.WriteAllText(Path.Combine(game,"log.txt"),text);
        Store.Save(Path.Combine(game,Store.Folder,"quiet-log.json"),new QuietState {existed=existed,attributes=originalAttributes,active=true});
        return game;
    }
    static void Protect(string game,FileAttributes additional) { string p=Path.Combine(game,"log.txt"); File.SetAttributes(p,File.GetAttributes(p)|FileAttributes.ReadOnly|additional); }
    [STAThread] public static int Main(string[] args) {
        try {
            Environment.SetEnvironmentVariable("C2_RESTORATION_FORBID_GAME_LAUNCH","1");
            string root=Path.GetFullPath(args[0]); if(Directory.Exists(root)) throw new IOException("Use a new isolated quiet-log test directory.");
            Directory.CreateDirectory(root);
            string game=Case(root,"previously-writable",true,(int)FileAttributes.Archive,"Original log bytes");
            string log=Path.Combine(game,"log.txt"),hash=Hashing.FileHash(log); Protect(game,0);
            Launcher.RecoverQuiet(game);
            Check((File.GetAttributes(log)&FileAttributes.ReadOnly)==0,"Writable bit was not restored.");
            Check(Hashing.FileHash(log)==hash,"Log bytes changed during recovery.");
            string journal=Path.Combine(game,Store.Folder,"quiet-log.json");
            Check(!Store.Read<QuietState>(journal).active,"Quiet journal remains active.");
            hash=Hashing.FileHash(journal); Launcher.RecoverQuiet(game);
            Check(Hashing.FileHash(journal)==hash,"Repeated recovery is not idempotent.");
            game=Case(root,"originally-readonly",true,(int)(FileAttributes.Archive|FileAttributes.ReadOnly),"Keep original read-only bit"); Protect(game,0); Launcher.RecoverQuiet(game);
            Check((File.GetAttributes(Path.Combine(game,"log.txt"))&FileAttributes.ReadOnly)!=0,"Original read-only bit lost.");
            game=Case(root,"new-empty-log",false,0,""); Protect(game,0); Launcher.RecoverQuiet(game);
            Check(!File.Exists(Path.Combine(game,"log.txt")),"Own new empty log was not removed.");
            game=Case(root,"new-nonempty-log",false,0,"New evidence must survive"); Protect(game,0); Launcher.RecoverQuiet(game);
            Check(File.ReadAllText(Path.Combine(game,"log.txt"))=="New evidence must survive","New evidence was destroyed.");
            game=Case(root,"unrelated-attributes",true,(int)FileAttributes.Archive,"Keep unrelated attributes"); Protect(game,FileAttributes.Hidden); Launcher.RecoverQuiet(game);
            Check((File.GetAttributes(Path.Combine(game,"log.txt"))&FileAttributes.Hidden)!=0,"Unrelated attribute lost.");
            game=Case(root,"missing-log",true,(int)FileAttributes.Archive,null); Launcher.RecoverQuiet(game);
            Check(!File.Exists(Path.Combine(game,"log.txt")) && !Store.Read<QuietState>(Path.Combine(game,Store.Folder,"quiet-log.json")).active,"Missing-log recovery failed.");
            Console.WriteLine("PASS: "+checks+" quiet-log recovery assertions; existing bytes / original read-only bit / unrelated attributes / evidence preserved; no game process.");
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
