# Eldoria — Game Rules

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
| Keywords | Extra properties, e.g. Biological, Robotic. |

| Weapon stat | Meaning |
|---|---|
| Base Damage | Damage dealt by a hit. |
| Penetration | How well it gets through Armor Level. |
| Accuracy Bonus | Added to the soldier's Accuracy. |
| Range | The farthest a target can be (meters, horizontal). None defined = unlimited. |
| Keywords | Extra properties, e.g. Explosive, Incendiary. |

## Units

### Roles — *implemented*

Every unit has a **role**. It's shown in the HUD when the unit is selected.

| Role | Units |
|---|---|
| **Infantry** | Infantry Squad |
| **Commander** | Field Commander, Field Commander in Powered Armor |
| **Heavy Support** | Machine Gun Team, Medium Tank, Tank Hunter Team, Stahl Colossus, Paladin Squad |
| **Specialist** | Powered Armor Squad, Sniper, Flamethrower Team, Battle Mage, Battle Priest (and any unit not given another role) |

### Factions — *implemented*

Every army belongs to a **faction**. The faction a player chooses at the start of building their list decides **which units and commanders** they can take.

| Faction | Colour | Units |
|---|---|---|
| **United States of Liberteria** | dark green | every unit, plus its own **Battle Mage** |
| **United Kingdom of Aelfland** | light green | every unit except other factions' own, plus its own **Paladin Squad** |
| **The Draksa Reich** | gray | every unit except other factions' own, plus its own **Stahl Colossus** |

**Faction colours:** a unit's **primary colour** (its soldiers' bodies and caps) is its faction's colour. Every faction uses the same models and materials: the primary-colour parts are tinted per unit when it deploys, so a new faction only needs a colour, not new textures. Other parts keep their own colours (skin, weapons, gold trim, the officer's cape, the priest's white robe, powered armor plates). Both sides can choose the same faction; the soldier labels (blue for yours, red for the enemy's) still tell them apart. The army builder shows each faction's colour.

**Faction weapons:** a faction can change its soldiers' weapons, without new weapons being made:

- **Stat changes:** each faction can list changes to a weapon's stats: Damage, Shots, Penetration, Accuracy Bonus and Range. They are **added to the weapon's own stats** (negative values subtract), and several changes to the same weapon add up. Example: *Assault Rifle: Damage +1, Range −5 m* makes that faction's Assault Rifles 5 damage, 15 m.
- **Names (aliases):** each faction can give a weapon its **own name**. Example: *Assault Rifle → "Sturmgewehr"*. It is the **same weapon and stat block** underneath (with that faction's stat changes, if any): only the name shown changes, in the army builder, the HUD and the combat log.
- The changes apply to whichever soldier holds the weapon, by **their unit's faction**, including a thrown grenade or an explosion (the faction of the soldier who used it). Armies placed directly in a scene have no faction, so their weapons are unchanged.
- **Limits:** Damage never goes below 0, Shots below 1, Penetration below 0 or Range below 1 m. A weapon with **no penetration value** keeps none, and **unlimited range** stays unlimited.
- The army builder shows each unit's weapons **as the chosen faction has them**.
- Each faction's weapon changes and names:

| Faction | Assault Rifle is called | Changes |
|---|---|---|
| United States of Liberteria | **Semi-auto rifle** | **Shots 2** (+1) |
| United Kingdom of Aelfland | **Bolt-Action Rifle** | — |
| The Draksa Reich | **Bolt-Action Rifle** | — |

### Army lists — *implemented*

Before a battle, the player chooses the **map** (Boxed in or Open Season), then each side chooses a **faction** and builds an **army list** from that faction's units:

- Every unit must be one the army's **faction** has access to.
- An army has **6 slots**: it can have **1 to 6 units** (empty slots are allowed).
- It must contain **exactly 1 Commander** unit: at least one, and **only 1 Commander is allowed** in a list at a time.
- It **can't have duplicates of a Commander or Specialist** unit (e.g. only one Sniper, only one Field Commander; the Field Commander and the Battle Priest together is fine). **Infantry** and **Heavy Support** units can be taken more than once.

**Map choice, then army builder:** pressing **Play** first asks which **battlefield** to fight on (each with a short description); **Back** in the army builder returns to it. Choosing one opens the army builder on the **faction choice**; **Change faction** goes back to it (and empties the list). It then lists every unit the faction can take with an **Add** button and its details: role and size; **stats** for each soldier type (health, armor, accuracy, move, weapons), each **weapon** (damage, penetration, accuracy bonus, range, keywords), its **abilities** and **keywords**, all read from the game data; and a short **description** of how the unit plays and what it does in an army. The Add button is greyed out with the reason when the unit can't be added; your list shows the 6 slots with **Remove** buttons. It starts with the demo army (3 Infantry Squads, the Field Commander and the Battle Priest, keeping only units the faction has), or your last confirmed army if you choose the same faction again. **Random** builds a random valid army; **Clear** empties the list. **Confirm army** checks the list against the rules: if it isn't valid, it says why and you stay in the builder; if it is, you go to the battlefield.

**Weapons and equipment (customizing a unit):** a soldier type can offer **weapon swaps** (a weapon it carries, and what it can be swapped for, e.g. *Assault Rifle → SMG*) and **equipment** it can be given. A unit in your list whose soldiers have any options gets a **Customize** button, which opens a panel listing each of those soldier types with its weapon choices and equipment.
- Choices apply to **every soldier of that type in the unit** (e.g. all the Troopers); the Sergeant is set separately. Each unit in the list has its own choices, even two of the same unit.
- **A swapped weapon replaces the original**; if it was the primary weapon, the new one is the primary.
- **Equipment** can give **keywords**, **abilities**, **extra weapons** (carried as well, not as the primary) and **stat changes** (Health, Armor Level, Accuracy, Movement; added to the soldier's own). The list shows a short summary of a unit's choices.
- Choices are kept with your last army, and applied when your army deploys. The AI's armies don't customize.
- *No soldier type has any options yet: they're set on each soldier's data (Weapon Options, Equipment Options) or given to me.*

**The AI's army:** the AI picks a **random faction** and builds its own **random valid army** from that faction's units each battle under the same rules, always filling all 6 slots: 1 Commander, then random units.

**Deployment:** each army deploys in its zone, **one unit per slot in list order**, left to right as that side looks across the map (the AI's slots are the player's turned 180°). A unit taken more than once is numbered ("Infantry Squad 1", "Infantry Squad 2"). Restarting a battle replays the same armies.

### Infantry Squad

1 **Sergeant** (leader) + 9 **Troopers**. All soldiers are **Biological**.

- **Active ability — Frag Grenade** (every Sergeant and Trooper has it): pick a **spot on the ground within 10 m of the unit leader** (no line of sight needed: it's lobbed over walls). **Every soldier within 2.5 m** of the spot — friend or foe — takes an area attack of **5 damage, penetration 0** (no hit roll; armor and armor keywords count, e.g. vs Armor Level 1 it does 2, rounded down). The explosion **can't go through object or structure terrain** (see Explosions). **Each unit can throw it once per battle**, and throwing it is the unit's action.

| Soldier | Health | Armor Level | Accuracy | Movement | Weapons (primary first) |
|---|---|---|---|---|---|
| Sergeant | 5 | 1 | 75% | 10 m | Assault Rifle, Pistol |
| Trooper | 5 | 1 | 75% | 10 m | Assault Rifle |

| Weapon | Base Damage | Shots | Penetration | Accuracy Bonus | Keywords | Range |
|---|---|---|---|---|---|---|
| Assault Rifle | 4 | 1 | 1 | +0 | — | 20 m |
| Pistol | 2 | 1 | 0 | +0 | Pistol | 10 m |
| SMG | 4 (as the Assault Rifle) | 1 | 0 | +0 | — | 10 m (as the Pistol) |
| Machine Gun | 5 | 5 | 1 | +0 | Deployed | 20 m |
| Flamethrower | 10 | 3 | X (none: always 100%) | +0 | Incendiary, Unstable | 10 m |
| Sniper Rifle | 5 | 1 | 1 | +50 | Deployed | 50 m |
| 100mm Cannon | 10 | 1 | 4 | +0 | Manual Load | 50 m |
| Rocket Launcher | 5 | 1 | 5 | +0 | Manual Load | 50 m |
| Iron Claw | 10 | 1 | 0 | +25 | Melee | 2 m |
| Broadsword | 5 | 1 | 1 | +0 | Melee | 2 m |

- **SMG** and **Machine Gun** are carried by the Machine Gun Team.
- The **Flamethrower** is carried by the Flamethrower Team. The **Sniper Rifle** is carried by the Sniper. The **100mm Cannon** is carried by the Medium Tank. The **Rocket Launcher** is used by the Tank Hunter Team's Rocket Trooper, as an active ability (not in volleys).
- **Incendiary** (weapon keyword): the weapon **only does damage to targets with the Biological keyword**. Against anything else the attack has no effect, so the soldier picks another target.
- **Unstable** (weapon keyword): when the holder of this weapon dies, they have a **10% chance to explode**: **5 damage** (no penetration value, so always 5) to **ANY** soldier within **5 m** of the holder — friend or foe. As an explosion it can't go through object or structure terrain.
- **Melee** (weapon keyword, Trait): the weapon **can be used when its holder is in close range** (like a Pistol).
- **Manual Load** (weapon keyword, Trait): the weapon **can't be fired more than once a turn**, even if the soldier carrying it has a way to shoot more than one volley in a turn. All the shots of the volley it fires in count as that one firing. It's ready again on its holder's next turn.
- **Deployed** (weapon keyword, Trait), unless the holder has an ability that nullifies it (the Stahl Colossus's Stabilizers): the weapon **can't be used in a volley if the soldier holding it has moved this turn** (they have the Moved status: any move this turn, even 0 m; a leader holding position and soldiers left where they stood haven't moved). Each soldier is checked on their own.
- **Using another weapon:** a soldier who can't use their primary weapon right now (Deployed after moving, or close range without a pistol) uses another weapon they carry that they can use; if none, they don't shoot in that volley. The same goes for abilities that use a weapon.

### Machine Gun Team

1 **Sergeant** (leader) + 3 **Troopers** + 1 **Machine Gunner**. All soldiers are **Biological**, with the Infantry Squad's stats. **No Frag Grenade.**

| Soldier | Health | Armor Level | Accuracy | Movement | Weapons (primary first) |
|---|---|---|---|---|---|
| Sergeant | 5 | 1 | 75% | 10 m | Pistol |
| Trooper (×3) | 5 | 1 | 75% | 10 m | SMG, Pistol |
| Machine Gunner | 5 | 1 | 75% | 10 m | Machine Gun |

- The Machine Gun is **Deployed**: the gunner can't fire it in a turn they've moved (and carries no other weapon).

### Powered Armor Squad

1 **Sergeant in Powered Armor** (leader) + 4 **Troopers in Powered Armor**, all with an **Assault Rifle**. All **Biological**. **No Frag Grenade.**

| Soldier | Health | Armor Level | Accuracy | Movement | Weapons |
|---|---|---|---|---|---|
| Sergeant in Powered Armor | 10 | 2 | 75% | 6 m (sprint 12 m) | Assault Rifle |
| Trooper in Powered Armor (×4) | 5 | 2 | 75% | 6 m (sprint 12 m) | Assault Rifle |

### Flamethrower Team

2 **Flamethrower Troopers** (one leads the unit), each with a **Flamethrower** (primary weapon) and a **Pistol**. Trooper stats as in the Infantry Squad: Health 5, Armor Level 1, Accuracy 75%, Movement 10 m (sprint 20 m), **Biological**. **No Frag Grenade.** Role: **Specialist**.

- The Flamethrower only damages **Biological** targets (Incendiary), reaches **10 m**, and its holder has a **10%** chance to **explode** on death (Unstable: 5 damage to everyone within 5 m).
- In **close range** they fall back on their **Pistols**.

### Paladin Squad

1 **Sergeant in Powered Armor** (leader) + 4 **Troopers in Powered Armor**, each holding a **Broadsword**. **United Kingdom of Aelfland only.** Role: **Heavy Support** (more than one can be taken).

| Soldier | Health | Armor Level | Accuracy | Movement | Weapons |
|---|---|---|---|---|---|
| Sergeant in Powered Armor | 10 | 2 | 75% | 6 m (sprint 12 m) | Broadsword |
| Trooper in Powered Armor (×4) | 5 | 2 | 75% | 6 m (sprint 12 m) | Broadsword |

- The soldiers have the Powered Armor Squad's soldiers' stats and keywords (Biological); only the weapon differs.
- **Broadsword:** Damage 5, Penetration 1, Range **2 m**, Accuracy Bonus +0, Shots 1, keyword **Melee**: it **can be used in close range**, so the Paladins keep fighting when an enemy is right beside them. They can only reach enemies within 2 m.

### Stahl Colossus

1 **Stahl Colossus**, a unit of one: a humanoid war robot **twice the size of a person** (3.6 m tall). **The Draksa Reich only.** Role: **Heavy Support** (more than one can be taken).

| Soldier | Health | Armor Level | Accuracy | Movement | Keywords | Weapons (primary first) |
|---|---|---|---|---|---|---|
| Stahl Colossus | 15 | 3 | 75% | 15 m (**can't sprint**) | Solo, Mechanical | Machine Gun, Iron Claw |

- **Iron Claw:** Damage 10, Penetration 0, Range **2 m**, Accuracy Bonus **+25** (100% for the Colossus), Shots 1, keyword **Melee**.
- **Passive ability — Stabilizers:** **nullifies the Deployed keyword** for its weapons, so its **Machine Gun** (5 shots) fires in its volleys **even after it moves**.
- **Active ability — Iron Claw:** instead of a volley, the Colossus can make an **aimed strike** at one enemy soldier **within 2 m** with the Iron Claw (a hit roll at 100%; Damage 10, Penetration 0), **whether or not it moved**. It's the unit's action for the turn. The AI only uses it on targets it can hurt (Armor Level 1 or less).
- **Mechanical:** it can't sprint and is never in close range. It isn't Biological (no healing; Flamethrowers and Firestorm don't affect it).
- Armor Level 3 shrugs off rifles, SMGs, pistols, grenades and the Machine Gun (penetration 0–1). The Rocket Launcher (penetration 5: 150%) and the 100mm Cannon (penetration 4: 150%) hurt it, as do armor-ignoring weapons (Fireball).
- It's wider than a person (0.7 m from its centre), so like the tank it moves on a walkable-area map sized to its body and needs room along its route.

### Battle Mage

1 **Battle Mage**, a unit of one. **United States of Liberteria only.** Role: **Specialist** (one per army).

| Soldier | Health | Armor Level | Accuracy | Movement | Keywords | Weapons |
|---|---|---|---|---|---|---|
| Battle Mage | 5 | 0 | 75% | 20 m (sprint 40 m) | Biological, Solo, Officer, Magic | Pistol |

- **Spell — Firestorm:** choose an **enemy unit within 10 m** (no line of sight needed): **every Biological soldier** in it takes **3 damage**. It **always hits** and **ignores armor**. A unit with no Biological soldiers (e.g. a tank) can't be chosen.
- **Spell — Fireball:** a magic **aimed attack** on one enemy soldier within **20 m** and in sight, with a hit roll at the mage's accuracy: **Damage 10, Penetration X** (ignores armor), Accuracy Bonus +0, Shots 1.
- Both are **Spells**: they **can't be cast after sprinting**, and close range doesn't stop them. Casting one is the unit's action for the turn.
- **The AI** casts Firestorm on the enemy unit in reach with the most Biological soldiers (2 or more), otherwise Fireball at a valuable target (Solo soldiers, sergeants) or the closest, otherwise shoots its pistol.

### Tank Hunter Team

1 **Sergeant** (leader, **Assault Rifle** and **Pistol**) + 3 **Troopers** (**Assault Rifle**) + 1 **Rocket Trooper** (**Assault Rifle**, and the **Rocket Launcher**). All with the Infantry Squad's stats (Health 5, Armor Level 1, Accuracy 75%, Movement 10 m), **Biological**. **No Frag Grenade.** Role: **Heavy Support** (more than one can be taken). Every faction can take it; the Assault Rifle shows under the faction's name for it (Semi-auto rifle, Bolt-Action Rifle).

- **Active ability — Rocket Launcher** (the Rocket Trooper): an **aimed shot at one enemy soldier** within **50 m** and in sight, with a hit roll: **Damage 5, Penetration 5** (Manual Load). Against a Medium Tank (Armor Level 4) a hit does **7**. Firing it is the **unit's action** for the turn (no rifle volley); Manual Load makes it **once a turn**. The rocket isn't fired in the team's volleys.
- **The AI** keeps the rocket for heavily armored targets (Armor Level 2 or more: tanks first, then the heaviest armor, then the closest) and otherwise shoots its rifles.

### Medium Tank

1 **Medium Tank**, a unit of one, armed with a **100mm Cannon**. Role: **Heavy Support** (more than one can be taken). Every faction can take it.

| Soldier | Health | Armor Level | Accuracy | Movement | Keywords | Weapons |
|---|---|---|---|---|---|---|
| Medium Tank | 20 | 4 | 75% | 30 m (**can't sprint**) | Mechanical, Tank, Unstable | 100mm Cannon |

- **Mechanical** (soldier keyword, Trait): soldiers with it **can't sprint** (see Movement) and **can't be in close range** (see Close range). The tank also isn't Biological, so Toxic and Incendiary weapons (the Flamethrower) have no effect on it, it can't be healed by Divine Healing, and it doesn't put enemies in close range either.
- **Tank** (soldier keyword, Trait): a trait for other rules to check.
- **Unstable**: when the tank is destroyed it has a **10% chance to explode**: 5 damage to everyone within 5 m.
- Armor Level 4 means rifles, machine guns, SMGs, pistols and grenades (penetration 0–1) do **no damage** to it; another 100mm Cannon (penetration 4) does full damage.
- The tank is much bigger than a soldier (a 3.4 × 5.6 m hull; it counts as 1.8 m across from its centre). It needs room for its hull **along its whole route**, not just where it stops: see Movement restrictions.

### Sniper

1 **Sniper**, armed with a **Sniper Rifle** — a **solo** unit (a unit of one).

- With Accuracy 100% and the rifle's +50, the Sniper's shots are at **150%**: they always hit, with a **50% crit** chance. The rifle is **Deployed**: no shooting in a turn the Sniper has moved.

- **Sniper Precision** (soldier keyword, Trait): when shooting with this soldier, the player picks a **specific enemy soldier** to shoot instead of an enemy unit. **The sniper makes no decision on who to shoot:** if they can't target the picked soldier (out of range or sight, in full cover, or keywords prevent it) the order is refused (error sound, no action used) and the player picks again. In a unit with other soldiers, the others shoot at the picked soldier's unit as usual.
- **No valid targets:** if, when the unit finishes moving, none of its soldiers has a valid target anywhere, it can't shoot (the Shoot button is greyed out): it can use an ability if it has one, or do nothing. *(This applies to every unit.)*
- **The AI's sniper** prioritizes **valuable targets**: soldiers with the **Solo** keyword or **sergeants** (a sergeant soldier type; a trooper who has taken over command doesn't count) — the closest of those it can target. Otherwise it picks the **closest** soldier it can target. A unit of snipers looks across every enemy unit; a sniper in a mixed unit picks within the unit being shot at.

### Field Commander

1 **Field Commander** (a unit of one).

| Soldier | Health | Armor Level | Accuracy | Movement | Weapons (primary first) |
|---|---|---|---|---|---|
| Field Commander | 10 | 1 | 75% | 10 m | Pistol |

- **Keywords:** Biological (Trait), Officer (Trait), Solo (Trait).
- **Passive ability — Lead from the front:** **friendly** soldiers within **15 m** gain **+15% accuracy**. The **Field Commander does not benefit**. Each soldier is checked **on its own**: being in the same unit as a soldier who has the bonus doesn't give it — so one soldier of a unit might have it, or all but one.

### Field Commander in Powered Armor

1 **Field Commander in Powered Armor** (a unit of one). Every faction can take it. Role: **Commander**.

The **Field Commander** in powered armor: the same soldier (Health 10, Accuracy 75%, Pistol; Biological, Officer, Solo; **Lead from the front**), with **Armor Level 2** and **Movement 6 m** (sprint 12 m).

| Soldier | Health | Armor Level | Accuracy | Movement | Weapons |
|---|---|---|---|---|---|
| Field Commander in Powered Armor | 10 | 2 | 75% | 6 m | Pistol |

- Only 1 Commander is allowed per army, so an army takes either this or the Field Commander, not both.

### Battle Priest

1 **Battle Priest** (a unit of one). Role: **Specialist** (one per army).

| Soldier | Health | Armor Level | Accuracy | Movement | Weapons (primary first) |
|---|---|---|---|---|---|
| Battle Priest | 5 | 0 | 50% | 10 m | Pistol |

- **Keywords:** Biological (Trait), Officer (Trait), Solo (Trait), Magic (Trait).
- **Active ability (Spell) — Divine Healing:** choose a **friendly** soldier within **25 m** who is **Biological** (the priest can choose themself): they restore **5 health** (not above their starting health). The dead can't be healed. A soldier **already at full health can't be chosen** (nor one who isn't Biological): the error sound plays, nothing happens, and the player can pick another target or do something else — the ability isn't used up.
- **Active ability (Spell) — Divine Sanctuary:** choose a **friendly unit** within **25 m** (measured to its closest soldier): **every soldier in that unit** gains **Divine Sanctuary: +1 Armor Level**, until the **start of the next turn**.

### Keywords — *implemented*

Keywords are properties on weapons and soldiers that **interact with each other** in unique ways. Each keyword is an asset (Data/Keywords) listing its interactions. An interaction says:

> **If** the *attacker / weapon / target* **has / lacks** keyword *X* → *effect*

Effects: **no effect** (the attack does nothing — no damage, no bonus effects), **damage bonus/penalty**, **accuracy bonus/penalty**, **always hits**, **always misses**, **can't be targeted**, **ignores line of sight**, **ignores accuracy modifiers**.

- Weapon keywords apply whenever the weapon attacks. A soldier's keyword interactions are marked as applying **when attacking** or **when defending**.
- Example: every living soldier has **Biological**. **Toxic** (weapon): *if the target lacks Biological → no effect*. So a Toxic weapon harms soldiers but does nothing to a tank.
- **Explosive** (weapon) and **Robotic** (soldier) are **trait keywords**: they do nothing by themselves and exist for other rules and keywords to check (e.g. Toxic and Incendiary check for Biological the same way).

#### Keyword categories

Keywords are marked **Trait** (what a soldier or ability is: Biological, Robotic, Officer, Solo, Sniper Precision, Spell) or **Status** (a temporary state: Moved, In half-cover, In full cover, Led from the front).

- Some traits **do nothing on their own**: **Officer** and **Solo** only matter because other weapons and abilities check for them (e.g. something that only affects Officers, or has no effect on Solo soldiers) — done with keyword interactions such as *"if the target has / lacks Officer → …"*.

#### Abilities — *implemented (passive auras)*

- Soldier templates can have **abilities**. A **passive** ability is always in effect.
- A passive **aura** grants a keyword to soldiers within a radius of the soldier who has it (measured horizontally), and that keyword carries the effect. It can affect allies, enemies or everyone, and can include the soldier themself. It's updated whenever anyone moves, a move is undone, and at the start of each turn, and ends when the soldier dies.
- Example: **Lead from the front** → other friendly soldiers within 15 m gain **Led from the front** (+15% accuracy when attacking).

#### Active abilities — *implemented*

- **Abilities can target their user** (or the user's own unit) **unless they say otherwise** — this applies to passive and active abilities. (Lead from the front says otherwise.)
- See **Unit actions** below for when an active ability can be used.
- The target must be the right side (e.g. friendly), within range (horizontal; for a unit, to its closest soldier) and have any required keyword (e.g. Biological).
- Effects so far: **restore health** (up to starting health) and **give a keyword** (to the soldier, or every soldier of the unit), optionally until the next turn; an **area attack** at a spot (Frag Grenade); an **aimed attack** on one soldier (Rocket Launcher, Fireball); and a **unit attack** on every soldier of a unit (Firestorm).
- **Passive abilities can nullify a keyword** for the holder's weapons (e.g. **Stabilizers** nullify **Deployed**).
- **Unit attack** (e.g. Firestorm): the user picks an **enemy unit** within the ability's range (measured to its closest soldier; **no line of sight needed**). Every soldier in it with the required keyword (Firestorm: Biological) is hit by the ability's weapon: it **always hits** (no hit roll) and does its damage after keywords and penetration vs armor (a weapon with no penetration value, like Firestorm's, ignores armor). "When targeted" and "when damaged" triggers fire for each soldier.
- **Spells** (magic) aren't weapons: close range and Manual Load don't apply to them, even when they attack.
- **Aimed attack** (e.g. the Tank Hunter Team's Rocket Launcher): the user shoots one **enemy soldier** with the ability's weapon, going through the shooting steps: the target must be within the **weapon's range**, in **line of sight** and not in **full cover**, and keywords mustn't prevent it; "when targeted" triggers fire; then damage, penetration, the **hit roll** at the user's accuracy (with the sprint penalty if the unit sprinted), a crit roll, damage, "when damaged" triggers. It shoots once per point of the weapon's Shots. The weapon is the user's own (their faction's changes apply), it isn't used in volleys, and **Manual Load** applies to it (once a turn).
- Each soldier can use each active ability once per turn (and in practice a unit uses at most one ability per turn — see Unit actions).
- *Display:* ability buttons don't show uses per turn. A use limit is only shown to the player for abilities limited to a number of uses **per battle** (e.g. Frag Grenade: "1 left this battle").
- **Uses per battle:** some abilities can only be used a number of times per battle **by each unit** (Frag Grenade: once). The count is shared by all the unit's soldiers.
- **Duplicate abilities are condensed:** when several soldiers in a unit have the same ability, the interface shows **one button**, and pressing it uses the ability **once**, as the unit's action. It's used by the unit leader if they have it (ranges are measured from them), otherwise by another soldier who does.
- **Area attacks** (target: a spot on the ground): every soldier within the radius is attacked with the ability's weapon, friend or foe. There's no hit roll; for each soldier, keywords, damage and penetration vs armor apply as for shooting, "when damaged" triggers fire, and anyone killed is removed when the blast is over.

#### Explosions — *implemented*

- **Under normal circumstances an explosion can't go through object terrain or structure terrain.** A soldier in the blast radius is only hit if the blast can reach them:
  - **Object terrain** (crates, low walls, cars, sandbags, window sills...) shields a soldier if the line from the blast to them, **seen from above**, crosses it — whatever its height. The object the blast lands on, and one the soldier stands on, don't count.
  - **Structure terrain** (building walls, hill crests, platforms) shields a soldier if a straight line just above the ground (0.3 m) from the blast to them hits it.
- Cover keywords (In half-cover, In full cover) don't matter for explosions; only the terrain in between does.
- Keywords can now change **Armor Level** for an attack (armor bonus / penalty), which counts in the penetration check.

#### Spells — *implemented*

- Abilities can have keywords that classify them. **Spell** (ability keyword, Trait) marks an ability as a spell.
- **A Spell can't be cast if the soldier sprinted** — meaning the soldier's **unit sprinted this turn** (its leader sprinted), the same test as the sprint accuracy penalty. So every soldier in a sprinting unit is affected.
- Casting a Spell after a normal move, after holding position, or instead of moving is allowed. Undoing a sprint lifts the restriction; it ends at the start of the next turn.
- Abilities that aren't Spells can still be used after sprinting.
- Spells so far: **Divine Healing**, **Divine Sanctuary**. (Lead from the front is not a Spell.)
- *Sound:* when the Battle Priest successfully casts a spell, a **hallelujah** plays once (not when a target is refused).

#### Gaining and losing keywords — *implemented*

- Besides their **inherent** keywords (from their template), soldiers and weapons can **gain and lose keywords** during the game. Losing an inherent keyword only affects that one soldier/weapon.
- A gained keyword can last until removed, or **wear off at the start of a given turn**.
- Gained keywords take part in keyword interactions exactly like inherent ones.
- **A soldier, weapon or object can only have each keyword once.** Gaining a keyword it already has adds nothing (it can only make it last longer). Keywords with the same name count as the same keyword, and repeats are removed from templates. So a soldier near two Half-cover objects is In half-cover once, and takes the −25% once.
- **Moved:** a soldier who moves (including a 0 m move such as climbing down) gains **Moved**; it is removed at the **start of the next turn**. Undoing a move removes it. A leader who holds position doesn't gain it.

#### Terrain keywords and cover — *implemented*

- **Object terrain can have keywords** too, and can gain and lose them like soldiers and weapons.
- A keyword on an object can **grant a keyword to soldiers nearby**: every soldier within its radius (measured **horizontally from the nearest part of the object**, ignoring height) has the granted keyword. This is updated whenever a soldier moves, a move is undone, and at the start of every turn.
- **Half-cover** (object keyword): soldiers within **5 m** gain **In half-cover**.
- **In half-cover** (soldier keyword): an attack against this soldier takes **−25% accuracy** — **only if the object granting it is between the attacker and the target** (see "Between" under line of sight).
- **Full-cover** (object keyword): a soldier is **In full cover** against an attacker whenever a Full-cover object is **between the attacker and them**. **Distance doesn't matter**: how far the cover is from the shooter or from the target is irrelevant. (So it isn't a standing status on the soldier; it's worked out for each attacker.)
- **In full cover**: the soldier is fully concealed and **cannot be targeted by a shooting attack**.
- **Indirect fire** (weapon keyword): the attack **ignores line of sight** — so it can target soldiers **in full cover**, and cover being "between" doesn't matter — and **ignores accuracy modifiers**: it uses the soldier's Accuracy stat with no penalties or bonuses (no sprint penalty, no cover penalty, not even its own Accuracy Bonus). **Accuracy overrides (always hits / always misses) still apply.**
- Interactions can now use the condition **Always** (apply whenever the keyword is involved), e.g. In half-cover's penalty.
- Test room: a **Barricade** (wood, mantleable) with Half-cover sits near the squad's right flank.

## Movement — *implemented*

1. **The squad leader moves first.**
   - **Normal move:** up to their **movement range** (10 m for the demo squad).
   - **Sprint:** up to **double** their movement range (20 m). **Mechanical** soldiers **can't sprint**: they only make a normal move (no sprint ring is shown).
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

### Wading — *implemented*

- **Water** (a riverbed below the water surface) can be walked through, but **every meter moved through water costs double** (2 m of movement per meter).
- Soldiers can stand in water. A **bridge** or a **shallow ford** crosses at normal cost.
- Routes take this into account: when going round by a bridge or ford is cheaper than wading, the route goes that way.
- While placing a move, the readout shows how much of it is wading (e.g. "14.2 m (wading 5.6 m: counts double)").

### Movement restrictions — *implemented*

- Soldiers **may walk through each other**, but may **not end a move overlapping another soldier**.
- Soldiers cannot end a move outside the battlefield.
- **Terrain that cannot be mantled over blocks movement**: building walls, and objects marked as not mantleable. Routes go around them.
- Soldiers can only end a move on walkable ground or **on top of an object that can be climbed onto**.
- **Room for the body:** a soldier wider than a person (e.g. the **Medium Tank**) moves on a walkable-area map built for its size. Its **route must have room for its hull the whole way**: it can't squeeze through a doorway, between buildings or between blocking objects that a soldier could pass, and goes around instead if there's a wide enough way; if there isn't, it can't get there. It can also only stop where its hull fits (not right up against a wall).

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

## Turns and the AI opponent — *implemented*

- **Turn order:** each turn, the **player** activates their units, then presses **End turn**; then the **AI** activates all of its units; then the next turn starts.
- **"Until the start of the next turn" means the caster's own next turn.** Something the player gains on their turn (Divine Sanctuary, Moved...) lasts through the AI's turn and wears off when the player's next turn starts; something the AI gains on its turn lasts through the player's turn and wears off when the AI's next turn starts. For a keyword trigger, it's the next turn of the soldier whose keyword fired.
- The AI's turn is **watchable**, unit by unit: the camera pans to each unit, it moves, then takes its action. **Skip (Space)** resolves the rest at once. The player can't give orders during it.
- **How the AI plays** (it uses the same rules as the player):
  - **Order:** the Field Commander first (so its aura is in place), then the infantry squads, then the other support units.
  - **Moving:** squads push toward the enemy, preferring spots in cover from which they can see enemies; they avoid close range, and avoid sprinting when it isn't worth the accuracy penalty. Support units (Field Commander, Battle Priest) stay about 5 m behind their squads, in cover where they can; the priest never sprints, so it can still cast. The rest of a squad gathers into cohesion around its leader.
  - **Acting:** heal a friendly soldier missing 2+ health if it can; otherwise cast Divine Sanctuary on the friendly unit the most enemies can see; otherwise shoot the enemy unit the most of its soldiers can target (a sniper picks a soldier: Solo soldiers and sergeants first, otherwise the closest); otherwise throw a grenade (once per battle) at the spot hitting the most enemies — never if it would hit its own soldiers.

### Turn timer — *implemented (option)*

- An optional **turn timer** limits how long the player has for each of their turns. It's set on the main menu: **off by default**, **10 minutes** by default (1–30 minutes), and remembered between sessions.
- It counts down only during the player's turn (it pauses while the AI plays) and starts again from the full length at the start of each of their turns. The HUD shows the time left (red in the last minute).
- **When time runs out, the player's turn ends** as if they'd pressed End turn. A unit half-way through its move gathers its stragglers into cohesion and finishes the move; if it can't, its move is undone.

## Multiplayer — *implemented*

Two players can fight each other over the network instead of against the AI.

- **Connecting:** on the main menu, **Multiplayer**, then:
  - **Host:** the game waits for the other player and shows this computer's IP address(es) and the port (**7777**). Tell the other player the address.
  - **Join:** type the host's IP address and press **Connect**.
- **Setting up:** the **host chooses the battlefield**. Then **both players choose a faction and build an army** under the usual army list rules (each player sees only their own builder). When both have confirmed, the battle starts.
- **Sides:** who deploys at the **south** edge and **moves first** is **decided at random** when the battle starts. The other player deploys at the north edge, and their view of the battlefield is turned around so their own army is at the bottom of the screen.
- **Turns:** the same as against the AI: the south player activates all their units and ends their turn, then the north player does, then the next turn starts. The rules (including "until the start of the next turn") are unchanged.
- **The other player's turn:** you watch their orders play out (the camera follows the unit they're commanding, and the log shows their volleys and abilities). You can't give orders until it's your turn.
- **Each player's own turn timer** (the main menu option) applies to their own turns.
- **Winning:** as usual. Each player sees **Victory** or **Defeat** from their own point of view. There's no Restart in multiplayer: **Main menu** leaves the game.
- **Leaving:** if either player leaves or the connection drops, the other player is told (**Opponent disconnected**) and returns to the main menu.
- Debug orders (kill the squad leader) aren't available in multiplayer.

How it works (for development): both games run the battle themselves. The host rolls the dice seed and who goes south, and both games use that seed. Each order the active player gives (select a unit, move, gather, end move, shoot, use an ability, end turn) is sent to the other game, which gives the same order through the same rules and dice. After every order the sending game includes a fingerprint of the battle (turn, every soldier's health and position), and the other game checks its own matches. If they ever disagree, the HUD shows a warning.

## Winning — *implemented*

- **A side with no soldiers left loses**, and the other side wins. If both sides lose their last soldiers at the same moment (e.g. one grenade), it's a **draw**.
- It's checked whenever soldiers are removed from the battlefield: at the end of a volley or blast, or when a soldier dies outside combat.
- When the battle is decided, a **Victory / Defeat / Draw** banner appears, no more orders can be given, and **Restart (R)** starts the battle again (single player only).

## Maps

### Open Season — *built (Scenes/Countryside, Eldoria > Build Open Season)*

- **100 × 100 m**, **not symmetrical**: open, gently rolling English farmland. Its shape is defined in `CountrysideLayout`.
- **Deployment zones:** the player sets up in the southern **10 m** of the map, the AI in the northern 10 m. Both strips are flat.
- **Hills and ridges** (structure terrain; slopes are gradual, never steeper than about 30°). Their crests **block line of sight**, so the high ground matters:
  - a **knoll** in the north-west (about 4 m high);
  - a **long ridge** running across the south-west (about 3 m);
  - a **low rise** in the south field (about 1.5 m).
- **The river** meanders west to east across the middle of the map, about 6 m of water between low banks. **Wading costs double** (see Wading). Two dry crossings:
  - a **stone bridge** (4 m wide) in the east, with low **parapets** (Half-cover; they block movement, so you can't step off the bridge into the water);
  - a **shallow ford** in the west, a gravel bed just above the water.
- **Farm buildings** (roofless, with doorways and climbable windows like the Boxed in's): a stone **farmhouse** in the north-east, a large wooden **barn** with wide doors in the south-east, and a small **stone shed** in the south field.
- **Farm props**, all **Half-cover**:

| Prop | Mantle | Climb onto | Notes |
|---|---|---|---|
| Hay bale | Yes | Yes | 1.2 m tall, in small groups |
| Haystack | No | No | 2.8 m tall |
| Cart | No | Yes | |
| Fence | Yes | No | 1 m tall, along field edges |
| Well | No | No | In the farmyard |
| Bridge parapet | No | No | Along both sides of the bridge |

- When the scene is opened directly in the editor, both sides field the demo army.

### Boxed in — *built (Scenes/UrbanDemo, Eldoria > Build Boxed in)*

- **80 × 60 m**, with **180° rotational symmetry**: every feature in the south half has a twin at the opposite point, turned round, so both sides face the same choices.
- **Deployment zones:** the player sets up in the southern **8 m** of the map, the AI in the northern 8 m.
- **Armies:** chosen in the army builder (see Army lists). With no list chosen (the scene opened directly in the editor), each side fields the demo army: **3 Infantry Squads**, a **Field Commander** and a **Battle Priest**.
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
| **No penetration value** (not a number, or none listed) | **100%, regardless of armor** | Apply |

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

### Weapon range — *implemented*

- A soldier can only target an enemy **within their weapon's range**, measured horizontally (exactly at the range counts). Assault Rifle **20 m**, Pistol **10 m**, SMG **10 m**, Machine Gun **20 m**, Flamethrower **10 m**, Sniper Rifle **50 m**.
- In step 6 soldiers only pick enemies they can target, so enemies out of range are passed over; a soldier with no enemy in range doesn't shoot.
- **Indirect fire** ignores line of sight but **not** range.
- If **none** of a unit's soldiers can target any soldier of the chosen enemy unit (out of range or out of sight), the shoot order is **refused** (error sound) and the unit's action isn't used.
- **If no range is defined for a weapon, it has unlimited range.** (So far only the blast weapons — the Frag Grenade's and the Unstable explosion — have none; how far the grenade can be thrown is the ability's own 10 m.)
- In shoot mode a red ring shows the unit leader's weapon range.
- **Range preview while moving:** while choosing where the unit leader moves, a red ring shows the range of the leader's **primary weapon**, centred on the spot the leader would end up, so you can see what will be in range before committing. It isn't shown for a weapon with unlimited range, or once the leader has moved.

### Close range — *implemented*

- **Mechanical** soldiers **can't be in close range**, whatever is near them.
- A **Biological** soldier is **in close range** if a **Biological enemy** soldier is **less than 2 m** away (horizontally; exactly 2 m is not close range). The dead don't count.
- A soldier in close range **can't use any weapon unless it has the keyword Pistol or Melee**:
  - **Shooting:** they shoot with a Pistol if they carry one (the Sergeant switches from his Assault Rifle to his Pistol); a soldier with no Pistol (e.g. a Trooper) doesn't shoot.
  - **Abilities that use a weapon** (e.g. Frag Grenade) can't be used by a soldier in close range. For a unit's shared ability, another soldier of the unit who isn't in close range uses it instead (ranges are then measured from them); if everyone is in close range, it can't be used.
- **Magic and non-weapon abilities still work** in close range (e.g. Divine Healing). **Passive abilities are unaffected** (e.g. Lead from the front).
- Soldiers in close range show the status keyword **In close range**, and a **red marker** floats over their head.
- **Pistol** (weapon keyword, Trait): usable in close range. The Pistol has it.

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

**Shots:** every weapon has a **Shots** value: in a volley, a soldier shoots that many times. **Every shot runs through steps 6–13**, including picking a target afresh. The volley is resolved in **rounds**: first, everyone whose weapon has **Shots 1 or more** fires one shot (steps 6–13 for all of them); then everyone with **Shots 2 or more** fires a shot; then **3 or more**; and so on, until no one has shots left.
- Each round's picks are made after the previous round has been resolved, so soldiers **killed in an earlier round aren't picked again** (they stay on the battlefield until the volley ends, as usual). If no valid target is left, the soldier doesn't take that shot.
- **Spreading fire** (step 6) counts only the soldiers aiming in the current round.
- A soldier killed during the volley (e.g. by an explosion) doesn't take any more shots.
- "When targeted" triggers still fire **only once per volley**, however many shots pick the soldier.
- Every weapon has **Shots 1**, except the **Flamethrower (3)** and the **Machine Gun (5)**.
6. **Targets:** the attacking soldiers pick one at a time, and **spread their fire**: each picks a valid target (one they can target) that **no other soldier of their unit is aiming at yet**. If every valid target is already taken, they pick one that **only 1** soldier is aiming at; if there's none, one with **2**, and so on. Among equally-aimed-at targets they pick the **closest**. So several soldiers only share a target once every valid target has someone on it. If the target has keywords **triggered by being targeted**, shooting pauses, the trigger is resolved, and shooting resumes (so its effects apply to that attack).
7. **Prevented?** If keywords prevent the attack (e.g. a **Toxic** weapon against a target without **Biological**), the soldier **goes back to step 6** with the next soldier in that order, and repeats steps 6–7 through **every enemy soldier** it can shoot until it finds a valid target. If there is none, it **doesn't shoot this turn**. The target picked at the end of step 7 is final.
8. **Damage** is calculated: Base Damage − keyword penalties (not below 0) + keyword bonuses.
9. **Armor penetration** modifies that damage (×1.5 / ×1 / ×0.5 / ×0, rounded down).
10. **Accuracy** is calculated, then the **hit roll**.
11. **Crit roll** if accuracy is over 100%: a crit adds 100% of the post-penetration damage. Otherwise skipped.
12. **Damage is applied.** At 0 Health a soldier dies (and leadership passes on if they led the unit). A soldier who picked a target that has since died **still shoots at it** (the shot does nothing).
13. Keywords **triggered by receiving damage** activate.

Details:

- **Line of sight:** a soldier can see a target if no **structure** (building walls, hill crests, platforms) blocks the line from their eyes to the target's chest or head. **Objects don't block sight** — they give cover through their keywords.
- **"Between"** for cover: an object is between attacker and target if the line between them, seen from above, passes through the object. (This now drives Half-cover and Full cover.)
- A target **can't be picked** if it's out of sight or a Full-cover object is between it and the shooter (at any distance) — unless the weapon ignores line of sight (Indirect fire).
- **Keyword triggers** (on keyword assets): *when Targeted / when Damaged / when Killed → Self / Attacker gains / loses keyword X* (optionally until the next turn), or *→ explode* (an area attack centred on the soldier). A trigger can have a **chance** (e.g. 10%), rolled each time. The keywords checked are the soldier's own **and those of the weapons they carry**. Coded abilities can hook the same moments.
- **On death:** whenever a soldier is killed — by shooting, a grenade, an explosion or anything else — their "on death" triggers are checked at once. Anyone killed by them (e.g. by an explosion) is removed afterwards, and their own on-death triggers are checked too (chain reactions).
- **Shooting is the unit's action** and ends its turn. A unit may shoot without moving (its leader then counts as holding position).
- **Primary weapon:** every soldier template designates one weapon as its **primary weapon**, used to shoot under normal conditions (the demo squad's is the Assault Rifle). Circumstances can make a soldier use an alternative weapon: so far, **close range** (a Pistol only).
- **The dead:** a soldier killed during a round of shooting (one attacking unit's volley) stays on the battlefield **until that round of shooting ends** — so soldiers of that unit who already picked them still shoot at them (step 12). When the round ends, the dead are **removed from their unit and the battlefield**: a **second attacking unit can't target them**, even in the same turn. A soldier killed outside shooting is removed at once.
- **"When targeted"** means *when targeted*: a soldier's "when targeted" triggers fire when they are picked in step 6, **even if no attack follows** (e.g. step 7 sends the shooter on to another target). They fire **only once per volley** — one attacking unit's sequence of shooting — however many soldiers from that unit pick them. A **different attacking unit's volley** in the same turn can trigger them again.

## Open questions

- *(None right now.)*
