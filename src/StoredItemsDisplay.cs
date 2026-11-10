using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StoringWhat
{
    /// <summary>How an item type's amount is measured and written.</summary>
    internal enum AmountKind { Mass, Units, Calories }

    /// <summary>One item type stored in one building. Passed as the status item's data.</summary>
    internal sealed class StoredLine
    {
        public Tag tag;
        public string name;
        public AmountKind kind;
        public float amount;
        public float mass; // for sorting
        public string text;
        public Guid handle;
    }

    /// <summary>
    /// Lists a building's stored items as status items, one line per item type:
    /// "Algae: 300 kg". Status items are drawn on the hover info card (and in the info
    /// panel's status list) by the game itself, so Better Info Cards sees them like any other line.
    ///
    /// Each item type has its own status item (ID "StoringWhat_&lt;tag&gt;"), which lets Better Info
    /// Cards group buildings storing the same things and add their amounts together.
    /// </summary>
    public sealed class StoredItemsDisplay : KMonoBehaviour, ISim200ms
    {
        private const string Prefix = "StoringWhat_";

        // ------------------------------------------------------------------ attaching

        /// <summary>Component type names of buildings whose job is storing things.</summary>
        private static readonly HashSet<string> StorageBuildingTypes = new HashSet<string>
        {
            "StorageLocker", "StorageLockerSmart", "Refrigerator", "RationBox",
            "Reservoir", "SmartReservoir", "ObjectDispenser", "StorageTile",
            "CargoBay", "CargoBayCluster", "ModularLaunchpadPort", "TreeFilterable",
        };

        private static FieldInfo showInUIField;
        private static bool resolved;

        /// <summary>Called for every building as it spawns.</summary>
        internal static void TryAttach(GameObject go)
        {
            try
            {
                if (go == null || go.GetComponent<Storage>() == null || go.GetComponent<KSelectable>() == null)
                    return;
                bool storageBuilding = IsStorageBuilding(go);
                if (!storageBuilding && StoringWhatOptions.Current.Scope != BuildingScope.AllWithStorage)
                    return;
                var display = go.AddOrGet<StoredItemsDisplay>();
                display.isStorageBuilding = storageBuilding;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StoringWhat] could not attach to " + go + ": " + e);
            }
        }

        private static bool IsStorageBuilding(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
                if (c != null && StorageBuildingTypes.Contains(c.GetType().Name))
                    return true;
            return false;
        }

        /// <summary>Storage.showInUI: false for purely internal storages. Missing field = shown.</summary>
        private static bool ShownInUI(Storage s)
        {
            if (!resolved)
            {
                resolved = true;
                showInUIField = AccessTools.Field(typeof(Storage), "showInUI");
            }
            if (showInUIField == null) return true;
            try { return (bool)showInUIField.GetValue(s); }
            catch { return true; }
        }

        // ------------------------------------------------------------------ status items

        private static readonly Dictionary<Tag, StatusItem> itemStatuses = new Dictionary<Tag, StatusItem>();
        private static StatusItem moreStatus;
        private static StatusItem emptyStatus;

        private static StatusItem MakeStatus(string id)
        {
            var item = new StatusItem(id, "BUILDING", "", StatusItem.IconType.Info, NotificationType.Neutral,
                false, OverlayModes.None.ID, showWorldIcon: false);
            item.resolveStringCallback = (str, data) => (data as StoredLine)?.text ?? str;
            item.resolveTooltipCallback = (str, data) =>
            {
                var line = data as StoredLine;
                return line == null ? str : line.text + "\n\nWhat this building is holding right now.";
            };
            return item;
        }

        private static StatusItem StatusFor(StoredLine line)
        {
            if (!itemStatuses.TryGetValue(line.tag, out var item))
            {
                string id = Prefix + line.tag.Name;
                item = MakeStatus(id);
                itemStatuses[line.tag] = item;
                BetterInfoCardsCompat.RegisterItem(id, line.name, line.kind);
            }
            return item;
        }

        private static StatusItem MoreStatus => moreStatus ?? (moreStatus = MakeStatus(Prefix + "More"));
        private static StatusItem EmptyStatus => emptyStatus ?? (emptyStatus = MakeStatus(Prefix + "Empty"));

        internal static string FormatAmount(AmountKind kind, float amount)
        {
            switch (kind)
            {
                case AmountKind.Calories: return GameUtil.GetFormattedCalories(amount);
                case AmountKind.Units: return GameUtil.GetFormattedUnits(amount);
                default: return GameUtil.GetFormattedMass(amount);
            }
        }

        internal static string FormatLine(string name, AmountKind kind, float amount)
        {
            return name + ": " + FormatAmount(kind, amount);
        }

        // ------------------------------------------------------------------ per building

        [NonSerialized] public bool isStorageBuilding;

        private Storage[] storages;
        private KSelectable selectable;
        private bool dirty = true;
        private int ticks;

        // Lines currently shown, in display order.
        private readonly List<StoredLine> shown = new List<StoredLine>();
        private readonly StoredLine extra = new StoredLine(); // "+N more" or "Empty"
        private StatusItem extraStatus;

        // Every item type this building has held (one line object each, reused), and this refresh's result.
        private readonly Dictionary<Tag, StoredLine> lines = new Dictionary<Tag, StoredLine>();
        private readonly List<StoredLine> sorted = new List<StoredLine>();

        protected override void OnSpawn()
        {
            base.OnSpawn();
            selectable = GetComponent<KSelectable>();
            var all = GetComponents<Storage>();
            var visible = new List<Storage>();
            foreach (var s in all)
                if (s != null && ShownInUI(s)) visible.Add(s);
            storages = visible.Count > 0 ? visible.ToArray() : all;
            Subscribe((int)GameHashes.OnStorageChange, OnStorageChange);
            Refresh();
        }

        private void OnStorageChange(object _)
        {
            dirty = true;
        }

        public void Sim200ms(float dt)
        {
            // Immediately after a change, and once a second anyway (liquids and gases change mass in place).
            if (dirty || ++ticks >= 5) Refresh();
        }

        private void Refresh()
        {
            dirty = false;
            ticks = 0;
            if (selectable == null || storages == null) return;
            try
            {
                Collect();
                Show();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StoringWhat] refresh failed on " + gameObject.name + ": " + e);
            }
        }

        /// <summary>Add up every stored item by type into <see cref="sorted"/>, most mass first.</summary>
        private void Collect()
        {
            foreach (var line in lines.Values) { line.amount = 0f; line.mass = 0f; }

            foreach (var storage in storages)
            {
                if (storage == null || storage.items == null) continue;
                foreach (var item in storage.items)
                {
                    if (item == null) continue;
                    var id = item.GetComponent<KPrefabID>();
                    var pe = item.GetComponent<PrimaryElement>();
                    if (id == null || pe == null) continue;
                    Tag tag = id.PrefabTag;

                    if (!lines.TryGetValue(tag, out var line))
                    {
                        line = new StoredLine
                        {
                            tag = tag,
                            name = STRINGS.UI.StripLinkFormatting(item.GetProperName()),
                            kind = item.GetComponent<Edible>() != null ? AmountKind.Calories
                                : Assets.IsTagCountable(tag) ? AmountKind.Units
                                : AmountKind.Mass,
                        };
                        lines[tag] = line;
                    }

                    switch (line.kind)
                    {
                        case AmountKind.Calories:
                            var food = item.GetComponent<Edible>();
                            if (food != null) line.amount += food.Calories;
                            break;
                        case AmountKind.Units: line.amount += pe.Units; break;
                        default: line.amount += pe.Mass; break;
                    }
                    line.mass += pe.Mass;
                }
            }

            sorted.Clear();
            foreach (var line in lines.Values)
                if (line.mass > 0f || line.amount > 0f) sorted.Add(line);
            sorted.Sort((a, b) =>
            {
                int c = b.mass.CompareTo(a.mass);
                return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
            });
        }

        /// <summary>Update the status items to match <see cref="sorted"/>.</summary>
        private void Show()
        {
            var opts = StoringWhatOptions.Current;
            int max = Mathf.Clamp(opts.MaxLines, 1, 30);
            int count = Mathf.Min(sorted.Count, max);

            foreach (var line in sorted)
                line.text = FormatLine(line.name, line.kind, line.amount);

            // Same item types in the same order: the text is read live, nothing to re-add.
            bool same = count == shown.Count;
            for (int i = 0; same && i < count; i++)
                same = shown[i] == sorted[i];

            StatusItem wantExtra = null;
            if (sorted.Count > count)
            {
                wantExtra = MoreStatus;
                int rest = sorted.Count - count;
                extra.text = "+" + rest + " more item type" + (rest == 1 ? "" : "s");
            }
            else if (sorted.Count == 0 && isStorageBuilding && opts.ShowEmpty)
            {
                wantExtra = EmptyStatus;
                extra.text = "Stored: nothing";
            }

            if (same && wantExtra == extraStatus) return;

            // Re-add everything so the lines keep their order on the card.
            foreach (var line in shown)
                if (line.handle != Guid.Empty) { selectable.RemoveStatusItem(line.handle); line.handle = Guid.Empty; }
            if (extra.handle != Guid.Empty) { selectable.RemoveStatusItem(extra.handle); extra.handle = Guid.Empty; }
            shown.Clear();

            for (int i = 0; i < count; i++)
            {
                var line = sorted[i];
                line.handle = selectable.AddStatusItem(StatusFor(line), line);
                shown.Add(line);
            }
            extraStatus = wantExtra;
            if (wantExtra != null)
                extra.handle = selectable.AddStatusItem(wantExtra, extra);
        }

        protected override void OnCleanUp()
        {
            if (selectable != null)
            {
                foreach (var line in shown)
                    if (line.handle != Guid.Empty) selectable.RemoveStatusItem(line.handle);
                if (extra.handle != Guid.Empty) selectable.RemoveStatusItem(extra.handle);
            }
            shown.Clear();
            base.OnCleanUp();
        }
    }
}
