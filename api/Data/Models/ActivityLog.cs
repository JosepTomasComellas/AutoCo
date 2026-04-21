namespace AutoCo.Api.Data.Models;

/// <summary>Registre d'accions importants sobre una activitat (sense FK per preservar el log si s'esborra l'activitat).</summary>
public class ActivityLog
{
    public int Id { get; set; }

    /// <summary>Id de l'activitat (sense FK — el log es preserva si l'activitat s'esborra).</summary>
    public int ActivityId { get; set; }

    /// <summary>Nom de l'activitat en el moment de l'acció (desnormalitzat).</summary>
    public string ActivityName { get; set; } = "";

    /// <summary>Nom de l'actor (professor o alumne) en el moment de l'acció (desnormalitzat).</summary>
    public string? ActorName { get; set; }

    /// <summary>Codi de l'acció: "created", "opened", "closed", "evaluated", "deleted".</summary>
    public string Action { get; set; } = "";

    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
