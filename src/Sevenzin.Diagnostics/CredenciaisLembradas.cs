using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Sevenzin.Diagnostics;

/// <summary>
/// Armazena e recupera credenciais lembradas ("Lembrar de mim") de forma segura via DPAPI
/// (escopo CurrentUser). A senha é cifrada pelo Windows e nunca é gravada em texto puro.
/// Arquivos padrão: `%LOCALAPPDATA%\Sevenzin\ultimo_email.txt` e `senha_lembrada.bin`.
/// </summary>
public sealed class CredenciaisLembradasStore
{
    static readonly Lazy<CredenciaisLembradasStore> PadraoLazy = new(() => new CredenciaisLembradasStore());
    public static CredenciaisLembradasStore Padrao => PadraoLazy.Value;

    readonly string _caminhoEmail;
    readonly string _caminhoSenha;
    readonly object _lock = new();

    public CredenciaisLembradasStore(string? caminhoEmail = null, string? caminhoSenha = null)
    {
        var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sevenzin");
        _caminhoEmail = caminhoEmail ?? Path.Combine(pasta, "ultimo_email.txt");
        _caminhoSenha = caminhoSenha ?? Path.Combine(pasta, "senha_lembrada.bin");
    }

    public bool TemCredenciais()
    {
        lock (_lock)
        {
            return File.Exists(_caminhoEmail) && File.Exists(_caminhoSenha);
        }
    }

    public (string? Email, string? Senha) Carregar()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_caminhoEmail)) return (null, null);
                var email = File.ReadAllText(_caminhoEmail, Encoding.UTF8).Trim();
                if (string.IsNullOrWhiteSpace(email)) return (null, null);

                if (!File.Exists(_caminhoSenha)) return (email, null);
                var bytesCifrados = File.ReadAllBytes(_caminhoSenha);
                if (bytesCifrados.Length == 0) return (email, null);

                var bytesClaros = ProtectedData.Unprotect(bytesCifrados, null, DataProtectionScope.CurrentUser);
                var senha = Encoding.UTF8.GetString(bytesClaros);
                return (email, senha);
            }
            catch
            {
                return (null, null);
            }
        }
    }

    public void Salvar(string email, string senha)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(senha)) return;

        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_caminhoEmail)!);
                Directory.CreateDirectory(Path.GetDirectoryName(_caminhoSenha)!);

                File.WriteAllText(_caminhoEmail, email.Trim(), Encoding.UTF8);
                var cifrado = ProtectedData.Protect(Encoding.UTF8.GetBytes(senha), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_caminhoSenha, cifrado);
            }
            catch { }
        }
    }

    public void Limpar()
    {
        lock (_lock)
        {
            try { if (File.Exists(_caminhoEmail)) File.Delete(_caminhoEmail); } catch { }
            try { if (File.Exists(_caminhoSenha)) File.Delete(_caminhoSenha); } catch { }
        }
    }
}
