# Eldoira — Game Rules

Living record of the rules decided so far. **Status** shows what the code does today.

## Scale

- All distances are in **meters** (1 Unity unit = 1 m). Soldiers are human-sized (~1.8 m tall) and have no bases.
- Distances are measured horizontally between soldiers' positions.

## Squads

- A squad is a group of soldiers that moves together. The demo squad is 10 infantry: 9 troopers and 1 sergeant.
- One living soldier holds the **squad leader** role. At the start of a battle it is the sergeant.

### Leader succession — *implemented*

- If the squad leader is killed, the living squad member **physically closest** to where the leader fell becomes the new squad leader. Only soldiers **of that same squad** can inherit command — never a soldier of another unit, however close.
- If two or more soldiers are equally close (within 1 cm), a **random roll** picks one of them.
- This repeats every time a leader falls, until the squad is wiped out.

## Units, soldiers and weapons — *implemented*

- A **unit** is made of **soldiers**, who carry **weapons**. Each is defined by a template asset (Assets/_Project/Data):
  - **Unit template:** the soldier class that leads the unit, plus which other soldier classes it contains and how many.
  - **Soldier template:** the soldier's stats, keywords and the weapon classes they carry.
  - **Weapon template:** the weapon's stats and keywords.

| Soldier stat | Meaning |
|---|---|
| Health | Damage the soldier can take. |
| Armor Level | Protection, compared against a weapon's Penetration. |
| Accuracy | Base chance to hit, in percent. |
| Movement range | Normal move in meters. Sprinting doubles it. |
| Keywords | Extra properties, e.g. Sniper, Robotic. |

| Weapon stat | Meaning |
|---|---|
| Base Damage | Damage dealt by a hit. |
| Penetration | How well it gets through Armor Level. |
| Accuracy Bonus | Added to the soldier's Accuracy. |
| Keywords | Extra properties, e.g. Explosive, Incendiary. |

## Units

### Infantry Squad

1 **Sergeant** (leader) + 9 **Troopers**. All soldiers are **Biological**.

| Soldier | Health | Armor Level | Accuracy | Movement | Weapons (primary first) |
|---|---|---|---|---|---|
| Sergeant | 5 | 1 | 75% | 10 m | Assault Rifle, Pistol |
| Trooper | 5 | 1 | 75% | 10 m | Assault Rifle |

| Weapon | Base Damage | Penetration | Accuracy Bonus | Keywords |
|---|---|---|---|---|
| Assault Rifle | 4 | 1 | +0 | — |
| Pistol | 2 | 0 | +0 | — |

### Field Commander

1 **Field Commander** (a unit of one).

| Soldier | Health | Armor Level | Accuracy | Movement | Weapons (primary first) |
|---|---|---|---|---|---|
| Field Commander | 10 | 1 | 75% | 10 m | Pistol |

- **Keywords:** Biological (Trait), Officer (Trait), Solo (Trait).
- **Passive ability — Lead from the front:** **friendly** soldiers within **15 m** gain **+15% accuracy**. The **Field Commander does not benefit**. Each soldier is checked **on its own**: being in the same unit as a soldier who has the bonus doesn't give it — so one soldier of a unit might have it, or all but one.

### Battlefield Priest

1 **Battlefield Priest** (a unit of one).

| Soldier | Health | Armor Level | Accuracy | Movement | Weapons (primary first) |
|---|---|---|---|---|---|
| Battlefield Priest | 5 | 0 | 50% | 10 m | Pistol |

- **Keywords:** Biological (Trait), Officer (Trait), Solo (Trait), Magic (Trait).
- **Active ability (Spell) — Divine Healing:** choose a **friendly** soldier within **25 m** who is **Biological** (the priest can choose themself): they restore **5 health** (not above their starting health). The dead can't be healed.
- **Active ability (Spell) — Divine Sanctuary:** choose a **friendly unit** within **25 m** (measured to its closest soldier): **every soldier in that unit** gains **Divine Sanctuary: +1 Armor Level**, until the **start of the next turn**.

### Keywords — *implemented*

Keywords are properties on weapons and soldiers that **interact with each other** in unique ways. Each keyword is an asset (Data/Keywords) listing its interactions. An interaction says:

> **If** the *attacker / weapon / target* **has / lacks** keyword *X* → *effect*

Effects: **no effect** (the attack does nothing — no damage, no bonus effects), **damage bonus/penalty**, **accuracy bonus/penalty**, **always hits**, **always misses**, **can't be targeted**, **ignores line of sight**, **ignores accuracy modifiers**.

- Weapon keywords apply whenever the weapon attacks. A soldier's keyword interactions are marked as applying **when attacking** or **when defending**.
- Example: every living soldier has **Biological**. **Toxic** (weapon): *if the target lacks Biological → no effect*. So a Toxic weapon harms soldiers but does nothing to a tank.
- Defined so far: **Biological**, **Toxic** (with the interaction above), **Moved**, **Half-cover**, **In half-cover** (below). **Explosive, Incendiary, Sniper, Robotic** exist with no interactions yet.

#### Keyword categories

Keywords are marked **Trait** (what a soldier or ability is: Biological, Robotic, Sniper, Officer, Solo, Spell) or **Status** (a temporary state: Moved, In half-cover, In full cover, Led from the front).

- Some traits **do nothing on their own**: **Officer** and **Solo** only matter because other weapons and abilities check for them (e.g. something that only affects Officers, or has no effect on Solo soldiers) — done with keyword interactions such as *"if the target has / lacks Officer → …"*.

#### Abilities — *implemented (passive auras)*

- Soldier templates can have **abilities**. A **passive** ability is always in effect.
- A passive **aura** grants a keyword to soldiers within a radius of the soldier who has it (measured horizontally), and that keyword carries the effect. It can affect allies, enemies or everyone, and can include the soldier themself. It's updated whenever anyone moves, a move is undone, and at the start of each turn, and ends when the soldier dies.
- Example: **Lead from the front** → other friendly soldiers within 15 m gain **Led from the front** (+15% accuracy when attacking).

#### Active abilities — *implemented*

- **Abilities can target their user** (or the user's own unit) **unless they say otherwise** — this applies to passive and active abilities. (Lead from the front says otherwise.)
- See **Unit actions** below for when an active ability can be used.
- The target must be the right side (e.g. friendly), within range (horizontal; for a unit, to its closest soldier) and have any required keyword (e.g. Biological).
- Effects so far: **restore health** (up to starting health) and **give a keyword** (to the soldier, or every soldier of the unit), optionally until the next turn.
- Each soldier can use each active ability once per turn (and in practice a unit uses at most one ability per turn — see Unit actions).
- Keywords can now change **Armor Level** for an attack (armor bonus / penalty), which counts in the penetration check.

#### Spells — *implemented*

- Abilities can have keywords that classify them. **Spell** (ability keyword, Trait) marks an ability as a spell.
- **A Spell can't be cast if the soldier sprinted** — meaning the soldier's **unit sprinted this turn** (its leader sprinted), the same test as the sprint accuracy penalty. So every soldier in a sprinting unit is affected.
- Casting a Spell after a normal move, after holding position, or instead of moving is allowed. Undoing a sprint lifts the restriction; it ends at the start of the next turn.
- Abilities that aren't Spells can still be used after sprinting.
- Spells so far: **Divine Healing**, **Divine Sanctuary**. (Lead from the front is not a Spell.)

#### Gaining and losing keywords — *implemented*

- Besides their **inherent** keywords (from their template), soldiers and weapons can **gain and lose keywords** during the game. Losing an inherent keyword only affects that one soldier/weapon.
- A gained keyword can last until removed, or **wear off at the start of a given turn**.
- Gained keywords take part in keyword interactions exactly like inherent ones.
- **A soldier, weapon or object can only have each keyword once.** Gaining a keyword it already has adds nothing (it can only make it last longer). Keywords with the same name count as the same keyword, and repeats are removed from templates. So a soldier near two Half-cover objects is In half-cover once, and takes the −25% once.
- **Moved:** a soldier who moves (including a 0 m move such as climbing down) gains **Moved**; it is removed at the **start of the next turn**. Undoing a move removes it. A leader who holds position doesn't gain it.

#### Terrain keywords and cover — *implemented (except the "between" check)*

- **Object terrain can have keywords** too, and can gain and lose them like soldiers and weapons.
- A keyword on an object can **grant a keyword to soldiers nearby**: every soldier within its radius (measured **horizontally from the nearest part of the object**, ignoring height) has the granted keyword. This is updated whenever a soldier moves, a move is undone, and at the start of every turn.
- **Half-cover** (object keyword): soldiers within **5 m** gain **In half-cover**.
- **In half-cover** (soldier keyword): an attack against this soldier takes **−25% accuracy** — **only if the object granting it is between the attacker and the target**. *Checking for obstacles between them comes later with line of sight; until then that check always passes.*
- **Full-cover** (object keyword): soldiers within **5 m** gain **In full cover**. *(5 m is a placeholder — see open questions.)*
- **In full cover** (soldier keyword): the soldier is fully concealed behind the cover and **cannot be targeted by a shooting attack** — again **only if the object granting it is between the attacker and the target** (same placeholder check as half-cover).
- **Indirect fire** (weapon keyword): the attack **ignores line of sight** — so it can target soldiers **in full cover**, and cover being "between" doesn't matter — and **ignores accuracy modifiers**: it uses the soldier's Accuracy stat with no penalties or bonuses (no sprint penalty, no cover penalty, not even its own Accuracy Bonus). *Accuracy overrides (always hits / always misses) still apply — assumed, see open questions.*
- Interactions can now use the condition **Always** (apply whenever the keyword is involved), e.g. In half-cover's penalty.
- Test room: a **Barricade** (wood, mantleable) with Half-cover sits near the squad's right flank.

## Movement — *implemented*

1. **The squad leader moves first.**
   - **Normal move:** up to their **movement range** (10 m for the demo squad).
   - **Sprint:** up to **double** their movement range (20 m).
   - Or the leader can **hold position**.
2. **Then the rest of the squad moves.** Each soldier may move **any distance**, but must **end in cohesion**: within **5 m** of the squad leader.
   - Cohesion is measured from whoever was squad leader **when the movement began**.
3. The squad's move can only end once every soldier is in cohesion.

### Measuring a move — *implemented*

- A move is measured along the **route** the soldier walks: the shortest way around anything that blocks movement.
- The route is measured **along the surface of structure terrain**: going over a hill costs the distance up and down it; crossing a ditch costs the distance down and back up.
- The route is measured **straight through object terrain**: objects never add distance.
- **Cohesion** (the 5 m) is measured as a **straight line** from the leader, **not following terrain and ignoring elevation**.
- **Climbing onto an object of any height costs 10 m** of movement (on top of walking to it). Soldiers climb on and off from right beside the object.
- **Climbing down costs 0 m**, but the soldier **still counts as having moved** this turn.
- Any soldier given a move counts as having moved, even if it costs 0 m. A leader who holds position has not moved.
- Walking up a **slope** is not climbing: it's measured along the slope.
- **Maps don't use steps.** Every elevation change is a slope, a climbable object, or purely visual.

### Movement restrictions — *implemented*

- Soldiers **may walk through each other**, but may **not end a move overlapping another soldier**.
- Soldiers cannot end a move outside the battlefield.
- **Terrain that cannot be mantled over blocks movement**: building walls, and objects marked as not mantleable. Routes go around them.
- Soldiers can only end a move on walkable ground or **on top of an object that can be climbed onto**.

## Terrain — *implemented*

Terrain comes in two categories:

**Structure** — large, static landforms and buildings: ground, hills, ditches, slopes, buildings. Movement is measured along its surface. Building walls block movement; doorways let soldiers through.

**Object** — modeled props with some interaction. Each object says whether it:

| Property | Meaning |
|---|---|
| **Cover** | Can be used as cover (used by the shooting rules later). |
| **Mantle** | Soldiers can move over it. If not, it blocks movement. |
| **Climb onto** | Soldiers can end a move standing on top of it (costs 10 m to climb up). |

Test room terrain:

| Terrain | Category | Cover | Mantle | Climb onto |
|---|---|---|---|---|
| Hill (3 m high) | Structure | | | |
| Ditch (1.2 m deep) | Structure | | | |
| Building (roofless, one doorway) | Structure | | | |
| Platform (2 m high) with a slope up to it | Structure | | | |
| Low wall | Object | Yes | Yes | No |
| Crate | Object | Yes | Yes | Yes |
| Shipping container (keyword: Full-cover) | Object | Yes | No | Yes |
| Rock pillar (keyword: Full-cover) | Object | Yes | No | No |
| Barricade (keyword: Half-cover) | Object | Yes | Yes | No |

## Maps

### Urban Demo — *built (Scenes/UrbanDemo, Eldoira > Build Urban Demo)*

- **80 × 60 m**, with **180° rotational symmetry**: every feature in the south half has a twin at the opposite point, turned round, so both sides face the same choices.
- **Deployment zones:** the player sets up in the southern **8 m** of the map, the AI in the northern 8 m.
- **Armies:** each side fields **3 Infantry Squads**, a **Field Commander** and a **Battlefield Priest**.
- **Centre:** an open plaza with a fountain (half-cover, blocks movement) and a statue (full cover), ringed by planters and low walls.
- **Each half:** an apartment block (two rooms), a corner shop with a raised loading dock (reached by a slope), a garage and a ruined house, plus streets with cars, sandbags, crates and a shipping container.
- **Buildings** are roofless, with doorways (2 m) and **windows**. A window can be **seen and shot through**, its sill gives **Half-cover**, and soldiers can **climb through** it (mantle: no extra movement cost).
- *Proposed:* cars, sandbags, crates, planters, low walls, rubble and furniture have **Half-cover**. Cars block movement but can be climbed onto. The container and the statue have **Full-cover**.

## Shooting — *partly implemented*

### Penetration vs armor — *implemented*

When shooting, the weapon's **Penetration** is compared with the target's **Armor Level**, and the result scales the attack's **final damage calculation**:

| Penetration compared to Armor Level | Damage | Bonus effects |
|---|---|---|
| Higher (by any amount) | 150% | Apply |
| Equal | 100% | Apply |
| 1 lower | 50% | Apply |
| 2 or more lower | 0% | **Do not apply** |

- Fractions are **rounded down** (50% of 5 = 2; 150% of 5 = 7).

### Accuracy — *implemented*

All hit-chance changes **add or subtract percentage points**, in this order:

1. Start with the soldier's **Accuracy** stat.
2. Subtract all **penalties**.
3. Add all **bonuses**.

Any modifier that **explicitly overrides** accuracy is used instead, and nothing else can modify it.

Example: Accuracy 100% → sprinted (−25%) → 75% → weapon Accuracy Bonus (+20%) → **95%**.

- **Sprint penalty:** a soldier whose unit sprinted this turn takes **−25%**.
- A weapon's Accuracy Bonus counts as a bonus (a negative one counts as a penalty).
- Accuracy **can't go below 0%** (penalties stop at 0% before bonuses are added).
- Accuracy **can go above 100%**. The excess becomes a **crit chance**: at 105% the hit roll is 100% and a separate crit roll is made at 5%. Above 200% nothing more happens (both rolls are 100%).
- If an attack is under both **"always hits"** and **"always misses"**, **always misses** applies. (In general, when overrides conflict the lowest wins.)

### Hit and crit rolls — *implemented*

1. **Hit roll:** d100 against accuracy (up to 100%).
2. If it hits, **crit roll:** d100 against accuracy over 100%.

A miss can never crit.

### Damage order — *implemented*

1. The **final damage calculation**: the weapon's **Base Damage**, minus keyword damage penalties (not below 0), plus keyword damage bonuses. With no keyword interactions it is just Base Damage. Penalties are applied before bonuses, as with accuracy.
2. **Penetration vs armor** multiplier (below), rounded down.
3. **Crit:** adds a bonus equal to **100% of the damage from step 2**.

Example: 6 damage, penetration beats armor → 9, crit → +9 = **18**. If armor blocks the attack (0 damage), a crit adds nothing.

### Other shooting rules — *not yet implemented*

- Cover, line of sight and range.

### Unit actions — *implemented*

Each turn, every unit can be activated once. It **may move**, then — **after moving, or instead of moving** — it takes **one action**:

- **Shoot** at an enemy unit (the shooting sequence below), **or**
- **Use one ability** that one soldier in the unit has.

Taking the action **ends that unit's turn**: no more moving, shooting or abilities. (A unit may also simply move and stop.)

### Shooting order — *implemented*

Under normal circumstances, shooting follows this order:

1. The player **selects a unit** (click one of your units, or Tab). Each unit acts once per turn; you can switch units before the current one starts moving, or after it has finished.
2. The player **may move** the unit.
3. The player **declares** the unit will shoot.
4. The player **selects an enemy unit**.
5. **Line of sight:** the attacking unit must be able to see the defending unit (at least one attacking soldier can see at least one defending soldier). Any **objects in the lines of sight** are noted.
6. **Targets:** every attacking soldier picks the **closest** defending soldier they can target. Several soldiers may pick the same target. If the target has keywords **triggered by being targeted**, shooting pauses, the trigger is resolved, and shooting resumes (so its effects apply to that attack).
7. **Prevented?** If keywords prevent the attack (e.g. a **Toxic** weapon against a target without **Biological**), the soldier **goes back to step 6** with the next-closest soldier, and repeats steps 6–7 through **every enemy soldier** it can shoot until it finds a valid target. If there is none, it **doesn't shoot this turn**. The target picked at the end of step 7 is final.
8. **Damage** is calculated: Base Damage − keyword penalties (not below 0) + keyword bonuses.
9. **Armor penetration** modifies that damage (×1.5 / ×1 / ×0.5 / ×0, rounded down).
10. **Accuracy** is calculated, then the **hit roll**.
11. **Crit roll** if accuracy is over 100%: a crit adds 100% of the post-penetration damage. Otherwise skipped.
12. **Damage is applied.** At 0 Health a soldier dies (and leadership passes on if they led the unit). A soldier who picked a target that has since died **still shoots at it** (the shot does nothing).
13. Keywords **triggered by receiving damage** activate.

Details:

- **Line of sight:** a soldier can see a target if no **structure** (building walls, hill crests, platforms) blocks the line from their eyes to the target's chest or head. **Objects don't block sight** — they give cover through their keywords.
- **"Between"** for cover: an object is between attacker and target if the line between them, seen from above, passes through the object. (This now drives Half-cover and Full cover.)
- A target **can't be picked** if it's out of sight or in full cover behind its cover — unless the weapon ignores line of sight (Indirect fire).
- **Keyword triggers** (on keyword assets): *when Targeted / when Damaged → Self / Attacker gains / loses keyword X* (optionally until the next turn). Coded abilities can hook the same moments.
- **Shooting is the unit's action** and ends its turn. A unit may shoot without moving (its leader then counts as holding position).
- **Primary weapon:** every soldier template designates one weapon as its **primary weapon**, used to shoot under normal conditions (the demo squad's is the Assault Rifle). Circumstances or abilities may make a soldier use an alternative weapon (hook in place, none defined yet).
- **The dead:** a soldier killed during a round of shooting (one attacking unit's volley) stays on the battlefield **until that round of shooting ends** — so soldiers of that unit who already picked them still shoot at them (step 12). When the round ends, the dead are **removed from their unit and the battlefield**: a **second attacking unit can't target them**, even in the same turn. A soldier killed outside shooting is removed at once.
- **"When targeted"** means *when targeted*: a soldier's "when targeted" triggers fire when they are picked in step 6, **even if no attack follows** (e.g. step 7 sends the shooter on to another target). They fire **only once per volley** — one attacking unit's sequence of shooting — however many soldiers from that unit pick them. A **different attacking unit's volley** in the same turn can trigger them again.

## Open questions

- Full cover: how close to the cover must a soldier be to count as concealed (currently 5 m, like half-cover)? Or is it purely a line-of-sight question?
- Indirect fire: do accuracy **overrides** (e.g. "always misses" from being blinded) still apply? (Currently yes.)
- Next to design: weapon range; what Explosive, Incendiary, Sniper and Robotic do.
