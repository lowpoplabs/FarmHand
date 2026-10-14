# Farm Hand

*Hire a hand to keep the farm running while you are away.*

An [Oxide/uMod](https://umod.org/) plugin for **Rust**. Players hire a farm hand for their
base: a worker who is posted inside their tool cupboard's range, paid by the day, who
harvests, replants, waters and fertilises the planters while they are away.

> **Status: 0.21.2.** Every stage of the plan is in and running on a live server: hiring,
> crops, the composter, chickens, bees, livestock, the biofuel generator, the kitchen, water
> runs to the river and deliveries to the Farmers Market. The newest parts (the kitchen's
> pies and teas, buying livestock) have had little play yet; please report anything odd.

The wage is paid at the [Public Works](https://github.com/lowpoplabs/PublicWorks) office
when that plugin is installed, so the island keeps a single payment desk. Without it, the
plugin takes scrap by command and works on its own.

## How to use (in-game)

1. Stand in your base where the farm hand should wait, inside the range of a tool cupboard
   you are authorised on, and run `/farmhand hire`. The first day's wage is taken in scrap.
   `/farmhand hire planter`, `stockman`, `yard` or `carter` hires one that sees to only
   that part of the farm; up to four hands per farm, and a farm at every base you own.
2. Show it the boxes: press the use key on the hand and open the SETUP tab, which lists
   every container in reach with a button for each use, or SET UP FOR ME. Or look at a
   box (within 5 m) and run:
   - `/farmhand output` — harvest goes here. Mark several; a fridge is filled first, so
     food keeps and cloth, flowers and plant fibre go to the plain box. Run it again on a
     box to unmark it.
   - `/farmhand supply` — seeds, clones and fertiliser are taken from here.
   - `/farmhand water` — a water barrel, jug or catcher it waters from (fresh water only).
   - `/farmhand compost` — a composter. Plant fibre and spoiled food go in; fertiliser
     comes out and is used in the planters before anything in the supply box.
   - `/farmhand fuel` — optional: a box whose contents are also fed to the biofuel
     generator (it gets its share of dung without one). Run it again to unmark it.
3. Press the **use** key on the farm hand to open its panel: switch duties on and off, set
   how many chickens, cows and sheep to keep and whether surplus is butchered, and for
   each crop choose whether to plant it, whether it may be fed to the animals, how many
   seeds to keep in the supply box, and the planter water band ("water when below" and
   "fill to").
4. Pay further days at the Public Works office: open **MY ACCOUNTS** and press **+1 DAY**
   on the *Farm hand* row (the office phone line works too). On a server without Public
   Works, run `/farmhand pay [days]`.
5. Keep the wage paid. When it runs out the farm hand stops work, waits for the grace
   period, and then leaves.

The farm hand does one job every few seconds: it picks ripe plants into the output boxes,
clears dying ones, waters planters that fall below their band, plants empty slots from the
supply box, keeps a fertiliser in each planter that has plants, and feeds the composter
with plant fibre and spoiled food. Chicken coops are fed and watered from the same boxes,
the chickens petted, the eggs collected and hatched to keep the flock at the number you
set, beehives are emptied of honeycomb, hitching troughs are kept in feed and water, tame
cows are milked and tame sheep sheared once the animals trust the farm hand (five minutes
of its company, as for a player), the dung around them goes to the composter, and a biofuel generator is fed from the fuel
box, stirred when it needs it and emptied of fuel. When the water container runs low it goes to
the nearest river or lake and comes back with a load. When the boxes hold surplus produce
and the [Farmers Market](https://github.com/lowpoplabs/FarmersMarket) is installed, it takes
the surplus to market, brings the scrap back to your boxes and can buy seeds there. With
vendor runs on, wool and surplus animals go to the game's livestock vendor instead, for
whatever it offers. It only touches planters, coops,
hives and boxes inside its own cupboard's range, within 30 m of its post. While its owner
is near it walks to each job and back along the game's navmesh, opening and closing the
owner's doors on the way; otherwise it works in place and never touches a door.

## Installation

1. Copy `FarmHand.cs` into your server's `oxide/plugins/` (or `carbon/plugins/`) folder.
2. The plugin compiles and loads automatically and writes `oxide/config/FarmHand.json`.

## Commands

| Command | Who | What it does |
| --- | --- | --- |
| `/farmhand` | everyone | Your farms, their hands, and how long the wages are paid for |
| `/farmhand hire [role]` | everyone | Hire a hand (farm hand, planter, stockman, yard, carter) and post it where you stand |
| `/farmhand post [role]` | everyone | Post a hand that is on your books where you stand |
| `/farmhand move` | everyone | Move the nearest of your hands to where you stand (same or another farm) |
| `/farmhand dismiss [role]` | everyone | Send the nearest of your hands away; paid wages stay on your account |
| `/farmhand output` | everyone | Look at a box: harvest goes here (again to unmark; mark several) |
| `/farmhand supply` | everyone | Look at a box: seeds, clones and fertiliser come from here |
| `/farmhand water` | everyone | Look at a water barrel, jug or catcher: water comes from here |
| `/farmhand compost` | everyone | Look at a composter: waste goes in, fertiliser comes out |
| `/farmhand fuel` | everyone | Look at a box: its contents are burned in the biofuel generator (again to unmark) |
| `/farmhand boxes` | everyone | Which boxes are set |
| `/farmhand pay [days]` | everyone | Pay wages in scrap (only when Public Works is not loaded) |
| `/farmhand list` | admin | List the posted farm hands |
| `/farmhand grant <player> <days>` | admin | Add wages to a player's account for free |
| `/farmhand remove <player>` | admin | Remove a player's farm hand |

## Permissions

- `farmhand.admin` — the admin subcommands above.
- `farmhand.use` — hiring a farm hand, only checked when
  `Require permission to hire a farm hand` is on.

```bash
oxide.grant group admin farmhand.admin
```

## Configuration

`oxide/config/FarmHand.json`:

```json
{
  "Wage per day (scrap)": 100,
  "Maximum days of wages a player can hold in advance": 7,
  "Grace period after the wage runs out (days the farm hand waits, without working, before it leaves)": 2.0,
  "Maximum farm hands posted on the server at once": 20,
  "Require permission to hire a farm hand (farmhand.use)": false,
  "Take wages in scrap by command when PublicWorks is not loaded (/farmhand pay)": true,
  "Farm hand display name": "Farm Hand",
  "Farm hand NPC prefab path": "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_cinematic.prefab",
  "Farm hand NPC fallback prefab path": "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roamtethered.prefab",
  "Farm hand outfit (item shortnames, optional @skinId)": [
    "hat.boonie",
    "burlap.shirt",
    "burlap.trousers",
    "burlap.gloves",
    "shoes.boots"
  ],
  "Farm hand turns to face players within this range (meters, 0 = disabled)": 5.0,
  "Reach when using the farm hand (meters)": 3.0,
  "Seconds between jobs (one harvest, planting or watering per job)": 15.0,
  "Farm radius: how far from its post the farm hand looks for planters and boxes (meters)": 30.0,
  "Default planter water level below which the farm hand waters (%)": 40,
  "Default planter water level the farm hand fills to (%)": 70,
  "Walk to each job when a player is within this range of the post (meters, 0 = never walk, work in place)": 40.0,
  "Walking speed (meters per second)": 1.8,
  "Water fetched per trip to the river (ml)": 10000,
  "Farthest river or lake the farm hand will walk to for water (meters)": 400.0,
  "Fetch water when the container is below this share of full (0-1)": 0.25,
  "Default number of seeds and clones to keep per crop in the supply box (eats produce or takes clones to make up the shortfall; 0 = don't)": 10,
  "Go to the Farmers Market only when the load is worth at least this much scrap": 50,
  "Farthest Farmers Market stall the farm hand will walk to (meters)": 3000.0,
  "Seconds spent trading at the market per run": 60.0,
  "Farthest livestock vendor (Ranch, Large Barn) the farm hand will walk to (meters)": 5000.0
}
```

Notes:

- **A farm is a tool cupboard**, with up to four hands; a player may own several farms.
  Farms are checked once a minute: if the cupboard is gone, or the owner is no longer
  authorised on it, the hands go back on the owner's books, to be posted again.
- **Wages are one bill per owner**: the daily wage times the hands on the books. Wages
  already paid are kept when a hand is dismissed, unposted or loses its cupboard.
- **A wage of 0** makes farm hands free; no bill is shown at the office.
- **The farm hand can't be hurt** and is ignored by turrets and other NPCs, including
  the custom NPCs of NpcSpawn / BetterNpc (answered through their own `OnCustomNpcTarget`).
- **Map wipes** unpost every farm hand; paid wages carry over.
- **Changing boxes:** supply, water and compost are single roles, so marking another box
  replaces the old one. Output and fuel toggle: run the command on a box again to unmark
  it. A box that is destroyed or picked up is forgotten and you are told; one you put back
  down is a new box and must be marked again.
- **Harvest is only taken when an output box has room for it.** With only a fridge marked,
  only food is harvested, because a fridge refuses everything else.
- **Seed stock:** when a planted crop's seeds and clones in the supply box fall below the
  number kept, one of its produce is eaten for the seed (one fruit, one seed), or a ripe
  plant is cloned when nothing can be eaten for it (hemp, flowers). Set the number to 0 to
  use only what you supply.
- **Feed:** animals are fed first from the harvest, only crops with their feed switch on
  and spoiled produce, then from the supply box (bread the farm hand baked, feed you
  bought). With a cooking workbench in the base it bakes bread, or brioche, from your
  wheat, eggs and cream to keep loaves in the supply box, skimming milk that has aged in
  the fridge for the cream.
- **Water levels** are per crop; a planter with mixed crops is watered when it falls below
  the highest "water when below" among them, up to the highest "fill to". The defaults
  match the band a sprinkler keeps.
- **Work rate:** one job per hand every 15 seconds by default, shared tick, no per-frame
  work. Each hand only takes the jobs of its role, so four hands are four jobs a tick.
- **Order of work:** what it carries, animals with nothing to eat or drink, harvest, dry
  planters and a market or vendor run that is due always come first. The rest (the
  animals' routine, replanting and fertilising, seeds, water runs, baking, muck,
  butchering) is arranged on the panel's HAND tab, along with the seconds between jobs
  for that hand.
- **Doors:** the farm hand marks the owner's doors inside the cupboard as openable by NPCs
  in the navmesh (a door property the game saves; nobody else's doors are touched) and
  opens them only while walking with the owner near, shutting each once past.
- **Water runs** are simulated once the farm hand is out of sight: it walks to the edge of
  the farm, vanishes for the time the round trip would take at walking pace, and reappears
  at its post with the water. It does not cross the island on foot. Needs a river or lake
  (not the sea) within range.

## How storage works

Hires, wages, box choices and crop settings live in `oxide/data/FarmHand.json`. The file is written **with the server's
world save** and when the plugin unloads, not on every change, so a crash rolls farm hands
and player inventories back to the same save. The NPC itself is spawned unsaved and removed
on unload; nothing is left in the world save. If the data file can't be read at load, farm
hands stay off and nothing is written until it is repaired.

Every job uses the game's own code: harvesting calls the same routine a player's pick does,
planting spawns the seed's own plant on one of the planter's slots as the deploy code does
(a clone keeps its genes), and watering is the same splash a sprinkler delivers. Nothing is
simulated with fake input.

## Farmers Market integration

With Farmers Market loaded and a stall placed, the farm hand sells the surplus in the
output boxes (everything a stall buys beyond the amount kept at home) through the market's
`SellProduce` call, so the market's prices, falling prices and the owner's daily limit all
apply exactly as if the owner had walked there. It goes at most once per the hours set on
the MARKET tab and only for a load worth the configured minimum; the scrap comes back with
it. With "buy seeds" on, crops short of seeds are restocked from the stall at its price
out of that run's takings.

## Public Works integration

With Public Works 2.11.0 or later loaded, Farm Hand registers a bill with the office. A
player who has hired a farm hand sees a *Farm hand* row on the MY ACCOUNTS page showing the
time left; the button buys one more day, up to the configured maximum in advance.
`/farmhand pay` is switched off while the office is available.

## Support

Provided as-is. Bug reports welcome via GitHub Issues. No Discord, no custom work, no promises on turnaround. If it saved you time or you and your players enjoy it:

[![Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/lowpoplabs)

## License

[MIT](LICENSE)
