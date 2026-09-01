using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Platform.Operations;

namespace JueMingR.ArchitectureTests
{
    internal static class OperationContractChecks
    {
        private static readonly string[] ForbiddenAssemblyPrefixes =
        {
            "Terraria",
            "ReLogic",
            "Microsoft.Xna.Framework",
            "0Harmony"
        };

        internal static void Check(IList<string> failures)
        {
            Type requestType = typeof(IGameOperationRequest);
            Type outcomeType = typeof(GameOperationOutcome);
            Type resultType = typeof(GameOperationResult);

            if (!requestType.IsPublic || !requestType.IsInterface || requestType.GetMembers().Length != 0)
            {
                failures.Add("IGameOperationRequest must be a public empty marker interface.");
            }

            string[] expectedOutcomes =
            {
                "Rejected",
                "Succeeded",
                "PartiallySucceeded",
                "Failed",
                "Cancelled",
                "TimedOut",
                "Unconfirmed"
            };
            if (!outcomeType.IsPublic || !outcomeType.IsEnum ||
                !new HashSet<string>(Enum.GetNames(outcomeType), StringComparer.Ordinal).SetEquals(expectedOutcomes))
            {
                failures.Add("GameOperationOutcome must expose exactly the approved terminal classifications.");
            }

            ConstructorInfo[] constructors = resultType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo[] properties = resultType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (!resultType.IsPublic || !resultType.IsSealed ||
                constructors.Length != 1 ||
                constructors[0].GetParameters().Length != 1 ||
                constructors[0].GetParameters()[0].ParameterType != outcomeType ||
                properties.Length != 1 ||
                properties[0].Name != "Outcome" ||
                properties[0].PropertyType != outcomeType ||
                !properties[0].CanRead ||
                properties[0].CanWrite)
            {
                failures.Add("GameOperationResult must be sealed and expose only an immutable Outcome set by its constructor.");
            }

            Type[] publicTypes = resultType.Assembly.GetExportedTypes();
            var expectedPublicTypes = new HashSet<Type> { requestType, outcomeType, resultType };
            if (!expectedPublicTypes.SetEquals(publicTypes))
            {
                failures.Add("Platform public API must contain only the minimal typed operation contract.");
            }

            CheckOutcomeRoundTrip(GameOperationOutcome.Failed, failures);
            CheckOutcomeRoundTrip(GameOperationOutcome.Unconfirmed, failures);

            foreach (AssemblyName reference in resultType.Assembly.GetReferencedAssemblies())
            {
                if (ForbiddenAssemblyPrefixes.Any(prefix => reference.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    failures.Add("Platform operation contract must not reference a game assembly: " + reference.Name);
                }
            }
        }

        private static void CheckOutcomeRoundTrip(GameOperationOutcome outcome, IList<string> failures)
        {
            try
            {
                var result = new GameOperationResult(outcome);
                if (result.Outcome != outcome)
                {
                    failures.Add("GameOperationResult must preserve the constructor outcome: " + outcome);
                }
            }
            catch (Exception exception)
            {
                failures.Add("GameOperationResult rejected an approved outcome " + outcome + ": " + exception.GetType().Name);
            }
        }
    }
}
