using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
namespace TextSnip {
 static class DesignPreview {
  internal static void Run() {
   using(var icon=Design.CreateIcon()) using(var file=File.Create(Path.Combine(Application.StartupPath,"TextSnip.ico"))) icon.Save(file);
   using(var form=new MainWindow(true)) {
    form.ShowInTaskbar=false; form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-10000,-10000); form.Show(); Application.DoEvents(); form.PerformLayout();
    using(var image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size)); image.Save(Path.Combine(Application.StartupPath,"settings-preview.png"),ImageFormat.Png); }
   }
   using(var screen=new Bitmap(1100,650)) {
    using(var g=Graphics.FromImage(screen)) using(var title=new Font("Malgun Gothic",24,FontStyle.Bold)) using(var body=new Font("Malgun Gothic",17)) {
     g.Clear(Color.FromArgb(245,247,251)); g.FillRectangle(Brushes.White,120,120,830,400);
     g.DrawString("아이디어를 이어 붙이는 가장 쉬운 방법",title,new SolidBrush(Design.Ink),170,180);
     g.DrawString("웹페이지의 문장도, 이미지 속 텍스트도.\n필요한 영역을 선택하고 바로 붙여넣으세요.\nHello, TextSnip.  100% on your device.",body,Brushes.DimGray,170,250);
    }
    using(var selection=new SelectionWindow(screen,new Rectangle(0,0,1100,650))) {
     selection.PreviewSelection(); using(var image=new Bitmap(1100,650)) { selection.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size)); image.Save(Path.Combine(Application.StartupPath,"selection-preview.png"),ImageFormat.Png); }
    }
   }
   using(var toast=new StatusToast("원문 복사 완료 · 128자 · Ctrl + V로 붙여넣으세요.",false,false)) using(var image=new Bitmap(toast.Width,toast.Height)) {
    toast.Location=new Point(-10000,-10000); toast.Show(); Application.DoEvents();
    toast.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size)); image.Save(Path.Combine(Application.StartupPath,"toast-preview.png"),ImageFormat.Png);
   }
  }
 }
}
