using System;
using System.Drawing;
class Sample {
  static void Main() {
    using (var src = new Bitmap(@"assets\coldy_lowres.png"))
    using (var cut = new Bitmap(@"assets\coldy.png")) {
      for (int y = 46; y <= 72; y += 2) {
        var line = y.ToString().PadLeft(3) + " ";
        for (int x = 90; x <= 180; x += 4) {
          var s = src.GetPixel(x, y);
          var c = cut.GetPixel(x, y);
          int m = (s.R + s.G + s.B) / 3;
          line += (c.A > 128 ? "O" : ".") + m.ToString("000") + " ";
        }
        Console.WriteLine(line);
      }
    }
  }
}
