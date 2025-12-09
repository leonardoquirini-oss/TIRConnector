using System.Text.Json.Serialization;

namespace TIRConnector.API.Models.DTOs;

public class ContainerDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("cassa")]
    public string? Cassa { get; set; }

    [JsonPropertyName("descrizione")]
    public string? Descrizione { get; set; }

    [JsonPropertyName("piantoni")]
    public int? Piantoni { get; set; }

    [JsonPropertyName("tipo")]
    public string? Tipo { get; set; }

    [JsonPropertyName("nota")]
    public string? Nota { get; set; }

    [JsonPropertyName("container")]
    public bool? Container { get; set; }

    [JsonPropertyName("mobile")]
    public bool? Mobile { get; set; }

    [JsonPropertyName("rottami")]
    public bool? Rottami { get; set; }

    [JsonPropertyName("larghezza")]
    public int? Larghezza { get; set; }

    [JsonPropertyName("altezza")]
    public int? Altezza { get; set; }

    [JsonPropertyName("lunghezza")]
    public int? Lunghezza { get; set; }

    [JsonPropertyName("volume")]
    public int? Volume { get; set; }

    [JsonPropertyName("manutenzione")]
    public string? Manutenzione { get; set; }

    [JsonPropertyName("modello")]
    public string? Modello { get; set; }

    [JsonPropertyName("numserie")]
    public string? NumSerie { get; set; }

    [JsonPropertyName("controllock")]
    public string? ControlLock { get; set; }

    [JsonPropertyName("portatakg")]
    public int? PortataKg { get; set; }

    [JsonPropertyName("sponda")]
    public bool? Sponda { get; set; }

    [JsonPropertyName("gru")]
    public bool? Gru { get; set; }

    [JsonPropertyName("carrelli")]
    public bool? Carrelli { get; set; }

    [JsonPropertyName("transpallet")]
    public bool? Transpallet { get; set; }

    [JsonPropertyName("pesaAPonte")]
    public bool? PesaAPonte { get; set; }

    [JsonPropertyName("targa")]
    public string? Targa { get; set; }

    [JsonPropertyName("assali")]
    public bool? Assali { get; set; }

    [JsonPropertyName("pneumatici")]
    public bool? Pneumatici { get; set; }

    [JsonPropertyName("ck")]
    public bool? Ck { get; set; }

    [JsonPropertyName("ckData")]
    public string? CkData { get; set; }

    [JsonPropertyName("giorniPre")]
    public int? GiorniPre { get; set; }

    [JsonPropertyName("tara")]
    public int? Tara { get; set; }

    [JsonPropertyName("identificativo")]
    public string? Identificativo { get; set; }

    [JsonPropertyName("foto")]
    public string? Foto { get; set; }

    [JsonPropertyName("id_tfp")]
    public string? IdTfp { get; set; }
}
