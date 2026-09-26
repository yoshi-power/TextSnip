using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace TextSnip {
 static class Native {
  [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);
  [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
  [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
  [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
 }
 static class Program {
  [STAThread] static void Main(string[] args) {
   try { if (!Native.SetProcessDpiAwarenessContext(new IntPtr(-4))) Native.SetProcessDPIAware(); } catch { Native.SetProcessDPIAware(); }
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   if (args.Length > 0 && args[0] == "--extract") { DirectText.Worker(args); return; }
   if (args.Contains("--direct-test")) { Application.Run(new DirectTextTests()); return; }
   if (args.Contains("--design-preview")) { DesignPreview.Run(); return; }
   if (args.Contains("--self-test")) { SelfTest.Run(); return; }
   bool created;
   using (var mutex = new Mutex(true, "Local\\TextSnip.Desktop.v1", out created)) {
    if (!created) { MessageBox.Show("TextSnip이 이미 실행 중입니다. 트레이 아이콘을 확인하세요.", "TextSnip"); return; }
    Application.Run(new MainWindow(false,args.Contains("--settings")));
   }
  }
 }
 sealed class LanguageItem {
  internal string Tag, Name;
  internal bool Available = true;
  internal LanguageItem(string tag, string name) { Tag = tag; Name = name; }
  public override string ToString() { return Name; }
 }
 static class OcrWorker {
  internal static Dictionary<string, object> Run(string input, string language, bool list) {
   string result = Path.Combine(Path.GetTempPath(), "textsnip-" + Guid.NewGuid().ToString("N") + ".json");
   try {
    string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ocr.ps1");
    if (!File.Exists(script)) throw new IOException("Ocr.ps1 파일이 없습니다. 앱 폴더에 함께 두세요.");
    string args = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(script) + " -OutputJson " + Quote(result);
    if (list) args += " -ListLanguages";
    else args += " -InputImage " + Quote(input) + " -Language " + Quote(language);
    var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe"), args);
    start.UseShellExecute = false; start.CreateNoWindow = true; start.RedirectStandardError = true;
    using (var process = Process.Start(start)) {
     var errors = process.StandardError.ReadToEndAsync();
     if (!process.WaitForExit(60000)) { try { process.Kill(); process.WaitForExit(5000); } catch { } throw new TimeoutException("글자 인식 시간이 초과되었습니다. 영역을 줄여 다시 시도하세요."); }
     if (!File.Exists(result)) throw new IOException("Windows OCR을 시작할 수 없습니다. " + errors.GetAwaiter().GetResult());
     var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(result, Encoding.UTF8));
     if (!(bool)data["ok"]) throw new InvalidOperationException(Convert.ToString(data["error"]));
     if (!list) { data["rawText"] = data["text"]; data["text"] = TextCleanup.Process(data); }
     return data;
    }
   } finally { try { File.Delete(result); } catch { } }
  }
  static string Quote(string value) { return "\"" + value.Replace("\"", "") + "\""; }
 }
 sealed class SelectionWindow : Form {
  readonly Bitmap screen; Point anchor, pointer; bool dragging; float dimProgress; string hint = "드래그하여 텍스트 선택"; readonly System.Windows.Forms.Timer entrance = new System.Windows.Forms.Timer { Interval = 15 };
  internal Rectangle Selected;
  internal static Rectangle Normalize(Point a, Point b, Size bounds) {
   int x1 = Math.Max(0, Math.Min(bounds.Width, Math.Min(a.X, b.X)));
   int y1 = Math.Max(0, Math.Min(bounds.Height, Math.Min(a.Y, b.Y)));
   int x2 = Math.Max(0, Math.Min(bounds.Width, Math.Max(a.X, b.X)));
   int y2 = Math.Max(0, Math.Min(bounds.Height, Math.Max(a.Y, b.Y)));
   return Rectangle.FromLTRB(x1, y1, x2, y2);
  }
  internal SelectionWindow(Bitmap frozen, Rectangle desktop) {
   screen = frozen; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
   AutoScaleMode = AutoScaleMode.None; Bounds = desktop; TopMost = true; ShowInTaskbar = false;
   DoubleBuffered = true; Cursor = Cursors.Cross; KeyPreview = true;
   AccessibleName = "텍스트를 인식할 화면 영역 선택"; entrance.Tick += delegate { dimProgress = Math.Min(1, dimProgress + .16f); Invalidate(); if (dimProgress >= 1) entrance.Stop(); };
  }
  protected override void OnShown(EventArgs e) { base.OnShown(e); Activate(); if (SystemInformation.IsMenuAnimationEnabled) entrance.Start(); else dimProgress = 1; }
  protected override void OnPaint(PaintEventArgs e) {
   e.Graphics.DrawImageUnscaled(screen,0,0);
   e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   using(var dim=new SolidBrush(Color.FromArgb((int)(105*dimProgress),15,25,45))) e.Graphics.FillRectangle(dim,ClientRectangle);
   Rectangle monitor=Screen.FromPoint(Cursor.Position).WorkingArea; monitor.Offset(-Left,-Top);
   monitor=Rectangle.Intersect(monitor,ClientRectangle); if(monitor.Width<200 || monitor.Height<100) monitor=ClientRectangle;
   Rectangle r=Normalize(anchor,pointer,screen.Size);
   if(dragging && r.Width>0 && r.Height>0) {
    e.Graphics.DrawImage(screen,r,r,GraphicsUnit.Pixel);
    using(var white=new Pen(Color.White,4)) e.Graphics.DrawRectangle(white,r);
    using(var blue=new Pen(Design.Blue,2)) e.Graphics.DrawRectangle(blue,r);
    foreach(Point corner in new[] { r.Location,new Point(r.Right,r.Top),new Point(r.Left,r.Bottom),new Point(r.Right,r.Bottom) }) {
     using(var brush=new SolidBrush(Design.Blue)) e.Graphics.FillRectangle(brush,corner.X-3,corner.Y-3,6,6);
    }
    DrawLabel(e.Graphics,"놓으면 자동 복사   ·   "+r.Width+" × "+r.Height,r.X,r.Bottom+12,monitor);
   } else {
    Point p=PointToClient(Cursor.Position);
    using(var white=new Pen(Color.White,3)) { e.Graphics.DrawLine(white,p.X-9,p.Y,p.X+9,p.Y); e.Graphics.DrawLine(white,p.X,p.Y-9,p.X,p.Y+9); }
    using(var blue=new Pen(Design.Blue,1)) { e.Graphics.DrawLine(blue,p.X-9,p.Y,p.X+9,p.Y); e.Graphics.DrawLine(blue,p.X,p.Y-9,p.X,p.Y+9); }
   }
   string instruction="TextSnip   /   "+hint+"     ·     Esc 취소";
   using(var font=new Font("Malgun Gothic",10,FontStyle.Bold)) DrawLabel(e.Graphics,instruction,monitor.X+(monitor.Width-TextRenderer.MeasureText(instruction,font).Width-32)/2,monitor.Y+24,monitor);
  }
  void DrawLabel(Graphics g,string text,int x,int y,Rectangle bounds) {
   using(var font=new Font("Malgun Gothic",10,FontStyle.Bold)) {
    Size size=TextRenderer.MeasureText(text,font); int width=size.Width+32,height=size.Height+22;
    x=Math.Max(bounds.Left+8,Math.Min(bounds.Right-width-8,x)); y=Math.Max(bounds.Top+8,Math.Min(bounds.Bottom-height-8,y));
    using(var shadow=Design.Round(new RectangleF(x,y+3,width,height),12)) using(var b=new SolidBrush(Color.FromArgb(30,0,0,0))) g.FillPath(b,shadow);
    using(var path=Design.Round(new RectangleF(x,y,width,height),12)) using(var b=new SolidBrush(Color.White)) g.FillPath(b,path);
    TextRenderer.DrawText(g,text,font,new Rectangle(x+16,y+10,width-32,height-20),Design.Blue,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
   }
  }
  internal void PreviewSelection() { anchor=new Point(170,180); pointer=new Point(750,390); dragging=true; dimProgress=1; }
  protected override void Dispose(bool disposing) { if(disposing) entrance.Dispose(); base.Dispose(disposing); }
  protected override void OnMouseDown(MouseEventArgs e) {
   if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; Close(); return; }
   if (e.Button != MouseButtons.Left) return;
   hint = "드래그하여 텍스트 선택"; anchor = pointer = e.Location; dragging = true; Capture = true; Invalidate();
  }
  protected override void OnMouseMove(MouseEventArgs e) { pointer = e.Location; Invalidate(); }
  protected override void OnMouseUp(MouseEventArgs e) {
   if (!dragging || e.Button != MouseButtons.Left) return;
   Selected = Normalize(anchor, e.Location, screen.Size); dragging = false; Capture = false;
   if (Selected.Width < 5 || Selected.Height < 5) { hint = "영역을 조금 더 크게 선택해 주세요"; Invalidate(); return; }
   DialogResult = DialogResult.OK; Close();
  }
  protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } base.OnKeyDown(e); }
 }
 sealed class MainWindow : Form {
  readonly Label status = new Label();
  StatusToast toast;
  readonly ComboBox languages = new ComboBox(); readonly ComboBox hotkeys = new ComboBox();
  readonly Button snip = new ActionButton(); readonly NotifyIcon tray;
  readonly CheckBox readable = new Toggle(); bool preferredReadable = true;
  readonly CheckBox sourceFirst = new Toggle(); bool preferredSourceFirst = true;
  bool busy, exiting, ready, changingHotkey; int activeHotkey = -1; string preferredLanguage = "ko";
  int preferredHotkey = 3; readonly Label shortcutHint = new Label(); readonly System.Windows.Forms.Timer revealAnimation = new System.Windows.Forms.Timer { Interval = 15 };
  readonly string settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TextSnip", "settings.txt");
  static readonly string[] ShortcutNames = { "Ctrl + Alt + T", "Ctrl + Shift + F8", "Alt + Shift + T", "Ctrl + Shift + Q", "Ctrl + Shift + A", "Ctrl + Alt + Q" };
  static readonly uint[] Modifiers = { 3, 6, 5, 6, 6, 3 }; static readonly uint[] KeyCodes = { 0x54, 0x77, 0x54, 0x51, 0x41, 0x51 };
  internal MainWindow(bool preview = false, bool openSettings = false) {
   Text = "TextSnip · 설정"; ClientSize = new Size(820, 740); MinimumSize = new Size(760, 620);
   StartPosition = FormStartPosition.CenterScreen; Font = new Font("Malgun Gothic", 10); BackColor = Color.White;
   Icon = Design.CreateIcon(); AutoScaleMode = AutoScaleMode.Dpi;
   ReadSettings();
   var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
   var layout = new TableLayoutPanel { Dock = DockStyle.Top, Height = 720, Padding = new Padding(32,24,32,20), ColumnCount = 1, RowCount = 7, BackColor = Color.White };
   foreach (int height in new[] { 52, 155, 24, 156, 20, 190, 78 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
   var header = new Panel { Dock = DockStyle.Fill };
   var logo = Design.Label("▣",22,Design.Blue,true); logo.Location=new Point(0,0); header.Controls.Add(logo);
   var brand = Design.Label("TextSnip",19,Design.Ink,true); brand.Location=new Point(40,0); header.Controls.Add(brand);
   var badge = Design.Label("ON YOUR DEVICE",9,Design.Blue,true); header.SizeChanged+=delegate { badge.Location=new Point(Math.Max(200,header.Width-badge.Width),12); }; header.Controls.Add(badge); layout.Controls.Add(header,0,0);
   var hero = new SoftCard { Dock = DockStyle.Fill, BackColor = Design.Pale };
   var title = Design.Label("보이는 글자를, 내 텍스트로.",20,Design.Ink,true); title.Location=new Point(24,22); hero.Controls.Add(title);
   var subtitle = Design.Label("드래그 한 번으로 복사하고, 원하는 곳에 붙여넣으세요.",10,Design.Muted,false); subtitle.Location=new Point(26,78); hero.Controls.Add(subtitle);
   shortcutHint.Text=ShortcutNames[preferredHotkey]+"   →   영역 선택   →   Ctrl + V"; shortcutHint.Font=new Font("Segoe UI",11,FontStyle.Bold); shortcutHint.ForeColor=Design.Blue; shortcutHint.AutoSize=true; shortcutHint.Location=new Point(26,112); hero.Controls.Add(shortcutHint); layout.Controls.Add(hero,0,1);
   var settings = new SoftCard { Dock=DockStyle.Fill };
   var columns = new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=3 };
   columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   columns.RowStyles.Add(new RowStyle(SizeType.Absolute,27)); columns.RowStyles.Add(new RowStyle(SizeType.Absolute,39)); columns.RowStyles.Add(new RowStyle(SizeType.Absolute,25));
   columns.Controls.Add(Design.Label("캡처 단축키",11,Design.Ink,true),0,0); columns.Controls.Add(Design.Label("이미지 인식 언어",11,Design.Ink,true),1,0);
   Design.Combo(hotkeys); hotkeys.Items.AddRange(ShortcutNames); hotkeys.SelectedIndex=preferredHotkey; hotkeys.AccessibleName="캡처 단축키"; hotkeys.Margin=Padding.Empty; columns.Controls.Add(hotkeys,0,1);
   Design.Combo(languages); languages.Enabled=false; languages.AccessibleName="이미지 인식 언어"; languages.Margin=Padding.Empty; columns.Controls.Add(languages,1,1);
   columns.Controls.Add(Design.Label("다른 앱과 겹치면 바로 알려드려요.",9,Design.Muted,false),0,2); columns.Controls.Add(Design.Label("앱 원문은 언어에 관계없이 복사해요.",9,Design.Muted,false),1,2);
   settings.Controls.Add(columns); layout.Controls.Add(settings,0,3);
   var preferences = new SoftCard { Dock=DockStyle.Fill };
   var prefTitle=Design.Label("복사 방식",11,Design.Ink,true); prefTitle.Location=new Point(24,18); preferences.Controls.Add(prefTitle);
   AddPreference(preferences,sourceFirst,"원문 그대로, 더 정확하게","앱의 실제 텍스트를 먼저 읽고, 없으면 이미지에서 인식해요.",54);
   AddPreference(preferences,readable,"여러 줄도 읽기 편하게","이미지 인식 결과의 줄을 연결하고 장식 기호를 정리해요.",119);
   sourceFirst.Checked=preferredSourceFirst; readable.Checked=preferredReadable; layout.Controls.Add(preferences,0,5);
   var footer = new Panel { Dock=DockStyle.Fill, Padding=new Padding(0,16,0,0) };
   snip.Text="영역 선택 시작  ↗"; snip.Size=new Size(205,46); snip.Location=new Point(0,18); footer.Controls.Add(snip);
   status.Text="인식 기능을 준비하고 있어요…"; status.Font=new Font("Malgun Gothic",9); status.ForeColor=Design.Muted; status.AutoEllipsis=true; status.Location=new Point(223,20); status.Size=new Size(500,22); footer.Controls.Add(status);
   var privacy=Design.Label("화면과 텍스트는 이 PC 안에서만 처리됩니다.",9,Design.Muted,false); privacy.Location=new Point(223,46); footer.Controls.Add(privacy); layout.Controls.Add(footer,0,6);
   scroll.Controls.Add(layout); Controls.Add(scroll);
   snip.Enabled=false;
   var menu=new ContextMenuStrip { Font = new Font("Malgun Gothic",10), BackColor=Color.White, ShowImageMargin=false };
   menu.Items.Add("영역 선택",null,async delegate { await CaptureText(); }); menu.Items.Add("설정 열기",null,delegate { Reveal(); }); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("TextSnip 종료",null,delegate { exiting=true; Close(); });
   tray=new NotifyIcon { Icon=Icon,Text="TextSnip",ContextMenuStrip=menu,Visible=!preview };
   tray.DoubleClick+=delegate { Reveal(); };
   snip.Click+=async delegate { await CaptureText(); };
   hotkeys.SelectedIndexChanged+=delegate { if(ready && !changingHotkey) SetHotkey(hotkeys.SelectedIndex); };
   languages.SelectedIndexChanged+=delegate { if(ready) WriteSettings(); };
   readable.CheckedChanged+=delegate { if(ready) WriteSettings(); }; sourceFirst.CheckedChanged+=delegate { if(ready) WriteSettings(); };
   revealAnimation.Tick+=delegate { Opacity=Math.Min(1,Opacity+.14); if(Opacity>=1) revealAnimation.Stop(); };
   if(preview) { languages.Items.Add(new LanguageItem("ko","한국어")); languages.SelectedIndex=0; languages.Enabled=true; snip.Enabled=true; status.Text="Ctrl + Shift + Q · 바로 캡처할 수 있어요"; shortcutHint.Text="Ctrl + Shift + Q   →   영역 선택   →   Ctrl + V"; hotkeys.SelectedIndex=3; }
   else Shown+=async delegate { Hide(); await Initialize(); if(openSettings && !IsDisposed) Reveal(); };
  }
  void AddPreference(Panel panel,CheckBox toggle,string title,string description,int y) {
   var heading=Design.Label(title,10,Design.Ink,true); heading.Location=new Point(24,y); panel.Controls.Add(heading);
   var detail=Design.Label(description,9,Design.Muted,false); detail.Location=new Point(24,y+27); panel.Controls.Add(detail);
   toggle.Text=title; toggle.AccessibleName=title; toggle.AutoSize=false; toggle.Size=new Size(54,30); toggle.Location=new Point(650,y+6); panel.SizeChanged+=delegate { toggle.Location=new Point(panel.Width-78,y+6); }; panel.Controls.Add(toggle);
  }
  async Task Initialize() {
   SetHotkey(preferredHotkey); if (activeHotkey < 0) foreach (int fallback in new[] { 3, 4, 5, 0, 1, 2 }) { SetHotkey(fallback); if (activeHotkey >= 0) break; }
   try {
    var data = await Task.Run(() => OcrWorker.Run(null, null, true));
    if (IsDisposed) return;
    var installed = ((System.Collections.IEnumerable)data["languages"]).Cast<Dictionary<string, object>>().Select(x => (string)x["tag"]).ToArray();
    string[] codes = { "ko", "en", "ja" }; string[] names = { "한국어", "영어", "일본어" };
    for (int i = 0; i < codes.Length; i++) {
     string tag = installed.FirstOrDefault(x => x == codes[i] || x.StartsWith(codes[i] + "-"));
     var item = new LanguageItem(tag ?? codes[i], names[i] + (tag == null ? " (OCR 설치 필요)" : ""));
     item.Available = tag != null;
     languages.Items.Add(item);
     if (preferredLanguage == codes[i] || preferredLanguage.StartsWith(codes[i] + "-")) languages.SelectedItem = item;
    }
    if (languages.SelectedIndex < 0) languages.SelectedIndex = 0;
    bool hasOcr = installed.Any(x => codes.Any(c => x == c || x.StartsWith(c + "-")));
    languages.Enabled = true; snip.Enabled = true;
    status.Text = activeHotkey >= 0 ? ShortcutNames[activeHotkey] + "로 화면의 글자를 선택하세요." : "단축키가 사용 중입니다. 다른 단축키를 선택하세요.";
    if (!hasOcr) status.Text = "원문 복사 가능 · 이미지 OCR은 Install-OCR.cmd로 설치하세요.";
   } catch (Exception ex) { if (!IsDisposed) { Notify("OCR 준비 실패: " + ex.Message, true); Reveal(); } }
   ready = true;
   if (snip.Enabled) WriteSettings();
   if (activeHotkey < 0) Reveal();
  }
  void SetHotkey(int index) {
   if (index < 0 || index >= ShortcutNames.Length || activeHotkey == index) return;
   if (!Native.RegisterHotKey(Handle, 800 + index, Modifiers[index] | 0x4000, KeyCodes[index])) {
    status.Text = "이 단축키는 다른 앱에서 사용 중입니다. 다른 단축키를 선택하세요.";
    changingHotkey = true; hotkeys.SelectedIndex = activeHotkey; changingHotkey = false; return;
   }
   if (activeHotkey >= 0) Native.UnregisterHotKey(Handle, 800 + activeHotkey);
   activeHotkey = index; changingHotkey = true; hotkeys.SelectedIndex = index; changingHotkey = false; shortcutHint.Text = ShortcutNames[index] + "   →   영역 선택   →   Ctrl + V"; status.Text = ShortcutNames[index] + " · 준비됨";
   tray.Text = "TextSnip · " + ShortcutNames[index]; if (ready) WriteSettings();
  }
  protected override void WndProc(ref Message m) {
   if (m.Msg == 0x0312 && !busy) BeginInvoke(new Action(async delegate { await CaptureText(); }));
   base.WndProc(ref m);
  }
  async Task CaptureText() {
   if (busy || !ready || !snip.Enabled) return;
   busy = true; bool wasVisible = Visible; bool selected = false; string imagePath = null;
   Bitmap original = null; Rectangle selectedArea = Rectangle.Empty;
   IntPtr previousWindow = Native.GetForegroundWindow();
   string language = ((LanguageItem)languages.SelectedItem).Tag;
   bool cleanText = readable.Checked;
   bool useSource = sourceFirst.Checked;
   snip.Enabled = false; languages.Enabled = false; hotkeys.Enabled = false;
   try {
    if (toast != null && !toast.IsDisposed) toast.Close();
    revealAnimation.Stop(); Hide(); Opacity=1; await Task.Delay(220);
    if (IsDisposed) return;
    Rectangle desktop = SystemInformation.VirtualScreen;
    using (var screenshot = new Bitmap(desktop.Width, desktop.Height, PixelFormat.Format32bppArgb)) {
     using (var g = Graphics.FromImage(screenshot)) g.CopyFromScreen(desktop.Location, Point.Empty, desktop.Size, CopyPixelOperation.SourceCopy);
     using (var selection = new SelectionWindow(screenshot, desktop)) {
      if (selection.ShowDialog() != DialogResult.OK) return;
      selected = true; Rectangle area = selection.Selected;
      selectedArea = new Rectangle(desktop.X + area.X, desktop.Y + area.Y, area.Width, area.Height);
      original = screenshot.Clone(area, PixelFormat.Format32bppArgb);
      imagePath = Path.Combine(Path.GetTempPath(), "textsnip-" + Guid.NewGuid().ToString("N") + ".png");
      using (var crop = screenshot.Clone(area, PixelFormat.Format32bppArgb)) {
       double scale = Math.Min(2.0, 2500.0 / Math.Max(crop.Width, crop.Height));
       using (var scaled = new Bitmap(Math.Max(1, (int)(crop.Width * scale)), Math.Max(1, (int)(crop.Height * scale)), PixelFormat.Format32bppArgb)) {
        using (var g = Graphics.FromImage(scaled)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(crop, new Rectangle(Point.Empty, scaled.Size)); }
        scaled.Save(imagePath, ImageFormat.Png);
       }
      }
     }
    }
    if (!wasVisible && previousWindow != IntPtr.Zero) Native.SetForegroundWindow(previousWindow);
    await Task.Delay(80);
    if (IsDisposed) return;
    status.Text = "원문 텍스트를 확인하고 있습니다…"; tray.Text = "TextSnip · 원문 확인 중";
    string text = null;
    if (useSource && DirectText.ScreenUnchanged(original, selectedArea)) {
     text = await Task.Run(() => DirectText.Read(selectedArea));
     if (IsDisposed) return;
     if (!DirectText.ScreenUnchanged(original, selectedArea)) text = null;
    }
    bool direct = !String.IsNullOrWhiteSpace(text);
    if (!direct) {
     if (!((LanguageItem)languages.SelectedItem).Available) { Notify("원문을 읽을 수 없고 선택한 언어의 OCR도 없습니다. Windows 언어 옵션에서 OCR을 설치하세요.", true); return; }
     status.Text = "이미지의 글자를 인식하고 있습니다…"; tray.Text = "TextSnip · OCR 인식 중";
     ShowToast("완료되면 알려드릴게요. 잠시만 기다려 주세요.",false,true);
     string path = imagePath; var result = await Task.Run(() => OcrWorker.Run(path, language, false));
     if (IsDisposed) return;
     text = Convert.ToString(result[cleanText ? "text" : "rawText"]);
    }
    if (String.IsNullOrWhiteSpace(text)) { Notify("글자를 찾지 못했습니다. 영역이나 기본 인식 언어를 바꿔 보세요. 기존 클립보드는 유지됩니다.", true); return; }
    Clipboard.SetDataObject(text, true, 10, 100);
    Notify((direct ? "원문 복사 완료 · " : "OCR 복사 완료 · ") + text.Length.ToString("N0") + "자 · Ctrl + V로 붙여넣으세요.", false);
   } catch (Exception ex) { if (!IsDisposed) Notify("인식 또는 복사 실패: " + ex.Message, true); }
   finally {
    if (original != null) original.Dispose();
    if (imagePath != null) { try { File.Delete(imagePath); } catch { } }
    busy = false;
    if (!IsDisposed) { snip.Enabled = true; languages.Enabled = true; hotkeys.Enabled = true; tray.Text = "TextSnip" + (activeHotkey >= 0 ? " · " + ShortcutNames[activeHotkey] : ""); if (wasVisible && !selected) Reveal(); }
   }
  }
  void Reveal() { if (!Visible && SystemInformation.IsMenuAnimationEnabled) { Opacity = 0; revealAnimation.Start(); } else Opacity = 1; Show(); WindowState = FormWindowState.Normal; Activate(); }
  void Notify(string message, bool warning) {
   status.Text = message;
   ShowToast(message,warning,false);
  }
  void ShowToast(string message,bool warning,bool working) { if(toast!=null && !toast.IsDisposed) toast.Close(); toast=new StatusToast(message,warning,working); toast.Show(); }
  void ReadSettings() {
   try { var lines = File.ReadAllLines(settingsFile); preferredLanguage = lines[0]; int n; if (Int32.TryParse(lines[1], out n) && n >= 0 && n < ShortcutNames.Length) preferredHotkey = n; if (preferredLanguage == "auto") preferredLanguage = "ko"; preferredReadable = lines.Length < 4 || lines[3] != "false"; preferredSourceFirst = lines.Length < 5 || lines[4] != "false"; if (lines.Length < 6 && preferredHotkey == 0) preferredHotkey = 3; } catch { }
  }
  void WriteSettings() {
   try { Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)); File.WriteAllLines(settingsFile, new[] { languages.SelectedItem == null ? preferredLanguage : ((LanguageItem)languages.SelectedItem).Tag, Math.Max(0, activeHotkey).ToString(), "true", readable.Checked ? "true" : "false", sourceFirst.Checked ? "true" : "false", "design-v2" }, Encoding.UTF8); }
   catch { status.Text = "설정을 저장할 수 없습니다. 현재 세션에는 적용됩니다."; }
  }
  protected override void OnFormClosing(FormClosingEventArgs e) {
   if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); tray.ShowBalloonTip(2500, "TextSnip이 실행 중입니다", "단축키로 영역을 선택하세요. 종료는 트레이 메뉴에서 할 수 있습니다.", ToolTipIcon.Info); }
   else { WriteSettings(); if (activeHotkey >= 0) Native.UnregisterHotKey(Handle, 800 + activeHotkey); if(toast!=null) toast.Dispose(); revealAnimation.Dispose(); tray.Visible = false; tray.Dispose(); }
   base.OnFormClosing(e);
  }
 }
 static class SelfTest {
  internal static void Run() {
   var log = new StringBuilder(); string path = Path.Combine(Path.GetTempPath(), "textsnip-test-" + Guid.NewGuid().ToString("N") + ".png");
   try {
    CleanupTests.Run(log);
    if (SelectionWindow.Normalize(new Point(90, 80), new Point(10, 20), new Size(100, 100)) != new Rectangle(10, 20, 80, 60)) throw new Exception("Reverse selection failed");
    if (SelectionWindow.Normalize(new Point(-10, -20), new Point(150, 200), new Size(100, 100)) != new Rectangle(0, 0, 100, 100)) throw new Exception("Bounds clamp failed");
    log.AppendLine("PASS: selection geometry");
    string txt = path + ".txt";
    try { File.WriteAllText(txt, "한글 UTF-8 테스트", new UTF8Encoding(true)); if (File.ReadAllText(txt, Encoding.UTF8) != "한글 UTF-8 테스트") throw new Exception("UTF-8 roundtrip failed"); log.AppendLine("PASS: UTF-8 save"); } finally { File.Delete(txt); }
    var langs = OcrWorker.Run(null, null, true); var tags = ((System.Collections.IEnumerable)langs["languages"]).Cast<Dictionary<string, object>>().Select(x => (string)x["tag"]).ToArray();
    log.AppendLine("Installed OCR languages: " + String.Join(", ", tags));
    if (tags.Length == 0) throw new Exception("No OCR languages installed");
    string language = tags.FirstOrDefault(x => x.StartsWith("en")) ?? tags.FirstOrDefault(x => x.StartsWith("ko")) ?? tags[0];
    using (var bitmap = new Bitmap(1100, 200)) { using (var g = Graphics.FromImage(bitmap)) using (var font = new Font("Arial", 38)) { g.Clear(Color.White); g.DrawString("TEXT SNIP 12345", font, Brushes.Black, 25, 35); } bitmap.Save(path, ImageFormat.Png); }
    string english = Convert.ToString(OcrWorker.Run(path, language, false)["text"]); log.AppendLine("English OCR: " + english);
    if (!english.Contains("12345") || !english.ToUpperInvariant().Contains("TEXT")) throw new Exception("English OCR assertion failed"); log.AppendLine("PASS: English OCR");
    string korean = tags.FirstOrDefault(x => x.StartsWith("ko"));
    if (korean != null) {
     using (var bitmap = new Bitmap(1100, 200)) { using (var g = Graphics.FromImage(bitmap)) using (var font = new Font("Malgun Gothic", 38)) { g.Clear(Color.White); g.DrawString("안녕하세요 텍스트 인식 12345", font, Brushes.Black, 25, 35); } bitmap.Save(path, ImageFormat.Png); }
     string text = Convert.ToString(OcrWorker.Run(path, korean, false)["text"]); log.AppendLine("Korean OCR: " + text);
     if (!text.Replace(" ", "").Contains("안녕하세요")) throw new Exception("Korean OCR assertion failed"); log.AppendLine("PASS: Korean OCR");
    } else log.AppendLine("SKIP: Korean OCR language is not installed");
    string japanese = tags.FirstOrDefault(x => x.StartsWith("ja"));
    if (japanese != null) {
     using (var bitmap = new Bitmap(1300, 200)) { using (var g = Graphics.FromImage(bitmap)) using (var font = new Font("Yu Gothic", 38)) { g.Clear(Color.White); g.DrawString("こんにちは 日本語 12345", font, Brushes.Black, 25, 35); } bitmap.Save(path, ImageFormat.Png); }
     string text = Convert.ToString(OcrWorker.Run(path, japanese, false)["text"]); log.AppendLine("Japanese OCR: " + text);
     if (!text.Replace(" ", "").Contains("こんにちは") || !text.Contains("12345")) throw new Exception("Japanese OCR assertion failed"); log.AppendLine("PASS: Japanese OCR");
    } else log.AppendLine("SKIP: Japanese OCR language is not installed");
    string englishTag = tags.FirstOrDefault(x => x.StartsWith("en"));
    if (englishTag != null) {
     using (var bitmap = new Bitmap(1500, 220)) { using (var g = Graphics.FromImage(bitmap)) using (var font = new Font("Consolas", 38)) { g.Clear(Color.White); g.DrawString("foo = 100; (open) [0] + 5", font, Brushes.Black, 25, 35); } bitmap.Save(path, ImageFormat.Png); }
     string text = Convert.ToString(OcrWorker.Run(path, englishTag, false)["text"]); log.AppendLine("English symbols OCR: " + text);
     if (!text.Contains("foo") || !text.Contains("100") || !text.Contains("open")) throw new Exception("English code words/numbers assertion failed");
     log.AppendLine("PASS: dedicated English OCR words and numbers");
     string compact = String.Concat(text.Where(c => !Char.IsWhiteSpace(c)));
     if (compact == "foo=100;(open)[0]+5") log.AppendLine("PASS: exact symbols and zero sample");
     else log.AppendLine("KNOWN LIMITATION: expected foo=100;(open)[0]+5; got " + compact + ". Windows OCR can still confuse 0/O or omit symbols; no automatic substitutions applied.");
    } else log.AppendLine("SKIP: dedicated English OCR language is not installed");
    if (englishTag != null) {
     using (var bitmap = new Bitmap(1100, 190)) {
      using (var g = Graphics.FromImage(bitmap)) using (var font = new Font("Arial", 24)) {
       g.Clear(Color.White);
       g.DrawString("This is a simple sentence", font, Brushes.Black, 25, 20);
       g.DrawString("with 100 words and clear text.", font, Brushes.Black, 25, 65);
      }
      bitmap.Save(path, ImageFormat.Png);
     }
     var paragraph = OcrWorker.Run(path, englishTag, false);
     string text = Convert.ToString(paragraph["text"]); log.AppendLine("Multiline prose OCR: " + text);
     if (text != "This is a simple sentence with 100 words and clear text.") throw new Exception("Real OCR paragraph reflow failed");
     if (!Convert.ToString(paragraph["rawText"]).Contains("\n")) throw new Exception("Raw OCR fallback did not preserve lines");
     log.AppendLine("PASS: real OCR line geometry, paragraph reflow and original text fallback");
    }
    using (var bitmap = new Bitmap(400, 120)) { using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.White); bitmap.Save(path, ImageFormat.Png); }
    if (!String.IsNullOrWhiteSpace(Convert.ToString(OcrWorker.Run(path, language, false)["text"]))) throw new Exception("Blank image assertion failed");
    log.AppendLine("PASS: blank image");
    log.AppendLine("SELF TEST PASSED");
   } catch (Exception ex) { log.AppendLine("FAIL: " + ex.ToString()); Environment.ExitCode = 1; }
   finally { try { File.Delete(path); } catch { } File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test.txt"), log.ToString(), Encoding.UTF8); }
  }
 }
}
