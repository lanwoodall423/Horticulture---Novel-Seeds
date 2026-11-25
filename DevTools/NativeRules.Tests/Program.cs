using HorticultureNovelSeeds;
using System.Text.Json;

static void Require(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    Console.WriteLine("PASS " + label);
}
var facts = new CultivationFacts();
Require(facts.firstObservedTick == -1 && facts.minYield == -1 && facts.harvests == 0, "fresh journal unknown");
Require(!facts.RecordHarvest(10, "failed", 0) && facts.harvests == 0, "failed harvest excluded");
Require(!facts.RecordSowing(-1, "invalid") && !facts.RecordDiscovery(10, ""), "invalid input excluded");
Require(facts.RecordSowing(20, "plant:1") && !facts.RecordSowing(20, "plant:1") && facts.sowings == 1, "duplicate sow rejected");
Require(facts.RecordHarvest(40, "plant:1:cycle:1", 8) && facts.RecordHarvest(50, "plant:1:cycle:2", 12), "distinct harvest cycles");
Require(!facts.RecordHarvest(50, "plant:1:cycle:2", 12) && facts.harvests == 2, "duplicate harvest rejected");
Require(facts.firstObservedTick == 20 && facts.firstHarvestTick == 40 && facts.lastHarvestTick == 50 && facts.minYield == 8 && facts.maxYield == 12, "real observed range and dates");
var options = new JsonSerializerOptions { IncludeFields = true };
var restored = JsonSerializer.Deserialize<CultivationFacts>(JsonSerializer.Serialize(facts, options), options);
restored.Normalize();
Require(!restored.RecordHarvest(50, "plant:1:cycle:2", 12) && restored.harvests == 2, "serialized recent-event identity");
Require(restored.RecordDiscovery(10, "document:1") && restored.firstObservedTick == 10 && restored.harvests == 2, "documentation does not invent harvest");
for (int i = 0; i < 100; i++) restored.RecordSowing(100 + i, "sow:" + i);
Require(restored.recentEvents.Count == CultivationFacts.RecentLimit && !restored.RecordSowing(200, "sow:99"), "bounded recent-event window");
restored.sowings = int.MaxValue;
Require(restored.RecordSowing(201, "saturation") && restored.sowings == int.MaxValue, "counter saturation");
restored.recentEvents = new List<string> { null, "", "a", "a", new string('x', 257) };
restored.Normalize();
Require(restored.recentEvents.SequenceEqual(new[] { "a" }), "corrupt duplicate identities normalized");
Console.WriteLine("Native rule tests passed. JSON round-trip is not RimWorld Scribe evidence.");
