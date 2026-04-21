namespace AutoCo.Api.Data.Models;

/// <summary>Plantilla d'activitat: desa nom, descripció i criteris per reutilitzar-los.</summary>
public class ActivityTemplate
{
    public int Id { get; set; }

    /// <summary>Professor propietari de la plantilla.</summary>
    public int ProfessorId { get; set; }

    public string Name { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>JSON array de {Key, Label} per als criteris.</summary>
    public string CriteriaJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
