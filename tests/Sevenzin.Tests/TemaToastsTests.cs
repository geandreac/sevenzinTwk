using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>O ToastHost busca cores do tema pelo nome ("WarningBg", "DangerBrush"…). Se um nome faltar no Theme.xaml,
/// o toast lança NullReferenceException — e com a recuperação automática isso virou um laço de reabertura.</summary>
public class TemaToastsTests
{
    static string Src(params string[] partes) =>
        Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Sevenzin.Wpf" }.Concat(partes).ToArray()));

    [Fact]
    public void Toda_cor_usada_pelos_toasts_existe_no_tema()
    {
        var toasts = File.ReadAllText(Src("Toasts.cs"));
        var tema = File.ReadAllText(Src("Theme.xaml"));
        var chaves = Regex.Matches(toasts, "\"(\\w+(?:Bg|Brush))\"").Select(m => m.Groups[1].Value).Distinct().ToList();

        Assert.Contains("WarningBg", chaves); // garante que a extração achou as chaves de verdade
        var faltando = chaves.Where(k => !tema.Contains($"x:Key=\"{k}\"")).ToList();
        Assert.True(faltando.Count == 0, "Cores ausentes no Theme.xaml: " + string.Join(", ", faltando));
    }
}
