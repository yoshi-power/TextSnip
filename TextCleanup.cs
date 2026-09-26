using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TextSnip {
 // Deliberately bounded dictionary: unknown words, identifiers and numbers stay unchanged.
 static class TextCleanup {
  static readonly HashSet<string> Words = new HashSet<string>((
   "a an at as be by do go he if in is it me my no of on or so to up us we formation " +
   "about above account action after again against all allow also always amount another answer any application around available away " +
   "back because become before being below between both browser button called can change check click close code color come common company complete computer consider contact content continue control copy could country course create current data default different document does doing done down download during each email end enough even every example experience explain feature file find first follow following for form found free from full function general get give good great group had have help here high home house how however icon image important improve include information input inside instead into issue item just keep know language large last later learn left less letter life like line link list little local long look made main make many may mean message might mode model more most move much must name need never new next not note now number object off often old once one only open option order other our out output over own page part people place please point possible problem process program project provide public put question read really reason remove result return right same save say screen search second see select sentence service set setting several should show side simple since small some something source space start state still stop string such system take test text than that the their them then there these they thing think this those through time together too tool total two type under until update use used user using value version very want was way web well were what when where which while white who why will window with within without word work world would write year you your " +
   "hello world recognition technology development international paragraph automatic automatically readable reading welcome morning afternoon evening tomorrow today button correction context correct connection option options common ordinary support solution software conversation attention notification function format original processing performance " +
   "안녕하세요 감사합니다 텍스트 인식 문장 문맥 단어 한국어 영어 일본어 이미지 아이콘 복사 자동으로 자연스럽게 읽기 연결합니다 테스트 정보 처리 프로그램 기능 사용합니다 있습니다 없습니다 합니다 됩니다").Split(' '), StringComparer.OrdinalIgnoreCase);
  static readonly Regex Token = new Regex(@"(?<![\w@./:\\-])[A-Za-z0]+(?![\w@./:\\-])");
  static readonly Regex Bullet = new Regex(@"^\s*(?:[-*•·]|\d+[.)])\s+");
  internal sealed class Word {
   internal string Text; internal double X, Y, Width, Height;
   internal Word(string text, double x, double y, double width, double height) { Text = text; X = x; Y = y; Width = width; Height = height; }
  }
  internal sealed class Line {
   internal string Text; internal double X, Y, Right, Bottom, Height;
   internal bool Structured;
  }
  static bool IsCode(string text) {
   return Regex.IsMatch(text, @"https?://|www\.|[\w.+-]+@[\w.-]+|[{}=<>]|(?:\w+\s*\([^)]*\)\s*;)|(?:[A-Za-z]:\\)|\b(?:0x[0-9a-fA-F]+|[A-Z]+\d+\w*)\b");
  }
  internal static string CorrectWords(string text) {
   if (IsCode(text)) return text;
   return Token.Replace(text, delegate(Match m) {
    string value = m.Value;
    if (value.Length < 4 || !value.Contains("0") || !Regex.IsMatch(value, "[a-z]") || value.Count(c => c == '0') > 2) return value;
    // A known lexical candidate is required; never change pure numbers or arbitrary IDs.
    string candidate = value.Replace('0', 'o');
    return Words.Contains(candidate) ? candidate : value;
   });
  }
  static bool Decoration(string text) {
   return Regex.IsMatch(text, @"^[\u2190-\u21ff\u2300-\u23ff\u2500-\u27ff\uE000-\uF8FF\uD800-\uDFFF]+$");
  }
  static double Median(IEnumerable<double> source) {
   double[] values = source.OrderBy(x => x).ToArray(); return values.Length == 0 ? 1 : values[values.Length / 2];
  }
  internal static Line CleanLine(List<Word> words, string language) {
   if (words.Count == 0) return null;
   double height = Median(words.Where(w => w.Text.Any(Char.IsLetterOrDigit)).Select(w => w.Height));
   var kept = new List<Word>();
   for (int i = 0; i < words.Count; i++) {
    Word w = words[i]; string t = w.Text.Trim();
    if (t.Length == 0) continue;
    bool listMarker = i == 0 && (t == "•" || t == "·") && words.Count > 1;
    if (Decoration(t) && !listMarker) continue;
    bool hasText = t.Any(Char.IsLetterOrDigit);
    double gap = Double.MaxValue;
    if (i > 0) gap = Math.Min(gap, Math.Max(0, w.X - words[i - 1].X - words[i - 1].Width));
    if (i + 1 < words.Count) gap = Math.Min(gap, Math.Max(0, words[i + 1].X - w.X - w.Width));
    // Preserve punctuation alongside text and list markers, discard detached symbol islands.
    if (!hasText && gap > height * 1.5 && !Regex.IsMatch(t, @"^[-*•]$")) continue;
    // Common icon OCR (O, X, a stray ideograph): require spatial evidence as well.
    if (t.Length == 1 && Char.IsLetter(t[0]) && t != "I" && t != "a" && t != "A" && words.Count > 1 &&
        gap > height * 2.5 && (w.Height > height * 1.4 || w.Height < height * 0.65)) continue;
    kept.Add(w);
   }
   if (kept.Count == 0 || !kept.Any(w => w.Text.Any(Char.IsLetterOrDigit))) return null;
   string text = String.Join(" ", kept.Select(w => w.Text.Trim()));
   if (language.StartsWith("ja")) text = Regex.Replace(text, @"(?<=[\u3040-\u30ff\u3400-\u9fff]) +(?=[\u3040-\u30ff\u3400-\u9fff])", "");
   bool columns = false;
   for (int i = 1; i < kept.Count; i++) if (kept[i].X - kept[i - 1].X - kept[i - 1].Width > height * 3) columns = true;
   return new Line { Text = CorrectWords(text), X = kept.Min(w => w.X), Y = kept.Min(w => w.Y), Right = kept.Max(w => w.X + w.Width),
    Bottom = kept.Max(w => w.Y + w.Height), Height = Median(kept.Select(w => w.Height)), Structured = columns || IsCode(text) || Regex.IsMatch(text, @"^[\d\s.,%+:/()-]+$") };
  }
  internal static string JoinLines(List<Line> lines, string language) {
   var output = new StringBuilder(); Line previous = null;
   foreach (Line line in lines) {
    if (previous == null) { output.Append(line.Text); previous = line; continue; }
    double height = Math.Max(previous.Height, line.Height);
    bool block = line.Y - previous.Bottom > height * 0.85 || line.Y < previous.Y ||
     Math.Abs(line.X - previous.X) > height * 2 || Math.Min(line.Right, previous.Right) <= Math.Max(line.X, previous.X) ||
     Math.Max(line.Height, previous.Height) > Math.Min(line.Height, previous.Height) * 1.4;
    bool separate = block || previous.Structured || line.Structured || Bullet.IsMatch(line.Text) || Bullet.IsMatch(previous.Text);
    if (separate) output.Append(block ? "\r\n\r\n" : "\r\n").Append(line.Text);
    else {
     string left = output.ToString();
     Match tail = Regex.Match(left, @"([A-Za-z가-힣]+)(-?)$"); Match head = Regex.Match(line.Text, @"^([A-Za-z가-힣]+)");
     bool joined = false;
     if (tail.Success && head.Success) {
      string a = tail.Groups[1].Value, b = head.Groups[1].Value;
      if (Words.Contains(a + b) && (tail.Groups[2].Value == "-" || (!Words.Contains(a) && !Words.Contains(b)))) {
       if (tail.Groups[2].Value == "-") output.Length--;
       output.Append(line.Text); joined = true;
      } else if (tail.Groups[2].Value == "-") { output.Append(line.Text); joined = true; }
     }
     if (!joined) {
      bool japanese = language.StartsWith("ja") && Regex.IsMatch(left, @"[\u3000-\u30ff\u3400-\u9fff]$") && Regex.IsMatch(line.Text, @"^[\u3000-\u30ff\u3400-\u9fff]");
      output.Append(japanese ? "" : " ").Append(line.Text);
     }
    }
    previous = line;
   }
   return output.ToString().Trim();
  }
  internal static string Process(Dictionary<string, object> result) {
   if (!result.ContainsKey("lines")) return Convert.ToString(result["text"]);
   string language = Convert.ToString(result["language"]); var lines = new List<Line>();
   foreach (Dictionary<string, object> line in (IEnumerable)result["lines"]) {
    var words = new List<Word>();
    foreach (Dictionary<string, object> w in (IEnumerable)line["words"])
     words.Add(new Word(Convert.ToString(w["text"]), Convert.ToDouble(w["x"]), Convert.ToDouble(w["y"]), Convert.ToDouble(w["width"]), Convert.ToDouble(w["height"])));
    Line cleaned = CleanLine(words, language); if (cleaned != null) lines.Add(cleaned);
   }
   return JoinLines(lines, language);
  }
 }
}
