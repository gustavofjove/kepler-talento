using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Infrastructure.Persistence;

/// <summary>
/// One value in the default vocabulary. <see cref="Code"/> is normally left null and
/// derived from the name; it is set explicitly only where <see cref="CatalogName.DeriveCode"/>
/// cannot tell two names apart, because <c>(Family, Code)</c> is unique.
/// </summary>
/// <remarks>
/// <c>C#</c> and <c>C++</c> are the case that forces this: the derive rule folds every
/// non-alphanumeric character to a separator and drops the empty segments, so both names
/// derive <c>C</c> and the second insert would violate the index. Resolution is unaffected —
/// the resolver matches on the exact name first — so only the stored code differs.
/// </remarks>
public readonly record struct CatalogSeedValue(string NameEs, string? Code = null)
{
    public static implicit operator CatalogSeedValue(string nameEs) => new(nameEs);

    public string ResolveCode() => Code ?? CatalogName.DeriveCode(NameEs);
}

/// <summary>
/// The default Spanish vocabulary each family is seeded with at deployment time.
/// Drawn from the <c>Lista_*</c> tables of the Access database this product replaces, so a
/// migrated export resolves against the vocabulary the business actually recruits for:
/// an industrial/metal core (welding certifications, CNC, PLC, CAD) alongside an
/// administrative and IT side.
/// This is a deployment-time seed only; the application never creates catalog values
/// implicitly at runtime when a family is found empty.
/// </summary>
public static class CatalogSeedData
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<CatalogSeedValue>> Families =
        new Dictionary<string, IReadOnlyList<CatalogSeedValue>>(StringComparer.Ordinal)
        {
            [CatalogFamilies.Language] =
            [
                "Inglés", "Francés", "Alemán", "Italiano", "Portugués",
                "Español", "Chino", "Árabe", "Ruso", "Japonés", "Hindi",
            ],

            [CatalogFamilies.Program] =
            [
                // Office and management
                "Excel", "SAP", "AutoCAD", "Navision", "Power BI",
                "Word", "PowerPoint", "Expertis", "ContaPlus", "A3", "Sage 200", "Holded", "Tableau",
                // Development
                "Python", "SQL", "Java", "JavaScript",
                new("C#", "CSHARP"), new("C++", "CPLUSPLUS"),
                "HTML", "CSS", "Git", "GitHub",
                // Engineering and simulation
                "MATLAB", "Simulink", "ANSYS", "SolidWorks", "CATIA", "NX Siemens", "Inventor",
                // Industrial automation and electrical
                "EPLAN", "SCADA", "PLC Siemens", "PLC Allen Bradley",
                // Architecture and 3D
                "Revit", "SketchUp", "3ds Max", "Rhino",
                // Design
                "Photoshop", "Illustrator", "CorelDRAW",
                // CAM and CNC controls
                "MasterCam", "SolidCam", "Fusion 360 CAM",
                "Fanuc", "Siemens CNC", "Heidenhain", "Mazatrol",
            ],

            [CatalogFamilies.Skill] =
            [
                "Gestión documental", "Atención al cliente", "Análisis", "Compras",
                // Industrial
                "Lectura de planos", "Metrología", "Control de calidad",
                "Mantenimiento preventivo", "Mantenimiento correctivo",
                "Soldadura", "Mecanizado", "Montaje industrial", "Carretillero",
                "Prevención de riesgos laborales", "Logística de almacén",
                // Administrative and transversal
                "Contabilidad", "Facturación", "Nóminas", "Selección de personal",
                "Gestión de proyectos", "Negociación", "Liderazgo de equipos", "Trabajo en equipo",
            ],

            [CatalogFamilies.LanguageLevel] = ["A1", "A2", "B1", "B2", "C1", "C2"],
            [CatalogFamilies.ProgramLevel] = ["Básico", "Medio", "Avanzado", "Experto"],
            [CatalogFamilies.SkillLevel] = ["Básico", "Medio", "Alto", "Experto"],

            [CatalogFamilies.EducationType] =
            [
                "Grado", "Máster", "FP", "Curso", "Doctorado",
                "Grado Universitario", "Licenciatura", "Diplomatura", "Ingeniería Técnica",
                "FP Grado superior", "FP Grado medio",
                "Bachillerato", "Educación Secundaria",
                "Certificación oficial", "Certificación profesional",
                // Metal-sector certifications the agency recruits against directly
                "Tarjeta del metal",
                "Soldadura TIG", "Soldadura MIG/MAG", "Soldadura Electrodo Revestido",
                "Soldadura Oxigas", "Soldadura Plasma", "Soldadura Por Puntos", "Soldadura Arco",
            ],

            [CatalogFamilies.EducationStatus] = ["Finalizada", "En curso", "Pendiente"],

            [CatalogFamilies.Sector] =
            [
                "Servicios", "Industria", "Tecnología", "Comercio", "Sanidad", "Educación",
                "Metal", "Automoción", "Construcción", "Logística y transporte", "Energía",
                "Alimentación", "Química", "Aeronáutica", "Naval",
                "Administración pública", "Hostelería", "Banca y seguros",
            ],
        };
}
