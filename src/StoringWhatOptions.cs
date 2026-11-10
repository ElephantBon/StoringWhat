using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace StoringWhat
{
    public enum BuildingScope
    {
        [Option("Storage buildings", "Storage Bins, Refrigerators, Ration Boxes, Reservoirs, Storage Tiles, rocket cargo bays, conveyor loaders, feeders...")]
        StorageBuildings,

        [Option("Every building with storage", "Also machines with internal storage: generators, Electrolyzers, fabricators, and so on.")]
        AllWithStorage,
    }

    /// <summary>
    /// Shown as "Options" next to the mod in the main-menu Mods screen (via PLib).
    /// Saved to config.json in the mod's folder and re-read each time a save is loaded.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class StoringWhatOptions
    {
        [Option("Show on", "Which buildings list their stored items on the hover card.")]
        [JsonProperty]
        public BuildingScope Scope { get; set; }

        [Option("Max item lines", "Item types listed per building. The rest are summed up as \"+N more\".")]
        [Limit(1, 30)]
        [JsonProperty]
        public int MaxLines { get; set; }

        [Option("Show \"Empty\"", "Storage buildings with nothing in them say so.")]
        [JsonProperty]
        public bool ShowEmpty { get; set; }

        public StoringWhatOptions()
        {
            Scope = BuildingScope.StorageBuildings;
            MaxLines = 6;
            ShowEmpty = true;
        }

        private static StoringWhatOptions current;

        /// <summary>Settings as of the last <see cref="Reload"/> (defaults before that).</summary>
        public static StoringWhatOptions Current
        {
            get { return current ?? (current = Read()); }
        }

        /// <summary>Re-read config.json. Called when a save is loaded.</summary>
        public static void Reload()
        {
            current = Read();
        }

        private static StoringWhatOptions Read()
        {
            StoringWhatOptions opts;
            try { opts = POptions.ReadSettings<StoringWhatOptions>(); }
            catch { opts = null; }
            return opts ?? new StoringWhatOptions();
        }
    }
}
