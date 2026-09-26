using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace TextSnip {
 static class Design {
  [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
  internal static Icon CreateIcon() {
   using(var image=new Bitmap(32,32)) using(var g=Graphics.FromImage(image)) {
    g.SmoothingMode=SmoothingMode.AntiAlias;
    using(var p=Round(new RectangleF(0,0,32,32),8)) using(var b=new SolidBrush(Blue)) g.FillPath(b,p);
    using(var pen=new Pen(Color.White,2)) { g.DrawLine(pen,8,14,8,8); g.DrawLine(pen,8,8,14,8); g.DrawLine(pen,24,18,24,24); g.DrawLine(pen,24,24,18,24); g.DrawLine(pen,12,14,22,14); g.DrawLine(pen,12,18,20,18); }
    IntPtr handle=image.GetHicon(); try { using(var borrowed=Icon.FromHandle(handle)) return (Icon)borrowed.Clone(); } finally { DestroyIcon(handle); }
   }
  }
  internal static readonly Color Blue = ColorTranslator.FromHtml("#2B59C3"), Ink = ColorTranslator.FromHtml("#17243D"), Muted = ColorTranslator.FromHtml("#63718A"), Pale = ColorTranslator.FromHtml("#EFF4FF"), Border = ColorTranslator.FromHtml("#E4EAF3");
  internal static GraphicsPath Round(RectangleF r, float radius) {
   var p = new GraphicsPath(); float d = radius * 2;
   p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right-d, r.Y, d, d, 270, 90);
   p.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90); p.AddArc(r.X, r.Bottom-d, d, d, 90, 90); p.CloseFigure(); return p;
  }
  internal static Label Label(string text, float size, Color color, bool bold) { return new Label { Text = text, AutoSize = true, ForeColor = color, Font = new Font("Malgun Gothic", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0) }; }
  internal static void Combo(ComboBox box) { box.DropDownStyle = ComboBoxStyle.DropDownList; box.FlatStyle = FlatStyle.Flat; box.BackColor = Pale; box.ForeColor = Ink; box.Font = new Font("Malgun Gothic", 11); box.Width = 235; box.Margin = new Padding(0, 12, 0, 0); }
 }
 sealed class SoftCard : Panel {
  internal SoftCard() { DoubleBuffered = true; BackColor = Color.White; Padding = new Padding(24); }
  protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var p = Design.Round(new RectangleF(0.5f,0.5f,Width-2,Height-2),16)) using (var pen = new Pen(Design.Border)) e.Graphics.DrawPath(pen,p); }
 }
 sealed class ActionButton : Button {
  readonly Timer animation = new Timer { Interval = 15 }; float hover; bool over;
  internal ActionButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; BackColor = Color.White; ForeColor = Color.White; Font = new Font("Malgun Gothic",11,FontStyle.Bold); animation.Tick += delegate { hover += ((over ? 1f : 0f)-hover)*0.3f; if (Math.Abs(hover-(over?1f:0f)) < .02f) animation.Stop(); Invalidate(); }; }
  protected override void OnMouseEnter(EventArgs e) { over=true; if(SystemInformation.IsMenuAnimationEnabled) animation.Start(); else { hover=1; Invalidate(); } base.OnMouseEnter(e); }
  protected override void OnMouseLeave(EventArgs e) { over=false; if(SystemInformation.IsMenuAnimationEnabled) animation.Start(); else { hover=0; Invalidate(); } base.OnMouseLeave(e); }
  protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; Color c = Enabled ? Color.FromArgb((int)(43-12*hover),(int)(89-16*hover),(int)(195-22*hover)) : Color.FromArgb(157,174,210); using(var p=Design.Round(new RectangleF(0,0,Width-1,Height-1),12)) using(var b=new SolidBrush(c)) e.Graphics.FillPath(b,p); TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter); if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(5,5,Width-10,Height-10),Color.White,c); }
  protected override void Dispose(bool disposing) { if(disposing) animation.Dispose(); base.Dispose(disposing); }
 }
 sealed class Toggle : CheckBox {
  readonly Timer animation = new Timer { Interval = 15 }; float position;
  internal Toggle() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint,true); AutoSize=false; Size=new Size(54,30); Cursor=Cursors.Hand; animation.Tick += delegate { position += ((Checked?1f:0f)-position)*.3f; if(Math.Abs(position-(Checked?1f:0f))<.02f) { position=Checked?1:0; animation.Stop(); } Invalidate(); }; }
  protected override void OnCheckedChanged(EventArgs e) { if(!IsHandleCreated || !SystemInformation.IsMenuAnimationEnabled) { position=Checked?1:0; Invalidate(); } else animation.Start(); base.OnCheckedChanged(e); }
  protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; float scale=Height/30f; using(var p=Design.Round(new RectangleF(1,3*scale,Width-2,24*scale),12*scale)) using(var b=new SolidBrush(Enabled ? (Checked?Design.Blue:Color.FromArgb(185,195,212)) : Design.Border)) e.Graphics.FillPath(b,p); using(var b=new SolidBrush(Color.White)) e.Graphics.FillEllipse(b,5*scale+position*(Width-30*scale),6*scale,18*scale,18*scale); if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle); }
  protected override void Dispose(bool disposing) { if(disposing) animation.Dispose(); base.Dispose(disposing); }
 }
 sealed class StatusToast : Form {
  readonly Timer clock = new Timer { Interval = 16 }; readonly System.Diagnostics.Stopwatch time = new System.Diagnostics.Stopwatch();
  readonly int duration; readonly int finalY; readonly bool motion;
  protected override bool ShowWithoutActivation { get { return true; } }
  protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=0x08000000|0x00000080; return p; } }
  internal StatusToast(string message,bool warning,bool working) {
   FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true; StartPosition=FormStartPosition.Manual; BackColor=Color.White; DoubleBuffered=true;
   ClientSize=new Size(390,112); duration=working?65000:(warning?6000:2800); motion=SystemInformation.IsMenuAnimationEnabled;
   Rectangle area=Screen.FromPoint(Cursor.Position).WorkingArea; finalY=area.Bottom-Height-24; Location=new Point(area.Right-Width-24,finalY);
   var heading=Design.Label(working?"TextSnip · 텍스트를 읽고 있어요":warning?"TextSnip · 확인해 주세요":"TextSnip · 복사 완료",10,warning?Color.FromArgb(155,91,18):Design.Blue,true); heading.Location=new Point(20,15); Controls.Add(heading);
   var body=new Label { Text=message,Font=new Font("Malgun Gothic",9),ForeColor=Design.Ink,Location=new Point(20,43),Size=new Size(350,55),AutoEllipsis=true }; Controls.Add(body);
   clock.Tick+=delegate { double t=time.Elapsed.TotalMilliseconds; if(motion) { Opacity=Math.Min(1,t/150); Top=finalY+(int)(8*(1-Math.Min(1,t/150))); if(t>duration-180) Opacity=Math.Max(0,(duration-t)/180); } if(t>=duration) Close(); };
  }
  protected override void OnShown(EventArgs e) { base.OnShown(e); if(motion) Opacity=0; time.Start(); clock.Start(); }
  protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using(var p=new Pen(Design.Border)) e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1); using(var b=new SolidBrush(Design.Blue)) e.Graphics.FillRectangle(b,0,0,4,Height); }
  protected override void Dispose(bool disposing) { if(disposing) clock.Dispose(); base.Dispose(disposing); }
 }
}
