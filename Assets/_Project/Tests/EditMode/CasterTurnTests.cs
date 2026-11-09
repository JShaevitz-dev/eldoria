using System.Collections.Generic;
using Eldoira.AI;
using Eldoira.Gameplay;
using Eldoira.Rules;
using Eldoira.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoira.Tests
{
    /// <summary>"Until the start of the next turn" means the caster's own next turn (the player goes first each turn).</summary>
    public class CasterTurnTests
    {
        private readonly List<Object> created = new List<Object>();
        private Keyword biological, blessed, alert;
        private Ability sanctuary;
        private SoldierTemplate priestT, trooperT;

        private T Make<T>() where T : ScriptableObject
        {
            var t = ScriptableObject.CreateInstance<T>();
            created.Add(t);
            return t;
        }

        private Keyword Kw(string name)
        {
            var k = Make<Keyword>();
            k.displayName = name;
            return k;
        }

        [SetUp]
        public void SetUp()
        {
            Turns.ResetCounter();
            Turns.StartNextTurn();   // turn 1, the player's side
            biological = Kw("Biological");
            blessed = Kw("Divine Sanctuary");
            alert = Kw("Alert");

            sanctuary = Make<Ability>();
            sanctuary.displayName = "Divine Sanctuary";
            sanctuary.kind = AbilityKind.Active; sanctuary.targets = AbilityTarget.Unit; sanctuary.affects = AuraAffects.Allies;
            sanctuary.range = 25f; sanctuary.grantsKeyword = blessed; sanctuary.grantUntilNextTurn = true;

            var rifle = Make<WeaponTemplate>();
            rifle.baseDamage = 4; rifle.penetration = 1;
            priestT = Make<SoldierTemplate>();
            priestT.health = 5; priestT.accuracy = 50;
            priestT.keywords.Add(biological);
            priestT.keywords.Add(Kw("Officer"));
            priestT.abilities = new List<Ability> { sanctuary };
            priestT.weapons.Add(rifle);
            trooperT = Make<SoldierTemplate>();
            trooperT.health = 10; trooperT.accuracy = 100;
            trooperT.keywords.Add(biological);
            trooperT.weapons.Add(rifle);
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

        [Test]
        public void ThePlayersSpellLastsThroughTheAIsTurnAndEndsAtThePlayersNextTurn()
        {
            var priest = MakeUnit("Priest", Side.Player, priestT, Vector3.zero).Soldiers[0];
            var squad = MakeUnit("Squad", Side.Player, trooperT, new Vector3(0, 0, 5));
            Assert.That(ActiveAbilities.Use(priest, sanctuary, null, squad).Used, Is.True);

            Turns.StartSideTurn(Side.AI);
            Assert.That(squad.Soldiers[0].HasKeyword("Divine Sanctuary"), Is.True, "protects through the AI's turn");
            Turns.StartNextTurn();
            Assert.That(squad.Soldiers[0].HasKeyword("Divine Sanctuary"), Is.False, "gone at the player's next turn");
        }

        [Test]
        public void TheAIsSpellLastsThroughThePlayersTurnAndEndsAtTheAIsNextTurn()
        {
            var priest = MakeUnit("AI Priest", Side.AI, priestT, Vector3.zero).Soldiers[0];
            var squad = MakeUnit("AI Squad", Side.AI, trooperT, new Vector3(0, 0, 5));
            Turns.StartSideTurn(Side.AI);
            Assert.That(ActiveAbilities.Use(priest, sanctuary, null, squad).Used, Is.True);

            Turns.StartNextTurn();   // turn 2, the player's side
            Assert.That(squad.Soldiers[0].HasKeyword("Divine Sanctuary"), Is.True, "still protected during the player's turn");
            Turns.StartSideTurn(Side.AI);
            Assert.That(squad.Soldiers[0].HasKeyword("Divine Sanctuary"), Is.False, "gone at the start of the AI's next turn");
        }

        [Test]
        public void ATriggerLastsUntilTheNextTurnOfTheSoldierWhoseKeywordFired()
        {
            // A player soldier's "when targeted → Self gains Alert until next turn" fires during the AI's turn.
            var wary = Kw("Wary");
            wary.triggers.Add(new KeywordTrigger { when = TriggerEvent.Targeted, action = TriggerAction.GainKeyword, subject = TriggerSubject.Self, keyword = alert, untilNextTurn = true });
            var waryT = Make<SoldierTemplate>();
            waryT.health = 10;
            waryT.keywords.Add(wary);
            var mine = MakeUnit("Mine", Side.Player, waryT, Vector3.zero).Soldiers[0];
            var theirs = MakeUnit("Theirs", Side.AI, trooperT, new Vector3(0, 0, 10));

            Turns.StartSideTurn(Side.AI);
            UnitShooting.Resolve(theirs, mine.Unit, false, new System.Random(1));
            Assert.That(mine.HasKeyword("Alert"), Is.True);
            Turns.StartNextTurn();
            Assert.That(mine.HasKeyword("Alert"), Is.False, "the player's next turn has come");
        }

        [Test]
        public void AnAIUnitThatMovedStillShowsMovedDuringThePlayersTurn()
        {
            var squad = MakeUnit("AI Squad", Side.AI, trooperT, Vector3.zero);
            Turns.StartSideTurn(Side.AI);
            var activation = new UnitMoveActivation(squad, squad.Soldiers);
            Assert.That(activation.TryMoveLeader(new Vector3(0, 0, 5), out _), Is.True);
            Turns.StartNextTurn();
            Assert.That(squad.Soldiers[0].HasKeyword("Moved"), Is.True);
            Turns.StartSideTurn(Side.AI);
            Assert.That(squad.Soldiers[0].HasKeyword("Moved"), Is.False);
        }

        [Test]
        public void TheAIPriestBlessesAFriendlyUnitThatEnemiesCanSee()
        {
            var priest = MakeUnit("AI Priest", Side.AI, priestT, new Vector3(0, 0, 25));
            var squad = MakeUnit("AI Squad", Side.AI, trooperT, new Vector3(0, 0, 10), new Vector3(2, 0, 10));
            MakeUnit("Player", Side.Player, trooperT, new Vector3(0, 0, -10));
            Turns.StartSideTurn(Side.AI);

            var action = AIPlanner.ChooseAction(priest);
            Assert.That(action.Kind, Is.EqualTo(AIActionKind.Ability));
            Assert.That(action.Ability, Is.EqualTo(sanctuary));
            Assert.That(action.TargetUnit, Is.EqualTo(squad), "the squad in the enemy's sight (bigger than the priest's own unit)");
        }
    }
}
