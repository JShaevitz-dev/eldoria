using System.Collections.Generic;
using System.Linq;
using Eldoira.AI;
using Eldoira.Gameplay;
using Eldoira.Rules;
using Eldoira.Terrain;
using Eldoira.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoira.Tests
{
    /// <summary>The AI's decisions, on flat open ground (straight-line moves).</summary>
    public class AIPlannerTests
    {
        private readonly List<Object> created = new List<Object>();
        private Keyword biological, officer;
        private WeaponTemplate rifleT, grenadeT;
        private SoldierTemplate trooperT, priestT, commanderT;
        private Ability frag, heal, lead;

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
            Turns.StartNextTurn();
            biological = Kw("Biological");
            officer = Kw("Officer");

            rifleT = Make<WeaponTemplate>();
            rifleT.displayName = "Rifle";
            rifleT.baseDamage = 4; rifleT.penetration = 1;
            grenadeT = Make<WeaponTemplate>();
            grenadeT.baseDamage = 2;

            frag = Make<Ability>();
            frag.displayName = "Frag Grenade";
            frag.kind = AbilityKind.Active; frag.targets = AbilityTarget.Point; frag.affects = AuraAffects.Everyone;
            frag.range = 10f; frag.areaRadius = 2.5f; frag.areaWeapon = grenadeT; frag.usesPerBattle = 1;
            heal = Make<Ability>();
            heal.displayName = "Divine Healing";
            heal.kind = AbilityKind.Active; heal.targets = AbilityTarget.Soldier; heal.affects = AuraAffects.Allies;
            heal.range = 25f; heal.heal = 5; heal.targetMustHave = biological;
            heal.keywords.Add(Kw("Spell"));
            lead = Make<Ability>();
            lead.kind = AbilityKind.Passive; lead.grantsNearby = Kw("Led from the front"); lead.radius = 15f;

            trooperT = Template("Trooper", rifleT);
            trooperT.abilities = new List<Ability> { frag };
            priestT = Template("Priest", rifleT);
            priestT.keywords.Add(officer);
            priestT.abilities = new List<Ability> { heal };
            commanderT = Template("Commander", rifleT);
            commanderT.keywords.Add(officer);
            commanderT.abilities = new List<Ability> { lead };
        }

        private SoldierTemplate Template(string name, WeaponTemplate weapon)
        {
            var t = Make<SoldierTemplate>();
            t.displayName = name;
            t.health = 10;
            t.accuracy = 75;
            t.movementRange = 10f;
            t.keywords.Add(biological);
            t.weapons.Add(weapon);
            return t;
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

        private static Vector3[] Line(float z, float x0, int n, float step = 2f) =>
            Enumerable.Range(0, n).Select(i => new Vector3(x0 + i * step, 0, z)).ToArray();

        private static UnitMoveActivation Activate(Unit unit) =>
            new UnitMoveActivation(unit, Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None));

        private GameObject Wall(Vector3 centre, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(go);
            go.layer = TerrainLayers.Structure;
            go.transform.position = centre;
            go.transform.localScale = size;
            Physics.SyncTransforms();
            return go;
        }

        // ---- Moving ----

        [Test]
        public void ASquadFarFromTheEnemyAdvances()
        {
            var ai = MakeUnit("AI", Side.AI, trooperT, Line(20, -4, 5));
            MakeUnit("Player", Side.Player, trooperT, Line(-20, -4, 5));
            var dest = AIPlanner.ChooseLeaderMove(ai, Activate(ai));
            Assert.That(dest.HasValue, Is.True);
            Assert.That(dest.Value.z, Is.LessThan(ai.Leader.Position.z - 5f), "moves toward the enemy");
        }

        [Test]
        public void TheAIDoesntWalkIntoCloseRange()
        {
            var ai = MakeUnit("AI", Side.AI, trooperT, new Vector3(0, 0, 6));
            var player = MakeUnit("Player", Side.Player, trooperT, new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(-2, 0, 0));
            var dest = AIPlanner.ChooseLeaderMove(ai, Activate(ai)) ?? ai.Leader.Position;
            Assert.That(player.LivingSoldiers.Min(p => Measure.Horizontal(p.Position, dest)), Is.GreaterThanOrEqualTo(CloseRange.Distance));
        }

        [Test]
        public void ThePriestStaysBehindItsSquadsAndNeverSprints()
        {
            MakeUnit("AI Squad", Side.AI, trooperT, Line(0, -4, 5));
            var priest = MakeUnit("AI Priest", Side.AI, priestT, new Vector3(0, 0, 25));
            MakeUnit("Player", Side.Player, trooperT, Line(-25, -4, 5));
            var activation = Activate(priest);
            var dest = AIPlanner.ChooseLeaderMove(priest, activation);
            Assert.That(dest.HasValue, Is.True);
            Assert.That(activation.CheckLeaderMove(dest.Value).Kind, Is.EqualTo(MoveKind.Normal), "a sprint would stop its spells");
            Assert.That(dest.Value.z, Is.GreaterThan(0f), "stays behind the squad (the enemy is to the south)");
            Assert.That(dest.Value.z, Is.LessThan(25f), "closes up toward the squad");
        }

        [Test]
        public void TheCommanderActsFirstAndSupportUnitsLast()
        {
            MakeUnit("Priest", Side.AI, priestT, new Vector3(0, 0, 25));
            MakeUnit("Squad", Side.AI, trooperT, Line(0, -4, 3));
            MakeUnit("Commander", Side.AI, commanderT, new Vector3(5, 0, 25));
            var order = AIPlanner.ActivationOrder(Side.AI).Select(u => u.DisplayName).ToList();
            Assert.That(order, Is.EqualTo(new[] { "Commander", "Squad", "Priest" }));
        }

        // ---- Acting ----

        [Test]
        public void ASquadThatCanSeeTheEnemyShootsTheUnitMostOfItsSoldiersCanTarget()
        {
            var ai = MakeUnit("AI", Side.AI, trooperT, Line(20, -4, 5));
            MakeUnit("Far", Side.Player, trooperT, Line(-20, -4, 3));
            var near = MakeUnit("Near", Side.Player, trooperT, Line(0, -4, 3));
            var action = AIPlanner.ChooseAction(ai);
            Assert.That(action.Kind, Is.EqualTo(AIActionKind.Shoot));
            Assert.That(action.ShootAt, Is.EqualTo(near), "equal numbers can target both: the nearer");
        }

        [Test]
        public void WithNoLineOfSightItThrowsAGrenadeAtEnemiesItCanReach()
        {
            var ai = MakeUnit("AI", Side.AI, trooperT, Line(8, -4, 5));
            Wall(new Vector3(0, 2, 4), new Vector3(30, 4, 0.3f));   // blocks sight between the squads
            var player = MakeUnit("Player", Side.Player, trooperT, new Vector3(0, 0, 1), new Vector3(1.5f, 0, 1), new Vector3(12, 0, 1));   // all behind the wall
            var action = AIPlanner.ChooseAction(ai);
            Assert.That(action.Kind, Is.EqualTo(AIActionKind.Ability));
            Assert.That(action.Ability, Is.EqualTo(frag));
            var spot = action.TargetPoint.Value;
            Assert.That(Measure.ToPoint(player.Soldiers[0], spot), Is.LessThanOrEqualTo(2.5f));
            Assert.That(Measure.ToPoint(player.Soldiers[1], spot), Is.LessThanOrEqualTo(2.5f), "catches both");
        }

        [Test]
        public void ItNeverThrowsAGrenadeThatWouldHitItsOwnSoldiers()
        {
            var ai = MakeUnit("AI", Side.AI, trooperT, Line(8, -4, 5));
            Wall(new Vector3(0, 2, 4), new Vector3(30, 4, 0.3f));
            MakeUnit("Player", Side.Player, trooperT, new Vector3(0, 0, 1));
            MakeUnit("AI Scout", Side.AI, trooperT, new Vector3(1, 0, 0));   // right beside the only target
            var action = AIPlanner.ChooseAction(ai);
            Assert.That(action.Kind, Is.Not.EqualTo(AIActionKind.Ability));
        }

        [Test]
        public void ThePriestHealsTheMostWoundedFriendInRangeFirst()
        {
            var priest = MakeUnit("Priest", Side.AI, priestT, new Vector3(0, 0, 20));
            var squad = MakeUnit("Squad", Side.AI, trooperT, Line(10, -4, 3));
            MakeUnit("Player", Side.Player, trooperT, Line(-20, -4, 3));
            squad.Soldiers[1].TakeDamage(3);
            squad.Soldiers[2].TakeDamage(6);
            var action = AIPlanner.ChooseAction(priest);
            Assert.That(action.Kind, Is.EqualTo(AIActionKind.Ability));
            Assert.That(action.TargetSoldier, Is.EqualTo(squad.Soldiers[2]));
        }

        [Test]
        public void ThePriestShootsWhenNobodyNeedsHealing()
        {
            var priest = MakeUnit("Priest", Side.AI, priestT, new Vector3(0, 0, 20));
            var squad = MakeUnit("Squad", Side.AI, trooperT, Line(10, -4, 3));
            MakeUnit("Player", Side.Player, trooperT, Line(-20, -4, 3));
            squad.Soldiers[0].TakeDamage(1); // only 1 missing: not worth the action
            Assert.That(AIPlanner.ChooseAction(priest).Kind, Is.EqualTo(AIActionKind.Shoot));
        }
    }
}
