using System.Collections.Generic;
using System.Linq;
using Eldoira.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Eldoira.EditorTools
{
    /// <summary>
    /// Creates the template assets for the test unit and spawns it in the open scene:
    ///   Keywords: Biological, Toxic, Moved, Explosive, Incendiary, Sniper, Robotic (Data/Keywords)
    ///             + the keyword library (Resources/KeywordLibrary)
    ///   Weapons:  Assault Rifle, Pistol                      (Data/Weapons)
    ///   Soldiers: Trooper (rifle), Sergeant (rifle + pistol), both Biological (Data/Soldiers)
    ///   Unit:     Infantry Squad = 1 Sergeant + 9 Troopers    (Data/Units)
    /// plus placeholder soldier prefabs built from primitives. Existing template assets are kept, so
    /// stats edited in the Inspector survive a rebuild.
    /// Menu: Eldoira > Spawn Test Unit.
    /// </summary>
    public static class SquadBuilder
    {
        private const string Root = "Assets/_Project";
        private const string DataDir = Root + "/Data";
        private const string PrefabDir = Root + "/Prefabs/Units";
        private const string MaterialDir = Root + "/Materials";

        private const string UnitName = "Infantry Squad";

        [MenuItem("Eldoira/Spawn Test Unit")]
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
            Debug.Log($"[Eldoira] Spawned {unit.Soldiers.Count}-soldier unit '{unit.DisplayName}' from template " +
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

        private const string PriestName = "Battlefield Priest";

        /// <summary>
        /// Battlefield Priest unit: 1 Battlefield Priest (Health 5, Armor 0, Accuracy 50%, 10 m; Biological, Officer,
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
                s.displayName = "Battlefield Priest";
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
                u.displayName = "Battlefield Priest";
                u.leader = priest;
                u.members = new List<UnitTemplate.Member>();
            });
            AssetDatabase.SaveAssets();
            return unit;
        }

        /// <summary>Divine Healing and Divine Sanctuary are Spells (can't be cast after the unit sprinted).</summary>
        private static void TagAsSpell(Ability ability, Dictionary<string, Keyword> keywords)
        {
            Keyword.AddUnique(ability.keywords, keywords["Spell"]);
            // The Battlefield Priest's spells ring out with a hallelujah when cast.
            ability.castSound = AssetDatabase.LoadAssetAtPath<AudioClip>(PriestSpellSoundPath);
            if (ability.castSound == null) Debug.LogWarning($"[Eldoira] Missing priest spell sound at {PriestSpellSoundPath}.");
        }

        private const string PriestSpellSoundPath = Root + "/Resources/Audio/Hallelujah.mp3";

        /// <summary>
        /// Updates the keyword and ability assets in place without rebuilding prefabs or touching the scene
        /// (batchmode: -executeMethod Eldoira.EditorTools.SquadBuilder.UpdateAbilityData).
        /// </summary>
        public static void UpdateAbilityData()
        {
            var keywords = EnsureKeywords();
            foreach (var file in new[] { "DivineHealing", "DivineSanctuary" })
            {
                var ability = AssetDatabase.LoadAssetAtPath<Ability>($"{DataDir}/Abilities/{file}.asset");
                if (ability == null) continue;
                TagAsSpell(ability, keywords);
                EditorUtility.SetDirty(ability);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Eldoira] Ability data updated.");
        }

        /// <summary>Spawns the player's Battlefield Priest just behind the Infantry Squad's right side.</summary>
        public static Unit SpawnBattlefieldPriest()
        {
            var template = CreateBattlefieldPriest();
            foreach (var old in Object.FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (old.DisplayName == PriestName && old.Side == Side.Player) Object.DestroyImmediate(old.gameObject);

            var unit = UnitFactory.Spawn(template, Side.Player, new Vector3(3f, 0f, -20.5f), Quaternion.identity,
                unitName: PriestName,
                instantiate: (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent));
            EditorUtility.SetDirty(unit);
            Debug.Log($"[Eldoira] Spawned '{unit.DisplayName}' ({unit.Soldiers.Count} soldier).");
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
            Debug.Log($"[Eldoira] Spawned '{unit.DisplayName}' ({unit.Soldiers.Count} soldier).");
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
                        Eldoira.Terrain.TerrainLayers.StructureMask, QueryTriggerInteraction.Ignore))
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
            Debug.Log($"[Eldoira] Spawned enemy unit '{unit.DisplayName}' ({unit.Soldiers.Count} soldiers).");
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
            Kw("Explosive", "Explosive", KeywordTarget.Weapon, "Hits affect an area. (Effect not designed yet.)");
            Kw("Incendiary", "Incendiary", KeywordTarget.Weapon, "Sets targets or terrain on fire. (Effect not designed yet.)");
            Kw("Sniper", "Sniper", KeywordTarget.Soldier, "A trained marksman. (Effect not designed yet.)");
            Kw("Robotic", "Robotic", KeywordTarget.Soldier, "A machine rather than a person. (Effect not designed yet.)");
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
            GetOrCreate<Keyword>($"{DataDir}/Keywords/FullCover.asset", k =>
            {
                k.displayName = "Full-cover";
                k.appliesTo = KeywordTarget.TerrainObject;
                k.description = "Soldiers within range gain In full cover. (Range is a placeholder.)";
                k.grantsNearby = inFullCover;
                k.grantRadius = 5f;
            });
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
            GetOrCreate<Keyword>($"{DataDir}/Keywords/DivineSanctuary.asset", k =>
            {
                k.displayName = "Divine Sanctuary";
                k.appliesTo = KeywordTarget.Soldier;
                k.description = "Blessed by a Battlefield Priest: +1 Armor Level.";
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
                ["Biological"] = KeywordCategory.Trait, ["Robotic"] = KeywordCategory.Trait, ["Sniper"] = KeywordCategory.Trait,
                ["Officer"] = KeywordCategory.Trait, ["Solo"] = KeywordCategory.Trait, ["Magic"] = KeywordCategory.Trait,
                ["Spell"] = KeywordCategory.Trait,
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

            // --- Weapons (placeholder stats) ---
            var rifle = GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/AssaultRifle.asset", w =>
            {
                w.displayName = "Assault Rifle";
                w.baseDamage = 4; w.penetration = 1; w.accuracyBonus = 0;
            });
            var pistol = GetOrCreate<WeaponTemplate>($"{DataDir}/Weapons/Pistol.asset", w =>
            {
                w.displayName = "Pistol";
                w.baseDamage = 2; w.penetration = 0; w.accuracyBonus = 0;
            });

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
            Material armor, Material skin, Material weapon, Material gold, Material cape = null, bool priest = false)
        {
            float h = template.height;          // 1.8m
            float headSize = 0.26f;
            float bodyHeight = h - headSize;    // capsule from feet to neck
            float bodyRadius = 0.25f;

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
            Part(root, "Head", PrimitiveType.Sphere, skin,
                new Vector3(0f, bodyHeight + headSize / 2f - 0.02f, 0f), Vector3.one * headSize);
            // Rifle held forward; also shows which way the soldier is facing.
            Part(root, "Weapon", PrimitiveType.Cube, weapon,
                new Vector3(0.18f, h * 0.68f, 0.32f), new Vector3(0.07f, 0.1f, 0.85f));

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

            Eldoira.Terrain.TerrainLayers.SetLayerRecursively(root, Eldoira.Terrain.TerrainLayers.Soldier);

            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Part(GameObject parent, string name, PrimitiveType type, Material mat, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
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
