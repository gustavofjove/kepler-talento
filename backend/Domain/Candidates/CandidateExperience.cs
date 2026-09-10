using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Domain.Candidates;

/// <summary>
/// A work experience record a candidate declares. The sector references a catalog entry
/// in a fixed family; company, position and functions are free text.
/// </summary>
public sealed class CandidateExperience : CandidateRelation
{
    private CandidateExperience() { }

    public CandidateExperience(
        Guid id,
        Guid candidateId,
        Guid sectorId,
        string company,
        string position)
        : base(id, candidateId)
    {
        SectorId = sectorId;
        Company = company.Trim();
        Position = position.Trim();
    }

    public Guid SectorId { get; private set; }
    public string SectorFamily { get; private set; } = CatalogFamilies.Sector;
    public string Company { get; private set; } = string.Empty;
    public string Position { get; private set; } = string.Empty;
    public string? Functions { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public int? YearsExperience { get; private set; }
    public bool IsCurrent { get; private set; }

    public void SetFunctions(string? functions) =>
        Functions = string.IsNullOrWhiteSpace(functions) ? null : functions.Trim();

    public void SetPeriod(DateOnly? startDate, DateOnly? endDate, bool isCurrent)
    {
        if (startDate is not null && endDate is not null && endDate < startDate)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate));
        }
        if (isCurrent && endDate is not null)
        {
            throw new ArgumentException("A current experience cannot have an end date.", nameof(endDate));
        }
        StartDate = startDate;
        EndDate = endDate;
        IsCurrent = isCurrent;
    }

    public void SetYearsExperience(int? yearsExperience)
    {
        if (yearsExperience is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(yearsExperience));
        }
        YearsExperience = yearsExperience;
    }
}
