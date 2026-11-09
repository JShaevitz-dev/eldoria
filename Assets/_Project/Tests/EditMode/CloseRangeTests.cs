using System.Collections.Generic;
using System.Linq;
using Eldoira.Rules;
using Eldoira.Terrain;
using Eldoira.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Eldoira.Tests
{
    /// <summary>
    /// Close range: a Biological soldier with a Biological enemy less than 2 m away can only use Pistol weapons;
    /// magic and non-weapon abilities still work; passive abilities are unaffected.
    /// </summary>
    public class CloseRangeTests
    {
        private const string Data = "Assets/_Project/Data";
        private readonly List<Object> created = new List<Object>();
        private Keyword biological, pistolKw;
        private WeaponTemplate rifleT, pistolT, grenadeT;
        private SoldierTemplate trooperT, sergeantT, robotT, priestT, commanderT;
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
            pistolKw = Kw("Pistol");

            rifleT = Make<WeaponTemplate>();
            rifleT.displayName = "Rifle";
            rifleT.baseDamage = 4; rifleT.penetration = 1;
            pistolT = Make<WeaponTemplate>();
            pistolT.displayName = "Pistol";
            pistolT.baseDamage = 2; pistolT.penetration = 0;
            pistolT.keywords.Add(pistolKw);
            grenadeT = Make<WeaponTemplate>();
            grenadeT.baseDamage = 2;

            frag = Make<Ability>();
            frag.displayName = "Frag Grenade";
            frag.kind = AbilityKind.Active; frag.targets = AbilityTarget.Point; frag.affects = AuraAffects.Everyone;
            frag.range = 10f; frag.areaRadius = 2.5f; frag.areaWeapon = grenadeT; frag.usesPerBattle = 1;

            heal = Make<Ability>();
            heal.displayName = "Divine Healing";
            heal.kind = AbilityKind.Active; heal.targets = AbilityTarget.Soldier; heal.range = 25f; heal.heal = 5;
            heal.keywords.Add(Kw("Spell"));

            var led = Kw("Led from the front");
            led.interactions.Add(new KeywordInteraction { role = KeywordRole.WhenAttacking, condition = KeywordCondition.Always, effect = KeywordEffect.AccuracyBonus, amount = 15 });
            lead = Make<Ability>();
            lead.kind = AbilityKind.Passive; lead.grantsNearby = led; lead.radius = 15f; lead.affects = AuraAffects.Allies; lead.includeSelf = false;

            trooperT = Soldier("Trooper", 100, rifleT);
            trooperT.abilities = new List<Ability> { frag };
            sergeantT = Soldier("Sergeant", 100, rifleT, pistolT);
            sergeantT.primaryWeapon = rifleT;
            sergeantT.abilities = new List<Ability> { frag };
            priestT = Soldier("Priest", 100, pistolT);
            priestT.abilities = new List<Ability> { heal };
            commanderT = Soldier("Commander", 100, pistolT);
            commanderT.abilities = new List<Ability> { lead };
            robotT = Make<SoldierTemplate>();
            robotT.displayName = "Robot";
            robotT.health = 10;
            robotT.weapons.Add(rifleT);
        }

        private SoldierTemplate Soldier(string name, int accuracy, params WeaponTemplate[] weapons)
        {
            var t = Make<SoldierTemplate>();
            t.displayName = name;
            t.health = 10;
            t.accuracy = accuracy;
            t.keywords.Add(biological);
            t.weapons.AddRange(weapons);
            return t;
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

        // ---- Who is in close range ----

        [Test]
        public void ABiologicalEnemyLessThan2MetresAwayPutsASoldierInCloseRange()
        {
            var me = MakeUnit("Me", Side.Player, (trooperT, Vector3.zero)).Soldiers[0];
            var enemy = MakeUnit("Enemy", Side.AI, (trooperT, new Vector3(1.99f, 0, 0))).Soldiers[0];
            Assert.That(CloseRange.IsInCloseRange(me), Is.True);
            Assert.That(CloseRange.IsInCloseRange(enemy), Is.True, "it works both ways");

            enemy.transform.position = new Vector3(2f, 0, 0);
            Assert.That(CloseRange.IsInCloseRange(me), Is.False, "less than 2 m: exactly 2 m is not close range");
        }

        [Test]
        public void FriendsAndNonBiologicalSoldiersDontCount()
        {
            var me = MakeUnit("Me", Side.Player, (trooperT, Vector3.zero)).Soldiers[0];
            MakeUnit("Friend", Side.Player, (trooperT, new Vector3(1, 0, 0)));
            var robot = MakeUnit("Robot", Side.AI, (robotT, new Vector3(0, 0, 1))).Soldiers[0];
            Assert.That(CloseRange.IsInCloseRange(me), Is.False, "a friend and a non-Biological enemy");
            Assert.That(CloseRange.IsInCloseRange(robot), Is.False, "a non-Biological soldier is never in close range");
        }

        [Test]
        public void TheDeadDontCount()
        {
            var me = MakeUnit("Me", Side.Player, (trooperT, Vector3.zero)).Soldiers[0];
            var enemy = MakeUnit("Enemy", Side.AI, (trooperT, new Vector3(1, 0, 0))).Soldiers[0];
            enemy.Kill();
            Assert.That(CloseRange.IsInCloseRange(me), Is.False);
        }

        // ---- Weapons ----

        [Test]
        public void InCloseRangeOnlyAPistolCanBeUsed()
        {
            var sergeant = MakeUnit("Squad", Side.Player, (sergeantT, Vector3.zero)).Soldiers[0];
            var trooper = MakeUnit("Squad 2", Side.Player, (trooperT, new Vector3(10, 0, 0))).Soldiers[0];
            Assert.That(sergeant.WeaponFor(null).Name, Is.EqualTo("Rifle"), "out of close range: primary weapon");

            MakeUnit("Enemy", Side.AI, (trooperT, new Vector3(1, 0, 0)), (trooperT, new Vector3(11, 0, 0)));
            Assert.That(sergeant.WeaponFor(null).Name, Is.EqualTo("Pistol"), "switches to the pistol");
            Assert.That(trooper.WeaponFor(null), Is.Null, "rifle only: can't shoot");
        }

        [Test]
        public void ASquadInCloseRangeShootsWithPistolsOnly()
        {
            var us = MakeUnit("Us", Side.Player, (sergeantT, Vector3.zero), (trooperT, new Vector3(4, 0, 0)), (trooperT, new Vector3(-6, 0, 0)));
            var them = MakeUnit("Them", Side.AI, (trooperT, new Vector3(0, 0, 1.5f)), (trooperT, new Vector3(4, 0, 1.5f)));
            // The third trooper is 6 m from anyone: not in close range, shoots its rifle.

            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            var bySoldier = r.Shots.ToDictionary(s => s.Shooter);
            Assert.That(bySoldier[us.Soldiers[0]].Weapon.Name, Is.EqualTo("Pistol"));
            Assert.That(bySoldier[us.Soldiers[1]].Weapon, Is.Null);
            Assert.That(bySoldier[us.Soldiers[1]].Note, Does.Contain("close range"));
            Assert.That(bySoldier[us.Soldiers[1]].Attack, Is.Null, "doesn't shoot");
            Assert.That(bySoldier[us.Soldiers[2]].Weapon.Name, Is.EqualTo("Rifle"));
        }

        // ---- Abilities ----

        [Test]
        public void AWeaponAbilityCantBeUsedInCloseRangeButAnotherSoldierCanThrowIt()
        {
            var squad = MakeUnit("Squad", Side.Player, (sergeantT, Vector3.zero), (trooperT, new Vector3(4, 0, 0)));
            MakeUnit("Enemy", Side.AI, (trooperT, new Vector3(0, 0, 1)));
            var sergeant = squad.Soldiers[0];
            Assert.That(ActiveAbilities.WhyCannotCast(sergeant, frag), Does.Contain("close range"));

            var user = ActiveAbilities.UserFor(squad, frag);
            Assert.That(user, Is.EqualTo(squad.Soldiers[1]), "the trooper out of close range throws it");
            Assert.That(ActiveAbilities.Use(user, frag, targetPoint: new Vector3(4, 0, 8)).Used, Is.True);
        }

        [Test]
        public void IfEveryoneIsInCloseRangeTheGrenadeCantBeThrown()
        {
            var squad = MakeUnit("Squad", Side.Player, (sergeantT, Vector3.zero), (trooperT, new Vector3(1, 0, 0)));
            MakeUnit("Enemy", Side.AI, (trooperT, new Vector3(0.5f, 0, 1)));
            var user = ActiveAbilities.UserFor(squad, frag);
            Assert.That(ActiveAbilities.Use(user, frag, targetPoint: new Vector3(0, 0, 8)).Reason, Does.Contain("close range"));
        }

        [Test]
        public void SpellsStillWorkInCloseRange()
        {
            var priest = MakeUnit("Priest", Side.Player, (priestT, Vector3.zero)).Soldiers[0];
            MakeUnit("Enemy", Side.AI, (trooperT, new Vector3(1, 0, 0)));
            priest.TakeDamage(3);
            Assert.That(CloseRange.IsInCloseRange(priest), Is.True);
            Assert.That(ActiveAbilities.Use(priest, heal, priest).Used, Is.True);
        }

        [Test]
        public void PassiveAbilitiesAreUnaffected()
        {
            var commander = MakeUnit("Commander", Side.Player, (commanderT, Vector3.zero)).Soldiers[0];
            var ally = MakeUnit("Squad", Side.Player, (trooperT, new Vector3(5, 0, 0))).Soldiers[0];
            MakeUnit("Enemy", Side.AI, (trooperT, new Vector3(-1, 0, 0)));
            NearbyKeywords.RefreshAll();
            Assert.That(CloseRange.IsInCloseRange(commander), Is.True);
            Assert.That(ally.HasKeyword("Led from the front"), Is.True, "the aura still works");
        }

        // ---- The status keyword ----

        [Test]
        public void SoldiersInCloseRangeShowTheInCloseRangeKeywordUntilTheEnemyIsGone()
        {
            var me = MakeUnit("Me", Side.Player, (trooperT, Vector3.zero)).Soldiers[0];
            var enemy = MakeUnit("Enemy", Side.AI, (trooperT, new Vector3(1, 0, 0))).Soldiers[0];
            NearbyKeywords.RefreshAll();
            Assert.That(me.HasKeyword("In close range"), Is.True);
            Assert.That(enemy.HasKeyword("In close range"), Is.True);

            enemy.Kill(); // removed at once, and nearby keywords refresh
            Assert.That(me.HasKeyword("In close range"), Is.False);
        }

        [Test]
        public void ThePistolHasThePistolKeywordAndTheRifleDoesnt()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{Data}/Weapons/Pistol.asset").HasKeyword("Pistol"), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{Data}/Weapons/AssaultRifle.asset").HasKeyword("Pistol"), Is.False);
            var status = AssetDatabase.LoadAssetAtPath<Keyword>($"{Data}/Keywords/InCloseRange.asset");
            Assert.That(status.category, Is.EqualTo(KeywordCategory.Status));
        }
    }
}
