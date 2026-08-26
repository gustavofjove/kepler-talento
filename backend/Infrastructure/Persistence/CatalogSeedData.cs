using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Infrastructure.Persistence;

/// <summary>
/// The default Spanish vocabulary each family is seeded with at deployment time.
/// Mirrors <c>DEFAULT_CATALOGS</c> as it stood before catalogs moved to the API.
/// This is a deployment-time seed only; the application never creates catalog values
/// implicitly at runtime when a family is found empty.
/// </summary>
public static class CatalogSeedData
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Families =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [CatalogFamilies.Language] = ["Inglés", "Francés", "Alemán", "Italiano", "Portugués"],
            [CatalogFamilies.Program] = ["Excel", "SAP", "AutoCAD", "Navision", "Power BI"],
            [CatalogFamilies.Skill] = ["Gestión documental", "Atención al cliente", "Análisis", "Compras"],
            [CatalogFamilies.LanguageLevel] = ["A1", "A2", "B1", "B2", "C1", "C2"],
            [CatalogFamilies.ProgramLevel] = ["Básico", "Medio", "Avanzado"],
            [CatalogFamilies.SkillLevel] = ["Básico", "Medio", "Alto"],
            [CatalogFamilies.EducationType] = ["Grado", "Master", "FP", "Curso", "Doctorado"],
            [CatalogFamilies.EducationStatus] = ["Finalizada", "En curso", "Pendiente"],
            [CatalogFamilies.Sector] = ["Servicios", "Industria", "Tecnología", "Comercio", "Sanidad", "Educación"],
        };
}
