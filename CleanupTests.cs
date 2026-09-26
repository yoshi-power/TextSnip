using System;
using System.Collections.Generic;
using System.Text;

namespace TextSnip {
 static class CleanupTests {
  static int count;
  static void Equal(string label, string actual, string expected) {
   if (actual != expected) throw new Exception(label + ": expected [" + expected + "] got [" + actual + "]");
   count++;
  }
  static TextCleanup.Line L(string text, double y) { return new TextCleanup.Line { Text = text, X = 0, Right = 400, Y = y, Bottom = y + 20, Height = 20 }; }
  static string Join(string first, string second, double y, string language) { return TextCleanup.JoinLines(new List<TextCleanup.Line> { L(first, 0), L(second, y) }, language); }
  internal static void Run(StringBuilder log) {
   count = 0;
   Equal("zero in common words", TextCleanup.CorrectWords("c0mmon w0rld inf0rmation"), "common world information");
   Equal("actual numbers and identifiers", TextCleanup.CorrectWords("100 0.05 2026 AB001 H20 r0ute"), "100 0.05 2026 AB001 H20 r0ute");
   Equal("URL", TextCleanup.CorrectWords("https://c0mmon.com/w0rld"), "https://c0mmon.com/w0rld");
   Equal("email", TextCleanup.CorrectWords("c0mmon@example.com"), "c0mmon@example.com");
   Equal("code", TextCleanup.CorrectWords("c0mmon = 100;"), "c0mmon = 100;");
   Equal("ordinary prose", Join("This is a readable", "sentence with 100 words.", 28, "en"), "This is a readable sentence with 100 words.");
   Equal("word split", Join("More infor", "mation is available.", 28, "en"), "More information is available.");
   Equal("hyphenation", Join("The recogni-", "tion works.", 28, "en"), "The recognition works.");
   Equal("compound hyphen", Join("A well-", "known example.", 28, "en"), "A well-known example.");
   Equal("valid separate words", Join("This is in", "formation today.", 28, "en"), "This is in formation today.");
   Equal("paragraphs", Join("First paragraph.", "Second paragraph.", 65, "en"), "First paragraph.\r\n\r\nSecond paragraph.");
   Equal("list", Join("1. First item", "2. Second item", 28, "en"), "1. First item\r\n2. Second item");
   Equal("Korean sentence", Join("여러 줄의 문장을", "읽기 좋게 연결합니다.", 28, "ko"), "여러 줄의 문장을 읽기 좋게 연결합니다.");
   Equal("Korean split word", Join("자연스", "럽게 연결합니다.", 28, "ko"), "자연스럽게 연결합니다.");
   Equal("Japanese sentence", Join("日本語の", "文章です。", 28, "ja"), "日本語の文章です。");
   var decorated = TextCleanup.CleanLine(new List<TextCleanup.Word> {
    new TextCleanup.Word("★", 0, 0, 20, 20), new TextCleanup.Word("Hello", 40, 0, 60, 20), new TextCleanup.Word("world", 110, 0, 60, 20)
   }, "en");
   Equal("decorative icon", decorated.Text, "Hello world");
   var onlyIcon = TextCleanup.CleanLine(new List<TextCleanup.Word> { new TextCleanup.Word("★", 0, 0, 20, 20) }, "en");
   if (onlyIcon != null) throw new Exception("Icon-only line retained"); count++;
   var symbol = TextCleanup.CleanLine(new List<TextCleanup.Word> {
    new TextCleanup.Word("|", 0, 0, 10, 20), new TextCleanup.Word("Hello", 100, 0, 60, 20)
   }, "en");
   Equal("detached symbol", symbol.Text, "Hello");
   var punctuation = TextCleanup.CleanLine(new List<TextCleanup.Word> {
    new TextCleanup.Word("Total:", 0, 0, 60, 20), new TextCleanup.Word("100", 70, 0, 40, 20), new TextCleanup.Word("+", 120, 0, 10, 20), new TextCleanup.Word("5", 140, 0, 15, 20)
   }, "en");
   Equal("numbers and punctuation", punctuation.Text, "Total: 100 + 5");
   var bullet = TextCleanup.CleanLine(new List<TextCleanup.Word> {
    new TextCleanup.Word("•", 0, 0, 10, 20), new TextCleanup.Word("First", 20, 0, 50, 20)
   }, "en");
   Equal("bullet preserved", bullet.Text, "• First");
   log.AppendLine("PASS: " + count + " cleanup regression checks (icons, prose, paragraphs, lists, ko/ja, zero corrections, numeric/code/URL guards)");
  }
 }
}
