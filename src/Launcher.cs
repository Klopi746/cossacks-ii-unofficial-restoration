using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace CossacksRestoration {
    public sealed class QuietState { public bool existed; public int attributes; public bool active; public string hash; }
    public static class Launcher {
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern uint GetShortPathName(string longPath,StringBuilder shortPath,uint length);
        public static string LegacyPath(string path) {
            if(Encoding.Default.GetString(Encoding.Default.GetBytes(path))==path) return path;
            StringBuilder shortPath=new StringBuilder(32768);
            if(GetShortPathName(path,shortPath,(uint)shortPath.Capacity)>0 && Encoding.Default.GetString(Encoding.Default.GetBytes(shortPath.ToString()))==shortPath.ToString()) return shortPath.ToString();
            // Installation and rollback support Unicode. DxWnd itself uses legacy ANSI APIs.
            return path;
        }
        public static string Profile(string template,string game,int width,int height) {
            template=template.Replace("\r\n","\n");
            string legacy=LegacyPath(game);
            Display primary=DisplayLayout.Primary();
            string[] keys={"title0","path0","startfolder0","notes0","monitorid0","posx0","posy0","sizx0","sizy0","initresw0","initresh0"};
            string[] values={"Cossacks II - Unofficial Restoration",Path.Combine(legacy,"engine.exe"),legacy,"Unofficial GOG BfE profile; borderless / stretched menu; no desktop mode switch.",primary.index.ToString(),primary.x.ToString(),primary.y.ToString(),width.ToString(),height.ToString(),width.ToString(),height.ToString()};
            for(int i=0;i<keys.Length;i++) {
                string pattern="(?m)^"+keys[i]+"=.*$";
                if(Regex.Matches(template,pattern).Count!=1) throw new InvalidDataException("Invalid DxWnd template: "+keys[i]);
                string replacement=keys[i]+"="+values[i];
                template=Regex.Replace(template,pattern,m=>replacement);
            }
            if(!Regex.IsMatch(template,@"(?m)^flagg0=1208221704\r?$") || !Regex.IsMatch(template,@"(?m)^flagx0=0\r?$"))
                throw new InvalidDataException("Unsafe display flags in profile.");
            return template.Replace("\n","\r\n");
        }
        public static string Play(string game,bool diagnostic,bool dryRun) {
            game=Store.Full(game); Store.SafePath(game); Store.EnsureClosed(game);
            InstallState state=Store.State(game);
            if(state.status!="installed") throw new IOException("Restoration is not installed. Run Setup first.");
            if(Store.HashOrNull(Store.Under(game,"engine.exe"))!=EnginePatch.PatchedHash) throw new IOException("Patched engine.exe differs from the verified file.");
            foreach(string name in new[]{"X3D8.DLL","d3d8.dll","d3d9.dll",Store.Folder+"\\dxwnd\\dxwnd.exe",Store.Folder+"\\dxwnd\\dxwnd.dll",Store.Folder+"\\dxwnd\\Disasm.dll",Store.Folder+"\\dxwnd\\MinHook.x86.dll",Store.Folder+"\\dxwnd\\launch.ini","dxvk.conf"}) {
                OwnedFile f=state.files.FirstOrDefault(item=>string.Equals(item.name,name,StringComparison.OrdinalIgnoreCase));
                if(f==null || Store.HashOrNull(Store.Under(game,name))!=f.after) throw new IOException("Installed component changed: "+name+". Reinstall or inspect the journal.");
            }
            if(Process.GetProcessesByName("dxwnd").Length!=0) throw new IOException("Close the existing DxWnd session before playing.");
            if(dryRun) return "DRY RUN: launch plan verified; no process started, no log attributes changed, no display modes changed.\n"+(diagnostic?"Diagnostic: writable game log, FPS HUD.":"Normal: temporary quiet-log, restored after exit.");
            if(Environment.GetEnvironmentVariable("C2_RESTORATION_FORBID_GAME_LAUNCH")=="1") throw new IOException("Game launch is disabled in this test environment.");
            string legacy=LegacyPath(game);
            if(Encoding.Default.GetString(Encoding.Default.GetBytes(legacy))!=legacy) throw new IOException("DxWnd cannot represent this path in the Windows system code page and no short path is available. Use an ASCII-only game folder. Installation / rollback work with Unicode paths.");
            string quietPath=Store.Under(game,Store.Folder+"\\quiet-log.json"),log=Store.Under(game,"log.txt");
            RecoverQuiet(game);
            if(diagnostic && File.Exists(log) && (File.GetAttributes(log)&FileAttributes.ReadOnly)!=0) throw new IOException("log.txt was already read-only before this launch. Make it writable to run diagnostics.");
            string session=Store.Under(game,Store.Folder+"\\sessions\\"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
            Directory.CreateDirectory(session);
            string dxwnd=Store.Under(game,Store.Folder+"\\dxwnd");
            // Re-evaluate primary monitor at launch. This edits only our profile, never the desktop mode.
            string profile=Profile(Encoding.ASCII.GetString(Store.Payload()["profile-template.ini"]),game,state.width,state.height);
            byte[] profileBytes=Encoding.Default.GetBytes(profile);
            Store.WriteBytes(Path.Combine(dxwnd,"launch.ini"),profileBytes);
            state.files.First(f=>f.name==Store.Folder+"\\dxwnd\\launch.ini").after=Hashing.Bytes(profileBytes);
            Store.Save(Store.StatePath(game),state);
            QuietState quiet=null;
            try {
                if(!diagnostic) {
                    quiet=new QuietState {existed=File.Exists(log),attributes=File.Exists(log)?(int)File.GetAttributes(log):0,active=true,hash=Store.HashOrNull(log)};
                    Store.Save(quietPath,quiet);
                    if(!quiet.existed) File.WriteAllBytes(log,new byte[0]);
                    File.Copy(log,Path.Combine(session,"game-log-before.txt"));
                    File.SetAttributes(log,File.GetAttributes(log)|FileAttributes.ReadOnly);
                }
                ProcessStartInfo info=new ProcessStartInfo {FileName=Path.Combine(dxwnd,"dxwnd.exe"),WorkingDirectory=dxwnd,Arguments="/C:launch.ini /R:1",UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden};
                info.EnvironmentVariables["DXVK_CONFIG_FILE"]=Store.Under(game,"dxvk.conf"); info.EnvironmentVariables["DXVK_LOG_PATH"]=session;
                if(diagnostic) info.EnvironmentVariables["DXVK_HUD"]="fps,version"; else info.EnvironmentVariables.Remove("DXVK_HUD");
                Store.Save(Path.Combine(session,"launch.json"),new {time=DateTime.UtcNow.ToString("o"),diagnostic=diagnostic,game=game,width=state.width,height=state.height,actualGameResult="Requires manual verification"});
                using(Process host=Process.Start(info)) {
                    bool observed=false; DateTime deadline=DateTime.UtcNow.AddSeconds(90);
                    while(true) {
                        bool running=false;
                        foreach(Process p in Process.GetProcessesByName("engine")) using(p) {
                            try { if(string.Equals(p.MainModule.FileName,Path.Combine(game,"engine.exe"),StringComparison.OrdinalIgnoreCase)) running=true; } catch(System.ComponentModel.Win32Exception) { }
                        }
                        if(running) observed=true;
                        if(observed && !running) break;
                        if(!observed && (host.HasExited || DateTime.UtcNow>deadline)) throw new IOException("Game start was not observed. See session logs: "+session);
                        Thread.Sleep(500);
                    }
                    // Only close the DxWnd process created by this invocation after the game exits.
                    if(!host.HasExited) { host.CloseMainWindow(); if(!host.WaitForExit(3000)) host.Kill(); }
                }
                return "Session finished. Diagnostics: "+session;
            } finally { if(quiet!=null) RecoverQuiet(game); }
        }
        public static void RecoverQuiet(string game) {
            string path=Store.Under(game,Store.Folder+"\\quiet-log.json");
            if(!File.Exists(path)) return;
            QuietState state=Store.Read<QuietState>(path); if(!state.active) return;
            Store.EnsureClosed(game);
            string log=Store.Under(game,"log.txt");
            if(File.Exists(log)) {
                FileAttributes attributes=File.GetAttributes(log)&~FileAttributes.ReadOnly;
                if((state.attributes&(int)FileAttributes.ReadOnly)!=0) attributes|=FileAttributes.ReadOnly;
                File.SetAttributes(log,attributes);
                // A newly created non-empty log is user evidence and is always retained.
                if(!state.existed && new FileInfo(log).Length==0) File.Delete(log);
            }
            state.active=false; Store.Save(path,state);
        }
    }
}
