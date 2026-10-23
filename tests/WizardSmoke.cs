// Exercises this project's own WinForms controls in an off-screen test form.
// No game or DxWnd process can be started by this test.
using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using CossacksRestoration;

public static class WizardSmoke {
    static object Field(object instance,string name) { return instance.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(instance); }
    static void Click(Wizard wizard) { ((Button)Field(wizard,"next")).PerformClick(); Application.DoEvents(); }
    [STAThread] public static int Main(string[] args) {
        try {
            Environment.SetEnvironmentVariable("C2_RESTORATION_FORBID_GAME_LAUNCH","1");
            using(Wizard wizard=new Wizard(args[0])) {
                wizard.GetType().GetField("rendering",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(wizard,true);
                wizard.ShowInTaskbar=false; wizard.StartPosition=FormStartPosition.Manual; wizard.Location=new Point(-20000,-20000);
                wizard.Show(); Application.DoEvents(); Click(wizard); Click(wizard);
                if((int)Field(wizard,"step")!=2) throw new Exception("Folder validation did not advance to component page.");
                ((CheckBox)Field(wizard,"consent")).Checked=true;
                ((CheckBox)Field(wizard,"shortcut")).Checked=false;
                Click(wizard);
                if((int)Field(wizard,"step")!=3 || !(bool)Field(wizard,"finished")) throw new Exception("Installation did not reach the completion page.");
                if(Hashing.FileHash(Path.Combine(args[0],"engine.exe"))!=EnginePatch.PatchedHash) throw new Exception("Wrong patched engine.");
                wizard.Close();
            }
            Console.WriteLine("PASS: real wizard Next / folder validation / consent / Install / completion events. Game launch forbidden.");
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
