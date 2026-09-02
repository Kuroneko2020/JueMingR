using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Platform.Operations;

namespace JueMingR.ArchitectureTests
{
    internal static class OperationContractChecks
    {
        private static readonly KeyValuePair<string, int>[] ExpectedOutcomes =
        {
            new KeyValuePair<string, int>("Rejected", 0),
            new KeyValuePair<string, int>("Succeeded", 1),
            new KeyValuePair<string, int>("PartiallySucceeded", 2),
            new KeyValuePair<string, int>("Failed", 3),
            new KeyValuePair<string, int>("Cancelled", 4),
            new KeyValuePair<string, int>("TimedOut", 5),
            new KeyValuePair<string, int>("Unconfirmed", 6)
        };

        internal static void Check(IList<string> failures)
        {
            Type requestType = typeof(IGameOperationRequest);
            Type outcomeType = typeof(GameOperationOutcome);
            Type resultType = typeof(GameOperationResult);

            CheckExportedTypes(resultType.Assembly, requestType, outcomeType, resultType, failures);
            CheckRequestType(requestType, failures);
            CheckOutcomeType(outcomeType, failures);
            CheckResultType(resultType, outcomeType, failures);
        }

        private static void CheckExportedTypes(
            Assembly assembly,
            Type requestType,
            Type outcomeType,
            Type resultType,
            IList<string> failures)
        {
            var expected = new HashSet<Type> { requestType, outcomeType, resultType };
            if (!expected.SetEquals(assembly.GetExportedTypes()))
            {
                failures.Add("Platform must export only the minimal typed operation contract.");
            }
        }

        private static void CheckRequestType(Type requestType, IList<string> failures)
        {
            MemberInfo[] members = requestType.GetMembers(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (!requestType.IsPublic ||
                !requestType.IsInterface ||
                requestType.IsGenericType ||
                requestType.GetInterfaces().Length != 0 ||
                members.Length != 0)
            {
                failures.Add("IGameOperationRequest must be a public empty marker interface.");
            }
        }

        private static void CheckOutcomeType(Type outcomeType, IList<string> failures)
        {
            bool matches = outcomeType.IsPublic &&
                outcomeType.IsEnum &&
                Enum.GetUnderlyingType(outcomeType) == typeof(int) &&
                Enum.GetNames(outcomeType).SequenceEqual(ExpectedOutcomes.Select(item => item.Key)) &&
                ExpectedOutcomes.All(item =>
                    Enum.IsDefined(outcomeType, item.Key) &&
                    Convert.ToInt32(Enum.Parse(outcomeType, item.Key, false)) == item.Value);
            if (!matches)
            {
                failures.Add("GameOperationOutcome must expose exactly the seven approved terminal outcomes.");
            }
        }

        private static void CheckResultType(Type resultType, Type outcomeType, IList<string> failures)
        {
            ConstructorInfo[] constructors = resultType.GetConstructors(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            ParameterInfo[] parameters = constructors.Length == 1
                ? constructors[0].GetParameters()
                : new ParameterInfo[0];
            PropertyInfo[] properties = resultType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            MethodInfo[] methods = resultType.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            FieldInfo[] fields = resultType.GetFields(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            EventInfo[] events = resultType.GetEvents(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

            bool matches = resultType.IsPublic &&
                resultType.IsSealed &&
                !resultType.IsGenericType &&
                resultType.BaseType == typeof(object) &&
                resultType.GetInterfaces().Length == 0 &&
                constructors.Length == 1 &&
                parameters.Length == 1 &&
                parameters[0].ParameterType == outcomeType &&
                !parameters[0].IsOptional &&
                !parameters[0].HasDefaultValue &&
                properties.Length == 1 &&
                properties[0].Name == "Outcome" &&
                properties[0].PropertyType == outcomeType &&
                properties[0].GetGetMethod() != null &&
                properties[0].GetSetMethod(true) == null &&
                methods.Length == 1 &&
                methods[0] == properties[0].GetGetMethod() &&
                fields.Length == 0 &&
                events.Length == 0 &&
                resultType.GetNestedTypes(BindingFlags.Public).Length == 0;
            if (!matches)
            {
                failures.Add("GameOperationResult must expose only an immutable Outcome set by its constructor.");
            }
        }
    }
}
