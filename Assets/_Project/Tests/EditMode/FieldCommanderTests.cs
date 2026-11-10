using System.Collections.Generic;
using System.Linq;
using Eldoria.Gameplay;
using Eldoria.Rules;
using Eldoria.Terrain;
using Eldoria.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoria.Tests
{
    public class FieldCommanderTests
    {
        private const string Data = "Assets/_Project/Data";
        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            Turns.ResetCounter();
            Turns.StartNextTurn();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Turns.ResetCounter();
        }

        // ---- The templates as designed ----

        [Test]
        public void FieldCommanderUnitIsOneFieldCommander()
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/FieldCommander.asset");
            Assert.That(unit, Is.Not.Null);
            Assert.That(unit.displayName, Is.EqualTo("Field Commander"));
            Assert.That(unit.Size, Is.EqualTo(1));
            Assert.That(unit.leader.displayName, Is.EqualTo("Field Commander"));
        }

        [Test]
        public void FieldCommanderHasTheDesignedStats()
        {
            var fc = AssetDatabase.LoadAssetAtPath<SoldierTemplate>($"{Data}/Soldiers/FieldCommander.asset");
            Assert.That(fc.health, Is.EqualTo(10));
            Assert.That(fc.armorLevel, Is.EqualTo(1));
            Assert.That(fc.accuracy, Is.EqualTo(75));
            Assert.That(fc.movementRange, Is.EqualTo(10f));
            Assert.That(fc.keywords.Select(k => k.displayName), Is.EquivalentTo(new[] { "Biological", "Officer", "Solo" }));
            Assert.That(fc.keywords.All(k => k.category == KeywordCategory.Trait), Is.True, "all three are Traits");
            Assert.That(fc.weapons.Select(w => w.displayName), Is.EqualTo(new[] { "Pistol" }));
            Assert.That(fc.PrimaryWeapon.displayName, Is.EqualTo("Pistol"));

            var ability = fc.abilities.Single();
            Assert.That(ability.displayName, Is.EqualTo("Lead from the front"));
            Assert.That(ability.kind, Is.EqualTo(AbilityKind.Passive));
            Assert.That(ability.radius, Is.EqualTo(15f));
            Assert.That(ability.affects, Is.EqualTo(AuraAffects.Allies), "friendly soldiers only");
            Assert.That(ability.includeSelf, Is.False, "the Field Commander doesn't benefit");
            var bonus = ability.grantsNearby.interactions.Single();
            Assert.That(bonus.effect, Is.EqualTo(KeywordEffect.AccuracyBonus));
            Assert.That(bonus.amount, Is.EqualTo(15));
        }

        [Test]
        public void InfantrySquadHasTheUpdatedStats()
        {
            var sgt = AssetDatabase.LoadAssetAtPath<SoldierTemplate>($"{Data}/Soldiers/Sergeant.asset");
            var trooper = AssetDatabase.LoadAssetAtPath<SoldierTemplate>($"{Data}/Soldiers/Trooper.asset");
            var pistol = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{Data}/Weapons/Pistol.asset");
            Assert.That(sgt.accuracy, Is.EqualTo(75));
            Assert.That((trooper.health, trooper.armorLevel, trooper.accuracy), Is.EqualTo((5, 1, 75)));
            Assert.That(pistol.accuracyBonus, Is.EqualTo(0));
            Assert.That(AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/InfantrySquad.asset").displayName, Is.EqualTo("Infantry Squad"));
        }

        // ---- Lead from the front ----

        private SoldierTemplate commanderT, trooperT;
        private Keyword led;

        private void MakeTemplates()
        {
            led = ScriptableObject.CreateInstance<Keyword>();
            led.displayName = "Led from the front";
            led.interactions.Add(new KeywordInteraction
            {
                role = KeywordRole.WhenAttacking, condition = KeywordCondition.Always, effect = KeywordEffect.AccuracyBonus, amount = 15
            });
            var ability = ScriptableObject.CreateInstance<Ability>();
            ability.grantsNearby = led;
            ability.radius = 15f;
            ability.affects = AuraAffects.Allies;
            ability.includeSelf = false;
            var rifle = ScriptableObject.CreateInstance<WeaponTemplate>();
            commanderT = ScriptableObject.CreateInstance<SoldierTemplate>();
            commanderT.abilities.Add(ability);
            commanderT.weapons.Add(rifle);
            trooperT = ScriptableObject.CreateInstance<SoldierTemplate>();
            trooperT.accuracy = 60;
            trooperT.weapons.Add(rifle);
            created.AddRange(new Object[] { led, ability, rifle, commanderT, trooperT });
        }

        private Unit MakeUnit(string name, Side side, SoldierTemplate t, params Vector3[] positions)
        {
            var go = new GameObject(name);
            created.Add(go);
            var unit = go.AddComponent<Unit>();
            unit.Configure(null, side, name);
            for (int i = 0; i < positions.Length; i++)
            {
                var s = new GameObject($"{name}_{i}");
                s.transform.SetParent(go.transform);
                s.transform.position = positions[i];
                s.AddComponent<Soldier>().Configure(t, i == 0);
            }
            unit.RefreshSoldiers();
            return unit;
        }

        [Test]
        public void SoldiersWithin15mOfTheirCommanderGainLedFromTheFront()
        {
            MakeTemplates();
            var cmd = MakeUnit("Commander", Side.Player, commanderT, Vector3.zero);
            var squad = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 10), new Vector3(0, 0, 14.9f), new Vector3(0, 0, 16));
            var enemy = MakeUnit("Enemy", Side.AI, trooperT, new Vector3(5, 0, 0));
            NearbyKeywords.RefreshAll();

            Assert.That(squad.Soldiers[0].HasKeyword("Led from the front"), Is.True);
            Assert.That(squad.Soldiers[1].HasKeyword("Led from the front"), Is.True);
            Assert.That(squad.Soldiers[2].HasKeyword("Led from the front"), Is.False, "16 m away");
            Assert.That(enemy.Soldiers[0].HasKeyword("Led from the front"), Is.False, "enemies aren't led");
            Assert.That(cmd.Soldiers[0].HasKeyword("Led from the front"), Is.False, "the commander doesn't benefit");
        }

        [Test]
        public void OnlyTheSoldiersInRangeGetTheBonusNotTheirWholeUnit()
        {
            MakeTemplates();
            MakeUnit("Commander", Side.Player, commanderT, Vector3.zero);
            // One soldier in range, the rest of the unit out of range.
            var oneIn = MakeUnit("OneIn", Side.Player, trooperT, new Vector3(0, 0, 14), new Vector3(0, 0, 17), new Vector3(0, 0, 19));
            // Every soldier but one in range.
            var allButOne = MakeUnit("AllButOne", Side.Player, trooperT, new Vector3(-5, 0, 0), new Vector3(5, 0, 0), new Vector3(0, 0, -20));
            NearbyKeywords.RefreshAll();

            Assert.That(oneIn.Soldiers.Count(s => s.HasKeyword("Led from the front")), Is.EqualTo(1));
            Assert.That(oneIn.Soldiers[0].HasKeyword("Led from the front"), Is.True);
            Assert.That(allButOne.Soldiers.Count(s => s.HasKeyword("Led from the front")), Is.EqualTo(2));
            Assert.That(allButOne.Soldiers[2].HasKeyword("Led from the front"), Is.False);
        }

        [Test]
        public void TheCommandersOwnAttacksGetNoBonus()
        {
            MakeTemplates();
            commanderT.accuracy = 75;
            var cmd = MakeUnit("Commander", Side.Player, commanderT, Vector3.zero);
            MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 3));
            var enemy = MakeUnit("Enemy", Side.AI, trooperT, new Vector3(0, 0, 30));
            NearbyKeywords.RefreshAll();

            var r = UnitShooting.Resolve(cmd, enemy, false, new System.Random(1));
            Assert.That(r.Shots[0].Attack.Accuracy.Final, Is.EqualTo(75));
        }

        [Test]
        public void OfficerAndSoloOnlyMatterWhenSomethingChecksForThem()
        {
            var officer = ScriptableObject.CreateInstance<Keyword>();
            officer.displayName = "Officer";
            var solo = ScriptableObject.CreateInstance<Keyword>();
            solo.displayName = "Solo";
            // e.g. a weapon that only affects Officers, and one that has no effect on Solo soldiers.
            var officersOnly = ScriptableObject.CreateInstance<Keyword>();
            officersOnly.interactions.Add(new KeywordInteraction { party = KeywordParty.Target, condition = KeywordCondition.Lacks, keyword = officer, effect = KeywordEffect.NoEffect });
            var notSolo = ScriptableObject.CreateInstance<Keyword>();
            notSolo.interactions.Add(new KeywordInteraction { party = KeywordParty.Target, condition = KeywordCondition.Has, keyword = solo, effect = KeywordEffect.NoEffect });
            created.AddRange(new Object[] { officer, solo, officersOnly, notSolo });

            var commander = new List<Keyword> { officer, solo };
            var trooper = new List<Keyword>();

            Assert.That(KeywordRules.Evaluate(null, null, commander).NoEffect, Is.False, "the traits do nothing by themselves");
            Assert.That(KeywordRules.Evaluate(null, new List<Keyword> { officersOnly }, commander).NoEffect, Is.False);
            Assert.That(KeywordRules.Evaluate(null, new List<Keyword> { officersOnly }, trooper).NoEffect, Is.True);
            Assert.That(KeywordRules.Evaluate(null, new List<Keyword> { notSolo }, commander).NoEffect, Is.True);
            Assert.That(KeywordRules.Evaluate(null, new List<Keyword> { notSolo }, trooper).NoEffect, Is.False);
        }

        [Test]
        public void LedFromTheFrontAdds15Accuracy()
        {
            MakeTemplates();
            MakeUnit("Commander", Side.Player, commanderT, Vector3.zero);
            var squad = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 5));
            var enemy = MakeUnit("Enemy", Side.AI, trooperT, new Vector3(0, 0, 30));
            NearbyKeywords.RefreshAll();

            var r = UnitShooting.Resolve(squad, enemy, false, new System.Random(1));
            Assert.That(r.Shots[0].Attack.Accuracy.Final, Is.EqualTo(60 + 15));
        }

        [Test]
        public void TheAuraFollowsTheCommanderWhenTheyMove()
        {
            MakeTemplates();
            var cmd = MakeUnit("Commander", Side.Player, commanderT, Vector3.zero);
            var squad = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 20));
            NearbyKeywords.RefreshAll();
            Assert.That(squad.Soldiers[0].HasKeyword("Led from the front"), Is.False);

            var move = new UnitMoveActivation(cmd, cmd.Soldiers);
            Assert.That(move.TryMoveLeader(new Vector3(0, 0, 8f), out var check), Is.True, check.Reason);
            Assert.That(squad.Soldiers[0].HasKeyword("Led from the front"), Is.True, "now 12 m away");

            move.Reset();
            Assert.That(squad.Soldiers[0].HasKeyword("Led from the front"), Is.False);
        }

        [Test]
        public void TheAuraEndsWhenTheCommanderDies()
        {
            MakeTemplates();
            var cmd = MakeUnit("Commander", Side.Player, commanderT, Vector3.zero);
            var squad = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 5));
            NearbyKeywords.RefreshAll();
            cmd.Soldiers[0].Kill();
            NearbyKeywords.RefreshAll();
            Assert.That(squad.Soldiers[0].HasKeyword("Led from the front"), Is.False);
            Assert.That(cmd.IsDestroyed, Is.True, "a unit of one");
        }
    }
}
