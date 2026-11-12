using Microsoft.Win32;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

/// <summary>Implementação real de IRegistro sobre o registro do Windows (igual à do app WinForms,
/// durante a migração). Leitura tolera chave ausente; escrita em HKLM exige elevação.</summary>
public sealed class RegistroWindows : IRegistro
{
    static RegistryKey Raiz(Colmeia h) => h == Colmeia.UsuarioAtual ? Registry.CurrentUser : Registry.LocalMachine;

    public int? LerDword(Colmeia h, string caminho, string nome)
    {
        using var k = Raiz(h).OpenSubKey(caminho);
        return k?.GetValue(nome) is int v ? v : null;
    }

    public void EscreverDword(Colmeia h, string caminho, string nome, int valor)
    {
        using var k = Raiz(h).CreateSubKey(caminho, writable: true);
        k.SetValue(nome, valor, RegistryValueKind.DWord);
    }

    public string? LerString(Colmeia h, string caminho, string nome)
    {
        using var k = Raiz(h).OpenSubKey(caminho);
        return k?.GetValue(nome) as string;
    }

    public void EscreverString(Colmeia h, string caminho, string nome, string valor)
    {
        using var k = Raiz(h).CreateSubKey(caminho, writable: true);
        k.SetValue(nome, valor, RegistryValueKind.String);
    }

    public void ApagarValor(Colmeia h, string caminho, string nome)
    {
        using var k = Raiz(h).OpenSubKey(caminho, writable: true);
        k?.DeleteValue(nome, throwOnMissingValue: false);
    }
}
