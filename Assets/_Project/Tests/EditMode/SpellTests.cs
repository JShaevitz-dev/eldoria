using System.Collections.Generic;
using System.Linq;
using Eldoria.Gameplay;
using Eldoria.Rules;
using Eldoria.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoria.Tests
{
    /// <summary>Abilities classified as Spells can't be cast if the soldier's unit sprinted this turn.</summary>
    public class SpellTests
    {
        private const string Data = "Assets/_Project/Data";
        private readonly List<Object> created = new List<Object>();
        private Keyword spellKw;
        private Ability spell, plainAbility;
        private SoldierTemplate casterT, trooperT;

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

            spellKw = Make<Keyword>();
            spellKw.displayName = "Spell";
            spellKw.appliesTo = KeywordTarget.Ability;

            spell = Make<Ability>();
            spell.displayName = "Mend";
            spell.kind = AbilityKind.Active;
            spell.targets = AbilityTarget.Soldier;
            spell.range = 25f;
            spell.heal = 5;
            spell.keywords.Add(spellKw);

            plainAbility = Make<Ability>();
            plainAbility.displayName = "Bandage";
            plainAbility.kind = AbilityKind.Active;
            plainAbility.targets = AbilityTarget.Soldier;
            plainAbility.range = 25f;
            plainAbility.heal = 5;

            casterT = Make<SoldierTemplate>();
            casterT.health = 5;
            casterT.movementRange = 10f;
            casterT.abilities = new List<Ability> { spell, plainAbility };

            trooperT = Make<SoldierTemplate>();
            trooperT.health = 5;
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

        private (Soldier caster, Soldier ally) Setup()
        {
            var caster = MakeUnit("Caster", Side.Player, casterT, Vector3.zero).Soldiers[0];
            var ally = MakeUnit("Ally", Side.Player, trooperT, new Vector3(5, 0, 0)).Soldiers[0];
            ally.TakeDamage(3);
            return (caster, ally);
        }

        private static UnitMoveActivation Activate(Soldier caster) =>
            new UnitMoveActivation(caster.Unit, Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None));

        [Test]
        public void ASpellCantBeCastAfterTheUnitSprinted()
        {
            var (caster, ally) = Setup();
            var activation = Activate(caster);
            Assert.That(activation.TryMoveLeader(new Vector3(0, 0, 15), out var check), Is.True, check.Reason);
            Assert.That(check.Kind, Is.EqualTo(MoveKind.Sprint));

            Assert.That(ActiveAbilities.WhyCannotCast(caster, spell), Does.Contain("Spell"));
            var r = ActiveAbilities.Use(caster, spell, ally);
            Assert.That(r.Used, Is.False);
            Assert.That(ally.Health, Is.EqualTo(2), "nothing happened");
        }

        [Test]
        public void ANonSpellAbilityCanStillBeUsedAfterSprinting()
        {
            var (caster, ally) = Setup();
            Activate(caster).TryMoveLeader(new Vector3(0, 0, 15), out _);
            Assert.That(ActiveAbilities.Use(caster, plainAbility, ally).Used, Is.True);
        }

        [Test]
        public void ASpellCanBeCastAfterANormalMove()
        {
            var (caster, ally) = Setup();
            var activation = Activate(caster);
            Assert.That(activation.TryMoveLeader(new Vector3(0, 0, 8), out var check), Is.True, check.Reason);
            Assert.That(check.Kind, Is.EqualTo(MoveKind.Normal));
            Assert.That(ActiveAbilities.Use(caster, spell, ally).Used, Is.True);
        }

        [Test]
        public void ASpellCanBeCastWhenHoldingPositionOrWithoutMoving()
        {
            var (caster, ally) = Setup();
            Assert.That(ActiveAbilities.WhyCannotCast(caster, spell), Is.Null, "instead of moving");
            Activate(caster).HoldLeader();
            Assert.That(ActiveAbilities.Use(caster, spell, ally).Used, Is.True);
        }

        [Test]
        public void UndoingTheSprintAllowsSpellsAgain()
        {
            var (caster, _) = Setup();
            var activation = Activate(caster);
            activation.TryMoveLeader(new Vector3(0, 0, 15), out _);
            Assert.That(caster.Unit.SprintedThisTurn, Is.True);

            activation.Reset();
            Assert.That(caster.Unit.SprintedThisTurn, Is.False);
            Assert.That(ActiveAbilities.WhyCannotCast(caster, spell), Is.Null);
        }

        [Test]
        public void SprintingOnlyBlocksSpellsForThatTurn()
        {
            var (caster, _) = Setup();
            Activate(caster).TryMoveLeader(new Vector3(0, 0, 15), out _);
            Turns.StartNextTurn();
            Assert.That(caster.Unit.SprintedThisTurn, Is.False);
            Assert.That(ActiveAbilities.WhyCannotCast(caster, spell), Is.Null);
        }

        [Test]
        public void EverySoldierInASprintingUnitIsAffected()
        {
            // The unit sprints as a whole (its leader's move decides it), so a follower can't cast either.
            var unit = MakeUnit("Casters", Side.Player, casterT, Vector3.zero, new Vector3(2, 0, 0));
            var follower = unit.Soldiers[1];
            new UnitMoveActivation(unit, unit.Soldiers).TryMoveLeader(new Vector3(0, 0, 15), out _);
            Assert.That(ActiveAbilities.WhyCannotCast(follower, spell), Does.Contain("sprinting"));
        }

        [Test]
        public void AbilityButtonsDontShowUsesPerTurn()
        {
            var (caster, ally) = Setup();
            string label = MovementController.AbilityButtonLabel(caster, spell);
            Assert.That(label, Is.EqualTo("Mend [Spell] — 25 m"));
            Assert.That(label, Does.Not.Contain("left"));

            ActiveAbilities.Use(caster, spell, ally);
            Assert.That(MovementController.AbilityButtonLabel(caster, spell), Is.EqualTo("Mend [Spell] — 25 m"), "no count after it's used either");

            Assert.That(MovementController.AbilityButtonLabel(caster, plainAbility), Is.EqualTo("Bandage — 25 m"));
        }

        [Test]
        public void TheButtonSaysWhyASpellCantBeCastAfterSprinting()
        {
            var (caster, _) = Setup();
            Activate(caster).TryMoveLeader(new Vector3(0, 0, 15), out _);
            Assert.That(MovementController.AbilityButtonLabel(caster, spell), Does.Contain("can't be cast after sprinting"));
        }

        [Test]
        public void ThePriestsAbilitiesAreSpells()
        {
            var healing = AssetDatabase.LoadAssetAtPath<Ability>($"{Data}/Abilities/DivineHealing.asset");
            var sanctuary = AssetDatabase.LoadAssetAtPath<Ability>($"{Data}/Abilities/DivineSanctuary.asset");
            var lead = AssetDatabase.LoadAssetAtPath<Ability>($"{Data}/Abilities/LeadFromTheFront.asset");
            Assert.That(healing.IsSpell, Is.True);
            Assert.That(sanctuary.IsSpell, Is.True);
            Assert.That(lead.IsSpell, Is.False);
            var kw = healing.keywords.Single(k => k != null && k.Is("Spell"));
            Assert.That(kw.appliesTo, Is.EqualTo(KeywordTarget.Ability));
            Assert.That(kw.category, Is.EqualTo(KeywordCategory.Trait));
        }
    }
}
