namespace ZnunyStats.Api.Analytics;

/// <summary>
/// Traduce las entradas de ticket_history a texto legible.
/// Znuny guarda los valores separados por "%%", p. ej. "%%new%%closed successful%%".
/// </summary>
public static class HistoryText
{
    public static string Describe(string type, string detail)
    {
        var p = detail.Split("%%", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string At(int i) => i < p.Length ? p[i] : "";

        return type switch
        {
            "NewTicket" => $"Ticket creado en {At(1)}",
            "StateUpdate" => $"Estado: {At(0)} → {At(1)}",
            "Move" => $"Movido a {At(0)}",
            "OwnerUpdate" => $"Asignado a {At(0)}",
            "ResponsibleUpdate" => $"Responsable: {At(0)}",
            "PriorityUpdate" => $"Prioridad: {At(0)} → {At(2)}",
            "Lock" => "Ticket bloqueado por el agente",
            "Unlock" => "Ticket desbloqueado",
            "AddNote" => "Nota agregada",
            "EmailCustomer" => "Correo del cliente",
            "EmailAgent" => "Correo enviado por el agente",
            "SendAnswer" => "Respuesta enviada al cliente",
            "PhoneCallCustomer" => "Llamada del cliente",
            "PhoneCallAgent" => "Llamada del agente",
            "WebRequestCustomer" => "Solicitud desde el portal",
            "FollowUp" => "Seguimiento del cliente",
            "Bulk" => "Acción masiva",
            "CustomerUpdate" => "Cliente actualizado",
            "SetPendingTime" => "Tiempo de espera actualizado",
            "SendAutoReply" => "Respuesta automática",
            "SendAgentNotification" => "Notificación a agentes",
            "SendCustomerNotification" => "Notificación al cliente",
            "Forward" => "Reenviado",
            "Merged" => "Fusionado",
            _ => string.Join(" · ", p),
        };
    }
}
