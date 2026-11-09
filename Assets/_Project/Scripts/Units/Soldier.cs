using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Eldoira.Units
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
        /// <summary>Starting (maximum) health.</summary>
        public int MaxHealth => template != null ? template.health : 1;
        /// <summary>Current health.</summary>
        public int Health { get { EnsureInitialized(); return health; } }
        public int ArmorLevel => template != null ? template.armorLevel : 0;
        /// <summary>Base chance to hit, in percent.</summary>
        public int Accuracy => template != null ? template.accuracy : 0;
        /// <summary>Movement range in meters (a normal move).</summary>
        public float Move => template != null ? template.movementRange : 0f;
        /// <summary>Sprinting move in meters.</summary>
        public float Sprint => Move * Rules.MovementRules.SprintMultiplier;

        // ---- Keywords: inherent (template) − lost + gained during the game ----
        private IEnumerable<Keyword> InherentKeywords => template != null ? template.keywords : null;
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

        /// <param name="expiresAtTurn">Turn number at whose start the keyword is removed; 0 = until removed.</param>
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

        /// <summary>Abilities this soldier has (from their template).</summary>
        public IReadOnlyList<Ability> Abilities => template != null ? template.abilities : (IReadOnlyList<Ability>)System.Array.Empty<Ability>();

        /// <summary>The weapon this soldier shoots with under normal conditions.</summary>
        public Weapon PrimaryWeapon
        {
            get
            {
                var primary = template != null ? template.PrimaryWeapon : null;
                foreach (var w in Weapons)
                    if (w.Template == primary) return w;
                return Weapons.Count > 0 ? Weapons[0] : null;
            }
        }

        /// <summary>
        /// The weapon to shoot <paramref name="target"/> with. Normally the primary weapon. In close range only a weapon
        /// with the Pistol keyword can be used: the primary if it is one, otherwise the soldier's first pistol, otherwise
        /// none (null: the soldier can't shoot). Other circumstances that change weapons will hook in here too.
        /// </summary>
        public Weapon WeaponFor(Soldier target)
        {
            var primary = PrimaryWeapon;
            if (Rules.CloseRange.CanUseWeapon(this, primary)) return primary;
            foreach (var w in Weapons)
                if (w.HasKeyword(Rules.CloseRange.PistolKeyword)) return w;
            return null;
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
            if (initialized || template == null) return;
            initialized = true;
            health = template.health;
            if (weapons.Count == 0)
                foreach (var w in template.weapons)
                    if (w != null) weapons.Add(new Weapon(w));
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
