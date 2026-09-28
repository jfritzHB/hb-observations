using System.Security.Cryptography;
using System.Text;

namespace FieldApp.Infrastructure.Seeding;

/// <summary>
/// Entirely fictional demo data for local development, manual UX testing and automated tests. Never real
/// customers, projects, people or companies. IDs are deterministic so tests and bookmarks stay stable.
/// </summary>
public static class DemoData
{
    /// <summary>Identity provider name used by the development persona authentication stub.</summary>
    public const string DevelopmentIdentityProvider = "development";

    public static readonly DateTimeOffset SeededAt = new(2026, 1, 5, 15, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<DemoPersona> Personas { get; } =
    [
        new(PersonaKeys.Superintendent, "Owen Lars", "Superintendent", "owen.lars@fieldapp.example",
            "Superintendent on HB-TEST-001 and HB-TEST-002. Cannot see HB-TEST-003 or manage areas."),
        new(PersonaKeys.ProjectManager, "Beru Whitesun", "Project Manager", "beru.whitesun@fieldapp.example",
            "Project Manager on HB-TEST-001 (can manage areas). Inactive membership on HB-TEST-002."),
        new(PersonaKeys.Administrator, "Wedge Antilles", "Administrator", "wedge.antilles@fieldapp.example",
            "Administrator member of all three demo projects."),
        new(PersonaKeys.TradePartner, "Wuher Dunesea", "Trade Partner", "wuher@dunesea-drywall.example",
            "Trade Partner (Dune Sea Drywall Co.) on HB-TEST-001 only. Read-only reference data."),
        new(PersonaKeys.Unassigned, "Biggs Darklighter", "No project access", "biggs.darklighter@fieldapp.example",
            "Provisioned user with no project memberships."),
    ];

    public static Guid UserId(string personaKey) => IdFor($"user:{personaKey}");

    public static Guid ProjectId(string projectNumber) => IdFor($"project:{projectNumber}");

    public static Guid TradeId(string code) => IdFor($"trade:{code}");

    public static Guid CompanyId(string name) => IdFor($"company:{name}");

    public static Guid AreaId(string projectNumber, string path) => IdFor($"area:{projectNumber}:{path}");

    /// <summary>Stable GUID derived from a name (SHA-256, formatted as an RFC 9562 version 8 UUID).</summary>
    public static Guid IdFor(string key)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes("fieldapp-demo:" + key), hash);

        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80); // version 8
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 9562 variant
        return new Guid(bytes, bigEndian: true);
    }

    public static class PersonaKeys
    {
        public const string Superintendent = "superintendent";
        public const string ProjectManager = "project-manager";
        public const string Administrator = "administrator";
        public const string TradePartner = "trade-partner";
        public const string Unassigned = "unassigned";
    }

    public static class ProjectNumbers
    {
        public const string MosEisley = "HB-TEST-001";
        public const string Anchorhead = "HB-TEST-002";
        public const string ToscheStation = "HB-TEST-003";
    }

    public static class Companies
    {
        public const string DuneSeaDrywall = "Dune Sea Drywall Co.";
        public const string TwinSunsPainting = "Twin Suns Painting";
        public const string JundlandElectric = "Jundland Electric";
        public const string BeggarsCanyonPlumbing = "Beggar's Canyon Plumbing";
        public const string DewbackFlooring = "Dewback Flooring";
        public const string MosEspaDoors = "Mos Espa Door & Hardware";
        public const string HothMechanical = "Hoth Climate Mechanical";
        public const string SandcrawlerConcrete = "Sandcrawler Concrete";
        public const string KraytFireproofing = "Krayt Fireproofing";
        public const string JawaGlass = "Jawa Glass & Glazing";
    }
}

public sealed record DemoPersona(string Key, string DisplayName, string RoleLabel, string Email, string Description);
