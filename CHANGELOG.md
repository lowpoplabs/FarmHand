# Changelog

All notable changes to the Farm Hand plugin.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.22.0] — Unreleased

Stock caps and a takings box. Deployed to a live server; not yet seen in game.

### Added
- **Stock caps** — on the Market tab, "Stop producing anything the boxes hold more than"
  (0 = never, the default) and, per produce, a "stop producing at" number beside "kept at
  home". Once the boxes hold that much of a produce the farm stops making more of it: cows
  are not milked, sheep not sheared, eggs and honeycomb not collected, ripe plants of that
  crop are left on the plant and the crop is not replanted, until some has gone (sold,
  eaten, fed, baked). Skimmed milk counts as milk.
- **Takings box** — an optional box role (`/farmhand takings`, or TAKINGS on the Setup tab):
  scrap from the market and whatever the livestock vendor pays go there instead of the
  output boxes, so the vendor's metal and fuel stop crowding the harvest.

### Changed
- **Milking in rounds** — every cow that is ready is milked in one go, with rounds no closer
  than five minutes (config), so a round's milk is created together and stacks. The game
  refuses to stack milk whose fridge time differs by more than three minutes, and one cow
  a tick had left a small stack per cow filling the fridge.
- Selling and skimming take the smallest stacks first, so fragments are used up rather
  than made.
- The streak warning in the log counts the same *job* repeating, not the same group: a
  stockman only ever does the animals group, so the old count warned about honest work.

## [0.21.2] — 2026-10-13

First public release on GitHub. Everything from 0.1.0 up is in the entries below.

### Fixed
- **Custom NPCs from NpcSpawn (BetterNpc and friends) no longer attack the farm hand.**
  NpcSpawn picks targets with its own scan and asks its own `OnCustomNpcTarget` hook
  instead of Oxide's `OnNpcTarget`, so the existing guard did nothing against it. The farm
  hand now answers that hook too, and is never a target. Listened to only while a hand
  stands, like the other hooks.

## [0.21.1] — 2026-10-11

### Changed
- **The animals come first in the planters and the kitchen.** The planter spreads the
  planted crops evenly by plants in the ground, which, once the pie crops were on, left
  the feed crop a quarter of the slots and the mangers empty. Crops marked as feed now
  get twice the plants of the rest, and the kitchen only takes a feed crop above its
  keep-at-home number (the MARKET tab's keep rows), so a pumpkin marked as feed goes to
  the trough before the pie. The YARD tab's shortage lines say how much is kept for feed.

## [0.21.0] — 2026-10-10

A kitchen with a focus. Deployed to a live server; not yet seen in game.

### Added
- **The kitchen's focus** (YARD tab): **Bread**, loaves into the supply box as feed, as
  before; **Pies**, the farm's own pumpkin, beef, mutton and chicken pies (rich with
  cream) baked until the output boxes hold the number kept; **Teas**, the teas you tick
  brewed from red, green, yellow, white and blue berries until the boxes hold the number
  kept of each, with the advanced teas (four of a tea) if you ask.
- **The barbecue**, under Pies: the cull's beef, mutton and chicken from the fridge are
  cooked on any barbecue or campfire inside the farm, with wood from the supply box; the
  cooked meat (and anything burnt) comes out as soon as it is seen, raw meat goes in
  only while nothing cooked waits, and a stock of nine of each cooked meat is kept.
- **PLANT FOR ...** on the CROPS tab sets the plant switches to the focus's crops in one
  press (wheat; wheat, corn, pumpkin and potato; the five tea berries), leaving crops
  marked as feed planted. A preset, not a lock. Black berries are in no tea and are not
  planted for Teas.
- The cooking workbench's own grill counts as the barbecue (it is a child oven of the
  table, which the farm scan did not see), so a workbench alone is a whole kitchen.
- The YARD tab says when there is no cooking workbench, or no barbecue for the pies.

## [0.20.0] — 2026-10-10

Buying livestock. Deployed to a live server; not yet seen in game.

### Changed
- The use key opens a hand's panel from 2 m (was 3) and only when looking at it
  squarely, so a hand standing behind a box no longer answers for the box.

### Added
- **Buy calves and lambs** (VENDOR tab, off by default). The livestock vendor sells
  calves for 300 scrap and lambs for 150 (its own prices; in the config). When a
  species is below the number kept on the Animals tab, young and animals already on
  the way counted, the vendor run buys the shortfall, up to the number per run (2):
  a female first where there is none, then a male where there is none, then females.
  The scrap comes from the output boxes; a run also goes for a purchase alone. The
  animals arrive with the farm hand, by the trough, trusting the owner and the hand at
  the lead threshold (as the vendor's own sale does) and with the farm as their home.
  The VENDOR tab says what the next run will buy and whether the scrap is there.

## [0.19.2] — 2026-10-10

### Changed
- **The strike-through moved** from the rail to the HAND tab: the steps of the order of
  work that are not this hand's job are struck through there, and the rail is plain.
  The banner at the top of each farm tab still says whose job it is.
- **A VENDOR tab** of its own (the vendor's whereabouts, what a run could take, the
  switches), so the MARKET tab is the market and the keep-at-home rows only.
- The keep-at-home rows no longer list skimmed milk or cream, which follow the milk.
- The server log records every hire, post and dismissal.

- **`/farmhand menu`** opens the panel from anywhere: on the nearest of your hands, else
  one at the farm you stand in, else any you have. On the panel, the rail lists the
  farm's hands by name under the tabs; pressing one switches to it.
- The panel opens on a hand that is away on a trip too (its NPC is not in the world
  then), so the rail's hand buttons and `/farmhand menu` always work.
- **The ANIMALS tab warns** when a manger or coop is empty and there is nothing to fill
  it with (no crop marked as feed, no feed or loaves in the supply box).

### Fixed
- The − and + of the keep-at-home and crop rows were drawn too large for their rows
  and clipped to nothing.
- With two hands that cart (a carter beside a farm hand of every duty), both could plan
  the same market or vendor run before the first had left; a trip is now claimed for the
  farm for two minutes when a hand takes it.

## [0.19.1] — 2026-10-10

Telling the hands apart. Deployed to a live server; not yet seen in game.

### Added
- **Names.** Every hand gets a short name at the hire (Tom, Bess, Maud...), different
  from the owner's other hands while the list lasts. The NPC's name over its head reads
  "Tom the stockman", the panel title "TOM · STOCKMAN · FARM IN D7", and messages and
  the status command name them.
- **An outfit per role** in the config (planter, stockman, yard, carter), each with its
  own hat by default; the plain farm hand wears the outfit as before. Changing a hand's
  role re-dresses and renames it on the spot.
- **Farm tabs say what they are.** Every tab but HAND opens with "Farm settings, shared
  by the N hands here", or, when the open hand's role does not cover that tab, "Not
  Tom's job: Bess sees to this". Those tabs are struck through on the rail.
- **Hands on this farm** on the SETUP tab: one line per hand with its name, role, what
  it covers and whether it is away.

## [0.19.0] — 2026-10-09

Farms by cupboard, several hands per farm, and a panel regrouped around a Setup tab.
Deployed to a live server; not yet seen in game.

### Added
- **Several hands per farm, each with a role.** `/farmhand hire [role]` hires a *farm
  hand* (everything), a *planter* (planters, seeds, water runs), a *stockman* (coops,
  hives, livestock, the herd, the knife), a *yard hand* (composter, generator, dung,
  baking) or a *carter* (market and vendor runs); up to four per farm (config). A hand
  only takes the jobs of its role, so four hands are four jobs a tick. The role can be
  changed on the hand's panel.
- **A farm at every base.** A farm is a tool cupboard you are authorised on, with its
  own boxes, settings and hands; you may have several. Commands act on the farm you
  stand in, the panel on the hand you pressed. `/farmhand` lists your farms.
- **Hands on the books.** Hired away from a base (or unposted by a wipe, a lost cupboard
  or unpaid wages) a hand waits on your account until `/farmhand post` where you stand.
  `/farmhand move` and `/farmhand dismiss` act on the nearest of your hands.
- **One bill for the crew.** Wages are per owner: the daily wage times the hands on your
  books, paid at the office or with `/farmhand pay`; one lapse stops every hand.
- **Setup tab**: the farm's figures, its boxes, and every container in reach with buttons
  to make it the harvest box, the supply box, the water, the compost or the fuel box,
  plus SET UP FOR ME, which picks the fridges and nearest box for the harvest, the next
  box for supplies, and the nearest water and composter.

### Changed
- **The panel is a rail of seven tabs** down the left: HAND (wages, role, pace, the
  order of work, post here, dismiss), SETUP, CROPS (plant duties and the crop table),
  ANIMALS, HERD, YARD (composter, generator, dung, water runs, baking) and MARKET
  (market and vendor runs, keep-at-home rows). The Duties and Priority tabs are gone
  into these; tabs a hand's role does not cover are dimmed.
- Data file: `Hands` keyed by owner becomes `Accounts` (wages, notes, unposted hands)
  and `Farms` keyed by cupboard. Read once and moved on load; the 0.18 hand becomes a
  farm hand of every duty at its cupboard.

## [0.18.0] — 2026-10-08

Breeding by the genes, and a Herd tab. Deployed to a live server; not yet seen in game.

### Added
- **Breed by the genes** (Animals tab, off by default). The game gives every cow and
  sheep two copies of five genes (dung, longevity, yield, fertility, hardiness), each
  Bad, Ok or Good, with the better copy showing and the other carried; and two lineage
  markers, one from each parent, which make the animal inbred when they match and
  halve every gene. Breeding itself is the game's own, so the farm hand's lever is who
  stays. With the duty on it keeps **one bull** per species, the one sharing a line with
  the fewest cows kept and then the best genes, and swaps him the moment a better or
  less related male grows up; keeps the **best cows** by breeding value (Good copies less
  Bad, carried ones included) up to the number kept; sends the **worst to the vendor**
  first; and marks the **inbred for the knife** (the butcher switch must be on for that;
  with vendor runs off, the surplus goes to the knife too, and with butchering off the
  inbred go to the vendor, so no animal is stranded). Young, carrying and untamed
  animals are left alone.
- **Herd tab** says when breeding by the genes is off (the plain rule applies, whatever
  the genes) and when butchering is off (the inbred go with the rest), and marks a
  surplus animal "spare" when neither the vendor nor the butcher is on to take it.
- **Herd tab**: every cow and sheep inside the farm with sex and age, its five genes as
  letter pairs (shown, carried), its line markers or INBRED, and the farm hand's plan
  for it (the bull, keep, in calf, growing, to the vendor, for the pot, not tamed).
  With breeding on, a line per species names the bull and how many of the kept cows
  share his line, and warns when fresh blood is needed. The game hides genes behind an
  admin convar; the farm hand reads them as a stockman would.

### Changed
- The surplus animal to go is now the worst by what it yields, not the first found.

### Fixed
- **The vendor refused every cow** ("does not trust the farm hand enough to be led"),
  and the bucket worked only by turns. Trust short of the game's full bond fades
  whenever nobody has credited the animal for a few seconds, and the farm hand stopped
  crediting at the lead threshold, so its trust hovered just under the line. It now
  keeps crediting every tick to the full bond, which never fades. Calves are born with
  their mother's trust at exactly the lead threshold and faded below it unattended,
  after which the farm hand treated them as wild; animals whose home is the owner's
  cupboard are now befriended too.


A Market tab with a keep-at-home number per produce, and two fixes that had stopped the
farm hand going anywhere. Deployed to a live server; not yet seen in game.

### Added
- **Market tab** — the market run, its hours, seed buying and the general "keep at home"
  number have moved here from Duties, and below them is a row for each produce the stalls
  buy that the boxes hold, with its own keep number (× goes back to the general one). So
  the wheat can stay while the milk goes.
- **Jobs since posting** on the Farm tab: a count per step of the day's work (hungry,
  harvest, dry, then the Priority groups), to show at a glance what the farm hand's time
  goes on.
- **For the vendor** on the Farm tab, under the vendor line: spare adult cows and sheep
  (beyond the numbers kept, never the last male or a female that is carrying) and the
  wool against the vendor's minimum, so it is plain why a vendor run is or is not due.

### Changed
- **Milk at the market.** Skimmed milk counts against the milk kept at home and is sold
  before whole milk, so what stays is the whole milk the cream comes from. Cream is never
  offered while brioche is being baked. Skimming for the brioche only takes milk above
  the number kept, so a big herd on brioche no longer turns every last milk into skim.
- **Vendor runs say what happened** in the server log: wool lots and animals sold, what
  comes home, or why something was refused (not trusted, not sellable, would not be led,
  or the vendor passed on it).
- **/farmhand go** sends the farm hand on its market and vendor runs as soon as it has a
  load, without waiting out the hours between runs.
- **Trips no longer wait for a quiet moment.** Market and vendor runs were a group in the
  Priority order, and a job was only taken from a group when every group above it had
  nothing to do. A farm with a big herd has a milking, a top-up or a pile of dung on every
  tick, so with errands sixth the farm hand never went anywhere (seen: 157 jobs, none of
  them a trip, 13 spare cows and 3680 wool waiting). A due trip is now the fifth fixed
  step, after dry planters and before the routine; "errands" has gone from the
  Priority tab.

### Fixed
- **Skimming ran away with the fridge.** The skim puts the cream and skimmed milk back
  into the milk's own box; when that box was full they were handed to the farm hand
  instead, who never put them down, so it never saw any cream, skimmed one more milk
  every job, and nothing later in the order (muck, errands, butchering) ever ran. It
  now skims only into a box with room, puts down what it is handed, and only when
  cream is the one ingredient the brioche is short of.
- **Vendor runs never happened** while market runs were on: the two shared the same
  "checked five minutes ago" stamp, and the market check came first, so the vendor was
  only ever asked in the two hours after a market run. Each has its own stamp now.

## [0.16.0] — 2026-10-07

A Priority tab. Deployed to a live server; not yet seen in game.

### Added
- **Priority tab** — the four jobs that always come first (put down what it carries,
  animals with nothing to eat or drink, harvest, dry planters) are shown fixed; the eight
  groups below them (animals' routine, replant and fertilise, seeds, water run, baking,
  muck, errands, butchering) can be moved up and down to suit the farm. The order is kept
  per farm hand.
- **Seconds between jobs** on the same tab, per farm hand (5–60), starting from the
  config's value. Each farm hand now works on its own clock.

### Fixed
- The generator went unstirred on a farm with livestock: dung pickup came before the stir
  in the same group and there is always dung on the ground. The stir now comes first.

## [0.15.0] — 2026-10-07

A priority order for the day's work. Deployed to a live server; not yet seen in game.

### Changed
- Jobs are chosen in a fixed order of urgency instead of planters-first: (1) put down
  what it carries; (2) animals with an empty manger, bowl or tank; (3) harvest ripe plants
  and clear dying ones; (4) water dry planters; (5) the animals' routine (hatch, eggs,
  top-ups, petting, milk, wool, honeycomb); (6) replant and fertilise; (7) eat produce for
  seeds; (8) a water run when the container is low; (9) bake; (10) dung, composter,
  generator; (11) market and vendor runs; (12) butchering and the cull. On a big farm the
  animals were going unfed while every planter was seen to first.
- Lower "seconds between jobs" in the config for a large farm; the work is still one job a
  tick.
- **Crop variety** — an empty slot is planted with the switched-on crop that has the fewest
  plants in the ground, instead of whichever seed sits first in the supply box, so the
  planters carry a mix.
- "Hours between vendor runs" on the Animals tab (2), separate from the market's; the Farm
  tab's tally line counts vendor runs.

## [0.14.0] — 2026-10-07

Baking, and feed from the harvest first. Deployed to a live server; not yet seen in game.

### Added
- **Baking** — with a cooking workbench in the base, the farm hand bakes for the animals:
  "Bake at the cooking workbench" on the Animals tab (off / bread / brioche; brioche by
  default, falling back to plain bread when there is no cream), and "loaves to keep in the supply box" (10). It
  loads the table from the boxes in the recipe's order (bread: 5 wheat; brioche: 5 wheat,
  2 eggs, 5 cream), starts it as a player would, and carries the loaves to the supply box
  when they are done. A table someone is using, or that has anything in it, is left alone.
- **Skimming** — for brioche, when cream is short, milk that has sat in the fridge long
  enough is skimmed with the item's own "skim" action (one milk becomes skimmed milk and
  cream, in the same box), as the player's menu click does.

### Fixed
- The milk's "skim" action was looked for in the wrong place and never found.
- A trough still holding a few of one feed was only offered more of the same, so it went
  unfed once that ran out; a trough now takes any feed when low.

### Changed
- Animals are fed from the crops marked as feed (and spoiled produce) **first**, then from
  the supply box; and the trough is topped up when its feed runs low, not only when empty.
- The fuel box line on the Farm tab says it is optional: the generator now gets its share
  of dung directly.

## [0.13.0] — 2026-10-06

The livestock vendor. Deployed to a live server; not yet seen in game.

### Added
- **Vendor runs** — "Take wool, and surplus animals, to the livestock vendor" on the new
  Animals tab (off by default). Wool beyond the amount kept at home, once there is at
  least the vendor's minimum lot, and up to "animals to sell per vendor run" (0 by default)
  adults beyond the numbers kept, go to the nearest vendor (Ranch or Large Barn, within
  5 km, config). The sale goes through the vendor's own negotiation, first offer taken;
  what it pays, scrap or otherwise, comes home with the farm hand. With vendor runs on,
  wool is left out of market runs.

### Changed
- The Duties tab is split: **Duties** (crops, composter, generator, water, market) and
  **Animals** (chickens, hive, livestock, herd numbers, vendor).

## [0.12.0] — 2026-10-06

Husbandry. Deployed to a live server; not yet seen in game.

### Added
- **Herd numbers** — "Keep this many adult cows" and "adult sheep" (2 each) on the Duties
  tab, and a "Butcher livestock beyond the numbers kept" switch, off by default. When on,
  one surplus adult an hour is butchered: a spare male first (one male is always kept so
  the herd breeds), then a female that is not carrying; never the young. The animal dies
  as it would to a player and its corpse's full yield goes to the boxes, meat to the
  fridge, hide and bone to the plain box. Breeding itself is the game's own.

### Changed
- The market report now says what was sold for, what was spent on seeds and what came
  home, so the takings and the box agree.

## [0.11.0] — 2026-10-06

Stage 6, "deliver to market". Seen on a live server: a run to the market and the scrap back in the boxes.

### Added
- **Market runs** — with the Farmers Market plugin loaded and a stall placed within range
  (3 km, config), the farm hand takes the surplus produce from the output boxes to the
  market: everything a stall buys beyond "keep this much of each produce at home" (20,
  Duties tab), seeds and clones never. It goes at most once per "hours between market
  runs" (2), only when the load is worth at least 50 scrap (config) and the owner's daily
  limit at the market is not used up. The sale is settled through the market's own call
  (its prices, falling prices and daily limit all apply); the farm hand is away for the
  walk there and back plus a minute's trading, and the scrap appears in the boxes when it
  returns. You are told what it earned, in chat or on next connecting.
- **Buying seeds** — "Buy seeds at the market with the takings" (on by default): crops short
  of their kept number of seeds are bought at the stall's price out of that run's scrap.
- The Farm tab shows how far the market is; the tally line counts runs and scrap earned.

## [0.10.0] — 2026-10-06

Seed stock and feed. Deployed to a live server.

### Added
- **Seed stock** — a "seeds kept" number per crop on the Crops tab (10 by default, config).
  When the supply box holds fewer seeds and clones of a crop that is planted, the farm hand
  eats one of that produce from the boxes for its seed (one fruit, one seed: the game's own
  table), or, when there is nothing to eat for it (hemp and flowers give no seed; nothing
  harvested yet), takes clones from the next ripe plant of that crop instead of picking
  it. Clones carry the plant's genes; choosing which plant to clone by its genes is for a
  later version.
- **Feed** — a "feed" switch per crop, off by default. Animals are fed from the supply box
  first; from the harvest in the output boxes only crops switched on, plus produce that has
  spoiled.
- Column headings on the Crops tab: Crop · Plant · Feed · Seeds kept · Water below · Fill to.
- **Dung share** — "Dung to the generator" on the Duties tab (50% by default): that share of
  the dung picked up goes into the biofuel generator, the rest to the composter; either
  takes all of it when the other is missing or full.
- **What spoils goes to the fridge.** Items that spoil (the game's own flag: eggs, milk,
  most produce, raw meat) are delivered fridge-first; items that keep (wool, honeycomb,
  wheat, fuel, cloth) go to a plain box first so the fridge's slots stay for food.

### Fixed
- Eggs and honeycomb landed in the last output box even when the fridge had room: the
  stack was moved into the fridge and then moved on again to the next box.

## [0.9.0] — 2026-10-06

Doors. Seen on a live server: the farm hand walked through the owner's door to a job.

### Added
- **Real paths** — the farm hand's walks are planned on the game's navmesh (RustNav),
  doors allowed, and stepped corner by corner; where the mesh has no complete route it
  falls back to a straight line, and a leg that stalls ends with a hop so the job is
  always done.
- **Doors** — the owner's own doors inside the cupboard are marked NPC-openable in the mesh
  (nobody else's are touched). The farm hand opens the nearest own door on its way and
  shuts it once past, and on arrival.

### Changed
- The farm hand only walks about, and so only opens doors, while the **owner** is within
  range; near anyone else it works in place.
- The farm hand's face seed is no longer a Steam-range id: other plugins that test
  `IsSteamId()` (XPerience, on every harvest) were treating it as a player and throwing.
  Farm hands hired before this get a new face once.
- Leaving for the river, the farm hand opened the door and then walked out through the
  wall: the goal on the terrain had no complete route, so the walk fell back to a straight
  line. Partial routes are now followed as far as they go (out through the door) and only
  the last stretch is hopped.

## [0.8.0] — 2026-10-06

Milk and wool. Seen on a live server: harvest and milk into the fridge, a sheep sheared,
a chick hatched.

### Added
- **Milking and shearing** — tame cows inside the farm are milked and tame sheep sheared (one
  section of fleece per job, as a knife takes it), into the output boxes, with the animals'
  own calls. An animal only lets someone it trusts handle it, and trust is time spent near
  it: the farm hand is credited its job tick while it is about the farm, as a player
  standing by would be, so after five minutes at default rates it may handle the animals.
  Only animals somebody has already tamed are befriended, never wild ones.
- **Lost boxes** — a marked box that is destroyed or picked up is forgotten within a minute
  and the owner is told which one, in chat or on next connecting. A box put back down is a
  new box and must be marked again.

### Fixed
- Nothing was ever put in the composter: the room check asked for "any slot" and ovens
  judge by slot, so dung and plant fibre were refused. Each slot is now asked in turn.
- A box could hold two roles at once (output and supply, say); marking a box for a new
  role now drops its old one.
- An egg the owner had already put in the coop's hatch slot was never set to hatch.
- Animals' trust was only credited on ticks when the farm hand had nothing else to do, so
  on a busy farm it took far longer than five minutes; it is now credited every tick.

## [0.7.0] — 2026-10-06

The biofuel generator. Seen working on a live server.

### Added
- **Biofuel generator** inside the cupboard's range: fed from the box you mark with
  `/farmhand fuel` (only what you put there is ever burned; anything the generator accepts),
  stirred when it asks for a stir (with someone watching, the farm hand takes the paddle for
  the ten seconds a player would; otherwise the stir is simply done), and the low grade
  fuel it makes is carried to the output boxes once there are twenty or more.
- A "Biofuel" duty switch; `/farmhand fuel` on the same box again unmarks it.

### Changed
- The panel is split into three tabs: **Farm** (wage, what is in range, the boxes),
  **Duties** (the switches and the chicken settings) and **Crops** (one row per crop), with
  roomier rows.

## [0.6.0] — 2026-10-05

Livestock. Seen working on a live server.

### Added
- **Hitching troughs** inside the cupboard's range are kept stocked: feed from the supply
  box (the same feed a coop takes) when the manger is empty, water into the trough's own
  tank from the water container when it is below a quarter.
- **Dung** dropped by livestock inside the farm is picked up and taken to the composter.
- A "Tend livestock" duty switch; the panel's farm line counts troughs.

## [0.5.0] — 2026-10-05

The first supply run: water from the river. Seen working on a live server.

### Added
- **Water runs** — when the water container falls below a quarter full (config) and there
  is a river or lake within 400 m (config), the farm hand walks to the edge of the farm,
  is away for the time the round trip would take at walking pace, and reappears at its
  post with 10 L (config) which it pours into the container. No pathfinding across the
  island: the trip is simulated once out of sight, as the brief recommends. A trip survives
  a reload or restart. Fresh water is never poured into a container holding salt water.
- A "Fetch water" duty switch; the panel says how far the nearest fresh water is, or that
  there is none in range; `/farmhand` reports when it is away and for how long.

## [0.4.0] — 2026-10-05

Chickens and bees. Seen working on a live server.

### Added
- **Chicken coops** inside the cupboard's range are tended: feed from the supply box
  (anything the coop accepts as feed) when the trough is empty or nearly so, water from the
  water container when the bowl is low, a pat for any chicken whose love is below 80, and
  the eggs laid go to the output boxes (fridge first).
- **Hatching** — "Keep this many chickens per coop" on the panel (4 by default, the coop's
  own limit). When a coop has fewer, an egg (from the coop's own nest first, then the
  boxes) is set to hatch, exactly as the coop's hatch button does.
- **Butchering** — "Butcher one chicken every N hours" (never, by default). The bird dies as
  it would to a player; the corpse's full yield goes to the boxes and the corpse is cleared.
  Never the last two chickens.
- **Beehives** inside the cupboard's range are emptied of honeycomb into the output boxes,
  so the hive keeps producing.
- Duty switches for chickens and honey; the panel's farm line counts coops and hives.

## [0.3.0] — 2026-10-05

The composter loop. Seen working on a live server.

### Added
- **Composter** — look at a composter and run `/farmhand compost`. Plant fibre from cleared
  plants goes in, as does any waste found in the output and supply boxes (plant fibre,
  dung, spoiled food; never seeds, clones, fertiliser or edible produce). Fertiliser for
  the planters is taken from the composter first, the supply box second, and a stack is
  moved out to the supply box when the composter's slots are filling up.
- A "Feed the composter" duty switch on the panel.

## [0.2.0] — 2026-10-05

Stage 2, "crops". Tested on a live server: planting, watering, walking to the planter.

### Added
- **Boxes** — look at a box and run `/farmhand output` (harvest goes here; mark several,
  fridges are filled first so food keeps), `/farmhand supply` (seeds, clones and
  fertiliser come from here) or `/farmhand water` (a water barrel, jug or catcher). All must
  be inside the same cupboard's range. `/farmhand boxes` shows what is set.
- **Harvest** — ripe plants are picked into the output boxes with the game's own harvest
  call; dying plants are cleared (plant fibre to the box). Nothing is picked unless a box
  has room for it.
- **Replant** — empty planter slots are planted from the supply box, seeds or clones;
  clones keep their genes.
- **Water** — planters with plants are watered from the water container when they fall
  below the crop's "water when below" level, up to its "fill to" level (40% and 70% by
  default, the same band a sprinkler uses).
- **Fertilise** — a planter with plants and no fertiliser gets one from the supply box.
- **Panel** — press the use key on the farm hand: wage and tallies, the boxes, four duty
  switches, and one row per crop (14 known) with a plant switch and the two water levels.
- **Walking** — while a player is within 40 m the farm hand walks to each job and back
  to its post; otherwise it works in place. Straight lines, no pathfinding yet.
- Config: seconds between jobs (15), farm radius (30 m), default water levels, walk
  range and speed.

### Changed
- The farm hand's panel replaces the chat status when pressing the use key on it.

## [0.1.0] — 2026-10-05

Stage 1, "hire and stand". The farm hand does no farm work yet.

### Added
- **Hiring** — `/farmhand hire` posts a farm hand where the player stands, inside the range
  of a tool cupboard they are authorised on. One farm hand per player.
- **Move and dismiss** — `/farmhand move` posts it at a new spot (or another cupboard),
  `/farmhand dismiss` sends it away.
- **Daily wage** — the first day is paid on hiring. An unpaid farm hand stops work, waits
  for a grace period and then leaves. Wages already paid stay on the account when the farm
  hand is dismissed or its cupboard is lost.
- **Paying through Public Works** — a "Farm hand" row on the office's MY ACCOUNTS page, at
  the clerk and over the phone line.
- **Standalone wages** — `/farmhand pay [days]` takes scrap when Public Works is not loaded.
- **A worker, not a guard** — the farm hand can't be hurt, doesn't fight, and is ignored by
  turrets and other NPCs. It turns to face players who come close.
- **Leaves with the cupboard** — if the tool cupboard is destroyed, or the owner is no
  longer authorised on it, the farm hand leaves.
- **Survives reloads, restarts and crashes** — the data file is written with the server
  save and on unload. The NPC itself is never in the world save.
- A server-wide limit on posted farm hands (20 by default).
- The farm hand's radio chatter is silenced.
- Admin commands: `/farmhand list`, `grant`, `remove`.
- `GetHelpInfo` hook for the HelpMenu plugin.
