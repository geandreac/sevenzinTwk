using Microsoft.Win32;

namespace Sevenzin.Diagnostics;

/// <summary>Implementação de <see cref="IRegistro"/> restrita ao HKCU, para o motor aplicar preferências
/// POR USUÁRIO (ex.: GPU de alto desempenho por jogo) sem depender da UI. HKLM lança — ajustes de máquina
/// continuam passando pelo RegistroWindows da UI, com elevação.</summary>
public sealed class RegistroUsuario : IRegistro
{
    public int? LerDword(Colmeia h, string caminho, string nome)
    {
        if (h != Colmeia.UsuarioAtual) throw new NotSupportedException("RegistroUsuario só aceita HKCU.");
        using var k = Registry.CurrentUser.OpenSubKey(caminho);
        return k?.GetValue(nome) is int v ? v : null;
    }

    public void EscreverDword(Colmeia h, string caminho, string nome, int valor)
    {
        if (h != Colmeia.UsuarioAtual) throw new NotSupportedException("RegistroUsuario só aceita HKCU.");
        using var k = Registry.CurrentUser.CreateSubKey(caminho, writable: true);
        k.SetValue(nome, valor, RegistryValueKind.DWord);
    }

    public string? LerString(Colmeia h, string caminho, string nome)
    {
        if (h != Colmeia.UsuarioAtual) throw new NotSupportedException("RegistroUsuario só aceita HKCU.");
        using var k = Registry.CurrentUser.OpenSubKey(caminho);
        return k?.GetValue(nome) as string;
    }

    public void EscreverString(Colmeia h, string caminho, string nome, string valor)
    {
        if (h != Colmeia.UsuarioAtual) throw new NotSupportedException("RegistroUsuario só aceita HKCU.");
        using var k = Registry.CurrentUser.CreateSubKey(caminho, writable: true);
        k.SetValue(nome, valor, RegistryValueKind.String);
    }

    public void ApagarValor(Colmeia h, string caminho, string nome)
    {
        if (h != Colmeia.UsuarioAtual) throw new NotSupportedException("RegistroUsuario só aceita HKCU.");
        using var k = Registry.CurrentUser.OpenSubKey(caminho, writable: true);
        k?.DeleteValue(nome, throwOnMissingValue: false);
    }
}
