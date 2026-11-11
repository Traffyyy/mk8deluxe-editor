using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Mk8Editor.Core
{
    [DataContract]
    public sealed class CatalogEntry
    {
        [DataMember] public string Section { get; set; }
        [DataMember] public int Index { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Image { get; set; }
        [DataMember] public string Note { get; set; }
    }
    /// <summary>Explicit category/index mapping from the verified ImHex bookmarks.</summary>
    public static class Catalog
    {
        private static readonly List<CatalogEntry> entries = Load();
        private static List<CatalogEntry> Load()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Mk8Editor.catalog.json"))
            {
                if (stream == null) throw new InvalidOperationException("The embedded catalog was not found.");
                return (List<CatalogEntry>)new DataContractJsonSerializer(typeof(List<CatalogEntry>)).ReadObject(stream);
            }
        }
        public static CatalogEntry Find(string section, int index)
        { return entries.Single(entry => entry.Section == section && entry.Index == index); }
    }
    public static class Statistics
    {
        public static readonly string[] Codes = { "WG", "AC", "ON", "OF", "MT", "SL", "SW", "SA", "SG", "TL", "TW", "TA", "TG", "IV" };
        public static readonly string[] Names = {
            "Weight", "Acceleration", "On-road traction", "Off-road traction", "Mini-turbo",
            "Ground speed", "Water speed", "Anti-gravity speed", "Air speed",
            "Ground handling", "Water handling", "Anti-gravity handling", "Air handling", "Invincibility"
        };
    }
}
