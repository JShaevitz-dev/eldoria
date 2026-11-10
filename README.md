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
3. Pick **Play** on the main menu. You can turn on the turn timer there too (it's off by default, 10 minutes per turn).

You play against the AI on the **Urban Demo** map: an 80 × 60 m city block with a central plaza, laid out with 180°
symmetry so both sides start out even. Each side has three Infantry Squads, a Field Commander and a Battlefield Priest.
You go first. Wipe out the enemy to win.

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

| Unit | In the demo? |
|---|---|
| Infantry Squad | Yes |
| Field Commander | Yes |
| Battlefield Priest | Yes |
| Machine Gun Team | Not yet |
| Powered Armor Squad | Not yet |
| Sniper | Not yet |

Units outside the demo are in the game data and waiting for a list-building system.

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
  Scenes/         MainMenu, UrbanDemo, CubeRoom (test room)
  Tests/          EditMode and PlayMode tests
Docs/Rules.md     the rules
```

**Editor menu (Eldoria):** Build Urban Demo, Build Main Menu, Build Cube Room, Spawn Test Unit, Build Game (Windows).
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

## Status

Early proof of concept. Next up: more units, then a list-building system for choosing armies.

## Audience and business model

Eldoria is aimed at players aged 16 to 40, mostly male, who play strategy games competitively. The pitch to them:
*"With this game you can play more matches of our strategy game in a night than you could play physically in a year."*

There are three planned revenue streams:

1. **New commanders and factions.** Commanders bring exclusive units, and new factions are released periodically to
   refresh the competitive environment.
2. **Cosmetic items**, so players can customize their army to their liking.
3. **Seasonal battle passes** containing customization items.
