using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using Microsoft.Win32;

namespace CossacksRestoration {
    public static class Program {
        public const string Version="0.1.1-preview";
        [STAThread] public static int Main(string[] args) {
            string report=null;
            try {
                string[] switches={"--silent","--install","--uninstall","--validate","--play","--diagnostic","--dry-run","--no-shortcut","--manage"};
                string[] values={"--game","--resolution","--report","--render-wizard"};
                var options=new System.Collections.Generic.Dictionary<string,string>();
                for(int i=0;i<args.Length;i++) {
                    if(options.ContainsKey(args[i])) throw new ArgumentException("Duplicate option: "+args[i]);
                    if(switches.Contains(args[i])) options.Add(args[i],"true");
                    else if(values.Contains(args[i])&&i+1<args.Length) { string key=args[i]; options.Add(key,args[++i]); }
                    else throw new ArgumentException("Unknown / incomplete option: "+args[i]);
                }
                string home=Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string game=options.ContainsKey("--game")?options["--game"]:Path.GetFileName(home)==Store.Folder?Directory.GetParent(home).FullName:null;
                if(options.ContainsKey("--report")) {
                    string candidate=Path.GetFullPath(options["--report"]); Store.SafePath(candidate);
                    if(File.Exists(candidate)||Directory.Exists(candidate)) throw new IOException("Report destination already exists. Choose a new file; existing files are never overwritten.");
                    if(game!=null && candidate.StartsWith(Store.Full(game).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Save the report outside the game folder and its recovery history.");
                    report=candidate;
                }
                int actions=new[]{"--install","--uninstall","--validate","--play","--diagnostic"}.Count(options.ContainsKey);
                if(actions>1) throw new ArgumentException("Select exactly one action.");
                if(options.ContainsKey("--dry-run") && !options.ContainsKey("--play") && !options.ContainsKey("--diagnostic"))
                    throw new ArgumentException("--dry-run is supported only with --play or --diagnostic. Use --validate to check installation compatibility without changes.");
                if(options.ContainsKey("--silent") && !options.ContainsKey("--dry-run") && (options.ContainsKey("--play")||options.ContainsKey("--diagnostic")))
                    throw new ArgumentException("Silent mode never starts the game. Use an explicit interactive play command.");
                if(actions==0) {
                    if(options.ContainsKey("--silent")) throw new ArgumentException("Silent mode requires --install, --validate or --uninstall.");
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    using(Wizard wizard=new Wizard(game)) {
                        if(options.ContainsKey("--render-wizard")) { wizard.Render(options["--render-wizard"]); return 0; }
                        Application.Run(wizard); return 0;
                    }
                }
                if(game==null) throw new ArgumentException("Specify --game <folder>.");
                string result;
                // Serialize cooperating restoration operations in this Windows session.
                using(Mutex mutex=new Mutex(false,"Local\\CossacksIIUnofficialRestoration")) {
                    bool held=false;
                    try { try { held=mutex.WaitOne(0); } catch(AbandonedMutexException) { held=true; }
                        if(!held) throw new IOException("Another restoration operation is running.");
                        if(options.ContainsKey("--install")) {
                            Display primary=DisplayLayout.Primary();
                            string resolution=options.ContainsKey("--resolution")?options["--resolution"]:primary.width+"x"+primary.height;
                            string[] dimensions=resolution.Split('x');
                            if(dimensions.Length!=2) throw new ArgumentException("Expected --resolution WIDTHxHEIGHT.");
                            result=Installer.Install(game,int.Parse(dimensions[0]),int.Parse(dimensions[1]),!options.ContainsKey("--no-shortcut"));
                        } else if(options.ContainsKey("--uninstall")) result=Installer.Uninstall(game);
                        else if(options.ContainsKey("--validate")) result=Installer.Validate(game);
                        else result=Launcher.Play(game,options.ContainsKey("--diagnostic"),options.ContainsKey("--dry-run"));
                    } finally { if(held) mutex.ReleaseMutex(); }
                }
                if(report!=null) Store.SaveReport(report,new {success=true,message=result,gameLaunched=options.ContainsKey("--dry-run")?false:options.ContainsKey("--play")||options.ContainsKey("--diagnostic")});
                if(!options.ContainsKey("--silent") && !options.ContainsKey("--play")) MessageBox.Show(result,"Cossacks II — Unofficial Restoration",MessageBoxButtons.OK,MessageBoxIcon.Information);
                return 0;
            } catch(Exception error) {
                if(report!=null) { try { Store.SaveReport(report,new {success=false,message=error.Message,gameLaunched=false}); } catch { } }
                if(!args.Contains("--silent")) MessageBox.Show(error.Message,"Cossacks II — Unofficial Restoration",MessageBoxButtons.OK,MessageBoxIcon.Error);
                return 1;
            }
        }
    }
    public sealed class Wizard:Form {
        bool ru=System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="ru";
        bool rendering=false;
        protected override bool ShowWithoutActivation { get { return rendering; } }
        int step=0; bool finished=false;
        Label heading=new Label(),subtitle=new Label(),status=new Label();
        Panel body=new Panel(); Button back=new Button(),next=new Button(),cancel=new Button();
        ComboBox language=new ComboBox(),resolution=new ComboBox(); TextBox folder=new TextBox();
        CheckBox shortcut=new CheckBox(),consent=new CheckBox();
        string initial;
        string T(string english,string russian) { return ru?russian:english; }
        public Wizard(string game) {
            initial=game; Text="Cossacks II — Unofficial Restoration"; ClientSize=new Size(740,540);
            MinimumSize=MaximumSize=Size; FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false;
            StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",10); BackColor=Color.White;
            heading.SetBounds(30,24,680,38); heading.Font=new Font("Segoe UI",19,FontStyle.Bold); Controls.Add(heading);
            subtitle.SetBounds(32,70,670,42); Controls.Add(subtitle);
            body.SetBounds(32,125,670,315); Controls.Add(body);
            status.SetBounds(32,445,670,30); status.AutoEllipsis=true; Controls.Add(status);
            back.SetBounds(382,490,100,32); next.SetBounds(492,490,100,32); cancel.SetBounds(602,490,110,32);
            Controls.AddRange(new Control[]{back,next,cancel}); AcceptButton=next; CancelButton=cancel;
            back.Click+=(s,e)=>{step--;Draw();}; cancel.Click+=(s,e)=>Close(); next.Click+=(s,e)=>Advance();
            folder.Text=game??DetectGame(); folder.SetBounds(0,65,560,30);
            resolution.DropDownStyle=ComboBoxStyle.DropDownList; resolution.SetBounds(0,180,260,30);
            Display primary=DisplayLayout.Primary(); string native=primary.width+"x"+primary.height;
            foreach(string item in new[]{native,"1920x1080","2560x1440","1280x720"}.Distinct()) resolution.Items.Add(item);
            resolution.SelectedIndex=0; shortcut.Checked=true; consent.Checked=false;
            language.DropDownStyle=ComboBoxStyle.DropDownList; language.Items.AddRange(new[]{"English","Русский"}); language.SelectedIndex=ru?1:0;
            language.SelectedIndexChanged+=(s,e)=>{ru=language.SelectedIndex==1;Draw();}; Draw();
        }
        public static string DetectGame() {
            foreach(RegistryKey root in new[]{Registry.CurrentUser,Registry.LocalMachine})
                foreach(string keyPath in new[]{@"SOFTWARE\GOG.com\Games",@"SOFTWARE\WOW6432Node\GOG.com\Games"}) {
                    using(RegistryKey games=root.OpenSubKey(keyPath)) if(games!=null) foreach(string name in games.GetSubKeyNames())
                        using(RegistryKey game=games.OpenSubKey(name)) {
                            string title=Convert.ToString(game.GetValue("gameName")),path=Convert.ToString(game.GetValue("path"));
                            if(title.IndexOf("Cossacks",StringComparison.OrdinalIgnoreCase)>=0 && title.IndexOf("Europe",StringComparison.OrdinalIgnoreCase)>=0 && Directory.Exists(path)) return path;
                        }
                }
            return "";
        }
        Label LabelAt(string text,int y,int height) { Label label=new Label {Text=text,AutoSize=false}; label.SetBounds(0,y,660,height); body.Controls.Add(label); return label; }
        void Draw() {
            body.Controls.Clear(); status.Text=""; heading.Text="Cossacks II";
            subtitle.Text="Unofficial Restoration · "+Program.Version+" · "+T("Unofficial community patch","Неофициальный патч сообщества");
            back.Text=T("Back","Назад"); next.Text=step==2?T("Install","Установить"):T("Next","Далее"); cancel.Text=finished?T("Close","Закрыть"):T("Cancel","Отмена");
            back.Enabled=step>0&&!finished; next.Visible=!finished;
            if(step==0) {
                LabelAt(T("Restore the verified GOG Battle for Europe installation.","Восстановление проверенной GOG-версии Battle for Europe."),0,35).Font=new Font(Font,FontStyle.Bold);
                LabelAt(T("Requires your own original game. No game EXE or game data are included.\n\nIncludes the formation capacity fix, DXVK, DxWnd, borderless mode, stretched menu, a 60 FPS limit and file backups.\n\nThe game will not start automatically. Desktop display mode stays under Windows control.","Требуется ваша оригинальная игра. Пакет не содержит игровой EXE или игровые данные.\n\nВ составе: фикс таблиц формирований, DXVK, DxWnd, Borderless, растянутое меню, лимит 60 FPS и резервирование.\n\nИгра не запускается автоматически. Режим рабочего стола не переключается."),40,190);
                LinkLabel gog=new LinkLabel {Text=T("Get the original game on GOG","Оригинальная игра на GOG"),AutoSize=true,Top=235};
                gog.LinkClicked+=(s,e)=>System.Diagnostics.Process.Start("https://www.gog.com/en/game/cossacks_ii_anthology"); body.Controls.Add(gog);
                language.SetBounds(0,275,180,30); body.Controls.Add(language);
            } else if(step==1) {
                LabelAt(T("Select the folder containing engine.exe","Выберите папку с engine.exe"),0,35).Font=new Font(Font,FontStyle.Bold);
                LabelAt(T("GOG is detected through its registry entries when available.","При наличии записи GOG папка определяется автоматически."),35,28);
                body.Controls.Add(folder); Button browse=new Button {Text="…"}; browse.SetBounds(570,65,70,30); body.Controls.Add(browse);
                browse.Click+=(s,e)=>{using(FolderBrowserDialog dialog=new FolderBrowserDialog {SelectedPath=folder.Text,Description=T("Choose Battle for Europe","Выберите Battle for Europe")}) if(dialog.ShowDialog()==DialogResult.OK) folder.Text=dialog.SelectedPath;};
                LabelAt(T("Compatibility is determined by the SHA-256 of the original engine.exe.\nOnly the verified GOG file is accepted. Napoleonic Wars, Steam and other files are rejected before changes.\n\nUse a writable folder. Administrator rights are not requested. Saves and player profiles are preserved.\n\nAll launch components and DxWnd corresponding sources are included. Installation works offline.","Совместимость определяется SHA-256 оригинального engine.exe.\nПринимается только проверенный файл GOG. Napoleonic Wars, Steam и другие файлы отклоняются до изменений.\n\nНужна доступная для записи папка. Права администратора не запрашиваются. Сохранения и профили игроков сохраняются.\n\nКомпоненты запуска и соответствующие исходники DxWnd уже в пакете. Установка работает без интернета."),115,195);
            } else if(step==2) {
                LabelAt(T("Ready to install","Готово к установке"),0,30).Font=new Font(Font,FontStyle.Bold);
                LabelAt(T("• Formation tables 64 → 128, with bounds checks\n• DXVK 3.1.1 (x86), 60 FPS, memory compatibility settings\n• DxWnd 2.06.16, primary monitor, borderless / stretched menu\n• Normal quiet-log and diagnostic writable-log launch\n• Verified backups, manifest and conflict-preserving uninstall", "• Таблицы формирований 64 → 128 с проверками границ\n• DXVK 3.1.1 (x86), 60 FPS, настройка доступной памяти\n• DxWnd 2.06.16, основной монитор, Borderless и растянутое меню\n• Обычный quiet-log и диагностический запуск с записью журнала\n• Резервирование, манифест и удаление с сохранением конфликтов"),35,125);
                LabelAt(T("Rendering resolution (primary monitor)","Разрешение рендеринга (основной монитор)"),155,25); body.Controls.Add(resolution);
                shortcut.Text=T("Create desktop shortcut (existing links are preserved)","Создать ярлык на рабочем столе (существующие сохраняются)"); shortcut.SetBounds(0,220,660,28); body.Controls.Add(shortcut);
                consent.Text=T("I own the original game and understand this is a preview.","У меня есть оригинальная игра; это предварительный выпуск."); consent.SetBounds(0,260,660,35); body.Controls.Add(consent);
                Button uninstall=new Button {Text=T("Uninstall / recover…","Удалить / восстановить…")}; uninstall.SetBounds(390,180,250,32); body.Controls.Add(uninstall);
                uninstall.Click+=(s,e)=>Remove();
            } else {
                finished=true; next.Visible=false; back.Enabled=false; cancel.Text=T("Close","Закрыть");
                LabelAt(T("Installation completed. The game was not launched.","Установка завершена. Игра не запускалась."),0,55).Font=new Font(Font,FontStyle.Bold);
                LabelAt(T("Use the desktop shortcut to play.\nFor diagnostics, open .c2-restoration\\Restoration.exe with --diagnostic.\nFor uninstall, run this wizard again and choose Uninstall / recover.\n\nBackups and journals are stored inside the game folder.\n\nThis preview has passed offline file-operation checks. Game checks on this installer remain manual.","Для игры используйте ярлык на рабочем столе.\nДиагностика: .c2-restoration\\Restoration.exe --diagnostic.\nДля удаления откройте мастер снова и нажмите «Удалить / восстановить».\n\nРезервные копии и журналы находятся в папке игры.\n\nВыпуск прошёл офлайн-проверки файловых операций. Игровые проверки этого установщика выполняются вручную."),65,230);
            }
        }
        void Advance() {
            try {
                if(step==1) { if(File.Exists(Store.StatePath(Store.Full(folder.Text)))) Store.State(Store.Full(folder.Text)); else Installer.Validate(folder.Text); }
                if(step==2) {
                    if(!consent.Checked) throw new InvalidOperationException(T("Confirm that you own the original game.","Подтвердите наличие оригинальной игры."));
                    string[] size=resolution.Text.Split('x'); next.Enabled=back.Enabled=cancel.Enabled=false; status.Text=T("Installing…","Установка…"); Refresh();
                    using(Mutex mutex=new Mutex(false,"Local\\CossacksIIUnofficialRestoration")) {
                        bool held=false; try { try { held=mutex.WaitOne(0); } catch(AbandonedMutexException) { held=true; }
                            if(!held) throw new IOException(T("Another restoration operation is running.","Другая операция уже выполняется."));
                            Installer.Install(folder.Text,int.Parse(size[0]),int.Parse(size[1]),shortcut.Checked);
                        } finally {if(held) mutex.ReleaseMutex();}
                    }
                }
                step++; Draw();
            } catch(Exception error) { status.Text=T("Stopped; see details.","Операция остановлена; см. подробности."); MessageBox.Show(this,error.Message,T("Restoration","Восстановление"),MessageBoxButtons.OK,MessageBoxIcon.Warning); }
            finally { next.Enabled=cancel.Enabled=true; back.Enabled=step>0&&!finished; }
        }
        void Remove() {
            if(MessageBox.Show(this,T("Restore original files? Changed files will be preserved in conflicts; saves remain untouched.","Вернуть исходные файлы? Изменённые файлы сохранятся в conflicts, сохранения игры останутся на месте."),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
            try {
                string result;
                using(Mutex mutex=new Mutex(false,"Local\\CossacksIIUnofficialRestoration")) {
                    bool held=false; try { try { held=mutex.WaitOne(0); } catch(AbandonedMutexException) { held=true; }
                        if(!held) throw new IOException(T("Another restoration operation is running.","Другая операция уже выполняется."));
                        result=Installer.Uninstall(folder.Text);
                    } finally { if(held) mutex.ReleaseMutex(); }
                }
                MessageBox.Show(this,result,Text); Close();
            }
            catch(Exception error) { MessageBox.Show(this,error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        }
        public void Render(string directory) {
            Directory.CreateDirectory(directory); rendering=true; ShowInTaskbar=false;
            StartPosition=FormStartPosition.Manual; Location=new Point(-20000,-20000); Show();
            foreach(bool russian in new[]{false,true}) { ru=russian; for(step=0;step<=2;step++) { Draw(); Application.DoEvents(); using(Bitmap bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height)); bitmap.Save(Path.Combine(directory,(ru?"ru":"en")+"-"+step+".png")); } } }
            Close();
        }
    }
}
