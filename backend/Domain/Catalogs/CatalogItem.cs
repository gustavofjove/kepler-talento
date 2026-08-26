namespace KeplerTalento.Domain.Catalogs;

/// <summary>
/// A business vocabulary value within one catalog family. Catalog values are business
/// reference data, not personal data. They are never physically deleted; removal from
/// use is expressed as <see cref="IsActive"/> becoming false.
/// </summary>
public sealed class CatalogItem
{
    private CatalogItem() { }

    public CatalogItem(
        Guid id,
        string family,
        string code,
        string nameEs,
        string? nameEn,
        int sortOrder,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Family = family;
        Code = code;
        SortOrder = sortOrder;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        Rename(nameEs, nameEn, createdAtUtc);
    }

    public Guid Id { get; private set; }
    public string Family { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string NameEs { get; private set; } = string.Empty;
    public string NameNormalized { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public uint Version { get; private set; }

    public void Rename(string nameEs, string? nameEn, DateTimeOffset updatedAtUtc)
    {
        NameEs = nameEs.Trim();
        NameNormalized = CatalogName.Normalize(NameEs);
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        UpdatedAtUtc = updatedAtUtc;
    }

    public void ChangeCode(string code, DateTimeOffset updatedAtUtc)
    {
        Code = code.Trim().ToUpperInvariant();
        UpdatedAtUtc = updatedAtUtc;
    }

    public void MoveTo(int sortOrder, DateTimeOffset updatedAtUtc)
    {
        if (SortOrder == sortOrder)
        {
            return;
        }
        SortOrder = sortOrder;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetActive(bool isActive, DateTimeOffset updatedAtUtc)
    {
        if (IsActive == isActive)
        {
            return;
        }
        IsActive = isActive;
        UpdatedAtUtc = updatedAtUtc;
    }
}
