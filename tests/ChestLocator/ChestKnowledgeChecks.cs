using System;
using System.Collections.Generic;
using System.Reflection;

namespace JueMingR.ArchitectureTests
{
    internal static class ChestKnowledgeChecks
    {
        internal static void Check(List<string> failures)
        {
            Type type = Assembly.Load("JueMingR.Features").GetType("JueMingR.Features.ChestLocator.ChestKnowledge");
            if (type == null) { failures.Add("G04 ordinary-client chest completeness observation missing"); return; }
            object owner = Activator.CreateInstance(type), chest = new object();
            Func<string, object[], object> call = (method, args) => type.GetMethod(method).Invoke(owner, args);
            Func<object, bool> complete = identity => (bool)call("Complete", new object[] { 7, identity, 10, 20 });
            if (complete(chest)) failures.Add("G04 allocated chest must not imply content received");
            call("Capacity", new object[] { 7, chest, 10, 20, 3, 100L });
            call("Slot", new object[] { 7, chest, 0, 101L }); call("Slot", new object[] { 7, chest, 0, 102L }); call("Slot", new object[] { 7, chest, 2, 103L });
            if (complete(chest)) failures.Add("G04 duplicate slots must not replace missing slot evidence");
            call("Slot", new object[] { 7, chest, 1, 104L });
            if (!complete(chest) || complete(new object())) failures.Add("G04 all received slots bind to exact chest identity");
            long before = (long)call("Revision", new object[] { 7 }); call("Slot", new object[] { 7, chest, 1, 105L });
            if ((long)call("Revision", new object[] { 7 }) == before) failures.Add("G04 new slot update must invalidate old result even when already complete");
            call("Capacity", new object[] { 7, chest, 10, 20, 2, 106L });
            if (complete(chest)) failures.Add("G04 new capacity receipt starts a new coverage sequence");
            call("Clear", new object[0]);
            if (complete(chest)) failures.Add("G04 reconnect must discard observed content knowledge");
            call("Capacity", new object[] { 7, chest, 10, 20, 257, 200L });
            for (int i = 0; i < 256; i++) call("Slot", new object[] { 7, chest, i, 201L });
            if (complete(chest)) failures.Add("G04 byte slot protocol cannot prove 257 slots");
        }
    }
}
