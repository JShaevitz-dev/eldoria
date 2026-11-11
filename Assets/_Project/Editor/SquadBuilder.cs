using System.Collections.Generic;
using System.Linq;
using Eldoria.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Eldoria.EditorTools
{
    /// <summary>
    /// Creates the template assets for the test unit and spawns it in the open scene:
    ///   Keywords: Biological, Toxic, Moved, Explosive, Incendiary, Robotic (Data/Keywords)
    ///             + the keyword library (Resources/KeywordLibrary)
    ///   Weapons:  Assault Rifle, Pistol                      (Data/Weapons)
    ///   Soldiers: Trooper (rifle), Sergeant (rifle + pistol), both Biological (Data/Soldiers)
    ///   Unit:     Infantry Squad = 1 Sergeant + 9 Troopers    (Data/Units)
    /// plus placeholder soldier prefabs built from primitives. Existing template assets are kept, so
    /// stats edited in the Inspector survive a rebuild.
    /// Menu: Eldoria > Spawn Test Unit.
    /// </summary>
    public static class SquadBuilder
    {
        private const string Root = "Assets/_Project";
        private const string DataDir = Root + "/Data";
        private const string PrefabDir = Root + "/Prefabs/Units";
        private const string MaterialDir = Root + "/Materials";

        private const string UnitName = "Infantry Squad";

        [MenuItem("Eldoria/Spawn Test Unit")]
        public static void SpawnInOpenScene()
        {
            SpawnTestSquad();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public static Unit SpawnTestSquad()
        {
            var infantry = CreateTemplates();

            // Replace any previous copy of the test unit.
            foreach (var old in Object.FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (old.DisplayName == UnitName && old.Side == Side.Player) Object.DestroyImmediate(old.gameObject);

            // Deployed near the south (player) side, facing north.
            var unit = UnitFactory.Spawn(infantry, Side.Player, new Vector3(0f, 0f, -16f), Quaternion.identity,
                unitName: UnitName,
                instantiate: (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent));

            var marker = unit.gameObject.AddComponent<LeaderMarker>();
            var markerSo = new SerializedObject(marker);
            markerSo.FindProperty("material").objectReferenceValue = GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f));
            markerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(unit);
            Debug.Log($"[Eldoria] Spawned {unit.Soldiers.Count}-soldier unit '{unit.DisplayName}' from template " +
                      $"'{infantry.displayName}', leader {unit.Leader.name}, in formation: {unit.IsInFormation()}");
            return unit;
        }

        private const string EnemyName = "Infantry Squad (Enemy)";
        private const string CommanderName = "Field Commander";

        /// <summary>
        /// Field Commander unit: 1 Field Commander (Health 10, Armor 1, Accuracy 75%, 10 m; Biological, Officer, Solo;
        /// Lead from the front; Pistol).
        /// </summary>
        public static UnitTemplate CreateFieldCommander()
        {
            CreateTemplates(); // shared weapons, keywords, materials
            EnsureFolder(DataDir, "Abilities");
            var keywords = EnsureKeywords();
            var pistol = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/Pistol.asset");

            var leadFromTheFront = GetOrCreate<Ability>($"{DataDir}/Abilities/LeadFromTheFront.asset", a =>
            {
                a.displayName = "Lead from the front";
                a.description = "Passive. Other friendly soldiers within 15 m gain +15% accuracy. Each soldier is checked on its own.";
                a.kind = AbilityKind.Passive;
                a.grantsNearby = keywords["Led from the front"];
                a.radius = 15f;
                a.affects = AuraAffects.Allies;
                a.includeSelf = false; // the Field Commander does not benefit
            });

            var commander = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/FieldCommander.asset", s =>
            {
                s.displayName = "Field Commander";
                s.health = 10; s.armorLevel = 1; s.accuracy = 75; s.movementRange = 10f;
                s.keywords = new List<Keyword> { keywords["Biological"], keywords["Officer"], keywords["Solo"] };
                s.abilities = new List<Ability> { leadFromTheFront };
                s.weapons = new List<WeaponTemplate> { pistol };
                s.primaryWeapon = pistol;
            });
            commander.prefab = BuildSoldierPrefab("FieldCommander", commander, true,
                GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f)),
                GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f)),
                GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f)),
                GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f)),
                GetOrCreateMaterial("Commander_Cape", new Color(0.45f, 0.08f, 0.1f)));
            EditorUtility.SetDirty(commander);

            var unit = GetOrCreate<UnitTemplate>($"{DataDir}/Units/FieldCommander.asset", u =>
            {
                u.displayName = "Field Commander";
                u.leader = commander;
                u.members = new List<UnitTemplate.Member>();
            });
            AssetDatabase.SaveAssets();
            return unit;
        }

        private const string PriestName = "Battle Priest";

        /// <summary>
        /// Battle Priest unit (Specialist): 1 Battle Priest (Health 5, Armor 0, Accuracy 50%, 10 m; Biological, Officer,
        /// Solo, Magic; Divine Healing, Divine Sanctuary; Pistol).
        /// </summary>
        public static UnitTemplate CreateBattlefieldPriest()
        {
            CreateTemplates();
            EnsureFolder(DataDir, "Abilities");
            var keywords = EnsureKeywords();
            var pistol = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/Pistol.asset");

            var healing = GetOrCreate<Ability>($"{DataDir}/Abilities/DivineHealing.asset", a =>
            {
                a.displayName = "Divine Healing";
                a.description = "Active. Choose a friendly Biological soldier within 25 m: they restore 5 health.";
                a.kind = AbilityKind.Active;
                a.targets = AbilityTarget.Soldier;
                a.affects = AuraAffects.Allies;
                a.includeSelf = true;
                a.range = 25f;
                a.targetMustHave = keywords["Biological"];
                a.heal = 5;
                a.usesPerTurn = 1;
            }, a => TagAsSpell(a, keywords));
            var sanctuary = GetOrCreate<Ability>($"{DataDir}/Abilities/DivineSanctuary.asset", a =>
            {
                a.displayName = "Divine Sanctuary";
                a.description = "Active. Choose a friendly unit within 25 m: every soldier in it gains +1 armor until the next turn.";
                a.kind = AbilityKind.Active;
                a.targets = AbilityTarget.Unit;
                a.affects = AuraAffects.Allies;
                a.includeSelf = true;
                a.range = 25f;
                a.grantsKeyword = keywords["Divine Sanctuary"];
                a.grantUntilNextTurn = true;
                a.usesPerTurn = 1;
            }, a => TagAsSpell(a, keywords));

            var priest = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/BattlefieldPriest.asset", s =>
            {
                s.displayName = PriestName;
                s.health = 5; s.armorLevel = 0; s.accuracy = 50; s.movementRange = 10f;
                s.keywords = new List<Keyword> { keywords["Biological"], keywords["Officer"], keywords["Solo"], keywords["Magic"] };
                s.abilities = new List<Ability> { healing, sanctuary };
                s.weapons = new List<WeaponTemplate> { pistol };
                s.primaryWeapon = pistol;
            });
            priest.prefab = BuildSoldierPrefab("BattlefieldPriest", priest, false,
                GetOrCreateMaterial("Priest_Robe", new Color(0.88f, 0.85f, 0.76f)),
                GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f)),
                GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f)),
                GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f)),
                priest: true);
            EditorUtility.SetDirty(priest);

            var unit = GetOrCreate<UnitTemplate>($"{DataDir}/Units/BattlefieldPriest.asset", u =>
            {
                u.displayName = PriestName;
                u.leader = priest;
                u.members = new List<UnitTemplate.Member>();
            });
            AssetDatabase.SaveAssets();
            return unit;
        }

        /// <summary>
        /// Frag Grenade: the unit (any trooper or its sergeant) throws one grenade, once per battle, at a spot within 10 m of
        /// the unit leader. Everyone within 2.5 m of the spot whom the blast can reach takes 2 damage, penetration 0.
        /// The blast is the "Frag Grenade" weapon (keyword Explosive).
        /// </summary>
        public static Ability EnsureFragGrenade(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Weapons");
            EnsureFolder(DataDir, "Abilities");
            var weapon = GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/FragGrenade.asset", w =>
            {
                w.displayName = "Frag Grenade";
                w.penetration = 0; w.accuracyBonus = 0;
                w.keywords = new List<Keyword> { keywords["Explosive"] };
            });
            weapon.baseDamage = 5;
            EditorUtility.SetDirty(weapon);
            var ability = GetOrCreate<Ability>($"{DataDir}/Abilities/FragGrenade.asset", a =>
            {
                a.displayName = "Frag Grenade";
                a.description = "Active, once per battle per unit. Throw at a spot within 10 m of the unit leader: everyone within 2.5 m " +
                                "takes 5 damage (penetration 0), unless terrain shields them from the blast.";
                a.kind = AbilityKind.Active;
                a.targets = AbilityTarget.Point;
                a.affects = AuraAffects.Everyone;
                a.includeSelf = true;
                a.range = 10f;
                a.areaRadius = 2.5f;
                a.areaWeapon = weapon;
                a.usesPerTurn = 1;
                a.usesPerBattle = 1;
            });
            ability.description = "Active, once per battle per unit. Throw at a spot within 10 m of the unit leader: everyone within 2.5 m " +
                                  "takes 5 damage (penetration 0), unless terrain shields them from the blast.";
            EditorUtility.SetDirty(ability);
            return ability;
        }

        /// <summary>
        /// SMG: Base Damage as the Assault Rifle (4), Penetration 0, Accuracy Bonus +0, no keywords.
        /// Range: the same as the Pistol (weapon range isn't in the rules yet). Not carried by anyone yet.
        /// </summary>
        public static WeaponTemplate EnsureSmg()
        {
            EnsureFolder(DataDir, "Weapons");
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/AssaultRifle.asset");
            return GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/SMG.asset", w =>
            {
                w.displayName = "SMG";
                w.baseDamage = rifle != null ? rifle.baseDamage : 4;   // same damage as the rifle
                w.hasPenetration = true;
                w.penetration = 0;
                w.accuracyBonus = 0;
                w.range = 10f;                                          // same as the pistol
                w.keywords = new List<Keyword>();
            });
        }

        /// <summary>
        /// Machine Gun: Base Damage 6, Penetration 1, Accuracy Bonus +0, range 20 m, keyword Deployed (can't be used in a
        /// volley if the soldier holding it has moved this turn). Not carried by anyone yet.
        /// </summary>
        public static WeaponTemplate EnsureMachineGun(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Weapons");
            return GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/MachineGun.asset", w =>
            {
                w.displayName = "Machine Gun";
                w.baseDamage = 5;
                w.hasPenetration = true;
                w.penetration = 1;
                w.accuracyBonus = 0;
                w.range = 20f;
                w.keywords = new List<Keyword> { keywords["Deployed"] };
            });
        }

        /// <summary>
        /// Machine Gun Team (not fielded in the demo yet):
        ///   1 Sergeant (leader) with a Pistol,
        ///   3 Troopers with an SMG (primary) and a Pistol,
        ///   1 Machine Gunner with a Machine Gun.
        /// Same soldier stats as the Infantry Squad (Health 5, Armor Level 1, Accuracy 75%, 10 m, Biological); no Frag Grenade.
        /// </summary>
        public static UnitTemplate EnsureMachineGunTeam(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Soldiers");
            EnsureFolder(DataDir, "Units");
            var pistol = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/Pistol.asset");
            var smg = EnsureSmg();
            var mg = EnsureMachineGun(keywords);
            var biological = keywords["Biological"];

            void Stats(SoldierTemplate s)
            {
                s.health = 5; s.armorLevel = 1; s.accuracy = 75; s.movementRange = 10f;
                s.keywords = new List<Keyword> { biological };
                s.abilities = new List<Ability>();
            }

            var sergeant = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/MGTeamSergeant.asset", s =>
            {
                s.displayName = "Sergeant";
                Stats(s);
                s.weapons = new List<WeaponTemplate> { pistol };
                s.primaryWeapon = pistol;
            });
            var smgTrooper = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/SMGTrooper.asset", s =>
            {
                s.displayName = "Trooper";
                Stats(s);
                s.weapons = new List<WeaponTemplate> { smg, pistol };
                s.primaryWeapon = smg;
            });
            var gunner = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/MachineGunner.asset", s =>
            {
                s.displayName = "Machine Gunner";
                Stats(s);
                s.weapons = new List<WeaponTemplate> { mg };
                s.primaryWeapon = mg;
            });

            // Models: the squad's sergeant and trooper bodies; the gunner gets a heavier gun.
            sergeant.prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Sergeant.prefab");
            smgTrooper.prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Trooper.prefab");
            if (gunner.prefab == null)
                gunner.prefab = BuildSoldierPrefab("MachineGunner", gunner, false,
                    GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f)),
                    GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f)),
                    GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f)),
                    GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f)),
                    heavyWeapon: true);
            foreach (var t in new[] { sergeant, smgTrooper, gunner }) EditorUtility.SetDirty(t);

            var unit = GetOrCreate<UnitTemplate>($"{DataDir}/Units/MachineGunTeam.asset", u =>
            {
                u.displayName = "Machine Gun Team";
                u.leader = sergeant;
                u.members = new List<UnitTemplate.Member>
                {
                    new UnitTemplate.Member { soldier = smgTrooper, count = 3 },
                    new UnitTemplate.Member { soldier = gunner, count = 1 },
                };
                u.deploymentSpacing = 2f;
            });
            AssetDatabase.SaveAssets();
            return unit;
        }

        /// <summary>
        /// Flamethrower: Base Damage 10, no penetration value ("X": always 100% damage), Accuracy Bonus +0, range 10 m,
        /// keywords Incendiary (only damages Biological targets) and Unstable (when the holder dies: 10% chance to explode,
        /// 5 damage - no penetration value - to every soldier within 5 m). Carried by the Flamethrower Team.
        /// </summary>
        /// <summary>
        /// Flamethrower Team: two Flamethrower Troopers (one leads), each with a Flamethrower (primary) and a Pistol.
        /// Trooper stats as in the Infantry Squad (Health 5, Armor Level 1, Accuracy 75%, 10 m, Biological); no Frag Grenade.
        /// </summary>
        public static UnitTemplate EnsureFlamethrowerTeam(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Soldiers");
            EnsureFolder(DataDir, "Units");
            var flamer = EnsureFlamethrower(keywords);
            var pistol = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/Pistol.asset");

            var trooper = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/FlamethrowerTrooper.asset", s =>
            {
                s.displayName = "Flamethrower Trooper";
                s.health = 5; s.armorLevel = 1; s.accuracy = 75; s.movementRange = 10f;
                s.keywords = new List<Keyword> { keywords["Biological"] };
                s.abilities = new List<Ability>();
                s.weapons = new List<WeaponTemplate> { flamer, pistol };
                s.primaryWeapon = flamer;
            });
            if (trooper.prefab == null)
                trooper.prefab = BuildSoldierPrefab("FlamethrowerTrooper", trooper, false,
                    GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f)),
                    GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f)),
                    GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f)),
                    GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f)),
                    flameKit: true);
            EditorUtility.SetDirty(trooper);

            var unit = GetOrCreate<UnitTemplate>($"{DataDir}/Units/FlamethrowerTeam.asset", u =>
            {
                u.displayName = "Flamethrower Team";
                u.leader = trooper;
                u.members = new List<UnitTemplate.Member> { new UnitTemplate.Member { soldier = trooper, count = 1 } };
                u.deploymentSpacing = 2f;
            });
            AssetDatabase.SaveAssets();
            return unit;
        }

        public static WeaponTemplate EnsureFlamethrower(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Weapons");

            // Incendiary: the attack has no effect on a target that isn't Biological.
            var incendiary = keywords["Incendiary"];
            incendiary.description = "Only damages targets with the Biological keyword.";
            incendiary.interactions = new List<KeywordInteraction>
            {
                new KeywordInteraction { party = KeywordParty.Target, condition = KeywordCondition.Lacks, keyword = keywords["Biological"], effect = KeywordEffect.NoEffect }
            };
            EditorUtility.SetDirty(incendiary);

            var explosion = GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/UnstableExplosion.asset", w =>
            {
                w.displayName = "Unstable Explosion";
                w.baseDamage = 5;
                w.hasPenetration = false;   // always 5
                w.accuracyBonus = 0;
                w.keywords = new List<Keyword> { keywords["Explosive"] };
            });

            var unstable = GetOrCreate<Keyword>($"{DataDir}/Keywords/Unstable.asset", k =>
            {
                k.displayName = "Unstable";
                k.appliesTo = KeywordTarget.Weapon;
                k.category = KeywordCategory.Trait;
                k.description = "When the holder of this weapon dies, 10% chance they explode: 5 damage to ANY soldier within 5 m.";
                k.triggers = new List<KeywordTrigger>
                {
                    new KeywordTrigger
                    {
                        when = TriggerEvent.Died, action = TriggerAction.Explode, chancePercent = 10,
                        explosionWeapon = explosion, explosionRadius = 5f
                    }
                };
            });
            var library = AssetDatabase.LoadAssetAtPath<KeywordLibrary>($"{Root}/Resources/{KeywordLibrary.ResourcePath}.asset");
            if (library != null && !library.keywords.Contains(unstable))
            {
                library.keywords.Add(unstable);
                EditorUtility.SetDirty(library);
            }

            return GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/Flamethrower.asset", w =>
            {
                w.displayName = "Flamethrower";
                w.baseDamage = 10;
                w.hasPenetration = false;   // "X": always 100% damage
                w.accuracyBonus = 0;
                w.range = 10f;
                w.keywords = new List<Keyword> { incendiary, unstable };
            });
        }

        /// <summary>
        /// Powered Armor Squad (not in the demo armies yet): 1 Sergeant in Powered Armor + 4 Troopers in Powered Armor,
        /// all with an Assault Rifle. No Frag Grenade.
        /// </summary>
        public static UnitTemplate EnsurePoweredArmorSquad(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Units");
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/AssaultRifle.asset");
            var sergeant = EnsurePoweredArmorSergeant(keywords);
            var trooper = EnsurePoweredArmorTrooper(keywords);
            foreach (var s in new[] { sergeant, trooper })
            {
                s.weapons = new List<WeaponTemplate> { rifle };   // armed now they're in a unit
                s.primaryWeapon = rifle;
                EditorUtility.SetDirty(s);
            }
            return GetOrCreate<UnitTemplate>($"{DataDir}/Units/PoweredArmorSquad.asset", u =>
            {
                u.displayName = "Powered Armor Squad";
                u.leader = sergeant;
                u.members = new List<UnitTemplate.Member> { new UnitTemplate.Member { soldier = trooper, count = 4 } };
                u.deploymentSpacing = 2f;
            });
        }

        /// <summary>
        /// Sergeant in Powered Armor: the same stats as the Trooper in Powered Armor (Health 10, Armor Level 2, Accuracy 75%,
        /// Movement 6 m; Biological). Assault Rifle (in the Powered Armor Squad). Model: powered armor with the sergeant's gold markings.
        /// </summary>
        public static SoldierTemplate EnsurePoweredArmorSergeant(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Soldiers");
            var sergeant = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/PoweredArmorSergeant.asset", s =>
            {
                s.displayName = "Sergeant in Powered Armor";
                s.health = 10; s.armorLevel = 2; s.accuracy = 75; s.movementRange = 6f;
                s.keywords = new List<Keyword> { keywords["Biological"] };
                s.abilities = new List<Ability>();
                s.weapons = new List<WeaponTemplate>();
            });
            if (sergeant.prefab == null)
            {
                sergeant.prefab = BuildSoldierPrefab("PoweredArmorSergeant", sergeant, true,
                    GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f)),
                    GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f)),
                    GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f)),
                    GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f)),
                    poweredArmor: true);
                EditorUtility.SetDirty(sergeant);
            }
            return sergeant;
        }

        /// <summary>
        /// Trooper in Powered Armor: Health 10, Armor Level 2, Accuracy 75%, Movement 6 m; Biological. Assault Rifle (in the Powered Armor Squad).
        /// </summary>
        public static SoldierTemplate EnsurePoweredArmorTrooper(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Soldiers");
            var trooper = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/PoweredArmorTrooper.asset", s =>
            {
                s.displayName = "Trooper in Powered Armor";
                s.health = 5; s.armorLevel = 2; s.accuracy = 75; s.movementRange = 6f;
                s.keywords = new List<Keyword> { keywords["Biological"] };
                s.abilities = new List<Ability>();
                s.weapons = new List<WeaponTemplate>();
            });
            if (trooper.prefab == null)
            {
                trooper.prefab = BuildSoldierPrefab("PoweredArmorTrooper", trooper, false,
                    GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f)),
                    GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f)),
                    GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f)),
                    GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f)),
                    poweredArmor: true);
                EditorUtility.SetDirty(trooper);
            }
            return trooper;
        }

        /// <summary>
        /// Sniper Rifle: Base Damage 5, Penetration 1, Accuracy Bonus +50, range 50 m, keyword Deployed (can't be used in a
        /// volley if the soldier holding it has moved this turn). Not carried by anyone yet.
        /// </summary>
        public static WeaponTemplate EnsureSniperRifle(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Weapons");
            return GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/SniperRifle.asset", w =>
            {
                w.displayName = "Sniper Rifle";
                w.baseDamage = 5;
                w.hasPenetration = true;
                w.penetration = 1;
                w.accuracyBonus = 50;
                w.range = 50f;
                w.keywords = new List<Keyword> { keywords["Deployed"] };
            });
        }

        /// <summary>
        /// Sniper: Health 5, Armor Level 0, Accuracy 100%, Movement 15 m; Biological, Solo, Sniper Precision.
        /// A unit of one (Data/Units/Sniper; not in the demo armies). Weapon: Sniper Rifle.
        /// </summary>
        /// <summary>
        /// Medium Tank: a unit of one soldier, the Medium Tank (Health 20, Armor Level 4, Accuracy 75%, Movement 30 m;
        /// Mechanical: can't sprint; Tank; Unstable: 10% chance to explode when destroyed), armed with a 100mm Cannon (Damage 10, Penetration 4, Range 50 m, Shots 1,
        /// Manual Load: fires at most once a turn). Role: Heavy Support.
        /// </summary>
        public static UnitTemplate EnsureMediumTank(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Keywords");
            EnsureFolder(DataDir, "Weapons");
            EnsureFolder(DataDir, "Soldiers");
            EnsureFolder(DataDir, "Units");
            Keyword Kw(string file, string name, KeywordTarget target, string description)
            {
                var k = GetOrCreate<Keyword>($"{DataDir}/Keywords/{file}.asset", x => { x.displayName = name; x.appliesTo = target; });
                k.description = description;
                k.category = KeywordCategory.Trait;
                EditorUtility.SetDirty(k);
                return k;
            }
            var mechanical = Kw("Mechanical", "Mechanical", KeywordTarget.Soldier, "A machine, not a living being. Mechanical soldiers can't sprint.");
            var tankKw = Kw("Tank", "Tank", KeywordTarget.Soldier, "An armored fighting vehicle. A trait for other rules to check.");
            var manualLoad = Kw("ManualLoad", "Manual Load", KeywordTarget.Weapon,
                "This weapon can't be fired more than once a turn, even if the soldier carrying it has a way to shoot more than one volley.");

            var cannon = GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/Cannon100mm.asset", w => w.displayName = "100mm Cannon");
            cannon.displayName = "100mm Cannon";
            cannon.baseDamage = 10;
            cannon.hasPenetration = true;
            cannon.penetration = 4;
            cannon.range = 50f;
            cannon.shots = 1;
            cannon.accuracyBonus = 0;
            cannon.keywords = new List<Keyword> { manualLoad };
            EditorUtility.SetDirty(cannon);

            var tank = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/MediumTank.asset", s => s.displayName = "Medium Tank");
            tank.displayName = "Medium Tank";
            tank.health = 20; tank.armorLevel = 4; tank.accuracy = 75; tank.movementRange = 30f;
            // Unstable: when the tank is destroyed, a 10% chance it explodes (5 damage to everyone within 5 m).
            var unstable = AssetDatabase.LoadAssetAtPath<Keyword>($"{DataDir}/Keywords/Unstable.asset");
            tank.keywords = new List<Keyword> { mechanical, tankKw };
            if (unstable != null) tank.keywords.Add(unstable);
            tank.abilities = new List<Ability>();
            tank.weapons = new List<WeaponTemplate> { cannon };
            tank.primaryWeapon = cannon;
            tank.radius = 1.8f;    // a 3.4 × 5.6 m hull
            tank.height = 2.3f;
            if (tank.prefab == null) tank.prefab = BuildTankPrefab("MediumTank", tank);
            EditorUtility.SetDirty(tank);

            var unit = GetOrCreate<UnitTemplate>($"{DataDir}/Units/MediumTank.asset", u => u.displayName = "Medium Tank");
            unit.displayName = "Medium Tank";
            unit.leader = tank;
            unit.members = new List<UnitTemplate.Member>();
            EditorUtility.SetDirty(unit);
            AssetDatabase.SaveAssets();
            return unit;
        }

        /// <summary>
        /// Rocket Launcher: Damage 5, Penetration 5, Accuracy Bonus +0, Range 50 m, Shots 1, keyword Manual Load (fires at
        /// most once a turn). Not carried by anyone yet.
        /// </summary>
        /// <summary>
        /// Tank Hunter Team (Heavy Support): a Sergeant (Assault Rifle, Pistol), 3 Troopers (Assault Rifle) and a Rocket
        /// Trooper (Assault Rifle, plus the Rocket Launcher used as an active ability: an aimed shot at one enemy soldier).
        /// Infantry Squad stats (Health 5, Armor Level 1, Accuracy 75%, 10 m, Biological); no Frag Grenade. The Assault
        /// Rifle shows under each faction's name for it.
        /// </summary>
        public static UnitTemplate EnsureTankHunterTeam(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Soldiers");
            EnsureFolder(DataDir, "Units");
            EnsureFolder(DataDir, "Abilities");
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/AssaultRifle.asset");
            var pistol = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/Pistol.asset");
            var rocket = EnsureRocketLauncher();

            // The Rocket Launcher as an active ability: an aimed shot at one enemy soldier, once a turn (Manual Load).
            var fire = GetOrCreate<Ability>($"{DataDir}/Abilities/RocketLauncher.asset", a => a.displayName = "Rocket Launcher");
            fire.displayName = "Rocket Launcher";
            fire.description = "Active. An aimed shot at one enemy soldier within 50 m and in sight: Damage 5, Penetration 5, " +
                               "with a hit roll. Firing it is the unit's action. Manual Load: once a turn.";
            fire.kind = AbilityKind.Active;
            fire.targets = AbilityTarget.Soldier;
            fire.affects = AuraAffects.Enemies;
            fire.includeSelf = false;
            fire.range = rocket.range;
            fire.attackWeapon = rocket;
            fire.areaWeapon = null;
            fire.heal = 0;
            fire.grantsKeyword = null;
            fire.usesPerTurn = 1;
            fire.usesPerBattle = 0;
            EditorUtility.SetDirty(fire);

            SoldierTemplate Soldier(string file, string name, List<WeaponTemplate> weapons, List<Ability> abilities)
            {
                var s = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/{file}.asset", x => x.displayName = name);
                s.displayName = name;
                s.health = 5; s.armorLevel = 1; s.accuracy = 75; s.movementRange = 10f;
                s.keywords = new List<Keyword> { keywords["Biological"] };
                s.weapons = weapons;
                s.primaryWeapon = rifle;
                s.abilities = abilities;
                EditorUtility.SetDirty(s);
                return s;
            }
            var sergeant = Soldier("TankHunterSergeant", "Sergeant", new List<WeaponTemplate> { rifle, pistol }, new List<Ability>());
            var trooper = Soldier("TankHunterTrooper", "Trooper", new List<WeaponTemplate> { rifle }, new List<Ability>());
            var rocketeer = Soldier("RocketTrooper", "Rocket Trooper", new List<WeaponTemplate> { rifle }, new List<Ability> { fire });

            sergeant.prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Sergeant.prefab");
            trooper.prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Trooper.prefab");
            if (rocketeer.prefab == null)
                rocketeer.prefab = BuildSoldierPrefab("RocketTrooper", rocketeer, false,
                    GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f)),
                    GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f)),
                    GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f)),
                    GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f)),
                    rocketKit: true);

            var unit = GetOrCreate<UnitTemplate>($"{DataDir}/Units/TankHunterTeam.asset", u => u.displayName = "Tank Hunter Team");
            unit.displayName = "Tank Hunter Team";
            unit.leader = sergeant;
            unit.members = new List<UnitTemplate.Member>
            {
                new UnitTemplate.Member { soldier = trooper, count = 3 },
                new UnitTemplate.Member { soldier = rocketeer, count = 1 },
            };
            unit.deploymentSpacing = 2f;
            EditorUtility.SetDirty(unit);
            AssetDatabase.SaveAssets();
            return unit;
        }

        /// <summary>
        /// Battle Mage (United States of Liberteria only; Specialist): a unit of one. Health 5, Armor 0, Accuracy 75%,
        /// Movement 20 m; Biological, Solo, Officer, Magic; Pistol. Two spells (can't be cast after sprinting):
        ///  - Firestorm: an enemy unit within 10 m; every Biological soldier in it takes 3 damage: it always hits (no roll)
        ///    and ignores armor (no penetration value). No line of sight needed.
        ///  - Fireball: an aimed attack on one enemy soldier: Damage 10, no penetration value, Range 20 m, Accuracy Bonus +0.
        /// </summary>
        public static UnitTemplate EnsureBattleMage(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Soldiers");
            EnsureFolder(DataDir, "Units");
            EnsureFolder(DataDir, "Abilities");
            EnsureFolder(DataDir, "Weapons");
            var pistol = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/Pistol.asset");

            WeaponTemplate SpellWeapon(string file, string name, int damage, float range)
            {
                var w = GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/{file}.asset", x => x.displayName = name);
                w.displayName = name;
                w.baseDamage = damage;
                w.hasPenetration = false;   // "X": ignores armor
                w.penetration = 0;
                w.accuracyBonus = 0;
                w.range = range;
                w.shots = 1;
                w.keywords = new List<Keyword>();
                EditorUtility.SetDirty(w);
                return w;
            }
            var firestormWeapon = SpellWeapon("Firestorm", "Firestorm", 3, 10f);
            var fireballWeapon = SpellWeapon("Fireball", "Fireball", 10, 20f);

            Ability Spell(string file, string name, string description, AbilityTarget targets, float range, WeaponTemplate weapon, Keyword mustHave)
            {
                var a = GetOrCreate<Ability>($"{DataDir}/Abilities/{file}.asset", x => x.displayName = name);
                a.displayName = name;
                a.description = description;
                a.kind = AbilityKind.Active;
                a.targets = targets;
                a.affects = AuraAffects.Enemies;
                a.includeSelf = false;
                a.range = range;
                a.attackWeapon = weapon;
                a.areaWeapon = null;
                a.targetMustHave = mustHave;
                a.heal = 0;
                a.grantsKeyword = null;
                a.usesPerTurn = 1;
                a.usesPerBattle = 0;
                a.keywords = new List<Keyword>();
                Keyword.AddUnique(a.keywords, keywords["Spell"]);
                EditorUtility.SetDirty(a);
                return a;
            }
            var firestorm = Spell("Firestorm", "Firestorm",
                "Spell. Choose an enemy unit within 10 m: every Biological soldier in it takes 3 damage. Always hits and ignores armor. " +
                "No line of sight needed. Can't be cast after sprinting.",
                AbilityTarget.Unit, 10f, firestormWeapon, keywords["Biological"]);
            var fireball = Spell("Fireball", "Fireball",
                "Spell. An aimed attack on one enemy soldier within 20 m and in sight: 10 damage that ignores armor, with a hit roll. " +
                "Can't be cast after sprinting.",
                AbilityTarget.Soldier, 20f, fireballWeapon, null);

            var mage = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/BattleMage.asset", x => x.displayName = "Battle Mage");
            mage.displayName = "Battle Mage";
            mage.health = 5; mage.armorLevel = 0; mage.accuracy = 75; mage.movementRange = 20f;
            mage.keywords = new List<Keyword> { keywords["Biological"], keywords["Solo"], keywords["Officer"], keywords["Magic"] };
            mage.weapons = new List<WeaponTemplate> { pistol };
            mage.primaryWeapon = pistol;
            mage.abilities = new List<Ability> { firestorm, fireball };
            if (mage.prefab == null)
                mage.prefab = BuildSoldierPrefab("BattleMage", mage, false,
                    GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f)),
                    GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f)),
                    GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f)),
                    GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f)),
                    mageKit: true);
            EditorUtility.SetDirty(mage);

            var unit = GetOrCreate<UnitTemplate>($"{DataDir}/Units/BattleMage.asset", u => u.displayName = "Battle Mage");
            unit.displayName = "Battle Mage";
            unit.leader = mage;
            unit.members = new List<UnitTemplate.Member>();
            EditorUtility.SetDirty(unit);
            AssetDatabase.SaveAssets();
            return unit;
        }

        /// <summary>
        /// Stahl Colossus (Draksa Reich only; Heavy Support): a unit of one humanoid war robot twice a person's size. Health 15,
        /// Armor Level 3, Accuracy 75%, Movement 15 m (Mechanical: no sprint); Solo, Mechanical. Machine Gun (primary) and
        /// Iron Claw (Damage 10, Penetration 0, Range 2 m, Accuracy Bonus +25, Melee: usable in close range). Abilities:
        /// Iron Claw (active: an aimed strike with the claw) and Stabilizers (passive: nullifies Deployed).
        /// </summary>
        public static UnitTemplate EnsureStahlColossus(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Keywords");
            EnsureFolder(DataDir, "Weapons");
            EnsureFolder(DataDir, "Soldiers");
            EnsureFolder(DataDir, "Units");
            var melee = GetOrCreate<Keyword>($"{DataDir}/Keywords/Melee.asset", k => { k.displayName = "Melee"; k.appliesTo = KeywordTarget.Weapon; });
            melee.description = "This weapon can be used when the holder is in close range.";
            melee.category = KeywordCategory.Trait;
            EditorUtility.SetDirty(melee);

            var claw = GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/IronClaw.asset", w => w.displayName = "Iron Claw");
            claw.displayName = "Iron Claw";
            claw.baseDamage = 10;
            claw.hasPenetration = true;
            claw.penetration = 0;
            claw.range = 2f;
            claw.accuracyBonus = 25;
            claw.shots = 1;
            claw.keywords = new List<Keyword> { melee };
            EditorUtility.SetDirty(claw);

            var mg = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/MachineGun.asset");
            var mechanical = AssetDatabase.LoadAssetAtPath<Keyword>($"{DataDir}/Keywords/Mechanical.asset");
            var deployed = AssetDatabase.LoadAssetAtPath<Keyword>($"{DataDir}/Keywords/Deployed.asset");
            EnsureFolder(DataDir, "Abilities");

            // The Iron Claw as an active ability: an aimed strike at one enemy within 2 m, instead of the machine gun.
            var strike = GetOrCreate<Ability>($"{DataDir}/Abilities/IronClaw.asset", a => a.displayName = "Iron Claw");
            strike.displayName = "Iron Claw";
            strike.description = "Active. An aimed strike at one enemy soldier within 2 m: Damage 10, Penetration 0, +25% accuracy, with a " +
                                 "hit roll. Using it is the unit's action (no machine gun volley).";
            strike.kind = AbilityKind.Active;
            strike.targets = AbilityTarget.Soldier;
            strike.affects = AuraAffects.Enemies;
            strike.includeSelf = false;
            strike.range = claw.range;
            strike.attackWeapon = claw;
            strike.usesPerTurn = 1;
            strike.usesPerBattle = 0;
            EditorUtility.SetDirty(strike);

            // Stabilizers: the Colossus's weapons ignore Deployed, so its machine gun fires even after it moves.
            var stabilizers = GetOrCreate<Ability>($"{DataDir}/Abilities/Stabilizers.asset", a => a.displayName = "Stabilizers");
            stabilizers.displayName = "Stabilizers";
            stabilizers.description = "Passive. Nullifies the Deployed keyword: the Stahl Colossus's weapons can fire after it moves.";
            stabilizers.kind = AbilityKind.Passive;
            stabilizers.grantsNearby = null;
            stabilizers.nullifiesKeyword = deployed;
            EditorUtility.SetDirty(stabilizers);
            var c = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/StahlColossus.asset", s => s.displayName = "Stahl Colossus");
            c.displayName = "Stahl Colossus";
            c.health = 15; c.armorLevel = 3; c.accuracy = 75; c.movementRange = 15f;
            c.keywords = new List<Keyword> { keywords["Solo"], mechanical };
            c.weapons = new List<WeaponTemplate> { mg, claw };
            c.primaryWeapon = mg;
            c.abilities = new List<Ability> { strike, stabilizers };
            c.height = 3.6f;   // twice a person
            c.radius = 0.7f;
            if (c.prefab == null) c.prefab = BuildColossusPrefab("StahlColossus", c);
            EditorUtility.SetDirty(c);

            var unit = GetOrCreate<UnitTemplate>($"{DataDir}/Units/StahlColossus.asset", u => u.displayName = "Stahl Colossus");
            unit.displayName = "Stahl Colossus";
            unit.leader = c;
            unit.members = new List<UnitTemplate.Member>();
            EditorUtility.SetDirty(unit);
            AssetDatabase.SaveAssets();
            return unit;
        }

        /// <summary>
        /// The Stahl Colossus: a humanoid robot twice a person's height. Torso, shoulders and upper legs take the faction
        /// colour (the primary-colour material); limbs, head and weapons are bare metal. Machine gun in the right hand, a
        /// three-fingered iron claw on the left.
        /// </summary>
        private static GameObject BuildColossusPrefab(string name, SoldierTemplate template)
        {
            var armor = GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f));
            var metal = GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f));
            var steel = GetOrCreateMaterial("Colossus_Steel", new Color(0.45f, 0.46f, 0.5f));
            var eye = GetOrCreateMaterial("Colossus_Eye", new Color(1f, 0.15f, 0.1f));
            float h = template.height;

            var root = new GameObject(name);
            var soldier = root.AddComponent<Soldier>();
            var sso = new SerializedObject(soldier);
            sso.FindProperty("template").objectReferenceValue = template;
            sso.FindProperty("isSergeant").boolValue = false;
            sso.ApplyModifiedPropertiesWithoutUndo();
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = template.radius;
            col.height = h;
            col.center = new Vector3(0f, h / 2f, 0f);

            // Legs.
            foreach (float side in new[] { -1f, 1f })
            {
                Part(root, "Thigh", PrimitiveType.Cube, armor, new Vector3(side * 0.28f, h * 0.36f, 0f), new Vector3(0.36f, h * 0.22f, 0.42f));
                Part(root, "Shin", PrimitiveType.Cube, steel, new Vector3(side * 0.28f, h * 0.13f, 0.02f), new Vector3(0.3f, h * 0.26f, 0.36f));
                Part(root, "Foot", PrimitiveType.Cube, metal, new Vector3(side * 0.28f, 0.08f, 0.12f), new Vector3(0.36f, 0.16f, 0.6f));
            }
            // Torso, shoulders, head with a red eye.
            Part(root, "Torso", PrimitiveType.Cube, armor, new Vector3(0f, h * 0.62f, 0f), new Vector3(1.0f, h * 0.3f, 0.6f));
            Part(root, "Shoulder_L", PrimitiveType.Sphere, armor, new Vector3(-0.62f, h * 0.76f, 0f), Vector3.one * 0.42f);
            Part(root, "Shoulder_R", PrimitiveType.Sphere, armor, new Vector3(0.62f, h * 0.76f, 0f), Vector3.one * 0.42f);
            Part(root, "Head", PrimitiveType.Cube, steel, new Vector3(0f, h * 0.86f, 0.04f), new Vector3(0.42f, 0.38f, 0.44f));
            Part(root, "Eye", PrimitiveType.Cube, eye, new Vector3(0f, h * 0.87f, 0.27f), new Vector3(0.3f, 0.07f, 0.03f));
            // Right arm with the machine gun.
            Part(root, "Arm_R", PrimitiveType.Cube, steel, new Vector3(0.66f, h * 0.6f, 0.15f), new Vector3(0.24f, h * 0.24f, 0.26f));
            Part(root, "Weapon", PrimitiveType.Cube, metal, new Vector3(0.66f, h * 0.48f, 0.7f), new Vector3(0.18f, 0.22f, 1.4f));
            // Left arm with the iron claw.
            Part(root, "Arm_L", PrimitiveType.Cube, steel, new Vector3(-0.66f, h * 0.6f, 0.15f), new Vector3(0.24f, h * 0.24f, 0.26f));
            Part(root, "Claw Hand", PrimitiveType.Cube, metal, new Vector3(-0.66f, h * 0.47f, 0.35f), new Vector3(0.3f, 0.26f, 0.3f));
            foreach (float x in new[] { -0.1f, 0f, 0.1f })
                Part(root, "Claw Finger", PrimitiveType.Cube, metal, new Vector3(-0.66f + x, h * 0.44f, 0.62f), new Vector3(0.06f, 0.08f, 0.4f));

            Eldoria.Terrain.TerrainLayers.SetLayerRecursively(root, Eldoria.Terrain.TerrainLayers.Soldier);
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Field Commander in Powered Armor (Commander, every faction): the Field Commander (Health 10, Accuracy 75%; Biological,
        /// Officer, Solo; Lead from the front; Pistol) with Armor Level 2 and Movement 6 m. Copies the Field Commander's
        /// soldier template, so changes to the Field Commander (keywords, abilities, weapons) carry over.
        /// </summary>
        public static UnitTemplate EnsureFieldCommanderInPoweredArmor()
        {
            EnsureFolder(DataDir, "Soldiers");
            EnsureFolder(DataDir, "Units");
            var original = AssetDatabase.LoadAssetAtPath<SoldierTemplate>($"{DataDir}/Soldiers/FieldCommander.asset");
            var s = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/FieldCommanderPoweredArmor.asset", x => x.displayName = "Field Commander in Powered Armor");
            s.displayName = "Field Commander in Powered Armor";
            s.health = original.health;
            s.accuracy = original.accuracy;
            s.armorLevel = 2;
            s.movementRange = 6f;
            s.keywords = new List<Keyword>(original.keywords);
            s.abilities = new List<Ability>(original.abilities);
            s.weapons = new List<WeaponTemplate>(original.weapons);
            s.primaryWeapon = original.primaryWeapon;
            if (s.prefab == null)
                s.prefab = BuildSoldierPrefab("FieldCommanderPoweredArmor", s, true,
                    GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f)),
                    GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f)),
                    GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f)),
                    GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f)),
                    GetOrCreateMaterial("Commander_Cape", new Color(0.45f, 0.08f, 0.1f)),
                    poweredArmor: true);
            EditorUtility.SetDirty(s);

            var unit = GetOrCreate<UnitTemplate>($"{DataDir}/Units/FieldCommanderPoweredArmor.asset", u => u.displayName = "Field Commander in Powered Armor");
            unit.displayName = "Field Commander in Powered Armor";
            unit.leader = s;
            unit.members = new List<UnitTemplate.Member>();
            EditorUtility.SetDirty(unit);
            AssetDatabase.SaveAssets();
            return unit;
        }

        /// <summary>
        /// Paladin Squad (United Kingdom of Aelfland only; Heavy Support): 1 Sergeant in Powered Armor and 4 Troopers in Powered
        /// Armor, each holding a Broadsword (Damage 5, Penetration 1, Range 2 m, Accuracy Bonus +0, Melee). The soldiers copy
        /// the Sergeant / Trooper in Powered Armor's stats and keywords, so changes to those carry over.
        /// </summary>
        public static UnitTemplate EnsurePaladinSquad()
        {
            EnsureFolder(DataDir, "Weapons");
            EnsureFolder(DataDir, "Soldiers");
            EnsureFolder(DataDir, "Units");
            var melee = AssetDatabase.LoadAssetAtPath<Keyword>($"{DataDir}/Keywords/Melee.asset");
            var sword = GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/Broadsword.asset", w => w.displayName = "Broadsword");
            sword.displayName = "Broadsword";
            sword.baseDamage = 5;
            sword.hasPenetration = true;
            sword.penetration = 1;
            sword.range = 2f;
            sword.accuracyBonus = 0;
            sword.shots = 1;
            sword.keywords = new List<Keyword>();
            if (melee != null) sword.keywords.Add(melee);
            EditorUtility.SetDirty(sword);

            SoldierTemplate Paladin(string file, string from, bool sergeant)
            {
                var original = AssetDatabase.LoadAssetAtPath<SoldierTemplate>($"{DataDir}/Soldiers/{from}.asset");
                var s = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/{file}.asset", x => x.displayName = original.displayName);
                s.displayName = original.displayName;
                s.health = original.health;
                s.armorLevel = original.armorLevel;
                s.accuracy = original.accuracy;
                s.movementRange = original.movementRange;
                s.keywords = new List<Keyword>(original.keywords);
                s.abilities = new List<Ability>();
                s.weapons = new List<WeaponTemplate> { sword };
                s.primaryWeapon = sword;
                if (s.prefab == null)
                    s.prefab = BuildSoldierPrefab(file, s, sergeant,
                        GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f)),
                        GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f)),
                        GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f)),
                        GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f)),
                        poweredArmor: true, swordKit: true);
                EditorUtility.SetDirty(s);
                return s;
            }
            var sergeantT = Paladin("PaladinSergeant", "PoweredArmorSergeant", true);
            var trooperT = Paladin("PaladinTrooper", "PoweredArmorTrooper", false);

            var unit = GetOrCreate<UnitTemplate>($"{DataDir}/Units/PaladinSquad.asset", u => u.displayName = "Paladin Squad");
            unit.displayName = "Paladin Squad";
            unit.leader = sergeantT;
            unit.members = new List<UnitTemplate.Member> { new UnitTemplate.Member { soldier = trooperT, count = 4 } };
            unit.deploymentSpacing = 2f;
            EditorUtility.SetDirty(unit);
            AssetDatabase.SaveAssets();
            return unit;
        }

        public static WeaponTemplate EnsureRocketLauncher()
        {
            EnsureFolder(DataDir, "Weapons");
            var manualLoad = AssetDatabase.LoadAssetAtPath<Keyword>($"{DataDir}/Keywords/ManualLoad.asset");
            var w = GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/RocketLauncher.asset", x => x.displayName = "Rocket Launcher");
            w.displayName = "Rocket Launcher";
            w.baseDamage = 5;
            w.hasPenetration = true;
            w.penetration = 5;
            w.accuracyBonus = 0;
            w.range = 50f;
            w.shots = 1;
            w.keywords = new List<Keyword>();
            if (manualLoad != null) w.keywords.Add(manualLoad);
            EditorUtility.SetDirty(w);
            return w;
        }

        /// <summary>
        /// The Medium Tank's model: tracks, a hull and a turret with a long barrel pointing forward. The hull and turret use
        /// the primary-colour material (they take the faction's colour); tracks and barrel the weapon material.
        /// </summary>
        private static GameObject BuildTankPrefab(string name, SoldierTemplate template)
        {
            var armor = GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f));
            var metal = GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f));
            var root = new GameObject(name);
            var soldier = root.AddComponent<Soldier>();
            var sso = new SerializedObject(soldier);
            sso.FindProperty("template").objectReferenceValue = template;
            sso.FindProperty("isSergeant").boolValue = false;
            sso.ApplyModifiedPropertiesWithoutUndo();

            // Selection collider (the soldier layer), roughly the hull.
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(3.4f, template.height, 5.6f);
            col.center = new Vector3(0f, template.height / 2f, 0f);

            Part(root, "Track_L", PrimitiveType.Cube, metal, new Vector3(-1.35f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 5.6f));
            Part(root, "Track_R", PrimitiveType.Cube, metal, new Vector3(1.35f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 5.6f));
            Part(root, "Hull", PrimitiveType.Cube, armor, new Vector3(0f, 0.95f, 0f), new Vector3(2.2f, 0.9f, 5.2f));
            Part(root, "Turret", PrimitiveType.Cube, armor, new Vector3(0f, 1.8f, -0.3f), new Vector3(1.8f, 0.8f, 2.2f));
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Weapon";
            Object.DestroyImmediate(barrel.GetComponent<Collider>());
            barrel.transform.SetParent(root.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 1.85f, 2.3f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.22f, 1.5f, 0.22f);
            barrel.GetComponent<Renderer>().sharedMaterial = metal;

            Eldoria.Terrain.TerrainLayers.SetLayerRecursively(root, Eldoria.Terrain.TerrainLayers.Soldier);
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static SoldierTemplate EnsureSniper(Dictionary<string, Keyword> keywords)
        {
            EnsureFolder(DataDir, "Soldiers");
            var sniper = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/Sniper.asset", s =>
            {
                s.displayName = "Sniper";
                s.health = 5; s.armorLevel = 0; s.accuracy = 100; s.movementRange = 15f;
                s.keywords = new List<Keyword> { keywords["Biological"], keywords["Sniper Precision"] };
                s.abilities = new List<Ability>();
                s.weapons = new List<WeaponTemplate>();
            });
            Keyword.AddUnique(sniper.keywords, keywords["Solo"]);   // a solo unit
            // Armed with the Sniper Rifle (5 damage, penetration 1, +50, 50 m, Deployed).
            var rifle = EnsureSniperRifle(keywords);
            sniper.weapons = new List<WeaponTemplate> { rifle };
            sniper.primaryWeapon = rifle;
            EditorUtility.SetDirty(sniper);
            GetOrCreate<UnitTemplate>($"{DataDir}/Units/Sniper.asset", u =>
            {
                u.displayName = "Sniper";
                u.leader = sniper;
                u.members = new List<UnitTemplate.Member>();
            });
            if (sniper.prefab == null)
            {
                sniper.prefab = BuildSoldierPrefab("Sniper", sniper, false,
                    GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f)),
                    GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f)),
                    GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f)),
                    GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f)),
                    sniperKit: true);
                EditorUtility.SetDirty(sniper);
            }
            return sniper;
        }

        /// <summary>Weapon ranges (meters): Assault Rifle 20, Pistol 10, SMG 10, Machine Gun 20. Applied to the existing weapon assets.</summary>
        public static readonly (string file, float range)[] WeaponRanges =
            { ("AssaultRifle", 20f), ("Pistol", 10f), ("SMG", 10f), ("MachineGun", 20f), ("SniperRifle", 50f), ("RocketLauncher", 50f) };

        /// <summary>Shots per volley for every weapon (each shot is a full pass through the shooting steps).</summary>
        public static readonly (string file, int shots)[] WeaponShots =
        {
            ("AssaultRifle", 1), ("Pistol", 1), ("SMG", 1), ("MachineGun", 5), ("Flamethrower", 3), ("SniperRifle", 1),
            ("FragGrenade", 1), ("UnstableExplosion", 1), ("Cannon100mm", 1), ("RocketLauncher", 1), ("IronClaw", 1), ("Broadsword", 1),
        };

        private static void ApplyWeaponShots()
        {
            foreach (var (file, shots) in WeaponShots)
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/{file}.asset");
                if (w == null) continue;
                w.shots = shots;
                EditorUtility.SetDirty(w);
            }
        }

        private static void ApplyWeaponRanges()
        {
            foreach (var (file, range) in WeaponRanges)
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/{file}.asset");
                if (w == null) continue;
                w.range = range;
                EditorUtility.SetDirty(w);
            }
        }

        private static void GiveAbility(SoldierTemplate soldier, Ability ability)
        {
            if (soldier.abilities == null) soldier.abilities = new List<Ability>();
            if (!soldier.abilities.Contains(ability)) soldier.abilities.Add(ability);
        }

        /// <summary>Divine Healing and Divine Sanctuary are Spells (can't be cast after the unit sprinted).</summary>
        private static void TagAsSpell(Ability ability, Dictionary<string, Keyword> keywords)
        {
            Keyword.AddUnique(ability.keywords, keywords["Spell"]);
            // The Battle Priest's spells ring out with a hallelujah when cast.
            ability.castSound = AssetDatabase.LoadAssetAtPath<AudioClip>(PriestSpellSoundPath);
            if (ability.castSound == null) Debug.LogWarning($"[Eldoria] Missing priest spell sound at {PriestSpellSoundPath}.");
        }

        private const string PriestSpellSoundPath = Root + "/Resources/Audio/Hallelujah.mp3";

        /// <summary>
        /// Updates the keyword and ability assets in place without rebuilding prefabs or touching the scene
        /// (batchmode: -executeMethod Eldoria.EditorTools.SquadBuilder.UpdateAbilityData).
        /// </summary>
        /// <summary>Each unit's role. Any unit not listed is a Specialist.</summary>
        public static readonly Dictionary<string, UnitRole> UnitRoleByFile = new Dictionary<string, UnitRole>
        {
            ["InfantrySquad"] = UnitRole.Infantry,
            ["FieldCommander"] = UnitRole.Commander,
            ["FieldCommanderPoweredArmor"] = UnitRole.Commander,
            ["BattlefieldPriest"] = UnitRole.Specialist,
            ["MachineGunTeam"] = UnitRole.HeavySupport,
            ["MediumTank"] = UnitRole.HeavySupport,
            ["TankHunterTeam"] = UnitRole.HeavySupport,
            ["StahlColossus"] = UnitRole.HeavySupport,
            ["PaladinSquad"] = UnitRole.HeavySupport,
        };

        /// <summary>How each unit plays and what it does in an army, shown in the army builder (by file name).</summary>
        public static readonly Dictionary<string, string> UnitDescriptions = new Dictionary<string, string>
        {
            ["InfantrySquad"] =
                "The backbone of an army: ten riflemen led by a Sergeant. Their Assault Rifles reach 20 m, and with ten soldiers the squad can soak up casualties and still hold ground or push forward. Once per battle the squad can lob a Frag Grenade over walls to hit enemies sheltering in cover. Infantry can be taken more than once, so most armies field several squads to form their battle line.",
            ["FieldCommander"] =
                "A lone officer who makes everyone nearby shoot straighter. Lead from the front gives friendly soldiers within 15 m +15% accuracy (the Commander doesn't get it), so keep the Commander just behind your main firing line. With 10 health but only a pistol, the Commander isn't a fighter, and as a Solo unit is a prime target for enemy snipers. A Commander: every army needs at least one.",
            ["BattlefieldPriest"] =
                "A fragile support caster (5 health, no armor, 50% accuracy) with two spells. Divine Healing restores 5 health to a friendly soldier within 25 m; Divine Sanctuary gives a whole friendly unit +1 Armor Level until your next turn, blunting the fire on a squad that's about to be shot. Spells can't be cast after sprinting, so move the Battle Priest with care, and keep it behind the squads it supports. A Specialist: one per army.",
            ["MachineGunTeam"] =
                "A heavy fire base. The Machine Gunner fires 5 shots a volley (5 damage, penetration 1, 20 m each) but can't fire after moving, so set the team up covering a firing lane and let it hold there. Three SMG troopers add close-range firepower out to 10 m and protect the gun; the Sergeant carries only a pistol. With five soldiers and no grenade, it's best covering ground rather than leading an assault. Heavy Support can be taken more than once.",
            ["PoweredArmorSquad"] =
                "Five soldiers in powered armor with Assault Rifles. Armor Level 2 halves rifle damage and shrugs off pistols and grenades entirely, so the squad can walk through fire that would shred an Infantry Squad; the Sergeant has 10 health, the Troopers 5. The trade-off is speed: only 6 m a move (12 m sprinting), so start advancing early or put them where the fight will come to them. A Specialist: one per army.",
            ["FlamethrowerTeam"] =
                "Two troopers with Flamethrowers for clearing buildings and cover at close quarters. Each Flamethrower fires 3 shots a volley, each doing 10 damage that ignores armor, but only against Biological targets and only out to 10 m, so the team has to get close. When a trooper dies there's a 10% chance their tank explodes, hurting everyone within 5 m: keep them away from your own squads. In close range they fall back on pistols. A Specialist: one per army.",
            ["MediumTank"] =
                "A lone armored vehicle and the hardest target on the field: 20 health and Armor Level 4 shrug off rifles, machine guns, grenades and flamethrowers alike. Its 100mm Cannon (10 damage, penetration 4, 50 m) is the only weapon that hurts heavy armor, but it's Manual Load: one shot a turn. It moves a long way (30 m) but, being Mechanical, can't sprint. When destroyed it has a 10% chance to explode (Unstable), so keep your own soldiers clear of it. Heavy Support, so you can field more than one.",
            ["TankHunterTeam"] =
                "Five riflemen built around one Rocket Trooper. Their rifles do the everyday work, but the Rocket Launcher is why you take them: an aimed shot at a single enemy soldier within 50 m, Damage 5 with Penetration 5, enough to beat a Medium Tank's armor for 7 damage a hit. Firing it is the team's action for the turn (no rifle volley), and it's Manual Load, so once a turn. Keep the Rocket Trooper alive and in sight of the enemy's armor. Heavy Support, so you can field more than one.",
            ["BattleMage"] =
                "A lone spellcaster of the United States of Liberteria. Firestorm engulfs an enemy unit within 10 m: every Biological soldier in it takes 3 damage, always hitting and ignoring armor, which can gut a whole squad. Fireball is an aimed shot at one soldier within 20 m and in sight: 10 damage that ignores armor, enough to kill most soldiers outright. Fast (20 m) but fragile (5 health, no armor), and both spells can't be cast after sprinting. A Specialist, and a Solo unit: one per army.",
            ["StahlColossus"] =
                "The Draksa Reich's war machine: a humanoid robot twice a man's height, with 15 health and Armor Level 3 that rifles, pistols and grenades can't dent. Its Stabilizers let its Machine Gun (5 shots) fire even after moving, and when an enemy is within 2 m it can strike with its Iron Claw instead (Damage 10, +25% accuracy). Mechanical: it moves 15 m but can't sprint. Rockets and tank cannons are its bane. Heavy Support, so you can field more than one.",
            ["FieldCommanderPoweredArmor"] =
                "The Field Commander sealed in powered armor. Lead from the front still gives friendly soldiers within 15 m +15% accuracy, and Armor Level 2 halves rifle fire and shrugs off pistols and grenades, so the Commander can stand much closer to the fight. The cost is speed: 6 m a move (12 m sprinting), so it has to plan ahead to keep the army inside its aura. A Commander: it fills your required Commander slot.",
            ["PaladinSquad"] =
                "The United Kingdom of Aelfland's armored knights: a Sergeant and four Troopers in powered armor, armed only with Broadswords. A sword strike does 5 damage at penetration 1, but only within 2 m, so the Paladins must close in before they can fight. Armor Level 2 halves rifle fire on the way, but at 6 m a move (12 m sprinting) they need cover to get there. Once in close range they keep fighting where riflemen can't. Heavy Support, so you can field more than one.",
            ["Sniper"] =
                "A lone marksman who picks off key enemies from up to 50 m. At 150% accuracy every shot hits, with a 50% chance to crit, and you choose exactly which enemy soldier to shoot: ideal for removing sergeants and officers. The Sniper Rifle can't fire after moving, and with 5 health and no armor the Sniper must stay out of sight; moving 15 m, they can reposition quickly between shots. A Specialist and a Solo unit: one per army.",
        };

        /// <summary>Sets the description on every unit template in Data/Units that has one written.</summary>
        public static void ApplyUnitDescriptions()
        {
            foreach (var pair in UnitDescriptions)
            {
                var unit = AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{DataDir}/Units/{pair.Key}.asset");
                if (unit == null) continue;
                unit.description = pair.Value;
                EditorUtility.SetDirty(unit);
            }
        }

        /// <summary>Sets the role on every unit template in Data/Units.</summary>
        public static void ApplyUnitRoles()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UnitTemplate", new[] { $"{DataDir}/Units" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var unit = AssetDatabase.LoadAssetAtPath<UnitTemplate>(path);
                if (unit == null) continue;
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                unit.role = UnitRoleByFile.TryGetValue(file, out var role) ? role : UnitRole.Specialist;
                EditorUtility.SetDirty(unit);
            }
        }

        /// <summary>The factions (Data/Factions), in the order the army builder shows them: file name → display name.</summary>
        public static readonly (string file, string name, Color color)[] FactionNames =
        {
            ("UnitedStatesOfLiberteria", "United States of Liberteria", new Color(0.16f, 0.30f, 0.15f)),   // dark green
            ("UnitedKingdomOfAelfland", "United Kingdom of Aelfland", new Color(0.50f, 0.74f, 0.36f)), // light green
            ("DraksaReich", "The Draksa Reich", new Color(0.46f, 0.47f, 0.49f)),                       // gray
        };

        /// <summary>
        /// Changes to existing data: Machine Gun Damage 5 (one weapon, so every holder: the Machine Gun Team's gunner and the
        /// Stahl Colossus); Trooper in Powered Armor Health 5; the Battlefield Priest renamed the Battle Priest (its role,
        /// Specialist, is set with the other roles).
        /// </summary>
        private static void ApplyBalanceChanges()
        {
            var mg = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/MachineGun.asset");
            if (mg != null) { mg.baseDamage = 5; EditorUtility.SetDirty(mg); }
            var paTrooper = AssetDatabase.LoadAssetAtPath<SoldierTemplate>($"{DataDir}/Soldiers/PoweredArmorTrooper.asset");
            if (paTrooper != null) { paTrooper.health = 5; EditorUtility.SetDirty(paTrooper); }
            var priest = AssetDatabase.LoadAssetAtPath<SoldierTemplate>($"{DataDir}/Soldiers/BattlefieldPriest.asset");
            if (priest != null) { priest.displayName = PriestName; EditorUtility.SetDirty(priest); }
            var priestUnit = AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{DataDir}/Units/BattlefieldPriest.asset");
            if (priestUnit != null) { priestUnit.displayName = PriestName; EditorUtility.SetDirty(priestUnit); }
        }

        /// <summary>Units only one faction can take (unit file name → faction file name). Every other unit is open to all.</summary>
        public static readonly Dictionary<string, string> FactionOnlyUnits = new Dictionary<string, string>
        {
            ["BattleMage"] = "UnitedStatesOfLiberteria",
            ["StahlColossus"] = "DraksaReich",
            ["PaladinSquad"] = "UnitedKingdomOfAelfland",
        };

        /// <summary>
        /// Creates the factions. Each faction can take every unit except those belonging only to another faction
        /// (<see cref="FactionOnlyUnits"/>).
        /// </summary>
        public static List<Faction> EnsureFactions(List<UnitTemplate> allUnits)
        {
            EnsureFolder(DataDir, "Factions");
            // The United States of Columbia was renamed the United States of Liberteria: keep its asset, under the new name.
            if (AssetDatabase.LoadAssetAtPath<Faction>($"{DataDir}/Factions/UnitedStatesOfColumbia.asset") != null
                && AssetDatabase.LoadAssetAtPath<Faction>($"{DataDir}/Factions/UnitedStatesOfLiberteria.asset") == null)
                AssetDatabase.RenameAsset($"{DataDir}/Factions/UnitedStatesOfColumbia.asset", "UnitedStatesOfLiberteria");

            var factions = new List<Faction>();
            foreach (var (file, name, color) in FactionNames)
            {
                var faction = GetOrCreate<Faction>($"{DataDir}/Factions/{file}.asset", f => f.displayName = name);
                faction.displayName = name;
                faction.color = color;
                faction.units = allUnits.Where(u =>
                    !FactionOnlyUnits.TryGetValue(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(u)), out var only)
                    || only == file).ToList();
                ApplyFactionWeapons(faction, file);
                EditorUtility.SetDirty(faction);
                factions.Add(faction);
            }
            return factions;
        }

        /// <summary>
        /// Each faction's names for weapons (aliases of the same stat block) and its changes to their stats (added to the
        /// weapon's own). By faction file name.
        /// </summary>
        private static void ApplyFactionWeapons(Faction faction, string file)
        {
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/AssaultRifle.asset");
            faction.weaponNames = new List<WeaponName>();
            faction.weaponAdjustments = new List<WeaponAdjustment>();
            switch (file)
            {
                case "UnitedStatesOfLiberteria":
                    // The Assault Rifle is a semi-automatic rifle here: Shots 2 (the rifle's 1, +1).
                    faction.weaponNames.Add(new WeaponName { weapon = rifle, name = "Semi-auto rifle" });
                    faction.weaponAdjustments.Add(new WeaponAdjustment { weapon = rifle, shots = 1 });
                    break;
                case "UnitedKingdomOfAelfland":
                case "DraksaReich":
                    faction.weaponNames.Add(new WeaponName { weapon = rifle, name = "Bolt-Action Rifle" });
                    break;
            }
        }

        /// <summary>
        /// The unit catalog for army lists (Resources/UnitCatalog): every unit in Data/Units, Commanders first, then
        /// Infantry, Heavy Support and Specialists; the demo army as the army builder's starting list; and the
        /// materials for deploying units at runtime.
        /// </summary>
        public static UnitCatalog EnsureUnitCatalog()
        {
            EnsureFolder(Root, "Resources");
            var catalog = GetOrCreate<UnitCatalog>($"{Root}/Resources/{UnitCatalog.ResourcePath}.asset", c => { });
            catalog.units = AssetDatabase.FindAssets("t:UnitTemplate", new[] { $"{DataDir}/Units" })
                .Select(g => AssetDatabase.LoadAssetAtPath<UnitTemplate>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(u => u != null)
                .OrderBy(u => u.role.ListOrder()).ThenBy(u => u.displayName).ToList();

            UnitTemplate Unit(string file) => AssetDatabase.LoadAssetAtPath<UnitTemplate>($"{DataDir}/Units/{file}.asset");
            catalog.defaultArmy = new List<UnitTemplate>
            {
                Unit("InfantrySquad"), Unit("FieldCommander"), Unit("InfantrySquad"), Unit("BattlefieldPriest"), Unit("InfantrySquad"),
            }.Where(u => u != null).ToList();

            catalog.factions = EnsureFactions(catalog.units);

            catalog.primaryColorMaterial = GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f));
            catalog.leaderMarker = GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f));
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        public static void UpdateAbilityData()
        {
            var keywords = EnsureKeywords();
            var frag = EnsureFragGrenade(keywords);
            EnsureSmg();
            EnsureMachineGun(keywords);
            ApplyWeaponRanges();
            EnsureMachineGunTeam(keywords);
            EnsureFlamethrower(keywords);
            EnsureFlamethrowerTeam(keywords);
            EnsurePoweredArmorTrooper(keywords);
            EnsurePoweredArmorSergeant(keywords);
            EnsurePoweredArmorSquad(keywords);
            EnsureSniperRifle(keywords);
            EnsureSniper(keywords);
            EnsureMediumTank(keywords);
            EnsureRocketLauncher();
            EnsureTankHunterTeam(keywords);
            EnsureBattleMage(keywords);
            EnsureStahlColossus(keywords);
            EnsureFieldCommanderInPoweredArmor();
            ApplyBalanceChanges();
            EnsurePaladinSquad();   // after the balance changes: it copies the powered-armor soldiers' stats
            ApplyWeaponShots();
            ApplyUnitRoles();
            ApplyUnitDescriptions();
            EnsureUnitCatalog();
            var pistolWeapon = AssetDatabase.LoadAssetAtPath<WeaponTemplate>($"{DataDir}/Weapons/Pistol.asset");
            if (pistolWeapon != null)
            {
                Keyword.AddUnique(pistolWeapon.keywords, keywords["Pistol"]);
                EditorUtility.SetDirty(pistolWeapon);
            }
            foreach (var file in new[] { "Trooper", "Sergeant" })
            {
                var soldier = AssetDatabase.LoadAssetAtPath<SoldierTemplate>($"{DataDir}/Soldiers/{file}.asset");
                if (soldier == null) continue;
                GiveAbility(soldier, frag);
                EditorUtility.SetDirty(soldier);
            }
            foreach (var file in new[] { "DivineHealing", "DivineSanctuary" })
            {
                var ability = AssetDatabase.LoadAssetAtPath<Ability>($"{DataDir}/Abilities/{file}.asset");
                if (ability == null) continue;
                TagAsSpell(ability, keywords);
                EditorUtility.SetDirty(ability);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Eldoria] Ability data updated.");
        }

        /// <summary>Spawns the player's Battle Priest just behind the Infantry Squad's right side.</summary>
        public static Unit SpawnBattlefieldPriest()
        {
            var template = CreateBattlefieldPriest();
            foreach (var old in Object.FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (old.DisplayName == PriestName && old.Side == Side.Player) Object.DestroyImmediate(old.gameObject);

            var unit = UnitFactory.Spawn(template, Side.Player, new Vector3(3f, 0f, -20.5f), Quaternion.identity,
                unitName: PriestName,
                instantiate: (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent));
            EditorUtility.SetDirty(unit);
            Debug.Log($"[Eldoria] Spawned '{unit.DisplayName}' ({unit.Soldiers.Count} soldier).");
            return unit;
        }

        /// <summary>Spawns the player's Field Commander just behind the Infantry Squad.</summary>
        public static Unit SpawnFieldCommander()
        {
            var template = CreateFieldCommander();
            foreach (var old in Object.FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (old.DisplayName == CommanderName && old.Side == Side.Player) Object.DestroyImmediate(old.gameObject);

            var unit = UnitFactory.Spawn(template, Side.Player, new Vector3(0f, 0f, -20.5f), Quaternion.identity,
                unitName: CommanderName,
                instantiate: (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent));
            EditorUtility.SetDirty(unit);
            Debug.Log($"[Eldoria] Spawned '{unit.DisplayName}' ({unit.Soldiers.Count} soldier).");
            return unit;
        }

        /// <summary>
        /// Spawns a unit from an existing template asset at any spot in the open scene (keeping prefab links).
        /// AI units are recoloured red; units of more than one soldier get the floating leader marker.
        /// Soldiers are placed on the ground below them.
        /// </summary>
        public static Unit SpawnUnit(UnitTemplate template, Side side, Vector3 position, Quaternion facing, string unitName)
        {
            var unit = UnitFactory.Spawn(template, side, position, facing, unitName: unitName,
                instantiate: (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent));

            if (side == Side.AI)
            {
                var blue = GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f));
                var red = GetOrCreateMaterial("Enemy_Armor", new Color(0.62f, 0.16f, 0.14f));
                foreach (var r in unit.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterial == blue) r.sharedMaterial = red;
            }

            foreach (var soldier in unit.Soldiers)
            {
                var p = soldier.transform.position;
                if (Physics.Raycast(new Vector3(p.x, 50f, p.z), Vector3.down, out var hit, 100f,
                        Eldoria.Terrain.TerrainLayers.StructureMask, QueryTriggerInteraction.Ignore))
                    soldier.transform.position = new Vector3(p.x, hit.point.y, p.z);
            }

            if (unit.Soldiers.Count > 1)
            {
                var marker = unit.gameObject.AddComponent<LeaderMarker>();
                var markerSo = new SerializedObject(marker);
                markerSo.FindProperty("material").objectReferenceValue = GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f));
                markerSo.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(unit);
            return unit;
        }

        /// <summary>Spawns an enemy (AI side) infantry squad, in red, facing the player's squad.</summary>
        public static Unit SpawnEnemySquad()
        {
            var infantry = CreateTemplates();
            foreach (var old in Object.FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (old.DisplayName == EnemyName) Object.DestroyImmediate(old.gameObject);

            // North of the container, facing south toward the player.
            var unit = UnitFactory.Spawn(infantry, Side.AI, new Vector3(-4f, 0f, 6.5f), Quaternion.Euler(0f, 180f, 0f),
                unitName: EnemyName,
                instantiate: (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent));

            var blue = GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f));
            var red = GetOrCreateMaterial("Enemy_Armor", new Color(0.62f, 0.16f, 0.14f));
            foreach (var r in unit.GetComponentsInChildren<Renderer>(true))
                if (r.sharedMaterial == blue) r.sharedMaterial = red;

            // Stand on the ground (the hill's foot reaches this far).
            foreach (var soldier in unit.Soldiers)
            {
                var p = soldier.transform.position;
                soldier.transform.position = new Vector3(p.x, TerrainBuilder.GroundHeight(p.x, p.z), p.z);
            }

            var marker = unit.gameObject.AddComponent<LeaderMarker>();
            var markerSo = new SerializedObject(marker);
            markerSo.FindProperty("material").objectReferenceValue = GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f));
            markerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(unit);
            Debug.Log($"[Eldoria] Spawned enemy unit '{unit.DisplayName}' ({unit.Soldiers.Count} soldiers).");
            return unit;
        }

        /// <summary>Creates (or loads) every keyword asset and the keyword library. Returns them by name.</summary>
        public static Dictionary<string, Keyword> EnsureKeywords()
        {
            EnsureFolder(Root, "Data");
            EnsureFolder(DataDir, "Keywords");

            Keyword Kw(string file, string name, KeywordTarget target, string description) =>
                GetOrCreate<Keyword>($"{DataDir}/Keywords/{file}.asset", k =>
                {
                    k.displayName = name;
                    k.appliesTo = target;
                    k.description = description;
                });
            Kw("Explosive", "Explosive", KeywordTarget.Weapon, "An explosive weapon. A trait for other rules to check.");
            Kw("Incendiary", "Incendiary", KeywordTarget.Weapon, "Only damages targets with the Biological keyword.");
            Kw("Robotic", "Robotic", KeywordTarget.Soldier, "A machine rather than a person. A trait for other rules to check.");
            Kw("Biological", "Biological", KeywordTarget.Soldier, "A living being. Every living soldier has this keyword.");
            GetOrCreate<Keyword>($"{DataDir}/Keywords/Toxic.asset", k =>
            {
                k.displayName = "Toxic";
                k.appliesTo = KeywordTarget.Weapon;
                k.description = "Only harms living beings: no effect on a target that isn't Biological.";
                k.interactions = new List<KeywordInteraction>
                {
                    new KeywordInteraction
                    {
                        party = KeywordParty.Target, condition = KeywordCondition.Lacks, keyword = AssetDatabase.LoadAssetAtPath<Keyword>($"{DataDir}/Keywords/Biological.asset"),
                        effect = KeywordEffect.NoEffect
                    }
                };
            });

            Kw("Moved", "Moved", KeywordTarget.Soldier, "Gained by a soldier who moves; removed at the start of the next turn.");

            // Cover: a Half-cover object grants In half-cover to soldiers within 5 m; attacks on them take −25%
            // accuracy if that object is between attacker and target.
            var inHalfCover = GetOrCreate<Keyword>($"{DataDir}/Keywords/InHalfCover.asset", k =>
            {
                k.displayName = "In half-cover";
                k.appliesTo = KeywordTarget.Soldier;
                k.description = "Within 5 m of half-cover. Attacks against this soldier take −25% accuracy if the cover is between them.";
                k.interactions = new List<KeywordInteraction>
                {
                    new KeywordInteraction
                    {
                        role = KeywordRole.WhenDefending, condition = KeywordCondition.Always,
                        effect = KeywordEffect.AccuracyPenalty, amount = 25, onlyIfSourceIsBetween = true
                    }
                };
            });
            // Full cover: a soldier fully concealed behind the cover can't be targeted by a shooting attack.
            var inFullCover = GetOrCreate<Keyword>($"{DataDir}/Keywords/InFullCover.asset", k =>
            {
                k.displayName = "In full cover";
                k.appliesTo = KeywordTarget.Soldier;
                k.description = "Fully concealed behind cover: can't be targeted by a shooting attack while the cover is between them.";
                k.interactions = new List<KeywordInteraction>
                {
                    new KeywordInteraction
                    {
                        role = KeywordRole.WhenDefending, condition = KeywordCondition.Always,
                        effect = KeywordEffect.CannotBeTargeted, onlyIfSourceIsBetween = true
                    }
                };
            });
            var fullCover = GetOrCreate<Keyword>($"{DataDir}/Keywords/FullCover.asset", k =>
            {
                k.displayName = "Full-cover";
                k.appliesTo = KeywordTarget.TerrainObject;
                k.grantsNearby = inFullCover;
            });
            // Full cover works at any distance: it only has to be between the shooter and the target.
            fullCover.description = "A soldier is In full cover against any attacker this object is between, however far either is from it.";
            fullCover.grantsWhenBetween = true;
            EditorUtility.SetDirty(fullCover);
            inFullCover.description = "Fully concealed behind cover: can't be targeted by a shooting attack. Applies whenever a Full-cover object is between the attacker and the soldier.";
            EditorUtility.SetDirty(inFullCover);
            GetOrCreate<Keyword>($"{DataDir}/Keywords/IndirectFire.asset", k =>
            {
                k.displayName = "Indirect fire";
                k.appliesTo = KeywordTarget.Weapon;
                k.description = "Fires over obstacles: ignores line of sight (including cover) and all accuracy modifiers.";
                k.interactions = new List<KeywordInteraction>
                {
                    new KeywordInteraction { condition = KeywordCondition.Always, effect = KeywordEffect.IgnoresLineOfSight },
                    new KeywordInteraction { condition = KeywordCondition.Always, effect = KeywordEffect.IgnoresAccuracyModifiers },
                };
            });
            GetOrCreate<Keyword>($"{DataDir}/Keywords/HalfCover.asset", k =>
            {
                k.displayName = "Half-cover";
                k.appliesTo = KeywordTarget.TerrainObject;
                k.description = "Soldiers within 5 m gain In half-cover.";
                k.grantsNearby = inHalfCover;
                k.grantRadius = 5f;
            });

            Kw("Officer", "Officer", KeywordTarget.Soldier, "A commissioned officer. (Effect not designed yet.)");
            Kw("Solo", "Solo", KeywordTarget.Soldier, "Fights as a unit of one. No effect on its own; some weapons and abilities check for it.");
            Kw("Magic", "Magic", KeywordTarget.Soldier, "Wields magic. No effect on its own; some weapons and abilities check for it.");
            Kw("Spell", "Spell", KeywordTarget.Ability, "A magical ability. Can't be cast by a soldier whose unit sprinted this turn.");
            Kw("Pistol", "Pistol", KeywordTarget.Weapon, "A sidearm: the only kind of weapon a soldier in close range can use.");
            Kw("SniperPrecision", "Sniper Precision", KeywordTarget.Soldier,
                "When this soldier shoots, the player picks the specific enemy soldier they target instead of the closest one.");
            Kw("Deployed", "Deployed", KeywordTarget.Weapon, "This weapon can't be used in a volley if the soldier holding it has moved this turn.");
            Kw("InCloseRange", "In close range", KeywordTarget.Soldier,
                "A Biological enemy is less than 2 m away: can only use weapons with the Pistol keyword (spells and other abilities still work).");
            GetOrCreate<Keyword>($"{DataDir}/Keywords/DivineSanctuary.asset", k =>
            {
                k.displayName = "Divine Sanctuary";
                k.appliesTo = KeywordTarget.Soldier;
                k.description = "Blessed by a Battle Priest: +1 Armor Level.";
                k.interactions = new List<KeywordInteraction>
                {
                    new KeywordInteraction
                    {
                        role = KeywordRole.WhenDefending, condition = KeywordCondition.Always,
                        effect = KeywordEffect.ArmorBonus, amount = 1
                    }
                };
            }, k =>
            {
                // Blessed soldiers glitter gold while the spell lasts.
                k.sparkles = true;
                k.sparkleColor = new Color(1f, 0.82f, 0.25f, 1f);
            });
            GetOrCreate<Keyword>($"{DataDir}/Keywords/LedFromTheFront.asset", k =>
            {
                k.displayName = "Led from the front";
                k.appliesTo = KeywordTarget.Soldier;
                k.description = "Within 15 m of a friendly Field Commander (not the commander themself): +15% accuracy.";
                k.interactions = new List<KeywordInteraction>
                {
                    new KeywordInteraction
                    {
                        role = KeywordRole.WhenAttacking, condition = KeywordCondition.Always,
                        effect = KeywordEffect.AccuracyBonus, amount = 15
                    }
                };
            });

            // Categories: Traits are what a soldier is; Statuses come and go during play.
            var categories = new Dictionary<string, KeywordCategory>
            {
                ["Biological"] = KeywordCategory.Trait, ["Robotic"] = KeywordCategory.Trait,
                ["Officer"] = KeywordCategory.Trait, ["Solo"] = KeywordCategory.Trait, ["Magic"] = KeywordCategory.Trait,
                ["Spell"] = KeywordCategory.Trait, ["Pistol"] = KeywordCategory.Trait, ["Deployed"] = KeywordCategory.Trait,
                ["Mechanical"] = KeywordCategory.Trait, ["Tank"] = KeywordCategory.Trait, ["Manual Load"] = KeywordCategory.Trait, ["Melee"] = KeywordCategory.Trait, ["Sniper Precision"] = KeywordCategory.Trait, ["In close range"] = KeywordCategory.Status,
                ["Divine Sanctuary"] = KeywordCategory.Status,
                ["Moved"] = KeywordCategory.Status, ["In half-cover"] = KeywordCategory.Status,
                ["In full cover"] = KeywordCategory.Status, ["Led from the front"] = KeywordCategory.Status,
            };
            foreach (var guid in AssetDatabase.FindAssets("t:Keyword", new[] { $"{DataDir}/Keywords" }))
            {
                var k = AssetDatabase.LoadAssetAtPath<Keyword>(AssetDatabase.GUIDToAssetPath(guid));
                if (k != null && k.category == KeywordCategory.None && categories.TryGetValue(k.displayName, out var cat))
                {
                    k.category = cat;
                    EditorUtility.SetDirty(k);
                }
            }

            // --- Keyword library (lets rules find keywords like "Moved" by name at runtime) ---
            EnsureFolder(Root, "Resources");
            var library = GetOrCreate<KeywordLibrary>($"{Root}/Resources/{KeywordLibrary.ResourcePath}.asset", l => { });
            library.keywords = AssetDatabase.FindAssets("t:Keyword", new[] { $"{DataDir}/Keywords" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<Keyword>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(k => k != null).OrderBy(k => k.displayName).ToList();
            EditorUtility.SetDirty(library);

            return library.keywords.ToDictionary(k => k.displayName);
        }

        /// <summary>Creates (or loads) the weapon, soldier and unit templates for the test infantry squad.</summary>
        public static UnitTemplate CreateTemplates()
        {
            EnsureFolder(Root, "Data");
            EnsureFolder(DataDir, "Weapons");
            EnsureFolder(DataDir, "Keywords");
            EnsureFolder(DataDir, "Soldiers");
            EnsureFolder(DataDir, "Units");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root + "/Prefabs", "Units");
            EnsureFolder(Root, "Materials");

            var keywords = EnsureKeywords();
            var biological = keywords["Biological"];
            var frag = EnsureFragGrenade(keywords);

            // --- Weapons (placeholder stats) ---
            var rifle = GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/AssaultRifle.asset", w =>
            {
                w.displayName = "Assault Rifle";
                w.baseDamage = 4; w.penetration = 1; w.accuracyBonus = 0; w.range = 20f;
            });
            var pistol = GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/Pistol.asset", w =>
            {
                w.displayName = "Pistol";
                w.baseDamage = 2; w.penetration = 0; w.accuracyBonus = 0; w.range = 10f;
            }, w => Keyword.AddUnique(w.keywords, keywords["Pistol"]));   // usable in close range
            EnsureSmg();

            // --- Soldiers (placeholder stats); re-link weapons if any are missing ---
            var trooper = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/Trooper.asset", s =>
            {
                s.displayName = "Trooper";
                s.health = 5; s.armorLevel = 1; s.accuracy = 75; s.movementRange = 10f;
            }, s =>
            {
                if (s.weapons.Count == 0 || s.weapons.Contains(null)) s.weapons = new List<WeaponTemplate> { rifle };
                if (s.primaryWeapon == null) s.primaryWeapon = rifle;
                if (!s.HasKeyword("Biological")) s.keywords.Add(biological);
                GiveAbility(s, frag);
            });
            var sergeant = GetOrCreate<SoldierTemplate>($"{DataDir}/Soldiers/Sergeant.asset", s =>
            {
                s.displayName = "Sergeant";
                s.health = 5; s.armorLevel = 1; s.accuracy = 75; s.movementRange = 10f;
            }, s =>
            {
                if (s.weapons.Count == 0 || s.weapons.Contains(null)) s.weapons = new List<WeaponTemplate> { rifle, pistol };
                if (s.primaryWeapon == null) s.primaryWeapon = rifle;
                if (!s.HasKeyword("Biological")) s.keywords.Add(biological);
                GiveAbility(s, frag);
            });

            // --- Placeholder models ---
            var armor = GetOrCreateMaterial("Player_Armor", new Color(0.18f, 0.32f, 0.62f));
            var skin = GetOrCreateMaterial("Skin", new Color(0.85f, 0.68f, 0.55f));
            var weapon = GetOrCreateMaterial("Weapon", new Color(0.22f, 0.22f, 0.24f));
            var gold = GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f));
            trooper.prefab = BuildSoldierPrefab("Trooper", trooper, false, armor, skin, weapon, gold);
            sergeant.prefab = BuildSoldierPrefab("Sergeant", sergeant, true, armor, skin, weapon, gold);
            EditorUtility.SetDirty(trooper);
            EditorUtility.SetDirty(sergeant);

            // --- Unit ---
            var infantry = GetOrCreate<UnitTemplate>($"{DataDir}/Units/InfantrySquad.asset", u =>
            {
                u.displayName = "Infantry Squad";
                u.leader = sergeant;
                u.members = new List<UnitTemplate.Member> { new UnitTemplate.Member { soldier = trooper, count = 9 } };
                u.deploymentSpacing = 2f;
            });

            AssetDatabase.SaveAssets();
            return infantry;
        }

        private static GameObject BuildSoldierPrefab(string name, SoldierTemplate template, bool isSergeant,
            Material armor, Material skin, Material weapon, Material gold, Material cape = null, bool priest = false,
            bool heavyWeapon = false, bool poweredArmor = false, bool sniperKit = false, bool flameKit = false, bool rocketKit = false,
            bool mageKit = false, bool swordKit = false)
        {
            float h = template.height;          // 1.8m
            float headSize = 0.26f;
            float bodyHeight = h - headSize;    // capsule from feet to neck
            float bodyRadius = poweredArmor ? 0.32f : 0.25f;

            var root = new GameObject(name);
            var soldier = root.AddComponent<Soldier>();
            var sso = new SerializedObject(soldier);
            sso.FindProperty("template").objectReferenceValue = template;
            sso.FindProperty("isSergeant").boolValue = isSergeant;
            sso.ApplyModifiedPropertiesWithoutUndo();

            // One collider on the root for selection / line-of-sight later.
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = template.radius;
            col.height = h;
            col.center = new Vector3(0f, h / 2f, 0f);

            // Body (capsule primitive is 2 tall, 1 wide at scale 1).
            Part(root, "Body", PrimitiveType.Capsule, armor,
                new Vector3(0f, bodyHeight / 2f, 0f), new Vector3(bodyRadius * 2f, bodyHeight / 2f, bodyRadius * 2f * 0.8f));
            if (poweredArmor)
            {
                // Powered armor: a helmet over the head, big shoulder plates and a back pack.
                var plate = GetOrCreateMaterial("Powered_Armor", new Color(0.38f, 0.42f, 0.48f));
                Part(root, "Helmet", PrimitiveType.Sphere, plate,
                    new Vector3(0f, bodyHeight + headSize / 2f, 0f), Vector3.one * (headSize * 1.35f));
                Part(root, "Shoulder_L", PrimitiveType.Cube, plate,
                    new Vector3(-0.34f, h * 0.76f, 0f), new Vector3(0.24f, 0.18f, 0.32f));
                Part(root, "Shoulder_R", PrimitiveType.Cube, plate,
                    new Vector3(0.34f, h * 0.76f, 0f), new Vector3(0.24f, 0.18f, 0.32f));
                Part(root, "Power Pack", PrimitiveType.Cube, plate,
                    new Vector3(0f, h * 0.62f, -0.3f), new Vector3(0.4f, 0.5f, 0.2f));
            }
            Part(root, "Head", PrimitiveType.Sphere, skin,
                new Vector3(0f, bodyHeight + headSize / 2f - 0.02f, 0f), Vector3.one * headSize);
            // Rifle held forward; also shows which way the soldier is facing.
            if (swordKit)
            {
                // A broadsword held point-forward: a long flat blade, a crossguard and a grip.
                var blade = GetOrCreateMaterial("Sword_Blade", new Color(0.78f, 0.8f, 0.84f));
                Part(root, "Weapon", PrimitiveType.Cube, blade, new Vector3(0.24f, h * 0.6f, 0.62f), new Vector3(0.06f, 0.02f, 1.0f));
                Part(root, "Crossguard", PrimitiveType.Cube, GetOrCreateMaterial("Sergeant_Gold", new Color(0.95f, 0.72f, 0.2f)),
                    new Vector3(0.24f, h * 0.6f, 0.1f), new Vector3(0.3f, 0.04f, 0.05f));
                Part(root, "Grip", PrimitiveType.Cube, weapon, new Vector3(0.24f, h * 0.6f, 0.0f), new Vector3(0.04f, 0.04f, 0.18f));
            }
            else if (sniperKit)
            {
                // A sniper: a long, thin rifle with a scope, and a green hooded cloak.
                Part(root, "Weapon", PrimitiveType.Cube, weapon,
                    new Vector3(0.18f, h * 0.7f, 0.5f), new Vector3(0.05f, 0.07f, 1.35f));
                Part(root, "Scope", PrimitiveType.Cube, weapon,
                    new Vector3(0.18f, h * 0.7f + 0.07f, 0.35f), new Vector3(0.05f, 0.05f, 0.25f));
                var cloak = GetOrCreateMaterial("Sniper_Cloak", new Color(0.28f, 0.36f, 0.22f));
                Part(root, "Hood", PrimitiveType.Sphere, cloak,
                    new Vector3(0f, bodyHeight + headSize / 2f + 0.02f, -0.03f), Vector3.one * (headSize * 1.3f));
                Part(root, "Cloak", PrimitiveType.Cube, cloak,
                    new Vector3(0f, h * 0.5f, -0.24f), new Vector3(0.52f, h * 0.7f, 0.05f));
            }
            else if (flameKit)
            {
                // A flamethrower: a wide-mouthed nozzle held low, fed by two fuel tanks on the back.
                var tank = GetOrCreateMaterial("Fuel_Tank", new Color(0.75f, 0.42f, 0.12f));
                Part(root, "Weapon", PrimitiveType.Cylinder, weapon,
                    new Vector3(0.2f, h * 0.58f, 0.42f), new Vector3(0.09f, 0.42f, 0.09f), new Vector3(90f, 0f, 0f));
                Part(root, "Nozzle", PrimitiveType.Cylinder, tank,
                    new Vector3(0.2f, h * 0.58f, 0.86f), new Vector3(0.14f, 0.04f, 0.14f), new Vector3(90f, 0f, 0f));
                Part(root, "Fuel Tank_L", PrimitiveType.Capsule, tank,
                    new Vector3(-0.1f, h * 0.6f, -0.27f), new Vector3(0.17f, 0.28f, 0.17f));
                Part(root, "Fuel Tank_R", PrimitiveType.Capsule, tank,
                    new Vector3(0.1f, h * 0.6f, -0.27f), new Vector3(0.17f, 0.28f, 0.17f));
            }
            else if (heavyWeapon)
            {
                // A machine gun: longer and bulkier, held lower, with an ammunition box.
                Part(root, "Weapon", PrimitiveType.Cube, weapon,
                    new Vector3(0.2f, h * 0.6f, 0.45f), new Vector3(0.12f, 0.16f, 1.25f));
                Part(root, "Ammo Box", PrimitiveType.Cube, weapon,
                    new Vector3(0.2f, h * 0.52f, 0.25f), new Vector3(0.16f, 0.14f, 0.2f));
            }
            else
                Part(root, "Weapon", PrimitiveType.Cube, weapon,
                    new Vector3(0.18f, h * 0.68f, 0.32f), new Vector3(0.07f, 0.1f, 0.85f));

            if (mageKit)
            {
                // A battle mage: a tall pointed hat with a wide brim, and a glowing orb in the off hand.
                var hat = GetOrCreateMaterial("Mage_Hat", new Color(0.24f, 0.14f, 0.36f));
                Part(root, "Hat Brim", PrimitiveType.Cylinder, hat, new Vector3(0f, h - 0.02f, 0f), new Vector3(0.5f, 0.015f, 0.5f));
                Part(root, "Hat", PrimitiveType.Capsule, hat, new Vector3(0f, h + 0.16f, -0.02f), new Vector3(0.2f, 0.22f, 0.2f));
                Part(root, "Hat Tip", PrimitiveType.Capsule, hat, new Vector3(0f, h + 0.36f, -0.06f), new Vector3(0.1f, 0.12f, 0.1f));
                Part(root, "Fire Orb", PrimitiveType.Sphere, GetOrCreateMaterial("Fire_Orb", new Color(1f, 0.5f, 0.1f)),
                    new Vector3(-0.3f, h * 0.55f, 0.25f), Vector3.one * 0.16f);
            }

            if (rocketKit)
            {
                // A rocket launcher tube carried on the right shoulder, pointing forward.
                var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tube.name = "Rocket Launcher";
                Object.DestroyImmediate(tube.GetComponent<Collider>());
                tube.transform.SetParent(root.transform, false);
                tube.transform.localPosition = new Vector3(0.3f, h * 0.86f, 0.1f);
                tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                tube.transform.localScale = new Vector3(0.16f, 0.6f, 0.16f);
                tube.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Rocket_Tube", new Color(0.32f, 0.36f, 0.24f));
            }

            if (isSergeant)
            {
                // Sergeant markings: helmet crest, shoulder plates and a sash.
                Part(root, "Crest", PrimitiveType.Cube, gold,
                    new Vector3(0f, h + 0.04f, -0.02f), new Vector3(0.05f, 0.12f, 0.28f));
                Part(root, "Pauldron_L", PrimitiveType.Sphere, gold,
                    new Vector3(-0.26f, h * 0.78f, 0f), Vector3.one * 0.2f);
                Part(root, "Pauldron_R", PrimitiveType.Sphere, gold,
                    new Vector3(0.26f, h * 0.78f, 0f), Vector3.one * 0.2f);
                Part(root, "Sash", PrimitiveType.Cylinder, gold,
                    new Vector3(0f, h * 0.5f, 0f), new Vector3(bodyRadius * 2.1f, 0.04f, bodyRadius * 2.1f * 0.8f));
            }

            if (priest)
            {
                // Tall gold mitre and a gold sash over the robe.
                Part(root, "Mitre", PrimitiveType.Cylinder, gold,
                    new Vector3(0f, h + 0.1f, 0f), new Vector3(0.2f, 0.14f, 0.2f));
                Part(root, "Sash", PrimitiveType.Cylinder, gold,
                    new Vector3(0f, h * 0.5f, 0f), new Vector3(bodyRadius * 2.1f, 0.04f, bodyRadius * 2.1f * 0.8f));
            }

            if (cape != null)
            {
                // Officer's cape and peaked cap.
                Part(root, "Cape", PrimitiveType.Cube, cape,
                    new Vector3(0f, h * 0.5f, -0.24f), new Vector3(0.5f, h * 0.62f, 0.04f));
                Part(root, "Cap", PrimitiveType.Cylinder, armor,
                    new Vector3(0f, h - 0.02f, 0.02f), new Vector3(0.3f, 0.03f, 0.32f));
            }

            Eldoria.Terrain.TerrainLayers.SetLayerRecursively(root, Eldoria.Terrain.TerrainLayers.Soldier);

            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Part(GameObject parent, string name, PrimitiveType type, Material mat, Vector3 pos, Vector3 scale,
            Vector3? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            if (rotation.HasValue) go.transform.localEulerAngles = rotation.Value;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Loads an asset, or creates it with <paramref name="init"/>. <paramref name="upgrade"/> runs on existing assets.</summary>
        private static T GetOrCreate<T>(string path, System.Action<T> init, System.Action<T> upgrade = null) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                if (upgrade != null)
                {
                    upgrade(asset);
                    EditorUtility.SetDirty(asset);
                }
                return asset; // keep any tweaks made in the inspector
            }
            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            upgrade?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
