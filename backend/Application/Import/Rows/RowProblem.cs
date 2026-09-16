namespace KeplerTalento.Application.Import.Rows;

/// <summary>
/// One reason a source row cannot be loaded, or was left alone.
/// </summary>
/// <remarks>
/// There is deliberately no field for the offending <em>value</em>. A rejected row is
/// identified by its source key and the name of the failing field, and nothing else — which
/// is what keeps candidate personal data out of the reconciliation report and the logs. The
/// absence of that parameter is the control; a reviewer adding one would be removing it.
/// Shared by the operator migration (source key = legacy identifier) and the API import
/// (source key = 1-based data row number), so both enforce the same control.
/// </remarks>
public sealed record RowProblem(string Entity, string SourceKey, string Field, string ReasonCode)
{
    public override string ToString() => $"{Entity}/{SourceKey}: {Field} {ReasonCode}";
}

/// <summary>
/// A problem with the shape of the file rather than with the data in it. These may carry
/// detail, because they describe files and columns — never a candidate's values.
/// </summary>
public sealed record StructuralProblem(string File, string Detail)
{
    public override string ToString() => $"{File}: {Detail}";
}
