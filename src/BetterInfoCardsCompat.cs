using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace StoringWhat
{
    /// <summary>
    /// Better Info Cards (BIC) merges hover cards of identical objects into one ("Storage Bin x 3").
    /// It tells lines apart by their status item ID, and lines of an ID without a "converter" by their
    /// exact text, so two bins holding different amounts would not merge.
    ///
    /// For each item type we register a converter with BIC's public reflection hook
    /// (ConverterManager.AddConverterReflect) that reads the amount from the line's data and, on a
    /// merged card, shows the total: "Algae: 900 kg (Σ)".
    ///
    /// Without BIC installed this does nothing; the lines are ordinary status items either way.
    /// </summary>
    internal static class BetterInfoCardsCompat
    {
        private const string SumSuffix = " <color=#ababab>(Σ)</color>";

        private static MethodInfo addConverter;
        private static bool resolved;
        private static bool failed;
        private static readonly HashSet<string> registered = new HashSet<string>();

        private static bool Resolve()
        {
            if (resolved) return addConverter != null;
            resolved = true;
            var type = AccessTools.TypeByName("BetterInfoCards.ConverterManager");
            if (type == null) return false;
            addConverter = AccessTools.Method(type, "AddConverterReflect");
            if (addConverter == null)
                Debug.LogWarning("[StoringWhat] Better Info Cards found, but not its converter hook; cards will merge only when amounts match.");
            else
                Debug.Log("[StoringWhat] Better Info Cards found: merged cards will total stored amounts.");
            return addConverter != null;
        }

        internal static void RegisterItem(string statusId, string name, AmountKind kind)
        {
            if (failed || registered.Contains(statusId) || !Resolve()) return;
            registered.Add(statusId);
            try
            {
                Func<object, float> getValue = data => (data as StoredLine)?.amount ?? 0f;
                Func<string, List<float>, string> getTextOverride = (original, amounts) =>
                    StoredItemsDisplay.FormatLine(name, kind, amounts.Sum()) + SumSuffix;
                addConverter.Invoke(null, new object[] { statusId, getValue, getTextOverride, null });
            }
            catch (Exception e)
            {
                // A duplicate name is harmless (another copy already registered it); anything else: stop trying.
                var inner = (e as TargetInvocationException)?.InnerException ?? e;
                if (!inner.Message.Contains("already present"))
                {
                    failed = true;
                    Debug.LogWarning("[StoringWhat] could not register with Better Info Cards: " + inner);
                }
            }
        }
    }
}
