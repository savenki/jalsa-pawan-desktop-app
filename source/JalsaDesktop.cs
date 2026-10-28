using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;

namespace JalsaDesktop
{
    [Flags] public enum ReminderKind { None = 0, Blink = 1, Water = 2 }

    public sealed class Preferences
    {
        public int BlinkMinutes = 10, WaterMinutes = 30, SizePercent = 100;
        public int HourlyMinutes = 60, NapSeconds = 120, BambooRegrowSeconds = 18;
        public bool Voice = true, Reminders = true, Roam = true, Bamboo = true, Hourly = true;
        public bool FocusMode = true, MouseSword = true;
        public string VoiceName = "";
        public void Sanitize()
        {
            BlinkMinutes = Math.Max(1, Math.Min(240, BlinkMinutes));
            WaterMinutes = Math.Max(1, Math.Min(480, WaterMinutes));
            SizePercent = Math.Max(70, Math.Min(140, SizePercent));
            HourlyMinutes = Math.Max(5, Math.Min(240, HourlyMinutes));
            NapSeconds = Math.Max(10, Math.Min(600, NapSeconds));
            BambooRegrowSeconds = Math.Max(5, Math.Min(120, BambooRegrowSeconds));
        }
        public static Preferences Load(string path)
        {
            Preferences p = new Preferences();
            try
            {
                if (!File.Exists(path)) return p;
                XElement e = XElement.Load(path);
                p.BlinkMinutes = (int?)e.Element("BlinkMinutes") ?? 10;
                p.WaterMinutes = (int?)e.Element("WaterMinutes") ?? 30;
                p.SizePercent = (int?)e.Element("SizePercent") ?? 100;
                p.HourlyMinutes = (int?)e.Element("HourlyMinutes") ?? 60;
                p.NapSeconds = (int?)e.Element("NapSeconds") ?? 120;
                p.BambooRegrowSeconds = (int?)e.Element("BambooRegrowSeconds") ?? 18;
                p.Voice = (bool?)e.Element("Voice") ?? true;
                p.Reminders = (bool?)e.Element("Reminders") ?? true;
                p.Roam = (bool?)e.Element("Roam") ?? true;
                p.Bamboo = (bool?)e.Element("Bamboo") ?? true;
                p.Hourly = (bool?)e.Element("Hourly") ?? true;
                p.FocusMode = (bool?)e.Element("FocusMode") ?? true;
                p.MouseSword = (bool?)e.Element("MouseSword") ?? true;
                p.VoiceName = (string)e.Element("VoiceName") ?? "";
            }
            catch (Exception ex) { Program.Log("Settings: " + ex.Message); }
            p.Sanitize();
            return p;
        }
        public void Save(string path)
        {
            Sanitize();
            XElement e = new XElement("JalsaDesktop",
                new XElement("BlinkMinutes", BlinkMinutes), new XElement("WaterMinutes", WaterMinutes),
                new XElement("SizePercent", SizePercent), new XElement("Voice", Voice),
                new XElement("HourlyMinutes", HourlyMinutes), new XElement("NapSeconds", NapSeconds), new XElement("BambooRegrowSeconds", BambooRegrowSeconds),
                new XElement("Reminders", Reminders), new XElement("Roam", Roam),
                new XElement("Bamboo", Bamboo), new XElement("Hourly", Hourly), new XElement("FocusMode",FocusMode),new XElement("MouseSword",MouseSword), new XElement("VoiceName", VoiceName));
            e.Save(path + ".tmp");
            File.Copy(path + ".tmp", path, true);
            File.Delete(path + ".tmp");
        }
    }

    public enum HourPhase { Normal, Sleeping, Growing, Waiting, Attack }
    public sealed class HourlyRoutine
    {
        public HourPhase Phase = HourPhase.Normal;
        public DateTime NextNap, PhaseStart;
        public bool Preview;
        public int NapIntervalMinutes = 60, NapDurationSeconds = 120;
        public HourlyRoutine(DateTime now) { NextNap = now.AddHours(1); }
        public void Cancel(DateTime now) { Phase = HourPhase.Normal; NextNap = now.AddMinutes(NapIntervalMinutes); Preview = false; }
        public void StartPreview(DateTime now) { Phase = HourPhase.Sleeping; PhaseStart = now; Preview = true; }
        public void Poll(DateTime now)
        {
            if (Phase == HourPhase.Normal && now >= NextNap) { Phase = HourPhase.Sleeping; PhaseStart = now; }
            else if (Phase == HourPhase.Sleeping && now >= PhaseStart.AddSeconds(Preview ? 4 : NapDurationSeconds)) { Phase = HourPhase.Growing; PhaseStart = now; }
            else if (Phase == HourPhase.Growing && now >= PhaseStart.AddSeconds(Preview ? 4 : 8)) { Phase = HourPhase.Waiting; PhaseStart = now; }
            else if (Phase == HourPhase.Attack && now >= PhaseStart.AddSeconds(2)) Cancel(now);
        }
        public bool Hover(DateTime now)
        {
            if (Phase != HourPhase.Waiting) return false;
            Phase = HourPhase.Attack; PhaseStart = now; return true;
        }
        public double Growth(DateTime now) { return Phase == HourPhase.Growing ? Math.Min(1, Math.Max(0, (now - PhaseStart).TotalSeconds / (Preview ? 4 : 8))) : 1; }
    }

    public sealed class ReminderSchedule
    {
        public DateTime NextBlink, NextWater;
        private readonly Preferences prefs;
        public ReminderSchedule(Preferences p, DateTime now) { prefs = p; Reset(now); }
        public void Reset(DateTime now)
        {
            NextBlink = now.AddMinutes(prefs.BlinkMinutes);
            NextWater = now.AddMinutes(prefs.WaterMinutes);
        }
        public ReminderKind Poll(DateTime now)
        {
            if (!prefs.Reminders) { Reset(now); return ReminderKind.None; }
            ReminderKind due = ReminderKind.None;
            if (now >= NextBlink) { due |= ReminderKind.Blink; NextBlink = now.AddMinutes(prefs.BlinkMinutes); }
            if (now >= NextWater) { due |= ReminderKind.Water; NextWater = now.AddMinutes(prefs.WaterMinutes); }
            return due;
        }
        public void Snooze(ReminderKind kind, DateTime now)
        {
            if ((kind & ReminderKind.Water) != 0) NextWater = now.AddMinutes(5);
            if ((kind & ReminderKind.Blink) != 0) NextBlink = now.AddMinutes(5);
        }
    }

    internal static class Program
    {
        public static readonly string Folder = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string SettingsPath = Path.Combine(Folder, "settings.xml");
        public const string WindowTitle = "Jalsa Pawan Desktop Companion";
        [STAThread] public static void Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--self-test")
                {
                    SelfTest.Run(args.Length > 1 ? args[1] : Path.Combine(Folder, "self-test.json"));
                    return;
                }
                if(args.Length>0&&args[0]=="--shutdown-test")
                {
                    Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                    Application.Run(new Companion(-1));
                    File.WriteAllText(Path.Combine(Folder,"shutdown-test.json"),"{\"ok\":true,\"scenario\":\"Close with control panel, focus tree, reminder bubble and speech active\"}");return;
                }
                try { Native.SetProcessDpiAwareness(2); } catch { Native.SetProcessDPIAware(); }
                int requestedCommand = args.Length == 0 ? 0 : Command(args[0]);
                IntPtr existingWindow = Native.FindWindow(null, WindowTitle);
                if (existingWindow != IntPtr.Zero)
                {
                    Native.PostMessage(existingWindow, 0x8000 + (requestedCommand == 9 ? 5 : requestedCommand == 0 ? 1 : requestedCommand), IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                if (requestedCommand == 5 || requestedCommand == 7) return;
                bool created;
                using (Mutex mutex = new Mutex(true, "Global\\JalsaPawanDesktopCompanionV2", out created))
                {
                    int command = args.Length == 0 ? 0 : Command(args[0]);
                    if (!created)
                    {
                        IntPtr window = Native.FindWindow(null, WindowTitle);
                        if (window != IntPtr.Zero) Native.PostMessage(window, 0x8000 + (command == 9 ? 5 : command == 0 ? 1 : command), IntPtr.Zero, IntPtr.Zero);
                        return;
                    }
                    Application.EnableVisualStyles();
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Companion(command == 9 ? 0 : command));
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                if (args.Length > 0 && args[0] == "--self-test")
                    File.WriteAllText(args.Length > 1 ? args[1] : Path.Combine(Folder, "self-test.json"), "{\"ok\":false,\"error\":\"" + ex.Message.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ") + "\"}");
                else MessageBox.Show("Jalsa Pawan could not start.\n\n" + ex.Message, WindowTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
            }
        }
        private static int Command(string arg)
        {
            switch (arg) { case "--settings": return 1; case "--water": return 2; case "--blink": return 3; case "--bamboo": return 4; case "--quit": return 5; case "--pause": return 6; case "--status": return 7; case "--hourly-demo": return 8; case "--toggle": return 9; default: return 0; }
        }
        public static void Log(string text)
        {
            try { File.AppendAllText(Path.Combine(Folder, "diagnostics.log"), DateTime.Now.ToString("s") + " " + text + Environment.NewLine); } catch { }
        }
    }

    internal sealed class Art : IDisposable
    {
        private readonly Dictionary<string, Bitmap[]> clips = new Dictionary<string, Bitmap[]>();
        public Art()
        {
            using (Bitmap sheet = Load("spritesheet.png"))
            {
                if (sheet.Size != new Size(1536, 2288)) throw new InvalidDataException("Unexpected pet sprite dimensions.");
                AddRow("idle", sheet, 0, 6); AddRow("right", sheet, 1, 8); AddRow("left", sheet, 2, 8);
                AddRow("wave", sheet, 3, 4); AddRow("jump", sheet, 4, 5);
            }
            using (Bitmap sheet = Load("water.png")) AddRow("water", sheet, 0, 6);
            using (Bitmap sheet = Load("bamboo.png")) AddRow("bamboo", sheet, 0, 8);
            using (Bitmap sheet = Load("sleep.png")) AddRow("sleep", sheet, 0, 6);
            using (Bitmap sheet = Load("finisher.png")) AddRow("finisher", sheet, 0, 6);
            using (Bitmap sheet = Load("reading.png")) AddRow("reading", sheet, 0, 6);
            using (Bitmap sheet = Load("aim.png")) AddRow("aim", sheet, 0, 8);
        }
        public static Bitmap Load(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) throw new FileNotFoundException("Missing embedded artwork: " + name);
                using (Bitmap image = new Bitmap(stream)) return new Bitmap(image);
            }
        }
        private void AddRow(string name, Bitmap sheet, int row, int count)
        {
            if (sheet.Width < count * 192 || sheet.Height < (row + 1) * 208) throw new InvalidDataException("Incomplete animation: " + name);
            Bitmap[] frames = new Bitmap[count];
            for (int i = 0; i < count; i++) frames[i] = sheet.Clone(new Rectangle(i * 192, row * 208, 192, 208), PixelFormat.Format32bppArgb);
            clips[name] = frames;
        }
        public Bitmap[] this[string name] { get { return clips[name]; } }
        public void Dispose() { foreach (Bitmap[] row in clips.Values) foreach (Bitmap b in row) b.Dispose(); }
    }

    internal class AlphaWindow : Form
    {
        private Bitmap hitImage;
        public AlphaWindow()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams p = base.CreateParams; p.ExStyle |= 0x80000 | 0x80 | 0x08000000; return p; }
        }
        public void Present(Bitmap bitmap)
        {
            if (hitImage != null) hitImage.Dispose();
            hitImage = new Bitmap(bitmap);
            Size = bitmap.Size;
            IntPtr screen = Native.GetDC(IntPtr.Zero), memory = Native.CreateCompatibleDC(screen);
            IntPtr handle = bitmap.GetHbitmap(Color.FromArgb(0)), old = Native.SelectObject(memory, handle);
            try
            {
                Native.POINT top = new Native.POINT(Left, Top), source = new Native.POINT(0, 0);
                Native.SIZE size = new Native.SIZE(bitmap.Width, bitmap.Height);
                Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION(); blend.SourceConstantAlpha = 255; blend.AlphaFormat = 1;
                if (!Native.UpdateLayeredWindow(Handle, screen, ref top, ref size, memory, ref source, 0, ref blend, 2))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { Native.SelectObject(memory, old); Native.DeleteObject(handle); Native.DeleteDC(memory); Native.ReleaseDC(IntPtr.Zero, screen); }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84 && hitImage != null)
            {
                long value = m.LParam.ToInt64();
                Point p = PointToClient(new Point((short)(value & 0xffff), (short)((value >> 16) & 0xffff)));
                m.Result = new IntPtr(p.X >= 0 && p.Y >= 0 && p.X < hitImage.Width && p.Y < hitImage.Height && hitImage.GetPixel(p.X, p.Y).A > 32 ? 1 : -1);
                return;
            }
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
            base.WndProc(ref m);
        }
        protected bool PointerOnArtwork()
        {
            if (hitImage == null) return false;
            Point p = PointToClient(Cursor.Position);
            return p.X >= 0 && p.Y >= 0 && p.X < hitImage.Width && p.Y < hitImage.Height && hitImage.GetPixel(p.X, p.Y).A > 32;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && hitImage != null) hitImage.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class BambooPost : AlphaWindow
    {
        public bool Cut;
        public int CenterX;
        public void Render(Bitmap image, int x, int bottom, double scale)
        {
            int width = (int)(image.Width * scale), height = (int)(image.Height * scale);
            CenterX = x; Location = new Point(x - width / 2, bottom - height);
            using (Bitmap b = new Bitmap(image, width, height)) Present(b);
        }
        public void RenderTall(Bitmap image, int x, int bottom, int height, int width)
        {
            CenterX = x; Location = new Point(x - width / 2, bottom - height);
            using (Bitmap b = new Bitmap(image, width, Math.Max(1, height))) Present(b);
        }
        public void RenderFalling(Bitmap image, int x, int top, double progress)
        {
            double angle = progress * 40, rad = angle * Math.PI / 180;
            int w = (int)(image.Width * Math.Cos(rad) + image.Height * Math.Sin(rad)) + 4;
            int h = (int)(image.Height * Math.Cos(rad) + image.Width * Math.Sin(rad)) + 4;
            Location = new Point(x - w / 2 + (int)(progress * 100), top + (int)(progress * progress * 220));
            using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(b))
            using (ImageAttributes attributes = new ImageAttributes())
            {
                ColorMatrix matrix = new ColorMatrix(); matrix.Matrix33 = (float)(1 - progress); attributes.SetColorMatrix(matrix);
                g.TranslateTransform(w / 2f, h / 2f); g.RotateTransform((float)angle);
                g.DrawImage(image, new Rectangle(-image.Width / 2, -image.Height / 2, image.Width, image.Height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                Present(b);
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; }
            base.WndProc(ref m);
        }
    }

    internal sealed class Companion : AlphaWindow
    {
        private readonly Preferences prefs;
        private readonly ReminderSchedule reminders;
        private readonly HourlyRoutine hourly;
        private readonly Art art;
        private readonly Random random = new Random();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly List<BambooPost> posts = new List<BambooPost>();
        private readonly Bitmap bambooProp;
        private BambooPost tallPost, fallingPost;
        private Bitmap tallImage, fallingImage;
        private HourPhase displayedHourPhase = HourPhase.Normal;
        private bool tallCut;
        private int tallX;
        private SpeechSynthesizer speech;
        private ReminderBubble bubble;
        private SettingsForm settings;
        private FocusPanel focusPanel;
        private readonly PomodoroClock focusClock = new PomodoroClock(DateTime.UtcNow);
        private BambooPost focusTree;
        private Bitmap treeArt;
        private Point previousCursor;
        private double combatUntil, slashUntil;
        private int swordDirection, startX;
        private Rectangle area;
        private string monitor = "", clip = "idle";
        private int frame, direction = 1, nextPost, command, bambooCuts;
        private double positionX, lastTick, frameElapsed, behaviorUntil, actionUntil, regrowAt;
        private bool walking, dragging, moved, exiting;
        private Point dragStart, formStart;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly ToolStripMenuItem roamItem = new ToolStripMenuItem("Pause walking"), remindersItem = new ToolStripMenuItem("Pause reminders");
        private readonly ToolStripMenuItem statusItem = new ToolStripMenuItem();
        public Companion(int initialCommand)
        {
            Text = Program.WindowTitle; command = initialCommand;
            prefs = Preferences.Load(Program.SettingsPath); reminders = new ReminderSchedule(prefs, DateTime.UtcNow); hourly = new HourlyRoutine(DateTime.UtcNow);
            hourly.NapIntervalMinutes = prefs.HourlyMinutes; hourly.NapDurationSeconds = prefs.NapSeconds; hourly.Cancel(DateTime.UtcNow);
            art = new Art(); bambooProp = Art.Load("bamboo-post.png");
            treeArt = Art.Load("tree.png"); previousCursor=Cursor.Position;
            try { speech = new SpeechSynthesizer(); SelectVoice(); } catch (Exception ex) { Program.Log("Voice unavailable: " + ex.Message); }
            BuildMenu();
            using (Bitmap iconImage = new Bitmap(art["idle"][0], 32, 35))
            {
                IntPtr iconHandle = iconImage.GetHicon();
                try { tray.Icon = (Icon)Icon.FromHandle(iconHandle).Clone(); } finally { Native.DestroyIcon(iconHandle); }
            }
            tray.Text = "Jalsa Pawan - water 30m / blink 10m"; tray.ContextMenuStrip = menu; tray.Visible = true;
            tray.DoubleClick += delegate { OpenSettings(); };
            MouseDown += OnPetDown; MouseMove += OnPetMove; MouseUp += OnPetUp;
            timer.Interval = 30; timer.Tick += Tick;
            Shown += delegate
            {
                SetMonitor(Screen.PrimaryScreen); positionX = area.Left + area.Width * 0.1;
                Snap(); Play("idle"); StartWalking(); timer.Start();
                if(prefs.FocusMode) EnterFocus();
                if (command != 0) ExecuteCommand(command);
            };
        }
        private new double Scale { get { return prefs.SizePercent / 100.0; } }
        private void BuildMenu()
        {
            menu.Items.Add("Jalsa Pawan", null, delegate { OpenSettings(); });
            menu.Items.Add(statusItem); statusItem.Enabled = false;
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(roamItem); roamItem.Click += delegate { prefs.Roam = !prefs.Roam; Save(); RebuildPosts(); if (prefs.Roam) StartWalking(); else { walking = false; actionUntil = 0; Play("idle"); } };
            menu.Items.Add(remindersItem); remindersItem.Click += delegate { prefs.Reminders = !prefs.Reminders; reminders.Reset(DateTime.UtcNow); Save(); };
            menu.Items.Add("Water break now", null, delegate { Remind(ReminderKind.Water); });
            menu.Items.Add("Blink break now", null, delegate { Remind(ReminderKind.Blink); });
            menu.Items.Add("Bamboo round", null, delegate { BeginRound(); });
            menu.Items.Add("Preview hourly nap + giant bamboo (fast)", null, delegate { LeaveFocus();hourly.StartPreview(DateTime.UtcNow); });
            menu.Items.Add("Wave", null, delegate { Act("wave", 2.5); });
            menu.Items.Add("Settings...", null, delegate { OpenSettings(); });
            menu.Items.Add("Pomodoro focus timer", null, delegate { EnterFocus(); OpenFocusPanel(); });
            ToolStripMenuItem mouseSword=new ToolStripMenuItem("Cursor-controlled katana"); mouseSword.Checked=prefs.MouseSword;
            mouseSword.Click+=delegate {prefs.MouseSword=!prefs.MouseSword;mouseSword.Checked=prefs.MouseSword;Save();};menu.Items.Add(mouseSword);
            ToolStripMenuItem screens = new ToolStripMenuItem("Move to screen");
            foreach (Screen screen in Screen.AllScreens)
            {
                Screen captured = screen;
                screens.DropDownItems.Add(screen.DeviceName + (screen.Primary ? " (primary)" : ""), null, delegate { SetMonitor(captured); positionX = area.Left + 30; Snap(); StartWalking(); });
            }
            menu.Items.Add(screens); menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit", null, delegate { RequestQuit(); });
            menu.Opening += delegate
            {
                roamItem.Text = prefs.Roam ? "Pause walking" : "Resume walking";
                remindersItem.Text = prefs.Reminders ? "Pause reminders" : "Resume reminders";
                statusItem.Text = hourly.Phase != HourPhase.Normal ? "Hourly routine: " + hourly.Phase : prefs.Reminders ? "Next blink " + reminders.NextBlink.ToLocalTime().ToString("t") + " | water " + reminders.NextWater.ToLocalTime().ToString("t") : "Reminders paused";
            };
        }
        private void SetMonitor(Screen screen)
        {
            monitor = screen.DeviceName; area = screen.WorkingArea;
            RebuildPosts();
        }
        private void RebuildPosts()
        {
            foreach (BambooPost post in posts) post.Dispose(); posts.Clear(); nextPost = 0;
            if (prefs.FocusMode || !prefs.Bamboo || !prefs.Roam) return;
            for (int i = 1; i <= 3; i++)
            {
                BambooPost post = new BambooPost();
                post.Render(bambooProp, area.Left + (int)(area.Width * (0.2 + (i - 1) * 0.3)), area.Bottom, Scale);
                posts.Add(post); post.Show();
            }
            regrowAt = 0;
        }
        private void Snap()
        {
            positionX = Math.Max(area.Left, Math.Min(area.Right - (int)(192 * Scale), positionX));
            Location = new Point((int)positionX, area.Bottom - (int)(202 * Scale));
        }
        private void Play(string name)
        {
            if (clip == name && IsHandleCreated) { Render(); return; }
            clip = name; frame = 0; frameElapsed = 0; Render();
        }
        private void Render()
        {
            Bitmap b = art[clip][frame];
            using (Bitmap scaled = new Bitmap((int)(192 * Scale), (int)(208 * Scale), PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(b, new Rectangle(0, 0, scaled.Width, scaled.Height));
                }
                Present(scaled);
            }
        }
        private int Duration()
        {
            if (clip == "idle") return new int[] { 280, 110, 110, 140, 140, 320 }[frame];
            if (clip == "wave") return frame == 3 ? 280 : 140;
            if (clip == "water") return new int[] { 350, 300, 500, 500, 300, 450 }[frame];
            if (clip == "sleep") return 450;
            if (clip == "reading") return new int[]{450,350,150,350,450,450}[frame];
            if (clip == "finisher") return 170;
            if (clip == "jump") return 140;
            if (clip == "bamboo") return new int[] { 220, 200, 160, 110, 120, 220, 260, 280 }[frame];
            return frame == 7 ? 220 : 120;
        }
        private void StartWalking()
        {
            if (!prefs.Roam) { walking = false; Play("idle"); return; }
            walking = true; behaviorUntil = clock.Elapsed.TotalSeconds + random.Next(5, 12);
            Play(direction == 1 ? "right" : "left");
        }
        private void Act(string name, double seconds)
        {
            if(name!="reading"&&focusTree!=null&&!focusTree.IsDisposed)focusTree.Hide();
            walking = false; actionUntil = clock.Elapsed.TotalSeconds + seconds; Play(name);
        }
        private void BeginRound()
        {
            LeaveFocus();
            hourly.Cancel(DateTime.UtcNow); CleanupTall(); displayedHourPhase = HourPhase.Normal;
            prefs.Bamboo = true; prefs.Roam = true; Save(); RebuildPosts();
            positionX = area.Left + 8; direction = 1; Snap(); actionUntil = 0; StartWalking();
        }
        private void Tick(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds, dt = Math.Min(0.1, now - lastTick); lastTick = now;
            ReminderKind due = reminders.Poll(DateTime.UtcNow);
            if (due != ReminderKind.None) Remind(due);
            if (dragging) return;
            if(HandleSword(now,dt))return;
            if(prefs.FocusMode){HandleFocus(DateTime.UtcNow,now,dt);return;}
            if (prefs.Hourly || hourly.Preview)
            {
                hourly.Poll(DateTime.UtcNow);
                if (HandleHourly(DateTime.UtcNow, dt)) return;
            }
            if (menu.Visible || (settings != null && !settings.IsDisposed))
            {
                if (actionUntil > 0 && now >= actionUntil) { actionUntil = 0; Play("idle"); }
                Animate(dt); return;
            }
            Screen current = Array.Find(Screen.AllScreens, delegate(Screen s) { return s.DeviceName == monitor; }) ?? Screen.PrimaryScreen;
            if (current.WorkingArea != area) { area = current.WorkingArea; Snap(); RebuildPosts(); }
            if (actionUntil > now) { Animate(dt); return; }
            if (actionUntil > 0) { actionUntil = 0; StartWalking(); }
            if (prefs.Bamboo && prefs.Roam && posts.Count > 0)
            {
                if (nextPost < posts.Count)
                {
                    double target = posts[nextPost].CenterX - 144 * Scale;
                    double delta = target - positionX;
                    if (Math.Abs(delta) <= 3)
                    {
                        positionX = target; Snap(); posts[nextPost].Hide(); posts[nextPost].Cut = true; nextPost++; bambooCuts++;
                        Act("bamboo", 1.65);
                        if (nextPost == posts.Count) regrowAt = now + prefs.BambooRegrowSeconds;
                    }
                    else
                    {
                        direction = delta > 0 ? 1 : -1; walking = true;
                        string movement = direction == 1 ? "right" : "left";
                        if (clip != movement) Play(movement);
                        positionX += direction * Math.Min(Math.Abs(delta), 72 * Scale * dt); Snap();
                    }
                }
                else if (now >= regrowAt) { RebuildPosts(); direction = -1; StartWalking(); }
                else Roam(now, dt);
            }
            else if (prefs.Roam) Roam(now, dt);
            else if (clip != "idle") Play("idle");
            Animate(dt);
        }
        private void Roam(double now, double dt)
        {
            if (now >= behaviorUntil)
            {
                if (walking) { walking = false; behaviorUntil = now + random.Next(2, 5); Play("idle"); }
                else StartWalking();
            }
            if (!walking) return;
            positionX += direction * 55 * Scale * dt;
            if (positionX <= area.Left || positionX >= area.Right - Width)
            {
                direction = -direction; Play(direction == 1 ? "right" : "left");
            }
            Snap();
        }
        private void Animate(double dt)
        {
            frameElapsed += dt * 1000;
            if (frameElapsed >= Duration())
            {
                frameElapsed -= Duration(); frame = (frame + 1) % art[clip].Length; Render();
            }
        }
        private void Remind(ReminderKind kind)
        {
            bool water = (kind & ReminderKind.Water) != 0, blink = (kind & ReminderKind.Blink) != 0;
            string title = water && blink ? "Water + blink break" : water ? "Water break" : "Blink break";
            string text = water && blink ? "Time for some water. Take a moment to blink your eyes, too." : water ? "Time to drink some water." : "Take a moment to blink your eyes.";
            if (hourly.Phase == HourPhase.Normal) Act(water ? "water" : "idle", 8);
            if (bubble != null) bubble.Close();
            bubble = new ReminderBubble(title, text, delegate { reminders.Snooze(kind, DateTime.UtcNow); });
            int x = Math.Max(area.Left + 8, Math.Min(area.Right - bubble.Width - 8, Left - 60));
            bubble.Location = new Point(x, Math.Max(area.Top + 8, Top - bubble.Height - 4)); bubble.Show();
            if (prefs.Voice && speech != null)
            {
                try { speech.SpeakAsyncCancelAll(); speech.SpeakAsync(text); } catch (Exception ex) { Program.Log("Speech: " + ex.Message); }
            }
        }
        private void SelectVoice()
        {
            if (speech == null || String.IsNullOrEmpty(prefs.VoiceName)) return;
            try { speech.SelectVoice(prefs.VoiceName); } catch { prefs.VoiceName = ""; }
        }
        private void Save()
        {
            try { prefs.Save(Program.SettingsPath); } catch (Exception ex) { Program.Log("Saving settings: " + ex.Message); }
        }
        private void OpenSettings()
        {
            if (settings != null && !settings.IsDisposed) { settings.Activate(); return; }
            settings = new SettingsForm(prefs, speech, reminders, delegate(ReminderKind kind) { Remind(kind); }, delegate { BeginRound(); }, delegate { LeaveFocus();hourly.StartPreview(DateTime.UtcNow); }, delegate { Act("wave", 2.5); }, delegate { EnterFocus();OpenFocusPanel(); },RequestQuit);
            settings.FormClosed += delegate
            {
                if (settings.DialogResult != DialogResult.OK) return;
                Save(); reminders.Reset(DateTime.UtcNow); SelectVoice();
                hourly.NapIntervalMinutes = prefs.HourlyMinutes; hourly.NapDurationSeconds = prefs.NapSeconds;
                hourly.Cancel(DateTime.UtcNow); CleanupTall(); displayedHourPhase = HourPhase.Normal;
                RebuildPosts(); Snap(); Play("idle"); StartWalking();
                if(prefs.FocusMode) EnterFocus();
            };
            settings.Show(); settings.Activate();
        }
        private void OnPetDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { menu.Show(Cursor.Position); return; }
            if (hourly.Phase != HourPhase.Normal) return;
            if (e.Button != MouseButtons.Left) return;
            dragging = true; moved = false; dragStart = Cursor.Position; formStart = Location; Capture = true;
        }
        private void OnPetMove(object sender, MouseEventArgs e)
        {
            if (hourly.Phase == HourPhase.Waiting && PointerOnArtwork()) hourly.Hover(DateTime.UtcNow);
            if (!dragging) return;
            Point p = Cursor.Position;
            if (Math.Abs(p.X - dragStart.X) + Math.Abs(p.Y - dragStart.Y) > 4) moved = true;
            if (moved) { Location = new Point(formStart.X + p.X - dragStart.X, formStart.Y + p.Y - dragStart.Y); positionX = Left; }
        }
        private void OnPetUp(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            dragging = false; Capture = false;
            if (moved) { Screen screen = Screen.FromPoint(Cursor.Position); if (screen.DeviceName != monitor) SetMonitor(screen); Snap(); }
            else OpenSettings();
            if(moved&&prefs.FocusMode)EnterFocus();
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg >= 0x8001 && m.Msg <= 0x8008) { ExecuteCommand(m.Msg - 0x8000); return; }
            base.WndProc(ref m);
        }
        private void ExecuteCommand(int value)
        {
            if(value==-1)
            {
                OpenSettings();Remind(ReminderKind.Water);
                System.Windows.Forms.Timer quitTimer=new System.Windows.Forms.Timer();quitTimer.Interval=800;
                quitTimer.Tick+=delegate {quitTimer.Stop();quitTimer.Dispose();RequestQuit();};quitTimer.Start();return;
            }
            if (value == 1) OpenSettings(); else if (value == 2) Remind(ReminderKind.Water);
            else if (value == 3) Remind(ReminderKind.Blink); else if (value == 4) BeginRound();
            else if (value == 5) RequestQuit(); else if (value == 6) { prefs.Roam = false; walking = false; actionUntil = 0; RebuildPosts(); Play("idle"); Save(); }
            else if (value == 7) WriteStatus();
            else if (value == 8) {LeaveFocus();hourly.StartPreview(DateTime.UtcNow);}
        }
        private void RequestQuit()
        {
            if(exiting||IsDisposed)return;
            timer.Stop();
            BeginInvoke(new Action(delegate {if(!IsDisposed)Close();}));
        }
        private void EnterFocus()
        {
            if(!prefs.FocusMode)focusClock.Reset(DateTime.UtcNow,false);
            prefs.FocusMode=true; walking=false; actionUntil=0; combatUntil=0;
            hourly.Cancel(DateTime.UtcNow);CleanupTall();displayedHourPhase=HourPhase.Normal;RebuildPosts();
            startX=area.Left+48; positionX=Math.Max(area.Left,Math.Min(area.Right-Width,startX-96*Scale));Snap();
            if(focusTree==null)focusTree=new BambooPost();
            focusTree.Render(treeArt,Left+(int)(68*Scale),area.Bottom,Scale);focusTree.Show();
            Play("reading");Save();
            ThreadPool.QueueUserWorkItem(delegate {
                int actual=StartButtonLocator.FindX(startX);
                if(!exiting&&IsHandleCreated)try {BeginInvoke(new Action(delegate {
                    if(!prefs.FocusMode||exiting)return;startX=actual;
                    positionX=Math.Max(area.Left,Math.Min(area.Right-Width,startX-96*Scale));Snap();
                    focusTree.Render(treeArt,Left+(int)(68*Scale),area.Bottom,Scale);
                }));}catch(InvalidOperationException){}
            });
        }
        private void LeaveFocus()
        {
            prefs.FocusMode=false;if(focusTree!=null){focusTree.Dispose();focusTree=null;}
            hourly.Cancel(DateTime.UtcNow);Save();
        }
        private void OpenFocusPanel()
        {
            if(focusPanel!=null&&!focusPanel.IsDisposed){focusPanel.Activate();return;}
            focusPanel=new FocusPanel(focusClock,delegate {LeaveFocus();RebuildPosts();StartWalking();});focusPanel.Show();focusPanel.Activate();
        }
        private void HandleFocus(DateTime utc,double now,double dt)
        {
            if(focusClock.Tick(utc))
            {
                string text=focusClock.IsBreak?"Focus complete. Take a five minute break.":"Break is over. Time for a new focus session.";
                if(bubble!=null&&!bubble.IsDisposed)bubble.Close();
                bubble=new ReminderBubble(focusClock.IsBreak?"Break time":"Focus time",text,delegate {});
                bubble.Location=new Point(Math.Max(area.Left,Math.Min(area.Right-340,Left)),Math.Max(area.Top,Top-170));bubble.Show();
                if(prefs.Voice&&speech!=null)try{speech.SpeakAsyncCancelAll();speech.SpeakAsync(text);}catch(Exception ex){Program.Log(ex.Message);}
                Act("wave",3);
            }
            if(actionUntil>now){if(focusTree!=null&&!focusTree.IsDisposed)focusTree.Hide();Animate(dt);return;}
            string name=focusClock.IsBreak?"idle":"reading";
            if(focusTree!=null&&!focusTree.IsDisposed){if(name=="reading"){if(!focusTree.Visible)focusTree.Show();}else focusTree.Hide();}
            if(clip!=name)Play(name);Animate(dt);
            tray.Text="Jalsa Pawan - "+(focusClock.IsBreak?"Break ":"Focus ")+focusClock.Display+(focusClock.Paused?" paused":"");
        }
        private bool HandleSword(double now,double dt)
        {
            Point cursor=Cursor.Position;
            double dx=cursor.X-previousCursor.X,dy=cursor.Y-previousCursor.Y;previousCursor=cursor;
            bool blocked=menu.Visible||(settings!=null&&!settings.IsDisposed)||(focusPanel!=null&&!focusPanel.IsDisposed)||hourly.Phase!=HourPhase.Normal;
            double px=cursor.X-(Left+96*Scale),py=cursor.Y-(Top+110*Scale);
            if(prefs.MouseSword&&!blocked&&px*px+py*py<150*150*Scale*Scale&&(Math.Abs(dx)+Math.Abs(dy)>1))
            {
                combatUntil=now+2.5;
                double speed=Math.Sqrt(dx*dx+dy*dy)/Math.Max(.01,dt);
                double angle=Math.Atan2(px,-py);
                swordDirection=((int)Math.Round(angle/(Math.PI/4))+8)%8;
                if(speed>180){slashUntil=now+.22;swordDirection=((int)Math.Round(Math.Atan2(dx,-dy)/(Math.PI/4))+8)%8;}
            }
            if(now>=combatUntil||blocked)return false;
            if(focusTree!=null&&!focusTree.IsDisposed)focusTree.Hide();
            clip="aim";frame=now<slashUntil?(swordDirection+7)%8:swordDirection;Render();
            if(!prefs.FocusMode&&now<slashUntil)
                foreach(BambooPost post in posts)if(!post.Cut&&Math.Abs(post.CenterX-cursor.X)<60*Scale){post.Cut=true;post.Hide();}
            return true;
        }
        private void CleanupTall()
        {
            if (tallPost != null) { tallPost.Dispose(); tallPost = null; }
            if (fallingPost != null) { fallingPost.Dispose(); fallingPost = null; }
            if (tallImage != null) { tallImage.Dispose(); tallImage = null; }
            if (fallingImage != null) { fallingImage.Dispose(); fallingImage = null; }
        }
        private bool HandleHourly(DateTime now, double dt)
        {
            HourPhase phase = hourly.Phase;
            if (phase == HourPhase.Normal)
            {
                if (displayedHourPhase != HourPhase.Normal)
                {
                    CleanupTall(); displayedHourPhase = phase; RebuildPosts(); Snap(); StartWalking();
                }
                return false;
            }
            if (phase != displayedHourPhase)
            {
                displayedHourPhase = phase;
                if (phase == HourPhase.Sleeping)
                {
                    foreach (BambooPost post in posts) post.Hide();
                    CleanupTall(); tallCut = false; walking = false; actionUntil = 0;
                    positionX = Math.Min(positionX, area.Right - 230 * Scale); Snap();
                    tallX = Left + (int)(144 * Scale);
                    tallImage = new Bitmap(bambooProp, (int)(54 * Scale), Math.Max(100, area.Height - 16));
                    tallPost = new BambooPost(); Play("sleep");
                }
                else if (phase == HourPhase.Waiting && tallPost != null)
                {
                    tallPost.RenderTall(tallImage, tallX, area.Bottom, tallImage.Height, tallImage.Width); tallPost.Show();
                }
                else if (phase == HourPhase.Attack) Play("jump");
            }
            if (phase == HourPhase.Sleeping) { Animate(dt); return true; }
            if (phase == HourPhase.Growing)
            {
                int height = Math.Max(1, (int)(tallImage.Height * hourly.Growth(now)));
                tallPost.RenderTall(tallImage, tallX, area.Bottom, height, tallImage.Width); tallPost.Show();
                Animate(dt); return true;
            }
            if (phase == HourPhase.Waiting)
            {
                Animate(dt);
                if (PointerOnArtwork()) hourly.Hover(now);
                return true;
            }
            if (phase == HourPhase.Attack)
            {
                double t = (now - hourly.PhaseStart).TotalSeconds;
                double height = Math.Sin(Math.PI * Math.Min(1, t / 1.6)) * Math.Max(80, area.Height - 230 * Scale);
                Top = area.Bottom - (int)(202 * Scale) - (int)height;
                string name = t < 0.55 ? "jump" : "finisher";
                int index = t < 0.55 ? Math.Min(2, (int)(t / 0.18)) : Math.Min(5, (int)((t - 0.55) / 0.18));
                clip = name; frame = index; Render();
                if (t >= 0.92 && !tallCut)
                {
                    tallCut = true;
                    int stump = Math.Min(70, tallImage.Height / 4);
                    fallingImage = tallImage.Clone(new Rectangle(0, 0, tallImage.Width, tallImage.Height - stump), PixelFormat.Format32bppArgb);
                    using (Bitmap baseImage = tallImage.Clone(new Rectangle(0, tallImage.Height - stump, tallImage.Width, stump), PixelFormat.Format32bppArgb))
                        tallPost.RenderTall(baseImage, tallX, area.Bottom, stump, tallImage.Width);
                    fallingPost = new BambooPost(); fallingPost.Show();
                }
                if (fallingPost != null)
                {
                    double progress = Math.Min(1, Math.Max(0, (t - 0.92) / 0.7));
                    if (progress < 1) fallingPost.RenderFalling(fallingImage, tallX, area.Top + 16, progress);
                    else fallingPost.Hide();
                }
                return true;
            }
            return false;
        }
        private void WriteStatus()
        {
            string status = "{\"running\":true,\"clip\":\"" + clip + "\",\"frame\":" + frame + ",\"x\":" + Left + ",\"y\":" + Top + ",\"bamboo_cuts\":" + bambooCuts + ",\"next_station\":" + nextPost + ",\"water_minutes\":" + prefs.WaterMinutes + ",\"blink_minutes\":" + prefs.BlinkMinutes + ",\"voice_available\":" + (speech != null ? "true" : "false") + ",\"voice_enabled\":" + (prefs.Voice ? "true" : "false") + ",\"roam\":" + (prefs.Roam ? "true" : "false") + ",\"hourly_phase\":\"" + hourly.Phase + "\",\"tall_bamboo_cut\":" + (tallCut ? "true" : "false") + ",\"working_area_bottom\":" + area.Bottom + ",\"pet_baseline\":" + (Top + (int)(202 * Scale)) + "}";
            status=status.Substring(0,status.Length-1)+",\"focus_mode\":"+(prefs.FocusMode?"true":"false")+",\"focus_break\":"+(focusClock.IsBreak?"true":"false")+",\"focus_remaining_seconds\":"+((int)focusClock.Remaining)+",\"mouse_katana\":"+(prefs.MouseSword?"true":"false")+"}";
            File.WriteAllText(Path.Combine(Program.Folder, "runtime-status.json"), status);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting)
            {
                exiting = true; timer.Stop(); tray.Visible = false; Save();
                foreach (BambooPost post in posts) post.Dispose();
                CleanupTall();
                timer.Tick-=Tick;
                if (bubble != null&&!bubble.IsDisposed) bubble.Close();
                if(focusPanel!=null&&!focusPanel.IsDisposed)focusPanel.Close();
                if (settings != null&&!settings.IsDisposed) {settings.DialogResult=DialogResult.Cancel;settings.Close();}
                if(focusTree!=null)focusTree.Dispose();
                if (speech != null) {try{speech.SpeakAsyncCancelAll();speech.Dispose();}catch(Exception ex){Program.Log("Speech shutdown: "+ex.Message);}speech=null;}
            }
            base.OnFormClosing(e);
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            art.Dispose();bambooProp.Dispose();treeArt.Dispose();tray.Dispose();timer.Dispose();menu.Dispose();
            base.OnFormClosed(e);
        }
    }

    internal sealed class ReminderBubble : Form
    {
        private readonly System.Windows.Forms.Timer dismiss = new System.Windows.Forms.Timer();
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { CreateParams p = base.CreateParams; p.ExStyle |= 0x80 | 0x08000000; return p; } }
        public ReminderBubble(string title, string text, Action snooze)
        {
            Text = title; Size = new Size(340, 164); FormBorderStyle = FormBorderStyle.None; TopMost = true;
            ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; BackColor = Color.FromArgb(24, 34, 47);
            Font = new Font("Segoe UI", 10); ForeColor = Color.White;
            Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 13, FontStyle.Bold), Location = new Point(16, 13), Size = new Size(310, 27) });
            Controls.Add(new Label { Text = text, Location = new Point(16, 48), Size = new Size(310, 48) });
            Button done = new Button { Text = "Done", Location = new Point(16, 111), Size = new Size(90, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 104, 84) };
            Button later = new Button { Text = "Snooze 5 min", Location = new Point(120, 111), Size = new Size(140, 32), FlatStyle = FlatStyle.Flat };
            done.Click += delegate { Close(); }; later.Click += delegate { snooze(); Close(); };
            Controls.Add(done); Controls.Add(later); dismiss.Interval = 15000; dismiss.Tick += delegate { Close(); };
            Shown += delegate { dismiss.Start(); }; FormClosed += delegate { dismiss.Dispose(); };
        }
    }

    internal sealed class SettingsForm : Form
    {
        public SettingsForm(Preferences prefs, SpeechSynthesizer speech, ReminderSchedule schedule, Action<ReminderKind> remind, Action bamboo, Action napPreview, Action wavePreview,Action focusPreview,Action stopApp)
        {
            Text = "Jalsa Pawan - Control Panel"; ClientSize = new Size(560, 800); StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(23, 31, 42); ForeColor = Color.White;
            LabelAt("Jalsa Pawan", 24, 18, 480, 32).Font = new Font("Segoe UI", 19, FontStyle.Bold);
            LabelAt("A little company, a little sword practice.", 26, 56, 500, 25).ForeColor = Color.Silver;
            LabelAt("Blink reminder (minutes)", 26, 104, 275, 25);
            NumericUpDown blink = Number(prefs.BlinkMinutes, 1, 240, 375, 101);
            LabelAt("Water reminder (minutes)", 26, 146, 275, 25);
            NumericUpDown water = Number(prefs.WaterMinutes, 1, 480, 375, 143);
            CheckBox reminders = Check("Enable reminders", prefs.Reminders, 26, 188);
            CheckBox voice = Check("Speak reminders", prefs.Voice, 285, 188);
            LabelAt("Windows voice", 26, 231, 170, 25);
            ComboBox voices = new ComboBox { Location = new Point(205, 228), Size = new Size(325, 28), DropDownStyle = ComboBoxStyle.DropDownList };
            voices.Items.Add("Windows default");
            if (speech != null) foreach (InstalledVoice v in speech.GetInstalledVoices()) if (v.Enabled) voices.Items.Add(v.VoiceInfo.Name);
            voices.SelectedIndex = String.IsNullOrEmpty(prefs.VoiceName) ? 0 : voices.Items.IndexOf(prefs.VoiceName);
            if (voices.SelectedIndex < 0) voices.SelectedIndex = 0; Controls.Add(voices);
            LabelAt("Uses installed speech voices; no celebrity voice cloning.", 26, 263, 510, 24).ForeColor = Color.Silver;
            CheckBox roam = Check("Walk along taskbar", prefs.Roam, 26, 305);
            CheckBox practice = Check("Three bamboo stations", prefs.Bamboo, 285, 305);
            LabelAt("Pet size (%)", 26, 349, 270, 25); NumericUpDown size = Number(prefs.SizePercent, 70, 140, 375, 346);
            CheckBox hourly = Check("Hourly nap + giant bamboo (hover to wake)", prefs.Hourly, 26, 390); hourly.Width = 510;
            LabelAt("Nap every (minutes)", 26, 436, 275, 25); NumericUpDown hourlyMinutes = Number(prefs.HourlyMinutes, 5, 240, 375, 433);
            LabelAt("Nap length (seconds)", 26, 478, 275, 25); NumericUpDown napSeconds = Number(prefs.NapSeconds, 10, 600, 375, 475);
            LabelAt("Bamboo regrows after (seconds)", 26, 520, 330, 25); NumericUpDown regrow = Number(prefs.BambooRegrowSeconds, 5, 120, 375, 517);
            ButtonAt("Water now", 26, 568, 155, delegate { remind(ReminderKind.Water); });
            ButtonAt("Blink now", 200, 568, 155, delegate { remind(ReminderKind.Blink); });
            ButtonAt("Bamboo round", 375, 568, 155, delegate { bamboo(); });
            ButtonAt("Wave", 26, 612, 155, wavePreview);
            ButtonAt("Fast nap preview", 200, 612, 180, napPreview);
            ButtonAt("Focus timer",395,612,135,focusPreview);
            LabelAt("Fast preview: 4-second nap, then growth; hover to attack.\nClick pet to open this panel. Drag to reposition.\nRight-click pet or tray icon for Quit. Save restarts the timers.", 26, 666, 510, 72).ForeColor = Color.Silver;
            Button save = ButtonAt("Save", 375, 752, 155, delegate
            {
                prefs.BlinkMinutes = (int)blink.Value; prefs.WaterMinutes = (int)water.Value;
                prefs.SizePercent = (int)size.Value; prefs.Reminders = reminders.Checked; prefs.Voice = voice.Checked;
                prefs.Roam = roam.Checked; prefs.Bamboo = practice.Checked; prefs.Hourly = hourly.Checked;
                prefs.HourlyMinutes = (int)hourlyMinutes.Value; prefs.NapSeconds = (int)napSeconds.Value; prefs.BambooRegrowSeconds = (int)regrow.Value;
                prefs.VoiceName = voices.SelectedIndex == 0 ? "" : voices.SelectedItem.ToString();
                DialogResult = DialogResult.OK; Close();
            });
            save.BackColor = Color.FromArgb(44, 109, 87); AcceptButton = save;
            Button cancel = ButtonAt("Cancel", 200, 752, 155, delegate { Close(); }); CancelButton = cancel;
            Button stop = ButtonAt("Stop pet", 26, 752, 155, delegate {DialogResult=DialogResult.Cancel;stopApp();});
            stop.BackColor = Color.FromArgb(119, 49, 53);
        }
        private Label LabelAt(string text, int x, int y, int w, int h)
        {
            Label label = new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h) }; Controls.Add(label); return label;
        }
        private NumericUpDown Number(int value, int min, int max, int x, int y)
        {
            NumericUpDown control = new NumericUpDown { Location = new Point(x, y), Size = new Size(155, 28), Minimum = min, Maximum = max, Value = value }; Controls.Add(control); return control;
        }
        private CheckBox Check(string text, bool check, int x, int y)
        {
            CheckBox control = new CheckBox { Text = text, Checked = check, Location = new Point(x, y), Size = new Size(248, 28) }; Controls.Add(control); return control;
        }
        private Button ButtonAt(string text, int x, int y, int width, Action action)
        {
            Button b = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 34), FlatStyle = FlatStyle.Flat };
            b.Click += delegate { action(); }; Controls.Add(b); return b;
        }
    }

    internal static class SelfTest
    {
        private static readonly List<string> passed = new List<string>();
        private static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); passed.Add(name); }
        public static void Run(string report)
        {
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Preferences p = new Preferences(); ReminderSchedule s = new ReminderSchedule(p, t);
            PomodoroClock pomodoro=new PomodoroClock(t);
            Check(!pomodoro.Tick(t.AddMinutes(24))&&!pomodoro.IsBreak,"Focus lasts full25 minutes");
            Check(pomodoro.Tick(t.AddMinutes(25))&&pomodoro.IsBreak&&pomodoro.Remaining==300,"Focus transitions to5-minute break");
            Check(pomodoro.Tick(t.AddMinutes(30))&&!pomodoro.IsBreak&&pomodoro.Remaining==1500,"Break starts new25-minute focus");
            pomodoro.TogglePause(t.AddMinutes(31));double pausedRemaining=pomodoro.Remaining;
            pomodoro.Tick(t.AddMinutes(40));Check(pomodoro.Remaining==pausedRemaining,"Paused Pomodoro doesnot elapse");
            pomodoro.TogglePause(t.AddMinutes(40));pomodoro.Tick(t.AddMinutes(41));Check(pomodoro.Remaining==pausedRemaining-60,"Resume consumes only active time");
            pomodoro.Reset(t,false);Check(pomodoro.Display=="25:00","Focus countdown formatting");
            Check(s.Poll(t.AddMinutes(9)) == ReminderKind.None, "No early reminders");
            Check(s.Poll(t.AddMinutes(10)) == ReminderKind.Blink, "Blink due at 10 minutes");
            Check(s.Poll(t.AddMinutes(20)) == ReminderKind.Blink, "Blink repeats at 20 minutes");
            Check(s.Poll(t.AddMinutes(30)) == (ReminderKind.Blink | ReminderKind.Water), "Water and blink combine at 30 minutes");
            Check(s.Poll(t.AddMinutes(30).AddSeconds(1)) == ReminderKind.None, "No duplicate reminder");
            Check(s.Poll(t.AddHours(8)) == (ReminderKind.Water | ReminderKind.Blink), "Sleep gap gives one combined reminder");
            Check(s.Poll(t.AddHours(8).AddSeconds(1)) == ReminderKind.None, "No overdue reminder backlog");
            s.Snooze(ReminderKind.Water, t); Check(s.NextWater == t.AddMinutes(5), "Snooze schedules exactly five minutes");
            p.Reminders = false; Check(s.Poll(t.AddDays(1)) == ReminderKind.None, "Paused reminders stay silent");
            p.Reminders = true; s.Reset(t); Check(s.NextBlink == t.AddMinutes(10), "Resume resets timers");
            HourlyRoutine routine = new HourlyRoutine(t);
            routine.Poll(t.AddMinutes(59)); Check(routine.Phase == HourPhase.Normal, "No early hourly nap");
            routine.Poll(t.AddHours(1)); Check(routine.Phase == HourPhase.Sleeping, "Hourly nap begins at60 minutes");
            Check(!routine.Hover(t.AddHours(1).AddSeconds(50)), "Hover cannot interrupt required2-minute nap");
            routine.Poll(t.AddHours(1).AddSeconds(119)); Check(routine.Phase == HourPhase.Sleeping, "Nap lasts full120 seconds");
            routine.Poll(t.AddHours(1).AddMinutes(2)); Check(routine.Phase == HourPhase.Growing, "Bamboo grows after2-minute nap");
            Check(routine.Growth(t.AddHours(1).AddMinutes(2).AddSeconds(4)) == 0.5, "Bamboo growth halfway at4 seconds");
            routine.Poll(t.AddHours(1).AddMinutes(2).AddSeconds(8)); Check(routine.Phase == HourPhase.Waiting, "Fully grown bamboo waits for hover");
            routine.Poll(t.AddHours(2)); Check(routine.Phase == HourPhase.Waiting, "No automatic attack before hover");
            Check(routine.Hover(t.AddHours(2)), "Hover triggers attack");
            Check(!routine.Hover(t.AddHours(2).AddSeconds(1)), "Hover cannot retrigger one-shot attack");
            routine.Poll(t.AddHours(2).AddSeconds(2)); Check(routine.Phase == HourPhase.Normal && routine.NextNap == t.AddHours(3).AddSeconds(2), "One attack completes then hourly timer resets");
            p.BlinkMinutes = -3; p.WaterMinutes = 999; p.SizePercent = 999; p.Sanitize();
            Check(p.BlinkMinutes == 1 && p.WaterMinutes == 480 && p.SizePercent == 140, "Invalid settings clamped");
            string roundtrip = report + ".settings-test.xml";
            p = new Preferences(); p.Save(roundtrip); Preferences loaded = Preferences.Load(roundtrip);
            Check(loaded.BlinkMinutes == 10 && loaded.WaterMinutes == 30 && loaded.Bamboo && loaded.Voice, "Settings persistence round trip");
            File.Delete(roundtrip);
            using (Art art = new Art())
            {
                Check(art["water"].Length == 6 && art["bamboo"].Length == 8, "Custom action frames embedded");
                foreach (string name in new string[] { "idle", "left", "right", "wave", "water", "bamboo", "sleep", "finisher", "reading", "aim" })
                    foreach (Bitmap b in art[name])
                    {
                        int count = 0;
                        for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) if (b.GetPixel(x, y).A > 32) count++;
                        Check(count > 400 && count < 192 * 208 / 2, "Nonempty transparent frame " + name + "/" + count);
                    }
            }
            using (Bitmap bamboo = Art.Load("bamboo-post.png")) Check(bamboo.Height > 20 && bamboo.Width > 10, "Bamboo station resource");
            string json = "{\"ok\":true,\"checks\":[\"" + String.Join("\",\"", passed.ToArray()) + "\"]}";
            File.WriteAllText(report, json);
        }
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int Width, Height; public SIZE(int w, int h) { Width = w; Height = h; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [DllImport("user32.dll", SetLastError = true)] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdc, ref POINT top, ref SIZE size, IntPtr source, ref POINT sourcePoint, int color, ref BLENDFUNCTION blend, int flags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int value);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, int message, IntPtr w, IntPtr l);
    }
}
