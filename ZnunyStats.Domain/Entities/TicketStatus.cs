namespace ZnunyStats.Domain.Entities;

/// <summary>Estados simplificados que ve el usuario.</summary>
public static class TicketStatus
{
    public const string New = "Nuevo";
    public const string Open = "Abierto";
    public const string Pending = "Pendiente";
    public const string Resolved = "Resuelto";
    public const string ClosedUnsuccessful = "Cerrado sin éxito";
    public const string Merged = "Fusionado";
    public const string Removed = "Eliminado";

    public static readonly string[] All = [New, Open, Pending, Resolved, ClosedUnsuccessful];
}
