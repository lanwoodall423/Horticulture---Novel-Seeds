using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeFramework;

namespace HorticultureNovelSeeds
{
    internal sealed class HorticultureKnowledgeCompatibilityReport
    {
        internal KnowledgeFrameworkReadinessStatus readiness;
        internal readonly Dictionary<string, int> capabilityVersions = new Dictionary<string, int>(StringComparer.Ordinal);
        internal readonly List<string> missingRequired = new List<string>();
        internal readonly List<string> missingOptional = new List<string>();
        internal string failure;

        internal bool IsReady => readiness?.IsReady == true;
        internal bool IsCompatible => IsReady && missingRequired.Count == 0;

        internal string CapabilitySummary(IEnumerable<string> capabilities)
        {
            return string.Join(", ", (capabilities ?? Enumerable.Empty<string>()).Select(capability => capability + "=" +
                (capabilityVersions.TryGetValue(capability, out int version) ? version.ToString() : "missing")));
        }
    }

    internal static class HorticultureKnowledgeCompatibility
    {
        internal const int MinimumApiGeneration = 3;

        internal static readonly string[] RequiredCapabilities =
        {
            KnowledgeFrameworkApi.TypedMeasurementsCapability,
            KnowledgeFrameworkApi.EvidenceCapability,
            KnowledgeFrameworkApi.ClaimsCapability,
            KnowledgeFrameworkApi.ContextsCapability,
            KnowledgeFrameworkApi.WitnessLearningCapability,
            KnowledgeFrameworkApi.MilestonesCapability,
            KnowledgeFrameworkApi.StructuralRelationsCapability,
            KnowledgeFrameworkApi.ConsumerMigrationCapability,
            KnowledgeFrameworkApi.DomainAliasesCapability,
            KnowledgeFrameworkApi.ReadinessInspectionCapability,
            KnowledgeFrameworkApi.SafeRegistrationCapability,
            KnowledgeFrameworkApi.RegistrationOwnershipCapability,
            KnowledgeFrameworkApi.TargetedInvalidationCapability
        };

        internal static readonly string[] OptionalCapabilities =
        {
            KnowledgeFrameworkApi.UiCapability,
            KnowledgeFrameworkApi.StructuredComparisonCapability,
            KnowledgeFrameworkApi.FilteredTransmissionCapability
        };

        internal static HorticultureKnowledgeCompatibilityReport Evaluate()
        {
            HorticultureKnowledgeCompatibilityReport report = new HorticultureKnowledgeCompatibilityReport
            {
                readiness = KnowledgeConsumerApi.Readiness
            };
            foreach (string capability in RequiredCapabilities.Concat(OptionalCapabilities))
            {
                report.capabilityVersions[capability] = KnowledgeFrameworkApi.CapabilityVersion(capability);
                if (!KnowledgeFrameworkApi.Supports(MinimumApiGeneration, capability))
                {
                    if (RequiredCapabilities.Contains(capability)) report.missingRequired.Add(capability);
                    else report.missingOptional.Add(capability);
                }
            }
            if (!report.IsReady) report.failure = "Knowledge Framework is not ready: " + report.readiness?.reason;
            else if (report.missingRequired.Count > 0) report.failure = "Missing required capabilities: " + report.CapabilitySummary(report.missingRequired);
            return report;
        }

        internal static bool HasOptional(string capability)
        {
            HorticultureKnowledgeCompatibilityReport report = Evaluate();
            return !report.missingOptional.Contains(capability);
        }
    }
}
