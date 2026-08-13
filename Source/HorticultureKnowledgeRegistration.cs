using System;
using System.Linq;
using KnowledgeFramework;
using Verse;

namespace HorticultureNovelSeeds
{
    internal static class HorticultureKnowledgeRegistration
    {
        private static HorticultureKnowledgeRegistrationState state = HorticultureKnowledgeRegistrationState.NotAttempted;
        private static HorticultureKnowledgeDiagnosticSnapshot diagnostics = CreateDiagnostic(
            HorticultureKnowledgeRegistrationState.NotAttempted, "none", "Registration has not been attempted.", null, null);
        private static KnowledgeFrameworkReadinessState lastReadinessState;
        private static KnowledgeFrameworkReadinessReason lastReadinessReason;
        private static bool hasAttempted;
        private static string lastLoggedFailure;

        internal static HorticultureKnowledgeRegistrationState State => state;
        internal static HorticultureKnowledgeDiagnosticSnapshot Diagnostics => diagnostics;
        internal static bool IsRegistered => state == HorticultureKnowledgeRegistrationState.Registered;

        internal static bool EnsureRegistered()
        {
            HorticultureKnowledgeCompatibilityReport compatibility = HorticultureKnowledgeCompatibility.Evaluate();
            KnowledgeFrameworkReadinessStatus readiness = compatibility.readiness;
            if (!compatibility.IsReady)
            {
                state = HorticultureKnowledgeRegistrationState.WaitingForFrameworkReadiness;
                diagnostics = CreateDiagnostic(state, "readiness", compatibility.failure, null, readiness);
                return false;
            }
            if (hasAttempted && (state == HorticultureKnowledgeRegistrationState.Incompatible ||
                state == HorticultureKnowledgeRegistrationState.ForeignDomainConflict || state == HorticultureKnowledgeRegistrationState.Failed) &&
                lastReadinessState == readiness.state && lastReadinessReason == readiness.reason)
                return false;
            if (!compatibility.IsCompatible)
            {
                hasAttempted = true;
                lastReadinessState = readiness.state;
                lastReadinessReason = readiness.reason;
                state = HorticultureKnowledgeRegistrationState.Incompatible;
                string message = "Knowledge Framework compatibility check failed. Required=" +
                    compatibility.CapabilitySummary(HorticultureKnowledgeCompatibility.RequiredCapabilities);
                SetFailure(state, "compatibility", message, null, readiness);
                return false;
            }

            if (IsRegistered) return true;
            hasAttempted = true;
            lastReadinessState = readiness.state;
            lastReadinessReason = readiness.reason;
            state = HorticultureKnowledgeRegistrationState.Registering;
            diagnostics = CreateDiagnostic(state, "registration", "Registering the Horticulture domain.", null, readiness);
            try
            {
                KnowledgeDomainRegistration registration = HorticultureKnowledgeAdapter.BuildRegistration();
                KnowledgeRegistrationOptions options = new KnowledgeRegistrationOptions
                {
                    source = HorticultureKnowledgeContract.RegistrationSource,
                    priority = HorticultureKnowledgeContract.RegistrationPriority,
                    conflict = KnowledgeRegistrationConflict.Reject
                };
                KnowledgeDomainRegistrationInspection inspection = KnowledgeConsumerApi.InspectDomainRegistration(registration, options);
                if (inspection != null && inspection.state == KnowledgeDomainRegistrationState.Incompatible &&
                    inspection.ownerRelation == KnowledgeDomainOwnerRelation.SameOwner)
                {
                    if (!KnowledgeRegistry.UnregisterDomain(inspection.domainId, HorticultureKnowledgeContract.RegistrationSource))
                    {
                        state = HorticultureKnowledgeRegistrationState.Failed;
                        SetFailure(state, "schema-migration", "The previous Horticulture-owned domain schema could not be replaced.", null, readiness);
                        return false;
                    }
                    inspection = KnowledgeConsumerApi.InspectDomainRegistration(registration, options);
                }
                if (inspection == null || inspection.state == KnowledgeDomainRegistrationState.RegisteredByOtherOwner ||
                    inspection.state == KnowledgeDomainRegistrationState.Incompatible)
                {
                    state = inspection?.state == KnowledgeDomainRegistrationState.RegisteredByOtherOwner
                        ? HorticultureKnowledgeRegistrationState.ForeignDomainConflict
                        : HorticultureKnowledgeRegistrationState.Incompatible;
                    SetFailure(state, "ownership", "Domain " + HorticultureKnowledgeContract.DomainId + " is not available for Horticulture. Owner=" +
                        (inspection?.registeredOwner ?? "unknown"), null, readiness);
                    return false;
                }
                KnowledgeConsumerRegistrationResult result = KnowledgeConsumerApi.RegisterDomain(registration, options);
                if (result == null || !result.Success)
                {
                    state = result?.code == KnowledgeConsumerRegistrationResultCode.RejectedForeignOwner
                        ? HorticultureKnowledgeRegistrationState.ForeignDomainConflict
                        : result?.code == KnowledgeConsumerRegistrationResultCode.RejectedIncompatible
                            ? HorticultureKnowledgeRegistrationState.Incompatible
                            : result?.code == KnowledgeConsumerRegistrationResultCode.FrameworkUnavailable
                                ? HorticultureKnowledgeRegistrationState.WaitingForFrameworkReadiness
                                : HorticultureKnowledgeRegistrationState.Failed;
                    SetFailure(state, "registration", "Knowledge Framework rejected the Horticulture domain: " + result?.code, null, readiness);
                    return false;
                }
                bool createdDomain = result.code == KnowledgeConsumerRegistrationResultCode.Registered;
                if (!HorticultureKnowledgeMigration.RegisterLegacyAlias())
                {
                    if (createdDomain) KnowledgeRegistry.UnregisterDomain(HorticultureKnowledgeContract.DomainId,
                        HorticultureKnowledgeContract.RegistrationSource);
                    state = HorticultureKnowledgeRegistrationState.Failed;
                    SetFailure(state, "legacy-domain-alias", "The legacy plants domain could not be aliased to " +
                        HorticultureKnowledgeContract.DomainId + ".", null, readiness);
                    return false;
                }
                 if (!HorticultureKnowledgeAdapter.RegisterContexts())
                     throw new InvalidOperationException("Context registration was rejected by Knowledge Framework.");
                 if (!HorticultureKnowledgeAdapter.RegisterRelationsAndComparisons())
                     throw new InvalidOperationException("Relation or comparison registration was rejected by Knowledge Framework.");
                 bool uiAvailable = HorticultureKnowledgeCompatibility.HasOptional(KnowledgeFrameworkApi.UiCapability);
                 if (uiAvailable && !HorticultureKnowledgeAdapter.RegisterUiProvider())
                     throw new InvalidOperationException("Knowledge Framework UI provider registration was rejected.");
                  if (uiAvailable)
                      KnowledgeProviderRegistry.Register(HorticultureKnowledgeContract.ProviderId, HorticultureKnowledgeContract.RegistrationPriority,
                          HorticultureKnowledgeAdapter.BioEntry);
                   KnowledgeInvalidationResult invalidation = KnowledgeConsumerApi.InvalidateDomain(HorticultureKnowledgeContract.DomainId);
                  if (invalidation == null || !invalidation.Success)
                      throw new InvalidOperationException("Initial Horticulture domain invalidation was rejected.");
                 HorticultureKnowledgeEventDiagnostics.BroadInvalidation();
                 HorticultureKnowledgeEventDiagnostics.SubjectCounts(
                     DefDatabase<ThingDef>.AllDefsListForReading.Count(HorticulturePlantPolicy.IsSupported),
                     GameComponent_NovelSeeds.Instance?.AllVarieties?.Count() ?? 0);
                 state = HorticultureKnowledgeRegistrationState.Registered;
                diagnostics = CreateDiagnostic(state, "complete", "Horticulture Knowledge Framework integration is ready.", null, readiness);
                return true;
            }
            catch (Exception exception)
            {
                state = HorticultureKnowledgeRegistrationState.Failed;
                SetFailure(state, "registration", "Horticulture Knowledge Framework registration failed.", exception, readiness);
                return false;
            }
        }

        private static void SetFailure(HorticultureKnowledgeRegistrationState failureState, string phase, string message,
            Exception exception, KnowledgeFrameworkReadinessStatus readiness)
        {
            diagnostics = CreateDiagnostic(failureState, phase, message, exception, readiness);
            string key = failureState + "|" + phase + "|" + message + "|" + exception?.ToString();
            if (key == lastLoggedFailure) return;
            lastLoggedFailure = key;
            Log.Error("Horticulture Knowledge Framework integration " + failureState + " during " + phase + ". " +
                message + " Framework=" + diagnostics.frameworkRelease + " API=" + diagnostics.frameworkApiVersion +
                " Required=" + diagnostics.requiredCapabilities + " Optional=" + diagnostics.optionalCapabilities +
                (exception == null ? string.Empty : "\n" + exception));
        }

        private static HorticultureKnowledgeDiagnosticSnapshot CreateDiagnostic(HorticultureKnowledgeRegistrationState currentState,
            string phase, string message, Exception exception, KnowledgeFrameworkReadinessStatus readiness)
        {
            HorticultureKnowledgeCompatibilityReport compatibility = HorticultureKnowledgeCompatibility.Evaluate();
            return new HorticultureKnowledgeDiagnosticSnapshot(currentState, phase, message, exception?.ToString(),
                KnowledgeFrameworkApi.ReleaseVersion, readiness?.apiVersion ?? KnowledgeFrameworkApi.ApiVersion,
                compatibility.CapabilitySummary(HorticultureKnowledgeCompatibility.RequiredCapabilities),
                compatibility.CapabilitySummary(HorticultureKnowledgeCompatibility.OptionalCapabilities),
                compatibility.CapabilitySummary(HorticultureKnowledgeCompatibility.RequiredCapabilities.Concat(
                    HorticultureKnowledgeCompatibility.OptionalCapabilities)));
        }
    }
}
