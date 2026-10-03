namespace ZnunyStats.Domain.Metrics;

/// <summary>Resultado de un ticket para la efectividad (ver <see cref="Kpis.Classify"/>).</summary>
public enum Outcome { Pending, Excluded, OnTime, Late, Reopened, Unsuccessful, Overdue }
