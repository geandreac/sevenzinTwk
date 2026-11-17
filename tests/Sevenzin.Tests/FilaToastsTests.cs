using Sevenzin.Diagnostics;
using Xunit;

namespace Sevenzin.Tests;

/// <summary>F9: lógica pura da fila de toasts — duração de 3 s, expiração, pausa, dedup em < 1 s
/// e remoção individual. A apresentação WPF é validada por render/smoke.</summary>
public class FilaToastsTests
{
    static DateTime Agora => new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Toast_expira_exatamente_aos_3_segundos()
    {
        var fila = new FilaToasts();
        fila.Adicionar(TipoToast.Info, "T", "M", Agora);

        Assert.Empty(fila.RemoverExpirados(Agora.AddSeconds(2.9), FilaToasts.DuracaoPadrao)); // ainda visível
        Assert.Single(fila.RemoverExpirados(Agora.AddSeconds(3), FilaToasts.DuracaoPadrao));   // expirou
        Assert.Empty(fila.Itens);
    }

    [Fact]
    public void Multiplos_toasts_empilham_com_timers_independentes()
    {
        var fila = new FilaToasts();
        fila.Adicionar(TipoToast.Sucesso, "A", "1", Agora);
        fila.Adicionar(TipoToast.Info, "B", "2", Agora.AddSeconds(1));

        Assert.Equal(2, fila.Itens.Count);
        Assert.Single(fila.RemoverExpirados(Agora.AddSeconds(3), FilaToasts.DuracaoPadrao));  // só o 1º
        Assert.Single(fila.RemoverExpirados(Agora.AddSeconds(4), FilaToasts.DuracaoPadrao));  // o 2º
    }

    [Fact]
    public void Evento_repetido_em_menos_de_1s_e_deduplicado()
    {
        var fila = new FilaToasts();
        var primeiro = fila.Adicionar(TipoToast.Aviso, "Falha", "x", Agora);
        Assert.NotNull(primeiro);

        Assert.Null(fila.Adicionar(TipoToast.Aviso, "Falha", "x", Agora.AddMilliseconds(900))); // dedup
        Assert.NotNull(fila.Adicionar(TipoToast.Aviso, "Falha", "x", Agora.AddSeconds(1.1)));    // já passou a janela
        Assert.Equal(2, fila.Itens.Count);
    }

    [Fact]
    public void Toast_pausado_nao_expira_e_remocao_individual_funciona()
    {
        var fila = new FilaToasts();
        var a = fila.Adicionar(TipoToast.Info, "A", "1", Agora)!;
        fila.Adicionar(TipoToast.Info, "B", "2", Agora);

        var pausados = new[] { a.Id };
        var expirados = fila.RemoverExpirados(Agora.AddSeconds(4), FilaToasts.DuracaoPadrao, pausados);
        Assert.Single(expirados);              // só o B expirou
        Assert.True(fila.Itens.Any(i => i.Id == a.Id)); // A continua (pausado)

        Assert.True(fila.Remover(a.Id));       // dismiss manual
        Assert.Empty(fila.Itens);
    }
}
