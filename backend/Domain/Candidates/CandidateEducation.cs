using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Domain.Candidates;

/// <summary>
/// An education record a candidate declares. The education type and its completion
/// status reference catalog entries in fixed families; degree, specialty and institution
/// are genuinely free text in the product and stay free text here.
/// </summary>
public sealed class CandidateEducation : CandidateRelation
{
    private CandidateEducation() { }

    public CandidateEducation(
        Guid id,
        Guid candidateId,
        Guid educationTypeId,
        Guid statusId,
        string degree,
        string institution)
        : base(id, candidateId)
    {
        EducationTypeId = educationTypeId;
        StatusId = statusId;
        Degree = degree.Trim();
        Institution = institution.Trim();
    }

    public Guid EducationTypeId { get; private set; }
    public string EducationTypeFamily { get; private set; } = CatalogFamilies.EducationType;
    public Guid StatusId { get; private set; }
    public string StatusFamily { get; private set; } = CatalogFamilies.EducationStatus;
    public string Degree { get; private set; } = string.Empty;
    public string? Specialty { get; private set; }
    public string Institution { get; private set; } = string.Empty;
    public int? EndYear { get; private set; }

    public void SetSpecialty(string? specialty) =>
        Specialty = string.IsNullOrWhiteSpace(specialty) ? null : specialty.Trim();

    public void SetEndYear(int? endYear)
    {
        if (endYear is < 1900 or > 2200)
        {
            throw new ArgumentOutOfRangeException(nameof(endYear));
        }
        EndYear = endYear;
    }
}
