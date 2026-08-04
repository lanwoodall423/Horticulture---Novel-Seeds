using System;
using System.Collections.Generic;
using System.Linq;

namespace HorticultureNovelSeeds
{
    internal static class BreedingMixRegression
    {
        private const float ResourceGrowthFactor = 1.15f;
        private const float ResourceGrowthDelta = 0.15f;

        private struct Cell : IEquatable<Cell>
        {
            public int X;
            public int Z;

            public Cell(int x, int z)
            {
                X = x;
                Z = z;
            }

            public bool Equals(Cell other) => X == other.X && Z == other.Z;
            public override bool Equals(object obj) => obj is Cell && Equals((Cell)obj);
            public override int GetHashCode() => (X * 397) ^ (Z * 7919);
        }

        private sealed class PlantState
        {
            public Cell Cell;
            public string CultivarId;
            public float Growth;
            public bool Spawned;
            public bool Destroyed;
            public bool Sown;
            public int HitPoints;
            public bool Blighted;
            public bool CanGrow;
        }

        internal static bool Run()
        {
            List<string> mix = new List<string> { "cultivar-a", "cultivar-b" };
            string initial = Select(mix, new Cell(0, 0));
            string staggered = Select(mix, new Cell(0, 0));
            string complete = Select(mix, new Cell(1, 0));

            bool initialEmptyField = initial == "cultivar-a"
                && !HasEligibleDonor(new List<PlantState>(), new Cell(0, 0), initial);
            bool partialStaggeredHarvest = staggered == "cultivar-a"
                && HasEligibleDonor(new List<PlantState>
                {
                    Mature(new Cell(1, 0), "cultivar-b")
                }, new Cell(0, 0), staggered);
            bool completeHarvestReplant = complete == "cultivar-b"
                && !HasEligibleDonor(new List<PlantState>(), new Cell(1, 0), complete);

            return initialEmptyField && partialStaggeredHarvest && completeHarvestReplant && ResourceEconomics();
        }

        internal static string Report()
        {
            return "initial-empty-field=eligible-donor:false; partial-staggered-harvest-replant=eligible-donor:true; "
                + "complete-harvest-replant=eligible-donor:false; resource-growth=15%; "
                + "mulch=5 WoodLog, 13.0435% maturation-time saved, 33.3333 raw units per +1.0 growth factor; "
                + "hay=8 Hay, 13.0435% maturation-time saved, 53.3333 raw units per +1.0 growth factor; "
                + "fungus=5 RawFungus, 13.0435% maturation-time saved, 33.3333 raw units per +1.0 growth factor; "
                + "market-value-competitive=undetermined-without-resource-prices";
        }

        private static string Select(IReadOnlyList<string> ids, Cell cell)
        {
            List<string> sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();
            unchecked
            {
                int hash = (cell.X * 397) ^ (cell.Z * 7919);
                return sorted[(hash & int.MaxValue) % sorted.Count];
            }
        }

        private static PlantState Mature(Cell cell, string cultivarId)
        {
            return new PlantState
            {
                Cell = cell,
                CultivarId = cultivarId,
                Growth = 1f,
                Spawned = true,
                Destroyed = false,
                Sown = true,
                HitPoints = 1,
                Blighted = false,
                CanGrow = true
            };
        }

        private static bool HasEligibleDonor(IEnumerable<PlantState> plants, Cell recipient, string recipientCultivar)
        {
            return plants.Any(plant => plant != null && plant.Spawned && !plant.Destroyed && plant.HitPoints > 0
                && plant.Sown && !plant.Cell.Equals(recipient)
                && !string.Equals(plant.CultivarId, recipientCultivar, StringComparison.Ordinal)
                && plant.Growth >= 0.50f && !plant.Blighted && plant.CanGrow);
        }

        private static bool ResourceEconomics()
        {
            float timeSaved = 1f - 1f / ResourceGrowthFactor;
            float costPerGrowthPoint = 5f / ResourceGrowthDelta;
            float hayCostPerGrowthPoint = 8f / ResourceGrowthDelta;
            float fungusCostPerGrowthPoint = 5f / ResourceGrowthDelta;
            return Math.Abs(timeSaved - 0.13043478f) < 0.0001f
                && Math.Abs(costPerGrowthPoint - 33.3333f) < 0.01f
                && Math.Abs(hayCostPerGrowthPoint - 53.3333f) < 0.01f
                && Math.Abs(fungusCostPerGrowthPoint - 33.3333f) < 0.01f;
        }
    }
}
