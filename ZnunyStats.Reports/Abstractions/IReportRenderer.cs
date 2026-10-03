using ZnunyStats.Reports.Content;

namespace ZnunyStats.Reports.Abstractions;

/// <summary>Una presentación de un <see cref="ReportContent"/>: Excel para analizar, PDF para compartir.</summary>
public interface IReportRenderer
{
    /// <summary>Valor del parámetro <c>format</c> y extensión del archivo (xlsx, pdf).</summary>
    string Format { get; }

    string ContentType { get; }

    byte[] Render(ReportContent content);
}
