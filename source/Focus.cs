using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Automation;

namespace JalsaDesktop
{
    public sealed class PomodoroClock
    {
        public int FocusMinutes = 25, BreakMinutes = 5;
        public bool IsBreak, Paused;
        public double Remaining = 1500;
        private DateTime last;
        public PomodoroClock(DateTime now) { last = now; }
        public bool Tick(DateTime now)
        {
            double elapsed = Math.Max(0, (now - last).TotalSeconds); last = now;
            if (Paused) return false;
            Remaining -= elapsed;
            if (Remaining > 0) return false;
            IsBreak = !IsBreak; Remaining = (IsBreak ? BreakMinutes : FocusMinutes) * 60;
            return true;
        }
        public void TogglePause(DateTime now) { Tick(now); Paused = !Paused; last = now; }
        public void Reset(DateTime now, bool takeBreak) { IsBreak = takeBreak; Remaining = (takeBreak ? BreakMinutes : FocusMinutes) * 60; Paused = false; last = now; }
        public string Display { get { return TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(Remaining))).ToString(@"mm\:ss"); } }
    }

    internal sealed class FocusPanel : Form
    {
        private readonly System.Windows.Forms.Timer refresh = new System.Windows.Forms.Timer();
        public FocusPanel(PomodoroClock clock, Action leaveFocus)
        {
            Text = "Jalsa Pawan - Focus Timer"; ClientSize = new Size(390, 290); FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen; MaximizeBox = false; MinimizeBox = false;
            BackColor = Color.FromArgb(23,31,42); ForeColor = Color.White; Font = new Font("Segoe UI",10);
            Label phase = new Label { Location = new Point(20,15), Size = new Size(350,28), TextAlign = ContentAlignment.MiddleCenter };
            Label digits = new Label { Location = new Point(20,48), Size = new Size(350,75), Font = new Font("Segoe UI",38,FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            Controls.Add(phase); Controls.Add(digits);
            Button pause = new Button { Location = new Point(20,135), Size = new Size(165,34), FlatStyle = FlatStyle.Flat };
            Button skip = new Button { Text = "Next phase", Location = new Point(205,135), Size = new Size(165,34), FlatStyle = FlatStyle.Flat };
            pause.Click += delegate { clock.TogglePause(DateTime.UtcNow); };
            skip.Click += delegate { clock.Reset(DateTime.UtcNow,!clock.IsBreak); };
            Controls.Add(pause); Controls.Add(skip);
            Label ft = new Label { Text = "Focus min", Location = new Point(20,187), Size = new Size(85,23) };
            NumericUpDown focus = new NumericUpDown { Minimum=1, Maximum=90, Value=clock.FocusMinutes, Location=new Point(105,183), Size=new Size(65,27) };
            Label bt = new Label { Text = "Break min", Location=new Point(205,187), Size=new Size(85,23) };
            NumericUpDown rest = new NumericUpDown { Minimum=1, Maximum=30, Value=clock.BreakMinutes, Location=new Point(300,183), Size=new Size(65,27) };
            Controls.Add(ft); Controls.Add(focus); Controls.Add(bt); Controls.Add(rest);
            Button reset = new Button { Text="Restart focus",Location=new Point(20,237),Size=new Size(165,34),FlatStyle=FlatStyle.Flat };
            reset.Click += delegate {clock.FocusMinutes=(int)focus.Value;clock.BreakMinutes=(int)rest.Value;clock.Reset(DateTime.UtcNow,false);};
            Button leave = new Button {Text="Leave Focus mode",Location=new Point(205,237),Size=new Size(165,34),FlatStyle=FlatStyle.Flat};
            leave.Click += delegate {leaveFocus();Close();}; Controls.Add(reset);Controls.Add(leave);
            refresh.Interval=250;refresh.Tick+=delegate {phase.Text=clock.IsBreak?"BREAK - stretch and relax":"FOCUS - reading under the tree";digits.Text=clock.Display;pause.Text=clock.Paused?"Resume":"Pause";};
            Shown+=delegate {refresh.Start();}; FormClosed+=delegate {refresh.Dispose();};
        }
    }

    internal static class StartButtonLocator
    {
        public static int FindX(int fallback)
        {
            try
            {
                IntPtr taskbar=Native.FindWindow("Shell_TrayWnd",null);
                if(taskbar==IntPtr.Zero)return fallback;
                AutomationElement bar=AutomationElement.FromHandle(taskbar);
                AutomationElement start=bar.FindFirst(TreeScope.Descendants,new OrCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty,"StartButton"),
                    new PropertyCondition(AutomationElement.NameProperty,"Start")));
                if(start!=null){System.Windows.Rect r=start.Current.BoundingRectangle;if(!r.IsEmpty&&r.Width>0)return (int)(r.Left+r.Width/2);}
            }
            catch { }
            return fallback;
        }
    }
}
