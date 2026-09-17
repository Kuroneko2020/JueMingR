using System;
using System.Collections.Generic;

namespace JueMingR.Platform.ItemCatalog
{
    public enum RelationKind { Recipe, NpcDrop, Container, Shop, Fishing, World, Shimmer }
    public sealed class RelationIngredient
    {
        public IReadOnlyList<int> Types { get; }
        public int Count { get; }
        public string Label { get; }
        public RelationIngredient(int[] types, int count, string label = "")
        { if (types == null || types.Length == 0 || count <= 0) throw new ArgumentException("Invalid ingredient"); Types = Array.AsReadOnly((int[])types.Clone()); Count = count; Label = label ?? ""; }
    }
    public sealed class ItemRelation
    {
        public string Id { get; }
        public RelationKind Kind { get; }
        public int Output { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public string Title { get; }
        public string Conditions { get; }
        public IReadOnlyList<RelationIngredient> Ingredients { get; }
        public int Station { get; }
        public string StationName { get; }
        public ItemRelation(string id, RelationKind kind, int output, int minimum, int maximum, string title, string conditions,
            RelationIngredient[] ingredients, int station = 0, string stationName = "")
        {
            Id = id; Kind = kind; Output = output; Minimum = minimum; Maximum = maximum; Title = title ?? ""; Conditions = conditions ?? "";
            Ingredients = Array.AsReadOnly((RelationIngredient[])(ingredients ?? new RelationIngredient[0]).Clone()); Station = station; StationName = stationName ?? "";
        }
    }
}
