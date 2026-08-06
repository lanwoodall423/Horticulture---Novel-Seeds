using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HorticultureNovelSeeds
{
    /// <summary>Stable identifiers and diagnostics for the Horticulture Knowledge Framework boundary.</summary>
    public static class HorticultureKnowledgeContract
    {
        public const string PackageId = "lan.horticulture.novelseeds";
        public const string ConsumerId = PackageId;
        public const string DomainId = PackageId + ".plants";
        public const string LegacyDomainId = "plants";
        public const string RegistrationSource = PackageId;
        public const string LegacyRegistrationSource = "horticulture.v3";
        public const string ProviderId = PackageId + ".provider";
        // Existing expertise records use this identifier; keep it stable while centralizing it.
        public const string ExpertiseNamespace = "horticulture.fieldcraft";
        public const string MigrationNamespace = PackageId + ".migration";
        public const string MigrationId = MigrationNamespace + ".legacy-domain";
        public const string LegacyMigrationId = "horticulture.v3.legacy";
        public const int MigrationVersion = 1;
        public const int RegistrationPriority = 30;

        public static string HorticultureVersion
        {
            get
            {
                Assembly assembly = typeof(HorticultureKnowledgeContract).Assembly;
                AssemblyInformationalVersionAttribute attribute = assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                    .OfType<AssemblyInformationalVersionAttribute>().FirstOrDefault();
                return attribute?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
            }
        }
    }

    public enum HorticultureKnowledgeRegistrationState
    {
        NotAttempted,
        WaitingForFrameworkReadiness,
        Registering,
        Registered,
        Incompatible,
        ForeignDomainConflict,
        Failed
    }

    /// <summary>Immutable integration diagnostics suitable for the Horticulture diagnostics bridge.</summary>
    public sealed class HorticultureKnowledgeDiagnosticSnapshot
    {
        public readonly HorticultureKnowledgeRegistrationState state;
        public readonly string phase;
        public readonly string message;
        public readonly string exception;
        public readonly string frameworkRelease;
        public readonly int frameworkApiVersion;
        public readonly string horticultureVersion;
        public readonly string requiredCapabilities;
        public readonly string optionalCapabilities;
        public readonly string capabilityVersions;

        internal HorticultureKnowledgeDiagnosticSnapshot(HorticultureKnowledgeRegistrationState state, string phase,
            string message, string exception, string frameworkRelease, int frameworkApiVersion, string requiredCapabilities,
            string optionalCapabilities, string capabilityVersions)
        {
            this.state = state;
            this.phase = phase ?? string.Empty;
            this.message = message ?? string.Empty;
            this.exception = exception ?? string.Empty;
            this.frameworkRelease = frameworkRelease ?? string.Empty;
            this.frameworkApiVersion = frameworkApiVersion;
            horticultureVersion = HorticultureKnowledgeContract.HorticultureVersion;
            this.requiredCapabilities = requiredCapabilities ?? string.Empty;
            this.optionalCapabilities = optionalCapabilities ?? string.Empty;
            this.capabilityVersions = capabilityVersions ?? string.Empty;
        }

        public bool IsUsable => state == HorticultureKnowledgeRegistrationState.Registered;

        public override string ToString() => state + " phase=" + phase + " framework=" + frameworkRelease + " api=" + frameworkApiVersion +
            " horticulture=" + horticultureVersion + " capabilities=" + capabilityVersions + " " + message;
    }
}
