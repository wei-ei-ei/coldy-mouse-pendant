using System;
using System.Drawing;
class Runs {
  static void Main() {
    using (var b = new Bitmap(@"assets\coldy.png")) {
      for (int y = 44; y <= 90; y += 2) {
        int runStart = -1;
        var s = y.ToString().PadLeft(3) + " ";
        for (int x = 0; x <= b.Width; x++) {
          bool on = x < b.Width && b.GetPixel(x, y).A >= 128;
          if (on && runStart < 0) runStart = x;
          if (!on && runStart >= 0) { s += runStart + "-" + (x - 1) + "(" + (x - runStart) + ") "; runStart = -1; }
        }
        Console.WriteLine(s);
      }
    }
  }
}
