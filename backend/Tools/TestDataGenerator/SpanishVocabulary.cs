namespace KeplerTalento.Tools.TestDataGenerator;

/// <summary>
/// The free-text vocabulary the generator draws on. Catalog-backed values are not here —
/// those come from <c>CatalogSeedData</c> so that every generated reference resolves.
/// </summary>
/// <remarks>
/// Names, companies and institutions are invented. They are plausible Spanish text rather
/// than sentinel tokens because the point of this data is to exercise search, sorting and
/// accent folding the way real records will; it is still fabricated, and must never be
/// presented as, or mixed with, real candidate data.
/// </remarks>
public static class SpanishVocabulary
{
    public static readonly string[] FirstNames =
    [
        "Antonio", "María", "Manuel", "Carmen", "José", "Ana", "Francisco", "Laura",
        "David", "Marta", "Javier", "Isabel", "Daniel", "Lucía", "Carlos", "Elena",
        "Miguel", "Cristina", "Rafael", "Patricia", "Pablo", "Sara", "Sergio", "Beatriz",
        "Jorge", "Raquel", "Alberto", "Silvia", "Álvaro", "Nuria", "Iván", "Rocío",
        "Rubén", "Andrea", "Adrián", "Mónica", "Óscar", "Natalia", "Víctor", "Alicia",
        "Íñigo", "Begoña", "Aitor", "Nerea", "Unai", "Ainhoa", "Jon", "Maite",
        "Roberto", "Yolanda", "Gonzalo", "Irene", "Héctor", "Sonia", "Ignacio", "Pilar",
        "Emilio", "Teresa", "Joaquín", "Inmaculada",
    ];

    public static readonly string[] LastNames =
    [
        "García", "Rodríguez", "González", "Fernández", "López", "Martínez", "Sánchez",
        "Pérez", "Gómez", "Martín", "Jiménez", "Ruiz", "Hernández", "Díaz", "Moreno",
        "Álvarez", "Muñoz", "Romero", "Alonso", "Gutiérrez", "Navarro", "Torres",
        "Domínguez", "Vázquez", "Ramos", "Gil", "Ramírez", "Serrano", "Blanco", "Molina",
        "Morales", "Suárez", "Ortega", "Delgado", "Castro", "Ortiz", "Rubio", "Marín",
        "Sanz", "Núñez", "Iglesias", "Medina", "Garrido", "Cortés", "Castillo", "Santos",
        "Lozano", "Guerrero", "Cano", "Prieto", "Méndez", "Cruz", "Calvo", "Gallego",
        "Vidal", "León", "Herrera", "Peña", "Flores", "Cabrera",
    ];

    /// <summary>City paired with the province it belongs to, so filters on either agree.</summary>
    public static readonly (string City, string Province)[] Places =
    [
        ("Madrid", "Madrid"), ("Barcelona", "Barcelona"), ("Valencia", "Valencia"),
        ("Sevilla", "Sevilla"), ("Zaragoza", "Zaragoza"), ("Málaga", "Málaga"),
        ("Bilbao", "Bizkaia"), ("Barakaldo", "Bizkaia"), ("Getxo", "Bizkaia"),
        ("Donostia", "Gipuzkoa"), ("Irún", "Gipuzkoa"), ("Eibar", "Gipuzkoa"),
        ("Vitoria", "Álava"), ("Pamplona", "Navarra"), ("Tudela", "Navarra"),
        ("Logroño", "La Rioja"), ("Santander", "Cantabria"), ("Torrelavega", "Cantabria"),
        ("Gijón", "Asturias"), ("Oviedo", "Asturias"), ("Avilés", "Asturias"),
        ("A Coruña", "A Coruña"), ("Ferrol", "A Coruña"), ("Vigo", "Pontevedra"),
        ("Valladolid", "Valladolid"), ("Burgos", "Burgos"), ("León", "León"),
        ("Salamanca", "Salamanca"), ("Palencia", "Palencia"), ("Miranda de Ebro", "Burgos"),
        ("Alicante", "Alicante"), ("Elche", "Alicante"), ("Castellón", "Castellón"),
        ("Murcia", "Murcia"), ("Cartagena", "Murcia"), ("Almería", "Almería"),
        ("Granada", "Granada"), ("Córdoba", "Córdoba"), ("Cádiz", "Cádiz"),
        ("Jerez de la Frontera", "Cádiz"), ("Huelva", "Huelva"), ("Jaén", "Jaén"),
        ("Sabadell", "Barcelona"), ("Terrassa", "Barcelona"), ("Mataró", "Barcelona"),
        ("Girona", "Girona"), ("Lleida", "Lleida"), ("Tarragona", "Tarragona"),
        ("Badajoz", "Badajoz"), ("Cáceres", "Cáceres"), ("Toledo", "Toledo"),
        ("Guadalajara", "Guadalajara"), ("Albacete", "Albacete"), ("Ciudad Real", "Ciudad Real"),
    ];

    public static readonly string[] Availabilities =
    [
        "Inmediata", "15 días", "Un mes", "Dos meses", "A convenir",
    ];

    public static readonly string[] Sources =
    [
        "Email", "Portal", "Web", "Referido", "Feria de empleo", "LinkedIn", "ETT",
        "Candidatura espontánea",
    ];

    /// <summary>Invented company names, built to read like the sectors the agency serves.</summary>
    public static readonly string[] Companies =
    [
        "Talleres Aranguren", "Mecanizados del Norte", "Industrias Belmonte",
        "Calderería Zubiaga", "Estructuras Metálicas Olarra", "Fundiciones Aguirre",
        "Montajes Industriales Larrea", "Soldaduras Egaña", "Troquelados Uriarte",
        "Automoción Sarriá", "Componentes Ibéricos", "Forjas del Duero",
        "Grupo Mendizábal", "Ingeniería Salcedo", "Precisión Bergara",
        "Utillajes Amézaga", "Hidráulica Peñalba", "Electromecánica Robledo",
        "Naval Astilleros Ría", "Aeronáutica Valdés", "Química Ribera",
        "Alimentaria La Vega", "Logística Trasmiera", "Transportes Zuriaga",
        "Construcciones Herrán", "Obras y Servicios Encinas", "Energías Altamira",
        "Solar Campoamor", "Distribuciones Basterra", "Comercial Mendaro",
        "Clínica San Braulio", "Asesoría Lascuráin", "Consultora Orbaneja",
        "Sistemas Arrieta", "Datos y Redes Villaverde", "Software Ondarreta",
        "Hostelería Miramar", "Seguros Peñarroya", "Financiera Cortabarría",
        "Formación Iturbe",
    ];

    public static readonly string[] Positions =
    [
        "Soldador", "Soldadora TIG", "Calderero", "Mecánico de mantenimiento",
        "Operario de producción", "Fresador CNC", "Tornero CNC", "Programador CNC",
        "Jefe de taller", "Encargado de producción", "Técnico de calidad",
        "Inspector de soldadura", "Delineante", "Proyectista mecánico",
        "Ingeniero de procesos", "Técnico de mantenimiento eléctrico",
        "Programador de PLC", "Automatista", "Montador industrial",
        "Carretillero", "Preparador de pedidos", "Jefe de almacén",
        "Responsable de logística", "Administrativo contable", "Auxiliar administrativo",
        "Responsable de compras", "Técnico de nóminas", "Técnico de selección",
        "Comercial técnico", "Jefe de ventas", "Analista de datos",
        "Desarrollador backend", "Administrador de sistemas", "Técnico de soporte",
        "Gestor de proyectos", "Recepcionista", "Atención al cliente",
    ];

    public static readonly string[] Institutions =
    [
        "Universidad de Deusto", "Universidad del País Vasco", "Universidad de Navarra",
        "Universidad de Zaragoza", "Universidad de Valladolid", "Universidad de Oviedo",
        "Universidad de Cantabria", "Universidad Politécnica de Valencia",
        "Universidad de Sevilla", "Universidad de Granada",
        "CIFP Txurdinaga", "CIFP Don Bosco", "IES Politécnico Easo",
        "Centro de Formación Somorrostro", "Instituto Máquina Herramienta",
        "Centro San Viator", "Salesianos Deusto", "IES Virgen de la Paloma",
        "Centro de Formación Peñascal", "Academia Mondragón",
        "Cámara de Comercio de Bilbao", "Fundación Metal Asturias",
        "Centro Tecnológico Tecnalia", "Escuela de Negocios ESIC",
    ];

    public static readonly string[] Specialties =
    [
        "Mecánica", "Electrónica", "Electricidad", "Organización industrial",
        "Diseño de producto", "Automatización", "Materiales", "Energía",
        "Administración", "Contabilidad", "Recursos humanos", "Marketing",
        "Informática de gestión", "Desarrollo de aplicaciones", "Logística",
        "Calidad y medio ambiente", "Prevención de riesgos", "Comercio internacional",
        "", "", "",
    ];

    public static readonly string[] DegreeTitles =
    [
        "Ingeniería Mecánica", "Ingeniería Eléctrica", "Ingeniería Electrónica Industrial",
        "Ingeniería de Organización Industrial", "Ingeniería Química",
        "Administración y Dirección de Empresas", "Relaciones Laborales",
        "Contabilidad y Finanzas", "Ingeniería Informática", "Comercio y Marketing",
        "Fabricación Mecánica", "Mecatrónica Industrial", "Programación de la Producción",
        "Mantenimiento Electromecánico", "Instalaciones Eléctricas y Automáticas",
        "Diseño en Fabricación Mecánica", "Administración y Finanzas",
        "Gestión Administrativa", "Transporte y Logística", "Soldadura y Calderería",
    ];

    /// <summary>
    /// Note fragments. Roughly a third of rows get no note at all, which is what the real
    /// data looks like and keeps empty-field rendering honest.
    /// </summary>
    public static readonly string[] CandidateNotes =
    [
        "Disponible para turnos rotativos.",
        "Busca estabilidad, no descarta desplazamientos.",
        "Aporta carnet de carretillero en vigor.",
        "Entrevista telefónica realizada, buena impresión.",
        "Pendiente de aportar documentación acreditativa.",
        "Interesado únicamente en jornada continua.",
        "Ha trabajado con nosotros en campañas anteriores.",
        "Requiere preaviso de quince días en su empresa actual.",
        "Vehículo propio, sin problema de movilidad.",
        "Perfil polivalente, encaja en varias vacantes.",
        "Nivel de inglés a confirmar en entrevista, \"lo indica como alto\".",
        "Contactar preferentemente por las tardes.",
        "", "", "", "", "", "",
    ];

    public static readonly string[] RelationNotes =
    [
        "Acreditado documentalmente.",
        "Uso diario en su puesto anterior.",
        "Autoformación, sin certificado.",
        "Pendiente de renovar.",
        "Nivel declarado por el candidato.",
        "", "", "", "", "", "", "",
    ];

    public static readonly string[] Functions =
    [
        "Preparación y soldadura de conjuntos según plano.",
        "Mantenimiento preventivo y correctivo de línea de producción.",
        "Programación y ajuste de centros de mecanizado.",
        "Control dimensional y elaboración de informes de calidad.",
        "Gestión del almacén y preparación de expediciones.",
        "Elaboración de planos y despieces en CAD.",
        "Supervisión de equipo de ocho personas a dos turnos.",
        "Facturación, conciliación bancaria y cierre mensual.",
        "Atención telefónica y gestión de incidencias de clientes.",
        "Desarrollo y mantenimiento de aplicaciones internas.",
        "Negociación con proveedores y seguimiento de pedidos.",
        "Puesta en marcha de instalaciones automatizadas en cliente.",
    ];

    public static readonly string[] Certifications =
    [
        "Cambridge First", "Cambridge Advanced", "TOEIC", "Escuela Oficial de Idiomas",
        "DELF B2", "Goethe-Zertifikat", "", "", "", "", "",
    ];
}
