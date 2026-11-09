using System.Collections.Generic;
using System.Linq;
using Eldoira.Rules;
using Eldoira.Units;
using NUnit.Framework;
using UnityEngine;

namespace Eldoira.Tests
{
    public class UnitShootingTests
    {
        private readonly List<Object> created = new List<Object>();
        private WeaponTemplate rifleT;
        private SoldierTemplate shooterT, targetT;
        private Keyword biological;
        private System.Func<Soldier, Soldier, bool> originalSight;

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
            originalSight = LineOfSight.CanSee;
            Turns.ResetCounter();
            Turns.StartNextTurn();

            biological = Kw("Biological");
            rifleT = Make<WeaponTemplate>();
            rifleT.displayName = "Rifle";
            rifleT.baseDamage = 2;
            rifleT.penetration = 1;

            shooterT = Make<SoldierTemplate>();
            shooterT.displayName = "Shooter";
            shooterT.accuracy = 100;   // always hits, never crits
            shooterT.keywords.Add(biological);
            shooterT.weapons.Add(rifleT);

            targetT = Make<SoldierTemplate>();
            targetT.displayName = "Target";
            targetT.health = 10;
            targetT.armorLevel = 1;
            targetT.keywords.Add(biological);
            targetT.weapons.Add(rifleT);
        }

        [TearDown]
        public void TearDown()
        {
            LineOfSight.CanSee = originalSight;
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
        public void EverySoldierShootsTheClosestEnemySoldier()
        {
            var us = MakeUnit("Us", Side.Player, shooterT, new Vector3(-3, 0, 0), new Vector3(0, 0, 0), new Vector3(3, 0, 0));
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(-3, 0, 10), new Vector3(3, 0, 10));

            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(r.HasLineOfSight, Is.True);
            Assert.That(r.Shots.Count, Is.EqualTo(3));
            Assert.That(r.Shots[0].Target, Is.EqualTo(them.Soldiers[0]), "left shooter → left target");
            Assert.That(r.Shots[2].Target, Is.EqualTo(them.Soldiers[1]), "right shooter → right target");
            Assert.That(r.Hits, Is.EqualTo(3));
            Assert.That(r.TotalDamage, Is.EqualTo(3 * 2));
        }

        [Test]
        public void SeveralSoldiersCanPickTheSameTarget()
        {
            var us = MakeUnit("Us", Side.Player, shooterT, new Vector3(-1, 0, 0), new Vector3(0, 0, 0), new Vector3(1, 0, 0));
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10), new Vector3(0, 0, 30));

            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(r.Shots.All(s => s.Target == them.Soldiers[0]), Is.True);
            Assert.That(them.Soldiers[0].Health, Is.EqualTo(10 - 3 * 2));
        }

        [Test]
        public void NoLineOfSightMeansNoShooting()
        {
            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10));
            LineOfSight.CanSee = (a, t) => false;

            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(r.HasLineOfSight, Is.False);
            Assert.That(r.Shots, Is.Empty);
            Assert.That(them.Soldiers[0].Health, Is.EqualTo(10));
        }

        [Test]
        public void KeywordsThatPreventTheAttackStopOnlyThatSoldiersAttack()
        {
            // The first shooter carries a Toxic weapon; the target isn't Biological.
            var toxic = Kw("Toxic");
            toxic.interactions.Add(new KeywordInteraction
            {
                party = KeywordParty.Target, condition = KeywordCondition.Lacks, keyword = biological, effect = KeywordEffect.NoEffect
            });
            var sprayerT = Make<WeaponTemplate>();
            sprayerT.baseDamage = 5;
            sprayerT.keywords.Add(toxic);
            var sprayerSoldierT = Make<SoldierTemplate>();
            sprayerSoldierT.accuracy = 100;
            sprayerSoldierT.weapons.Add(sprayerT);
            targetT.keywords.Clear(); // a tank

            var us = MakeUnit("Us", Side.Player, shooterT, new Vector3(1, 0, 0));
            var sprayer = new GameObject("Sprayer");
            sprayer.transform.SetParent(us.transform);
            sprayer.transform.position = Vector3.zero;
            sprayer.AddComponent<Soldier>().Configure(sprayerSoldierT, false);
            us.RefreshSoldiers();
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10));

            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            var sprayerShot = r.Shots.Single(s => s.Shooter.name == "Sprayer");
            Assert.That(sprayerShot.Target, Is.Null, "no enemy it could usefully shoot, so it doesn't shoot");
            Assert.That(sprayerShot.Rejected.Count, Is.EqualTo(1));
            Assert.That(r.Shots.Single(s => s.Shooter.name != "Sprayer").Attack.Hit, Is.True);
            Assert.That(them.Soldiers[0].Health, Is.EqualTo(10 - 2), "only the rifle did damage");
        }

        [Test]
        public void BeingTargetedTriggersBeforeTheAttackIsResolved()
        {
            // "When targeted, gain Braced"; Braced: −1 damage when defending.
            var braced = Kw("Braced");
            braced.interactions.Add(new KeywordInteraction
            {
                role = KeywordRole.WhenDefending, condition = KeywordCondition.Always, effect = KeywordEffect.DamagePenalty, amount = 1
            });
            var alert = Kw("Alert");
            alert.triggers.Add(new KeywordTrigger { when = TriggerEvent.Targeted, action = TriggerAction.GainKeyword, subject = TriggerSubject.Self, keyword = braced });
            targetT.keywords.Add(alert);

            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10));

            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(them.Soldiers[0].HasKeyword("Braced"), Is.True);
            Assert.That(r.Shots[0].Attack.FinalDamage, Is.EqualTo(2 - 1), "Braced applied to the same attack");
            Assert.That(r.Log.Any(l => l.Contains("gains Braced")), Is.True);

            Turns.StartNextTurn();
            Assert.That(them.Soldiers[0].HasKeyword("Braced"), Is.False, "wore off at the next turn");
        }

        private Keyword AlertTrigger()
        {
            var spotted = Kw("Spotted");
            var alert = Kw("Alert");
            alert.triggers.Add(new KeywordTrigger { when = TriggerEvent.Targeted, action = TriggerAction.GainKeyword, subject = TriggerSubject.Self, keyword = spotted });
            return alert;
        }

        private static int CountTargetedTriggers(System.Action shoot)
        {
            int count = 0;
            void OnFired(TriggerEvent e, Soldier self, Soldier other) { if (e == TriggerEvent.Targeted) count++; }
            KeywordTriggers.Fired += OnFired;
            try { shoot(); }
            finally { KeywordTriggers.Fired -= OnFired; }
            return count;
        }

        [Test]
        public void WhenTargetedFiresOncePerVolleyHoweverManySoldiersPickThem()
        {
            targetT.keywords.Add(AlertTrigger());
            var us = MakeUnit("Us", Side.Player, shooterT, new Vector3(-1, 0, 0), Vector3.zero, new Vector3(1, 0, 0));
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10));

            ShootingResult r = null;
            int fired = CountTargetedTriggers(() => r = UnitShooting.Resolve(us, them, false, new System.Random(1)));
            Assert.That(r.Shots.All(s => s.Target == them.Soldiers[0]), Is.True, "all three picked the same soldier");
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(r.Log.Count(l => l.Contains("gains Spotted")), Is.EqualTo(1));
        }

        [Test]
        public void WhenTargetedFiresAgainTheNextTimeTheUnitIsShotAt()
        {
            targetT.keywords.Add(AlertTrigger());
            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10));

            int first = CountTargetedTriggers(() => UnitShooting.Resolve(us, them, false, new System.Random(1)));
            int second = CountTargetedTriggers(() => UnitShooting.Resolve(us, them, false, new System.Random(2)));
            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(1));
        }

        [Test]
        public void EachAttackingUnitsVolleyCanTriggerWhenTargetedInTheSameTurn()
        {
            targetT.keywords.Add(AlertTrigger());
            var alpha = MakeUnit("Alpha", Side.Player, shooterT, new Vector3(-1, 0, 0), new Vector3(1, 0, 0));
            var bravo = MakeUnit("Bravo", Side.Player, shooterT, new Vector3(-1, 0, 2), new Vector3(1, 0, 2));
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 12));

            // Same turn: two different units shoot the same enemy unit. Each volley triggers once.
            int alphaVolley = CountTargetedTriggers(() => UnitShooting.Resolve(alpha, them, false, new System.Random(1)));
            int bravoVolley = CountTargetedTriggers(() => UnitShooting.Resolve(bravo, them, false, new System.Random(2)));
            Assert.That(alphaVolley, Is.EqualTo(1), "two Alpha soldiers picked the target: one trigger");
            Assert.That(bravoVolley, Is.EqualTo(1), "Bravo's volley triggers it again");
        }

        [Test]
        public void WhenTargetedFiresEvenIfNoAttackFollows()
        {
            // A Toxic shooter picks a tank (not Biological), so step 7 rejects it — but it was still targeted.
            var toxic = Kw("Toxic");
            toxic.interactions.Add(new KeywordInteraction
            {
                party = KeywordParty.Target, condition = KeywordCondition.Lacks, keyword = biological, effect = KeywordEffect.NoEffect
            });
            var sprayerT = Make<WeaponTemplate>();
            sprayerT.keywords.Add(toxic);
            shooterT.weapons = new List<WeaponTemplate> { sprayerT };
            targetT.keywords.Clear();
            targetT.keywords.Add(AlertTrigger());

            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10));

            ShootingResult r = null;
            int fired = CountTargetedTriggers(() => r = UnitShooting.Resolve(us, them, false, new System.Random(1)));
            Assert.That(r.Shots[0].Target, Is.Null, "no attack");
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(them.Soldiers[0].HasKeyword("Spotted"), Is.True);
        }

        [Test]
        public void TakingDamageTriggersAfterDamageIsApplied()
        {
            var wounded = Kw("Wounded");
            var pain = Kw("Feels pain");
            pain.triggers.Add(new KeywordTrigger { when = TriggerEvent.Damaged, action = TriggerAction.GainKeyword, subject = TriggerSubject.Self, keyword = wounded, untilNextTurn = false });
            targetT.keywords.Add(pain);

            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10), new Vector3(0, 0, 40));

            UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(them.Soldiers[0].HasKeyword("Wounded"), Is.True);
            Assert.That(them.Soldiers[1].HasKeyword("Wounded"), Is.False, "not shot");
        }

        [Test]
        public void SoldiersShootTheTargetTheyPickedEvenIfItDiedFirst()
        {
            targetT.health = 2; // one rifle hit kills
            var us = MakeUnit("Us", Side.Player, shooterT, new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0));
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10), new Vector3(0, 0, 40));

            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(r.Kills, Is.EqualTo(1));
            Assert.That(r.Shots.All(s => s.Target == them.Soldiers[0]), Is.True, "both picked the closest soldier");
            var second = r.Shots[1];
            Assert.That(second.TargetAlreadyDown, Is.True);
            Assert.That(second.Attack, Is.Not.Null, "the shot is still made");
            Assert.That(second.Attack.DamageDealt, Is.EqualTo(0));
            Assert.That(them.Soldiers[1].Health, Is.EqualTo(2), "the second shot did not move on to another soldier");
        }

        [Test]
        public void SoldiersKilledThisTurnCanStillBeTargetedUntilTheNextTurn()
        {
            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10), new Vector3(0, 0, 30));
            var fallen = them.Soldiers[0];
            fallen.Kill();
            Assert.That(fallen.CanBeTargeted, Is.True);
            Assert.That(fallen.gameObject.activeSelf, Is.True, "the body stays on the battlefield this turn");

            // Another unit shooting this turn still picks the closest soldier, even though they're down.
            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(r.Shots[0].Target, Is.EqualTo(fallen));

            Turns.StartNextTurn();
            Assert.That(fallen.CanBeTargeted, Is.False);
            Assert.That(fallen.gameObject.activeSelf, Is.False, "removed at the start of the next turn");
            var next = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(next.Shots[0].Target, Is.EqualTo(them.Soldiers[1]));
        }

        [Test]
        public void IfKeywordsPreventTheAttackTheSoldierTriesTheNextClosest()
        {
            // A Toxic sprayer: the closest enemy is a tank (not Biological), the next is a trooper (Biological).
            var toxic = Kw("Toxic");
            toxic.interactions.Add(new KeywordInteraction
            {
                party = KeywordParty.Target, condition = KeywordCondition.Lacks, keyword = biological, effect = KeywordEffect.NoEffect
            });
            var sprayerT = Make<WeaponTemplate>();
            sprayerT.baseDamage = 3;
            sprayerT.penetration = 1;
            sprayerT.keywords.Add(toxic);
            shooterT.weapons = new List<WeaponTemplate> { sprayerT };
            var tankT = Make<SoldierTemplate>();
            tankT.health = 10;
            tankT.weapons.Add(rifleT);

            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, tankT, new Vector3(0, 0, 5));
            var trooper = new GameObject("Trooper");
            trooper.transform.SetParent(them.transform);
            trooper.transform.position = new Vector3(0, 0, 12);
            trooper.AddComponent<Soldier>().Configure(targetT, false);
            them.RefreshSoldiers();

            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(r.Shots[0].Rejected.Single().name, Is.EqualTo("Them_0"), "tried the tank first");
            Assert.That(r.Shots[0].Target.name, Is.EqualTo("Trooper"));
            Assert.That(r.Shots[0].Attack.DamageDealt, Is.EqualTo(3));
        }

        [Test]
        public void ASoldierWithNoTargetItCanShootDoesNotShoot()
        {
            var toxic = Kw("Toxic");
            toxic.interactions.Add(new KeywordInteraction
            {
                party = KeywordParty.Target, condition = KeywordCondition.Lacks, keyword = biological, effect = KeywordEffect.NoEffect
            });
            var sprayerT = Make<WeaponTemplate>();
            sprayerT.keywords.Add(toxic);
            shooterT.weapons = new List<WeaponTemplate> { sprayerT };
            targetT.keywords.Clear(); // a unit of tanks

            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 5), new Vector3(0, 0, 12));

            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(r.Shots[0].Target, Is.Null);
            Assert.That(r.Shots[0].Rejected.Count, Is.EqualTo(2), "checked every enemy soldier");
            Assert.That(r.Shots[0].Attack, Is.Null);
        }

        [Test]
        public void SoldiersShootWithTheirPrimaryWeapon()
        {
            var pistolT = Make<WeaponTemplate>();
            pistolT.displayName = "Pistol";
            pistolT.baseDamage = 1;
            shooterT.weapons = new List<WeaponTemplate> { pistolT, rifleT };
            shooterT.primaryWeapon = rifleT;

            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10));
            Assert.That(us.Soldiers[0].PrimaryWeapon.Template, Is.EqualTo(rifleT));

            var r = UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(r.Shots[0].Weapon.Template, Is.EqualTo(rifleT));
            Assert.That(r.Shots[0].Attack.DamageDealt, Is.EqualTo(2));
        }

        [Test]
        public void WithoutAPrimaryWeaponTheFirstWeaponIsUsed()
        {
            var pistolT = Make<WeaponTemplate>();
            shooterT.weapons = new List<WeaponTemplate> { pistolT, rifleT };
            shooterT.primaryWeapon = null;
            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero);
            Assert.That(us.Soldiers[0].PrimaryWeapon.Template, Is.EqualTo(pistolT));
        }

        [Test]
        public void KillingTheLeaderPassesCommandMidShooting()
        {
            targetT.health = 2;
            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero);
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10), new Vector3(1, 0, 11));
            var leader = them.Leader;

            UnitShooting.Resolve(us, them, false, new System.Random(1));
            Assert.That(leader.IsAlive, Is.False);
            Assert.That(them.Leader, Is.EqualTo(them.Soldiers[1]));
        }

        [Test]
        public void SprintingAppliesToTheWholeUnitsShooting()
        {
            var us = MakeUnit("Us", Side.Player, shooterT, Vector3.zero, new Vector3(1, 0, 0));
            var them = MakeUnit("Them", Side.AI, targetT, new Vector3(0, 0, 10));
            var r = UnitShooting.Resolve(us, them, attackersSprinted: true, new System.Random(1));
            Assert.That(r.Shots.All(s => s.Attack.Accuracy.Final == 75), Is.True);
        }
    }
}
