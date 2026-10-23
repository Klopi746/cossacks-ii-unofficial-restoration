using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CossacksRestoration {
    public sealed class OwnedFile {
        public string name; public string before; public string after; public int attributes;
        public string backup; public bool written;
    }
    public sealed class InstallState {
        public int format=1; public string version=Program.Version; public string game;
        public string status; public string created; public int width; public int height;
        public List<OwnedFile> files=new List<OwnedFile>();
        public string shortcut; public string shortcutHash;
        public List<string> conflicts=new List<string>();
    }
    public static class Store {
        public const string Folder=".c2-restoration";
        public static string Full(string path) { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
        public static void SafePath(string path) {
            string current=Path.GetFullPath(path);
            while(!string.IsNullOrEmpty(current)) {
                if((File.Exists(current)||Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("Links / junctions are not supported: "+current);
                DirectoryInfo parent=Directory.GetParent(current); current=parent==null?null:parent.FullName;
            }
        }
        public static string Under(string root,string relative) {
            string full=Path.GetFullPath(Path.Combine(root,relative));
            if(!full.StartsWith(Full(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new IOException("Path is outside the installation.");
            SafePath(full); return full;
        }
        public static string HashOrNull(string path) { return File.Exists(path)?Hashing.FileHash(path):null; }
        public static void WriteBytes(string path,byte[] bytes) {
            SafePath(path); Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                File.WriteAllBytes(temp,bytes);
                if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
            } finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        public static void Save(string path,object value) {
            WriteBytes(path,new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(value)));
        }
        public static T Read<T>(string path) { return new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(path)); }
        public static string StatePath(string game) { return Under(game,Folder+"\\state.json"); }
        public static InstallState State(string game) {
            string p=StatePath(game); if(!File.Exists(p)) throw new IOException("Restoration is not installed in this folder.");
            InstallState state=Read<InstallState>(p);
            if(state.format!=1 || !string.Equals(Full(state.game),Full(game),StringComparison.OrdinalIgnoreCase))
                throw new IOException("The installation manifest belongs to another folder.");
            return state;
        }
        public static void EnsureClosed(string game) {
            string target=Path.Combine(game,"engine.exe");
            foreach(Process process in Process.GetProcessesByName("engine")) {
                using(process) {
                    try { if(string.Equals(process.MainModule.FileName,target,StringComparison.OrdinalIgnoreCase)) throw new IOException("Close this game before continuing."); }
                    catch(System.ComponentModel.Win32Exception) { throw new IOException("Cannot inspect a running engine.exe. Close it before continuing."); }
                }
            }
        }
        public static byte[] Resource(string name) {
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) {
                if(stream==null) throw new IOException("Missing embedded resource: "+name);
                using(MemoryStream result=new MemoryStream()) { stream.CopyTo(result); return result.ToArray(); }
            }
        }
        public static Dictionary<string,byte[]> Payload() {
            Dictionary<string,byte[]> files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
            using(MemoryStream stream=new MemoryStream(Resource("payload.zip")))
            using(ZipArchive zip=new ZipArchive(stream,ZipArchiveMode.Read)) {
                foreach(ZipArchiveEntry entry in zip.Entries) {
                    if(entry.FullName.EndsWith("/")) continue;
                    using(Stream input=entry.Open()) using(MemoryStream output=new MemoryStream()) {
                        input.CopyTo(output); files.Add(entry.FullName.Replace('/', '\\'),output.ToArray());
                    }
                }
            }
            return files;
        }
    }
    public static class Installer {
        public static string Validate(string game) {
            game=Store.Full(game); Store.SafePath(game); Store.EnsureClosed(game);
            if(!Directory.Exists(game)) throw new IOException("Game folder does not exist.");
            string engine=Store.Under(game,"engine.exe");
            string hash=Store.HashOrNull(engine);
            if(hash!=EnginePatch.OriginalHash) {
                if(File.Exists(Store.StatePath(game)) && Store.State(game).status=="installed" && hash==EnginePatch.PatchedHash) return "installed";
                throw new InvalidDataException("Unsupported engine.exe SHA-256: "+(hash??"missing")+"\nOnly the verified original GOG Battle for Europe file is supported. Steam / Napoleonic Wars / other patches are not supported. No files changed.");
            }
            foreach(string name in new[]{"settings.xml","settings.cfg","X3D8.DLL"})
                if(!File.Exists(Store.Under(game,name))) throw new IOException("Required original file missing: "+name);
            if(Hashing.FileHash(Store.Under(game,"X3D8.DLL"))!="5D5B49A0366D5C98CE05F854B402169D8FC7DDF00A67393F810BB21436BEE7F5")
                throw new InvalidDataException("Unsupported original X3D8.DLL. Restore the verified original GOG helper before installation. No files changed.");
            ResolutionSettings(File.ReadAllBytes(Store.Under(game,"settings.xml")),"settings.xml",1280,720);
            ResolutionSettings(File.ReadAllBytes(Store.Under(game,"settings.cfg")),"settings.cfg",1280,720);
            return "compatible";
        }
        public static byte[] ResolutionSettings(byte[] bytes,string name,int width,int height) {
            if(bytes.Any(b=>b>127)) throw new InvalidDataException("Non-ASCII settings require manual review: "+name);
            string text=Encoding.ASCII.GetString(bytes);
            string[] names=name=="settings.xml"?new[]{"ScreenSizeX","ScreenSizeY"}:new[]{"ScreenWidth","ScreenHeigth"};
            int[] numbers={width,height};
            for(int i=0;i<names.Length;i++) {
                string pattern=name=="settings.xml"?"(<"+names[i]+">)\\d+(</"+names[i]+">)":"(?m)^("+names[i]+"\\s*=\\s*)\\d+";
                if(Regex.Matches(text,pattern).Count!=1) throw new InvalidDataException("Expected exactly one "+names[i]+" in "+name);
                text=Regex.Replace(text,pattern,"${1}"+numbers[i]+(name=="settings.xml"?"${2}":""));
            }
            return Encoding.ASCII.GetBytes(text);
        }
        public static void Dimensions(int width,int height) {
            if(width<1024 || height<720 || width>7680 || height>4320) throw new ArgumentException("Resolution must be between 1024x720 and 7680x4320.");
        }
        public static string Install(string game,int width,int height,bool shortcut) {
            game=Store.Full(game); Dimensions(width,height); string compatibility=Validate(game);
            if(compatibility=="installed") {
                InstallState existing=Store.State(game);
                foreach(OwnedFile f in existing.files) if(Store.HashOrNull(Store.Under(game,f.name))!=f.after)
                    throw new IOException("An installed file was changed: "+f.name+". Uninstall first; changed files will be preserved in conflicts.");
                if(existing.width!=width||existing.height!=height) throw new IOException("Already installed at another resolution. Uninstall and reinstall to change it.");
                return "Already installed and verified; no files changed. The game was not launched.";
            }
            string statePath=Store.StatePath(game);
            string previousHistory=null;
            if(Directory.Exists(Store.Under(game,Store.Folder))) {
                InstallState previous=Store.State(game);
                if(previous.status!="removed") throw new IOException("An incomplete restoration history exists. Use Uninstall / recover first. No files changed.");
                previousHistory=Store.Under(game,Store.Folder+"-history-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
            }
            Dictionary<string,byte[]> payload=Store.Payload();
            Dependencies.Verify(payload);
            Dictionary<string,byte[]> plan=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
            plan.Add("engine.exe",EnginePatch.Apply(File.ReadAllBytes(Store.Under(game,"engine.exe")),Encoding.UTF8.GetString(Store.Resource("patch-plan.json"))));
            plan.Add("X3D8.DLL",payload["dxvk\\d3d8.dll"]);
            plan.Add("d3d8.dll",payload["dxvk\\d3d8.dll"]); plan.Add("d3d9.dll",payload["dxvk\\d3d9.dll"]);
            plan.Add("engine.exe.local",new byte[0]);
            plan.Add("Restoration-Diagnostics.cmd",Encoding.ASCII.GetBytes("@echo off\r\nstart \"\" \"%~dp0.c2-restoration\\Restoration.exe\" --diagnostic\r\n"));
            plan.Add("Restoration-Uninstall.cmd",Encoding.ASCII.GetBytes("@echo off\r\nstart \"\" \"%~dp0.c2-restoration\\Restoration.exe\" --manage\r\n"));
            plan.Add("dxvk.conf",Encoding.ASCII.GetBytes("# Cossacks II Unofficial Restoration; DXVK 3.1.1\r\nd3d9.maxFrameRate = 60\r\nd3d9.maxAvailableMemory = 1024\r\nd3d9.presentInterval = 0\r\n"));
            foreach(string name in new[]{"settings.xml","settings.cfg"}) plan.Add(name,ResolutionSettings(File.ReadAllBytes(Store.Under(game,name)),name,width,height));
            plan.Add(Store.Folder+"\\Restoration.exe",File.ReadAllBytes(Assembly.GetExecutingAssembly().Location));
            foreach(KeyValuePair<string,byte[]> item in payload) {
                if(item.Key.StartsWith("dxvk\\",StringComparison.OrdinalIgnoreCase)) continue;
                plan.Add(Store.Folder+"\\"+item.Key,item.Value);
            }
            string profile=Launcher.Profile(Encoding.ASCII.GetString(payload["profile-template.ini"]),game,width,height);
            plan.Add(Store.Folder+"\\dxwnd\\launch.ini",Encoding.Default.GetBytes(profile));
            InstallState state=new InstallState {game=game,status="preparing",created=DateTime.UtcNow.ToString("o"),width=width,height=height};
            foreach(KeyValuePair<string,byte[]> item in plan) {
                string path=Store.Under(game,item.Key);
                if(Directory.Exists(path)) throw new IOException("A directory blocks an output file: "+item.Key);
                if(File.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReadOnly)!=0) throw new IOException("Read-only output file: "+item.Key);
                state.files.Add(new OwnedFile {name=item.Key,before=Store.HashOrNull(path),after=Hashing.Bytes(item.Value),attributes=File.Exists(path)?(int)File.GetAttributes(path):0,backup=Store.Folder+"\\backups\\"+item.Key});
            }
            if(previousHistory!=null) Directory.Move(Store.Under(game,Store.Folder),previousHistory);
            Directory.CreateDirectory(Store.Under(game,Store.Folder)); Store.Save(statePath,state);
            try {
                // Back up every original and persist the plan before replacing the first file.
                foreach(OwnedFile f in state.files) if(f.before!=null) {
                    string backup=Store.Under(game,f.backup); Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(Store.Under(game,f.name),backup,false);
                    if(Hashing.FileHash(backup)!=f.before) throw new IOException("Backup verification failed: "+f.name);
                }
                state.status="installing"; Store.Save(statePath,state);
                foreach(OwnedFile f in state.files) {
                    if(Store.HashOrNull(Store.Under(game,f.name))!=f.before) throw new IOException("File changed during installation: "+f.name);
                    // Mark intent first; recovery also handles interruption between replacement and journal update.
                    f.written=true; Store.Save(statePath,state);
                    Store.WriteBytes(Store.Under(game,f.name),plan[f.name]);
                    if(Hashing.FileHash(Store.Under(game,f.name))!=f.after) throw new IOException("Write verification failed: "+f.name);
                }
                if(shortcut) CreateShortcut(game,state);
                state.status="installed"; Store.Save(statePath,state);
                return "Installed successfully. The game was not launched.\nBackup and journal: "+Path.GetDirectoryName(statePath);
            } catch {
                state.status="interrupted"; Store.Save(statePath,state);
                try { Restore(game,state,false); } catch { /* keep journal for explicit recovery */ }
                throw;
            }
        }
        static void CreateShortcut(string game,InstallState state) {
            string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string testDesktop=Environment.GetEnvironmentVariable("C2_RESTORATION_TEST_DESKTOP");
            if(Environment.GetEnvironmentVariable("C2_RESTORATION_FORBID_GAME_LAUNCH")=="1" && !string.IsNullOrEmpty(testDesktop)) desktop=Store.Full(testDesktop);
            Store.SafePath(desktop); Directory.CreateDirectory(desktop);
            string stem="Cossacks II — Unofficial Restoration";
            string path=Path.Combine(desktop,stem+".lnk"); int suffix=2;
            while(File.Exists(path)||Directory.Exists(path)) { path=Path.Combine(desktop,stem+" ("+suffix+").lnk"); suffix++; }
            Store.SafePath(path);
            dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic link=shell.CreateShortcut(path);
            link.TargetPath=Store.Under(game,Store.Folder+"\\Restoration.exe"); link.Arguments="--play";
            link.WorkingDirectory=game; link.IconLocation=Path.Combine(game,"engine.exe")+",0";
            link.Description="Unofficial restoration; original GOG Battle for Europe required"; link.Save();
            state.shortcut=path; state.shortcutHash=Hashing.FileHash(path);
        }
        public static string Uninstall(string game) {
            game=Store.Full(game); Store.SafePath(game); Store.EnsureClosed(game);
            InstallState state=Store.State(game);
            if(state.status=="removed") return "Already removed. Backups and journals are retained.";
            return Restore(game,state,true);
        }
        static string Restore(string game,InstallState state,bool removeShortcut) {
            // Verify all originals before changing any current files.
            foreach(OwnedFile f in state.files) if(f.written && f.before!=null && Store.HashOrNull(Store.Under(game,f.backup))!=f.before)
                throw new IOException("Original backup missing or changed: "+f.name+". Restoration stopped before changes.");
            state.status="restoring"; Store.Save(Store.StatePath(game),state);
            foreach(OwnedFile f in state.files.AsEnumerable().Reverse()) {
                if(!f.written) continue;
                string path=Store.Under(game,f.name),current=Store.HashOrNull(path);
                if(current==f.before) { f.written=false; Store.Save(Store.StatePath(game),state); continue; }
                if(current!=null && current!=f.after && current!=f.before) {
                    string conflict=Store.Under(game,Store.Folder+"\\conflicts\\"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N")+"\\"+f.name);
                    Directory.CreateDirectory(Path.GetDirectoryName(conflict)); File.Copy(path,conflict,false);
                    if(Hashing.FileHash(conflict)!=current) throw new IOException("Cannot preserve changed file: "+f.name);
                    state.conflicts.Add(conflict); Store.Save(Store.StatePath(game),state);
                }
                // The running maintenance EXE is retained with backups; Windows locks it until exit.
                if(string.Equals(path,Assembly.GetExecutingAssembly().Location,StringComparison.OrdinalIgnoreCase)) continue;
                if(current!=null) File.SetAttributes(path,File.GetAttributes(path)&~FileAttributes.ReadOnly);
                if(f.before!=null) {
                    Store.WriteBytes(path,File.ReadAllBytes(Store.Under(game,f.backup))); File.SetAttributes(path,(FileAttributes)f.attributes);
                } else if(File.Exists(path)) File.Delete(path);
                f.written=false; Store.Save(Store.StatePath(game),state);
            }
            if(removeShortcut && state.shortcut!=null && Store.HashOrNull(state.shortcut)==state.shortcutHash) { Store.SafePath(state.shortcut); File.Delete(state.shortcut); }
            state.status="removed"; Store.Save(Store.StatePath(game),state);
            return "Original files restored. User data preserved. Backups / journals retained in .c2-restoration.\nPreserved changed files: "+state.conflicts.Count;
        }
    }
}
