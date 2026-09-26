using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TextSnip {
 sealed class DirectTextTests : Form {
  readonly RichTextBox document = new RichTextBox();
  readonly PictureBox picture = new PictureBox();
  readonly StringBuilder log = new StringBuilder();
  const string Sample = "c0mmon o0O 100 = [0] + 5\n한국어 원문 보존\n日本語テキスト";
  internal DirectTextTests() {
   Text = "TextSnip 원문 추출 자체 테스트 (자동 종료)"; ClientSize = new Size(700, 300);
   StartPosition = FormStartPosition.CenterScreen; TopMost = true;
   document.Text = Sample; document.Font = new Font("Malgun Gothic", 16); document.Bounds = new Rectangle(10, 10, 680, 190);
   picture.Bounds = new Rectangle(10, 215, 680, 70); picture.BackColor = Color.LightGray;
   Controls.Add(document); Controls.Add(picture);
   Shown += async delegate {
    try {
     Show(); WindowState = FormWindowState.Normal; Activate(); BringToFront(); picture.Focus(); await Task.Delay(500);
     Rectangle area = document.RectangleToScreen(document.ClientRectangle); area.Inflate(-2, -2);
     string text = await Task.Run(() => DirectText.Read(area));
     log.AppendLine("Direct text: " + text);
     log.AppendLine("Provider diagnostic: " + DirectText.LastReason);
     if ((text ?? "").Replace("\r\n", "\n") != Sample) throw new Exception("Exact source extraction failed");
     log.AppendLine("PASS: actual UI Automation extraction preserves ko/ja/en, 0/o/O, symbols and source spelling");
     Point lineStart = document.GetPositionFromCharIndex(Sample.IndexOf("한국어"));
     Point nextStart = document.GetPositionFromCharIndex(Sample.IndexOf("日本語"));
     Rectangle lineArea = document.RectangleToScreen(new Rectangle(2, lineStart.Y, document.ClientSize.Width - 4, nextStart.Y - lineStart.Y));
     string selectedLine = await Task.Run(() => DirectText.Read(lineArea));
     if (selectedLine != "한국어 원문 보존") throw new Exception("Selected line boundary failed: " + selectedLine + " / " + DirectText.LastReason);
     log.AppendLine("PASS: selected line only; neighbouring text excluded");
     Rectangle imageArea = picture.RectangleToScreen(picture.ClientRectangle); imageArea.Inflate(-2, -2);
     string imageText = await Task.Run(() => DirectText.Read(imageArea));
     if (!String.IsNullOrEmpty(imageText)) throw new Exception("Image region must fall back to OCR");
     log.AppendLine("PASS: non-text image control falls back to OCR");
     using (var snapshot = new Bitmap(imageArea.Width, imageArea.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
      using (var g = Graphics.FromImage(snapshot)) g.CopyFromScreen(imageArea.Location, Point.Empty, imageArea.Size);
      if (!DirectText.ScreenUnchanged(snapshot, imageArea)) throw new Exception("Stable screen rejected");
      picture.BackColor = Color.DarkBlue; picture.Refresh();
      if (DirectText.ScreenUnchanged(snapshot, imageArea)) throw new Exception("Changed screen accepted");
     }
     log.AppendLine("PASS: screen stability guard");
     if (!DirectText.Inside(new System.Windows.Rect(-100, 0, 100, 100), new System.Windows.Rect(-80, 10, 20, 20))) throw new Exception("Negative screen coordinates failed");
     if (DirectText.Inside(new System.Windows.Rect(0, 0, 10, 10), new System.Windows.Rect(5, 0, 20, 20))) throw new Exception("Partial glyph boundary failed");
     log.AppendLine("PASS: negative coordinates and partial glyph rejection");
     log.AppendLine("DIRECT TEST PASSED");
    } catch (Exception ex) { log.AppendLine("FAIL: " + ex); Environment.ExitCode = 1; }
    finally { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "direct-test.txt"), log.ToString(), Encoding.UTF8); Close(); }
   };
  }
 }
}
