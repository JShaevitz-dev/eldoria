using System.Collections.Generic;
using System.Linq;
using Eldoira.Gameplay;
using Eldoira.Rules;
using Eldoira.Terrain;
using Eldoira.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoira.Tests
{
    /// <summary>
    /// Frag Grenade: a spot within 10 m of the unit leader; everyone within 2.5 m the blast can reach takes 2 damage
    /// (penetration 0); once per battle per unit; one button however many soldiers have it; it is the unit's action.
    /// </summary>
    public class FragGrenadeTests
    {
        private const string Data = "Assets/_Project/Data";
        private readonly List<Object> created = new List<Object>();
        private Ability frag, other;
        private WeaponTemplate grenadeT;
        private SoldierTemplate trooperT, sergeantT, unarmouredT;

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

            grenadeT = Make<WeaponTemplate>();
            grenadeT.displayName = "Frag Grenade";
            grenadeT.baseDamage = 2;
            grenadeT.penetration = 0;

            frag = Make<Ability>();
            frag.displayName = "Frag Grenade";
            frag.kind = AbilityKind.Active;
            frag.targets = AbilityTarget.Point;
            frag.affects = AuraAffects.Everyone;
            frag.range = 10f;
            frag.areaRadius = 2.5f;
            frag.areaWeapon = grenadeT;
            frag.usesPerBattle = 1;

            other = Make<Ability>();
            other.displayName = "Smoke";
            other.kind = AbilityKind.Active;
            other.targets = AbilityTarget.Point;
            other.range = 10f;

            trooperT = Make<SoldierTemplate>();
            trooperT.health = 5;
            trooperT.armorLevel = 1;
            trooperT.abilities = new List<Ability> { frag };

            sergeantT = Make<SoldierTemplate>();
            sergeantT.health = 5;
            sergeantT.armorLevel = 1;
            sergeantT.abilities = new List<Ability> { frag, other };

            unarmouredT = Make<SoldierTemplate>();
            unarmouredT.health = 5;
            unarmouredT.armorLevel = 0;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            Turns.ResetCounter();
        }

        private Unit MakeUnit(string name, Side side, params (SoldierTemplate t, Vector3 pos)[] soldiers)
        {
            var go = new GameObject(name);
            created.Add(go);
            var unit = go.AddComponent<Unit>();
            unit.Configure(null, side, name);
            for (int i = 0; i < soldiers.Length; i++)
            {
                var s = new GameObject($"{name}_{i}");
                s.transform.SetParent(go.transform);
                s.transform.position = soldiers[i].pos;
                s.AddComponent<Soldier>().Configure(soldiers[i].t, i == 0);
            }
            unit.RefreshSoldiers();
            return unit;
        }

        /// <summary>A squad: sergeant at the origin plus two troopers.</summary>
        private Unit Squad(string name = "Squad", Side side = Side.Player, float x = 0f) => MakeUnit(name, side,
            (sergeantT, new Vector3(x, 0, 0)), (trooperT, new Vector3(x - 2, 0, -1)), (trooperT, new Vector3(x + 2, 0, -1)));

        private GameObject Block(string name, int layer, Vector3 pos, Vector3 size, bool asObject)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(go);
            go.name = name;
            go.layer = layer;
            go.transform.position = pos;
            go.transform.localScale = size;
            if (asObject) go.AddComponent<TerrainObject>().Configure(name, true, true, false);
            Physics.SyncTransforms();
            return go;
        }

        // ---- The blast ----

        [Test]
        public void EveryoneWithin2Point5MetresTakesTheBlastFriendOrFoe()
        {
            var squad = Squad();
            var spot = new Vector3(0, 0, 8);
            var enemies = MakeUnit("Enemies", Side.AI,
                (unarmouredT, spot + new Vector3(1, 0, 0)),      // armor 0: 2 damage
                (trooperT, spot + new Vector3(0, 0, 2.4f)),      // armor 1 vs penetration 0: half, 1 damage
                (unarmouredT, spot + new Vector3(2.6f, 0, 0)));  // outside the blast
            var friend = MakeUnit("Friend", Side.Player, (unarmouredT, spot + new Vector3(-1.5f, 0, 0))).Soldiers[0];

            var r = ActiveAbilities.Use(squad.Leader, frag, targetPoint: spot);
            Assert.That(r.Used, Is.True, r.Reason);
            Assert.That(enemies.Soldiers[0].Health, Is.EqualTo(3));
            Assert.That(enemies.Soldiers[1].Health, Is.EqualTo(4));
            Assert.That(enemies.Soldiers[2].Health, Is.EqualTo(5), "2.6 m away");
            Assert.That(friend.Health, Is.EqualTo(3), "friendly fire");
            Assert.That(r.AreaHits.Count, Is.EqualTo(3));
        }

        [Test]
        public void NoLineOfSightIsNeededToThrowButTheSpotMustBeWithin10MetresOfTheLeader()
        {
            var squad = Squad();
            Block("Wall", TerrainLayers.Structure, new Vector3(0, 1.5f, 4), new Vector3(6, 3, 0.3f), asObject: false);
            Assert.That(ActiveAbilities.WhyCannotUse(squad.Leader, frag, targetPoint: new Vector3(0, 0, 9.9f)), Is.Null, "lobbed over the wall");
            Assert.That(ActiveAbilities.WhyCannotUse(squad.Leader, frag, targetPoint: new Vector3(0, 0, 10.5f)), Does.Contain("range"));
            Assert.That(ActiveAbilities.WhyCannotUse(squad.Leader, frag), Does.Contain("spot"));
        }

        [Test]
        public void ObjectTerrainBetweenTheBlastAndASoldierShieldsThem()
        {
            var squad = Squad();
            var spot = new Vector3(0, 0, 8);
            Block("Low Wall", TerrainLayers.Object, spot + new Vector3(1, 0.5f, 0), new Vector3(0.3f, 1f, 3f), asObject: true);
            var targets = MakeUnit("Enemies", Side.AI,
                (unarmouredT, spot + new Vector3(2, 0, 0)),     // behind the low wall
                (unarmouredT, spot + new Vector3(-2, 0, 0)));   // in the open

            var r = ActiveAbilities.Use(squad.Leader, frag, targetPoint: spot);
            Assert.That(targets.Soldiers[0].Health, Is.EqualTo(5), "the low wall stops the blast");
            Assert.That(targets.Soldiers[1].Health, Is.EqualTo(3));
            Assert.That(r.Shielded, Is.EqualTo(new[] { targets.Soldiers[0] }));
        }

        [Test]
        public void StructureTerrainBetweenTheBlastAndASoldierShieldsThem()
        {
            var squad = Squad();
            var spot = new Vector3(0, 0, 8);
            Block("Building Wall", TerrainLayers.Structure, spot + new Vector3(0, 1.5f, 1), new Vector3(4, 3, 0.3f), asObject: false);
            var targets = MakeUnit("Enemies", Side.AI,
                (unarmouredT, spot + new Vector3(0, 0, 2)),     // the other side of the wall
                (unarmouredT, spot + new Vector3(0, 0, -2)));   // same side as the blast

            ActiveAbilities.Use(squad.Leader, frag, targetPoint: spot);
            Assert.That(targets.Soldiers[0].Health, Is.EqualTo(5));
            Assert.That(targets.Soldiers[1].Health, Is.EqualTo(3));
        }

        [Test]
        public void ArmorKeywordsCountAgainstTheGrenade()
        {
            var squad = Squad();
            var spot = new Vector3(0, 0, 8);
            var sanctuary = Make<Keyword>();
            sanctuary.displayName = "Divine Sanctuary";
            sanctuary.interactions.Add(new KeywordInteraction
            {
                role = KeywordRole.WhenDefending, condition = KeywordCondition.Always, effect = KeywordEffect.ArmorBonus, amount = 1
            });
            var blessed = MakeUnit("Blessed", Side.AI, (trooperT, spot)).Soldiers[0];
            blessed.GainKeyword(sanctuary, Turns.NextTurn);

            var r = ActiveAbilities.Use(squad.Leader, frag, targetPoint: spot);
            Assert.That(blessed.Health, Is.EqualTo(5), "armor 2 vs penetration 0: blocked");
            Assert.That(r.AreaHits.Single().damage, Is.EqualTo(0));
        }

        [Test]
        public void SoldiersKilledByTheBlastAreRemovedWhenItEnds()
        {
            var squad = Squad();
            var spot = new Vector3(0, 0, 8);
            var victim = MakeUnit("Victim", Side.AI, (unarmouredT, spot)).Soldiers[0];
            victim.TakeDamage(4); // 1 health left
            ActiveAbilities.Use(squad.Leader, frag, targetPoint: spot);
            Assert.That(victim.IsAlive, Is.False);
            Assert.That(victim.gameObject.activeSelf, Is.False);
            Assert.That(victim.CanBeTargeted, Is.False);
        }

        // ---- Once per battle, one button ----

        [Test]
        public void EachUnitCanThrowOnlyOnceInTheWholeBattle()
        {
            var squad = Squad();
            var otherSquad = Squad("Other Squad", x: 20f);
            Assert.That(ActiveAbilities.Use(squad.Leader, frag, targetPoint: new Vector3(0, 0, 8)).Used, Is.True);

            Turns.StartNextTurn();
            Turns.StartNextTurn();
            var trooper = squad.Soldiers[1];
            Assert.That(ActiveAbilities.WhyCannotUse(squad.Leader, frag, targetPoint: new Vector3(0, 0, 8)), Does.Contain("this battle"));
            Assert.That(ActiveAbilities.WhyCannotUse(trooper, frag, targetPoint: new Vector3(-2, 0, 7)), Does.Contain("this battle"),
                "the troopers' grenades are the unit's one use");
            Assert.That(ActiveAbilities.Use(otherSquad.Leader, frag, targetPoint: new Vector3(20, 0, 8)).Used, Is.True,
                "another unit still has its own");
        }

        [Test]
        public void DuplicateAbilitiesAreOneEntryUsedByTheLeader()
        {
            var squad = Squad();
            var list = ActiveAbilities.UnitAbilities(squad);
            Assert.That(list.Select(e => e.ability), Is.EqualTo(new[] { frag, other }), "three soldiers have Frag Grenade: listed once");
            Assert.That(list.First().user, Is.EqualTo(squad.Leader));

            // If the leader doesn't have it, another soldier who does uses it.
            squad.Leader.Kill();
            Assert.That(ActiveAbilities.UserFor(squad, frag), Is.Not.Null);
            Assert.That(ActiveAbilities.UserFor(squad, frag).Abilities, Does.Contain(frag));
        }

        [Test]
        public void TheButtonShowsUsesLeftThisBattle()
        {
            var squad = Squad();
            Assert.That(MovementController.AbilityButtonLabel(squad.Leader, frag), Is.EqualTo("Frag Grenade — 10 m (1 left this battle)"));
            ActiveAbilities.Use(squad.Leader, frag, targetPoint: new Vector3(0, 0, 8));
            Turns.StartNextTurn();
            Assert.That(MovementController.AbilityButtonLabel(squad.Leader, frag), Does.Contain("already been used this battle"));
            Assert.That(MovementController.AbilityButtonLabel(squad.Leader, other), Is.EqualTo("Smoke — 10 m"), "no limit shown when there's none per battle");
        }

        // ---- The data ----

        [Test]
        public void TroopersAndSergeantsHaveFragGrenade()
        {
            var ability = AssetDatabase.LoadAssetAtPath<Ability>($"{Data}/Abilities/FragGrenade.asset");
            Assert.That((ability.kind, ability.targets, ability.range, ability.areaRadius, ability.usesPerBattle),
                Is.EqualTo((AbilityKind.Active, AbilityTarget.Point, 10f, 2.5f, 1)));
            Assert.That((ability.areaWeapon.baseDamage, ability.areaWeapon.penetration), Is.EqualTo((2, 0)));
            Assert.That(ability.areaWeapon.HasKeyword("Explosive"), Is.True);
            foreach (var file in new[] { "Trooper", "Sergeant" })
                Assert.That(AssetDatabase.LoadAssetAtPath<SoldierTemplate>($"{Data}/Soldiers/{file}.asset").abilities, Does.Contain(ability), file);
        }
    }
}
