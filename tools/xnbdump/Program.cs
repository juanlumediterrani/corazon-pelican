using Microsoft.Xna.Framework.Content;
class SP : IServiceProvider { public object? GetService(Type t) => null; }
class P {
  static void Main(string[] a) {
    var cm = new ContentManager(new SP(), Path.Combine(a[0], "Content"));
    for (int i = 1; i < a.Length; i++) {
      Console.WriteLine("### " + a[i]);
      var d = cm.Load<Dictionary<string,string>>(a[i]);
      foreach (var kv in d) Console.WriteLine(kv.Key + " => " + kv.Value);
    }
  }
}
