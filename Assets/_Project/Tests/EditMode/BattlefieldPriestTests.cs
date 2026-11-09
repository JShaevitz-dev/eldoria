using System.Collections.Generic;
using System.Linq;
using Eldoira.Rules;
using Eldoira.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoira.Tests
{
    public class BattlefieldPriestTests
    {
        private const string Data = "Assets/_Project/Data";
        private readonly List<Object> created = new List<Object>();
        private Keyword biological, sanctuaryKw;
        private Ability healing, sanctuary;
        private SoldierTemplate priestT, trooperT, robotT;
        private WeaponTemplate rifleT;

        private T Make<T>() where T : ScriptableObject
        {
            var t = ScriptableObject.CreateInstance<T>();
            created.Add(t);
            return t;
        }

        [SetUp]
        public void SetUp()
        {
            Turns.ResetCounter();
            Turns.StartNextTurn();

            biological = Make<Keyword>();
            biological.displayName = "Biological";
            sanctuaryKw = Make<Keyword>();
            sanctuaryKw.displayName = "Divine Sanctuary";
            sanctuaryKw.interactions.Add(new KeywordInteraction
            {
                role = KeywordRole.WhenDefending, condition = KeywordCondition.Always, effect = KeywordEffect.ArmorBonus, amount = 1
            });

            healing = Make<Ability>();
            healing.displayName = "Divine Healing";
            healing.kind = AbilityKind.Active;
            healing.targets = AbilityTarget.Soldier;
            healing.range = 25f;
            healing.targetMustHave = biological;
            healing.heal = 5;

            sanctuary = Make<Ability>();
            sanctuary.displayName = "Divine Sanctuary";
            sanctuary.kind = AbilityKind.Active;
            sanctuary.targets = AbilityTarget.Unit;
            sanctuary.range = 25f;
            sanctuary.grantsKeyword = sanctuaryKw;

            rifleT = Make<WeaponTemplate>();
            rifleT.baseDamage = 4;
            rifleT.penetration = 1;

            priestT = Make<SoldierTemplate>();
            priestT.health = 5;
            priestT.keywords.Add(biological);
            priestT.abilities = new List<Ability> { healing, sanctuary };
            priestT.weapons.Add(rifleT);

            trooperT = Make<SoldierTemplate>();
            trooperT.health = 5;
            trooperT.armorLevel = 1;
            trooperT.accuracy = 100;
            trooperT.keywords.Add(biological);
            trooperT.weapons.Add(rifleT);

            robotT = Make<SoldierTemplate>();
            robotT.health = 5;
            robotT.weapons.Add(rifleT);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Turns.ResetCounter();
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

        private Soldier Priest() => MakeUnit("Priest", Side.Player, priestT, Vector3.zero).Soldiers[0];

        // ---- The templates as designed ----

        [Test]
        public void BattlefieldPriestHasTheDesignedStats()
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/BattlefieldPriest.asset");
            Assert.That(unit.Size, Is.EqualTo(1));
            var p = unit.leader;
            Assert.That(p.displayName, Is.EqualTo("Battlefield Priest"));
            Assert.That((p.health, p.armorLevel, p.accuracy, p.movementRange), Is.EqualTo((5, 0, 50, 10f)));
            Assert.That(p.keywords.Select(k => k.displayName), Is.EquivalentTo(new[] { "Biological", "Officer", "Solo", "Magic" }));
            Assert.That(p.keywords.All(k => k.category == KeywordCategory.Trait), Is.True);
            Assert.That(p.PrimaryWeapon.displayName, Is.EqualTo("Pistol"));

            var heal = p.abilities.Single(a => a.displayName == "Divine Healing");
            Assert.That((heal.kind, heal.targets, heal.affects, heal.range, heal.heal), Is.EqualTo((AbilityKind.Active, AbilityTarget.Soldier, AuraAffects.Allies, 25f, 5)));
            Assert.That(heal.targetMustHave.displayName, Is.EqualTo("Biological"));

            var sanct = p.abilities.Single(a => a.displayName == "Divine Sanctuary");
            Assert.That((sanct.kind, sanct.targets, sanct.affects, sanct.range), Is.EqualTo((AbilityKind.Active, AbilityTarget.Unit, AuraAffects.Allies, 25f)));
            Assert.That(sanct.grantsKeyword.interactions.Single().effect, Is.EqualTo(KeywordEffect.ArmorBonus));
            Assert.That(sanct.grantsKeyword.interactions.Single().amount, Is.EqualTo(1));
        }

        // ---- Divine Healing ----

        [Test]
        public void HealingRestores5HealthUpToTheSoldiersStartingHealth()
        {
            var priest = Priest();
            var ally = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 10)).Soldiers[0];
            ally.TakeDamage(4); // 1 left

            var r = ActiveAbilities.Use(priest, healing, ally);
            Assert.That(r.Used, Is.True, r.Reason);
            Assert.That(ally.Health, Is.EqualTo(5), "1 + 5, capped at 5");
        }

        [Test]
        public void HealingNeedsAFriendlyBiologicalSoldierWithin25m()
        {
            var priest = Priest();
            var robot = MakeUnit("Robots", Side.Player, robotT, new Vector3(0, 0, 5)).Soldiers[0];
            var far = MakeUnit("Far", Side.Player, trooperT, new Vector3(0, 0, 26)).Soldiers[0];
            var edge = MakeUnit("Edge", Side.Player, trooperT, new Vector3(0, 0, 25)).Soldiers[0];
            var enemy = MakeUnit("Enemy", Side.AI, trooperT, new Vector3(0, 0, 5)).Soldiers[0];
            foreach (var s in new[] { robot, far, edge, enemy }) s.TakeDamage(1); // wounded, so only the tested reason applies

            Assert.That(ActiveAbilities.WhyCannotUse(priest, healing, robot), Does.Contain("Biological"));
            Assert.That(ActiveAbilities.WhyCannotUse(priest, healing, far), Does.Contain("range"));
            Assert.That(ActiveAbilities.WhyCannotUse(priest, healing, enemy), Is.Not.Null);
            Assert.That(ActiveAbilities.WhyCannotUse(priest, healing, edge), Is.Null);
        }

        [Test]
        public void HealingCantTargetASoldierAtFullHealthAndIsNotUsedUp()
        {
            var priest = Priest();
            var healthy = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 5)).Soldiers[0];
            var wounded = MakeUnit("Others", Side.Player, trooperT, new Vector3(0, 0, 8)).Soldiers[0];
            wounded.TakeDamage(2);

            var r = ActiveAbilities.Use(priest, healing, healthy);
            Assert.That(r.Used, Is.False);
            Assert.That(r.Reason, Does.Contain("full health"));
            Assert.That(priest.TimesUsedThisTurn(healing), Is.EqualTo(0), "a refused target doesn't use up the ability");

            // ... so the priest can still heal someone else this turn.
            Assert.That(ActiveAbilities.Use(priest, healing, wounded).Used, Is.True);
            Assert.That(wounded.Health, Is.EqualTo(5));
        }

        [Test]
        public void ARefusedNonBiologicalTargetIsNotUsedUpEither()
        {
            var priest = Priest();
            var robot = MakeUnit("Robots", Side.Player, robotT, new Vector3(0, 0, 5)).Soldiers[0];
            robot.TakeDamage(2);
            Assert.That(ActiveAbilities.Use(priest, healing, robot).Reason, Does.Contain("Biological"));
            Assert.That(priest.TimesUsedThisTurn(healing), Is.EqualTo(0));
        }

        [Test]
        public void AbilityUsedIsRaisedOnlyForASuccessfulCast()
        {
            var priest = Priest();
            var healthy = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 5)).Soldiers[0];
            var wounded = MakeUnit("Others", Side.Player, trooperT, new Vector3(0, 0, 8)).Soldiers[0];
            wounded.TakeDamage(2);
            var used = new List<Ability>();
            System.Action<Soldier, Ability, AbilityResult> handler = (u, a, r) => used.Add(a);
            ActiveAbilities.AbilityUsed += handler;
            try
            {
                ActiveAbilities.Use(priest, healing, healthy);   // refused: full health
                Assert.That(used, Is.Empty);
                ActiveAbilities.Use(priest, healing, wounded);   // cast
                Assert.That(used, Is.EqualTo(new[] { healing }));
            }
            finally
            {
                ActiveAbilities.AbilityUsed -= handler;
            }
        }

        [Test]
        public void ThePriestsSpellsPlayTheHallelujah()
        {
            var p = AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{Data}/Units/BattlefieldPriest.asset").leader;
            foreach (var spell in p.abilities)
            {
                Assert.That(spell.IsSpell, Is.True);
                Assert.That(spell.castSound, Is.Not.Null, spell.displayName);
                Assert.That(spell.castSound.name, Is.EqualTo("Hallelujah"));
            }
            var lead = AssetDatabase.LoadAssetAtPath<Ability>($"{Data}/Abilities/LeadFromTheFront.asset");
            Assert.That(lead.castSound, Is.Null, "not a spell, no sound");
        }

        [Test]
        public void SanctuaryCanStillTargetAUnitAtFullHealth()
        {
            var priest = Priest();
            var squad = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 10));
            Assert.That(ActiveAbilities.Use(priest, sanctuary, null, squad).Used, Is.True, "the full-health check is only for healing");
        }

        [Test]
        public void TheDeadCannotBeHealed()
        {
            var priest = Priest();
            var ally = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 5), new Vector3(1, 0, 5)).Soldiers[1];
            ally.Kill();
            Assert.That(ActiveAbilities.Use(priest, healing, ally).Used, Is.False);
        }

        [Test]
        public void ThePriestCanHealThemself()
        {
            var priest = Priest();
            priest.TakeDamage(3);
            Assert.That(ActiveAbilities.Use(priest, healing, priest).Used, Is.True);
            Assert.That(priest.Health, Is.EqualTo(5));
        }

        [Test]
        public void EachAbilityOncePerTurn()
        {
            var priest = Priest();
            var ally = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 5)).Soldiers[0];
            ally.TakeDamage(4);
            Assert.That(ActiveAbilities.Use(priest, healing, ally).Used, Is.True);
            ally.TakeDamage(2);
            Assert.That(ActiveAbilities.Use(priest, healing, ally).Reason, Does.Contain("already been used"));
            Assert.That(ActiveAbilities.Use(priest, sanctuary, null, ally.Unit).Used, Is.True, "a different ability is still available");

            Turns.StartNextTurn();
            Assert.That(ActiveAbilities.Use(priest, healing, ally).Used, Is.True, "usable again next turn");
        }

        // ---- Divine Sanctuary ----

        [Test]
        public void SanctuaryGivesEverySoldierInTheUnitPlusOneArmor()
        {
            var priest = Priest();
            // The unit's closest soldier is 10 m away; another is 40 m away — still affected.
            var squad = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 10), new Vector3(0, 0, 40));
            var r = ActiveAbilities.Use(priest, sanctuary, null, squad);
            Assert.That(r.Used, Is.True, r.Reason);
            Assert.That(squad.Soldiers.All(s => s.HasKeyword("Divine Sanctuary")), Is.True);
        }

        [Test]
        public void SanctuaryArmorCountsAgainstPenetration()
        {
            var priest = Priest();
            var squad = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 10));
            var enemy = MakeUnit("Enemy", Side.AI, trooperT, new Vector3(0, 0, 30));

            // Rifle (Penetration 1) vs Armor 1 → 100%: 4 damage.
            var before = Attack.Resolve(enemy.Soldiers[0], enemy.Soldiers[0].Weapons[0], squad.Soldiers[0], false, new System.Random(1), applyDamage: false);
            Assert.That(before.TargetArmor, Is.EqualTo(1));
            Assert.That(before.DamageDealt, Is.EqualTo(4));

            ActiveAbilities.Use(priest, sanctuary, null, squad);
            // Armor 1 + 1 = 2 → penetration 1 lower → 50%: 2 damage.
            var after = Attack.Resolve(enemy.Soldiers[0], enemy.Soldiers[0].Weapons[0], squad.Soldiers[0], false, new System.Random(1), applyDamage: false);
            Assert.That(after.TargetArmor, Is.EqualTo(2));
            Assert.That(after.DamageDealt, Is.EqualTo(2));
        }

        [Test]
        public void SanctuaryLastsUntilTheNextTurn()
        {
            var priest = Priest();
            var squad = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 10));
            ActiveAbilities.Use(priest, sanctuary, null, squad);
            Turns.StartNextTurn();
            Assert.That(squad.Soldiers[0].HasKeyword("Divine Sanctuary"), Is.False);
        }

        [Test]
        public void SanctuaryNeedsAFriendlyUnitWithin25m()
        {
            var priest = Priest();
            var far = MakeUnit("Far", Side.Player, trooperT, new Vector3(0, 0, 26), new Vector3(0, 0, 30));
            var enemy = MakeUnit("Enemy", Side.AI, trooperT, new Vector3(0, 0, 5));
            Assert.That(ActiveAbilities.WhyCannotUse(priest, sanctuary, null, far), Does.Contain("range"));
            Assert.That(ActiveAbilities.WhyCannotUse(priest, sanctuary, null, enemy), Is.Not.Null);
            Assert.That(ActiveAbilities.WhyCannotUse(priest, sanctuary, null, priest.Unit), Is.Null, "its own unit (itself) is allowed");
        }
    }
}
