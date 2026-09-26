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
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Web.Script.Serialization;

namespace TextSnip {
 static class DirectText {
  internal static string LastReason = "";
  [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(System.Drawing.Point point);
  [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
  internal static string Read(Rectangle area) {
   string path = Path.Combine(Path.GetTempPath(), "textsnip-direct-" + Guid.NewGuid().ToString("N") + ".json");
   try {
    var start = new ProcessStartInfo(System.Windows.Forms.Application.ExecutablePath,
     "--extract " + area.X + " " + area.Y + " " + area.Width + " " + area.Height + " \"" + path + "\"");
    start.UseShellExecute = false; start.CreateNoWindow = true;
    using (var process = Process.Start(start)) {
     if (!process.WaitForExit(3500)) { try { process.Kill(); process.WaitForExit(1000); } catch { } return null; }
     if (!File.Exists(path)) return null;
     var result = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
     LastReason = result.ContainsKey("reason") ? Convert.ToString(result["reason"]) : "";
     return result.ContainsKey("text") ? Convert.ToString(result["text"]) : null;
    }
   } catch { return null; }
   finally { try { File.Delete(path); } catch { } }
  }
  internal static void Worker(string[] args) {
   if (args.Length != 6) return;
   string text = null;
   // An isolated MTA process bounds hangs in accessibility providers.
   var thread = new Thread(delegate() {
    try { text = Extract(new Rect(Int32.Parse(args[1]), Int32.Parse(args[2]), Int32.Parse(args[3]), Int32.Parse(args[4]))); } catch (Exception ex) { LastReason += ex.ToString(); }
   });
   thread.SetApartmentState(ApartmentState.MTA); thread.IsBackground = true; thread.Start();
   if (!thread.Join(2800)) text = null;
   try { File.WriteAllText(args[5], new JavaScriptSerializer().Serialize(new { text = text, reason = LastReason }), Encoding.UTF8); } catch { }
  }
  static bool Intersects(Rect a, Rect b) { return !a.IsEmpty && a.Width > 0 && a.Height > 0 && a.IntersectsWith(b); }
  internal static bool Inside(Rect selection, Rect glyph) {
   if (!Intersects(selection, glyph)) return false;
   Rect overlap = Rect.Intersect(selection, glyph);
   return selection.Contains(new System.Windows.Point(glyph.X + glyph.Width / 2, glyph.Y + glyph.Height / 2)) &&
    overlap.Width * overlap.Height >= glyph.Width * glyph.Height * 0.8;
  }
  static IEnumerable<Rect> Bounds(TextPatternRange range) {
   return range.GetBoundingRectangles().Where(r => !r.IsEmpty && r.Width > 0 && r.Height > 0);
  }
  static string Extract(Rect area) {
   var center = new System.Windows.Point(area.X + area.Width / 2, area.Y + area.Height / 2);
   IntPtr root = GetAncestor(WindowFromPoint(new System.Drawing.Point((int)center.X, (int)center.Y)), 2);
   if (root == IntPtr.Zero) { LastReason = "No source window"; return null; }
   // A region spanning different windows is not safe to extract as one document.
   foreach (var p in new[] { area.TopLeft, area.TopRight, area.BottomLeft, area.BottomRight }) {
    var point = new System.Drawing.Point((int)Math.Min(area.Right - 1, Math.Max(area.Left + 1, p.X)), (int)Math.Min(area.Bottom - 1, Math.Max(area.Top + 1, p.Y)));
    if (GetAncestor(WindowFromPoint(point), 2) != root) { LastReason = "Selection spans windows"; return null; }
   }
   AutomationElement element = AutomationElement.FromPoint(center);
   for (int depth = 0; element != null && depth < 20; depth++, element = TreeWalker.ControlViewWalker.GetParent(element)) {
    LastReason += element.Current.ControlType.ProgrammaticName + ";";
    if (element.Current.IsPassword) return null;
    object value;
    if (!element.TryGetCurrentPattern(TextPattern.Pattern, out value)) continue;
    LastReason += "TextPattern;";
    Rect box = element.Current.BoundingRectangle;
    if (!box.Contains(area)) { LastReason += "bounds outside;"; continue; }
    var pattern = (TextPattern)value;
    var output = new StringBuilder(); bool omitted = false; int steps = 0;
    foreach (TextPatternRange visible in pattern.GetVisibleRanges()) {
     if (!Bounds(visible).Any(r => Intersects(r, area))) continue;
     // Embedded images can contain text that accessibility does not expose.
     foreach (AutomationElement child in visible.GetChildren()) {
      if (child.Current.ControlType == ControlType.Image && Intersects(child.Current.BoundingRectangle, area)) return null;
     }
     if (Bounds(visible).All(r => area.Contains(r))) {
      string whole = visible.GetText(50001);
      if (whole.Length > 50000 || whole.IndexOf('\uFFFC') >= 0) return null;
      if (output.Length > 0) output.Append("\r\n");
      output.Append(whole); continue;
     }
     TextPatternRange cursor = visible.Clone();
     cursor.MoveEndpointByRange(TextPatternRangeEndpoint.End, cursor, TextPatternRangeEndpoint.Start);
     while (cursor.CompareEndpoints(TextPatternRangeEndpoint.Start, visible, TextPatternRangeEndpoint.End) < 0) {
      if (++steps > 1800) return null;
      if (cursor.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Word, 1) == 0) break;
      if (cursor.CompareEndpoints(TextPatternRangeEndpoint.End, visible, TextPatternRangeEndpoint.End) > 0)
       cursor.MoveEndpointByRange(TextPatternRangeEndpoint.End, visible, TextPatternRangeEndpoint.End);
      Rect[] rectangles = Bounds(cursor).ToArray();
      string word = cursor.GetText(4096);
      if (word.Length >= 4096) return null;
      if (rectangles.Length > 0 && rectangles.All(r => area.Contains(r))) {
       if (word.IndexOf('\uFFFC') >= 0) return null;
       if (omitted && output.Length > 0) output.Append("\r\n");
       output.Append(word); omitted = false;
      } else if (rectangles.Any(r => Intersects(r, area))) {
       TextPatternRange character = cursor.Clone();
       character.MoveEndpointByRange(TextPatternRangeEndpoint.End, character, TextPatternRangeEndpoint.Start);
       while (character.CompareEndpoints(TextPatternRangeEndpoint.Start, cursor, TextPatternRangeEndpoint.End) < 0) {
        if (++steps > 1800) return null;
        if (character.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 1) == 0) break;
        string ch = character.GetText(16);
        if (ch.IndexOf('\uFFFC') >= 0) return null;
        if (Bounds(character).Any(r => Inside(area, r))) {
         if (omitted && output.Length > 0) output.Append("\r\n");
         output.Append(ch); omitted = false;
        } else omitted = true;
        character.MoveEndpointByRange(TextPatternRangeEndpoint.Start, character, TextPatternRangeEndpoint.End);
       }
      } else if (!String.IsNullOrWhiteSpace(word)) omitted = true;
      else if (!omitted && output.Length > 0) output.Append(word);
      if (output.Length > 50000) return null;
      cursor.MoveEndpointByRange(TextPatternRangeEndpoint.Start, cursor, TextPatternRangeEndpoint.End);
     }
    }
    string text = output.ToString().Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n").Trim();
    if (!String.IsNullOrWhiteSpace(text)) return text;
   }
   return null;
  }
  internal static bool ScreenUnchanged(Bitmap original, Rectangle area) {
   try {
    using (var current = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb)) {
     using (var graphics = Graphics.FromImage(current)) graphics.CopyFromScreen(area.Location, System.Drawing.Point.Empty, area.Size);
     BitmapData a = original.LockBits(new Rectangle(System.Drawing.Point.Empty, original.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
     try {
      BitmapData b = current.LockBits(new Rectangle(System.Drawing.Point.Empty, current.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
      try {
       byte[] left = new byte[area.Width * 4], right = new byte[left.Length];
       for (int row = 0; row < area.Height; row++) {
        Marshal.Copy(IntPtr.Add(a.Scan0, row * a.Stride), left, 0, left.Length);
        Marshal.Copy(IntPtr.Add(b.Scan0, row * b.Stride), right, 0, right.Length);
        for (int i = 0; i < left.Length; i += 4)
         if (left[i] != right[i] || left[i + 1] != right[i + 1] || left[i + 2] != right[i + 2]) return false;
       }
       return true;
      } finally { current.UnlockBits(b); }
     } finally { original.UnlockBits(a); }
    }
   } catch { return false; }
  }
 }
}
