using OpenSintegra.Core;

namespace OpenSintegra.Reports;

public enum ReportMode
{
    Completo,
    SomenteNotasNaoLancadas,
    ProdutosMaisVendidos
}

public sealed record FiscalReportContext(
    SintegraDocument Document,
    ValidationReport Report,
    ScopedFiscalView ScopedView,
    AuditReport? Audit,
    string TaxRegime = "Simples Nacional",
    string AppVersion = "1.0.0",
    ReportMode Mode = ReportMode.Completo
);
