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
            "0Harmony",
            "TerrariaHelper",
            "JueMingZ"
        };

        internal static void Check(IList<string> failures)
        {
            Type requestType = typeof(IGameOperationRequest);
            Type outcomeType = typeof(GameOperationOutcome);
            Type resultType = typeof(GameOperationResult);

            if (!requestType.IsPublic ||
                !requestType.IsInterface ||
                requestType.IsGenericType ||
                requestType.GetInterfaces().Length != 0 ||
                requestType.GetMembers().Length != 0)
            {
                failures.Add("IGameOperationRequest must be a public empty marker interface.");
            }

            var expectedOutcomes = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "Rejected", 0 },
                { "Succeeded", 1 },
                { "PartiallySucceeded", 2 },
                { "Failed", 3 },
                { "Cancelled", 4 },
                { "TimedOut", 5 },
                { "Unconfirmed", 6 }
            };
            string[] actualOutcomeNames = Enum.GetNames(outcomeType);
            if (!outcomeType.IsPublic || !outcomeType.IsEnum ||
                Enum.GetUnderlyingType(outcomeType) != typeof(int) ||
                !new HashSet<string>(actualOutcomeNames, StringComparer.Ordinal).SetEquals(expectedOutcomes.Keys) ||
                expectedOutcomes.Any(expected =>
                    !Enum.IsDefined(outcomeType, expected.Key) ||
                    Convert.ToInt32(Enum.Parse(outcomeType, expected.Key, false)) != expected.Value))
            {
                failures.Add("GameOperationOutcome must expose exactly the approved terminal classifications.");
            }

            ConstructorInfo[] constructors = resultType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            ParameterInfo constructorParameter = constructors.Length == 1 && constructors[0].GetParameters().Length == 1
                ? constructors[0].GetParameters()[0]
                : null;
            PropertyInfo[] properties = resultType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            MethodInfo[] methods = resultType.GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
            FieldInfo[] fields = resultType.GetFields(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
            EventInfo[] events = resultType.GetEvents(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Type[] nestedTypes = resultType.GetNestedTypes(BindingFlags.Public);
            if (!resultType.IsPublic || !resultType.IsSealed ||
                resultType.IsGenericType ||
                resultType.BaseType != typeof(object) ||
                resultType.GetInterfaces().Length != 0 ||
                constructors.Length != 1 ||
                constructorParameter == null ||
                constructorParameter.ParameterType != outcomeType ||
                constructorParameter.IsOptional ||
                constructorParameter.HasDefaultValue ||
                properties.Length != 1 ||
                properties[0].Name != "Outcome" ||
                properties[0].PropertyType != outcomeType ||
                !properties[0].CanRead ||
                properties[0].CanWrite ||
                methods.Length != 1 ||
                methods[0] != properties[0].GetGetMethod() ||
                fields.Length != 0 ||
                events.Length != 0 ||
                nestedTypes.Length != 0)
            {
                failures.Add("GameOperationResult must be sealed and expose only an immutable Outcome set by its constructor.");
            }

            Type[] publicTypes = resultType.Assembly.GetExportedTypes();
            var expectedPublicTypes = new HashSet<Type> { requestType, outcomeType, resultType };
            if (!expectedPublicTypes.SetEquals(publicTypes))
            {
                failures.Add("Platform public API must contain only the minimal typed operation contract.");
            }

            foreach (GameOperationOutcome outcome in Enum.GetValues(outcomeType))
            {
                CheckOutcomeRoundTrip(outcome, failures);
            }
            CheckUndefinedOutcomeRejected((GameOperationOutcome)(-1), failures);
            CheckUndefinedOutcomeRejected((GameOperationOutcome)7, failures);

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

        private static void CheckUndefinedOutcomeRejected(GameOperationOutcome outcome, IList<string> failures)
        {
            try
            {
                new GameOperationResult(outcome);
                failures.Add("GameOperationResult accepted an undefined outcome value: " + (int)outcome);
            }
            catch (ArgumentOutOfRangeException)
            {
                // The minimal result contract accepts only one of the seven defined terminal outcomes.
            }
            catch (Exception exception)
            {
                failures.Add(
                    "GameOperationResult rejected an undefined outcome with the wrong exception: " +
                    exception.GetType().Name);
            }
        }
    }
}
