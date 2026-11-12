# Eldoria

Eldoria is a WWII-inspired fantasy turn-based strategy game focused on company-level warfare across a variety of
battlefields. The goal of this project is to offer the tactical freedom of tabletop wargames with the convenience and
tactical minutiae of a computer strategy game.

Like on a tabletop, there's no grid and no hexes: soldiers move, take cover and shoot across an open 3D battlefield,
with every distance measured in meters. The game is built in Unity and is an early proof of concept, focused on
getting the rules right.

## Play it

1. Download [`Eldoria-Windows.zip`](Eldoria-Windows.zip) from this repo.
2. Unzip it anywhere and run `Eldoria/Eldoria.exe` (Windows 64-bit).
3. Pick **Play** on the main menu (or **Multiplayer**, see below). You can turn on the turn timer there too (it's off by default, 10 minutes per turn).
4. **Choose a battlefield:** the Boxed in or the Open Season.
5. **Choose your faction:** the United States of Liberteria, the United Kingdom of Aelfland or the Draksa Reich. Your
   faction decides which units and commanders you can take.
6. **Build your army:** up to 6 units, exactly 1 Commander, and no duplicate Specialist units. Units whose soldiers
   have weapon or equipment options can be customized. Then
   press **Confirm army**.

You play against the AI on one of two maps:

- **Boxed in:** an 80 × 60 m city block with a central plaza, laid out with 180° symmetry so both sides start out even.
- **Open Season:** 100 × 100 m of open, rolling farmland. Hills block line of sight. A river crosses the map:
  wading it costs double movement, unless you take the stone bridge or the ford. A farmhouse, a barn, a stone shed,
  and hay bales, carts and fences give cover.

The AI picks a random faction and builds its own random army under the same rules. You go first. Wipe out the enemy to
win.

### Multiplayer

Two players can play each other over a network. On the main menu, pick **Multiplayer**:

1. One player presses **Host**. The game shows their IP address and the port (**7777**).
2. The other player presses **Join**, types in the host's IP address, and presses **Connect**.
3. The host chooses the battlefield. Then both players choose a faction and build an army, and the battle starts once
   both have confirmed. Who deploys at the south edge and moves first is decided at random.

Things to check:

- **Firewall:** the first time you host, Windows asks whether to let Eldoria through the firewall. Allow it, at least on
  private networks.
- **Same network (LAN):** use the address the host's game shows (usually `192.168.x.x` or `10.x.x.x`).
- **Over the internet:** the host's router must forward **TCP port 7777** to the host's computer, and the other player
  joins the host's *public* IP address. A virtual LAN tool, such as a VPN shared by both players, also works and avoids
  port forwarding.
- Both players need the same version of the game.

## Controls

**Camera**

| Input | Action |
|---|---|
| W A S D / arrow keys | Pan |
| Middle mouse drag | Pan |
| Right mouse drag | Rotate |
| Q / E | Rotate in steps |
| Mouse wheel | Zoom |

**Commanding a unit**

| Input | Action |
|---|---|
| Click a unit / Tab | Select a unit (Tab cycles through them) |
| Click the ground | Move the unit leader, then the rest of the unit |
| H | Leader holds position |
| G | Gather the rest of the unit around the leader |
| Enter | End the move |
| Backspace | Undo the move |
| F | Shoot: then click an enemy soldier |
| Esc | Back out: stop aiming, or deselect a unit that hasn't moved |
| N | End your turn |
| Space | Skip ahead while the AI is playing |
| R | Play again once the battle is over |

Abilities (grenades, spells and so on) have buttons on the HUD when the selected unit has them.

## The rules

All the game rules live in [`Docs/Rules.md`](Docs/Rules.md): movement and cohesion, terrain and cover, line of sight,
accuracy and crits, armor penetration, keywords, abilities, spells, turns and winning. Each section says whether it's
implemented.

At a glance:

- **Units, not individual soldiers.** A unit's leader moves first (normal move, or sprint for double), and everyone else
  must end within 5 m of them. If the leader dies, another soldier *in the same unit* takes over.
- **Shooting is the unit's action** and ends its turn. Soldiers spread their fire across the target unit: each picks
  an enemy nobody else is aiming at if it can (the closest first), and only doubles up once every target is covered. The dead are removed when the volley ends.
- **Cover matters.** Half-cover costs the shooter 25% accuracy. A Full-cover object between shooter and target blocks the
  shot entirely.
- **Keywords drive the interactions**, such as Biological, Toxic, Incendiary, Deployed, Solo, Spell and Sniper Precision.

### Units

| Unit | Role |
|---|---|
| Field Commander | Commander |
| Field Commander in Powered Armor | Commander |
| Battle Priest | Specialist |
| Infantry Squad | Infantry |
| Machine Gun Team | Heavy Support |
| Flamethrower Team | Specialist |
| Medium Tank | Heavy Support |
| Tank Hunter Team | Heavy Support |
| Battle Mage (United States of Liberteria only) | Specialist |
| Stahl Colossus (Draksa Reich only) | Heavy Support |
| Paladin Squad (United Kingdom of Aelfland only) | Heavy Support |
| Powered Armor Squad | Specialist |
| Sniper | Specialist |

Every faction can take every unit except another faction's own (so far, Liberteria's Battle Mage, Aelfland's Paladin Squad and the Draksa Reich's Stahl Colossus). An army has exactly 1 Commander, and Specialist units are limited to one of each.

Weapons: Assault Rifle, Pistol, SMG, Machine Gun, Flamethrower, Sniper Rifle, plus the Frag Grenade ability.

## Working on the project

**Requirements:** Unity **6000.3.1f1** (Unity 6.3) with the Universal Render Pipeline and the Input System package. Open
the repo folder in Unity Hub. (The local folder may be named `Eldoira`; the project is Eldoria.)

**Layout**

```
Assets/_Project/
  Scripts/
    Rules/        the rules engine: shooting, attacks, keywords, abilities, turns, victory
    Units/        templates (units, soldiers, weapons, keywords, abilities) and runtime soldiers/units
    Terrain/      terrain objects, cover, line of sight, movement measuring
    AI/           the AI planner
    Gameplay/     player input, HUD, AI turns, audio, menus
    CameraControl/
    Core/         battlefield scale and the shared random number generator
  Data/           ScriptableObject assets: Units, Soldiers, Weapons, Keywords, Abilities
  Editor/         builders for data, maps, scenes and the Windows build
  Scenes/         MainMenu, UrbanDemo, Countryside, CubeRoom (test room)
  Tests/          EditMode and PlayMode tests
Docs/Rules.md     the rules
```

**Editor menu (Eldoria):** Build Boxed in, Build Open Season, Build Main Menu, Build Cube Room, Spawn Test Unit, Build Game (Windows).
Game data is created and upgraded in code by `SquadBuilder`, so the assets can be rebuilt at any time.

**Tests:** run them in Unity's Test Runner, or from the command line:

```bash
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults edit.xml
```

```bash
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults play.xml
```

**Windows build:** use **Eldoria > Build Game (Windows)**, or run it in batch mode:

```bash
Unity.exe -batchmode -quit -projectPath . -executeMethod Eldoria.EditorTools.GameBuilder.BuildWindows
```

The game is written to `Builds/Eldoria/`. Run `Eldoria.exe -smokeTest` to have it play a few AI turns on its own and
report whether everything loaded.

To test multiplayer, run two copies on one computer, starting the host first. They connect over `127.0.0.1`, play three
turns against each other, and check that both games agree on the battle after every order:

```bash
Eldoria.exe -batchmode -nographics -mpSmoke host -logFile host.log
```

```bash
Eldoria.exe -batchmode -nographics -mpSmoke join -logFile join.log
```

## Status

Early proof of concept, with army list building. Next up: more units and factions.

## Audience and business model

Eldoria is aimed at players aged 16 to 40, mostly male, who play strategy games competitively. The pitch to them:
*"With this game you can play more matches of our strategy game in a night than you could play physically in a year."*

There are three planned revenue streams:

1. **New commanders and factions.** Commanders bring exclusive units, and new factions are released periodically to
   refresh the competitive environment.
2. **Cosmetic items**, so players can customize their army to their liking.
3. **Seasonal battle passes** containing customization items.
