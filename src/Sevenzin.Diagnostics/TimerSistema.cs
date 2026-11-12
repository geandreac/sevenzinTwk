using System.Runtime.InteropServices;

namespace Sevenzin.Diagnostics;

/// <summary>Timer de alta resolução do sistema via <c>timeBeginPeriod</c>/<c>timeEndPeriod</c> (winmm.dll) —
/// paridade com o original (04_funcionalidades §8: timeBeginPeriod/timeEndPeriod), mas **opt-in e temporário**:
/// só sobe a resolução enquanto houver pelo menos um jogo aplicado e baixa ao último terminar.
///
/// ⚠ Aviso técnico (mostrado na UI): <c>timeBeginPeriod(1)</c> é global para o sistema e aumenta o consumo
/// de energia; por isso o Sevenzin NUNCA mantém o timer ativo sem um perfil de jogo aplicado.</summary>
public sealed class TimerAltaResolucao : IDisposable
{
    [DllImport("winmm.dll")]
    static extern uint timeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll")]
    static extern uint timeEndPeriod(uint uMilliseconds);

    /// <summary>Período mínimo aceito pelo Windows (1 ms = resolução de 0,5 ms na prática).</summary>
    public const uint PeriodoPadrao = 1;

    readonly Func<uint, uint> _begin;
    readonly Func<uint, uint> _end;
    readonly uint _periodo;
    int _referencias;

    /// <param name="begin/end">injetáveis para teste; por padrão chamam a API real do Windows.</param>
    public TimerAltaResolucao(uint periodoMs = PeriodoPadrao, Func<uint, uint>? begin = null, Func<uint, uint>? end = null)
    {
        _periodo = Math.Clamp(periodoMs, 1, 16); // timeBeginPeriod só aceita 1..16
        _begin = begin ?? timeBeginPeriod;
        _end = end ?? timeEndPeriod;
    }

    public bool Ativo => _referencias > 0;
    public int Referencias => _referencias;

    /// <summary>Sobe a resolução na 1ª referência; chamadas seguintes só incrementam a contagem.</summary>
    public void Iniciar()
    {
        _referencias++;
        if (_referencias == 1) _begin(_periodo);
    }

    /// <summary>Baixa a resolução quando a última referência termina. Chamadas extras são ignoradas.</summary>
    public void Parar()
    {
        if (_referencias <= 0) return;
        _referencias--;
        if (_referencias == 0) _end(_periodo);
    }

    /// <summary>Restaura o estado do sistema se o app for fechado com o timer ativo.</summary>
    public void Dispose() { while (_referencias > 0) Parar(); }
}
