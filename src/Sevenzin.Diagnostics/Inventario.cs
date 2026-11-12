namespace Sevenzin.Diagnostics;

/// <summary>Grau de confiança do dado, seguindo a convenção dos relatórios (07_funcionalidades.md).</summary>
public enum Confianca { Fato, Inferencia, Hipotese }

/// <summary>Um dado de inventário. Sempre carrega a origem, o grau de confiança e a hora da leitura.
/// Por desenho, nenhum coletor preenche identificadores persistentes (HWID, serial, MAC, usuário, nome do PC).</summary>
public sealed class ItemInventario
{
    public string Categoria { get; init; } = "";
    public string Nome { get; init; } = "";
    public string? Valor { get; init; }
    public string Fonte { get; init; } = "";
    public Confianca Tipo { get; init; } = Confianca.Fato;
    public string? Observacao { get; init; }
    public DateTime CapturadoUtc { get; init; } = DateTime.UtcNow;
}
