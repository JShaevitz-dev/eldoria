using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Eldoria.Units
{
    /// <summary>
    /// A single soldier on the battlefield, built from a <see cref="SoldierTemplate"/>. Belongs to a <see cref="Unit"/>
    /// and carries <see cref="Weapon"/>s. Its pivot sits at its feet, on the ground.
    /// Rank (sergeant or not) is fixed; being unit leader is a role held by one living soldier
    /// at a time and can pass to someone else (see <see cref="Unit.Leader"/>).
    /// </summary>
    public class Soldier : MonoBehaviour
    {
        [FormerlySerializedAs("profile")]
        [SerializeField] private SoldierTemplate template;
        [FormerlySerializedAs("isLeader")]
        [SerializeField] private bool isSergeant;
        [SerializeField] private int health;
        [SerializeField] private List<Weapon> weapons = new List<Weapon>();
        [SerializeField] private KeywordSet keywordState = new KeywordSet();
        [SerializeField] private List<NearbyKeyword> nearbyKeywords = new List<NearbyKeyword>();

        private Unit unit;
        private bool isAlive = true;
        private bool removedFromPlay;
        private readonly Dictionary<Ability, int> abilityUses = new Dictionary<Ability, int>();
        private bool initialized;

        /// <summary>The army builder choices for this soldier (weapons swapped, equipment), if any.</summary>
        [System.NonSerialized] private SoldierLoadout loadout;
        [System.NonSerialized] private List<Ability> abilitiesWithEquipment;

        public SoldierLoadout Loadout => loadout;
        private IEnumerable<Equipment> Gear => loadout != null ? loadout.Equipment.Where(e => e != null) : Enumerable.Empty<Equipment>();

        /// <summary>
        /// Applies the army builder choices: swapped weapons replace the ones they stand for (a swapped primary weapon is the
        /// new primary), equipment adds its weapons, keywords, abilities and stat changes. Health starts at the new maximum.
        /// </summary>
        public void ApplyLoadout(SoldierLoadout newLoadout)
        {
            loadout = newLoadout;
            abilitiesWithEquipment = null;
            weapons.Clear();
            if (template != null)
                foreach (var w in template.weapons)
                    if (w != null) weapons.Add(new Weapon(loadout != null ? loadout.Swap(w) : w, this));
            foreach (var e in Gear)
                foreach (var w in e.weapons)
                    if (w != null) weapons.Add(new Weapon(w, this));
            if (loadout != null && template != null)
            {
                abilitiesWithEquipment = new List<Ability>(template.abilities);
                foreach (var e in Gear)
                    foreach (var a in e.abilities)
                        if (a != null && !abilitiesWithEquipment.Contains(a)) abilitiesWithEquipment.Add(a);
            }
            initialized = true;
            health = MaxHealth;
        }

        public SoldierTemplate Template => template;
        public bool IsSergeant => isSergeant;
        public bool IsAlive => isAlive;

        /// <summary>
        /// Can be picked as a target: alive, or killed during the volley being resolved right now (the dead are removed
        /// when that volley ends, so a later attacking unit can't target them).
        /// </summary>
        public bool CanBeTargeted => isAlive || (!removedFromPlay && Rules.Casualties.IsPendingRemoval(this));
        public Unit Unit => unit != null ? unit : unit = GetComponentInParent<Unit>(true);

        /// <summary>True while this soldier holds the unit-leader role.</summary>
        public bool IsLeader => Unit != null && Unit.Leader == this;

        // ---- Stats (from the template) ----
        public string ClassName => template != null ? template.displayName : name;
        /// <summary>Starting (maximum) health (equipment can change it).</summary>
        public int MaxHealth => template != null ? Mathf.Max(1, template.health + Gear.Sum(e => e.health)) : 1;
        /// <summary>Current health.</summary>
        public int Health { get { EnsureInitialized(); return health; } }
        public int ArmorLevel => template != null ? Mathf.Max(0, template.armorLevel + Gear.Sum(e => e.armorLevel)) : 0;
        /// <summary>Base chance to hit, in percent.</summary>
        public int Accuracy => template != null ? template.accuracy + Gear.Sum(e => e.accuracy) : 0;
        /// <summary>Movement range in meters (a normal move).</summary>
        public float Move => template != null ? Mathf.Max(0f, template.movementRange + Gear.Sum(e => e.movement)) : 0f;
        /// <summary>Can this soldier sprint? Mechanical soldiers can't.</summary>
        public bool CanSprint => !HasKeyword(Rules.MovementRules.MechanicalKeyword);
        /// <summary>Sprinting move in meters (just the normal move for a soldier who can't sprint).</summary>
        public float Sprint => CanSprint ? Move * Rules.MovementRules.SprintMultiplier : Move;

        // ---- Keywords: inherent (template) − lost + gained during the game ----
        private IEnumerable<Keyword> InherentKeywords
        {
            get
            {
                if (template == null) return null;
                if (loadout == null) return template.keywords;
                var list = new List<Keyword>(template.keywords);
                foreach (var e in Gear)
                    foreach (var k in e.keywords) Keyword.AddUnique(list, k);   // equipment keywords
                return list;
            }
        }
        /// <summary>Keywords the soldier has right now (including any granted by nearby terrain).</summary>
        public IReadOnlyList<Keyword> Keywords
        {
            get
            {
                var list = keywordState.Current(InherentKeywords);
                foreach (var n in nearbyKeywords)
                    Keyword.AddUnique(list, n.keyword);
                return list;
            }
        }

        /// <summary>Keywords granted by nearby terrain right now, and which object granted each.</summary>
        public IReadOnlyList<NearbyKeyword> NearbyKeywords => nearbyKeywords;

        /// <summary>Replaces the keywords granted by nearby terrain (see Terrain.NearbyKeywords).</summary>
        public void SetNearbyKeywords(IEnumerable<NearbyKeyword> granted)
        {
            nearbyKeywords.Clear();
            if (granted != null) nearbyKeywords.AddRange(granted);
        }

        /// <summary>The terrain objects currently granting this soldier a keyword (e.g. the barricade giving In half-cover).</summary>
        public IEnumerable<Component> SourcesOf(Keyword keyword)
        {
            foreach (var n in nearbyKeywords)
                if (n.keyword == keyword && n.source != null) yield return n.source;
        }
        public bool HasKeyword(string keyword) => Keyword.Contains(Keywords, keyword);
        /// <summary>Keywords gained during the game and when they wear off.</summary>
        public IReadOnlyList<GainedKeyword> GainedKeywords => keywordState.Gained;

        /// <param name="expiresAtTurn">Expiry point (see Rules.Turns.NextTurnOf) at whose start the keyword is removed; 0 = until removed.</param>
        public void GainKeyword(Keyword keyword, int expiresAtTurn = 0) => keywordState.Gain(keyword, InherentKeywords, expiresAtTurn);
        /// <summary>Removes a keyword, gained or inherent. Returns false if the soldier didn't have it.</summary>
        public bool LoseKeyword(Keyword keyword) => keywordState.Lose(keyword, InherentKeywords);

        /// <summary>Called at the start of every turn: keywords (on the soldier and their weapons) that wear off are removed.</summary>
        public void OnTurnStarted(int turn)
        {
            abilityUses.Clear();
            keywordState.ExpireAtTurnStart(turn);
            foreach (var w in Weapons) w.OnTurnStarted(turn);
        }

        /// <summary>Physical footprint radius in meters.</summary>
        public float Radius => template != null ? template.radius : 0.35f;
        public float Height => template != null ? template.height : 1.8f;

        /// <summary>How many times this soldier has used an active ability this turn.</summary>
        public int TimesUsedThisTurn(Ability ability) => ability != null && abilityUses.TryGetValue(ability, out int n) ? n : 0;

        public void RecordAbilityUse(Ability ability)
        {
            if (ability == null) return;
            abilityUses[ability] = TimesUsedThisTurn(ability) + 1;
        }

        /// <summary>Restores health, not above the soldier's starting health. Returns how much was restored.</summary>
        public int Heal(int amount)
        {
            if (!isAlive || amount <= 0) return 0;
            EnsureInitialized();
            int before = health;
            health = Mathf.Min(MaxHealth, health + amount);
            return health - before;
        }

        /// <summary>Abilities this soldier has (from their template, plus any their equipment gives).</summary>
        public IReadOnlyList<Ability> Abilities => abilitiesWithEquipment != null ? abilitiesWithEquipment
            : template != null ? template.abilities : (IReadOnlyList<Ability>)System.Array.Empty<Ability>();

        /// <summary>The weapon this soldier shoots with under normal conditions.</summary>
        public Weapon PrimaryWeapon
        {
            get
            {
                var primary = template != null ? template.PrimaryWeapon : null;
                if (loadout != null) primary = loadout.Swap(primary);   // a swapped primary weapon is the new primary
                foreach (var w in Weapons)
                    if (w.Template == primary) return w;
                return Weapons.Count > 0 ? Weapons[0] : null;
            }
        }

        /// <summary>
        /// The weapon to shoot <paramref name="target"/> with. Normally the primary weapon; if it can't be used right now
        /// (see <see cref="Rules.WeaponUse"/>: close range, Deployed after moving), another weapon the soldier can use;
        /// otherwise none (null: the soldier can't shoot).
        /// </summary>
        public Weapon WeaponFor(Soldier target)
        {
            // The primary weapon if it can be used right now; otherwise the first other weapon that can (e.g. a pistol in
            // close range, or anything but a Deployed weapon after moving); otherwise none.
            var primary = PrimaryWeapon;
            if (primary != null && Rules.WeaponUse.CanUse(this, primary)) return primary;
            foreach (var w in Weapons)
                if (w != primary && Rules.WeaponUse.CanUse(this, w)) return w;
            return null;
        }

        /// <summary>Weapons this soldier uses through abilities (e.g. a Rocket Launcher), one instance each so they remember
        /// when they last fired (Manual Load) and take the soldier's faction changes. Not used in volleys.</summary>
        [System.NonSerialized] private Dictionary<WeaponTemplate, Weapon> abilityWeapons;

        /// <summary>Does one of this soldier's passive abilities nullify this keyword (e.g. Stabilizers: Deployed)?</summary>
        public bool Nullifies(string keyword) =>
            Abilities.Any(a => a != null && a.kind == AbilityKind.Passive && a.nullifiesKeyword != null && a.nullifiesKeyword.Is(keyword));

        public Weapon AbilityWeapon(WeaponTemplate template)
        {
            if (template == null) return null;
            abilityWeapons ??= new Dictionary<WeaponTemplate, Weapon>();
            if (!abilityWeapons.TryGetValue(template, out var w)) abilityWeapons[template] = w = new Weapon(template, this);
            return w;
        }

        /// <summary>Weapons this soldier is carrying.</summary>
        public IReadOnlyList<Weapon> Weapons { get { EnsureInitialized(); return weapons; } }

        public Vector3 Position => transform.position;

        private void Awake() => EnsureInitialized();

        /// <summary>Sets up a soldier from a template (used when spawning units, and in tests).</summary>
        public void Configure(SoldierTemplate newTemplate, bool sergeant)
        {
            template = newTemplate;
            isSergeant = sergeant;
            initialized = false;
            weapons.Clear();
            keywordState.Clear();
            nearbyKeywords.Clear();
            EnsureInitialized();
        }

        /// <summary>Fills in health and weapons from the template the first time they're needed.</summary>
        private void EnsureInitialized()
        {
            // Each weapon knows who holds it (its stats depend on the holder's faction).
            foreach (var w in weapons)
                if (w != null && w.Owner != this) w.Owner = this;
            if (initialized || template == null) return;
            initialized = true;
            health = template.health;
            if (weapons.Count == 0)
                foreach (var w in template.weapons)
                    if (w != null) weapons.Add(new Weapon(w, this));
        }

        /// <summary>Reduces health; the soldier is killed at 0.</summary>
        public void TakeDamage(int amount)
        {
            if (!isAlive || amount <= 0) return;
            EnsureInitialized();
            health = Mathf.Max(0, health - amount);
            if (health == 0) Kill();
        }

        /// <summary>
        /// The soldier dies. If they were the unit leader, command passes on at once. If they die during a volley,
        /// the body stays (and can still be shot by soldiers who already picked them) until that volley ends;
        /// otherwise it is removed at once (see <see cref="Rules.Casualties"/>).
        /// </summary>
        public void Kill()
        {
            if (!isAlive) return;
            isAlive = false;
            health = 0;
            var myUnit = Unit;
            if (myUnit != null) myUnit.OnSoldierKilled(this);
            // Lie down where they fell.
            transform.rotation = Quaternion.Euler(-90f, transform.eulerAngles.y, 0f);
            // "On death" triggers (their own keywords and their weapons', e.g. Unstable) - whatever killed them.
            // Inside an ongoing volley or blast; anyone they take with them is removed when it ends.
            Rules.Casualties.BeginVolley();
            try { Rules.KeywordTriggers.Fire(TriggerEvent.Died, this, null); }
            finally { Rules.Casualties.EndVolley(); }
            Rules.Casualties.OnKilled(this);
        }

        /// <summary>Takes a dead soldier off the battlefield (when the volley that killed them ends).</summary>
        public void RemoveFromPlay()
        {
            if (isAlive) return;
            removedFromPlay = true;
            gameObject.SetActive(false);
        }

        /// <summary>Dead and taken off the battlefield: can't be targeted any more.</summary>
        public bool IsRemovedFromPlay => removedFromPlay;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!IsLeader) return;
            // Normal move (blue) and sprint (orange) reach from the leader's current position.
            Handles.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Handles.DrawWireDisc(Position + Vector3.up * 0.02f, Vector3.up, Move);
            Handles.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Handles.DrawWireDisc(Position + Vector3.up * 0.02f, Vector3.up, Sprint);
        }
#endif
    }
}
