using System.Security;
using Microsoft.Win32;
using Sevenzin.Diagnostics;

namespace Sevenzin.Wpf;

/// <summary>
/// Implementação real de IRegistro sobre o registro do Windows.
/// Leitura tolera chave ausente ou bloqueada; escrita converte SecurityException (ex.: Tamper Protection do Defender)
/// em UnauthorizedAccessException tratada pela UI, evitando qualquer encerramento inesperado do processo.
/// </summary>
public sealed class RegistroWindows : IRegistro
{
    static RegistryKey Raiz(Colmeia h) => h == Colmeia.UsuarioAtual ? Registry.CurrentUser : Registry.LocalMachine;

    public int? LerDword(Colmeia h, string caminho, string nome)
    {
        try
        {
            using var k = Raiz(h).OpenSubKey(caminho);
            return k?.GetValue(nome) is int v ? v : null;
        }
        catch { return null; }
    }

    public void EscreverDword(Colmeia h, string caminho, string nome, int valor)
    {
        try
        {
            using var k = Raiz(h).CreateSubKey(caminho, writable: true)
                ?? throw new UnauthorizedAccessException($"Não foi possível abrir '{caminho}' para gravação.");
            k.SetValue(nome, valor, RegistryValueKind.DWord);
        }
        catch (SecurityException ex)
        {
            throw new UnauthorizedAccessException($"Chave protegida pelo sistema ({caminho}): {ex.Message}", ex);
        }
    }

    public string? LerString(Colmeia h, string caminho, string nome)
    {
        try
        {
            using var k = Raiz(h).OpenSubKey(caminho);
            return k?.GetValue(nome) as string;
        }
        catch { return null; }
    }

    public void EscreverString(Colmeia h, string caminho, string nome, string valor)
    {
        try
        {
            using var k = Raiz(h).CreateSubKey(caminho, writable: true)
                ?? throw new UnauthorizedAccessException($"Não foi possível abrir '{caminho}' para gravação.");
            k.SetValue(nome, valor, RegistryValueKind.String);
        }
        catch (SecurityException ex)
        {
            throw new UnauthorizedAccessException($"Chave protegida pelo sistema ({caminho}): {ex.Message}", ex);
        }
    }

    public void ApagarValor(Colmeia h, string caminho, string nome)
    {
        try
        {
            using var k = Raiz(h).OpenSubKey(caminho, writable: true);
            k?.DeleteValue(nome, throwOnMissingValue: false);
        }
        catch (SecurityException ex)
        {
            throw new UnauthorizedAccessException($"Chave protegida pelo sistema ({caminho}): {ex.Message}", ex);
        }
    }
}
