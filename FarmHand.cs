using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Farm Hand", "LowPopLabs", "0.22.0")]
    [Description("Hire a farm hand to look after your base's farm. Posted inside your tool cupboard's range for a daily wage, it harvests ripe plants into your boxes, replants from your seed box, waters and fertilises the planters. Wages are paid at the Public Works office, or in scrap by command when that plugin is not installed.")]
    public class FarmHand : RustPlugin
    {
        [PluginReference] private Plugin PublicWorks;
        [PluginReference] private Plugin FarmersMarket;

        private const string PermAdmin = "farmhand.admin";
        private const string PermUse = "farmhand.use";
        private const string BillKey = "wage";
        private const string ScrapShortname = "scrap";
        private const double DaySeconds = 86400;

        private const string NpcPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_cinematic.prefab";
        private const string NpcPrefabFallback = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roamtethered.prefab";

        // Face seeds sit below the Steam id range (IsSteamId is > 76561197960265728) and far
        // above the engine's bot range (IsBot is < 10000000): a stable face, and every
        // plugin that asks IsSteamId() to tell a real player leaves the farm hand alone.
        private const ulong FaceSeedBase = 1000000000000UL;
        private const ulong SteamIdFloor = 76561197960265728UL;

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Prefix"] = "<color=#8bc34a>Farm Hand</color>: ",
                ["NoPermission"] = "You don't have permission to do that.",

                ["Bill.Label"] = "Farm hand",
                ["Bill.Description"] = "Daily wages for your {0} farm hand(s), {1} scrap each",
                ["Bill.Left"] = "{0} left",
                ["Bill.Unpaid"] = "UNPAID — work stopped",
                ["Bill.Button"] = "+1 DAY",

                ["Status.None"] = "You have no farm hands. Stand in your base, inside your tool cupboard's range, and use /farmhand hire [role]. The wage is {0} scrap a day each and the first day is paid when you hire.",
                ["Status.Farm"] = "Farm in {0}: {1}.",
                ["Status.Unposted"] = "On the books, not posted: {0}. Stand in your base and use /farmhand post.",
                ["Status.Paid"] = "Wages paid for another {0} ({1} scrap a day for {2} hand(s)).",
                ["Status.Unpaid"] = "The wages ran out and work has stopped. The hands wait another {0}, then leave.",
                ["Status.NotPosted"] = "You have {0} of wages paid but no farm hands. Use /farmhand hire in your base.",
                ["Status.MaxPrepaid"] = "Wages can be paid up to {0} day(s) ahead.",

                ["Role.all"] = "farm hand",
                ["Role.planter"] = "planter",
                ["Role.stockman"] = "stockman",
                ["Role.yard"] = "yard hand",
                ["Role.carter"] = "carter",
                ["Role.none"] = "nobody",
                ["Ui.Named"] = "{0} the {1}",
                ["Ui.FarmIn"] = "FARM IN {0}",
                ["Ui.Shared"] = "Farm settings, shared by the {0} hand(s) here.",
                ["Ui.NotTheirs"] = "Not {0}'s job: {1} see(s) to this. Farm settings, shared by every hand here.",
                ["Ui.NobodysJob"] = "Not {0}'s job, and no hand here does it. Farm settings, shared by every hand here.",
                ["Hire.Roles"] = "Roles: {0}. A farm hand does everything; a planter sees to the planters, seeds and water runs; a stockman to the animals; a yard hand to the composter, generator and baking; a carter to the market and vendor runs.",
                ["Hire.NoCupboard"] = "Stand inside the range of a tool cupboard you are authorised on.",
                ["Hire.NotStanding"] = "Stand on the floor where the farm hand should wait.",
                ["Hire.ServerFull"] = "No farm hands are looking for work right now (the island's limit of {0} is reached).",
                ["Hire.FarmFull"] = "This farm already has its {0} hands.",
                ["Hire.NotYours"] = "This farm belongs to {0}.",
                ["Hire.NeedScrap"] = "You need {0} scrap for the first day's wages.",
                ["Hire.Failed"] = "The farm hand could not be brought here. Try another spot.",
                ["Hire.Done"] = "A {0} hired for {1} scrap and posted here. Your hands are paid for another {2}.",
                ["Hire.DonePaid"] = "A {0} hired and posted here. Your hands are paid for another {2}.",
                ["Hire.Unposted"] = "A {0} hired and on your books. Stand in your base where it should wait and use /farmhand post.",
                ["Post.None"] = "No hired hand is waiting to be posted. Use /farmhand hire [role].",
                ["Post.Done"] = "Your {0} now waits here.",
                ["Move.None"] = "Stand near the hand you want to move.",
                ["Move.Done"] = "Your farm hand now waits here.",
                ["Move.Away"] = "{0} is away on a trip; post them when they are back.",
                ["Dismiss.None"] = "Stand near the hand you want to dismiss, or name a role of one on your books.",
                ["Dismiss.Done"] = "Your {0} has been sent away. Wages already paid ({1}) stay on your account until they run out.",
                ["Dismiss.DoneUnpaid"] = "Your {0} has been sent away.",

                ["Pay.Office"] = "Pay the wage ({0} scrap a day) at the Public Works office, under MY ACCOUNTS.",
                ["Pay.OfficeGrid"] = "Pay the wage ({0} scrap a day) at the Public Works office in {1}, under MY ACCOUNTS.",
                ["Pay.Phone"] = "Or call the office from any telephone: {0}.",
                ["Pay.Command"] = "Pay the wage ({0} scrap a day) with /farmhand pay [days].",
                ["Pay.AtOfficeOnly"] = "The wage is paid at the Public Works office, not here.",
                ["Pay.NoHand"] = "You have no farm hands to pay. Use /farmhand hire in your base.",
                ["Pay.NeedScrap"] = "You need {0} scrap for {1} day(s) of wages.",
                ["Pay.Done"] = "Paid {0} scrap. Your farm hand is paid for another {1}.",
                ["Pay.Extended"] = "Your farm hand is paid for another {0}.",
                ["Pay.Usage"] = "Usage: /farmhand pay [days]",

                ["Left.Unpaid"] = "Your farm hands went unpaid for too long and have left their posts. They are still on your books; pay the wages and post them again.",
                ["Left.Cupboard"] = "Your farm hands have left a farm: its tool cupboard is gone, or you are no longer authorised on it. They are on your books; post them again with /farmhand post.",
                ["Left.Wipe"] = "The map was wiped and your farm hands are no longer posted. They are on your books; post them again with /farmhand post. Wages already paid stay on your account.",
                ["Left.Admin"] = "Your farm hands were taken off their posts by an admin. They are on your books; wages already paid stay on your account.",

                ["Use.Other"] = "This farm hand works for {0}.",

                ["Box.None"] = "Stand in a farm of yours (inside its tool cupboard's range, with a hand posted) first.",
                ["Box.LookAt"] = "Look at the box, from within 5 m, and run the command again.",
                ["Box.NotBox"] = "That isn't a storage box.",
                ["Box.NotWater"] = "That doesn't hold water. Look at a water barrel, jug or catcher.",
                ["Box.NotHere"] = "That isn't inside your farm hand's base.",
                ["Box.Output"] = "Harvest will go into this box. Mark a fridge too and food goes there first.",
                ["Box.OutputRemoved"] = "Harvest will no longer go into this box.",
                ["Box.Supply"] = "Seeds, clones and fertiliser will be taken from this box.",
                ["Box.Water"] = "Water will be taken from this container.",
                ["Box.Compost"] = "Plant fibre and spoiled food will go into this composter, and fertiliser will be taken from it.",
                ["Box.NotCompost"] = "That isn't a composter.",
                ["Box.Fuel"] = "Whatever is in this box will be fed to the biofuel generator.",
                ["Box.FuelRemoved"] = "This box will no longer be fed to the generator.",
                ["Box.Status"] = "Output boxes: {0} · Supply box: {1} · Water: {2} · Composter: {3} · Fuel box: {4}",
                ["Box.Set"] = "set",
                ["Box.Unset"] = "not set",
                ["Box.Usage"] = "Look at a box and use /farmhand output, /farmhand supply, /farmhand water, /farmhand compost or /farmhand fuel.",
                ["Box.Gone"] = "Your farm hand's {0} is gone. Look at another and use /farmhand {1} to replace it.",
                ["Box.Name.output"] = "output box",
                ["Box.Name.supply"] = "supply box",
                ["Box.Name.water"] = "water container",
                ["Box.Name.compost"] = "composter",
                ["Box.Name.fuel"] = "fuel box",
                ["Box.Name.takings"] = "takings box",
                ["Box.Takings"] = "Scrap from the market and the vendor's pay will go into this box.",
                ["Box.TakingsRemoved"] = "Takings will go into the output boxes again.",

                ["Ui.Title"] = "FARM HAND",
                ["Ui.Paid"] = "Paid for another {0} ({1} scrap a day for {2} hand(s))",
                ["Ui.Unpaid"] = "UNPAID — work stopped, leaves in {0}",
                ["Ui.Tally"] = "Harvested {0} · Planted {1} · Watered {2} times · {3} water run(s)",
                ["Ui.Farm"] = "In range: {0} planter(s) with {1} plant(s) · {2} coop(s) · {3} hive(s) · {4} trough(s) · {5} generator(s)",
                ["Ui.Boxes"] = "BOXES",
                ["Ui.BoxOutput"] = "Harvest to:",
                ["Ui.BoxSupply"] = "Seeds from:",
                ["Ui.BoxWater"] = "Water from:",
                ["Ui.BoxCompost"] = "Compost in:",
                ["Ui.BoxFuel"] = "Burn in generator:",
                ["Ui.BoxSet"] = "{0}",
                ["Ui.BoxUnset"] = "not set — look at one and use /farmhand {0}",
                ["Ui.Duties"] = "DUTIES",
                ["Ui.DutyHarvest"] = "Harvest ripe plants",
                ["Ui.DutyReplant"] = "Replant empty slots",
                ["Ui.DutyWater"] = "Water planters",
                ["Ui.DutyFertilise"] = "Keep planters fertilised",
                ["Ui.DutyCompost"] = "Feed the composter, take its fertiliser",
                ["Ui.DutyChickens"] = "Tend chickens: feed, water, pet, collect eggs",
                ["Ui.DutyHoney"] = "Collect honeycomb from beehives",
                ["Ui.DutyFetch"] = "Fetch water from the river when the container runs low",
                ["Ui.DutyMarket"] = "Take surplus produce to the Farmers Market",
                ["Ui.DutyBuySeeds"] = "Buy seeds at the market with the takings",
                ["Ui.KeepProduce"] = "Keep this much of each produce at home",
                ["Ui.KeepEachHead"] = "KEPT AT HOME, BY PRODUCE",
                ["Ui.KeepEach"] = "What the stalls buy and the boxes hold; × goes back to the general number. Skimmed milk sells first and counts as milk; cream stays home while brioche is baked.",
                ["Ui.KeepNone"] = "The boxes hold nothing a stall buys yet.",
                ["Ui.TabVendor"] = "VENDOR",
                ["Ui.HandsRail"] = "HANDS",
                ["Panel.None"] = "No hand of yours is standing anywhere. Hire one, or post one from your books.",
                ["Help.Cmd.Menu"] = "Open the panel from anywhere: the nearest of your hands, else one at the farm you stand in, else any",
                ["Ui.NoFeed"] = "A manger or coop is empty and there is nothing to fill it with: no crop is marked as feed on the CROPS tab and the supply box holds no feed or loaves.",
                ["Ui.VendorHint"] = "Which animals go, and how many are bought, follows the numbers kept on the Animals tab; breeding by the genes and the knife for the inbred are there too.",
                ["Ui.DutyBuyStock"] = "Buy calves ({0} scrap) and lambs ({1}) from the vendor when the herd is below the numbers kept, with scrap from the boxes",
                ["Ui.BuyPerRun"] = "Animals bought per vendor run",
                ["Ui.VendorWanted"] = "To buy on the next run: {0} for {1} scrap ({2} in the boxes)",
                ["Ui.Animal.cow:m"] = "bull calf",
                ["Ui.Animal.cow:f"] = "heifer calf",
                ["Ui.Animal.sheep:m"] = "ram lamb",
                ["Ui.Animal.sheep:f"] = "ewe lamb",
                ["Ui.JobTally"] = "Jobs since posting: {0}",
                ["Ui.VendorStock"] = "For the vendor: {0} spare of {1} adult cow(s), {2} spare of {3} adult sheep, wool {4} of {5}",
                ["Ui.MarketHours"] = "Hours between market runs",
                ["Ui.MarketNear"] = "Farmers Market {0} m away",
                ["Ui.MarketNone"] = "No Farmers Market stall within {0} m",
                ["Ui.MarketClosed"] = "The Farmers Market plugin is not loaded",
                ["Ui.AwayMarket"] = "Away at the market, back in {0}",
                ["Ui.MarketTally"] = "Market runs {0} · {1} scrap earned · vendor runs {2}",
                ["Ui.VendorHours"] = "Hours between vendor runs",
                ["Market.Returned"] = "Your farm hand is back from the market: sold produce for {0} scrap{1}; {2} scrap brought home.",
                ["Market.Bought"] = ", spent {0} on {1} seed(s)",
                ["Ui.DutyVendor"] = "Take wool, and surplus animals, to the livestock vendor",
                ["Ui.AnimalsPerRun"] = "Animals to sell per vendor run (beyond the numbers kept)",
                ["Ui.VendorNear"] = "Livestock vendor {0} m away",
                ["Ui.VendorNone"] = "No livestock vendor within {0} m",
                ["Ui.AwayVendor"] = "Away at the livestock vendor, back in {0}",
                ["Vendor.Returned"] = "Your farm hand is back from the livestock vendor with {0}.",
                ["Vendor.Nothing"] = "nothing: the vendor made no deal",
                ["Ui.Focus"] = "THE KITCHEN",
                ["Ui.Focus.bread"] = "Bread",
                ["Ui.Focus.pies"] = "Pies",
                ["Ui.Focus.teas"] = "Teas",
                ["Ui.FocusHint.bread"] = "Loaves baked from wheat (brioche with eggs and cream) into the supply box, as feed for the animals.",
                ["Ui.FocusHint.pies"] = "Pumpkin, beef, mutton and chicken pies (rich with cream) from wheat, eggs, corn, pumpkin, potato and the cull's meat cooked on a barbecue with wood from the supply box; pies to the output boxes.",
                ["Ui.FocusHint.teas"] = "Teas brewed from red, green, yellow, white and blue berries at the workbench; teas to the output boxes.",
                ["Ui.NoWorkbench"] = "No cooking workbench inside the farm: nothing can be baked or brewed.",
                ["Ui.NoBbq"] = "No barbecue or campfire inside the farm: the meat cannot be cooked, so only pumpkin pies can be made.",
                ["Ui.Fire"] = "Wood for the fire in the supply box: {0}",
                ["Ui.TableHolds"] = "On the workbench: {0} {1}",
                ["Ui.TableOn"] = "(mixing)",
                ["Ui.CanBake"] = "Can bake now (batches): {0}",
                ["Ui.CannotBake"] = "Short for: {0}",
                ["Ui.Meat"] = "Meat in the boxes, raw/cooked: {0} (cooked kept at {1} each)",
                ["Ui.NoTeas"] = "No tea recipes known: is there a cooking workbench inside the farm?",
                ["Ui.PiesKeep"] = "Pies to keep in the output boxes",
                ["Ui.TeasKeep"] = "Of each tea, keep in the output boxes",
                ["Ui.AdvancedTeas"] = "Brew the advanced teas too (four of a tea make one)",
                ["Ui.PlantFocus"] = "PLANT FOR {0}",
                ["Ui.PlantFocusHint"] = "Sets the plant switches to the kitchen's crops (feed crops stay); then tweak as you like.",
                ["Ui.Bake"] = "Bake at the cooking workbench for the animals",
                ["Ui.BakeOff"] = "off",
                ["Ui.BakeBread"] = "bread",
                ["Ui.BakeBrioche"] = "brioche",
                ["Ui.LoavesKeep"] = "Loaves to keep in the supply box",
                ["Ui.BoxFuelHint"] = "optional: extra things to burn",
                ["Ui.BoxTakings"] = "Takings go to:",
                ["Ui.BoxTakingsHint"] = "optional: scrap and the vendor's pay, else the output boxes",
                ["Ui.StockCap"] = "Stop producing anything the boxes hold more than (0 = never)",
                ["Ui.KeepEachCols"] = "kept at home · stop producing at",
                ["Ui.DutyLivestock"] = "Tend livestock: feed and water, milk cows, shear sheep, pick up dung",
                ["Ui.DutyBiofuel"] = "Feed and stir the biofuel generator, collect the fuel",
                ["Ui.DungShare"] = "Dung to the generator (the rest to the composter)",
                ["Ui.DutyCull"] = "Butcher livestock beyond the numbers kept (one an hour; never young, pregnant or the last male)",
                ["Ui.KeepCows"] = "Keep this many adult cows",
                ["Ui.KeepSheep"] = "Keep this many adult sheep",
                ["Ui.DutyBreeding"] = "Breed by the genes: one bull unrelated to the cows, the best kept, the worst sold, the inbred to the knife (see the Herd tab)",
                ["Ui.TabHerd"] = "HERD",
                ["Ui.ColAnimal"] = "Animal",
                ["Ui.ColSex"] = "Sex",
                ["Ui.ColGenes"] = "Genes D L Y F H (shown, carried)",
                ["Ui.ColLine"] = "Line",
                ["Ui.ColPlan"] = "Plan",
                ["Ui.HerdNone"] = "No cows or sheep inside the farm.",
                ["Ui.HerdBull"] = "{0} bull: {1}, sharing a line with {2} of the {3} cows kept",
                ["Ui.HerdNoBull"] = "{0}: no bull",
                ["Ui.HerdFreshBlood"] = "Fresh blood needed: most calves of this pairing will be inbred",
                ["Ui.HerdHint"] = "G good, O ok, B bad; the first letter shows, the second is carried. Matching line markers mean inbred.",
                ["Ui.Inbred"] = "INBRED",
                ["Ui.Sex.M"] = "M",
                ["Ui.Sex.F"] = "F",
                ["Ui.Age.calf"] = "calf",
                ["Ui.Age.young"] = "young",
                ["Ui.Plan.bull"] = "the bull",
                ["Ui.Plan.keep"] = "keep",
                ["Ui.Plan.carrying"] = "in calf",
                ["Ui.Plan.growing"] = "growing",
                ["Ui.Plan.wild"] = "not tamed",
                ["Ui.Plan.sell"] = "to the vendor",
                ["Ui.Plan.knife"] = "for the pot",
                ["Ui.Plan.spare"] = "spare (vendor and butcher off)",
                ["Ui.HerdPlain"] = "Breeding by the genes is off (Animals tab): spare males go first, then any female, whatever their genes.",
                ["Ui.HerdNoKnife"] = "Butchering is off (Animals tab): the inbred go with the rest instead of to the pot.",
                ["Ui.Away"] = "Away fetching water, back in {0}",
                ["Ui.WaterNear"] = "Fresh water {0} m away",
                ["Ui.WaterNone"] = "No river or lake within {0} m: water must be brought by hand",
                ["Ui.KeepChickens"] = "Hatch eggs to keep this many chickens per coop",
                ["Ui.Butcher"] = "Butcher one chicken every",
                ["Ui.Never"] = "never",
                ["Ui.Hours"] = "{0} h",
                ["Ui.Crops"] = "CROPS",
                ["Ui.ColCrop"] = "Crop",
                ["Ui.ColPlant"] = "Plant",
                ["Ui.ColFeed"] = "Feed",
                ["Ui.ColSeeds"] = "Seeds kept",
                ["Ui.ColBelow"] = "Water below",
                ["Ui.ColFill"] = "Fill to",
                ["Ui.On"] = "ON",
                ["Ui.Off"] = "OFF",
                ["Ui.Close"] = "✕",
                ["Ui.TabHand"] = "HAND",
                ["Ui.TabSetup"] = "SETUP",
                ["Ui.TabCrops"] = "CROPS",
                ["Ui.TabAnimals"] = "ANIMALS",
                ["Ui.TabYard"] = "YARD",
                ["Ui.TabMarket"] = "MARKET",
                ["Ui.Role"] = "ROLE",
                ["Ui.RoleHint.all"] = "Sees to everything on the farm.",
                ["Ui.RoleHint.planter"] = "The planters: harvest, replant, water, fertilise, seeds and the water run.",
                ["Ui.RoleHint.stockman"] = "The animals: coops, hives, livestock, milk, wool, the herd and the knife.",
                ["Ui.RoleHint.yard"] = "The yard: composter, generator, dung and the baking.",
                ["Ui.RoleHint.carter"] = "The errands: market and vendor runs.",
                ["Ui.Order"] = "ORDER OF WORK",
                ["Ui.FixedSteps"] = "Always first: put down what it carries, animals with nothing to eat or drink, harvest, dry planters, a trip that is due. Then, in this order:",
                ["Ui.PostHere"] = "POST HERE",
                ["Ui.Dismiss"] = "DISMISS",
                ["Ui.Hands"] = "HANDS ON THIS FARM: {0} OF {1}",
                ["Ui.Candidates"] = "CONTAINERS IN REACH",
                ["Ui.SetupHint"] = "Pick what each is for, or let the farm hand choose.",
                ["Ui.AutoSetup"] = "SET UP FOR ME",
                ["Ui.NoCandidates"] = "No boxes, fridges, barrels or composters inside the farm within reach of the post.",
                ["Ui.Dist"] = "{0} m",
                ["Ui.Is.output"] = "harvest",
                ["Ui.Is.supply"] = "supplies",
                ["Ui.Is.water"] = "water",
                ["Ui.Is.compost"] = "compost",
                ["Ui.Is.fuel"] = "fuel",
                ["Ui.Is.takings"] = "takings",
                ["Ui.Pick.output"] = "HARVEST",
                ["Ui.Pick.supply"] = "SUPPLY",
                ["Ui.Pick.water"] = "WATER",
                ["Ui.Pick.compost"] = "COMPOST",
                ["Ui.Pick.fuel"] = "FUEL",
                ["Ui.Pick.takings"] = "TAKINGS",
                ["Ui.JobSeconds"] = "Seconds between jobs",
                ["Ui.Prio.animals"] = "Animals: hatch, eggs, top-ups, petting, milk, wool, honey",
                ["Ui.Prio.planting"] = "Replant and fertilise",
                ["Ui.Prio.seeds"] = "Seeds: eat produce or take clones",
                ["Ui.Prio.waterrun"] = "Water run to the river",
                ["Ui.Prio.bake"] = "Bake for the animals",
                ["Ui.Prio.muck"] = "Dung, composter, generator",
                ["Ui.Prio.knife"] = "Butchering and the cull",

                ["Admin.Usage"] = "/farmhand list · grant <player> <days> · remove <player>",
                ["Admin.ListHeader"] = "{0} farm hand(s) posted, limit {1}:",
                ["Admin.ListPosted"] = "  {0} — farm in {1}: {2}, paid {3}",
                ["Admin.ListUnpaid"] = "  {0} — farm in {1}: {2}, UNPAID",
                ["Admin.ListNone"] = "No farm hands are posted.",
                ["Admin.NoPlayer"] = "No player found for '{0}'.",
                ["Admin.Granted"] = "{0} now has {1} of farm hand wages.",
                ["Admin.Removed"] = "{0}'s farm hands have been taken off their posts.",
                ["Admin.NotPosted"] = "{0} has no farm hands posted.",

                ["Help.Title"] = "Farm Hand",
                ["Help.Description"] = "Hire farm hands for your bases. They harvest, replant, water and fertilise the planters inside your tool cupboard's range, tend the animals, the yard and the errands, and are paid by the day.",
                ["Help.HowTo"] = "Stand in your base where the hand should wait and use /farmhand hire, or /farmhand hire planter, stockman, yard or carter for one that sees to only that part of the farm. The first day's wages are paid on the spot. Up to four hands per farm, and a farm at every base you own.\nPress the use key on a hand to open its panel. The SETUP tab lists the boxes in reach: pick which take the harvest, hold the seeds, the water and the compost, or press SET UP FOR ME. (Or look at a box and use /farmhand output, supply, water, compost or fuel.)\nThe HAND tab is that hand: its role, pace and the order of its work. The other tabs are the farm: crops, animals, the herd, the yard and the market.\nPay further days at the Public Works office (MY ACCOUNTS), or with /farmhand pay when there is no office. One bill covers all your hands.\nKeep the wages paid: unpaid hands stop work, wait for the grace period and then leave their posts; they stay on your books to be posted again.",
                ["Help.Notes"] = "A farm is a tool cupboard you are authorised on, with up to four hands; hands only touch planters and boxes inside that cupboard's range. /farmhand move and /farmhand dismiss act on the nearest of your hands.\nThey also tend chicken coops (feed, water and petting from your boxes, eggs to the output box, hatching to keep the number you set, and a chicken for the pot now and then if you ask) and empties beehives of honeycomb.\nA biofuel generator is fed from the box you mark with /farmhand fuel, stirred when it needs it, and its low grade fuel is carried to the output boxes.\nLivestock at a hitching trough are fed and watered from the same boxes, tame cows are milked and tame sheep sheared into the output boxes, and dung is picked up for the composter.\nWhen the water container runs low it walks to the nearest river or lake and comes back with a load; it is away for as long as the walk takes.\nWhen the boxes hold surplus produce it takes it to the Farmers Market, brings the scrap back to your boxes, and can buy seeds there with the takings.\nWith vendor runs on, wool and surplus animals go to the livestock vendor at the Ranch or Large Barn for whatever it offers.\nWith a cooking workbench in the base it bakes bread (brioche when there is cream) from your wheat, eggs and cream to keep loaves in the supply box for the animals.\nIt does one job every few seconds, and only walks about while someone is near.\nThe farm hand can't be hurt and doesn't fight.\nIf the cupboard is destroyed the farm hand leaves; wages already paid stay on your account.",
                ["Help.Cmd.Status"] = "Where your farm hand is and how long it is paid for",
                ["Help.Cmd.Hire"] = "Hire a hand of a role and post it where you stand (up to four per farm)",
                ["Help.Cmd.Post"] = "Post a hired hand where you stand",
                ["Help.Cmd.Move"] = "Move the nearest of your hands to where you stand",
                ["Help.Cmd.Dismiss"] = "Send the nearest of your hands away",
                ["Help.Cmd.Boxes"] = "Look at a box: where harvest goes, where seeds come from, where water comes from, which composter to feed, what to burn for biofuel",
                ["Help.Cmd.Pay"] = "Pay wages in scrap (servers without a Public Works office)",
                ["Help.Cmd.Go"] = "Send it on its market and vendor runs now, without waiting out the hours between runs",
                ["Go"] = "Your farm hand will go on its next trip as soon as it has a load for it.",
                ["Help.Cmd.Admin"] = "Admin: list farm hands, grant wages, remove a farm hand",
            }, this);
        }

        private string Msg(string key, string userId, params object[] args)
        {
            string text = lang.GetMessage(key, this, userId);
            return args != null && args.Length > 0 ? string.Format(text, args) : text;
        }

        private void Reply(BasePlayer player, string key, params object[] args) =>
            SendReply(player, Msg("Prefix", player.UserIDString) + Msg(key, player.UserIDString, args));

        // Unprefixed reply (admin command feedback, follow-on lines).
        private void ReplyRaw(BasePlayer player, string key, params object[] args) =>
            SendReply(player, Msg(key, player.UserIDString, args));

        private static string FormatSpan(double seconds)
        {
            if (seconds < 60) return "1m";
            int minutes = (int)(seconds / 60);
            int days = minutes / 1440, hours = minutes % 1440 / 60, mins = minutes % 60;
            if (days > 0) return hours > 0 ? $"{days}d {hours}h" : $"{days}d";
            if (hours > 0) return mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
            return $"{mins}m";
        }

        #endregion

        #region Configuration

        private Configuration config;
        private bool configBroken;

        private class Configuration
        {
            [JsonProperty("Wage per day (scrap)")]
            public int WagePerDay = 100;

            [JsonProperty("Maximum days of wages a player can hold in advance")]
            public int MaxPrepaidDays = 7;

            [JsonProperty("Grace period after the wage runs out (days the farm hand waits, without working, before it leaves)")]
            public float GraceDays = 2f;

            [JsonProperty("Maximum farm hands posted on the server at once")]
            public int MaxFarmHands = 20;

            [JsonProperty("Maximum farm hands per farm (one tool cupboard)")]
            public int MaxHandsPerFarm = 4;

            [JsonProperty("Require permission to hire a farm hand (farmhand.use)")]
            public bool RequirePermission = false;

            [JsonProperty("Take wages in scrap by command when PublicWorks is not loaded (/farmhand pay)")]
            public bool PayByCommand = true;

            [JsonProperty("Farm hand display name")]
            public string DisplayName = "Farm Hand";

            [JsonProperty("Farm hand NPC prefab path")]
            public string Prefab = NpcPrefab;

            [JsonProperty("Farm hand NPC fallback prefab path")]
            public string PrefabFallback = NpcPrefabFallback;

            [JsonProperty("Farm hand outfit (item shortnames, optional @skinId)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Outfit = new List<string>
            {
                "hat.boonie", "burlap.shirt", "burlap.trousers", "burlap.gloves", "shoes.boots"
            };

            [JsonProperty("Outfit per role (planter, stockman, yard, carter; a role missing here, or the plain farm hand, wears the outfit above)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, List<string>> OutfitByRole = new Dictionary<string, List<string>>
            {
                ["planter"] = new List<string> { "hat.boonie", "burlap.shirt", "burlap.trousers", "burlap.gloves", "shoes.boots" },
                ["stockman"] = new List<string> { "hat.beenie", "tshirt.long", "burlap.trousers", "burlap.gloves", "shoes.boots" },
                ["yard"] = new List<string> { "hat.cap", "burlap.shirt", "burlap.trousers", "burlap.gloves", "shoes.boots" },
                ["carter"] = new List<string> { "mask.bandana", "hat.boonie", "tshirt", "burlap.trousers", "shoes.boots" },
            };

            [JsonProperty("Farm hand turns to face players within this range (meters, 0 = disabled)")]
            public float FaceRange = 5f;

            [JsonProperty("Reach when using the farm hand (meters)")]
            public float UseDistance = 2f;

            [JsonProperty("Seconds between jobs (one harvest, planting or watering per job)")]
            public float JobSeconds = 15f;

            [JsonProperty("Farm radius: how far from its post the farm hand looks for planters and boxes (meters)")]
            public float FarmRadius = 30f;

            [JsonProperty("Default planter water level below which the farm hand waters (%)")]
            public int DefaultMinWater = 40;

            [JsonProperty("Default planter water level the farm hand fills to (%)")]
            public int DefaultMaxWater = 70;

            [JsonProperty("Walk to each job when a player is within this range of the post (meters, 0 = never walk, work in place)")]
            public float WalkRange = 40f;

            [JsonProperty("Walking speed (meters per second)")]
            public float WalkSpeed = 1.8f;

            [JsonProperty("Water fetched per trip to the river (ml)")]
            public int WaterPerTrip = 10000;

            [JsonProperty("Farthest river or lake the farm hand will walk to for water (meters)")]
            public float WaterRange = 400f;

            [JsonProperty("Fetch water when the container is below this share of full (0-1)")]
            public float FetchBelow = 0.25f;

            [JsonProperty("Default number of seeds and clones to keep per crop in the supply box (eats produce or takes clones to make up the shortfall; 0 = don't)")]
            public int DefaultKeepSeeds = 10;

            [JsonProperty("Go to the Farmers Market only when the load is worth at least this much scrap")]
            public int MarketMinLoad = 50;

            [JsonProperty("Farthest Farmers Market stall the farm hand will walk to (meters)")]
            public float MarketRange = 3000f;

            [JsonProperty("Seconds spent trading at the market per run")]
            public float MarketSeconds = 60f;

            [JsonProperty("What the livestock vendor charges for a calf (scrap; the game's own price on this build)")]
            public int CalfPrice = 300;

            [JsonProperty("What the livestock vendor charges for a lamb (scrap)")]
            public int LambPrice = 150;

            [JsonProperty("Farthest livestock vendor (Ranch, Large Barn) the farm hand will walk to (meters)")]
            public float VendorRange = 5000f;

            [JsonProperty("Seconds between milking rounds (the whole herd is milked together, so the milk stacks)")]
            public float MilkRoundSeconds = 300f;
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                configBroken = false;
            }
            catch
            {
                PrintError($"Config file is invalid JSON — running on defaults until it's fixed. " +
                           $"The file was left untouched: repair oxide/config/{Name}.json (check quotes and commas) and run oxide.reload {Name}.");
                LoadDefaultConfig();
                configBroken = true;
            }
            if (config.Outfit == null) config.Outfit = new List<string>();
            if (config.OutfitByRole == null) config.OutfitByRole = new Dictionary<string, List<string>>();
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            if (configBroken)
            {
                PrintWarning($"Not saving config — oxide/config/{Name}.json is invalid JSON and writing would overwrite it. Fix the file and reload the plugin.");
                return;
            }
            Config.WriteObject(config);
        }

        private int Wage => Mathf.Max(0, config.WagePerDay);
        private int MaxHands => Mathf.Max(0, config.MaxFarmHands);
        private int MaxPerFarm => Mathf.Clamp(config.MaxHandsPerFarm, 1, 10);
        private double GraceSeconds => Math.Max(0, config.GraceDays) * DaySeconds;
        private float FarmRadius => Mathf.Clamp(config.FarmRadius, 5f, 100f);
        private int DefaultMinWater => Mathf.Clamp(config.DefaultMinWater, 0, 100);
        private int DefaultMaxWater => Mathf.Clamp(config.DefaultMaxWater, DefaultMinWater, 100);
        private int DefaultKeepSeeds => Mathf.Clamp(config.DefaultKeepSeeds, 0, 500);

        #endregion

        #region Data

        // A farm is a tool cupboard: its boxes, its settings and the hands posted there.
        // A player may own several. Wages are per owner (Account) and outlive the farm.
        private class FarmData
        {
            public ulong Owner;
            public ulong Cupboard;     // the tool cupboard's net id (the key in Farms)

            // The boxes the owner marked (net ids, 0 = none): harvest goes to Output,
            // seeds and fertiliser come from Supply, water from the Water container.
            public ulong Supply, Water, Compost, Fuel;
            public ulong Takings;      // optional: where market scrap and the vendor's pay go (else the outputs)

            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> Outputs = new List<ulong>();

            // Duties, each a checkbox on the panel.
            public bool Harvest = true, Replant = true, Watering = true, Fertilise = true, Composting = true;
            public bool Chickens = true, Honey = true, FetchWater = true, Livestock = true, Biofuel = true;

            public int Trips;
            public double LastMilkRound;
            public bool MarketRuns = true, BuySeeds = true;
            public int KeepProduce = 20;      // of each produce, kept at home
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> KeepEach = new Dictionary<string, int>();   // shortname -> kept, over KeepProduce

            // Stock caps: once the boxes hold this much of a produce the farm stops making
            // more of it (no milking, shearing, picking, collecting or replanting of it)
            // until some has gone. 0 = no cap. Per produce over the general number.
            public int StockCap = 0;
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> CapEach = new Dictionary<string, int>();
            public int MarketHours = 2;       // at least this long between runs
            public double LastMarketRun;
            public int Runs, Earned;

            // Livestock vendor runs: wool and surplus animals for whatever the vendor offers.
            public bool VendorRuns = false;
            public int AnimalsPerRun = 0;      // 0 = wool only
            public int VendorHours = 2;        // at least this long between vendor runs
            public int VendorTrips;
            public double LastVendorRun;
            public bool BuyLivestock = false;  // calves and lambs bought to keep up the numbers kept
            public int BuyPerRun = 2;

            // The kitchen's focus: bread (loaves for the animals, as feed), pies (the farm's
            // own pies, cooked meat from a barbecue) or teas (berries brewed at the
            // workbench). Bread: 0 off, 1 bread, 2 brioche when there is cream.
            public string Focus = "bread";
            public int Bake = 2;
            public int LoavesToKeep = 10;
            public int PiesToKeep = 10;
            public int TeasToKeep = 10;       // of each tea ticked
            public bool AdvancedTeas = false;
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Teas = new List<string>();   // tea shortnames to brew

            // Chickens: hatch eggs until each coop has this many; butcher one every so
            // many hours (0 = never), the flock being kept up by hatching.
            public int ChickensWanted = 4;
            public int ButcherHours = 0;
            public int DungToGenerator = 50;   // share of dung picked up that goes to the generator (%)

            // Herd: adults kept per species; surplus butchered (one an hour) when switched on.
            public bool CullLivestock = false;
            public bool Breeding = false;     // one bull, unrelated; the best kept; the worst sold; the inbred to the knife
            public int KeepCows = 2, KeepSheep = 2;
            public double LastCull;
            public double LastButcher;

            // Per-crop settings keyed by the crop's produce shortname ("corn", "cloth").
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, CropSetting> Crops = new Dictionary<string, CropSetting>();

            public int Harvested, Planted, Watered;   // lifetime tallies for the panel

            // The hands posted here.
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Worker> Hands = new List<Worker>();

            // When a hand last took a trip job (realtime): with two hands that cart, the
            // second must not plan the same run while the first is still walking out.
            [JsonIgnore] public float TripClaimedAt = -1000f;

            // 0.18 and earlier kept one farm hand per player in this same record, keyed
            // by the owner; these are read once on load and moved to a Worker and an
            // Account, then left empty.
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public double PaidUntil;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public string Home;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public float Yaw;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public ulong Face;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public string Note;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public ulong Output;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public int JobSeconds;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public List<string> Order;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public double AwayUntil;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public int AwayWater;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public string AwayKind;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public int AwayScrap, AwayGross, AwaySpent;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public Dictionary<string, int> AwayBought;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public Dictionary<string, int> AwayRewards;
        }

        // One farm hand: where it stands, what it does, and the trip it is on.
        private class Worker
        {
            public ulong Id;            // appearance seed and the NPC's userID; stable and unique
            public string Name = "";    // drawn at the hire, so the owner can tell hands apart
            public string Role = "all"; // all, planter, stockman, yard, carter
            public string Home = "";
            public float Yaw;

            // This hand's own pace (0 = the config's seconds between jobs) and the order
            // of its routine work (the owner's to arrange).
            public int JobSeconds = 0;
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Order = new List<string>();

            // A trip: away until this time (epoch seconds, 0 = at the farm), of this kind
            // (null = water), bringing back water, scrap, seeds or the vendor's goods.
            public double AwayUntil;
            public int AwayWater;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string AwayKind;
            public int AwayScrap;
            public int AwayGross, AwaySpent;   // this trip's takings and what went on seeds
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> AwayBought = new Dictionary<string, int>();
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> AwayRewards = new Dictionary<string, int>();
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> AwayAnimals = new List<string>();   // bought at the vendor: "cow:f", "sheep:m"
        }

        // A player's account: wages paid ahead for all their hands, hands hired but not
        // yet posted, and a message they were offline for.
        private class Account
        {
            public double PaidUntil;   // epoch seconds; 0 = nothing paid
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string Note;        // a lang key, shown when they next connect
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Worker> Unposted = new List<Worker>();
        }

        private class CropSetting
        {
            public bool Plant = true;
            public bool Feed = false;                  // may be fed to the animals from the harvest
            public int MinWater = -1, MaxWater = -1;   // -1 = the config default
            public int KeepSeeds = -1;                 // seeds + clones to keep in the supply box
        }

        private class StoredData
        {
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, Account> Accounts = new Dictionary<ulong, Account>();
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, FarmData> Farms = new Dictionary<ulong, FarmData>();

            // 0.18 and earlier: one record per owner. Read once, moved, then dropped.
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public Dictionary<ulong, FarmData> Hands;
        }

        private StoredData data;
        private bool dataBroken;

        private void LoadData()
        {
            try { data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name); }
            catch (Exception e)
            {
                // Never start from an empty file over a damaged one: every hire and paid wage would be lost on the next save.
                dataBroken = true;
                PrintError($"Data file oxide/data/{Name}.json could not be read ({e.Message}). Farm hands are off and nothing will be saved until it is repaired and the plugin reloaded.");
            }
            if (data == null) data = new StoredData();
            if (data.Accounts == null) data.Accounts = new Dictionary<ulong, Account>();
            if (data.Farms == null) data.Farms = new Dictionary<ulong, FarmData>();

            // 0.18 and earlier: the owner's one record becomes an account, and a farm at
            // its cupboard with one hand of every duty.
            if (data.Hands != null)
            {
                int moved = 0;
                foreach (var pair in data.Hands)
                {
                    var old = pair.Value;
                    if (old == null) continue;
                    var account = AccountOf(pair.Key, true);
                    account.PaidUntil = Math.Max(account.PaidUntil, old.PaidUntil);
                    if (string.IsNullOrEmpty(account.Note)) account.Note = old.Note;
                    if (old.Cupboard != 0 && !data.Farms.ContainsKey(old.Cupboard))
                    {
                        var worker = new Worker
                        {
                            Id = old.Face, Role = "all", Home = old.Home ?? "", Yaw = old.Yaw, JobSeconds = old.JobSeconds,
                            Order = old.Order ?? new List<string>(),
                            AwayUntil = old.AwayUntil, AwayWater = old.AwayWater, AwayKind = old.AwayKind,
                            AwayScrap = old.AwayScrap, AwayGross = old.AwayGross, AwaySpent = old.AwaySpent,
                            AwayBought = old.AwayBought ?? new Dictionary<string, int>(),
                            AwayRewards = old.AwayRewards ?? new Dictionary<string, int>(),
                        };
                        old.Owner = pair.Key;
                        old.Hands = new List<Worker> { worker };
                        data.Farms[old.Cupboard] = old;
                        moved++;
                    }
                    old.PaidUntil = 0; old.Home = null; old.Yaw = 0; old.Face = 0; old.Note = null; old.JobSeconds = 0;
                    old.Order = null; old.AwayUntil = 0; old.AwayWater = 0; old.AwayKind = null;
                    old.AwayScrap = old.AwayGross = old.AwaySpent = 0; old.AwayBought = null; old.AwayRewards = null;
                }
                data.Hands = null;
                if (moved > 0) Puts($"Data from 0.18: {moved} farm hand(s) moved to their farms.");
            }

            foreach (var farm in data.Farms.Values)
            {
                if (farm.Crops == null) farm.Crops = new Dictionary<string, CropSetting>();
                if (farm.Outputs == null) farm.Outputs = new List<ulong>();
                if (farm.KeepEach == null) farm.KeepEach = new Dictionary<string, int>();
                if (farm.CapEach == null) farm.CapEach = new Dictionary<string, int>();
                if (farm.Hands == null) farm.Hands = new List<Worker>();
                if (farm.Teas == null) farm.Teas = new List<string>();
                if (FocusOf(farm.Focus) == null) farm.Focus = "bread";
                if (farm.Output != 0)
                {
                    if (!farm.Outputs.Contains(farm.Output)) farm.Outputs.Add(farm.Output);
                    farm.Output = 0;
                }
                foreach (var worker in farm.Hands) MendWorker(worker);
            }
            foreach (var account in data.Accounts.Values)
            {
                if (account.Unposted == null) account.Unposted = new List<Worker>();
                foreach (var worker in account.Unposted) MendWorker(worker);
            }
        }

        private void MendWorker(Worker worker)
        {
            if (RoleOf(worker.Role) == null) worker.Role = "all";
            if (string.IsNullOrEmpty(worker.Name)) worker.Name = DrawName(0);
            if (worker.Order == null) worker.Order = new List<string>();
            if (worker.AwayBought == null) worker.AwayBought = new Dictionary<string, int>();
            if (worker.AwayRewards == null) worker.AwayRewards = new Dictionary<string, int>();
            if (worker.AwayAnimals == null) worker.AwayAnimals = new List<string>();
            if (worker.Home == null) worker.Home = "";
        }

        // The file is written with the world save (and on unload), not on every change:
        // a crash then rolls farm hands and player inventories back to the same moment.
        private void SaveData()
        {
            if (dataBroken) return;
            Interface.Oxide.DataFileSystem.WriteObject(Name, data);
        }

        private static double Now => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

        private static readonly string[] Focuses = { "bread", "pies", "teas" };

        private static string FocusOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (var focus in Focuses) if (focus == text) return focus;
            return null;
        }

        // Roles are presets over the duties: which parts of the farm this hand sees to.
        private static readonly string[] Roles = { "all", "planter", "stockman", "yard", "carter" };

        private static string RoleOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return "all";
            text = text.ToLower();
            foreach (var role in Roles) if (role == text) return role;
            return null;
        }

        private static bool Does(Worker worker, string area) => worker == null || worker.Role == "all" || worker.Role == area;

        // Which role a group of the routine belongs to.
        private static string AreaOf(string group) =>
            group == "animals" || group == "knife" ? "stockman" : group == "bake" || group == "muck" ? "yard" : "planter";

        // Hands get a short country name; the owner's hands get different ones while
        // the list lasts.
        private static readonly string[] Names =
        {
            "Tom", "Bess", "Alf", "Maud", "Ned", "Peg", "Sid", "Dot", "Jem", "Nell", "Wat", "Fen", "Hob", "Mab",
            "Rob", "Tess", "Hal", "Meg", "Gil", "Nan", "Abe", "Flo", "Ike", "Liv", "Zeb", "Una", "Ivy", "Lou",
            "Joss", "Kit", "Pip", "Wren", "Bram", "Elsie", "Otto", "Hattie", "Reuben", "Ada", "Cy", "Mo",
        };

        private string DrawName(ulong owner)
        {
            var taken = new HashSet<string>();
            if (owner != 0)
            {
                foreach (var farm in data.Farms.Values)
                    if (farm.Owner == owner) foreach (var worker in farm.Hands) taken.Add(worker.Name);
                var account = AccountOf(owner);
                if (account != null) foreach (var worker in account.Unposted) taken.Add(worker.Name);
            }
            for (int tries = 0; tries < 20; tries++)
            {
                string name = Names[UnityEngine.Random.Range(0, Names.Length)];
                if (!taken.Contains(name)) return name;
            }
            return Names[UnityEngine.Random.Range(0, Names.Length)];
        }

        // "Tom the stockman", for messages.
        private string NameOf(Worker worker, string uid) => Msg("Ui.Named", uid, worker.Name, Msg("Role." + worker.Role, uid));

        private ulong NewWorkerId()
        {
            for (int tries = 0; tries < 100; tries++)
            {
                ulong id = FaceSeedBase + (ulong)UnityEngine.Random.Range(1, int.MaxValue);
                if (FindWorker(id) == null) return id;
            }
            return FaceSeedBase + (ulong)UnityEngine.Random.Range(1, int.MaxValue);
        }

        private Worker FindWorker(ulong id)
        {
            if (id == 0) return null;
            foreach (var farm in data.Farms.Values)
                foreach (var worker in farm.Hands) if (worker.Id == id) return worker;
            foreach (var account in data.Accounts.Values)
                foreach (var worker in account.Unposted) if (worker.Id == id) return worker;
            return null;
        }

        private static bool TryParsePosition(string text, out Vector3 position)
        {
            position = Vector3.zero;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            float x, y, z;
            if (parts.Length != 3
                || !float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x)
                || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y)
                || !float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
                return false;
            position = new Vector3(x, y, z);
            return true;
        }

        private static string FormatPosition(Vector3 v) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2}", v.x, v.y, v.z);

        #endregion

        #region Wage

        private enum WageState { None, Paid, Grace, Lapsed }

        // Wages are per owner: one account, one bill, every hand on the books (posted
        // or not) at the daily wage each.
        private Account AccountOf(ulong userId, bool create = false)
        {
            Account account;
            if (data.Accounts.TryGetValue(userId, out account)) return account;
            if (!create) return null;
            account = new Account();
            data.Accounts[userId] = account;
            return account;
        }

        private FarmData FarmOf(ulong cupboardId)
        {
            FarmData farm;
            return cupboardId != 0 && data.Farms.TryGetValue(cupboardId, out farm) ? farm : null;
        }

        private List<FarmData> FarmsOf(ulong owner)
        {
            var farms = new List<FarmData>();
            foreach (var farm in data.Farms.Values) if (farm.Owner == owner) farms.Add(farm);
            return farms;
        }

        // The farm whose cupboard the player stands in, when it is theirs.
        private FarmData FarmHere(BasePlayer player)
        {
            var cupboard = player.GetBuildingPrivilege();
            if (cupboard == null || cupboard.net == null) return null;
            var farm = FarmOf(cupboard.net.ID.Value);
            return farm != null && farm.Owner == player.userID ? farm : null;
        }

        private int HandsOf(ulong owner)
        {
            int count = 0;
            foreach (var farm in data.Farms.Values) if (farm.Owner == owner) count += farm.Hands.Count;
            var account = AccountOf(owner);
            if (account != null) count += account.Unposted.Count;
            return count;
        }

        private int PostedCount()
        {
            int count = 0;
            foreach (var farm in data.Farms.Values) count += farm.Hands.Count;
            return count;
        }

        private int DailyWage(ulong owner) => Wage * Mathf.Max(1, HandsOf(owner));

        private double PaidUntil(ulong owner)
        {
            var account = AccountOf(owner);
            return account != null ? account.PaidUntil : 0;
        }

        private WageState StateOf(ulong owner)
        {
            bool hands = HandsOf(owner) > 0;
            if (Wage <= 0) return hands ? WageState.Paid : WageState.None;
            double paidUntil = PaidUntil(owner);
            // Hands on the books but never paid (the wage was switched on after the hire): due at once.
            if (paidUntil <= 0) return hands ? WageState.Lapsed : WageState.None;
            double now = Now;
            if (now < paidUntil) return WageState.Paid;
            return now < paidUntil + GraceSeconds ? WageState.Grace : WageState.Lapsed;
        }

        // How many more whole days this player may buy right now.
        private int DaysPurchasable(ulong owner)
        {
            double paidAhead = Math.Max(0, PaidUntil(owner) - Now);
            return Mathf.Max(0, (int)Math.Floor((Math.Max(1, config.MaxPrepaidDays) * DaySeconds - paidAhead) / DaySeconds + 0.0001));
        }

        // Paying after the wage ran out starts from now; there is no back pay.
        private double ExtendWage(ulong userId, double days)
        {
            var account = AccountOf(userId, true);
            account.PaidUntil = Math.Max(account.PaidUntil, Now) + days * DaySeconds;
            return account.PaidUntil - Now;
        }

        private bool MayUse(BasePlayer player) =>
            !config.RequirePermission || permission.UserHasPermission(player.UserIDString, PermUse);

        private bool OfficeOpen => PublicWorks != null && PublicWorks.IsLoaded;

        // A word for the owner: now when they are online, else kept for their next visit.
        private void Notify(ulong owner, string noteKey)
        {
            if (string.IsNullOrEmpty(noteKey)) return;
            var account = AccountOf(owner, true);
            var player = BasePlayer.FindByID(owner);
            if (player != null && player.IsConnected)
            {
                string kept = account.Note;
                account.Note = noteKey;
                TellNote(player, account);
                account.Note = kept;
                return;
            }
            if (string.IsNullOrEmpty(account.Note)) account.Note = noteKey;
        }

        private void TellStatus(BasePlayer player)
        {
            ulong owner = player.userID;
            string uid = player.UserIDString;
            var account = AccountOf(owner);
            TellNote(player, account);
            var state = StateOf(owner);
            var farms = FarmsOf(owner);
            int unposted = account != null ? account.Unposted.Count : 0;

            if (farms.Count == 0 && unposted == 0)
            {
                if (state == WageState.Paid) Reply(player, "Status.NotPosted", FormatSpan(PaidUntil(owner) - Now));
                else Reply(player, "Status.None", Wage);
                return;
            }

            foreach (var farm in farms)
            {
                Reply(player, "Status.Farm", GridOf(farm), RolesOf(farm.Hands, uid));
                foreach (var worker in farm.Hands)
                    if (worker.AwayUntil > Now)
                        ReplyRaw(player, worker.AwayKind == "market" ? "Ui.AwayMarket" : worker.AwayKind == "vendor" ? "Ui.AwayVendor" : "Ui.Away", FormatSpan(worker.AwayUntil - Now));
            }
            if (unposted > 0) Reply(player, "Status.Unposted", RolesOf(account.Unposted, uid));

            if (state == WageState.Paid)
            {
                Reply(player, "Status.Paid", FormatSpan(Wage <= 0 ? DaySeconds : PaidUntil(owner) - Now), DailyWage(owner), HandsOf(owner));
                return;
            }
            Reply(player, "Status.Unpaid", FormatSpan(PaidUntil(owner) + GraceSeconds - Now));
            TellWhereToPay(player);
        }

        private string RolesOf(List<Worker> workers, string uid)
        {
            string list = null;
            foreach (var worker in workers)
                list = (list == null ? "" : list + ", ") + NameOf(worker, uid);
            return list ?? Msg("Role.none", uid);
        }

        private void TellWhereToPay(BasePlayer player)
        {
            int wage = DailyWage(player.userID);
            if (OfficeOpen)
            {
                string grid = null;
                int phone = 0;
                try
                {
                    grid = PublicWorks.Call("GetOfficeGrid") as string;
                    object number = PublicWorks.Call("GetOfficePhoneNumber");
                    if (number != null) phone = Convert.ToInt32(number);
                }
                catch { }
                if (string.IsNullOrEmpty(grid)) ReplyRaw(player, "Pay.Office", wage);
                else ReplyRaw(player, "Pay.OfficeGrid", wage, grid);
                if (phone > 0) ReplyRaw(player, "Pay.Phone", phone);
            }
            else if (config.PayByCommand)
                ReplyRaw(player, "Pay.Command", wage);
        }

        private void TellNote(BasePlayer player, Account account)
        {
            if (account == null || string.IsNullOrEmpty(account.Note)) return;
            string note = account.Note;
            account.Note = null;
            if (note.StartsWith("Box.Gone."))
            {
                string which = note.Substring("Box.Gone.".Length);
                Reply(player, "Box.Gone", Msg("Box.Name." + which, player.UserIDString), which);
            }
            else if (note.StartsWith("Vendor."))
            {
                string list = note.Substring("Vendor.".Length);
                Reply(player, "Vendor.Returned", list.Length > 0 ? list : Msg("Vendor.Nothing", player.UserIDString));
            }
            else if (note.StartsWith("Market."))
            {
                var parts = note.Split('.');
                int gross = 0, spent = 0, seeds = 0, net = 0;
                if (parts.Length > 1) int.TryParse(parts[1], out gross);
                if (parts.Length > 2) int.TryParse(parts[2], out spent);
                if (parts.Length > 3) int.TryParse(parts[3], out seeds);
                if (parts.Length > 4) int.TryParse(parts[4], out net);
                Reply(player, "Market.Returned", gross, seeds > 0 ? Msg("Market.Bought", player.UserIDString, spent, seeds) : "", net);
            }
            else
                Reply(player, note);
        }

        private string GridOf(FarmData farm)
        {
            var cupboard = FindCupboard(farm.Cupboard);
            if (cupboard != null) return MapHelper.PositionToString(cupboard.transform.position);
            Vector3 home;
            return farm.Hands.Count > 0 && TryParsePosition(farm.Hands[0].Home, out home) ? MapHelper.PositionToString(home) : "?";
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermAdmin, this);
            permission.RegisterPermission(PermUse, this);
            LoadData();
            WatchHands();   // nothing is posted until the server is up
            Puts($"FarmHand v{Version} loaded - by LowPopLabs - ko-fi.com/lowpoplabs");
        }

        private void OnNewSave(string filename)
        {
            if (data == null || dataBroken) return;
            int unposted = 0;
            foreach (var farm in new List<FarmData>(data.Farms.Values))
            {
                foreach (var worker in new List<Worker>(farm.Hands)) { Unpost(farm, worker, null); unposted++; }
                if (unposted > 0) Notify(farm.Owner, "Left.Wipe");
            }
            data.Farms.Clear();
            SaveData();
            if (unposted > 0) Puts($"Map wipe: {unposted} farm hand(s) are no longer posted. Paid wages and the hands carry over.");
        }

        private void OnServerInitialized()
        {
            BuildCropList();
            Sweep();
            sweepTimer = timer.Every(60f, Sweep);
            if (config.FaceRange > 0f) faceTimer = timer.Every(0.5f, FaceTick);
            workTimer = timer.Every(1f, WorkTick);
            RegisterBill();
        }

        private void OnPublicWorksReady() => RegisterBill();   // PublicWorks (re)loaded

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            sweepTimer?.Destroy();
            faceTimer?.Destroy();
            workTimer?.Destroy();
            walkTimer?.Destroy();
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, UiPanelName);
            foreach (var userId in new List<ulong>(live.Keys))
                Despawn(userId);
            SaveData();
            if (OfficeOpen) PublicWorks.Call("UnregisterBillable", this, BillKey);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            var account = data != null && !dataBroken ? AccountOf(player.userID) : null;
            if (account == null || string.IsNullOrEmpty(account.Note)) return;
            ulong userId = player.userID;
            timer.Once(10f, () =>
            {
                var online = BasePlayer.FindByID(userId);
                if (online != null && online.IsConnected) TellNote(online, AccountOf(userId));
            });
        }

        // The farm hand is a worker, not a guard: nothing hurts it and nothing targets it.
        private object OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (IsHand(player)) return true;
            return null;
        }

        private object OnNpcTarget(BaseEntity npc, BaseEntity target)
        {
            if (IsHand(npc) || IsHand(target)) return true;
            return null;
        }

        // NpcSpawn (BetterNpc and friends) picks targets with its own scan and asks
        // this hook instead of OnNpcTarget; only a bool answer counts (false = never).
        // Declared on base types so the plugin compiles when NpcSpawn is absent.
        private object OnCustomNpcTarget(BaseEntity npc, BaseEntity target)
        {
            if (IsHand(target)) return false;
            return null;
        }

        private object CanBeTargeted(BasePlayer player, MonoBehaviour turret)
        {
            if (IsHand(player)) return false;
            return null;
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input == null || !input.WasJustPressed(BUTTON.USE)) return;
            if (player == null || !player.userID.IsSteamId() || player.IsDead() || player.IsSleeping()) return;

            float reach = Mathf.Max(1f, config.UseDistance);
            foreach (var hand in live.Values)
            {
                if (hand.Npc == null || hand.Npc.IsDestroyed) continue;
                Vector3 chest = hand.Npc.transform.position + Vector3.up * 1.4f;
                Vector3 toHand = chest - player.eyes.position;
                if (toHand.sqrMagnitude > reach * reach) continue;
                if (Vector3.Dot(player.eyes.HeadForward(), toHand.normalized) < 0.8f) continue;   // looking at it, not past it
                UseHand(player, hand);
                return;
            }
        }

        private Timer sweepTimer, faceTimer, workTimer, walkTimer;

        #endregion

        #region The farm hand in the world

        // A posted farm hand: an NPC that is spawned unsaved and removed on unload, so
        // nothing of it is ever in the world save. The record in the data file is what lasts.
        private class LiveHand
        {
            public ulong Owner;
            public Worker Worker;
            public FarmData Farm;
            public BasePlayer Npc;
            public Vector3 Home;
            public float HomeYaw, LastYaw;

            // Work: the planters found around the post, and the job in hand.
            public List<PlanterBox> Planters = new List<PlanterBox>();
            public List<ChickenCoop> Coops = new List<ChickenCoop>();
            public List<Beehive> Hives = new List<Beehive>();
            public List<HitchTrough> Troughs = new List<HitchTrough>();
            public List<BiofuelGenerator> Generators = new List<BiofuelGenerator>();
            public bool Busy;                 // stirring: stays put until done
            public float PlantersScannedAt = -1000f;
            public Vector3 WaterPoint;        // nearest fresh water, or zero when none in range
            public bool WaterSearched;
            public Vector3 MarketPoint;       // nearest market stall, or zero
            public bool MarketSearched;
            public float MarketCheckedAt = -1000f;
            public float VendorCheckedAt = -1000f;
            public string JobGroup = "";       // which step of FindJob produced the job in hand
            public string LastGroup = "";      // the step that produced the last job, and how many in a row
            public string LastJobName = "";
            public int SameGroupRun;
            public Dictionary<string, int> JobTally = new Dictionary<string, int>();   // jobs done per step since posting
            public LivestockVendor Vendor;    // nearest livestock vendor, or null
            public bool VendorSearched;
            public List<CookingWorkbench> Workbenches = new List<CookingWorkbench>();
            public List<BaseOven> Ovens = new List<BaseOven>();   // barbecues and campfires: anything that cooks
            public float NextJobAt;
            public Vector3 WalkTarget;
            public Action OnArrive;
            public bool Walking;
            public bool AwayFromHome;

            // The route in hand: path corners, the owner's doors, the one being passed.
            public List<Vector3> Corners = new List<Vector3>();
            public int CornerIndex;
            public List<Door> Doors = new List<Door>();
            public Door PassDoor;
            public float WalkStartedAt, ProgressAt;
            public Vector3 ProgressPos;
        }

        private readonly Dictionary<ulong, LiveHand> live = new Dictionary<ulong, LiveHand>();
        private readonly HashSet<ulong> handNetIds = new HashSet<ulong>();

        private bool IsHand(BaseEntity entity) =>
            entity != null && entity.net != null && handNetIds.Contains(entity.net.ID.Value);

        // The hooks below fire for every player and NPC on the server, so they are
        // only listened to while a farm hand is standing somewhere.
        private void WatchHands()
        {
            if (live.Count > 0)
            {
                Subscribe(nameof(OnPlayerInput));
                Subscribe(nameof(OnEntityTakeDamage));
                Subscribe(nameof(OnNpcTarget));
                Subscribe(nameof(OnCustomNpcTarget));
                Subscribe(nameof(CanBeTargeted));
            }
            else
            {
                Unsubscribe(nameof(OnPlayerInput));
                Unsubscribe(nameof(OnEntityTakeDamage));
                Unsubscribe(nameof(OnNpcTarget));
                Unsubscribe(nameof(OnCustomNpcTarget));
                Unsubscribe(nameof(CanBeTargeted));
            }
        }

        private static BuildingPrivlidge FindCupboard(ulong netId)
        {
            if (netId == 0) return null;
            var cupboard = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BuildingPrivlidge;
            return cupboard != null && !cupboard.IsDestroyed ? cupboard : null;
        }

        private bool Spawn(FarmData farm, Worker worker)
        {
            // Clients derive face and gender from userID — set it BEFORE Spawn() so the
            // appearance networks with the create. 0.1–0.9 used Steam-range seeds, which
            // other plugins mistook for a player.
            if (worker.Id == 0 || worker.Id >= SteamIdFloor) worker.Id = NewWorkerId();
            Despawn(worker.Id);
            Vector3 home;
            if (!TryParsePosition(worker.Home, out home)) return false;

            var rotation = Quaternion.Euler(0f, worker.Yaw, 0f);
            var entity = GameManager.server.CreateEntity(
                string.IsNullOrEmpty(config.Prefab) ? NpcPrefab : config.Prefab, home, rotation);
            if (entity == null)
                entity = GameManager.server.CreateEntity(
                    string.IsNullOrEmpty(config.PrefabFallback) ? NpcPrefabFallback : config.PrefabFallback, home, rotation);
            var npc = entity as BasePlayer;
            if (npc == null)
            {
                if (entity != null) UnityEngine.Object.Destroy(entity.gameObject);
                PrintError("Failed to create the farm hand NPC from both prefabs.");
                return false;
            }

            npc.userID = worker.Id;
            npc.enableSaving = false;
            npc.Spawn();
            npc.displayName = string.IsNullOrEmpty(worker.Name)
                ? (string.IsNullOrEmpty(config.DisplayName) ? "Farm Hand" : config.DisplayName)
                : Msg("Ui.Named", null, worker.Name, Msg("Role." + worker.Role, null));
            Pacify(npc);
            Dress(npc, worker.Role);

            var liveHand = new LiveHand { Owner = farm.Owner, Worker = worker, Farm = farm, Npc = npc, Home = home, HomeYaw = worker.Yaw, LastYaw = worker.Yaw };
            live[worker.Id] = liveHand;
            handNetIds.Add(npc.net.ID.Value);
            WatchHands();

            Pin(liveHand);
            npc.SendNetworkUpdateImmediate();
            // The nav agent can warp an NPC to the nearest navmesh (the roof, the ground
            // outside) a moment after spawn — re-assert the spot once settled.
            timer.Once(2f, () => Pin(liveHand));
            return true;
        }

        private void Despawn(ulong workerId)
        {
            LiveHand hand;
            if (!live.TryGetValue(workerId, out hand)) return;
            live.Remove(workerId);
            CloseDoor(hand);
            if (hand.Npc != null && !hand.Npc.IsDestroyed)
            {
                if (hand.Npc.net != null) handNetIds.Remove(hand.Npc.net.ID.Value);
                PutDownCarried(hand);
                hand.Npc.Kill();
            }
            WatchHands();
        }

        // The scientist prefabs come with a combat brain — shut it down completely so the
        // farm hand never targets, moves, or shoots, and take its weapons away.
        private void Pacify(BasePlayer npc)
        {
            try
            {
                var brain = npc.GetComponent<BaseAIBrain>();
                if (brain != null)
                {
                    brain.SetThinkMode(AIThinkMode.None);
                    brain.SetEnabled(false);
                }
            }
            catch (Exception e) { PrintWarning($"Could not disable the farm hand's brain: {e.Message}"); }

            try { npc.inventory?.Strip(); }
            catch (Exception e) { PrintWarning($"Could not strip the farm hand's inventory: {e.Message}"); }

            // No walkie-talkie: the scientist prefab plays radio chatter every 10-15 s and
            // an idle check re-queues it every 20 s. With no effects to play, the chatter
            // method returns before it can re-queue itself.
            try
            {
                var scientist = npc as ScientistNPC;
                if (scientist != null)
                {
                    scientist.RadioChatterEffects = new GameObjectRef[0];
                    scientist.CancelInvoke(scientist.PlayRadioChatter);
                    scientist.CancelInvoke(scientist.IdleCheck);
                }
            }
            catch (Exception e) { PrintWarning($"Could not silence the farm hand's radio: {e.Message}"); }
        }

        // "shortname" or "shortname@skinId", the same outfit convention as the Public Works clerk.
        private void Dress(BasePlayer npc, string role)
        {
            var wear = npc.inventory?.containerWear;
            if (wear == null) return;
            foreach (var item in new List<Item>(wear.itemList)) if (item != null) item.Remove();

            List<string> outfit;
            if (role == null || role == "all" || config.OutfitByRole == null || !config.OutfitByRole.TryGetValue(role, out outfit) || outfit == null || outfit.Count == 0)
                outfit = config.Outfit;
            foreach (var entry in outfit)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                string shortname = entry;
                ulong skin = 0;
                int at = entry.IndexOf('@');
                if (at > 0)
                {
                    shortname = entry.Substring(0, at);
                    ulong.TryParse(entry.Substring(at + 1), out skin);
                }
                var item = ItemManager.CreateByName(shortname, 1, skin);
                if (item == null) { PrintWarning($"Outfit item '{shortname}' unknown — skipped."); continue; }
                if (!item.MoveToContainer(wear)) item.Remove();
            }
            npc.SendNetworkUpdate();
        }

        // Hold the farm hand exactly where its owner posted it: navmesh inside player
        // bases is poor, and the prefab's nav agent would otherwise snap it elsewhere.
        private void Pin(LiveHand hand)
        {
            var npc = hand.Npc;
            if (npc == null || npc.IsDestroyed) return;
            try
            {
                var agent = npc as NPCPlayer;
                if (agent != null && agent.NavAgent != null) agent.NavAgent.enabled = false;
            }
            catch { }
            npc.ServerPosition = hand.Home;
            npc.viewAngles = new Vector3(0f, hand.LastYaw, 0f);
            npc.ServerRotation = Quaternion.Euler(0f, hand.LastYaw, 0f);
            npc.SendNetworkUpdate();
        }

        // Turn each farm hand toward the nearest player in range; back to its post when alone.
        private void FaceTick()
        {
            if (live.Count == 0) return;
            float rangeSqr = config.FaceRange * config.FaceRange;
            foreach (var hand in live.Values)
            {
                var npc = hand.Npc;
                if (npc == null || npc.IsDestroyed || hand.Walking) continue;
                Vector3 standing = npc.transform.position;

                BasePlayer nearest = null;
                float bestSqr = rangeSqr;
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == null || player.IsDead()) continue;
                    float sqr = (player.transform.position - standing).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; nearest = player; }
                }

                float targetYaw = hand.AwayFromHome ? hand.LastYaw : hand.HomeYaw;
                if (nearest != null)
                {
                    Vector3 dir = nearest.transform.position - standing;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                        targetYaw = Quaternion.LookRotation(dir.normalized).eulerAngles.y;
                }

                if (Mathf.Abs(Mathf.DeltaAngle(hand.LastYaw, targetYaw)) < 3f) continue;
                hand.LastYaw = targetYaw;
                npc.viewAngles = new Vector3(0f, targetYaw, 0f);
                npc.ServerRotation = Quaternion.Euler(0f, targetYaw, 0f);
                npc.SendNetworkUpdate();
            }
        }

        // Once a minute: a farm whose cupboard is gone (or whose owner lost authority on
        // it) is struck off and its hands go back on the books; an owner whose wage ran
        // out past the grace period has every hand unposted; any hand that should be
        // standing is put back.
        private void Sweep()
        {
            if (dataBroken) return;
            foreach (var farm in new List<FarmData>(data.Farms.Values))
            {
                var cupboard = FindCupboard(farm.Cupboard);
                if (cupboard == null || !cupboard.IsAuthed(farm.Owner))
                {
                    RemoveFarm(farm, "Left.Cupboard");
                    continue;
                }

                if (StateOf(farm.Owner) == WageState.Lapsed)
                {
                    var account = AccountOf(farm.Owner, true);
                    account.PaidUntil = 0;
                    bool any = farm.Hands.Count > 0;
                    foreach (var worker in new List<Worker>(farm.Hands)) Unpost(farm, worker, null);
                    if (any) Notify(farm.Owner, "Left.Unpaid");
                    continue;
                }

                ForgetLostBoxes(farm);

                foreach (var worker in new List<Worker>(farm.Hands))
                {
                    if (worker.AwayUntil > 0)
                    {
                        if (Now < worker.AwayUntil) { Despawn(worker.Id); continue; }   // still on the road
                        Return(farm, worker);
                        continue;
                    }
                    LiveHand liveHand;
                    if (!live.TryGetValue(worker.Id, out liveHand) || liveHand.Npc == null || liveHand.Npc.IsDestroyed)
                        Spawn(farm, worker);
                }
            }
        }

        // A marked box that has been destroyed or picked up is forgotten, and the owner
        // told, so it is not silently left waiting for a box that will never come back.
        private void ForgetLostBoxes(FarmData farm)
        {
            var lost = new List<string>();
            for (int i = farm.Outputs.Count - 1; i >= 0; i--)
                if (FindBox(farm.Outputs[i]) == null) { farm.Outputs.RemoveAt(i); if (!lost.Contains("output")) lost.Add("output"); }
            if (farm.Supply != 0 && FindBox(farm.Supply) == null) { farm.Supply = 0; lost.Add("supply"); }
            if (farm.Water != 0 && FindWater(farm.Water) == null) { farm.Water = 0; lost.Add("water"); }
            if (farm.Compost != 0 && !(FindBox(farm.Compost) is Composter)) { farm.Compost = 0; lost.Add("compost"); }
            if (farm.Fuel != 0 && FindBox(farm.Fuel) == null) { farm.Fuel = 0; lost.Add("fuel"); }
            if (farm.Takings != 0 && FindBox(farm.Takings) == null) { farm.Takings = 0; lost.Add("takings"); }
            foreach (var which in lost) Notify(farm.Owner, "Box.Gone." + which);
        }

        // The hand leaves its post and goes back on the owner's books; the farm stays.
        private void Unpost(FarmData farm, Worker worker, string noteKey)
        {
            Despawn(worker.Id);
            farm.Hands.Remove(worker);
            worker.AwayUntil = 0;
            worker.AwayWater = 0;
            worker.AwayKind = null;
            worker.AwayScrap = 0;
            worker.AwayBought.Clear();
            worker.AwayRewards.Clear();
            worker.AwayAnimals.Clear();
            var account = AccountOf(farm.Owner, true);
            if (!account.Unposted.Contains(worker)) account.Unposted.Add(worker);
            Notify(farm.Owner, noteKey);
        }

        // The farm is struck off; its hands go back on the books.
        private void RemoveFarm(FarmData farm, string noteKey)
        {
            bool any = farm.Hands.Count > 0;
            foreach (var worker in new List<Worker>(farm.Hands)) Unpost(farm, worker, null);
            data.Farms.Remove(farm.Cupboard);
            if (any) Notify(farm.Owner, noteKey);
        }

        // The hand is let go for good; wages already paid stay on the account.
        private void Dismiss(ulong owner, Worker worker)
        {
            Puts($"{worker.Name} the {worker.Role} dismissed by {owner}.");
            Despawn(worker.Id);
            foreach (var farm in data.Farms.Values) farm.Hands.Remove(worker);
            var account = AccountOf(owner);
            if (account != null) account.Unposted.Remove(worker);
        }

        // Where the player stands, if a farm hand may be posted there: on something
        // solid, inside the range of a tool cupboard the player is authorised on.
        private bool TryGetPost(BasePlayer player, out BuildingPrivlidge cupboard)
        {
            cupboard = null;
            if (player.isMounted || player.IsSwimming() || !player.IsOnGround())
            {
                Reply(player, "Hire.NotStanding");
                return false;
            }
            cupboard = player.GetBuildingPrivilege();
            if (cupboard == null || cupboard.net == null || !cupboard.IsAuthed(player))
            {
                cupboard = null;
                Reply(player, "Hire.NoCupboard");
                return false;
            }
            return true;
        }

        // Posts the hand where the player stands, at the farm of that cupboard (made
        // on the spot when it is the first hand there). Replies on failure.
        private bool PostHere(BasePlayer player, Worker worker, BuildingPrivlidge cupboard)
        {
            ulong owner = player.userID;
            ulong cupboardId = cupboard.net.ID.Value;
            var farm = FarmOf(cupboardId);
            if (farm != null && farm.Owner != owner)
            {
                var other = covalence.Players.FindPlayerById(farm.Owner.ToString());
                Reply(player, "Hire.NotYours", other != null ? other.Name : "?");
                return false;
            }
            bool already = farm != null && farm.Hands.Contains(worker);
            if (farm != null && !already && farm.Hands.Count >= MaxPerFarm) { Reply(player, "Hire.FarmFull", MaxPerFarm); return false; }

            // Out of wherever it was.
            FarmData from = null;
            foreach (var candidate in data.Farms.Values) if (candidate.Hands.Contains(worker)) from = candidate;
            var account = AccountOf(owner, true);
            string oldHome = worker.Home;
            float oldYaw = worker.Yaw;

            if (farm == null)
            {
                farm = new FarmData { Owner = owner, Cupboard = cupboardId };
                data.Farms[cupboardId] = farm;
            }
            worker.Home = FormatPosition(player.transform.position);
            worker.Yaw = player.eyes.rotation.eulerAngles.y;
            if (Spawn(farm, worker))
            {
                if (from != null && from != farm) from.Hands.Remove(worker);
                account.Unposted.Remove(worker);
                if (!farm.Hands.Contains(worker)) farm.Hands.Add(worker);
                return true;
            }

            // Could not be spawned there: back where it was.
            worker.Home = oldHome;
            worker.Yaw = oldYaw;
            if (from != null) Spawn(from, worker);
            if (farm.Hands.Count == 0 && from != farm) data.Farms.Remove(cupboardId);
            Reply(player, "Hire.Failed");
            return false;
        }

        // The player's nearest posted hand within reach, for move and dismiss.
        private LiveHand NearestHand(BasePlayer player, float range)
        {
            LiveHand best = null;
            float bestSqr = range * range;
            foreach (var liveHand in live.Values)
            {
                if (liveHand.Owner != player.userID || liveHand.Npc == null || liveHand.Npc.IsDestroyed) continue;
                float sqr = (liveHand.Npc.transform.position - player.transform.position).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = liveHand; }
            }
            return best;
        }

        private void UseHand(BasePlayer player, LiveHand liveHand)
        {
            if (liveHand.Owner != player.userID)
            {
                var owner = covalence.Players.FindPlayerById(liveHand.Owner.ToString());
                Reply(player, "Use.Other", owner != null ? owner.Name : "?");
                return;
            }
            panelHand[player.userID] = liveHand.Worker.Id;
            ShowPanel(player);
        }

        #endregion

        #region Crops

        // Every plant that can be grown from an item: seed and clone both map to one crop,
        // keyed by what it yields ("corn", "cloth"). Built once from the item list.
        private class Crop
        {
            public string Key, Name;
            public List<int> ItemIds = new List<int>();
        }

        private readonly List<Crop> crops = new List<Crop>();
        private readonly Dictionary<int, Crop> cropByItem = new Dictionary<int, Crop>();
        private readonly Dictionary<string, Crop> cropByKey = new Dictionary<string, Crop>();

        private void BuildCropList()
        {
            crops.Clear();
            cropByItem.Clear();
            cropByKey.Clear();
            foreach (var def in ItemManager.itemList)
            {
                var deploy = def.GetComponent<ItemModDeployable>();
                if (deploy == null || deploy.entityPrefab == null || !deploy.entityPrefab.isValid) continue;
                GameObject prefab;
                try { prefab = GameManager.server.FindPrefab(deploy.entityPrefab.resourcePath); }
                catch { continue; }
                var growable = prefab != null ? prefab.GetComponent<GrowableEntity>() : null;
                if (growable == null || growable.Properties == null || growable.Properties.pickupItem == null) continue;

                string key = growable.Properties.pickupItem.shortname;
                Crop crop;
                if (!cropByKey.TryGetValue(key, out crop))
                {
                    crop = new Crop { Key = key, Name = CropName(deploy.entityPrefab.resourcePath) };
                    crops.Add(crop);
                    cropByKey[key] = crop;
                }
                crop.ItemIds.Add(def.itemid);
                cropByItem[def.itemid] = crop;
            }
            crops.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            Puts($"{crops.Count} crop(s) known from {cropByItem.Count} seed and clone items.");
        }

        // "assets/prefabs/plants/berrry/black/black_berry.entity.prefab" -> "Black berry"
        private static string CropName(string prefabPath)
        {
            string file = prefabPath.Substring(prefabPath.LastIndexOf('/') + 1);
            int dot = file.IndexOf('.');
            if (dot > 0) file = file.Substring(0, dot);
            file = file.Replace('_', ' ');
            return file.Length > 1 ? char.ToUpper(file[0]) + file.Substring(1) : file.ToUpper();
        }

        private CropSetting SettingOf(FarmData hand, Crop crop)
        {
            CropSetting setting;
            return hand.Crops.TryGetValue(crop.Key, out setting) ? setting : null;
        }

        private CropSetting EditSetting(FarmData hand, Crop crop)
        {
            CropSetting setting;
            if (!hand.Crops.TryGetValue(crop.Key, out setting))
            {
                setting = new CropSetting();
                hand.Crops[crop.Key] = setting;
            }
            return setting;
        }

        private bool PlantsCrop(FarmData hand, Crop crop)
        {
            var setting = SettingOf(hand, crop);
            return setting == null || setting.Plant;
        }

        private int MinWater(FarmData hand, Crop crop)
        {
            var setting = SettingOf(hand, crop);
            return setting == null || setting.MinWater < 0 ? DefaultMinWater : Mathf.Clamp(setting.MinWater, 0, 100);
        }

        private bool FeedsCrop(FarmData hand, Crop crop)
        {
            var setting = SettingOf(hand, crop);
            return setting != null && setting.Feed;
        }

        private int KeepSeeds(FarmData hand, Crop crop)
        {
            var setting = SettingOf(hand, crop);
            return setting == null || setting.KeepSeeds < 0 ? DefaultKeepSeeds : Mathf.Clamp(setting.KeepSeeds, 0, 500);
        }

        private int MaxWater(FarmData hand, Crop crop)
        {
            var setting = SettingOf(hand, crop);
            int min = MinWater(hand, crop);
            return setting == null || setting.MaxWater < 0 ? Mathf.Max(min, DefaultMaxWater) : Mathf.Clamp(setting.MaxWater, min, 100);
        }

        private Crop CropOf(GrowableEntity plant)
        {
            if (plant == null || plant.Properties == null || plant.Properties.pickupItem == null) return null;
            Crop crop;
            return cropByKey.TryGetValue(plant.Properties.pickupItem.shortname, out crop) ? crop : null;
        }

        #endregion

        #region Farm work

        // The groups of routine work the owner may reorder. Carrying, hungry animals,
        // harvest and dry planters always come first; these follow in the owner's order.
        private static readonly string[] OrderKeys = { "animals", "planting", "seeds", "waterrun", "bake", "muck", "knife" };

        private static List<string> OrderOf(Worker worker)
        {
            if (worker.Order == null) worker.Order = new List<string>();
            var order = worker.Order;
            for (int i = order.Count - 1; i >= 0; i--)
                if (Array.IndexOf(OrderKeys, order[i]) < 0 || order.IndexOf(order[i]) != i) order.RemoveAt(i);
            foreach (var key in OrderKeys)
                if (!order.Contains(key)) order.Add(key);
            return order;
        }

        // Every job is done by calling the game's own logic: PickFruit, RemoveDying and
        // DoSplash are what a player's interaction calls, and planting spawns the seed's
        // own plant prefab on one of the planter's sockets, as the deploy code does.
        private class Job
        {
            public Vector3 Spot;
            public Action Do;
            public string Name;   // the function that made it and which of its jobs: for the stuck-step warning
        }

        private const string FertilizerShortname = "fertilizer";

        // A box is anything with an item container a player can loot: a wood box or
        // locker (StorageContainer), a fridge or barrel (ContainerIOEntity).
        private static ItemContainer InventoryOf(BaseEntity entity)
        {
            var storage = entity as StorageContainer;
            if (storage != null) return storage.inventory;
            var powered = entity as ContainerIOEntity;
            if (powered != null) return powered.inventory;
            return null;
        }

        private static BaseCombatEntity FindBox(ulong netId)
        {
            if (netId == 0) return null;
            var box = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BaseCombatEntity;
            return box != null && !box.IsDestroyed && !(box is PlanterBox) && InventoryOf(box) != null ? box : null;
        }

        // The output boxes that still exist, fridges first so food is kept cold.
        private static List<BaseCombatEntity> OutputsOf(FarmData hand)
        {
            var boxes = new List<BaseCombatEntity>();
            foreach (var id in hand.Outputs)
            {
                var box = FindBox(id);
                if (box == null) continue;
                if (box is Fridge) boxes.Insert(0, box);
                else boxes.Add(box);
            }
            return boxes;
        }

        // The same boxes ordered for one item: what spoils goes to a fridge first; what
        // keeps (wool, honeycomb, wheat, fuel) goes to a plain box first so the fridge's
        // slots stay for food that needs them.
        private static bool Spoils(ItemDefinition def) => def != null && def.GetComponent<ItemModFoodSpoiling>() != null;

        private static List<BaseCombatEntity> OutputsFor(List<BaseCombatEntity> outputs, ItemDefinition def)
        {
            if (Spoils(def)) return outputs;   // already fridges first
            var ordered = new List<BaseCombatEntity>(outputs.Count);
            foreach (var box in outputs) if (!(box is Fridge)) ordered.Add(box);
            foreach (var box in outputs) if (box is Fridge) ordered.Add(box);
            return ordered;
        }

        private static bool AnyRoom(List<BaseCombatEntity> outputs, ItemDefinition def, int amount)
        {
            foreach (var box in outputs)
                if (HasRoom(box, def, amount)) return true;
            return false;
        }

        private static LiquidContainer FindWater(ulong netId)
        {
            if (netId == 0) return null;
            var water = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as LiquidContainer;
            return water != null && !water.IsDestroyed ? water : null;
        }

        // One job per farm hand per tick. The tick is shared by every farm hand.
        private void WorkTick()
        {
            if (dataBroken) return;
            foreach (var farm in new List<FarmData>(data.Farms.Values))
                foreach (var worker in new List<Worker>(farm.Hands))
                    if (worker.AwayUntil > 0 && Now >= worker.AwayUntil)
                        Return(farm, worker);
            if (live.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            foreach (var liveHand in new List<LiveHand>(live.Values))
            {
                if (now < liveHand.NextJobAt) continue;
                var worker = liveHand.Worker;
                liveHand.NextJobAt = now + Mathf.Max(3f, worker != null && worker.JobSeconds > 0 ? worker.JobSeconds : config.JobSeconds);
                try { WorkOne(liveHand); }
                catch (Exception e) { PrintWarning($"Farm hand job failed: {e.Message}"); }
            }
        }

        private void WorkOne(LiveHand liveHand)
        {
            var npc = liveHand.Npc;
            if (npc == null || npc.IsDestroyed || liveHand.Walking || liveHand.Busy) return;
            var hand = liveHand.Farm;
            if (hand == null || !data.Farms.ContainsKey(hand.Cupboard) || !hand.Hands.Contains(liveHand.Worker) || StateOf(hand.Owner) != WageState.Paid) return;
            var cupboard = FindCupboard(hand.Cupboard);
            if (cupboard == null) return;

            if (Time.realtimeSinceStartup - liveHand.PlantersScannedAt > 120f)
                ScanPlanters(liveHand, cupboard);
            if (hand.Livestock) Befriend(liveHand, npc, cupboard);

            var job = FindJob(liveHand, hand);
            if (job == null)
            {
                if (liveHand.AwayFromHome) GoHome(liveHand);
                return;
            }
            int done;
            liveHand.JobTally.TryGetValue(liveHand.JobGroup, out done);
            liveHand.JobTally[liveHand.JobGroup] = done + 1;

            // The very same job sixty times running is worth a line in the log: a job that
            // never gets done starves everything after it. (The same *group* in a row means
            // nothing for a hand whose role covers one group: a stockman only ever does
            // animals.)
            string jobName = job.Name ?? "";
            if (jobName == liveHand.LastJobName) liveHand.SameGroupRun++;
            else { liveHand.LastJobName = jobName; liveHand.SameGroupRun = 1; }
            liveHand.LastGroup = liveHand.JobGroup;
            if (liveHand.SameGroupRun % 60 == 0)
            {
                string carried = null;
                if (npc.inventory != null)
                    foreach (var item in npc.inventory.containerMain.itemList)
                        if (item != null) carried = (carried == null ? "" : carried + ", ") + item.amount + " " + item.info.shortname;
                PrintWarning($"{liveHand.Worker.Name} the {liveHand.Worker.Role} has taken the same job {liveHand.SameGroupRun} times running: {liveHand.LastJobName} ('{liveHand.JobGroup}') at {MapHelper.PositionToString(job.Spot)} (carrying {carried ?? "nothing"}).");
            }

            if (ShouldWalk(liveHand)) Walk(liveHand, job.Spot, job.Do);
            else job.Do();
        }

        // Everything farmable around the post that belongs to the owner's cupboard;
        // never a neighbour's.
        private void ScanPlanters(LiveHand liveHand, BuildingPrivlidge cupboard)
        {
            liveHand.PlantersScannedAt = Time.realtimeSinceStartup;
            liveHand.Planters.Clear();
            liveHand.Coops.Clear();
            liveHand.Hives.Clear();
            liveHand.Troughs.Clear();
            liveHand.Generators.Clear();
            liveHand.Workbenches.Clear();
            liveHand.Ovens.Clear();
            var found = new List<BaseEntity>();
            Vis.Entities(liveHand.Home, FarmRadius, found, Rust.Layers.Mask.Deployed);
            var seen = new HashSet<ulong>();
            foreach (var entity in found)
            {
                if (entity == null || entity.IsDestroyed || entity.net == null || !seen.Add(entity.net.ID.Value)) continue;
                var planter = entity as PlanterBox;
                var coop = entity as ChickenCoop;
                var hive = entity as Beehive;
                var trough = entity as HitchTrough;
                var generator = entity as BiofuelGenerator;
                var workbench = entity as CookingWorkbench;
                var oven = entity as BaseOven;
                if (oven != null && (oven is Composter || oven.temperature != BaseOven.TemperatureType.Cooking)) oven = null;
                if (planter == null && coop == null && hive == null && trough == null && generator == null && workbench == null && oven == null) continue;
                if (entity.GetBuildingPrivilege() != cupboard) continue;
                if (planter != null) liveHand.Planters.Add(planter);
                else if (coop != null) liveHand.Coops.Add(coop);
                else if (hive != null) liveHand.Hives.Add(hive);
                else if (trough != null) liveHand.Troughs.Add(trough);
                else if (generator != null) liveHand.Generators.Add(generator);
                else if (workbench != null)
                {
                    liveHand.Workbenches.Add(workbench);
                    // The workbench's own grill is a child oven of its own, not found by the scan.
                    foreach (var child in workbench.children)
                    {
                        var grill = child as BaseOven;
                        if (grill != null && !grill.IsDestroyed && !liveHand.Ovens.Contains(grill)) liveHand.Ovens.Add(grill);
                    }
                }
                else if (!liveHand.Ovens.Contains(oven)) liveHand.Ovens.Add(oven);
            }
            MarkDoors(liveHand, cupboard);
            if (!recipesLogged && liveHand.Workbenches.Count > 0) LogRecipes(liveHand.Workbenches[0]);
        }

        // Once per load, the cooking workbench's recipe book to oxide/logs/FarmHand/, so
        // what a pie or a tea needs on this build can be read without a client.
        private static bool recipesLogged;

        private void LogRecipes(CookingWorkbench table)
        {
            recipesLogged = true;
            if (table == null || table.Recipes == null || table.Recipes.AllRecipes == null) return;
            foreach (var recipe in table.Recipes.AllRecipes)
            {
                if (recipe == null || recipe.ProducedItem == null) continue;
                string line = recipe.ProducedItem.shortname + " x" + recipe.ProducedItemCount + " <- ";
                if (recipe.Ingredients != null)
                    foreach (var ingredient in recipe.Ingredients)
                    {
                        if (ingredient.Ingredient == null) continue;
                        line += ingredient.Ingredient.shortname + " x" + ingredient.GetIngredientCount(recipe.ProducedItem) + ", ";
                    }
                line += "; " + recipe.MixingDuration + " s; blueprint " + recipe.RequiresBlueprint;
                LogToFile("recipes", line, this);
            }
        }

        // One job a tick, chosen in a fixed order: what hurts if left (animals with nothing
        // to eat or drink, produce ready to spoil on the plant, dry planters), then a trip
        // that is due, then the routine in the owner's order, and last the knife.
        private Job FindJob(LiveHand liveHand, FarmData hand)
        {
            var npc = liveHand.Npc;
            var worker = liveHand.Worker;
            var outputs = OutputsOf(hand);

            // 1. Anything still carried from the last job goes to the output boxes first.
            if (outputs.Count > 0 && Carrying(npc))
                return new Job { Name = "FindJob#1", Spot = Beside(outputs[0], npc), Do = () => Deliver(npc, outputs) };

            var supply = FindBox(hand.Supply);
            var compost = hand.Composting ? FindBox(hand.Compost) as Composter : null;
            var fibreBins = outputs;
            if (compost != null)
            {
                fibreBins = new List<BaseCombatEntity> { compost };
                fibreBins.AddRange(outputs);
            }
            var water = hand.Watering ? FindWater(hand.Water) : null;
            var liquid = water != null ? water.GetLiquidItem() : null;
            if (liquid != null && (liquid.info != WaterTypes.WaterItemDef || liquid.amount <= 0)) liquid = null;
            var shortOfSeeds = hand.Replant && supply != null ? ShortOfSeeds(hand, supply) : null;
            Job job;

            // 2. Animals with nothing to eat or drink.
            liveHand.JobGroup = "hungry";
            if (Does(worker, "stockman") && hand.Chickens && (job = CoopJob(liveHand, hand, npc, outputs, supply, liquid, 0)) != null) return job;
            if (Does(worker, "stockman") && hand.Livestock && (job = TroughJob(liveHand, hand, npc, supply, liquid, compost, outputs, 0)) != null) return job;

            // 3. Harvest: ripe plants picked (or cloned when short of that crop's seeds and
            //    nothing can be eaten for them), dying ones cleared.
            liveHand.JobGroup = "harvest";
            if (Does(worker, "planter") && hand.Harvest && outputs.Count > 0 && (job = HarvestJob(liveHand, hand, npc, outputs, supply, fibreBins, shortOfSeeds)) != null) return job;

            // 4. Dry planters.
            liveHand.JobGroup = "dry";
            if (Does(worker, "planter") && liquid != null && (job = WateringJob(liveHand, hand, npc, liquid)) != null) return job;

            // 5. A trip that is due: the market or the livestock vendor. Trips are timed
            //    (hours between runs) and checked every five minutes, so they must not wait
            //    for a tick with no other work, which a big herd never gives.
            liveHand.JobGroup = "trips";
            if (Does(worker, "carter") && hand.MarketRuns && (job = MarketRunJob(liveHand, hand, outputs)) != null) return job;
            if (Does(worker, "carter") && hand.VendorRuns && (job = VendorRunJob(liveHand, hand, outputs)) != null) return job;

            // 6 on: the routine, in the owner's order (the Priority tab).
            foreach (var group in OrderOf(worker))
            {
                if (!Does(worker, AreaOf(group))) continue;
                liveHand.JobGroup = group;
                switch (group)
                {
                    case "animals":   // hatch, eggs, top-ups, petting, milk, wool, honeycomb
                        if (hand.Chickens && (job = CoopJob(liveHand, hand, npc, outputs, supply, liquid, 1)) != null) return job;
                        if (hand.Livestock && (job = TroughJob(liveHand, hand, npc, supply, liquid, compost, outputs, 1)) != null) return job;
                        if (hand.Honey && (job = HoneyJob(liveHand, hand, npc, outputs)) != null) return job;
                        break;
                    case "planting":
                        if (hand.Replant && supply != null && (job = PlantingJob(liveHand, hand, npc, supply, outputs)) != null) return job;
                        if (hand.Fertilise && (supply != null || compost != null) && (job = FertilisingJob(liveHand, hand, npc, supply, compost)) != null) return job;
                        break;
                    case "seeds":     // eat one of a crop's produce for its seed
                        if (shortOfSeeds != null)
                            foreach (var crop in shortOfSeeds)
                            {
                                var fruit = FindSeedFruit(crop, outputs);
                                if (fruit == null) continue;
                                var theFruit = fruit;
                                var into = supply;
                                return new Job { Name = "FindJob#2", Spot = Beside(fruit.parent.entityOwner ?? supply, npc), Do = () => EatForSeeds(hand, npc, theFruit, into) };
                            }
                        break;
                    case "waterrun":
                        if (hand.FetchWater && (job = WaterRunJob(liveHand, hand, water, liquid)) != null) return job;
                        break;
                    case "bake":
                        if (hand.Focus == "pies" && outputs.Count > 0)
                        {
                            if ((job = BbqJob(liveHand, hand, npc, outputs, supply)) != null) return job;
                            if ((job = PieJob(liveHand, hand, npc, outputs, supply)) != null) return job;
                        }
                        else if (hand.Focus == "teas" && outputs.Count > 0)
                        {
                            if ((job = TeaJob(liveHand, hand, npc, outputs, supply)) != null) return job;
                        }
                        else if (hand.Bake > 0 && supply != null && (job = BakeJob(liveHand, hand, npc, outputs, supply)) != null) return job;
                        break;
                    case "muck":      // the generator (a stir is ten seconds and it idles without one), then dung, then the composter
                        if (hand.Biofuel && (job = GeneratorJob(liveHand, hand, npc, outputs)) != null) return job;
                        if (hand.Livestock && (job = TroughJob(liveHand, hand, npc, supply, liquid, compost, outputs, 3)) != null) return job;
                        if (compost != null && (job = CompostJob(liveHand, npc, outputs, supply, compost)) != null) return job;
                        break;
                    case "knife":
                        if (hand.Chickens && (job = CoopJob(liveHand, hand, npc, outputs, supply, liquid, 2)) != null) return job;
                        if (hand.Livestock && (job = TroughJob(liveHand, hand, npc, supply, liquid, compost, outputs, 2)) != null) return job;
                        break;
                }
            }
            return null;
        }

        private Job HarvestJob(LiveHand liveHand, FarmData hand, BasePlayer npc, List<BaseCombatEntity> outputs, BaseCombatEntity supply, List<BaseCombatEntity> fibreBins, List<Crop> shortOfSeeds)
        {
            for (int i = liveHand.Planters.Count - 1; i >= 0; i--)
            {
                var planter = liveHand.Planters[i];
                if (planter == null || planter.IsDestroyed) { liveHand.Planters.RemoveAt(i); continue; }
                foreach (var child in planter.children)
                {
                    var plant = child as GrowableEntity;
                    if (plant == null || plant.IsDestroyed) continue;
                    if (plant.State != PlantProperties.State.Ripe && plant.State != PlantProperties.State.Dying) continue;

                    var plantCrop = CropOf(plant);
                    if (plantCrop != null && shortOfSeeds != null && shortOfSeeds.Contains(plantCrop) && supply != null
                        && plant.State == PlantProperties.State.Ripe && plant.CanClone(npc) && plant.Properties.CloneItem != null
                        && FindSeedFruit(plantCrop, outputs) == null && HasRoom(supply, plant.Properties.CloneItem, 1))
                    {
                        var mother = plant;
                        var into = supply;
                        return new Job { Name = "HarvestJob#1", Spot = Beside(planter, npc), Do = () => CloneForSeeds(hand, npc, mother, into) };
                    }
                    // Over the cap: the fruit stays on the plant (and the crop is not replanted).
                    if (plant.CanPick(npc) && plant.CurrentPickAmount > 0 && !AtCap(hand, outputs, plant.Properties.pickupItem.shortname)
                        && AnyRoom(outputs, plant.Properties.pickupItem, plant.CurrentPickAmount))
                    {
                        var ripe = plant;
                        return new Job { Name = "HarvestJob#2", Spot = Beside(planter, npc), Do = () => HarvestPlant(hand, npc, ripe, outputs) };
                    }
                    if (plant.State == PlantProperties.State.Dying && plant.Properties.removeDyingItem != null
                        && AnyRoom(fibreBins, plant.Properties.removeDyingItem, 1))
                    {
                        var dying = plant;
                        return new Job { Name = "HarvestJob#3", Spot = Beside(planter, npc), Do = () => ClearPlant(npc, dying, fibreBins) };
                    }
                }
            }
            return null;
        }

        private Job WateringJob(LiveHand liveHand, FarmData hand, BasePlayer npc, Item liquid)
        {
            foreach (var planter in liveHand.Planters)
            {
                if (planter == null || planter.IsDestroyed) continue;
                int plants = 0, highestMin = -1, highestMax = -1;
                foreach (var child in planter.children)
                {
                    var plant = child as GrowableEntity;
                    if (plant == null || plant.IsDestroyed) continue;
                    plants++;
                    var crop = CropOf(plant);
                    if (crop == null) continue;
                    highestMin = Mathf.Max(highestMin, MinWater(hand, crop));
                    highestMax = Mathf.Max(highestMax, MaxWater(hand, crop));
                }
                if (plants == 0 || highestMin < 0 || planter.soilSaturationFraction * 100f >= highestMin) continue;
                int need = Mathf.FloorToInt(Mathf.Max(highestMax, highestMin) / 100f * planter.soilSaturationMax) - planter.soilSaturation;
                int take = Mathf.Min(need, liquid.amount);
                if (take <= 0) continue;
                var thirsty = planter;
                var source = liquid;
                return new Job { Name = "WateringJob#1", Spot = Beside(planter, npc), Do = () => WaterPlanter(hand, thirsty, source, take) };
            }
            return null;
        }

        private Job PlantingJob(LiveHand liveHand, FarmData hand, BasePlayer npc, BaseCombatEntity supply, List<BaseCombatEntity> outputs)
        {
            foreach (var planter in liveHand.Planters)
            {
                if (planter == null || planter.IsDestroyed) continue;
                Vector3 slot;
                Quaternion facing;
                if (!FreeSlot(planter, out slot, out facing)) continue;
                var seed = PickSeed(hand, supply, outputs);
                if (seed == null) return null;   // nothing to plant anywhere
                var bed = planter;
                return new Job { Name = "PlantingJob#1", Spot = Beside(planter, npc), Do = () => PlantSeed(hand, liveHand.Owner, bed, seed, slot, facing) };
            }
            return null;
        }

        private Job FertilisingJob(LiveHand liveHand, FarmData hand, BasePlayer npc, BaseCombatEntity supply, Composter compost)
        {
            foreach (var planter in liveHand.Planters)
            {
                if (planter == null || planter.IsDestroyed || planter.inventory == null || planter.GetFertilizerCount() > 0) continue;
                bool plants = false;
                foreach (var child in planter.children) if (child is GrowableEntity && !child.IsDestroyed) { plants = true; break; }
                if (!plants) continue;
                // The composter's own fertiliser first, the supply box after.
                var fertiliser = FindItem(compost, FertilizerShortname) ?? FindItem(supply, FertilizerShortname);
                if (fertiliser == null) return null;
                var bed = planter;
                return new Job { Name = "FertilisingJob#1", Spot = Beside(planter, npc), Do = () => Fertilise(bed, fertiliser) };
            }
            return null;
        }

        private Job HoneyJob(LiveHand liveHand, FarmData hand, BasePlayer npc, List<BaseCombatEntity> outputs)
        {
            foreach (var hive in liveHand.Hives)
            {
                if (hive == null || hive.IsDestroyed || hive.inventory == null) continue;
                for (int slot = 1; slot < hive.inventory.capacity; slot++)
                {
                    var comb = hive.inventory.GetSlot(slot);
                    if (comb == null || comb.amount <= 0 || AtCap(hand, outputs, comb.info.shortname) || !AnyRoom(outputs, comb.info, 1)) continue;
                    var honeycomb = comb;
                    return new Job { Name = "HoneyJob#1", Spot = Beside(hive, npc), Do = () => MoveSome(honeycomb, outputs) };
                }
            }
            return null;
        }

        private Job CompostJob(LiveHand liveHand, BasePlayer npc, List<BaseCombatEntity> outputs, BaseCombatEntity supply, Composter compost)
        {
            // A composter whose slots are filling with fertiliser stops composting:
            // move a stack out to the supply box.
            if (supply != null && FreeSlots(compost) < 2)
            {
                var made = FindItem(compost, FertilizerShortname);
                if (made != null && HasRoom(supply, made.info, made.amount))
                {
                    var stack = made;
                    return new Job { Name = "CompostJob#1", Spot = Beside(compost, npc), Do = () => MoveStack(stack, supply) };
                }
            }

            // Waste lying in the boxes (plant fibre, spoiled food, dung) goes in.
            var bins = new List<BaseCombatEntity>(outputs);
            if (supply != null) bins.Add(supply);
            foreach (var bin in bins)
            {
                var inventory = InventoryOf(bin);
                if (inventory == null) continue;
                foreach (var item in inventory.itemList)
                {
                    if (item == null || item.amount <= 0 || !IsWaste(item) || !HasRoom(compost, item.info, item.amount)) continue;
                    var waste = item;
                    return new Job { Name = "CompostJob#2", Spot = Beside(bin, npc), Do = () => MoveStack(waste, compost) };
                }
            }
            return null;
        }

        // The point at the farm's edge in a direction, where a run out of sight begins.
        private Vector3 EdgeToward(LiveHand liveHand, Vector3 target)
        {
            Vector3 toward = target - liveHand.Home;
            toward.y = 0f;
            Vector3 edge = liveHand.Home + (toward.sqrMagnitude > 1f ? toward.normalized : Vector3.forward) * Mathf.Min(FarmRadius * 0.5f, toward.magnitude);
            edge.y = TerrainMeta.HeightMap.GetHeight(edge);
            if (edge.y < liveHand.Home.y) edge.y = liveHand.Home.y;
            return edge;
        }

        private Job WaterRunJob(LiveHand liveHand, FarmData hand, LiquidContainer water, Item liquid)
        {
            if (water == null || water.inventory == null || config.WaterPerTrip <= 0) return null;
            int held = liquid != null ? liquid.amount : 0;
            int capacity = water.inventory.maxStackSize > 0 ? water.inventory.maxStackSize : 20000;
            var current = water.GetLiquidItem();
            bool fresh = current == null || current.info == WaterTypes.WaterItemDef;
            if (!fresh || held >= capacity * Mathf.Clamp01(config.FetchBelow)) return null;
            if (!liveHand.WaterSearched) FindFreshWater(liveHand);
            if (liveHand.WaterPoint == Vector3.zero) return null;
            int carry = Mathf.Min(config.WaterPerTrip, capacity - held);
            if (carry <= 0) return null;
            return new Job { Name = "WaterRunJob#1", Spot = EdgeToward(liveHand, liveHand.WaterPoint), Do = () => Depart(liveHand, hand, carry) };
        }

        private Job MarketRunJob(LiveHand liveHand, FarmData hand, List<BaseCombatEntity> outputs)
        {
            if (!MarketOpen || outputs.Count == 0 || Now - hand.LastMarketRun < hand.MarketHours * 3600.0) return null;
            if (Time.realtimeSinceStartup - liveHand.MarketCheckedAt < 300f) return null;
            liveHand.MarketCheckedAt = Time.realtimeSinceStartup;
            if (!liveHand.MarketSearched) FindMarket(liveHand);
            if (liveHand.MarketPoint == Vector3.zero || Allowance(liveHand.Owner) == 0.0) return null;
            if (LoadWorth(liveHand.Owner, hand, outputs) < Mathf.Max(0, config.MarketMinLoad)) return null;
            if (Time.realtimeSinceStartup - hand.TripClaimedAt < 120f) return null;
            hand.TripClaimedAt = Time.realtimeSinceStartup;
            return new Job { Name = "MarketRunJob#1", Spot = EdgeToward(liveHand, liveHand.MarketPoint), Do = () => DepartForMarket(liveHand, hand, OutputsOf(hand)) };
        }

        private Job VendorRunJob(LiveHand liveHand, FarmData hand, List<BaseCombatEntity> outputs)
        {
            if (outputs.Count == 0 || Now - hand.LastVendorRun < hand.VendorHours * 3600.0) return null;
            if (Time.realtimeSinceStartup - liveHand.VendorCheckedAt < 300f) return null;
            liveHand.VendorCheckedAt = Time.realtimeSinceStartup;
            if (!liveHand.VendorSearched) FindVendor(liveHand);
            var vendor = liveHand.Vendor;
            if (vendor == null || vendor.IsDestroyed) return null;
            bool wool = SpareWool(hand, outputs) >= Mathf.Max(1, ConVar.Livestock.vendorWoolMinimum);
            bool animals = hand.AnimalsPerRun > 0 && PickSurplus(liveHand, FindCupboard(hand.Cupboard), hand, false) != null;
            var wanted = WantedAnimals(liveHand, hand);
            bool buying = wanted.Count > 0 && TotalOf(outputs, ScrapShortname) >= PriceOf(wanted[0]);
            if (!wool && !animals && !buying) return null;
            if (Time.realtimeSinceStartup - hand.TripClaimedAt < 120f) return null;
            hand.TripClaimedAt = Time.realtimeSinceStartup;
            return new Job { Name = "VendorRunJob#1", Spot = EdgeToward(liveHand, vendor.transform.position), Do = () => DepartForVendor(liveHand, hand, OutputsOf(hand)) };
        }

        #region Livestock

        // pass 0: an empty manger or tank; 1: top-ups, milk, wool; 2: the cull; 3: dung.
        private Job TroughJob(LiveHand liveHand, FarmData hand, BasePlayer npc, BaseCombatEntity supply, Item liquid, Composter compost, List<BaseCombatEntity> outputs, int pass)
        {
            var cupboard = FindCupboard(hand.Cupboard);
            if (pass == 2)
            {
                // Surplus adults beyond the numbers kept go for meat, one an hour.
                if (hand.CullLivestock && outputs.Count > 0 && Now - hand.LastCull >= 3600.0)
                {
                    var victim = PickSurplus(liveHand, cupboard, hand, true);
                    if (victim != null)
                    {
                        var theVictim = victim;
                        return new Job { Name = "TroughJob#1", Spot = Beside(victim, npc), Do = () => Slaughter(hand, npc, theVictim, outputs) };
                    }
                }
                return null;
            }
            if (pass == 3) return DungJob(liveHand, hand, npc, compost, cupboard);

            foreach (var trough in liveHand.Troughs)
            {
                if (trough == null || trough.IsDestroyed || trough.inventory == null) continue;

                // Hay in the manger: the same feed a coop takes. A trough has room for more
                // than one kind, so any feed will do; empty in pass 0, low in pass 1.
                var manger = trough.GetLivestockFoodItem();
                if ((supply != null || outputs.Count > 0) && (manger == null || (pass == 1 && manger.amount < 5)))
                {
                    var feed = FindCoopFood(hand, supply, outputs, null);
                    if (feed != null && HasRoom(trough, feed.info, 1))
                    {
                        var theFeed = feed;
                        var theTrough = trough;
                        return new Job { Name = "TroughJob#2", Spot = Beside(trough, npc), Do = () => MoveSome(theFeed, new List<BaseCombatEntity> { theTrough }) };
                    }
                }

                // Water in the trough's own tank.
                var tank = trough.WaterStorage;
                if (liquid != null && tank != null && !tank.IsDestroyed && tank.inventory != null)
                {
                    var held = tank.GetLiquidItem();
                    bool fresh = held == null || held.info == WaterTypes.WaterItemDef;
                    int capacity = tank.inventory.maxStackSize > 0 ? tank.inventory.maxStackSize : 2000;
                    if (fresh && (held == null || held.amount <= 0 || (pass == 1 && held.amount < capacity / 4)))
                    {
                        int take = Mathf.Min(liquid.amount, capacity - (held != null ? held.amount : 0));
                        if (take > 0)
                        {
                            var source = liquid;
                            var theTank = tank;
                            return new Job { Name = "TroughJob#3", Spot = Beside(trough, npc), Do = () => FillTank(theTank, source, take) };
                        }
                    }
                }
            }

            if (pass == 0) return null;

            // Milk and wool from the animals inside the farm that trust the farm hand, with
            // the animals' own calls (the same ones the player's bucket and knife end in).
            if (outputs.Count > 0)
            {
                var animals = new List<Rust.Ai.Gen2.LivestockAnimal>();
                Vis.Entities(liveHand.Home, FarmRadius, animals);
                foreach (var animal in animals)
                {
                    if (animal == null || animal.IsDestroyed || animal.IsDead() || !animal.IsLeadable) continue;
                    if (cupboard != null && animal.GetBuildingPrivilege() != cupboard) continue;
                    if (!animal.TrustsToHandle(npc)) continue;

                    // Milking is done in rounds: every cow that is ready, one after the other
                    // in the same job, so the round's milk is created together and stacks
                    // (the game will not stack milk whose fridge time differs by more than
                    // a few minutes, and one cow a tick left a stack per cow).
                    var cow = animal as Rust.Ai.Gen2.Cow;
                    if (cow != null && cow.MilkDef != null && cow.CanBeMilked(npc) && !AtCap(hand, outputs, cow.MilkDef.shortname)
                        && Now - hand.LastMilkRound >= Mathf.Max(0f, config.MilkRoundSeconds) && AnyRoom(outputs, cow.MilkDef, cow.MilkedAmount()))
                    {
                        var theCow = cow;
                        return new Job { Name = "TroughJob#4", Spot = Beside(cow, npc), Do = () => MilkRound(hand, npc, liveHand, cupboard, theCow, outputs) };
                    }

                    var sheep = animal as Rust.Ai.Gen2.Sheep;
                    if (sheep != null && sheep.ShearItems != null && sheep.ShearItems.Length > 0 && sheep.ShearItems[0] != null
                        && sheep.ShearItems[0].itemDef != null && sheep.CanBeSheared(npc) && !AtCap(hand, outputs, sheep.ShearItems[0].itemDef.shortname) && AnyRoom(outputs, sheep.ShearItems[0].itemDef, 1))
                    {
                        var theSheep = sheep;
                        return new Job { Name = "TroughJob#5", Spot = Beside(sheep, npc), Do = () => Shear(hand, npc, theSheep, outputs) };
                    }
                }
            }

            return null;
        }

        // Dung on the ground inside the farm: a share to the biofuel generator, the rest
        // to the composter (either alone when the other is missing or full).
        private Job DungJob(LiveHand liveHand, FarmData hand, BasePlayer npc, Composter compost, BuildingPrivlidge cupboard)
        {
            BiofuelGenerator generator = null;
            if (hand.Biofuel)
                foreach (var candidate in liveHand.Generators)
                    if (candidate != null && !candidate.IsDestroyed && candidate.inventory != null) { generator = candidate; break; }
            if ((compost != null || generator != null) && liveHand.Troughs.Count > 0)
            {
                var dropped = new List<DroppedItem>();
                Vis.Entities(liveHand.Home, FarmRadius, dropped);
                foreach (var pile in dropped)
                {
                    if (pile == null || pile.IsDestroyed || pile.item == null || !IsWaste(pile.item)) continue;
                    if (cupboard != null && pile.GetBuildingPrivilege() != cupboard) continue;
                    bool toGenerator = generator != null && HasRoom(generator, pile.item.info, pile.item.amount)
                        && (compost == null || !HasRoom(compost, pile.item.info, pile.item.amount)
                            || UnityEngine.Random.Range(0, 100) < Mathf.Clamp(hand.DungToGenerator, 0, 100));
                    BaseCombatEntity bin = toGenerator ? (BaseCombatEntity)generator : compost;
                    if (bin == null || !HasRoom(bin, pile.item.info, pile.item.amount)) continue;
                    var thePile = pile;
                    var theBin = bin;
                    return new Job { Name = "DungJob#1", Spot = Beside(pile, npc), Do = () => PickUpInto(thePile, theBin) };
                }
            }
            return null;
        }

        // An animal only lets someone it trusts handle it, and trust is time spent near
        // it: every job tick the farm hand is credited its tick with each of the farm's
        // animals, as a player standing by would be. Trust short of the full bond
        // (maxTrust, fifteen minutes) fades whenever nobody has credited the animal for a
        // few seconds, so the farm hand keeps crediting all the way to the full bond,
        // which never fades; stopping at the lead threshold left it hovering just under
        // the line, refused by the vendor and the bucket by turns. The farm's animals are
        // the tamed ones and those whose home is the owner's cupboard (a calf is born with
        // its mother's trust at exactly the lead threshold and fades below it unattended).
        // Wild animals that wander in are left alone.
        private void Befriend(LiveHand liveHand, BasePlayer npc, BuildingPrivlidge cupboard)
        {
            float tick = Mathf.Max(3f, config.JobSeconds) * Mathf.Max(0f, ConVar.Livestock.familiarityRate);
            if (tick <= 0f) return;
            float full = Mathf.Max(ConVar.Livestock.trustToLead, ConVar.Livestock.maxTrust);
            var animals = new List<Rust.Ai.Gen2.LivestockAnimal>();
            Vis.Entities(liveHand.Home, FarmRadius, animals);
            foreach (var animal in animals)
            {
                if (animal == null || animal.IsDestroyed || animal.IsDead()) continue;
                if (cupboard != null && animal.GetBuildingPrivilege() != cupboard) continue;
                BuildingPrivlidge home;
                bool ours = animal.IsLeadable || (animal.TryGetHomeCupboard(out home) && home == cupboard);
                if (!ours) continue;
                float stored;
                try { stored = animal.StoredSecondsFor(npc.userID); } catch { continue; }
                if (stored >= full) continue;
                try { animal.AddFamiliarity(npc.userID, tick); } catch { }
            }
        }

        private void MilkRound(FarmData hand, BasePlayer npc, LiveHand liveHand, BuildingPrivlidge cupboard, Rust.Ai.Gen2.Cow first, List<BaseCombatEntity> outputs)
        {
            hand.LastMilkRound = Now;
            Milk(hand, npc, first, outputs);
            var animals = new List<Rust.Ai.Gen2.LivestockAnimal>();
            Vis.Entities(liveHand.Home, FarmRadius, animals);
            foreach (var animal in animals)
            {
                var cow = animal as Rust.Ai.Gen2.Cow;
                if (cow == null || cow == first || cow.IsDestroyed || cow.IsDead() || !cow.IsLeadable || cow.MilkDef == null) continue;
                if (cupboard != null && cow.GetBuildingPrivilege() != cupboard) continue;
                if (!cow.TrustsToHandle(npc) || !cow.CanBeMilked(npc)) continue;
                if (AtCap(hand, outputs, cow.MilkDef.shortname) || !AnyRoom(outputs, cow.MilkDef, cow.MilkedAmount())) break;
                Milk(hand, npc, cow, outputs);
            }
        }

        private void Milk(FarmData hand, BasePlayer npc, Rust.Ai.Gen2.Cow cow, List<BaseCombatEntity> outputs)
        {
            if (cow == null || cow.IsDestroyed || npc == null || npc.IsDestroyed || !cow.TrustsToHandle(npc) || !cow.CanBeMilked(npc)) return;
            int amount = cow.MilkedAmount();
            if (cow.TryMilk(npc))
            {
                hand.Harvested += amount;
                Deliver(npc, outputs);
            }
        }

        // One quadrant of fleece per job, as a knife takes it; the wool grows back on the
        // sheep's own timer.
        private void Shear(FarmData hand, BasePlayer npc, Rust.Ai.Gen2.Sheep sheep, List<BaseCombatEntity> outputs)
        {
            if (sheep == null || sheep.IsDestroyed || npc == null || npc.IsDestroyed || !sheep.TrustsToHandle(npc) || !sheep.CanBeSheared(npc)) return;
            if (sheep.TryShear(npc))
            {
                hand.Harvested++;
                Deliver(npc, outputs);
            }
        }

        #region Breeding

        // The herd by its genes. The game gives every animal two copies of five genes
        // (dung, longevity, yield, fertility, hardiness), each Bad, Ok or Good; the better
        // copy shows, the other is carried and passed on. Two lineage markers, one from
        // each parent; matching markers make the animal inbred and halve every gene.
        // Breeding is the game's own (any bonded pair in the pen), so the farm hand's
        // levers are who stays: one bull per species, unrelated to the cows and with the
        // best genes; the best cows up to the number kept; the rest to the vendor, the
        // inbred to the knife.
        private class HerdEntry
        {
            public Rust.Ai.Gen2.LivestockAnimal Animal;
            public string Species;        // "cow" or "sheep"
            public string Plan;           // bull, keep, carrying, growing, wild, sell, knife
            public int Score;             // Good copies less Bad copies, over all ten
            public float Quality;         // what it yields today, 0..1
            public bool Inbred, Male, Adult, Carrying, Leadable;
            public int Marker0, Marker1;
        }

        private static int BreedScore(int genome)
        {
            int score = 0;
            foreach (var gene in Rust.Ai.Gen2.LivestockGenome.AllGenes)
                for (int copy = 0; copy < 2; copy++)
                {
                    var allele = Rust.Ai.Gen2.LivestockGenome.GetAllele(genome, gene, copy);
                    if (allele == Rust.Ai.Gen2.LivestockAllele.Good) score++;
                    else if (allele == Rust.Ai.Gen2.LivestockAllele.Bad) score--;
                }
            return score;
        }

        private static bool SharesLine(HerdEntry a, HerdEntry b)
        {
            return a.Marker0 == b.Marker0 || a.Marker0 == b.Marker1 || a.Marker1 == b.Marker0 || a.Marker1 == b.Marker1;
        }

        private static string AlleleLetter(Rust.Ai.Gen2.LivestockAllele allele)
        {
            return allele == Rust.Ai.Gen2.LivestockAllele.Good ? "G" : allele == Rust.Ai.Gen2.LivestockAllele.Bad ? "B" : "O";
        }

        // Five pairs, shown copy then carried copy: "GO OB BB GG OO".
        private static string GenesText(int genome)
        {
            if (!Rust.Ai.Gen2.LivestockGenome.IsRolled(genome)) return "?";
            string text = "";
            foreach (var gene in Rust.Ai.Gen2.LivestockGenome.AllGenes)
                text += (text.Length > 0 ? " " : "") + AlleleLetter(Rust.Ai.Gen2.LivestockGenome.Expressed(genome, gene)) + AlleleLetter(Rust.Ai.Gen2.LivestockGenome.Carried(genome, gene));
            return text;
        }

        // Every cow and sheep inside the farm, each with what the farm hand intends for it.
        private List<HerdEntry> PlanHerd(LiveHand liveHand, BuildingPrivlidge cupboard, FarmData hand)
        {
            var herd = new List<HerdEntry>();
            var animals = new List<Rust.Ai.Gen2.LivestockAnimal>();
            Vis.Entities(liveHand.Home, FarmRadius, animals);
            foreach (var animal in animals)
            {
                if (animal == null || animal.IsDestroyed || animal.IsDead()) continue;
                string species = animal is Rust.Ai.Gen2.Cow ? "cow" : animal is Rust.Ai.Gen2.Sheep ? "sheep" : null;
                if (species == null) continue;
                if (cupboard != null && animal.GetBuildingPrivilege() != cupboard) continue;
                int genome = animal.Genes;
                var entry = new HerdEntry
                {
                    Animal = animal, Species = species,
                    Score = BreedScore(genome),
                    Quality = Rust.Ai.Gen2.LivestockGenome.ExpressedQuality(genome),
                    Inbred = Rust.Ai.Gen2.LivestockGenome.IsInbred(genome),
                    Male = animal.IsMale, Adult = animal.IsAdult(), Carrying = animal.IsCarrying, Leadable = animal.IsLeadable,
                    Marker0 = Rust.Ai.Gen2.LivestockGenome.GetMarker(genome, 0),
                    Marker1 = Rust.Ai.Gen2.LivestockGenome.GetMarker(genome, 1),
                };
                entry.Plan = !entry.Leadable ? "wild" : !entry.Adult ? "growing" : entry.Carrying ? "carrying" : "keep";
                herd.Add(entry);
            }

            // Where a surplus animal can go, given the switches: the vendor, the knife, or
            // nowhere (then it is only marked spare). The inbred go to the knife when
            // butchering is on, else wherever the rest go.
            bool vendor = hand.VendorRuns && hand.AnimalsPerRun > 0;
            string surplusPlan = vendor ? "sell" : hand.CullLivestock ? "knife" : "spare";
            string inbredPlan = hand.CullLivestock ? "knife" : surplusPlan;
            foreach (var species in new[] { "cow", "sheep" })
            {
                int keep = Mathf.Max(0, species == "cow" ? hand.KeepCows : hand.KeepSheep);
                var adults = new List<HerdEntry>();
                foreach (var entry in herd)
                    if (entry.Species == species && entry.Leadable && entry.Adult) adults.Add(entry);
                if (adults.Count == 0) continue;

                if (!hand.Breeding)
                {
                    // The plain rule: spare males first (one is kept so the herd breeds),
                    // then females that are not carrying, until only the number kept remain.
                    int surplus = adults.Count - keep;
                    int males = 0;
                    foreach (var entry in adults) if (entry.Male) males++;
                    foreach (var entry in adults)
                        if (surplus > 0 && entry.Male && males > 1) { entry.Plan = surplusPlan; males--; surplus--; }
                    foreach (var entry in adults)
                        if (surplus > 0 && !entry.Male && !entry.Carrying) { entry.Plan = surplusPlan; surplus--; }
                    continue;
                }

                // The cows kept: the best by breeding value, inbred ones last. Carrying
                // females cannot go anywhere yet and fill a place meanwhile.
                var females = new List<HerdEntry>();
                foreach (var entry in adults) if (!entry.Male) females.Add(entry);
                females.Sort((a, b) =>
                {
                    if (a.Inbred != b.Inbred) return a.Inbred ? 1 : -1;
                    if (a.Score != b.Score) return b.Score.CompareTo(a.Score);
                    return a.Animal.net.ID.Value.CompareTo(b.Animal.net.ID.Value);
                });
                int places = Mathf.Max(0, keep - 1);
                var kept = new List<HerdEntry>();
                foreach (var entry in females)
                {
                    if (kept.Count < places) { kept.Add(entry); entry.Plan = entry.Carrying ? "carrying" : "keep"; }
                    else if (entry.Carrying) entry.Plan = "carrying";
                    else entry.Plan = entry.Inbred ? inbredPlan : surplusPlan;
                }

                // The bull: not inbred if any is not, sharing a line with the fewest kept
                // cows, then the best genes. Everything else male is surplus.
                HerdEntry bull = null;
                int bullShared = int.MaxValue;
                foreach (var entry in adults)
                {
                    if (!entry.Male) continue;
                    int shared = 0;
                    foreach (var cow in kept) if (SharesLine(entry, cow)) shared++;
                    bool better = bull == null
                        || (bull.Inbred && !entry.Inbred)
                        || (bull.Inbred == entry.Inbred && (shared < bullShared || (shared == bullShared && (entry.Score > bull.Score
                            || (entry.Score == bull.Score && entry.Animal.net.ID.Value < bull.Animal.net.ID.Value)))));
                    if (better) { bull = entry; bullShared = shared; }
                }
                foreach (var entry in adults)
                    if (entry.Male) entry.Plan = entry == bull ? "bull" : entry.Inbred ? inbredPlan : surplusPlan;
            }
            return herd;
        }

        // The next animal to go, for the knife or for the vendor: the plan already says
        // which, given the switches. The worst first, so what stays is the best.
        private Rust.Ai.Gen2.LivestockAnimal PickSurplus(LiveHand liveHand, BuildingPrivlidge cupboard, FarmData hand, bool knife)
        {
            var herd = PlanHerd(liveHand, cupboard, hand);
            HerdEntry pick = null;
            foreach (var entry in herd)
            {
                if (entry.Plan != (knife ? "knife" : "sell") || entry.Carrying || !entry.Adult) continue;
                if (pick == null || (entry.Inbred && !pick.Inbred) || (entry.Inbred == pick.Inbred && entry.Quality < pick.Quality)) pick = entry;
            }
            return pick != null ? pick.Animal : null;
        }

        // What a vendor run could take right now, for the Farm tab.
        private void VendorStock(LiveHand liveHand, BuildingPrivlidge cupboard, FarmData hand, out int cows, out int spareCows, out int sheep, out int spareSheep)
        {
            cows = spareCows = sheep = spareSheep = 0;
            foreach (var entry in PlanHerd(liveHand, cupboard, hand))
            {
                if (!entry.Adult || !entry.Leadable) continue;
                bool spare = (entry.Plan == "sell" || entry.Plan == "knife" || entry.Plan == "spare") && !entry.Carrying;
                if (entry.Species == "cow") { cows++; if (spare) spareCows++; }
                else { sheep++; if (spare) spareSheep++; }
            }
        }

        #endregion

        private void Slaughter(FarmData hand, BasePlayer npc, Rust.Ai.Gen2.LivestockAnimal animal, List<BaseCombatEntity> outputs)
        {
            if (animal == null || animal.IsDestroyed || animal.IsDead() || npc == null || npc.IsDestroyed) return;
            if (animal.IsCarrying || !animal.IsAdult()) return;
            hand.LastCull = Now;
            TakeCorpse(npc, animal, outputs);
        }

        // The animal dies as it would to a player, its corpse's full yield goes to the
        // boxes (meat to the fridge, hide and bone to the plain box) and the corpse is
        // cleared away. Shared by the chickens and the livestock.
        private void TakeCorpse(BasePlayer npc, BaseCombatEntity animal, List<BaseCombatEntity> outputs)
        {
            Vector3 where = animal.transform.position;
            animal.Die();

            var corpses = new List<BaseCorpse>();
            Vis.Entities(where, 4f, corpses);
            BaseCorpse corpse = null;
            foreach (var found in corpses)
                if (found != null && !found.IsDestroyed && found.parentEnt == animal) { corpse = found; break; }
            if (corpse == null) return;   // never anything but this animal's own corpse

            var dispenser = corpse.resourceDispenser ?? corpse.GetComponent<ResourceDispenser>();
            if (dispenser != null && dispenser.containedItems != null)
                foreach (var yield in dispenser.containedItems)
                {
                    if (yield == null || yield.itemDef == null) continue;
                    int amount = Mathf.RoundToInt(yield.amount);
                    if (amount <= 0) continue;
                    var item = ItemManager.Create(yield.itemDef, amount);
                    if (item != null && !item.MoveToContainer(npc.inventory.containerMain)) item.Drop(where + Vector3.up * 0.5f, Vector3.zero);
                }
            corpse.Kill();
            Deliver(npc, outputs);
        }

        private static void FillTank(LiquidContainer tank, Item liquid, int amount)
        {
            if (tank == null || tank.IsDestroyed || tank.inventory == null || liquid == null || liquid.amount <= 0) return;
            if (liquid.info != WaterTypes.WaterItemDef) return;
            int take = Mathf.Min(amount, liquid.amount);
            var held = tank.GetLiquidItem();
            if (held != null)
            {
                if (held.info != WaterTypes.WaterItemDef) return;
                int capacity = tank.inventory.maxStackSize > 0 ? tank.inventory.maxStackSize : 2000;
                take = Mathf.Min(take, Mathf.Max(0, capacity - held.amount));
                if (take <= 0) return;
                held.amount += take;
                held.MarkDirty();
            }
            else
            {
                var water = ItemManager.Create(WaterTypes.WaterItemDef, take);
                if (water == null) return;
                if (!water.MoveToContainer(tank.inventory)) { water.Remove(); return; }
            }
            liquid.UseItem(take);
            var holder = liquid.parent != null ? liquid.parent.entityOwner : null;
            if (holder != null) holder.SendNetworkUpdate();
            tank.SendNetworkUpdate();
        }

        // What a player's pickup does, minus the player: the item leaves the world entity
        // and goes straight into the box.
        private static void PickUpInto(DroppedItem pile, BaseCombatEntity box)
        {
            if (pile == null || pile.IsDestroyed || pile.item == null) return;
            var inventory = box != null && !box.IsDestroyed ? InventoryOf(box) : null;
            if (inventory == null) return;
            var item = pile.item;
            Vector3 where = pile.transform.position;
            pile.RemoveItem();
            if (!item.MoveToContainer(inventory))
            {
                item.Drop(where + Vector3.up * 0.2f, Vector3.zero);
                return;
            }
            if (!pile.IsDestroyed) pile.Kill();
        }

        #endregion

        #region Baking

        // The cooking workbench is the game's mixing table: ingredients in its slots in the
        // recipe's order, StartMixing, and the loaves appear in the table when the time is
        // up. The farm hand loads it from the boxes, starts it, and later carries the
        // loaves to the supply box, where they are feed. It never touches a table someone
        // is using (anything in it, or running).
        private const string BreadShortname = "bread.loaf";
        private const string BriocheShortname = "bread.brioche.loaf";

        private Job BakeJob(LiveHand liveHand, FarmData hand, BasePlayer npc, List<BaseCombatEntity> outputs, BaseCombatEntity supply)
        {
            foreach (var table in liveHand.Workbenches)
            {
                if (table == null || table.IsDestroyed || table.inventory == null || table.IsOn()) continue;
                bool busy;
                var unload = UnloadTable(table, npc, supply, outputs, out busy);
                if (unload != null) return unload;
                if (busy) continue;

                int have = CountOf(supply, BreadShortname) + CountOf(supply, BriocheShortname);
                int want = Mathf.Max(0, hand.LoavesToKeep) - have;
                if (want <= 0) continue;

                var boxes = new List<BaseCombatEntity>(outputs) { supply };
                Recipe recipe = null;
                int batch = 0;
                if (hand.Bake >= 2)
                {
                    recipe = FindRecipe(table, BriocheShortname);
                    batch = recipe != null ? Mathf.Min(want, 5, Affordable(recipe, boxes, hand)) : 0;
                    if (recipe != null && batch < Mathf.Min(want, 5) && Affordable(recipe, boxes, hand, CreamShortname) > batch)
                    {
                        // Short of cream and of nothing else: skim milk that has sat in the
                        // fridge long enough. Never when the rest is short too, or the farm
                        // hand would skim the fridge dry for brioche it cannot bake.
                        var milk = TotalOf(outputs, MilkShortname) > KeepOf(hand, MilkShortname) ? ReadyMilk(outputs) : null;
                        if (milk != null)
                        {
                            var theMilk = milk;
                            return new Job { Name = "BakeJob#1", Spot = Beside(milk.parent.entityOwner ?? table, npc), Do = () => Skim(npc, theMilk, outputs) };
                        }
                    }
                }
                if (batch <= 0)
                {
                    recipe = FindRecipe(table, BreadShortname);
                    batch = recipe != null ? Mathf.Min(want, 5, Affordable(recipe, boxes, hand)) : 0;
                }
                if (recipe == null || batch <= 0 || recipe.RequiresBlueprint) continue;

                var theTable = table;
                var theRecipe = recipe;
                int theBatch = batch;
                return new Job { Name = "BakeJob#2", Spot = Beside(table, npc), Do = () => Bake(npc, theTable, theRecipe, theBatch, boxes, outputs) };
            }
            return null;
        }

        // What the table holds when it is done: loaves to the supply box (they are feed),
        // anything else to the output boxes. Busy = something in it that cannot be moved.
        private Job UnloadTable(CookingWorkbench table, BasePlayer npc, BaseCombatEntity supply, List<BaseCombatEntity> outputs, out bool busy)
        {
            busy = false;
            foreach (var item in table.inventory.itemList)
            {
                if (item == null) continue;
                bool loaf = item.info.shortname == BreadShortname || item.info.shortname == BriocheShortname;
                List<BaseCombatEntity> into = loaf && supply != null && HasRoom(supply, item.info, 1) ? new List<BaseCombatEntity> { supply }
                    : AnyRoom(outputs, item.info, 1) ? OutputsFor(outputs, item.info) : null;
                if (into != null)
                {
                    var product = item;
                    return new Job { Name = "UnloadTable#1", Spot = Beside(table, npc), Do = () => MoveSome(product, into) };
                }
                busy = true;
            }
            return null;
        }

        // The farm's own pies: pumpkin, beef, mutton and chicken, rich with cream when
        // there is cream. Baked until the output boxes hold the number kept.
        private static readonly string[] Pies = { "pie.pumpkin", "pie.beef", "pie.mutton", "pie.chicken" };

        private Job PieJob(LiveHand liveHand, FarmData hand, BasePlayer npc, List<BaseCombatEntity> outputs, BaseCombatEntity supply)
        {
            if (liveHand.Workbenches.Count == 0) { KitchenNote("no cooking workbench inside the farm"); return null; }
            foreach (var table in liveHand.Workbenches)
            {
                if (table == null || table.IsDestroyed || table.inventory == null) continue;
                if (table.IsOn()) { KitchenNote("the workbench is mixing"); continue; }
                bool busy;
                var unload = UnloadTable(table, npc, supply, outputs, out busy);
                if (unload != null) return unload;
                if (busy)
                {
                    string held = null;
                    foreach (var item in table.inventory.itemList) if (item != null) held = (held == null ? "" : held + ", ") + item.amount + " " + item.info.shortname;
                    KitchenNote($"the workbench holds {held} that cannot be put away");
                    continue;
                }

                int have = 0;
                foreach (var box in outputs)
                {
                    var inventory = InventoryOf(box);
                    if (inventory == null) continue;
                    foreach (var item in inventory.itemList)
                        if (item != null && item.info.shortname.StartsWith("pie.")) have += item.amount;
                }
                int want = Mathf.Max(0, hand.PiesToKeep) - have;
                if (want <= 0) { KitchenNote($"the boxes already hold {have} pies of the {hand.PiesToKeep} kept"); continue; }

                var boxes = new List<BaseCombatEntity>(outputs);
                if (supply != null) boxes.Add(supply);
                string shortOf = null;
                foreach (var pie in Pies)
                    foreach (var name in new[] { pie + ".rich", pie })
                    {
                        var recipe = FindRecipe(table, name);
                        if (recipe == null || recipe.RequiresBlueprint) continue;
                        int batch = Mathf.Min(want, 5, Affordable(recipe, boxes, hand));
                        if (batch <= 0)
                        {
                            if (name == pie) shortOf = (shortOf == null ? "" : shortOf + "; ") + name + ": " + ShortOf(recipe, boxes, hand);
                            continue;
                        }
                        var theTable = table;
                        var theRecipe = recipe;
                        int theBatch = batch;
                        return new Job { Name = "PieJob#1", Spot = Beside(table, npc), Do = () => Bake(npc, theTable, theRecipe, theBatch, boxes, outputs) };
                    }
                KitchenNote($"{want} pie(s) wanted, none can be made; short of: {shortOf ?? "?"}");
            }
            return null;
        }

        // Why the kitchen is idle, once every five minutes, so it can be read in the log.
        private static float kitchenNoteAt = -1000f;

        private void KitchenNote(string why)
        {
            if (Time.realtimeSinceStartup - kitchenNoteAt < 300f) return;
            kitchenNoteAt = Time.realtimeSinceStartup;
            Puts($"Kitchen: idle, {why}.");
        }

        private string ShortOf(Recipe recipe, List<BaseCombatEntity> boxes, FarmData hand)
        {
            string text = null;
            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.Ingredient == null) continue;
                int need = ingredient.GetIngredientCount(recipe.ProducedItem);
                int reserved = Reserved(hand, ingredient.Ingredient.shortname);
                int have = TotalOf(boxes, ingredient.Ingredient.shortname) - reserved;
                if (have < need) text = (text == null ? "" : text + ", ") + ingredient.Ingredient.shortname + " " + Mathf.Max(0, have) + "/" + need + (reserved > 0 ? " (" + reserved + " kept for feed)" : "");
            }
            return text ?? "nothing (the table may be busy)";
        }

        // The barbecue: the cull's meat cooked for the pies, wood from the supply box.
        // Cooked meat (and anything burnt) is taken out as soon as it is seen, raw meat
        // goes in only while no cooked meat waits, a stack at a time, so nothing burns
        // for want of attention. A stock of cooked meat is kept, enough for a few pies.
        private static readonly string[] RawMeats = { "beefmeat", "muttonmeat", "chicken.raw" };
        private const int CookedMeatKept = 9;
        private const string WoodShortname = "wood";

        private Job BbqJob(LiveHand liveHand, FarmData hand, BasePlayer npc, List<BaseCombatEntity> outputs, BaseCombatEntity supply)
        {
            foreach (var oven in liveHand.Ovens)
            {
                if (oven == null || oven.IsDestroyed || oven.inventory == null) continue;

                // Out: anything cooked or burnt.
                bool cooking = false;
                foreach (var item in oven.inventory.itemList)
                {
                    if (item == null) continue;
                    var cookable = item.info.GetComponent<ItemModCookable>();
                    bool raw = Array.IndexOf(RawMeats, item.info.shortname) >= 0;
                    bool done = !raw && (item.info.shortname.EndsWith(".cooked") || item.info.shortname.Contains("burned") || item.info.shortname.Contains("burnt"));
                    if (done && AnyRoom(outputs, item.info, 1))
                    {
                        var product = item;
                        var theOven = oven;
                        return new Job { Name = "BbqJob#1", Spot = Beside(oven, npc), Do = () => { int had = product.amount; MoveSome(product, OutputsFor(outputs, product.info)); Puts($"Barbecue: {had} x {product.info.shortname} off the fire."); if (!HasRawMeat(theOven)) theOven.StopCooking(); } };
                    }
                    if (raw && cookable != null) cooking = true;
                }
                if (cooking)
                {
                    if (!oven.IsOn() && oven.FindBurnable() != null) { var theOven = oven; return new Job { Name = "BbqJob#2", Spot = Beside(oven, npc), Do = () => theOven.StartCooking() }; }
                    continue;
                }

                // In: a stack of raw meat whose cooked form is short, once the fire has wood.
                foreach (var rawName in RawMeats)
                {
                    var rawDef = ItemManager.FindItemDefinition(rawName);
                    var cookable = rawDef != null ? rawDef.GetComponent<ItemModCookable>() : null;
                    if (cookable == null || cookable.becomeOnCooked == null) continue;
                    if (TotalOf(outputs, cookable.becomeOnCooked.shortname) >= CookedMeatKept) continue;
                    Item raw = null;
                    foreach (var box in outputs) { raw = FindItem(box, rawName); if (raw != null) break; }
                    if (raw == null) continue;

                    if (oven.FindBurnable() == null)
                    {
                        var wood = supply != null ? FindItem(supply, WoodShortname) : null;
                        if (wood == null || oven.fuelType == null || wood.info != oven.fuelType) continue;   // no wood: this fire waits
                        var theWood = wood;
                        var theOven = oven;
                        return new Job { Name = "BbqJob#3", Spot = Beside(oven, npc), Do = () => { int take = Mathf.Min(50, theWood.amount); var part = take < theWood.amount ? theWood.SplitItem(take) : theWood; if (part != null && !part.MoveToContainer(theOven.inventory) && part != theWood) part.MoveToContainer(theWood.parent); } };
                    }
                    var theRaw = raw;
                    var fire = oven;
                    int amount = Mathf.Min(raw.amount, Mathf.Max(1, CookedMeatKept - TotalOf(outputs, cookable.becomeOnCooked.shortname)));
                    return new Job
                    {
                        Spot = Beside(oven, npc),
                        Do = () =>
                        {
                            var part = amount < theRaw.amount ? theRaw.SplitItem(amount) : theRaw;
                            if (part == null) return;
                            if (!part.MoveToContainer(fire.inventory)) { if (part != theRaw) part.MoveToContainer(theRaw.parent); return; }
                            if (!fire.IsOn()) fire.StartCooking();
                            Puts($"Barbecue: {amount} x {part.info.shortname} on the fire.");
                        }
                    };
                }
            }
            return null;
        }

        private static bool HasRawMeat(BaseOven oven)
        {
            if (oven == null || oven.IsDestroyed || oven.inventory == null) return false;
            foreach (var item in oven.inventory.itemList)
                if (item != null && Array.IndexOf(RawMeats, item.info.shortname) >= 0) return true;
            return false;
        }

        // Teas: the ones ticked, brewed from berries until the output boxes hold the
        // number kept of each; with advanced teas on, four of a tea go into its advanced
        // form, and the plain one is kept only four deep.
        private Job TeaJob(LiveHand liveHand, FarmData hand, BasePlayer npc, List<BaseCombatEntity> outputs, BaseCombatEntity supply)
        {
            if (hand.Teas.Count == 0) return null;
            foreach (var table in liveHand.Workbenches)
            {
                if (table == null || table.IsDestroyed || table.inventory == null || table.IsOn()) continue;
                bool busy;
                var unload = UnloadTable(table, npc, supply, outputs, out busy);
                if (unload != null) return unload;
                if (busy) continue;

                var boxes = new List<BaseCombatEntity>(outputs);
                if (supply != null) boxes.Add(supply);
                foreach (var tea in hand.Teas)
                {
                    var recipe = FindRecipe(table, tea);
                    if (recipe == null || recipe.RequiresBlueprint) continue;
                    var advanced = hand.AdvancedTeas ? AdvancedOf(table, recipe.ProducedItem) : null;
                    int keepPlain = advanced != null ? 4 : Mathf.Max(0, hand.TeasToKeep);
                    int plain = TotalOf(outputs, tea);

                    if (advanced != null)
                    {
                        int wantAdvanced = Mathf.Max(0, hand.TeasToKeep) - TotalOf(outputs, advanced.ProducedItem.shortname);
                        int batch = Mathf.Min(wantAdvanced, 5, Affordable(advanced, boxes, hand));
                        if (batch > 0)
                        {
                            var theTable = table; var theRecipe = advanced; int theBatch = batch;
                            return new Job { Name = "TeaJob#1", Spot = Beside(table, npc), Do = () => Bake(npc, theTable, theRecipe, theBatch, boxes, outputs) };
                        }
                        if (wantAdvanced <= 0) continue;
                    }
                    int want = keepPlain - plain;
                    int brew = Mathf.Min(want, 5, Affordable(recipe, boxes, hand));
                    if (brew <= 0) continue;
                    {
                        var theTable = table; var theRecipe = recipe; int theBatch = brew;
                        return new Job { Name = "TeaJob#2", Spot = Beside(table, npc), Do = () => Bake(npc, theTable, theRecipe, theBatch, boxes, outputs) };
                    }
                }
            }
            return null;
        }

        // The advanced tea of a plain one: the recipe whose every ingredient is that tea.
        private static Recipe AdvancedOf(CookingWorkbench table, ItemDefinition plain)
        {
            if (table.Recipes == null || plain == null) return null;
            foreach (var recipe in table.Recipes.AllRecipes)
            {
                if (recipe == null || recipe.ProducedItem == null || recipe.Ingredients == null || recipe.Ingredients.Length == 0) continue;
                bool all = true;
                foreach (var ingredient in recipe.Ingredients) if (ingredient.Ingredient != plain) { all = false; break; }
                if (all) return recipe;
            }
            return null;
        }

        // The plain teas the workbench knows: recipes made of berries alone.
        private List<ItemDefinition> TeasKnown(LiveHand liveHand)
        {
            var teas = new List<ItemDefinition>();
            foreach (var table in liveHand.Workbenches)
            {
                if (table == null || table.IsDestroyed || table.Recipes == null) continue;
                foreach (var recipe in table.Recipes.AllRecipes)
                {
                    if (recipe == null || recipe.ProducedItem == null || recipe.Ingredients == null || recipe.Ingredients.Length == 0 || recipe.RequiresBlueprint) continue;
                    bool berries = true;
                    foreach (var ingredient in recipe.Ingredients)
                        if (ingredient.Ingredient == null || !ingredient.Ingredient.shortname.EndsWith(".berry")) { berries = false; break; }
                    if (berries && !teas.Contains(recipe.ProducedItem)) teas.Add(recipe.ProducedItem);
                }
                break;
            }
            return teas;
        }

        // The crops a focus plants: wheat for bread; wheat, corn, pumpkin and potato for
        // pies; the five berries the teas use. Crops marked as feed stay planted.
        private static readonly Dictionary<string, string[]> FocusCrops = new Dictionary<string, string[]>
        {
            ["bread"] = new[] { "wheat" },
            ["pies"] = new[] { "wheat", "corn", "pumpkin", "potato" },
            ["teas"] = new[] { "red.berry", "green.berry", "yellow.berry", "white.berry", "blue.berry" },
        };

        private void PlantForFocus(FarmData hand)
        {
            string[] wanted;
            if (!FocusCrops.TryGetValue(hand.Focus, out wanted)) return;
            foreach (var crop in crops)
            {
                var setting = EditSetting(hand, crop);
                setting.Plant = Array.IndexOf(wanted, crop.Key) >= 0 || FeedsCrop(hand, crop);
            }
        }

        // Milk ready to skim: the item's own "skim" menu action, whose condition is time
        // spent refrigerated. Running it is what the player's click does: one milk becomes
        // skimmed milk and cream, in the same box.
        private const string MilkShortname = "milk";
        private const string CreamShortname = "cream";

        private static ItemModMenuOption SkimOption(Item item)
        {
            if (item == null || item.info == null || item.info.shortname != MilkShortname) return null;
            if (item.info.itemMods == null) return null;
            foreach (var mod in item.info.itemMods)
            {
                var option = mod as ItemModMenuOption;
                var swap = option != null ? option.actionTarget as ItemModSwap : null;
                if (swap == null || swap.becomeItem == null) continue;
                foreach (var becomes in swap.becomeItem)
                    if (becomes != null && becomes.itemDef != null && becomes.itemDef.shortname == CreamShortname) return option;
            }
            return null;
        }

        // Milk ready to skim, in a box with room for what it becomes: the swap puts the
        // cream and skimmed milk back into the milk's own box, and hands them to the
        // farm hand when that box is full (then Skim delivers them).
        private const string SkimmedMilkShortname = "skimmedmilk";

        private Item ReadyMilk(List<BaseCombatEntity> boxes)
        {
            Item smallest = null;
            var creamDef = ItemManager.FindItemDefinition(CreamShortname);
            var skimmedDef = ItemManager.FindItemDefinition(SkimmedMilkShortname);
            foreach (var box in boxes)
            {
                var inventory = InventoryOf(box);
                if (inventory == null) continue;
                if (creamDef != null && skimmedDef != null && !(HasRoom(box, creamDef, 1) && HasRoom(box, skimmedDef, 1))
                    && !(AnyRoom(boxes, creamDef, 1) && AnyRoom(boxes, skimmedDef, 1))) continue;
                foreach (var item in inventory.itemList)
                {
                    var option = SkimOption(item);
                    if (option == null) continue;
                    bool ready;
                    try { ready = option.actionTarget.CanDoAction(item, null); }
                    catch { ready = false; }
                    if (ready && (smallest == null || item.amount < smallest.amount)) smallest = item;   // fragments first
                }
            }
            return smallest;
        }

        private void Skim(BasePlayer npc, Item milk, List<BaseCombatEntity> outputs)
        {
            if (npc == null || npc.IsDestroyed || milk == null || milk.amount <= 0 || milk.parent == null) return;
            var option = SkimOption(milk);
            if (option == null) return;
            try
            {
                if (!option.actionTarget.CanDoAction(milk, npc)) return;
                option.ServerCommand(milk, option.commandName, npc);
            }
            catch (Exception e) { PrintWarning($"Skimming failed: {e.Message}"); }
            Deliver(npc, outputs);   // what the full box could not take was handed to the farm hand
        }

        private static Recipe FindRecipe(CookingWorkbench table, string shortname)
        {
            if (table.Recipes == null) return null;
            foreach (var recipe in table.Recipes.AllRecipes)
                if (recipe != null && recipe.ProducedItem != null && recipe.ProducedItem.shortname == shortname && recipe.Ingredients != null) return recipe;
            return null;
        }

        private static int TotalOf(List<BaseCombatEntity> boxes, string shortname)
        {
            int total = 0;
            foreach (var box in boxes) total += CountOf(box, shortname);
            return total;
        }

        private static int CountOf(BaseCombatEntity box, string shortname)
        {
            var inventory = box != null ? InventoryOf(box) : null;
            if (inventory == null) return 0;
            int count = 0;
            foreach (var item in inventory.itemList)
                if (item != null && item.info.shortname == shortname) count += item.amount;
            return count;
        }

        // How many of the recipe the boxes can supply.
        // How many batches the boxes hold the ingredients for; one ingredient may be
        // left out of the reckoning (the cream the farm hand can still make). A crop
        // marked as feed is only used above its keep-at-home number: the animals come
        // first.
        private int Affordable(Recipe recipe, List<BaseCombatEntity> boxes, FarmData hand, string except = null)
        {
            var need = new Dictionary<string, int>();
            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.Ingredient == null || ingredient.Ingredient.shortname == except) continue;
                int count;
                need.TryGetValue(ingredient.Ingredient.shortname, out count);
                need[ingredient.Ingredient.shortname] = count + ingredient.GetIngredientCount(recipe.ProducedItem);
            }
            int batches = int.MaxValue;
            foreach (var pair in need)
            {
                if (pair.Value <= 0) continue;
                int have = Mathf.Max(0, TotalOf(boxes, pair.Key) - Reserved(hand, pair.Key));
                batches = Mathf.Min(batches, have / pair.Value);
            }
            return batches == int.MaxValue ? 0 : batches;
        }

        // What the kitchen must leave: a feed crop's keep-at-home number.
        private int Reserved(FarmData hand, string shortname)
        {
            Crop crop;
            return hand != null && cropByKey.TryGetValue(shortname, out crop) && FeedsCrop(hand, crop) ? KeepOf(hand, shortname) : 0;
        }

        private void Bake(BasePlayer npc, CookingWorkbench table, Recipe recipe, int batch, List<BaseCombatEntity> boxes, List<BaseCombatEntity> outputs)
        {
            if (npc == null || npc.IsDestroyed || table == null || table.IsDestroyed || table.inventory == null || table.IsOn()) return;
            foreach (var item in table.inventory.itemList) if (item != null) return;   // someone got there first

            // Each slot takes its ingredient, in the recipe's order.
            bool loaded = true;
            for (int slot = 0; slot < recipe.Ingredients.Length && slot < table.inventory.capacity; slot++)
            {
                var ingredient = recipe.Ingredients[slot];
                if (ingredient.Ingredient == null) continue;
                int needed = ingredient.GetIngredientCount(recipe.ProducedItem) * batch;
                foreach (var box in boxes)
                {
                    if (needed <= 0) break;
                    var inventory = InventoryOf(box);
                    if (inventory == null) continue;
                    foreach (var item in new List<Item>(inventory.itemList))
                    {
                        if (needed <= 0) break;
                        if (item == null || item.info != ingredient.Ingredient) continue;
                        int take = Mathf.Min(needed, item.amount);
                        var part = take < item.amount ? item.SplitItem(take) : item;
                        if (part == null) continue;
                        if (part.MoveToContainer(table.inventory, slot)) needed -= take;
                        else if (part != item) part.MoveToContainer(inventory);
                    }
                }
                if (needed > 0) { loaded = false; break; }
            }

            if (!loaded)
            {
                // Short after all: everything back to the boxes.
                foreach (var item in new List<Item>(table.inventory.itemList))
                    if (item != null && item.MoveToContainer(npc.inventory.containerMain)) { }
                Deliver(npc, outputs);
                return;
            }

            try
            {
                table.StartMixing(npc);
                Puts($"Kitchen: {batch} x {recipe.ProducedItem.shortname} on at the workbench.");
            }
            catch (Exception e) { PrintWarning($"The cooking workbench would not start: {e.Message}"); }
            Deliver(npc, outputs);   // anything the table handed back as excess
        }

        #endregion

        #region Biofuel generator

        private const int GeneratorOutputSlot = 3;

        private Job GeneratorJob(LiveHand liveHand, FarmData hand, BasePlayer npc, List<BaseCombatEntity> outputs)
        {
            var fuelBox = FindBox(hand.Fuel);
            foreach (var generator in liveHand.Generators)
            {
                if (generator == null || generator.IsDestroyed || generator.inventory == null) continue;

                // Fuel made: carry it to the boxes once there is a worthwhile amount.
                var made = generator.inventory.GetSlot(GeneratorOutputSlot);
                if (made != null && made.amount >= 20 && AnyRoom(outputs, made.info, 1))
                {
                    var fuel = made;
                    return new Job { Name = "GeneratorJob#1", Spot = Beside(generator, npc), Do = () => MoveSome(fuel, outputs) };
                }

                // Feed: whatever the fuel box holds that the generator will take, into an
                // empty input slot. Feeding resets the stir, so this comes before stirring.
                if (fuelBox != null)
                {
                    int empty = -1;
                    for (int slot = 0; slot < GeneratorOutputSlot; slot++)
                        if (generator.inventory.GetSlot(slot) == null) { empty = slot; break; }
                    if (empty >= 0)
                    {
                        var fuelInventory = InventoryOf(fuelBox);
                        if (fuelInventory != null)
                            foreach (var item in fuelInventory.itemList)
                            {
                                if (item == null || item.amount <= 0) continue;
                                bool accepted;
                                try { accepted = generator.inventory.CanAcceptItem(null, item, empty) == ItemContainer.CanAcceptResult.CanAccept; }
                                catch { accepted = false; }
                                if (!accepted) continue;
                                var food = item;
                                var into = generator;
                                int at = empty;
                                return new Job { Name = "GeneratorJob#2", Spot = Beside(generator, npc), Do = () => { if (food.parent != null && into != null && !into.IsDestroyed) food.MoveToContainer(into.inventory, at); } };
                            }
                    }
                }

                // Stir when it asks for it.
                if (generator.HasFlag(BiofuelGenerator.Flag_NeedsStir) && !generator.IsStirred())
                {
                    var pot = generator;
                    return new Job { Name = "GeneratorJob#3", Spot = Beside(generator, npc), Do = () => Stir(liveHand, pot) };
                }
            }
            return null;
        }

        // The stir is what the player's ten seconds on the paddle does: with someone
        // watching the farm hand takes the paddle for those ten seconds; otherwise the
        // generator is simply marked stirred.
        private void Stir(LiveHand liveHand, BiofuelGenerator generator)
        {
            if (generator == null || generator.IsDestroyed || generator.IsStirred()) return;
            var npc = liveHand.Npc;
            var paddle = generator.StirMount;
            if (!ShouldWalk(liveHand) || npc == null || npc.IsDestroyed || paddle == null || paddle.IsMounted())
            {
                generator.SetStirred(true);
                return;
            }

            try { paddle.MountPlayer(npc); } catch { }
            if (paddle.GetMounted() != npc)
            {
                generator.SetStirred(true);
                return;
            }
            liveHand.Busy = true;
            timer.Once(BiofuelGenerator.StirTime, () =>
            {
                liveHand.Busy = false;
                if (generator != null && !generator.IsDestroyed) generator.SetStirred(true);
                if (npc != null && !npc.IsDestroyed && paddle != null && paddle.GetMounted() == npc)
                    paddle.DismountPlayer(npc, true);
                if (npc != null && !npc.IsDestroyed) Pin(liveHand);
            });
        }

        #endregion

        #region Market runs

        // Stage 6: surplus to the Farmers Market, scrap back. The same vanish-and-return
        // trip as water, but the sale is settled through the market's own call at
        // departure (it prices, applies the owner's daily limit and destroys the goods),
        // and the scrap appears in the boxes when the farm hand gets back. What is kept
        // at home, how often it goes and whether it buys seeds are the owner's settings.
        private bool MarketOpen => FarmersMarket != null && FarmersMarket.IsLoaded;

        private static readonly string[] StallKeys = { "greengrocer", "seeds", "butcher" };

        private void FindMarket(LiveHand liveHand)
        {
            liveHand.MarketSearched = true;
            liveHand.MarketPoint = Vector3.zero;
            if (!MarketOpen) return;
            float best = Mathf.Clamp(config.MarketRange, 50f, 10000f);
            foreach (var key in StallKeys)
            {
                object answer = null;
                try { answer = FarmersMarket.Call("GetStallPosition", key); } catch { }
                if (!(answer is Vector3)) continue;
                var at = (Vector3)answer;
                float distance = Vector3.Distance(liveHand.Home, at);
                if (distance < best) { best = distance; liveHand.MarketPoint = at; }
            }
        }

        private double Allowance(ulong owner)
        {
            try
            {
                object answer = FarmersMarket.Call("GetDailyAllowance", owner);
                return answer != null ? Convert.ToDouble(answer) : -1.0;
            }
            catch { return -1.0; }
        }

        private double BuyPrice(string shortname)
        {
            try
            {
                object answer = FarmersMarket.Call("GetBuyPrice", shortname);
                return answer != null ? Convert.ToDouble(answer) : -1.0;
            }
            catch { return -1.0; }
        }

        private double SellPrice(string shortname)
        {
            try
            {
                object answer = FarmersMarket.Call("GetSellPrice", shortname);
                return answer != null ? Convert.ToDouble(answer) : -1.0;
            }
            catch { return -1.0; }
        }

        // What is kept at home of one produce: its own number when the owner set one,
        // otherwise the general one.
        private int KeepOf(FarmData hand, string shortname)
        {
            int keep;
            if (hand.KeepEach != null && hand.KeepEach.TryGetValue(shortname, out keep)) return Mathf.Max(0, keep);
            return Mathf.Max(0, hand.KeepProduce);
        }

        // The cap on one produce: its own when the owner set one, else the general one; 0 = none.
        private int CapOf(FarmData hand, string shortname)
        {
            int cap;
            if (hand.CapEach != null && hand.CapEach.TryGetValue(shortname, out cap)) return Mathf.Max(0, cap);
            return Mathf.Max(0, hand.StockCap);
        }

        // How much of a produce the boxes hold (pooled, so skimmed milk counts as milk).
        private static int StockOf(List<BaseCombatEntity> outputs, string shortname)
        {
            int total = 0;
            foreach (var box in outputs)
            {
                var inventory = InventoryOf(box);
                if (inventory == null) continue;
                foreach (var item in inventory.itemList)
                    if (item != null && PoolOf(item.info.shortname) == shortname) total += item.amount;
            }
            return total;
        }

        // Enough of this already: the farm stops making it until some has gone.
        private bool AtCap(FarmData hand, List<BaseCombatEntity> outputs, string shortname)
        {
            if (string.IsNullOrEmpty(shortname)) return false;
            string pool = PoolOf(shortname);
            int cap = CapOf(hand, pool);
            return cap > 0 && StockOf(outputs, pool) >= cap;
        }

        // Where the farm's money goes: the takings box when one is marked, else the outputs.
        private List<BaseCombatEntity> TakingsOf(FarmData hand)
        {
            var box = FindBox(hand.Takings);
            if (box != null) return new List<BaseCombatEntity> { box };
            return OutputsOf(hand);
        }

        // Skimmed milk is the lesser form of milk: it counts against the milk kept and is
        // sold first, so the whole milk (and the cream in it) stays home.
        private static string PoolOf(string shortname) => shortname == SkimmedMilkShortname ? MilkShortname : shortname;

        // Produce never offered: cream while brioche is baked (the farm hand made it for that).
        private static bool KeptForBaking(FarmData hand, string shortname) => hand.Bake >= 2 && shortname == CreamShortname;

        // The surplus: of each produce a stall buys, everything beyond what is kept at
        // home. Seeds and clones live in the supply box and are never offered.
        private List<Item> Surplus(FarmData hand, List<BaseCombatEntity> outputs, out Dictionary<Item, int> amounts)
        {
            amounts = new Dictionary<Item, int>();
            var totals = new Dictionary<string, int>();
            var items = new List<Item>();
            foreach (var box in outputs)
            {
                var inventory = InventoryOf(box);
                if (inventory == null) continue;
                foreach (var item in inventory.itemList)
                {
                    if (item == null || item.amount <= 0 || cropByItem.ContainsKey(item.info.itemid)) continue;
                    if (hand.VendorRuns && item.info.shortname == WoolShortname) continue;
                    if (KeptForBaking(hand, item.info.shortname)) continue;
                    if (BuyPrice(item.info.shortname) < 0) continue;
                    items.Add(item);
                    string pool = PoolOf(item.info.shortname);
                    int total;
                    totals.TryGetValue(pool, out total);
                    totals[pool] = total + item.amount;
                }
            }
            // The lesser form of a pooled produce goes first.
            items.Sort((a, b) =>
            {
                int form = (a.info.shortname == PoolOf(a.info.shortname) ? 1 : 0).CompareTo(b.info.shortname == PoolOf(b.info.shortname) ? 1 : 0);
                return form != 0 ? form : a.amount.CompareTo(b.amount);   // small stacks go whole, so fragments are used up, not made
            });
            var spare = new Dictionary<string, int>();
            foreach (var pair in totals) spare[pair.Key] = Mathf.Max(0, pair.Value - KeepOf(hand, pair.Key));
            var load = new List<Item>();
            foreach (var item in items)
            {
                string pool = PoolOf(item.info.shortname);
                int left = spare[pool];
                if (left <= 0) continue;
                int take = Mathf.Min(left, item.amount);
                spare[pool] = left - take;
                amounts[item] = take;
                load.Add(item);
            }
            return load;
        }

        // A rough worth of the surplus at today's prices, without the daily limit.
        private double LoadWorth(ulong owner, FarmData hand, List<BaseCombatEntity> outputs)
        {
            Dictionary<Item, int> amounts;
            var load = Surplus(hand, outputs, out amounts);
            double worth = 0;
            foreach (var item in load) worth += amounts[item] * Math.Max(0, BuyPrice(item.info.shortname));
            return worth;
        }

        private void DepartForMarket(LiveHand liveHand, FarmData hand, List<BaseCombatEntity> outputs)
        {
            if (!MarketOpen || liveHand.MarketPoint == Vector3.zero) return;
            Dictionary<Item, int> amounts;
            var load = Surplus(hand, outputs, out amounts);
            if (load.Count == 0) return;

            // The load goes into a container of its own for the stalls to price; what
            // they do not buy goes straight back where it came from.
            var cart = new ItemContainer();
            cart.ServerInitialize(null, 48);
            cart.GiveUID();
            var origins = new Dictionary<Item, ItemContainer>();
            foreach (var item in load)
            {
                var origin = item.parent;
                int take = amounts[item];
                var part = take < item.amount ? item.SplitItem(take) : item;
                if (part == null) continue;
                if (!part.MoveToContainer(cart)) { if (part != item) part.MoveToContainer(origin); continue; }
                origins[part] = origin;
            }

            int scrap = 0;
            try
            {
                object answer = FarmersMarket.Call("SellProduce", liveHand.Owner, cart);
                if (answer != null) scrap = Convert.ToInt32(answer);
            }
            catch (Exception e) { PrintWarning($"The market refused the farm hand's load: {e.Message}"); }

            foreach (var left in new List<Item>(cart.itemList))
            {
                ItemContainer origin;
                if (left == null) continue;
                if (!(origins.TryGetValue(left, out origin) && origin != null && left.MoveToContainer(origin)))
                    if (!left.MoveToContainer(InventoryOf(outputs[0]))) left.Drop(outputs[0].transform.position + Vector3.up * 0.8f, Vector3.zero);
            }
            cart.Kill();

            var worker = liveHand.Worker;
            hand.LastMarketRun = Now;
            hand.Runs++;
            hand.Earned += scrap;
            worker.AwayGross = scrap;
            worker.AwayBought.Clear();
            if (hand.BuySeeds && scrap > 0)
            {
                var supply = FindBox(hand.Supply);
                var shortOf = supply != null ? ShortOfSeeds(hand, supply) : null;
                if (shortOf != null)
                    foreach (var crop in shortOf)
                    {
                        string seed = null;
                        double price = -1;
                        foreach (var id in crop.ItemIds)
                        {
                            var def = ItemManager.FindItemDefinition(id);
                            if (def == null || !def.shortname.StartsWith("seed.")) continue;
                            price = SellPrice(def.shortname);
                            if (price > 0) { seed = def.shortname; break; }
                        }
                        if (seed == null) continue;
                        int shortfall = Mathf.Max(0, KeepSeeds(hand, crop) - SeedsHeld(hand, supply, crop));
                        int afford = (int)Math.Floor(scrap / price);
                        int buy = Mathf.Min(shortfall, afford);
                        if (buy <= 0) continue;
                        scrap -= (int)Math.Ceiling(buy * price);
                        worker.AwayBought[seed] = buy;
                    }
            }
            worker.AwayScrap = Mathf.Max(0, scrap);
            worker.AwaySpent = Mathf.Max(0, worker.AwayGross - worker.AwayScrap);
            worker.AwayKind = "market";
            float distance = Vector3.Distance(liveHand.Home, liveHand.MarketPoint);
            worker.AwayUntil = Now + 2.0 * distance / Mathf.Max(0.5f, config.WalkSpeed) + Mathf.Max(0f, config.MarketSeconds);
            Despawn(worker.Id);
        }

        private int SeedsHeld(FarmData hand, BaseCombatEntity supply, Crop crop)
        {
            var inventory = InventoryOf(supply);
            if (inventory == null) return 0;
            int held = 0;
            foreach (var item in inventory.itemList)
                if (item != null && crop.ItemIds.Contains(item.info.itemid)) held += item.amount;
            return held;
        }

        private void ReturnFromMarket(FarmData hand, Worker worker)
        {
            ulong userId = hand.Owner;
            LiveHand liveHand;
            live.TryGetValue(worker.Id, out liveHand);
            var npc = liveHand != null ? liveHand.Npc : null;
            var outputs = TakingsOf(hand);   // the takings box when there is one
            int scrap = worker.AwayScrap;
            worker.AwayScrap = 0;

            if (scrap > 0)
            {
                var item = ItemManager.CreateByName(ScrapShortname, scrap);
                if (item != null)
                {
                    if (npc != null && !npc.IsDestroyed && npc.inventory != null && outputs.Count > 0 && item.MoveToContainer(npc.inventory.containerMain))
                        Deliver(npc, outputs);
                    else if (outputs.Count == 0 || !item.MoveToContainer(InventoryOf(outputs[0])))
                    {
                        Vector3 home;
                        item.Drop((TryParsePosition(worker.Home, out home) ? home : npc != null ? npc.transform.position : Vector3.zero) + Vector3.up * 0.5f, Vector3.zero);
                    }
                }
            }

            int seeds = 0;
            var supply = FindBox(hand.Supply);
            foreach (var pair in worker.AwayBought)
            {
                if (pair.Value <= 0) continue;
                var item = ItemManager.CreateByName(pair.Key, pair.Value);
                if (item == null) continue;
                seeds += pair.Value;
                var inventory = supply != null ? InventoryOf(supply) : null;
                if (inventory == null || !item.MoveToContainer(inventory))
                {
                    Vector3 home;
                    item.Drop((TryParsePosition(worker.Home, out home) ? home : Vector3.zero) + Vector3.up * 0.5f, Vector3.zero);
                }
            }
            worker.AwayBought.Clear();

            int gross = worker.AwayGross, spent = worker.AwaySpent;
            worker.AwayGross = worker.AwaySpent = 0;
            Notify(userId, $"Market.{gross}.{spent}.{seeds}.{scrap}");
        }

        #endregion

        #region Vendor runs

        // Wool and surplus animals to the game's livestock vendor, through the vendor's own
        // negotiation: the first offer is taken (the offers are random by design). An animal
        // is led by the farm hand and stands in the vendor's trigger for the sale, as it
        // would with a player; the vendor kills it as sold. Rewards come home with the
        // farm hand.
        private const string WoolShortname = "wool";

        private void FindVendor(LiveHand liveHand)
        {
            liveHand.VendorSearched = true;
            liveHand.Vendor = null;
            float best = Mathf.Clamp(config.VendorRange, 50f, 20000f);
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var vendor = entity as LivestockVendor;
                if (vendor == null || vendor.IsDestroyed) continue;
                float distance = Vector3.Distance(liveHand.Home, vendor.transform.position);
                if (distance < best) { best = distance; liveHand.Vendor = vendor; }
            }
        }

        private int SpareWool(FarmData hand, List<BaseCombatEntity> outputs)
        {
            int total = 0;
            foreach (var box in outputs)
            {
                var inventory = InventoryOf(box);
                if (inventory == null) continue;
                foreach (var item in inventory.itemList)
                    if (item != null && item.info.shortname == WoolShortname) total += item.amount;
            }
            return Mathf.Max(0, total - KeepOf(hand, WoolShortname));
        }

        private void DepartForVendor(LiveHand liveHand, FarmData hand, List<BaseCombatEntity> outputs)
        {
            var vendor = liveHand.Vendor;
            var npc = liveHand.Npc;
            var worker = liveHand.Worker;
            if (vendor == null || vendor.IsDestroyed || npc == null || npc.IsDestroyed || npc.inventory == null) return;
            Deliver(npc, outputs);   // start with empty hands
            var carried = npc.inventory.containerMain;

            // Wool, a lot at a time.
            int spare = SpareWool(hand, outputs);
            if (spare >= Mathf.Max(1, ConVar.Livestock.vendorWoolMinimum))
            {
                int toCarry = spare;
                foreach (var box in outputs)
                {
                    var inventory = InventoryOf(box);
                    if (inventory == null) continue;
                    foreach (var item in new List<Item>(inventory.itemList))
                    {
                        if (toCarry <= 0) break;
                        if (item == null || item.info.shortname != WoolShortname) continue;
                        int take = Mathf.Min(toCarry, item.amount);
                        var part = take < item.amount ? item.SplitItem(take) : item;
                        if (part == null) continue;
                        if (part.MoveToContainer(carried)) toCarry -= take;
                        else if (part != item) part.MoveToContainer(inventory);
                    }
                }
                int lots = 0;
                for (int lot = 0; lot < 10; lot++)
                {
                    bool sold = false;
                    string why = "the vendor did not take the offer";
                    try
                    {
                        if (!vendor.Server_HasWoolToSell(npc)) { why = "nothing to sell (carrying " + vendor.Server_WoolCarriedBy(npc) + ")"; break; }
                        bool selected = false;
                        try { selected = vendor.Server_SelectWool(npc); }   // the offer's client message has nobody to go to
                        catch (Exception e) { why = "selecting the lot threw: " + e.Message; }
                        if (!selected && why.StartsWith("the vendor")) why = "the vendor would not consider the lot";
                        sold = vendor.Server_AcceptOffer(npc);
                    }
                    catch (Exception e) { why = "it threw: " + e.Message; }
                    if (!sold) { PrintWarning($"Vendor run: wool lot {lot + 1} not sold, {why}."); break; }
                    lots++;
                }
                Puts($"Vendor run: sold {lots} wool lot(s) of {spare} wool carried.");
            }

            // Animals, beyond the numbers kept, up to the number per run.
            int animalsSold = 0;
            for (int i = 0; i < hand.AnimalsPerRun; i++)
            {
                var animal = PickSurplus(liveHand, FindCupboard(hand.Cupboard), hand, false);
                if (animal == null) break;
                string name = LivestockVendor.NameOf(animal) + " (" + animal.ShortPrefabName + ")";
                if (!animal.TrustsToHandle(npc))
                {
                    float stored = 0f; bool sore = false;
                    try { stored = animal.StoredSecondsFor(npc.userID); sore = animal.IsSoreAt(npc); } catch { }
                    PrintWarning($"Vendor run: {name} does not trust the farm hand enough to be led (trust {animal.TrustOf(npc):0}s of {ConVar.Livestock.trustToLead:0} needed, stored {stored:0}s, sore at it {sore}).");
                    break;
                }
                if (!LivestockVendor.IsSellable(animal)) { PrintWarning($"Vendor run: {name} is not sellable (adult {animal.IsAdult()}, pregnant {animal.IsPregnant()})."); break; }
                bool sold = false;
                string why = "the vendor did not take the offer";
                try
                {
                    if (!animal.TryLead(npc, true)) { PrintWarning($"Vendor run: {name} would not be led (sore at the farm hand {animal.IsSoreAt(npc)}, mounted {animal.IsMounted})."); break; }
                    if (vendor.Trigger != null) vendor.Trigger.OnEntityEnter(animal);
                    bool selected = false;
                    try { selected = vendor.Server_SelectAnimal(npc, animal.net.ID); }
                    catch (Exception e) { why = "selecting it threw: " + e.Message; }
                    if (!selected && why.StartsWith("the vendor")) why = "the vendor would not consider it (following " + animal.IsFollowing(npc) + ", in the pen " + (vendor.Trigger != null && vendor.Trigger.entityContents != null && vendor.Trigger.entityContents.Contains(animal)) + ")";
                    sold = vendor.Server_AcceptOffer(npc);
                }
                catch (Exception e) { why = "it threw: " + e.Message; }
                if (sold) Puts($"Vendor run: sold {name}.");
                else PrintWarning($"Vendor run: {name} not sold, {why}.");
                if (!sold)
                {
                    try
                    {
                        if (vendor.Trigger != null && !animal.IsDestroyed) vendor.Trigger.RemoveEntity(animal);
                        if (!animal.IsDestroyed && animal.IsFollowing(npc)) animal.TryLead(npc, false);
                        vendor.Server_ClearSelection(npc);
                    }
                    catch { }
                    break;
                }
                animalsSold++;
            }

            // What the vendor paid goes home with the farm hand; unsold wool back to the boxes.
            worker.AwayRewards.Clear();
            string brought = null;
            foreach (var item in new List<Item>(carried.itemList))
            {
                if (item == null) continue;
                if (item.info.shortname == WoolShortname) continue;
                int had;
                worker.AwayRewards.TryGetValue(item.info.shortname, out had);
                worker.AwayRewards[item.info.shortname] = had + item.amount;
                brought = (brought == null ? "" : brought + ", ") + item.amount + " " + item.info.shortname;
                item.Remove();
            }
            Puts($"Vendor run: {animalsSold} animal(s) sold; bringing home {brought ?? "nothing"}.");
            Deliver(npc, outputs);

            // Calves and lambs to keep up the numbers kept, paid in scrap from the boxes.
            // They come home with the farm hand and appear at the farm on its return.
            worker.AwayAnimals.Clear();
            foreach (var kind in WantedAnimals(liveHand, hand))
            {
                int price = PriceOf(kind);
                if (TotalOf(outputs, ScrapShortname) < price) break;
                if (TakeFrom(outputs, ScrapShortname, price) < price) break;
                worker.AwayAnimals.Add(kind);
                Puts($"Vendor run: bought a {Msg("Ui.Animal." + kind, null)} for {price} scrap.");
            }

            hand.LastVendorRun = Now;
            hand.VendorTrips++;
            worker.AwayKind = "vendor";
            float distance = Vector3.Distance(liveHand.Home, vendor.transform.position);
            worker.AwayUntil = Now + 2.0 * distance / Mathf.Max(0.5f, config.WalkSpeed) + Mathf.Max(0f, config.MarketSeconds);
            Despawn(worker.Id);
        }

        private void ReturnFromVendor(FarmData hand, Worker worker)
        {
            ulong userId = hand.Owner;
            LiveHand liveHand;
            live.TryGetValue(worker.Id, out liveHand);
            var npc = liveHand != null ? liveHand.Npc : null;
            var outputs = TakingsOf(hand);   // the takings box when there is one
            Vector3 home;
            TryParsePosition(worker.Home, out home);

            string list = null;
            foreach (var pair in worker.AwayRewards)
            {
                if (pair.Value <= 0) continue;
                var item = ItemManager.CreateByName(pair.Key, pair.Value);
                if (item == null) continue;
                string name = $"{pair.Value} {item.info.displayName.english}";
                list = list == null ? name : list + ", " + name;
                if (npc != null && !npc.IsDestroyed && npc.inventory != null && outputs.Count > 0 && item.MoveToContainer(npc.inventory.containerMain)) continue;
                if (outputs.Count == 0 || !item.MoveToContainer(InventoryOf(outputs[0]))) item.Drop(home + Vector3.up * 0.5f, Vector3.zero);
            }
            if (npc != null && !npc.IsDestroyed) Deliver(npc, outputs);
            worker.AwayRewards.Clear();

            foreach (var kind in worker.AwayAnimals)
            {
                string name = Msg("Ui.Animal." + kind, null);
                if (BringHomeAnimal(hand, worker, liveHand, kind)) list = list == null ? "a " + name : list + ", a " + name;
                else
                {
                    // No vendor to take the prefab from: the scrap comes back instead.
                    var refund = ItemManager.CreateByName(ScrapShortname, PriceOf(kind));
                    if (refund != null && (outputs.Count == 0 || !refund.MoveToContainer(InventoryOf(outputs[0])))) refund.Drop(home + Vector3.up * 0.5f, Vector3.zero);
                    PrintWarning($"Vendor run: the {name} could not be brought home (no livestock vendor found); the scrap was returned.");
                }
            }
            worker.AwayAnimals.Clear();
            Notify(userId, "Vendor." + (list ?? ""));
        }

        #region Buying livestock

        // The vendor sells calves and lambs (its own conversation does exactly this: a
        // prefab spawned, the scrap taken, trust to the buyer set at the lead threshold).
        // The farm hand pays at the vendor and the animal appears at the farm when it is
        // back, by the trough, trusting the owner and the hand, with the farm as home.
        private int PriceOf(string kind) => kind.StartsWith("cow") ? Mathf.Max(0, config.CalfPrice) : Mathf.Max(0, config.LambPrice);

        // What the farm is short of, against the numbers kept, counting the young and
        // what is already on its way: a female first where there is none, then a male
        // where there is none, then females.
        private List<string> WantedAnimals(LiveHand liveHand, FarmData hand)
        {
            var wanted = new List<string>();
            if (!hand.BuyLivestock || hand.BuyPerRun <= 0) return wanted;
            var herd = PlanHerd(liveHand, FindCupboard(hand.Cupboard), hand);
            foreach (var species in new[] { "cow", "sheep" })
            {
                int keep = Mathf.Max(0, species == "cow" ? hand.KeepCows : hand.KeepSheep);
                int males = 0, females = 0;
                foreach (var entry in herd)
                    if (entry.Species == species && (entry.Leadable || entry.Plan != "wild")) { if (entry.Male) males++; else females++; }
                foreach (var other in hand.Hands)
                    foreach (var kind in other.AwayAnimals)
                        if (kind.StartsWith(species)) { if (kind.EndsWith(":m")) males++; else females++; }
                while (males + females < keep && wanted.Count < hand.BuyPerRun)
                {
                    bool female = females == 0 || males > 0;
                    wanted.Add(species + (female ? ":f" : ":m"));
                    if (female) females++; else males++;
                }
            }
            return wanted;
        }

        private static int TakeFrom(List<BaseCombatEntity> boxes, string shortname, int amount)
        {
            int taken = 0;
            foreach (var box in boxes)
            {
                var inventory = InventoryOf(box);
                if (inventory == null) continue;
                foreach (var item in new List<Item>(inventory.itemList))
                {
                    if (item == null || item.info.shortname != shortname) continue;
                    int take = Mathf.Min(amount - taken, item.amount);
                    item.UseItem(take);
                    taken += take;
                    if (taken >= amount) return taken;
                }
            }
            return taken;
        }

        private bool BringHomeAnimal(FarmData hand, Worker worker, LiveHand liveHand, string kind)
        {
            if (liveHand == null) return false;
            if (!liveHand.VendorSearched) FindVendor(liveHand);
            var vendor = liveHand.Vendor;
            if (vendor == null || vendor.IsDestroyed) return false;
            bool cow = kind.StartsWith("cow");
            bool male = kind.EndsWith(":m");
            var prefab = cow ? (male ? vendor.MaleCalfPrefab : vendor.FemaleCalfPrefab) : (male ? vendor.MaleLambPrefab : vendor.FemaleLambPrefab);
            if (prefab == null || !prefab.isValid) return false;

            // By the trough when there is one, else by the post.
            Vector3 spot = liveHand.Home;
            foreach (var trough in liveHand.Troughs)
                if (trough != null && !trough.IsDestroyed) { spot = trough.transform.position; break; }
            spot += new Vector3(UnityEngine.Random.Range(-2f, 2f), 0f, UnityEngine.Random.Range(-2f, 2f));
            spot = GroundSnap(spot);

            var animal = GameManager.server.CreateEntity(prefab.resourcePath, spot) as Rust.Ai.Gen2.LivestockAnimal;
            if (animal == null) return false;
            animal.IsBeingPurchased = true;
            animal.IsMale = male;
            animal.Spawn();
            try
            {
                animal.SetFamiliarity(hand.Owner, ConVar.Livestock.trustToLead, Rust.Ai.Gen2.LivestockAnimal.FamiliarityReason.Purchased);
                animal.SetFamiliarity(worker.Id, ConVar.Livestock.trustToLead);
                foreach (var entry in PlanHerd(liveHand, FindCupboard(hand.Cupboard), hand))
                    if (entry.Animal != animal && entry.Plan != "wild") { animal.TakeHomeFrom(entry.Animal); break; }
            }
            catch (Exception e) { PrintWarning($"The bought animal could not be settled in: {e.Message}"); }
            animal.SendNetworkUpdate();
            return true;
        }

        #endregion

        #endregion

        #region Water runs

        // The first supply run: no pathfinding across the island. The farm hand walks to
        // the edge of the farm, is gone for as long as the round trip would take at
        // walking pace, and reappears at its post with the water.
        private static readonly int FreshWaterMask = (int)(TerrainTopology.Enum.River | TerrainTopology.Enum.Lake);

        private void FindFreshWater(LiveHand liveHand)
        {
            liveHand.WaterSearched = true;
            liveHand.WaterPoint = Vector3.zero;
            if (TerrainMeta.TopologyMap == null || TerrainMeta.WaterMap == null || TerrainMeta.HeightMap == null) return;
            float range = Mathf.Clamp(config.WaterRange, 20f, 2000f);
            for (float radius = 10f; radius <= range; radius += 10f)
            {
                int steps = Mathf.Max(8, Mathf.RoundToInt(radius / 4f));
                for (int i = 0; i < steps; i++)
                {
                    float angle = i * Mathf.PI * 2f / steps;
                    var point = liveHand.Home + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (!TerrainMeta.TopologyMap.GetTopology(point, FreshWaterMask)) continue;
                    float ground = TerrainMeta.HeightMap.GetHeight(point);
                    float surface = TerrainMeta.WaterMap.GetHeight(point);
                    if (surface <= ground + 0.1f) continue;   // riverside, not the river itself
                    point.y = surface;
                    liveHand.WaterPoint = point;
                    return;
                }
            }
        }

        private void Depart(LiveHand liveHand, FarmData hand, int carry)
        {
            if (liveHand.WaterPoint == Vector3.zero || carry <= 0) return;
            float distance = Vector3.Distance(liveHand.Home, liveHand.WaterPoint);
            double seconds = 2.0 * distance / Mathf.Max(0.5f, config.WalkSpeed) + 20.0;
            var worker = liveHand.Worker;
            worker.AwayUntil = Now + seconds;
            worker.AwayWater = carry;
            Despawn(worker.Id);
        }

        private void Return(FarmData hand, Worker worker)
        {
            int carried = worker.AwayWater;
            string kind = worker.AwayKind;
            worker.AwayUntil = 0;
            worker.AwayWater = 0;
            worker.AwayKind = null;
            if (!data.Farms.ContainsKey(hand.Cupboard) || !hand.Hands.Contains(worker)) return;
            Spawn(hand, worker);
            if (kind == "market")
            {
                ReturnFromMarket(hand, worker);
                return;
            }
            if (kind == "vendor")
            {
                ReturnFromVendor(hand, worker);
                return;
            }

            var water = FindWater(hand.Water);
            if (water == null || water.inventory == null || carried <= 0) return;
            var current = water.GetLiquidItem();
            int capacity = water.inventory.maxStackSize > 0 ? water.inventory.maxStackSize : 20000;
            if (current != null)
            {
                if (current.info != WaterTypes.WaterItemDef) return;   // never pour fresh water into salt
                int room = Mathf.Max(0, capacity - current.amount);
                if (room <= 0) return;
                current.amount += Mathf.Min(carried, room);
                current.MarkDirty();
            }
            else
            {
                var item = ItemManager.Create(WaterTypes.WaterItemDef, Mathf.Min(carried, capacity));
                if (item == null) return;
                if (!item.MoveToContainer(water.inventory)) { item.Remove(); return; }
            }
            water.SendNetworkUpdate();
            hand.Trips++;
        }

        #endregion

        #region Chickens and bees

        private const int CoopEggSlot = 0, CoopFoodSlot = 1, CoopWaterSlot = 2, CoopEggsMadeSlot = 3;

        private static bool IsCoopFood(Item item)
        {
            if (item == null || item.amount <= 0) return false;
            var consumable = item.info.GetComponent<ItemModConsumable>();
            return consumable != null && consumable.chickenCoopFood;
        }

        // pass 0: an empty trough or bowl (the birds go hungry); 1: the routine; 2: the pot.
        private Job CoopJob(LiveHand liveHand, FarmData hand, BasePlayer npc, List<BaseCombatEntity> outputs, BaseCombatEntity supply, Item liquid, int pass)
        {
            var eggDef = ItemManager.FindItemDefinition("egg");
            foreach (var coop in liveHand.Coops)
            {
                if (coop == null || coop.IsDestroyed || coop.inventory == null) continue;

                var laid = coop.inventory.GetSlot(CoopEggsMadeSlot);
                int chickens = coop.Animals.Count;
                int wanted = Mathf.Clamp(hand.ChickensWanted, 0, coop.MaxChickens);
                bool hatching = coop.HasFlag(ChickenCoop.Hatching) || coop.HasFlag(ChickenCoop.Full);
                var food = coop.inventory.GetSlot(CoopFoodSlot);
                var bowl = coop.inventory.GetSlot(CoopWaterSlot);

                if (pass == 2)
                {
                    // One for the pot, every so often, never the last two.
                    if (hand.ButcherHours > 0 && Now - hand.LastButcher >= hand.ButcherHours * 3600.0 && chickens >= 2 && outputs.Count > 0)
                        foreach (var child in coop.children)
                        {
                            var bird = child as FarmableAnimal;
                            if (bird == null || bird.IsDestroyed || bird.IsDead()) continue;
                            var theBird = bird;
                            return new Job { Name = "CoopJob#1", Spot = Beside(coop, npc), Do = () => Butcher(hand, npc, theBird, outputs) };
                        }
                    continue;
                }

                // Feed and water: an empty trough or bowl in pass 0, a low one in pass 1.
                if ((supply != null || outputs.Count > 0) && (food == null || (pass == 1 && food.amount < 5)))
                {
                    var feed = FindCoopFood(hand, supply, outputs, food != null ? food.info : null);
                    if (feed != null)
                    {
                        var theFeed = feed;
                        var theCoop = coop;
                        return new Job { Name = "CoopJob#2", Spot = Beside(coop, npc), Do = () => Feed(theCoop, theFeed) };
                    }
                }
                if (liquid != null && (bowl == null || (pass == 1 && bowl.amount < 250)))
                {
                    int cap = Mathf.Max(250, WaterTypes.WaterItemDef.stackable);
                    int take = Mathf.Min(liquid.amount, cap - (bowl != null ? bowl.amount : 0));
                    if (take > 0)
                    {
                        var source = liquid;
                        var theCoop = coop;
                        return new Job { Name = "CoopJob#3", Spot = Beside(coop, npc), Do = () => WaterCoop(theCoop, source, take) };
                    }
                }
                if (pass == 0) continue;

                // Eggs the hens laid go to the boxes, a few kept back when the flock is short.
                if (chickens < wanted && !hatching)
                {
                    // An egg already in the hatch slot first, then the nest, then the boxes.
                    var inSlot = coop.inventory.GetSlot(CoopEggSlot);
                    var egg = inSlot != null && inSlot.amount > 0 && (eggDef == null || inSlot.info == eggDef) ? inSlot : null;
                    if (egg == null && laid != null && laid.amount > 0) egg = laid;
                    if (egg == null && eggDef != null)
                    {
                        var bins = new List<BaseCombatEntity>(outputs);
                        if (supply != null) bins.Add(supply);
                        foreach (var bin in bins)
                        {
                            egg = FindItem(bin, eggDef.shortname);
                            if (egg != null) break;
                        }
                    }
                    if (egg != null && (egg == inSlot || inSlot == null))
                    {
                        var theEgg = egg;
                        var theCoop = coop;
                        return new Job { Name = "CoopJob#4", Spot = Beside(coop, npc), Do = () => HatchEgg(theCoop, theEgg) };
                    }
                }

                if (laid != null && laid.amount > 0 && !AtCap(hand, outputs, laid.info.shortname) && AnyRoom(outputs, laid.info, 1))
                {
                    var eggs = laid;
                    return new Job { Name = "CoopJob#5", Spot = Beside(coop, npc), Do = () => MoveSome(eggs, outputs) };
                }

                // A pat for a chicken that wants one.
                foreach (var child in coop.children)
                {
                    var bird = child as FarmableAnimal;
                    if (bird == null || bird.IsDestroyed || bird.AnimalLove >= 80f) continue;
                    if (bird.HasFlag(FarmableAnimal.RecentlyPetted) || bird.HasFlag(FarmableAnimal.Sleeping)) continue;
                    var theBird = bird;
                    return new Job { Name = "CoopJob#6", Spot = Beside(coop, npc), Do = () => Pet(theBird) };
                }
            }
            return null;
        }

        // Feed: first the crops the owner marked as feed (and produce that has spoiled)
        // from the harvest in the output boxes, then anything in the supply box the
        // animals eat (bread the farm hand baked, feed the owner bought).
        private Item FindCoopFood(FarmData hand, BaseCombatEntity supply, List<BaseCombatEntity> outputs, ItemDefinition sameAs)
        {
            Item any = null;
            foreach (var box in outputs)
            {
                var inventory = InventoryOf(box);
                if (inventory == null) continue;
                foreach (var item in inventory.itemList)
                {
                    if (!IsCoopFood(item)) continue;
                    Crop crop;
                    bool allowed = item.info.shortname == "spoiled.produce"
                        || (cropByKey.TryGetValue(item.info.shortname, out crop) && FeedsCrop(hand, crop));
                    if (!allowed) continue;
                    if (sameAs == null || item.info == sameAs) return item;
                    if (any == null) any = item;
                }
            }
            var supplyInventory = supply != null ? InventoryOf(supply) : null;
            if (supplyInventory != null)
                foreach (var item in supplyInventory.itemList)
                {
                    if (!IsCoopFood(item)) continue;
                    if (sameAs == null || item.info == sameAs) return item;
                    if (any == null) any = item;
                }
            return sameAs == null ? any : null;   // a part-full trough only takes more of the same
        }

        private static void Feed(ChickenCoop coop, Item feed)
        {
            if (coop == null || coop.IsDestroyed || coop.inventory == null || feed == null || feed.amount <= 0 || feed.parent == null) return;
            var trough = coop.inventory.GetSlot(CoopFoodSlot);
            if (trough != null && trough.info != feed.info) return;
            int room = trough == null ? feed.info.stackable : Mathf.Max(0, feed.info.stackable - trough.amount);
            if (room <= 0) return;
            var part = feed.amount > room ? feed.SplitItem(room) : feed;
            if (part == null) return;
            if (!part.MoveToContainer(coop.inventory, CoopFoodSlot) && part != feed)
                part.MoveToContainer(feed.parent);
        }

        private static void WaterCoop(ChickenCoop coop, Item liquid, int amount)
        {
            if (coop == null || coop.IsDestroyed || coop.inventory == null || liquid == null || liquid.amount <= 0) return;
            if (liquid.info != WaterTypes.WaterItemDef) return;
            int take = Mathf.Min(amount, liquid.amount);
            var bowl = coop.inventory.GetSlot(CoopWaterSlot);
            if (bowl != null)
            {
                if (bowl.info != WaterTypes.WaterItemDef) return;
                take = Mathf.Min(take, Mathf.Max(0, bowl.info.stackable - bowl.amount));
                if (take <= 0) return;
                bowl.amount += take;
                bowl.MarkDirty();
            }
            else
            {
                var water = ItemManager.Create(WaterTypes.WaterItemDef, take);
                if (water == null) return;
                if (!water.MoveToContainer(coop.inventory, CoopWaterSlot))
                {
                    water.Remove();
                    return;
                }
            }
            liquid.UseItem(take);
            var holder = liquid.parent != null ? liquid.parent.entityOwner : null;
            if (holder != null) holder.SendNetworkUpdate();
            coop.SendNetworkUpdate();
        }

        // What the coop's own "hatch" button does: the egg is used up, a hatch timer is
        // added, and the coop's load path restarts its hatch check.
        private static void HatchEgg(ChickenCoop coop, Item egg)
        {
            if (coop == null || coop.IsDestroyed || coop.inventory == null || egg == null || egg.amount <= 0 || egg.parent == null) return;
            if (coop.HasFlag(ChickenCoop.Hatching) || coop.HasFlag(ChickenCoop.Full)) return;
            if (coop.Animals.Count >= coop.MaxChickens) return;

            if (egg.parent != coop.inventory || egg.position != CoopEggSlot)
            {
                var one = egg.amount > 1 ? egg.SplitItem(1) : egg;
                if (one == null) return;
                if (!one.MoveToContainer(coop.inventory, CoopEggSlot))
                {
                    if (one != egg) one.MoveToContainer(egg.parent);
                    return;
                }
                egg = one;
            }
            egg.UseItem();
            coop.Animals.Add(new ChickenCoop.AnimalStatus { TimeUntilHatch = coop.ChickenHatchTimeMinutes * 60f });
            using (var scope = coop.StartSetFlags(BaseEntity.FlagsUpdateMode.SendNetworkUpdate))
            {
                scope.Set(ChickenCoop.Hatching, true);
                scope.Set(ChickenCoop.Full, coop.Animals.Count >= coop.MaxChickens);
            }
            coop.PostServerLoad();
            coop.SendNetworkUpdate();
        }

        // Petting: the game keeps the love stat behind a private RPC, so it goes through
        // the animal's own save/load round trip.
        private void Pet(FarmableAnimal bird)
        {
            if (bird == null || bird.IsDestroyed || bird.IsDead()) return;
            if (bird.HasFlag(FarmableAnimal.RecentlyPetted) || bird.HasFlag(FarmableAnimal.Sleeping)) return;
            var data = new ProtoBuf.FarmableAnimal();
            bird.SaveToData(data);
            data.love = Mathf.Clamp(data.love + 15f, 0f, 100f);
            bird.LoadFromData(data);
            using (var scope = bird.StartSetFlags(BaseEntity.FlagsUpdateMode.SendNetworkUpdate))
                scope.Set(FarmableAnimal.RecentlyPetted, true);
            try { bird.ClientRPC(RpcTarget.NetworkGroup("OnPetted")); } catch { }
            timer.Once(5f, () =>
            {
                if (bird == null || bird.IsDestroyed) return;
                using (var scope = bird.StartSetFlags(BaseEntity.FlagsUpdateMode.SendNetworkUpdate))
                    scope.Set(FarmableAnimal.RecentlyPetted, false);
            });
        }

        // The bird dies as it would to a player, leaving its corpse; the corpse's full
        // yield goes to the boxes and the corpse is cleared away.
        private void Butcher(FarmData hand, BasePlayer npc, FarmableAnimal bird, List<BaseCombatEntity> outputs)
        {
            if (bird == null || bird.IsDestroyed || bird.IsDead() || npc == null || npc.IsDestroyed) return;
            hand.LastButcher = Now;
            TakeCorpse(npc, bird, outputs);
        }

        #endregion

        // Move as much of a stack as the boxes will take, box by box.
        private static void MoveSome(Item item, List<BaseCombatEntity> boxes)
        {
            if (item == null || item.amount <= 0 || item.parent == null) return;
            foreach (var box in OutputsFor(boxes, item.info))
            {
                if (item.amount <= 0 || item.parent == null) return;
                int room = RoomFor(box, item.info);
                if (room <= 0) continue;
                var part = item.amount > room ? item.SplitItem(room) : item;
                if (part == null) continue;
                bool moved = part.MoveToContainer(InventoryOf(box));
                if (!moved && part != item) part.MoveToContainer(item.parent);
                if (moved && part == item) return;   // all of it is in; stop, or the next box would take it again
            }
        }

        private static int RoomFor(BaseCombatEntity box, ItemDefinition def)
        {
            if (!HasRoom(box, def, 1)) return 0;
            var inventory = InventoryOf(box);
            int room = 0;
            for (int slot = 0; slot < inventory.capacity; slot++)
            {
                var item = inventory.GetSlot(slot);
                if (item == null) room += Mathf.Max(1, def.stackable);
                else if (item.info == def) room += Mathf.Max(0, def.stackable - item.amount);
            }
            return room;
        }

        // Compostable and good for nothing else: plant fibre, dung, food that has spoiled.
        // Seeds, clones and edible produce are compostable too, and are never wasted.
        private bool IsWaste(Item item)
        {
            if (item == null || item.info == null) return false;
            if (item.info.ItemModCompostable == null) return false;
            if (item.info.shortname == FertilizerShortname) return false;
            if (cropByItem.ContainsKey(item.info.itemid)) return false;
            return item.info.GetComponent<ItemModConsume>() == null;
        }

        private static int FreeSlots(BaseCombatEntity box)
        {
            var inventory = InventoryOf(box);
            if (inventory == null) return 0;
            int free = 0;
            for (int slot = 0; slot < inventory.capacity; slot++)
                if (inventory.GetSlot(slot) == null) free++;
            return free;
        }

        private static void MoveStack(Item item, BaseCombatEntity box)
        {
            var inventory = box != null && !box.IsDestroyed ? InventoryOf(box) : null;
            if (item == null || item.amount <= 0 || item.parent == null || inventory == null) return;
            item.MoveToContainer(inventory);
        }

        // The crops the owner plants whose seed and clone count in the supply box is
        // below what they asked to keep.
        private List<Crop> ShortOfSeeds(FarmData hand, BaseCombatEntity supply)
        {
            var inventory = InventoryOf(supply);
            if (inventory == null) return null;
            var held = new Dictionary<Crop, int>();
            foreach (var item in inventory.itemList)
            {
                Crop crop;
                if (item == null || item.amount <= 0 || !cropByItem.TryGetValue(item.info.itemid, out crop)) continue;
                int count;
                held.TryGetValue(crop, out count);
                held[crop] = count + item.amount;
            }
            List<Crop> result = null;
            foreach (var crop in crops)
            {
                if (!PlantsCrop(hand, crop)) continue;
                int want = KeepSeeds(hand, crop);
                int count;
                held.TryGetValue(crop, out count);
                if (want <= 0 || count >= want) continue;
                if (result == null) result = new List<Crop>();
                result.Add(crop);
            }
            return result;
        }

        // A fruit of this crop in the boxes that gives a seed when eaten.
        private Item FindSeedFruit(Crop crop, List<BaseCombatEntity> boxes)
        {
            foreach (var box in boxes)
            {
                var inventory = InventoryOf(box);
                if (inventory == null) continue;
                foreach (var item in inventory.itemList)
                {
                    if (item == null || item.amount <= 0 || item.info.shortname != crop.Key) continue;
                    if (SeedProduct(item) != null) return item;
                }
            }
            return null;
        }

        private ItemAmountRandom SeedProduct(Item item)
        {
            var eat = item.info.GetComponent<ItemModConsume>();
            if (eat == null || eat.product == null) return null;
            foreach (var product in eat.product)
                if (product != null && product.itemDef != null && cropByItem.ContainsKey(product.itemDef.itemid)) return product;
            return null;
        }

        // Eating as the game does it, for the seed: one fruit used, the seed the fruit
        // gives (the game's own product table and amount), into the supply box.
        private void EatForSeeds(FarmData hand, BasePlayer npc, Item fruit, BaseCombatEntity supply)
        {
            if (fruit == null || fruit.amount <= 0 || fruit.parent == null || npc == null || npc.IsDestroyed) return;
            var product = SeedProduct(fruit);
            var inventory = supply != null && !supply.IsDestroyed ? InventoryOf(supply) : null;
            if (product == null || inventory == null) return;
            int amount = Mathf.RoundToInt(product.RandomAmount() * (fruit.hasCondition ? fruit.conditionNormalized : 1f));
            if (amount <= 0) amount = 1;
            if (!HasRoom(supply, product.itemDef, amount)) return;
            var seeds = ItemManager.Create(product.itemDef, amount);
            if (seeds == null) return;
            if (!seeds.MoveToContainer(inventory)) { seeds.Remove(); return; }
            var eat = fruit.info.GetComponent<ItemModConsume>();
            var effect = eat != null ? eat.GetConsumeEffect() : default(GameObjectRef);
            if (effect.isValid) Effect.server.Run(effect.resourcePath, npc, 0u, new Vector3(0f, 2f, 0f), Vector3.zero);
            fruit.UseItem(1);
        }

        // Cuttings from a ripe plant, as a player's knife takes them: the plant is spent,
        // the clones carry its genes, and they go to the supply box.
        private void CloneForSeeds(FarmData hand, BasePlayer npc, GrowableEntity plant, BaseCombatEntity supply)
        {
            if (plant == null || plant.IsDestroyed || npc == null || npc.IsDestroyed || !plant.CanClone(npc)) return;
            if (supply == null || supply.IsDestroyed) return;
            plant.TakeClones(npc);
            Deliver(npc, new List<BaseCombatEntity> { supply });
        }

        // Standing room next to an entity, on the side the farm hand is coming from.
        private static Vector3 Beside(BaseEntity entity, BasePlayer npc)
        {
            Vector3 center = entity.transform.position;
            Vector3 toward = npc.transform.position - center;
            toward.y = 0f;
            Vector3 side = toward.sqrMagnitude > 0.01f ? toward.normalized : entity.transform.forward;
            return center + side * 1.3f;
        }

        private static bool Carrying(BasePlayer npc)
        {
            var inventory = npc.inventory;
            if (inventory == null) return false;
            return (inventory.containerMain != null && inventory.containerMain.itemList.Count > 0)
                || (inventory.containerBelt != null && inventory.containerBelt.itemList.Count > 0);
        }

        // Room for `amount` of `def`, counting part stacks, and the box's own filter (a
        // fridge takes only food) checked with a throwaway item.
        private static bool HasRoom(BaseCombatEntity box, ItemDefinition def, int amount)
        {
            var inventory = box != null ? InventoryOf(box) : null;
            if (inventory == null || def == null || amount <= 0) return false;
            int room = 0;
            for (int slot = 0; slot < inventory.capacity && room < amount; slot++)
            {
                var item = inventory.GetSlot(slot);
                if (item == null) room += Mathf.Max(1, def.stackable);
                else if (item.info == def) room += Mathf.Max(0, def.stackable - item.amount);
            }
            if (room < amount) return false;

            // The box's own filter (a fridge takes only food, an oven only what it cooks).
            // Ovens and the like judge by slot, so when "any slot" is refused each empty
            // or matching slot is asked in turn.
            var probe = ItemManager.Create(def, amount);
            if (probe == null) return false;
            bool accepted;
            try
            {
                accepted = inventory.CanAcceptItem(null, probe, -1) == ItemContainer.CanAcceptResult.CanAccept;
                for (int slot = 0; !accepted && slot < inventory.capacity; slot++)
                {
                    var item = inventory.GetSlot(slot);
                    if (item != null && item.info != def) continue;
                    accepted = inventory.CanAcceptItem(null, probe, slot) == ItemContainer.CanAcceptResult.CanAccept;
                }
            }
            catch { accepted = true; }
            probe.Remove();
            return accepted;
        }

        private static Item FindItem(BaseCombatEntity box, string shortname)
        {
            var inventory = box != null ? InventoryOf(box) : null;
            if (inventory == null) return null;
            foreach (var item in inventory.itemList)
                if (item != null && item.amount > 0 && item.info.shortname == shortname) return item;
            return null;
        }

        private void HarvestPlant(FarmData hand, BasePlayer npc, GrowableEntity plant, List<BaseCombatEntity> outputs)
        {
            if (plant == null || plant.IsDestroyed || npc == null || npc.IsDestroyed) return;
            int amount = plant.CurrentPickAmount;
            if (!plant.CanPick(npc) || amount <= 0) return;
            plant.PickFruit(npc);
            hand.Harvested += amount;
            Deliver(npc, outputs);
        }

        private void ClearPlant(BasePlayer npc, GrowableEntity plant, List<BaseCombatEntity> outputs)
        {
            if (plant == null || plant.IsDestroyed || npc == null || npc.IsDestroyed) return;
            if (plant.State != PlantProperties.State.Dying) return;
            plant.RemoveDying(npc);
            Deliver(npc, outputs);
        }

        // Everything the farm hand holds goes into the output box. Harvest is only taken
        // when the box has room, so a refusal here is the box having changed since;
        // rather than carry it about, it is set down in front of the box.
        private static void Deliver(BasePlayer npc, List<BaseCombatEntity> outputs)
        {
            if (npc == null || npc.IsDestroyed || npc.inventory == null || outputs.Count == 0) return;
            foreach (var container in new[] { npc.inventory.containerMain, npc.inventory.containerBelt })
            {
                if (container == null) continue;
                foreach (var item in new List<Item>(container.itemList))
                {
                    if (item == null) continue;
                    bool stored = false;
                    foreach (var box in OutputsFor(outputs, item.info))
                    {
                        var inventory = InventoryOf(box);
                        if (inventory != null && item.MoveToContainer(inventory)) { stored = true; break; }
                    }
                    if (!stored) item.Drop(outputs[0].transform.position + Vector3.up * 0.8f, Vector3.zero);
                }
            }
        }

        // On despawn nothing may stay in the NPC (it is never saved): into the output box,
        // or onto the floor at its feet.
        private void PutDownCarried(LiveHand liveHand)
        {
            var npc = liveHand.Npc;
            if (npc == null || npc.IsDestroyed || npc.inventory == null || !Carrying(npc)) return;
            var hand = liveHand.Farm;
            if (hand != null) Deliver(npc, OutputsOf(hand));
            foreach (var container in new[] { npc.inventory.containerMain, npc.inventory.containerBelt })
            {
                if (container == null) continue;
                foreach (var item in new List<Item>(container.itemList))
                    if (item != null) item.Drop(npc.transform.position + Vector3.up * 0.5f, Vector3.zero);
            }
        }

        private void WaterPlanter(FarmData hand, PlanterBox planter, Item liquid, int amount)
        {
            if (planter == null || planter.IsDestroyed || liquid == null || liquid.amount <= 0) return;
            if (liquid.info != WaterTypes.WaterItemDef) return;
            int take = Mathf.Min(amount, liquid.amount);
            if (take <= 0) return;
            int used = planter.DoSplash(WaterTypes.WaterItemDef, take);
            if (used <= 0) return;
            liquid.UseItem(used);
            var holder = liquid.parent != null ? liquid.parent.entityOwner : null;
            if (holder != null) holder.SendNetworkUpdate();
            hand.Watered++;
        }

        // The planter's plant sockets, from its prefab; cached per prefab.
        private readonly Dictionary<uint, List<Socket_Base>> socketsByPrefab = new Dictionary<uint, List<Socket_Base>>();

        private List<Socket_Base> SocketsOf(PlanterBox planter)
        {
            List<Socket_Base> sockets;
            if (socketsByPrefab.TryGetValue(planter.prefabID, out sockets)) return sockets;
            sockets = new List<Socket_Base>();
            try
            {
                foreach (var socket in PrefabAttribute.server.FindAll<Socket_Base>(planter.prefabID))
                    if (socket != null && socket.female) sockets.Add(socket);
            }
            catch (Exception e) { PrintWarning($"Could not read the sockets of {planter.ShortPrefabName}: {e.Message}"); }
            socketsByPrefab[planter.prefabID] = sockets;
            return sockets;
        }

        private bool FreeSlot(PlanterBox planter, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            foreach (var socket in SocketsOf(planter))
            {
                Vector3 world = planter.transform.TransformPoint(socket.position);
                bool taken = false;
                foreach (var child in planter.children)
                {
                    var plant = child as GrowableEntity;
                    if (plant != null && !plant.IsDestroyed && (plant.transform.position - world).sqrMagnitude < 0.09f)
                    {
                        taken = true;
                        break;
                    }
                }
                if (taken) continue;
                position = world;
                rotation = planter.transform.rotation * socket.rotation;
                return true;
            }
            return false;
        }

        // The first seed or clone in the supply box of a crop the owner wants planted.
        // The seed of the crop the owner plants that has the fewest plants in the ground;
        // crops marked as feed are reckoned at half, so they end up with twice the plants
        // (the animals eat faster than the kitchen bakes).
        // so the planters carry a mix rather than whatever sits first in the supply box.
        private Item PickSeed(FarmData hand, BaseCombatEntity supply, List<BaseCombatEntity> outputs)
        {
            var inventory = supply != null ? InventoryOf(supply) : null;
            if (inventory == null) return null;
            Item best = null;
            int fewest = int.MaxValue;
            foreach (var item in inventory.itemList)
            {
                if (item == null || item.amount <= 0) continue;
                Crop crop;
                if (!cropByItem.TryGetValue(item.info.itemid, out crop) || !PlantsCrop(hand, crop)) continue;
                if (AtCap(hand, outputs, crop.Key)) continue;   // the boxes are full of it
                int growing = PlantsOf(crop) * (FeedsCrop(hand, crop) ? 1 : 2);
                if (growing < fewest) { fewest = growing; best = item; }
            }
            return best;
        }

        private int PlantsOf(Crop crop)
        {
            int count = 0;
            LiveHand liveHand;
            foreach (var pair in live)
            {
                liveHand = pair.Value;
                foreach (var planter in liveHand.Planters)
                {
                    if (planter == null || planter.IsDestroyed) continue;
                    foreach (var child in planter.children)
                        if (child is GrowableEntity && !child.IsDestroyed && CropOf((GrowableEntity)child) == crop) count++;
                }
            }
            return count;
        }

        private void PlantSeed(FarmData hand, ulong owner, PlanterBox planter, Item seed, Vector3 position, Quaternion rotation)
        {
            if (planter == null || planter.IsDestroyed || seed == null || seed.amount <= 0 || seed.parent == null) return;
            var deploy = seed.info.GetComponent<ItemModDeployable>();
            if (deploy == null || deploy.entityPrefab == null || !deploy.entityPrefab.isValid) return;

            // The slot may have been taken since the job was chosen.
            foreach (var child in planter.children)
                if (child is GrowableEntity && !child.IsDestroyed && (child.transform.position - position).sqrMagnitude < 0.09f) return;

            var plant = GameManager.server.CreateEntity(deploy.entityPrefab.resourcePath, position, rotation) as GrowableEntity;
            if (plant == null) return;
            plant.OwnerID = owner;
            plant.Spawn();
            plant.SetParent(planter, worldPositionStays: true);
            if (seed.instanceData != null) plant.ReceiveInstanceData(seed.instanceData);   // a clone's genes
            plant.SendNetworkUpdate();
            seed.UseItem(1);
            hand.Planted++;
        }

        private static void Fertilise(PlanterBox planter, Item fertiliser)
        {
            if (planter == null || planter.IsDestroyed || planter.inventory == null) return;
            if (fertiliser == null || fertiliser.amount <= 0 || fertiliser.parent == null) return;
            if (planter.GetFertilizerCount() > 0) return;
            var one = fertiliser.amount > 1 ? fertiliser.SplitItem(1) : fertiliser;
            if (one == null) return;
            if (!one.MoveToContainer(planter.inventory) && one != fertiliser && !one.MoveToContainer(fertiliser.parent))
                one.Remove();
        }

        #endregion

        #region Walking

        // Movement is the game's RustNav mesh where it has an answer: a doors-allowed path
        // from where the farm hand stands to the job, stepped corner by corner at walking
        // pace, the owner's own doors opened on the way and shut behind. Where the mesh has
        // no complete path (a planter on a ledge, a box in a corner the mesh never baked)
        // it falls back to a straight line, and a stalled leg ends with a hop so the job
        // always gets done. It only walks about at all while the owner is near; the rest of
        // the time it works in place and never touches a door.
        private const float DoorOpenRange = 2.5f;
        private const float DoorCloseRange = 3.0f;
        private const float WalkStallSeconds = 4f;
        private const float WalkLegTimeout = 40f;

        private static int groundMask = -1;

        private bool ShouldWalk(LiveHand liveHand)
        {
            if (config.WalkRange <= 0f) return false;
            var owner = BasePlayer.FindByID(liveHand.Owner);
            if (owner == null || !owner.IsConnected || owner.IsDead()) return false;
            return (owner.transform.position - liveHand.Home).sqrMagnitude < config.WalkRange * config.WalkRange;
        }

        private static Vector3 GroundSnap(Vector3 pos)
        {
            if (groundMask == -1) groundMask = LayerMask.GetMask("Terrain", "World", "Construction", "Default");
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out hit, 4f, groundMask, QueryTriggerInteraction.Ignore))
                return new Vector3(pos.x, hit.point.y + 0.05f, pos.z);
            return pos;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private Rust.Ai.Gen2.RustNavMeshPath navPath;   // reused; CalculatePath overwrites it

        // True with Corners filled when the mesh has a doors-allowed route. A partial route
        // (the goal itself is off the mesh: on a ledge, behind a fence) is still used as far
        // as it goes, so the farm hand leaves the building by the door and hops only the
        // last stretch; only no route at all means a straight line.
        private bool PlanPath(LiveHand liveHand, Vector3 goal)
        {
            liveHand.Corners.Clear();
            liveHand.CornerIndex = 0;
            var npc = liveHand.Npc;
            if (npc == null || npc.IsDestroyed || ConVar.AI.useUnityNavmesh) return false;
            try
            {
                goal = GroundSnap(goal);
                UnityEngine.AI.NavMeshHit startHit, goalHit;
                if (!Rust.Ai.Gen2.RustNavMeshHelpers.SamplePosition(npc.transform.position, out startHit, 6f, Rust.Ai.Gen2.RustNavMeshHelpers.AllAreas, true)) return false;
                if (!Rust.Ai.Gen2.RustNavMeshHelpers.SamplePosition(goal, out goalHit, 4f, Rust.Ai.Gen2.RustNavMeshHelpers.AllAreas, true)) return false;
                if (navPath == null) navPath = new Rust.Ai.Gen2.RustNavMeshPath();
                if (!Rust.Ai.Gen2.RustNavMeshHelpers.CalculatePath(startHit.position, goalHit.position, Rust.Ai.Gen2.RustNavMeshHelpers.AllAreas, navPath, true)
                    || navPath.corners.Count < 2)
                    return false;
                if (navPath.status != UnityEngine.AI.NavMeshPathStatus.PathComplete && navPath.status != UnityEngine.AI.NavMeshPathStatus.PathPartial)
                    return false;
                for (int i = 1; i < navPath.corners.Count; i++) liveHand.Corners.Add(navPath.corners[i].Value);
                return true;
            }
            catch (Exception e)
            {
                PrintWarning($"Path planning failed: {e.Message}");
                liveHand.Corners.Clear();
                return false;
            }
        }

        // RustNav treats a closed door as a wall unless the door is NPC-openable. The
        // owner's own doors inside the cupboard are marked so, once, when the farm is
        // scanned; the flag is a door property and nobody else's doors are ever touched.
        private void MarkDoors(LiveHand liveHand, BuildingPrivlidge cupboard)
        {
            liveHand.Doors.Clear();
            if (ConVar.AI.useUnityNavmesh) return;
            var doors = new List<Door>();
            Vis.Entities(liveHand.Home, FarmRadius + 10f, doors);
            var seen = new HashSet<ulong>();
            foreach (var door in doors)
            {
                if (door == null || door.IsDestroyed || door.net == null || !seen.Add(door.net.ID.Value)) continue;
                if (door.GetBuildingPrivilege() != cupboard) continue;
                liveHand.Doors.Add(door);
                if (door.canNpcOpen) continue;
                try
                {
                    door.canNpcOpen = true;
                    Rust.Ai.Gen2.Nav.RustNavDoorGates.OnDoorToggled(door);
                }
                catch (Exception e) { PrintWarning($"Could not mark a door for the farm hand: {e.Message}"); }
            }
        }

        private void Walk(LiveHand liveHand, Vector3 target, Action onArrive)
        {
            liveHand.WalkTarget = target;
            liveHand.OnArrive = onArrive;
            liveHand.Walking = true;
            liveHand.AwayFromHome = true;
            if (!PlanPath(liveHand, target))
                liveHand.Corners.Add(target);   // straight there
            float now = Time.realtimeSinceStartup;
            liveHand.WalkStartedAt = now;
            liveHand.ProgressAt = now;
            liveHand.ProgressPos = liveHand.Npc != null ? liveHand.Npc.transform.position : target;
            if (walkTimer == null) walkTimer = timer.Every(0.1f, WalkTick);
        }

        private void GoHome(LiveHand liveHand)
        {
            if (ShouldWalk(liveHand))
                Walk(liveHand, liveHand.Home, () =>
                {
                    liveHand.AwayFromHome = false;
                    liveHand.LastYaw = liveHand.HomeYaw;
                    Pin(liveHand);
                });
            else
            {
                liveHand.AwayFromHome = false;
                liveHand.LastYaw = liveHand.HomeYaw;
                Pin(liveHand);
            }
        }

        // One door at a time: the nearest closed own door within reach opens, and shuts
        // once the farm hand is past it.
        private static void DoorPass(LiveHand liveHand, Vector3 pos)
        {
            var passing = liveHand.PassDoor;
            if (passing != null)
            {
                if (passing.IsDestroyed) liveHand.PassDoor = null;
                else if (FlatDistance(pos, passing.transform.position) > DoorCloseRange) { passing.SetOpen(false, true); liveHand.PassDoor = null; }
                else return;
            }
            Door best = null;
            float bestSqr = DoorOpenRange * DoorOpenRange;
            foreach (var door in liveHand.Doors)
            {
                if (door == null || door.IsDestroyed || door.IsOpen()) continue;
                var at = door.transform.position;
                float sqr = (at.x - pos.x) * (at.x - pos.x) + (at.z - pos.z) * (at.z - pos.z);
                if (sqr < bestSqr) { bestSqr = sqr; best = door; }
            }
            if (best == null) return;
            best.SetOpen(true);
            liveHand.PassDoor = best;
        }

        private static void CloseDoor(LiveHand liveHand)
        {
            if (liveHand.PassDoor != null && !liveHand.PassDoor.IsDestroyed) liveHand.PassDoor.SetOpen(false, true);
            liveHand.PassDoor = null;
        }

        private void Arrive(LiveHand liveHand, bool hop)
        {
            var npc = liveHand.Npc;
            liveHand.Walking = false;
            liveHand.Corners.Clear();
            CloseDoor(liveHand);
            if (hop && npc != null && !npc.IsDestroyed)
            {
                npc.ServerPosition = liveHand.WalkTarget;
                npc.SendNetworkUpdate();
            }
            var arrived = liveHand.OnArrive;
            liveHand.OnArrive = null;
            try { arrived?.Invoke(); }
            catch (Exception e) { PrintWarning($"Farm hand job failed: {e.Message}"); }
        }

        private void WalkTick()
        {
            bool anyWalking = false;
            float step = Mathf.Max(0.2f, config.WalkSpeed) * 0.1f;
            float now = Time.realtimeSinceStartup;
            foreach (var liveHand in new List<LiveHand>(live.Values))
            {
                if (!liveHand.Walking) continue;
                var npc = liveHand.Npc;
                if (npc == null || npc.IsDestroyed)
                {
                    liveHand.Walking = false;
                    liveHand.OnArrive = null;
                    CloseDoor(liveHand);
                    continue;
                }

                Vector3 position = npc.transform.position;
                DoorPass(liveHand, position);

                // A leg that stops making progress, or runs too long, ends with a hop.
                if (FlatDistance(position, liveHand.ProgressPos) >= 0.5f) { liveHand.ProgressPos = position; liveHand.ProgressAt = now; }
                else if (now - liveHand.ProgressAt > WalkStallSeconds) { Arrive(liveHand, true); continue; }
                if (now - liveHand.WalkStartedAt > WalkLegTimeout) { Arrive(liveHand, true); continue; }

                if (liveHand.CornerIndex >= liveHand.Corners.Count) { Arrive(liveHand, FlatDistance(position, liveHand.WalkTarget) > 1.5f); continue; }
                Vector3 corner = liveHand.Corners[liveHand.CornerIndex];
                if (FlatDistance(position, corner) <= 0.3f && Mathf.Abs(position.y - corner.y) < 1.5f)
                {
                    liveHand.CornerIndex++;
                    if (liveHand.CornerIndex >= liveHand.Corners.Count) { Arrive(liveHand, FlatDistance(position, liveHand.WalkTarget) > 1.5f); continue; }
                    corner = liveHand.Corners[liveHand.CornerIndex];
                }

                anyWalking = true;
                Vector3 next = Vector3.MoveTowards(position, corner, step);
                Vector3 flat = corner - position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f)
                {
                    float yaw = Quaternion.LookRotation(flat.normalized).eulerAngles.y;
                    liveHand.LastYaw = yaw;
                    npc.viewAngles = new Vector3(0f, yaw, 0f);
                    npc.ServerRotation = Quaternion.Euler(0f, yaw, 0f);
                }
                npc.ServerPosition = next;
                npc.SendNetworkUpdate();
            }
            if (!anyWalking)
            {
                walkTimer?.Destroy();
                walkTimer = null;
            }
        }

        #endregion

        #region Boxes

        private void CmdBox(BasePlayer player, string which)
        {
            if (!MayUse(player)) { Reply(player, "NoPermission"); return; }
            if (dataBroken) return;
            var hand = FarmHere(player);
            if (hand == null) { Reply(player, "Box.None"); return; }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 5f, Rust.Layers.Solid, QueryTriggerInteraction.Ignore))
            {
                Reply(player, "Box.LookAt");
                return;
            }
            var entity = hit.GetEntity();
            var box = entity as BaseCombatEntity;
            bool wrongKind = which == "water" ? !(box is LiquidContainer)
                : which == "compost" ? !(box is Composter)
                : (box == null || box is PlanterBox || box is LiquidContainer || box is Composter || box is BiofuelGenerator || InventoryOf(box) == null);
            if (wrongKind)
            {
                Reply(player, which == "water" ? "Box.NotWater" : which == "compost" ? "Box.NotCompost" : "Box.NotBox");
                return;
            }
            if (box.net == null) return;

            // Inside the farm only, and within a hand's reach of its post.
            var cupboard = FindCupboard(hand.Cupboard);
            if (cupboard == null || box.GetBuildingPrivilege() != cupboard || !WithinReach(hand, box.transform.position))
            {
                Reply(player, "Box.NotHere");
                return;
            }
            AssignBox(hand, which, box, player);
        }

        // Gives a box a role (output boxes can be several; the others one each). One
        // role per box: taking a new role drops the old one. Output and fuel toggle.
        private void AssignBox(FarmData hand, string which, BaseCombatEntity box, BasePlayer replyTo)
        {
            ulong id = box.net.ID.Value;
            bool wasOutput = hand.Outputs.Contains(id);
            if (which != "output") hand.Outputs.Remove(id);
            if (which != "supply" && hand.Supply == id) hand.Supply = 0;
            if (which != "water" && hand.Water == id) hand.Water = 0;
            if (which != "compost" && hand.Compost == id) hand.Compost = 0;
            if (which != "fuel" && hand.Fuel == id) hand.Fuel = 0;
            if (which != "takings" && hand.Takings == id) hand.Takings = 0;

            string said = null;
            switch (which)
            {
                case "output":
                    if (wasOutput) { hand.Outputs.Remove(id); said = "Box.OutputRemoved"; }
                    else { hand.Outputs.Add(id); said = "Box.Output"; }
                    break;
                case "supply": hand.Supply = id; said = "Box.Supply"; break;
                case "water": hand.Water = id; said = "Box.Water"; break;
                case "compost": hand.Compost = id; said = "Box.Compost"; break;
                case "fuel":
                    if (hand.Fuel == id) { hand.Fuel = 0; said = "Box.FuelRemoved"; }
                    else { hand.Fuel = id; said = "Box.Fuel"; }
                    break;
                case "takings":
                    if (hand.Takings == id) { hand.Takings = 0; said = "Box.TakingsRemoved"; }
                    else { hand.Takings = id; said = "Box.Takings"; }
                    break;
            }
            if (replyTo != null && said != null) Reply(replyTo, said);
        }

        // What a box can be: water for a liquid container, compost for a composter,
        // output/supply/fuel for an ordinary box or fridge; nothing for the farm's own
        // working entities (planters, coops, hives, troughs, tables, generators, ovens).
        private static string BoxKind(BaseCombatEntity box)
        {
            if (box == null || box.net == null) return null;
            if (box is LiquidContainer) return "water";
            if (box is Composter) return "compost";
            if (box is PlanterBox || box is ChickenCoop || box is Beehive || box is HitchTrough || box is MixingTable || box is BiofuelGenerator || box is BaseOven) return null;
            return InventoryOf(box) != null ? "box" : null;
        }

        // Every container inside the farm a hand could use, nearest first.
        private List<BaseCombatEntity> Candidates(FarmData farm, Vector3 from)
        {
            var found = new List<BaseCombatEntity>();
            var cupboard = FindCupboard(farm.Cupboard);
            if (cupboard == null) return found;
            var near = new List<BaseCombatEntity>();
            Vis.Entities(from, FarmRadius, near, Rust.Layers.Mask.Deployed, QueryTriggerInteraction.Ignore);
            foreach (var entity in near)
            {
                if (entity == null || entity.IsDestroyed || BoxKind(entity) == null) continue;
                if (entity.GetBuildingPrivilege() != cupboard || found.Contains(entity)) continue;
                found.Add(entity);
            }
            found.Sort((a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
            return found;
        }

        // Sensible defaults from what is in reach: every fridge and the nearest box for
        // the harvest, the next box for supplies, the nearest water and composter; a fuel
        // box is never guessed (whatever is in it gets burned).
        private void AutoSetup(FarmData farm, Vector3 from)
        {
            var candidates = Candidates(farm, from);
            if (farm.Outputs.Count == 0)
            {
                foreach (var box in candidates) if (box is Fridge) farm.Outputs.Add(box.net.ID.Value);
                foreach (var box in candidates)
                    if (BoxKind(box) == "box" && !(box is Fridge) && box.net.ID.Value != farm.Supply && box.net.ID.Value != farm.Fuel && box.net.ID.Value != farm.Takings) { farm.Outputs.Add(box.net.ID.Value); break; }
            }
            if (farm.Supply == 0)
                foreach (var box in candidates)
                    if (BoxKind(box) == "box" && !(box is Fridge) && !farm.Outputs.Contains(box.net.ID.Value) && box.net.ID.Value != farm.Fuel && box.net.ID.Value != farm.Takings) { farm.Supply = box.net.ID.Value; break; }
            if (farm.Water == 0)
                foreach (var box in candidates) if (box is LiquidContainer) { farm.Water = box.net.ID.Value; break; }
            if (farm.Compost == 0)
                foreach (var box in candidates) if (box is Composter) { farm.Compost = box.net.ID.Value; break; }
        }

        // Within the farm radius of any hand's post (or of the cupboard, with none posted).
        private bool WithinReach(FarmData farm, Vector3 position)
        {
            float radiusSqr = FarmRadius * FarmRadius;
            Vector3 home;
            foreach (var worker in farm.Hands)
                if (TryParsePosition(worker.Home, out home) && (position - home).sqrMagnitude <= radiusSqr) return true;
            if (farm.Hands.Count > 0) return false;
            var cupboard = FindCupboard(farm.Cupboard);
            return cupboard != null && (position - cupboard.transform.position).sqrMagnitude <= radiusSqr;
        }

        private string BoxName(BaseCombatEntity box, string userId)
        {
            if (box == null) return null;
            var def = box.pickup.itemTarget;
            return def != null ? def.displayName.english : box.ShortPrefabName;
        }

        #endregion

        #region Panel

        private const string UiPanelName = "FarmHand.Panel";
        private const string ColBg = "0.09 0.09 0.10 0.97";
        private const string ColRow = "0.15 0.15 0.16 1";
        private const string ColBtn = "0.25 0.25 0.25 1";
        private const string ColOn = "0.30 0.55 0.20 1";
        private const string ColOff = "0.45 0.18 0.18 1";
        private const string ColText = "0.88 0.88 0.88 1";
        private const string ColDim = "0.6 0.6 0.6 1";
        private const string ColTitle = "0.55 0.77 0.29 1";
        private const string ColWarn = "0.95 0.55 0.20 1";
        private const string ColTab = "0.18 0.26 0.14 1";

        // The panel is opened on a hand; the tab is remembered per player.
        private readonly Dictionary<ulong, string> panelTab = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, ulong> panelHand = new Dictionary<ulong, ulong>();

        // The hand whose panel the player has open. A hand away on a trip has no NPC;
        // a stand-in with its farm and post is enough to draw the panel.
        private LiveHand PanelHand(BasePlayer player)
        {
            ulong id;
            LiveHand liveHand;
            if (!panelHand.TryGetValue(player.userID, out id)) return null;
            if (live.TryGetValue(id, out liveHand)) return liveHand.Owner == player.userID ? liveHand : null;
            var worker = FindWorker(id);
            if (worker == null) return null;
            FarmData farm = null;
            foreach (var candidate in data.Farms.Values) if (candidate.Hands.Contains(worker)) farm = candidate;
            if (farm == null || farm.Owner != player.userID) return null;
            Vector3 home;
            if (!TryParsePosition(worker.Home, out home)) return null;
            var ghost = new LiveHand { Owner = farm.Owner, Worker = worker, Farm = farm, Home = home, HomeYaw = worker.Yaw, LastYaw = worker.Yaw };
            var cupboard = FindCupboard(farm.Cupboard);
            if (cupboard != null) ScanPlanters(ghost, cupboard);
            return ghost;
        }

        private void ShowPanel(BasePlayer player)
        {
            var liveHand = PanelHand(player);
            if (liveHand == null) { CuiHelper.DestroyUi(player, UiPanelName); return; }
            var hand = liveHand.Farm;
            var worker = liveHand.Worker;
            string uid = player.UserIDString;
            string tab;
            if (!panelTab.TryGetValue(player.userID, out tab)) tab = "hand";

            CuiHelper.DestroyUi(player, UiPanelName);
            var ui = new CuiElementContainer();
            var panel = ui.Add(new CuiPanel
            {
                Image = { Color = ColBg },
                RectTransform = { AnchorMin = "0.24 0.12", AnchorMax = "0.76 0.92" },
                CursorEnabled = true
            }, "Overlay", UiPanelName);

            ui.Add(new CuiLabel
            {
                Text = { Text = worker.Name.ToUpper() + " · " + Msg("Role." + worker.Role, uid).ToUpper() + " · " + Msg("Ui.FarmIn", uid, GridOf(hand)), FontSize = 16, Align = TextAnchor.MiddleCenter, Color = ColTitle },
                RectTransform = { AnchorMin = "0 0.935", AnchorMax = "1 0.995" }
            }, panel);
            ui.Add(new CuiButton
            {
                Button = { Color = ColOff, Command = "farmhand.ui close" },
                Text = { Text = Msg("Ui.Close", uid), FontSize = 13, Align = TextAnchor.MiddleCenter, Color = ColText },
                RectTransform = { AnchorMin = "0.945 0.945", AnchorMax = "0.99 0.99" }
            }, panel);

            // The rail: the hand first, the farm's tabs after.
            string[] tabs = { "hand", "setup", "crops", "animals", "herd", "yard", "market", "vendor" };
            string[] tabKeys = { "Ui.TabHand", "Ui.TabSetup", "Ui.TabCrops", "Ui.TabAnimals", "Ui.TabHerd", "Ui.TabYard", "Ui.TabMarket", "Ui.TabVendor" };
            float ty = 0.925f;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool current = tabs[i] == tab;
                ui.Add(new CuiButton
                {
                    Button = { Color = current ? ColTab : ColBtn, Command = $"farmhand.ui tab {tabs[i]}" },
                    Text = { Text = Msg(tabKeys[i], uid), FontSize = 10, Align = TextAnchor.MiddleCenter, Color = current ? ColTitle : ColText },
                    RectTransform = { AnchorMin = $"0.01 {ty - 0.05f}", AnchorMax = $"0.14 {ty}" }
                }, panel);
                ty -= 0.058f;
            }

            // Under the tabs, the farm's hands: pressing one opens its panel.
            if (hand.Hands.Count > 1)
            {
                ty -= 0.01f;
                ui.Add(new CuiLabel
                {
                    Text = { Text = Msg("Ui.HandsRail", uid), FontSize = 9, Align = TextAnchor.MiddleCenter, Color = ColTitle },
                    RectTransform = { AnchorMin = $"0.01 {ty - 0.03f}", AnchorMax = $"0.14 {ty}" }
                }, panel);
                ty -= 0.032f;
                foreach (var other in hand.Hands)
                {
                    bool current = other == worker;
                    ui.Add(new CuiButton
                    {
                        Button = { Color = current ? ColTab : ColBtn, Command = $"farmhand.ui hand {other.Id}" },
                        Text = { Text = other.Name, FontSize = 9, Align = TextAnchor.MiddleCenter, Color = current ? ColTitle : ColText },
                        RectTransform = { AnchorMin = $"0.01 {ty - 0.04f}", AnchorMax = $"0.14 {ty}" }
                    }, panel);
                    ty -= 0.046f;
                }
            }

            // The content beside the rail keeps its own 0–1 coordinates.
            var content = ui.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.15 0.0", AnchorMax = "0.995 0.93" }
            }, panel, UiPanelName + ".Content");

            float y = 0.98f;
            if (tab != "hand")
            {
                // Everything but the hand tab is the farm, shared by every hand here.
                string area = tab == "crops" ? "planter" : tab == "animals" || tab == "herd" ? "stockman" : tab == "yard" ? "yard" : tab == "market" || tab == "vendor" ? "carter" : null;
                if (area != null && !Does(worker, area))
                {
                    string others = null;
                    foreach (var other in hand.Hands)
                        if (other != worker && Does(other, area)) others = (others == null ? "" : others + ", ") + other.Name;
                    Line(ui, content, ref y, 0.036f, others != null ? Msg("Ui.NotTheirs", uid, worker.Name, others) : Msg("Ui.NobodysJob", uid, worker.Name), ColWarn, 9);
                }
                else
                    Line(ui, content, ref y, 0.036f, Msg("Ui.Shared", uid, hand.Hands.Count), ColDim, 9);
            }
            switch (tab)
            {
                case "setup": DrawSetup(ui, content, ref y, hand, liveHand, uid); break;
                case "crops": DrawCrops(ui, content, ref y, hand, uid); break;
                case "animals": DrawAnimals(ui, content, ref y, hand, liveHand, uid); break;
                case "herd": DrawHerd(ui, content, ref y, hand, liveHand, uid); break;
                case "yard": DrawYard(ui, content, ref y, hand, liveHand, uid); break;
                case "market": DrawMarket(ui, content, ref y, hand, liveHand, uid); break;
                case "vendor": DrawVendor(ui, content, ref y, hand, liveHand, uid); break;
                default: DrawHand(ui, content, ref y, hand, liveHand, uid); break;
            }

            CuiHelper.AddUi(player, ui);
        }

        // The hand: wages, role, pace, the order of its routine, post and dismiss.
        private void DrawHand(CuiElementContainer ui, string panel, ref float y, FarmData hand, LiveHand liveHand, string uid)
        {
            var worker = liveHand.Worker;
            var state = StateOf(hand.Owner);
            Line(ui, panel, ref y, 0.04f,
                state == WageState.Paid
                    ? Msg("Ui.Paid", uid, FormatSpan(Wage <= 0 ? DaySeconds : PaidUntil(hand.Owner) - Now), DailyWage(hand.Owner), HandsOf(hand.Owner))
                    : Msg("Ui.Unpaid", uid, FormatSpan(PaidUntil(hand.Owner) + GraceSeconds - Now)),
                state == WageState.Paid ? ColText : ColWarn, 12);
            if (worker.AwayUntil > Now)
                Line(ui, panel, ref y, 0.032f, Msg(worker.AwayKind == "market" ? "Ui.AwayMarket" : worker.AwayKind == "vendor" ? "Ui.AwayVendor" : "Ui.Away", uid, FormatSpan(worker.AwayUntil - Now)), ColWarn, 10);
            if (liveHand.JobTally.Count > 0)
            {
                string tally = null;
                foreach (var pair in liveHand.JobTally)
                    tally = (tally == null ? "" : tally + " · ") + pair.Key + " " + pair.Value;
                Line(ui, panel, ref y, 0.032f, Msg("Ui.JobTally", uid, tally), ColDim, 9);
            }

            // The role: five buttons, the current one lit.
            y -= 0.01f;
            Heading(ui, panel, ref y, Msg("Ui.Role", uid));
            {
                const float h = 0.05f;
                float slot = 0.94f / Roles.Length;
                for (int i = 0; i < Roles.Length; i++)
                {
                    bool current = Roles[i] == worker.Role;
                    float x0 = 0.03f + i * slot, x1 = x0 + slot - 0.008f;
                    ui.Add(new CuiButton
                    {
                        Button = { Color = current ? ColOn : ColBtn, Command = $"farmhand.ui role {Roles[i]}" },
                        Text = { Text = Msg("Role." + Roles[i], uid), FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColText },
                        RectTransform = { AnchorMin = $"{x0} {y - h}", AnchorMax = $"{x1} {y}" }
                    }, panel);
                }
                y -= h;
            }
            Line(ui, panel, ref y, 0.05f, Msg("Ui.RoleHint." + worker.Role, uid), ColDim, 9);

            SettingLine(ui, panel, ref y, Msg("Ui.JobSeconds", uid), (worker.JobSeconds > 0 ? worker.JobSeconds : Mathf.RoundToInt(Mathf.Max(3f, config.JobSeconds))).ToString(), "farmhand.ui tick", 5, uid);
            y -= 0.01f;
            Heading(ui, panel, ref y, Msg("Ui.Order", uid));
            Line(ui, panel, ref y, 0.05f, Msg("Ui.FixedSteps", uid), ColDim, 9);
            const float rowH = 0.046f;
            var order = OrderOf(worker);
            for (int i = 0; i < order.Count; i++)
            {
                bool covers = Does(worker, AreaOf(order[i]));
                ui.Add(new CuiPanel { Image = { Color = ColRow }, RectTransform = { AnchorMin = $"0.03 {y - rowH + 0.004f}", AnchorMax = $"0.97 {y}" } }, panel);
                ui.Add(new CuiLabel
                {
                    Text = { Text = Msg("Ui.Prio." + order[i], uid), FontSize = 10, Align = TextAnchor.MiddleLeft, Color = covers ? ColText : ColDim },
                    RectTransform = { AnchorMin = $"0.05 {y - rowH}", AnchorMax = $"0.84 {y}" }
                }, panel);
                if (!covers)   // a line through it: another hand's job (or nobody's)
                    ui.Add(new CuiPanel
                    {
                        Image = { Color = ColWarn },
                        RectTransform = { AnchorMin = $"0.05 {y - rowH / 2f - 0.0015f}", AnchorMax = $"0.60 {y - rowH / 2f + 0.0015f}" }
                    }, panel);
                if (i > 0)
                    ui.Add(new CuiButton
                    {
                        Button = { Color = ColBtn, Command = $"farmhand.ui prio {order[i]} up" },
                        Text = { Text = "▲", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColText },
                        RectTransform = { AnchorMin = $"0.855 {y - rowH + 0.008f}", AnchorMax = $"0.905 {y - 0.004f}" }
                    }, panel);
                if (i < order.Count - 1)
                    ui.Add(new CuiButton
                    {
                        Button = { Color = ColBtn, Command = $"farmhand.ui prio {order[i]} down" },
                        Text = { Text = "▼", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColText },
                        RectTransform = { AnchorMin = $"0.915 {y - rowH + 0.008f}", AnchorMax = $"0.965 {y - 0.004f}" }
                    }, panel);
                y -= rowH;
            }

            // Post here, dismiss.
            y -= 0.015f;
            ui.Add(new CuiButton
            {
                Button = { Color = ColBtn, Command = "farmhand.ui post" },
                Text = { Text = Msg("Ui.PostHere", uid), FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColText },
                RectTransform = { AnchorMin = $"0.03 {y - 0.05f}", AnchorMax = $"0.48 {y}" }
            }, panel);
            ui.Add(new CuiButton
            {
                Button = { Color = ColOff, Command = "farmhand.ui dismiss" },
                Text = { Text = Msg("Ui.Dismiss", uid), FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColText },
                RectTransform = { AnchorMin = $"0.52 {y - 0.05f}", AnchorMax = $"0.97 {y}" }
            }, panel);
            y -= 0.05f;
        }

        private void DrawSetup(CuiElementContainer ui, string panel, ref float y, FarmData hand, LiveHand liveHand, string uid)
        {
            Line(ui, panel, ref y, 0.032f, Msg("Ui.Tally", uid, hand.Harvested, hand.Planted, hand.Watered, hand.Trips), ColDim, 10);
            Line(ui, panel, ref y, 0.032f, Msg("Ui.MarketTally", uid, hand.Runs, hand.Earned, hand.VendorTrips), ColDim, 10);
            int planters = 0, plants = 0, coops = 0, hives = 0, troughs = 0, generators = 0;
            foreach (var planter in liveHand.Planters)
            {
                if (planter == null || planter.IsDestroyed) continue;
                planters++;
                foreach (var child in planter.children)
                    if (child is GrowableEntity) plants++;
            }
            foreach (var coop in liveHand.Coops) if (coop != null && !coop.IsDestroyed) coops++;
            foreach (var hive in liveHand.Hives) if (hive != null && !hive.IsDestroyed) hives++;
            foreach (var trough in liveHand.Troughs) if (trough != null && !trough.IsDestroyed) troughs++;
            foreach (var generator in liveHand.Generators) if (generator != null && !generator.IsDestroyed) generators++;
            Line(ui, panel, ref y, 0.032f, Msg("Ui.Farm", uid, planters, plants, coops, hives, troughs, generators), ColDim, 10);
            y -= 0.01f;
            Heading(ui, panel, ref y, Msg("Ui.Hands", uid, hand.Hands.Count, MaxPerFarm));
            foreach (var worker in hand.Hands)
            {
                string away = worker.AwayUntil > Now
                    ? " · " + Msg(worker.AwayKind == "market" ? "Ui.AwayMarket" : worker.AwayKind == "vendor" ? "Ui.AwayVendor" : "Ui.Away", uid, FormatSpan(worker.AwayUntil - Now))
                    : "";
                Line(ui, panel, ref y, 0.032f, NameOf(worker, uid) + " · " + Msg("Ui.RoleHint." + worker.Role, uid) + away, worker == liveHand.Worker ? ColText : ColDim, 9);
            }
            if (hand.FetchWater)
            {
                if (!liveHand.WaterSearched) FindFreshWater(liveHand);
                Line(ui, panel, ref y, 0.032f,
                    liveHand.WaterPoint != Vector3.zero
                        ? Msg("Ui.WaterNear", uid, Mathf.RoundToInt(Vector3.Distance(liveHand.Home, liveHand.WaterPoint)))
                        : Msg("Ui.WaterNone", uid, Mathf.RoundToInt(config.WaterRange)),
                    liveHand.WaterPoint != Vector3.zero ? ColDim : ColWarn, 10);
            }

            y -= 0.015f;
            Heading(ui, panel, ref y, Msg("Ui.Boxes", uid));
            var outputs = OutputsOf(hand);
            string outputNames = null;
            foreach (var box in outputs) outputNames = outputNames == null ? BoxName(box, uid) : outputNames + ", " + BoxName(box, uid);
            BoxLine(ui, panel, ref y, Msg("Ui.BoxOutput", uid), outputNames, "output", uid);
            BoxLine(ui, panel, ref y, Msg("Ui.BoxSupply", uid), BoxName(FindBox(hand.Supply), uid), "supply", uid);
            BoxLine(ui, panel, ref y, Msg("Ui.BoxWater", uid), BoxName(FindWater(hand.Water), uid), "water", uid);
            BoxLine(ui, panel, ref y, Msg("Ui.BoxCompost", uid), BoxName(FindBox(hand.Compost) as Composter, uid), "compost", uid);
            BoxLine(ui, panel, ref y, Msg("Ui.BoxFuel", uid), BoxName(FindBox(hand.Fuel), uid) ?? Msg("Ui.BoxFuelHint", uid), "fuel", uid);
            BoxLine(ui, panel, ref y, Msg("Ui.BoxTakings", uid), BoxName(FindBox(hand.Takings), uid) ?? Msg("Ui.BoxTakingsHint", uid), "takings", uid);

            // The containers in reach, each with the roles it can take.
            y -= 0.015f;
            Heading(ui, panel, ref y, Msg("Ui.Candidates", uid));
            ui.Add(new CuiButton
            {
                Button = { Color = ColBtn, Command = "farmhand.ui autosetup" },
                Text = { Text = Msg("Ui.AutoSetup", uid), FontSize = 9, Align = TextAnchor.MiddleCenter, Color = ColText },
                RectTransform = { AnchorMin = $"0.62 {y + 0.004f}", AnchorMax = $"0.97 {y + 0.036f}" }
            }, panel);
            Line(ui, panel, ref y, 0.036f, Msg("Ui.SetupHint", uid), ColDim, 9);
            var candidates = Candidates(hand, liveHand.Home);
            if (candidates.Count == 0) { Line(ui, panel, ref y, 0.04f, Msg("Ui.NoCandidates", uid), ColWarn, 9); return; }
            const float rowH = 0.04f;
            int fit = Mathf.Max(1, Mathf.FloorToInt((y - 0.02f) / rowH));
            int shown = 0;
            foreach (var box in candidates)
            {
                if (shown++ >= fit) break;
                float top = y, bottom = y - rowH + 0.004f;
                y -= rowH;
                ulong id = box.net.ID.Value;
                string kind = BoxKind(box);
                string tag = hand.Outputs.Contains(id) ? Msg("Ui.Is.output", uid) : hand.Supply == id ? Msg("Ui.Is.supply", uid)
                    : hand.Water == id ? Msg("Ui.Is.water", uid) : hand.Compost == id ? Msg("Ui.Is.compost", uid) : hand.Fuel == id ? Msg("Ui.Is.fuel", uid) : hand.Takings == id ? Msg("Ui.Is.takings", uid) : "";
                ui.Add(new CuiPanel { Image = { Color = ColRow }, RectTransform = { AnchorMin = $"0.03 {bottom}", AnchorMax = $"0.97 {top}" } }, panel);
                ui.Add(new CuiLabel
                {
                    Text = { Text = BoxName(box, uid) + " · " + Msg("Ui.Dist", uid, Mathf.RoundToInt(Vector3.Distance(box.transform.position, liveHand.Home))), FontSize = 9, Align = TextAnchor.MiddleLeft, Color = tag.Length > 0 ? ColText : ColDim },
                    RectTransform = { AnchorMin = $"0.04 {bottom}", AnchorMax = $"0.42 {top}" }
                }, panel);
                ui.Add(new CuiLabel
                {
                    Text = { Text = tag, FontSize = 9, Align = TextAnchor.MiddleLeft, Color = ColTitle },
                    RectTransform = { AnchorMin = $"0.42 {bottom}", AnchorMax = $"0.60 {top}" }
                }, panel);
                string[] roles = kind == "water" ? new[] { "water" } : kind == "compost" ? new[] { "compost" } : new[] { "output", "supply", "fuel", "takings" };
                float bx = 0.58f, bw = roles.Length > 3 ? 0.092f : 0.115f, bs = roles.Length > 3 ? 0.0975f : 0.12f;
                foreach (var role in roles)
                {
                    bool on = role == "output" ? hand.Outputs.Contains(id) : role == "supply" ? hand.Supply == id : role == "water" ? hand.Water == id : role == "compost" ? hand.Compost == id : role == "fuel" ? hand.Fuel == id : hand.Takings == id;
                    ui.Add(new CuiButton
                    {
                        Button = { Color = on ? ColOn : ColBtn, Command = $"farmhand.ui box {role} {id}" },
                        Text = { Text = Msg("Ui.Pick." + role, uid), FontSize = 8, Align = TextAnchor.MiddleCenter, Color = ColText },
                        RectTransform = { AnchorMin = $"{bx} {bottom + 0.004f}", AnchorMax = $"{bx + bw} {top - 0.004f}" }
                    }, panel);
                    bx += bs;
                }
            }
        }

        // The vendor: the livestock vendor's whereabouts, what a run could take, and the rules.
        private void DrawVendor(CuiElementContainer ui, string panel, ref float y, FarmData hand, LiveHand liveHand, string uid)
        {
            if (!liveHand.VendorSearched) FindVendor(liveHand);
            var vendor = liveHand.Vendor;
            Line(ui, panel, ref y, 0.032f,
                vendor != null && !vendor.IsDestroyed
                    ? Msg("Ui.VendorNear", uid, Mathf.RoundToInt(Vector3.Distance(liveHand.Home, vendor.transform.position)))
                    : Msg("Ui.VendorNone", uid, Mathf.RoundToInt(config.VendorRange)),
                vendor != null && !vendor.IsDestroyed ? ColDim : ColWarn, 10);
            int cows, spareCows, sheep, spareSheep;
            VendorStock(liveHand, FindCupboard(hand.Cupboard), hand, out cows, out spareCows, out sheep, out spareSheep);
            Line(ui, panel, ref y, 0.032f,
                Msg("Ui.VendorStock", uid, spareCows, cows, spareSheep, sheep, SpareWool(hand, OutputsOf(hand)), Mathf.Max(1, ConVar.Livestock.vendorWoolMinimum)),
                ColDim, 9);
            if (hand.BuyLivestock)
            {
                var wanted = WantedAnimals(liveHand, hand);
                string names = null;
                int cost = 0;
                foreach (var kind in wanted) { names = (names == null ? "" : names + ", ") + Msg("Ui.Animal." + kind, uid); cost += PriceOf(kind); }
                int scrap = TotalOf(OutputsOf(hand), ScrapShortname);
                if (wanted.Count > 0)
                    Line(ui, panel, ref y, 0.032f, Msg("Ui.VendorWanted", uid, names, cost, scrap), scrap >= cost ? ColDim : ColWarn, 9);
            }
            y -= 0.01f;
            DutyLine(ui, panel, ref y, Msg("Ui.DutyVendor", uid), hand.VendorRuns, "vendor", uid);
            SettingLine(ui, panel, ref y, Msg("Ui.AnimalsPerRun", uid), hand.AnimalsPerRun.ToString(), "farmhand.ui animalsperrun", 1, uid);
            SettingLine(ui, panel, ref y, Msg("Ui.VendorHours", uid), Msg("Ui.Hours", uid, hand.VendorHours), "farmhand.ui vendorhours", 1, uid);
            DutyLine(ui, panel, ref y, Msg("Ui.DutyBuyStock", uid, Mathf.Max(0, config.CalfPrice), Mathf.Max(0, config.LambPrice)), hand.BuyLivestock, "buystock", uid);
            SettingLine(ui, panel, ref y, Msg("Ui.BuyPerRun", uid), hand.BuyPerRun.ToString(), "farmhand.ui buyperrun", 1, uid);
            Line(ui, panel, ref y, 0.06f, Msg("Ui.VendorHint", uid), ColDim, 9);
        }

        // The yard: composter, generator, water runs and the baking.
        private void DrawYard(CuiElementContainer ui, string panel, ref float y, FarmData hand, LiveHand liveHand, string uid)
        {
            DutyLine(ui, panel, ref y, Msg("Ui.DutyCompost", uid), hand.Composting, "compost", uid);
            DutyLine(ui, panel, ref y, Msg("Ui.DutyBiofuel", uid), hand.Biofuel, "biofuel", uid);
            SettingLine(ui, panel, ref y, Msg("Ui.DungShare", uid), hand.DungToGenerator + "%", "farmhand.ui dung", 25, uid);
            DutyLine(ui, panel, ref y, Msg("Ui.DutyFetch", uid), hand.FetchWater, "fetch", uid);

            // The kitchen's focus: three buttons, the current one lit.
            y -= 0.01f;
            Heading(ui, panel, ref y, Msg("Ui.Focus", uid));
            {
                const float h = 0.05f;
                float slot = 0.94f / Focuses.Length;
                for (int i = 0; i < Focuses.Length; i++)
                {
                    bool current = Focuses[i] == hand.Focus;
                    float x0 = 0.03f + i * slot, x1 = x0 + slot - 0.008f;
                    ui.Add(new CuiButton
                    {
                        Button = { Color = current ? ColOn : ColBtn, Command = $"farmhand.ui focus {Focuses[i]}" },
                        Text = { Text = Msg("Ui.Focus." + Focuses[i], uid), FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColText },
                        RectTransform = { AnchorMin = $"{x0} {y - h}", AnchorMax = $"{x1} {y}" }
                    }, panel);
                }
                y -= h;
            }
            Line(ui, panel, ref y, 0.05f, Msg("Ui.FocusHint." + hand.Focus, uid), ColDim, 9);
            if (liveHand.Workbenches.Count == 0) Line(ui, panel, ref y, 0.036f, Msg("Ui.NoWorkbench", uid), ColWarn, 9);

            if (hand.Focus == "bread")
            {
                SettingLine(ui, panel, ref y, Msg("Ui.Bake", uid), Msg(hand.Bake >= 2 ? "Ui.BakeBrioche" : hand.Bake == 1 ? "Ui.BakeBread" : "Ui.BakeOff", uid), "farmhand.ui bake", 1, uid);
                SettingLine(ui, panel, ref y, Msg("Ui.LoavesKeep", uid), hand.LoavesToKeep.ToString(), "farmhand.ui loaves", 5, uid);
            }
            else if (hand.Focus == "pies")
            {
                SettingLine(ui, panel, ref y, Msg("Ui.PiesKeep", uid), hand.PiesToKeep.ToString(), "farmhand.ui pies", 5, uid);
                if (liveHand.Ovens.Count == 0) Line(ui, panel, ref y, 0.036f, Msg("Ui.NoBbq", uid), ColWarn, 9);
                else
                {
                    // What the fire has to work with: wood, raw meat, cooked meat.
                    var supply = FindBox(hand.Supply);
                    var outputs = OutputsOf(hand);
                    int wood = supply != null ? CountOf(supply, WoodShortname) : 0;
                    bool lit = false;
                    foreach (var oven in liveHand.Ovens) if (oven != null && !oven.IsDestroyed && oven.FindBurnable() != null) lit = true;
                    Line(ui, panel, ref y, 0.032f, Msg("Ui.Fire", uid, wood), wood > 0 || lit ? ColDim : ColWarn, 9);
                    string meats = null;
                    foreach (var rawName in RawMeats)
                    {
                        var rawDef = ItemManager.FindItemDefinition(rawName);
                        var cookable = rawDef != null ? rawDef.GetComponent<ItemModCookable>() : null;
                        string cooked = cookable != null && cookable.becomeOnCooked != null ? cookable.becomeOnCooked.shortname : null;
                        string part = (rawDef != null ? rawDef.displayName.english : rawName) + " " + TotalOf(outputs, rawName) + "/" + (cooked != null ? TotalOf(outputs, cooked) : 0);
                        meats = meats == null ? part : meats + " · " + part;
                    }
                    Line(ui, panel, ref y, 0.032f, Msg("Ui.Meat", uid, meats, CookedMeatKept), ColDim, 9);

                    // What the workbench could bake right now, and what stops the rest.
                    foreach (var table in liveHand.Workbenches)
                    {
                        if (table == null || table.IsDestroyed || table.inventory == null) continue;
                        string held = null;
                        foreach (var item in table.inventory.itemList)
                            if (item != null) held = (held == null ? "" : held + ", ") + item.amount + " " + item.info.displayName.english;
                        if (held != null) Line(ui, panel, ref y, 0.032f, Msg("Ui.TableHolds", uid, held, table.IsOn() ? Msg("Ui.TableOn", uid) : ""), ColDim, 9);
                        var boxes = new List<BaseCombatEntity>(outputs);
                        if (supply != null) boxes.Add(supply);
                        string can = null, cannot = null;
                        foreach (var pie in Pies)
                        {
                            var recipe = FindRecipe(table, pie);
                            if (recipe == null) continue;
                            string pieName = recipe.ProducedItem.displayName.english;
                            int batches = Affordable(recipe, boxes, hand);
                            if (batches > 0) { can = (can == null ? "" : can + ", ") + pieName + " " + batches; continue; }
                            string shortOf = null;
                            foreach (var ingredient in recipe.Ingredients)
                            {
                                if (ingredient.Ingredient == null) continue;
                                int need = ingredient.GetIngredientCount(recipe.ProducedItem);
                                int reserved = Reserved(hand, ingredient.Ingredient.shortname);
                                int have = TotalOf(boxes, ingredient.Ingredient.shortname) - reserved;
                                if (have < need) shortOf = (shortOf == null ? "" : shortOf + ", ") + ingredient.Ingredient.displayName.english + " " + Mathf.Max(0, have) + "/" + need + (reserved > 0 ? " (" + reserved + " kept for feed)" : "");
                            }
                            cannot = (cannot == null ? "" : cannot + "; ") + pieName + ": " + (shortOf ?? "?");
                        }
                        if (can != null) Line(ui, panel, ref y, 0.032f, Msg("Ui.CanBake", uid, can), ColDim, 9);
                        if (cannot != null) Line(ui, panel, ref y, 0.05f, Msg("Ui.CannotBake", uid, cannot), ColWarn, 8);
                        break;
                    }
                }
            }
            else
            {
                SettingLine(ui, panel, ref y, Msg("Ui.TeasKeep", uid), hand.TeasToKeep.ToString(), "farmhand.ui teas", 5, uid);
                DutyLine(ui, panel, ref y, Msg("Ui.AdvancedTeas", uid), hand.AdvancedTeas, "advteas", uid);
                var known = TeasKnown(liveHand);
                if (known.Count == 0) { Line(ui, panel, ref y, 0.036f, Msg("Ui.NoTeas", uid), ColDim, 9); return; }
                float rowH = Mathf.Min(0.045f, Mathf.Max(0.028f, (y - 0.02f) / known.Count));
                foreach (var tea in known)
                {
                    float top = y, bottom = y - rowH + 0.004f;
                    y -= rowH;
                    bool on = hand.Teas.Contains(tea.shortname);
                    ui.Add(new CuiPanel { Image = { Color = ColRow }, RectTransform = { AnchorMin = $"0.03 {bottom}", AnchorMax = $"0.97 {top}" } }, panel);
                    ui.Add(new CuiLabel
                    {
                        Text = { Text = tea.displayName.english, FontSize = 9, Align = TextAnchor.MiddleLeft, Color = on ? ColText : ColDim },
                        RectTransform = { AnchorMin = $"0.05 {bottom}", AnchorMax = $"0.80 {top}" }
                    }, panel);
                    ui.Add(new CuiButton
                    {
                        Button = { Color = on ? ColOn : ColOff, Command = $"farmhand.ui tea {tea.shortname}" },
                        Text = { Text = Msg(on ? "Ui.On" : "Ui.Off", uid), FontSize = 8, Align = TextAnchor.MiddleCenter, Color = ColText },
                        RectTransform = { AnchorMin = $"0.86 {bottom + 0.004f}", AnchorMax = $"0.96 {top - 0.004f}" }
                    }, panel);
                }
            }
        }

        // The herd tab: every cow and sheep inside the farm, its genes and line, and what
        // the farm hand intends for it.
        private void DrawHerd(CuiElementContainer ui, string panel, ref float y, FarmData hand, LiveHand liveHand, string uid)
        {
            Line(ui, panel, ref y, 0.04f, Msg("Ui.HerdHint", uid), ColDim, 9);
            if (liveHand == null) { Line(ui, panel, ref y, 0.05f, Msg("Ui.HerdNone", uid), ColDim, 10); return; }
            var herd = PlanHerd(liveHand, FindCupboard(hand.Cupboard), hand);
            if (herd.Count == 0) { Line(ui, panel, ref y, 0.05f, Msg("Ui.HerdNone", uid), ColDim, 10); return; }

            if (!hand.Breeding) Line(ui, panel, ref y, 0.05f, Msg("Ui.HerdPlain", uid), ColWarn, 9);
            else if (!hand.CullLivestock) Line(ui, panel, ref y, 0.05f, Msg("Ui.HerdNoKnife", uid), ColWarn, 9);
            if (hand.Breeding)
                foreach (var species in new[] { "cow", "sheep" })
                {
                    HerdEntry bull = null;
                    int kept = 0, shared = 0;
                    bool any = false;
                    foreach (var entry in herd)
                    {
                        if (entry.Species != species) continue;
                        any = true;
                        if (entry.Plan == "bull") bull = entry;
                    }
                    if (!any) continue;
                    foreach (var entry in herd)
                        if (entry.Species == species && (entry.Plan == "keep" || entry.Plan == "carrying") && !entry.Male)
                        {
                            kept++;
                            if (bull != null && SharesLine(bull, entry)) shared++;
                        }
                    string label = species == "cow" ? "Cattle" : "Sheep";
                    if (bull == null) Line(ui, panel, ref y, 0.032f, Msg("Ui.HerdNoBull", uid, label), ColWarn, 10);
                    else
                    {
                        bool stale = kept > 0 && shared * 2 >= kept;
                        Line(ui, panel, ref y, 0.032f, Msg("Ui.HerdBull", uid, label, LivestockVendor.NameOf(bull.Animal), shared, kept), stale ? ColWarn : ColDim, 10);
                        if (stale) Line(ui, panel, ref y, 0.032f, Msg("Ui.HerdFreshBlood", uid), ColWarn, 9);
                    }
                }

            var headings = new[]
            {
                new { Key = "Ui.ColAnimal", X0 = 0.04f, X1 = 0.24f, Align = TextAnchor.MiddleLeft },
                new { Key = "Ui.ColSex", X0 = 0.24f, X1 = 0.33f, Align = TextAnchor.MiddleLeft },
                new { Key = "Ui.ColGenes", X0 = 0.33f, X1 = 0.63f, Align = TextAnchor.MiddleLeft },
                new { Key = "Ui.ColLine", X0 = 0.63f, X1 = 0.75f, Align = TextAnchor.MiddleLeft },
                new { Key = "Ui.ColPlan", X0 = 0.75f, X1 = 0.97f, Align = TextAnchor.MiddleLeft },
            };
            foreach (var heading in headings)
                ui.Add(new CuiLabel
                {
                    Text = { Text = Msg(heading.Key, uid), FontSize = 9, Align = heading.Align, Color = ColTitle },
                    RectTransform = { AnchorMin = $"{heading.X0} {y - 0.036f}", AnchorMax = $"{heading.X1} {y}" }
                }, panel);
            y -= 0.038f;

            // Cattle before sheep; the bull first, then by breeding value.
            herd.Sort((a, b) =>
            {
                if (a.Species != b.Species) return a.Species == "cow" ? -1 : 1;
                if ((a.Plan == "bull") != (b.Plan == "bull")) return a.Plan == "bull" ? -1 : 1;
                if (a.Adult != b.Adult) return a.Adult ? -1 : 1;
                if (a.Score != b.Score) return b.Score.CompareTo(a.Score);
                return a.Animal.net.ID.Value.CompareTo(b.Animal.net.ID.Value);
            });
            int fit = Mathf.Max(1, Mathf.FloorToInt((y - 0.02f) / 0.025f));
            float rowH = Mathf.Min(0.045f, Mathf.Max(0.025f, (y - 0.02f) / Mathf.Max(1, Mathf.Min(herd.Count, fit))));
            int shown = 0;
            foreach (var entry in herd)
            {
                if (shown++ >= fit) break;
                float top = y, bottom = y - rowH + 0.004f;
                y -= rowH;
                var animal = entry.Animal;
                string sex = Msg(entry.Male ? "Ui.Sex.M" : "Ui.Sex.F", uid) + (animal.IsInfant() ? " " + Msg("Ui.Age.calf", uid) : !entry.Adult ? " " + Msg("Ui.Age.young", uid) : "");
                string line = entry.Inbred ? Msg("Ui.Inbred", uid) : entry.Marker0 + "·" + entry.Marker1;
                string colour = entry.Plan == "knife" || entry.Inbred ? ColWarn : entry.Plan == "bull" || entry.Plan == "keep" ? ColText : ColDim;
                ui.Add(new CuiPanel { Image = { Color = ColRow }, RectTransform = { AnchorMin = $"0.03 {bottom}", AnchorMax = $"0.97 {top}" } }, panel);
                var cells = new[]
                {
                    new { Text = LivestockVendor.NameOf(animal), X0 = 0.04f, X1 = 0.24f, Color = colour },
                    new { Text = sex, X0 = 0.24f, X1 = 0.33f, Color = colour },
                    new { Text = GenesText(animal.Genes), X0 = 0.33f, X1 = 0.63f, Color = colour },
                    new { Text = line, X0 = 0.63f, X1 = 0.75f, Color = entry.Inbred ? ColWarn : colour },
                    new { Text = Msg("Ui.Plan." + entry.Plan, uid), X0 = 0.75f, X1 = 0.97f, Color = colour },
                };
                foreach (var cell in cells)
                    ui.Add(new CuiLabel
                    {
                        Text = { Text = cell.Text, FontSize = 9, Align = TextAnchor.MiddleLeft, Color = cell.Color },
                        RectTransform = { AnchorMin = $"{cell.X0} {bottom}", AnchorMax = $"{cell.X1} {top}" }
                    }, panel);
            }
        }

        // The market tab: the run itself, then what is kept at home of each produce the
        // stalls buy. Rows are the produce in the boxes plus anything with a number of
        // its own, so a setting on something sold out is still there to change.
        private void DrawMarket(CuiElementContainer ui, string panel, ref float y, FarmData hand, LiveHand liveHand, string uid)
        {
            if (hand.MarketRuns)
            {
                if (!liveHand.MarketSearched) FindMarket(liveHand);
                Line(ui, panel, ref y, 0.032f,
                    !MarketOpen ? Msg("Ui.MarketClosed", uid)
                    : liveHand.MarketPoint != Vector3.zero ? Msg("Ui.MarketNear", uid, Mathf.RoundToInt(Vector3.Distance(liveHand.Home, liveHand.MarketPoint)))
                    : Msg("Ui.MarketNone", uid, Mathf.RoundToInt(config.MarketRange)),
                    MarketOpen && liveHand.MarketPoint != Vector3.zero ? ColDim : ColWarn, 10);
            }
            DutyLine(ui, panel, ref y, Msg("Ui.DutyMarket", uid), hand.MarketRuns, "market", uid);
            SettingLine(ui, panel, ref y, Msg("Ui.MarketHours", uid), Msg("Ui.Hours", uid, hand.MarketHours), "farmhand.ui markethours", 1, uid);
            DutyLine(ui, panel, ref y, Msg("Ui.DutyBuySeeds", uid), hand.BuySeeds, "buyseeds", uid);
            SettingLine(ui, panel, ref y, Msg("Ui.KeepProduce", uid), hand.KeepProduce.ToString(), "farmhand.ui keepproduce", 10, uid);
            SettingLine(ui, panel, ref y, Msg("Ui.StockCap", uid), hand.StockCap.ToString(), "farmhand.ui stockcap", 50, uid);
            y -= 0.01f;
            Heading(ui, panel, ref y, Msg("Ui.KeepEachHead", uid));
            ui.Add(new CuiLabel
            {
                Text = { Text = Msg("Ui.KeepEachCols", uid), FontSize = 9, Align = TextAnchor.MiddleRight, Color = ColTitle },
                RectTransform = { AnchorMin = $"0.40 {y}", AnchorMax = $"0.90 {y + 0.036f}" }
            }, panel);
            Line(ui, panel, ref y, 0.036f, Msg("Ui.KeepEach", uid), ColDim, 9);

            var names = new SortedDictionary<string, string>();
            var seen = new HashSet<string>();
            foreach (var box in OutputsOf(hand))
            {
                var inventory = InventoryOf(box);
                if (inventory == null) continue;
                foreach (var item in inventory.itemList)
                {
                    if (item == null || item.amount <= 0 || cropByItem.ContainsKey(item.info.itemid)) continue;
                    if (seen.Contains(item.info.shortname)) continue;
                    if (PoolOf(item.info.shortname) != item.info.shortname || KeptForBaking(hand, item.info.shortname)) continue;
                    if (MarketOpen && BuyPrice(item.info.shortname) < 0) continue;
                    seen.Add(item.info.shortname);
                    names[item.info.displayName.english] = item.info.shortname;
                }
            }
            var owned = new List<string>();
            if (hand.KeepEach != null) owned.AddRange(hand.KeepEach.Keys);
            if (hand.CapEach != null) owned.AddRange(hand.CapEach.Keys);
            foreach (var shortname in owned)
            {
                if (!seen.Add(shortname) || PoolOf(shortname) != shortname || KeptForBaking(hand, shortname)) continue;
                var def = ItemManager.FindItemDefinition(shortname);
                names[def != null ? def.displayName.english : shortname] = shortname;
            }
            if (names.Count == 0) { Line(ui, panel, ref y, 0.05f, Msg("Ui.KeepNone", uid), ColDim, 10); return; }

            float rowH = Mathf.Min(0.05f, Mathf.Max(0.025f, (y - 0.02f) / names.Count));
            foreach (var pair in names)
            {
                float top = y, bottom = y - rowH + 0.004f;
                y -= rowH;
                bool own = (hand.KeepEach != null && hand.KeepEach.ContainsKey(pair.Value)) || (hand.CapEach != null && hand.CapEach.ContainsKey(pair.Value));
                ui.Add(new CuiPanel { Image = { Color = ColRow }, RectTransform = { AnchorMin = $"0.03 {bottom}", AnchorMax = $"0.97 {top}" } }, panel);
                ui.Add(new CuiLabel
                {
                    Text = { Text = pair.Key, FontSize = 10, Align = TextAnchor.MiddleLeft, Color = own ? ColText : ColDim },
                    RectTransform = { AnchorMin = $"0.04 {bottom}", AnchorMax = $"0.40 {top}" }
                }, panel);
                Stepper(ui, panel, 0.41f, bottom, top, KeepOf(hand, pair.Value).ToString(), $"farmhand.ui keepof {pair.Value}", 10, uid);
                Stepper(ui, panel, 0.64f, bottom, top, CapOf(hand, pair.Value).ToString(), $"farmhand.ui capof {pair.Value}", 50, uid);
                if (own)
                    ui.Add(new CuiButton
                    {
                        Button = { Color = ColOff, Command = $"farmhand.ui keepreset {pair.Value}" },
                        Text = { Text = "×", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColText },
                        RectTransform = { AnchorMin = $"0.915 {bottom + 0.004f}", AnchorMax = $"0.96 {top - 0.004f}" }
                    }, panel);
            }
        }

        private void DrawAnimals(CuiElementContainer ui, string panel, ref float y, FarmData hand, LiveHand liveHand, string uid)
        {
            // Nothing to feed them with: say so, since an empty manger otherwise looks like idleness.
            var supply = FindBox(hand.Supply);
            var outputs = OutputsOf(hand);
            bool hungry = false;
            foreach (var trough in liveHand.Troughs)
                if (trough != null && !trough.IsDestroyed && trough.inventory != null && trough.GetLivestockFoodItem() == null) hungry = true;
            foreach (var coop in liveHand.Coops)
                if (coop != null && !coop.IsDestroyed && coop.inventory != null && coop.inventory.GetSlot(CoopFoodSlot) == null) hungry = true;
            if (hungry && FindCoopFood(hand, supply, outputs, null) == null)
                Line(ui, panel, ref y, 0.06f, Msg("Ui.NoFeed", uid), ColWarn, 9);
            DutyLine(ui, panel, ref y, Msg("Ui.DutyChickens", uid), hand.Chickens, "chickens", uid);
            SettingLine(ui, panel, ref y, Msg("Ui.KeepChickens", uid), hand.ChickensWanted.ToString(), "farmhand.ui keep", 1, uid);
            SettingLine(ui, panel, ref y, Msg("Ui.Butcher", uid), hand.ButcherHours > 0 ? Msg("Ui.Hours", uid, hand.ButcherHours) : Msg("Ui.Never", uid), "farmhand.ui butcher", 6, uid);
            DutyLine(ui, panel, ref y, Msg("Ui.DutyHoney", uid), hand.Honey, "honey", uid);
            DutyLine(ui, panel, ref y, Msg("Ui.DutyLivestock", uid), hand.Livestock, "livestock", uid);
            DutyLine(ui, panel, ref y, Msg("Ui.DutyCull", uid), hand.CullLivestock, "cull", uid);
            DutyLine(ui, panel, ref y, Msg("Ui.DutyBreeding", uid), hand.Breeding, "breeding", uid);
            SettingLine(ui, panel, ref y, Msg("Ui.KeepCows", uid), hand.KeepCows.ToString(), "farmhand.ui keepcows", 1, uid);
            SettingLine(ui, panel, ref y, Msg("Ui.KeepSheep", uid), hand.KeepSheep.ToString(), "farmhand.ui keepsheep", 1, uid);
        }

        // One row per crop: the plant switch and the water band.
        private void DrawCrops(CuiElementContainer ui, string panel, ref float y, FarmData hand, string uid)
        {
            DutyLine(ui, panel, ref y, Msg("Ui.DutyHarvest", uid), hand.Harvest, "harvest", uid);
            DutyLine(ui, panel, ref y, Msg("Ui.DutyReplant", uid), hand.Replant, "replant", uid);
            DutyLine(ui, panel, ref y, Msg("Ui.DutyWater", uid), hand.Watering, "water", uid);
            DutyLine(ui, panel, ref y, Msg("Ui.DutyFertilise", uid), hand.Fertilise, "fertilise", uid);
            ui.Add(new CuiButton
            {
                Button = { Color = ColBtn, Command = "farmhand.ui plantfocus" },
                Text = { Text = Msg("Ui.PlantFocus", uid, Msg("Ui.Focus." + hand.Focus, uid)), FontSize = 9, Align = TextAnchor.MiddleCenter, Color = ColText },
                RectTransform = { AnchorMin = $"0.55 {y - 0.04f}", AnchorMax = $"0.97 {y - 0.004f}" }
            }, panel);
            ui.Add(new CuiLabel
            {
                Text = { Text = Msg("Ui.PlantFocusHint", uid), FontSize = 9, Align = TextAnchor.MiddleLeft, Color = ColDim },
                RectTransform = { AnchorMin = $"0.04 {y - 0.04f}", AnchorMax = $"0.54 {y}" }
            }, panel);
            y -= 0.045f;

            // Column headings, over the columns they name.
            var headings = new[]
            {
                new { Key = "Ui.ColCrop", X0 = 0.04f, X1 = 0.25f, Align = TextAnchor.MiddleLeft },
                new { Key = "Ui.ColPlant", X0 = 0.25f, X1 = 0.32f, Align = TextAnchor.MiddleCenter },
                new { Key = "Ui.ColFeed", X0 = 0.32f, X1 = 0.39f, Align = TextAnchor.MiddleCenter },
                new { Key = "Ui.ColSeeds", X0 = 0.40f, X1 = 0.59f, Align = TextAnchor.MiddleCenter },
                new { Key = "Ui.ColBelow", X0 = 0.595f, X1 = 0.785f, Align = TextAnchor.MiddleCenter },
                new { Key = "Ui.ColFill", X0 = 0.79f, X1 = 0.98f, Align = TextAnchor.MiddleCenter },
            };
            foreach (var heading in headings)
                ui.Add(new CuiLabel
                {
                    Text = { Text = Msg(heading.Key, uid), FontSize = 9, Align = heading.Align, Color = ColTitle },
                    RectTransform = { AnchorMin = $"{heading.X0} {y - 0.04f}", AnchorMax = $"{heading.X1} {y}" }
                }, panel);
            y -= 0.042f;
            float rowH = Mathf.Min(0.05f, Mathf.Max(0.025f, (y - 0.02f) / Mathf.Max(1, crops.Count)));
            foreach (var crop in crops)
            {
                float top = y, bottom = y - rowH + 0.004f;
                y -= rowH;
                ui.Add(new CuiPanel { Image = { Color = ColRow }, RectTransform = { AnchorMin = $"0.03 {bottom}", AnchorMax = $"0.97 {top}" } }, panel);
                ui.Add(new CuiLabel
                {
                    Text = { Text = crop.Name, FontSize = 10, Align = TextAnchor.MiddleLeft, Color = ColText },
                    RectTransform = { AnchorMin = $"0.04 {bottom}", AnchorMax = $"0.25 {top}" }
                }, panel);
                bool plant = PlantsCrop(hand, crop);
                ui.Add(new CuiButton
                {
                    Button = { Color = plant ? ColOn : ColOff, Command = $"farmhand.ui plant {crop.Key}" },
                    Text = { Text = Msg(plant ? "Ui.On" : "Ui.Off", uid), FontSize = 9, Align = TextAnchor.MiddleCenter, Color = ColText },
                    RectTransform = { AnchorMin = $"0.255 {bottom + 0.004f}", AnchorMax = $"0.315 {top - 0.004f}" }
                }, panel);
                bool feed = FeedsCrop(hand, crop);
                ui.Add(new CuiButton
                {
                    Button = { Color = feed ? ColOn : ColOff, Command = $"farmhand.ui feed {crop.Key}" },
                    Text = { Text = Msg(feed ? "Ui.On" : "Ui.Off", uid), FontSize = 9, Align = TextAnchor.MiddleCenter, Color = ColText },
                    RectTransform = { AnchorMin = $"0.325 {bottom + 0.004f}", AnchorMax = $"0.385 {top - 0.004f}" }
                }, panel);
                Stepper(ui, panel, 0.40f, bottom, top, KeepSeeds(hand, crop).ToString(), $"farmhand.ui seeds {crop.Key}", 5, uid);
                Stepper(ui, panel, 0.595f, bottom, top, MinWater(hand, crop) + "%", $"farmhand.ui min {crop.Key}", 10, uid);
                Stepper(ui, panel, 0.79f, bottom, top, MaxWater(hand, crop) + "%", $"farmhand.ui max {crop.Key}", 10, uid);
            }
        }

        private static void Line(CuiElementContainer ui, string panel, ref float y, float height, string text, string color, int size)
        {
            ui.Add(new CuiLabel
            {
                Text = { Text = text, FontSize = size, Align = TextAnchor.MiddleCenter, Color = color },
                RectTransform = { AnchorMin = $"0.03 {y - height}", AnchorMax = $"0.97 {y}" }
            }, panel);
            y -= height;
        }

        private static void Heading(CuiElementContainer ui, string panel, ref float y, string text)
        {
            ui.Add(new CuiLabel
            {
                Text = { Text = text, FontSize = 11, Align = TextAnchor.MiddleLeft, Color = ColTitle },
                RectTransform = { AnchorMin = $"0.04 {y - 0.036f}", AnchorMax = $"0.6 {y}" }
            }, panel);
            y -= 0.036f;
        }

        private void BoxLine(CuiElementContainer ui, string panel, ref float y, string label, string box, string which, string uid)
        {
            const float h = 0.045f;
            ui.Add(new CuiPanel { Image = { Color = ColRow }, RectTransform = { AnchorMin = $"0.03 {y - h + 0.003f}", AnchorMax = $"0.97 {y}" } }, panel);
            ui.Add(new CuiLabel
            {
                Text = { Text = label, FontSize = 10, Align = TextAnchor.MiddleLeft, Color = ColText },
                RectTransform = { AnchorMin = $"0.05 {y - h}", AnchorMax = $"0.30 {y}" }
            }, panel);
            ui.Add(new CuiLabel
            {
                Text =
                {
                    Text = box != null ? Msg("Ui.BoxSet", uid, box) : Msg("Ui.BoxUnset", uid, which),
                    FontSize = 10, Align = TextAnchor.MiddleLeft, Color = box != null ? ColText : ColWarn
                },
                RectTransform = { AnchorMin = $"0.30 {y - h}", AnchorMax = $"0.97 {y}" }
            }, panel);
            y -= h;
        }

        private void DutyLine(CuiElementContainer ui, string panel, ref float y, string label, bool on, string key, string uid)
        {
            const float h = 0.058f;
            ui.Add(new CuiPanel { Image = { Color = ColRow }, RectTransform = { AnchorMin = $"0.03 {y - h + 0.003f}", AnchorMax = $"0.97 {y}" } }, panel);
            ui.Add(new CuiLabel
            {
                Text = { Text = label, FontSize = 10, Align = TextAnchor.MiddleLeft, Color = ColText },
                RectTransform = { AnchorMin = $"0.05 {y - h}", AnchorMax = $"0.80 {y}" }
            }, panel);
            ui.Add(new CuiButton
            {
                Button = { Color = on ? ColOn : ColOff, Command = $"farmhand.ui duty {key}" },
                Text = { Text = Msg(on ? "Ui.On" : "Ui.Off", uid), FontSize = 9, Align = TextAnchor.MiddleCenter, Color = ColText },
                RectTransform = { AnchorMin = $"0.86 {y - h + 0.009f}", AnchorMax = $"0.96 {y - 0.005f}" }
            }, panel);
            y -= h;
        }

        // A row with a label and a [-] value [+] stepper on the right.
        private static void SettingLine(CuiElementContainer ui, string panel, ref float y, string label, string value, string command, int step, string uid)
        {
            const float h = 0.058f;
            ui.Add(new CuiPanel { Image = { Color = ColRow }, RectTransform = { AnchorMin = $"0.03 {y - h + 0.003f}", AnchorMax = $"0.97 {y}" } }, panel);
            ui.Add(new CuiLabel
            {
                Text = { Text = label, FontSize = 10, Align = TextAnchor.MiddleLeft, Color = ColText },
                RectTransform = { AnchorMin = $"0.05 {y - h}", AnchorMax = $"0.76 {y}" }
            }, panel);
            ui.Add(new CuiButton
            {
                Button = { Color = ColBtn, Command = $"{command} -{step}" },
                Text = { Text = "−", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColText },
                RectTransform = { AnchorMin = $"0.78 {y - h + 0.009f}", AnchorMax = $"0.825 {y - 0.005f}" }
            }, panel);
            ui.Add(new CuiLabel
            {
                Text = { Text = value, FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColText },
                RectTransform = { AnchorMin = $"0.825 {y - h}", AnchorMax = $"0.915 {y}" }
            }, panel);
            ui.Add(new CuiButton
            {
                Button = { Color = ColBtn, Command = $"{command} {step}" },
                Text = { Text = "+", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = ColText },
                RectTransform = { AnchorMin = $"0.915 {y - h + 0.009f}", AnchorMax = $"0.96 {y - 0.005f}" }
            }, panel);
            y -= h;
        }

        // [-] value [+], 0.19 wide
        private static void Stepper(CuiElementContainer ui, string panel, float x, float bottom, float top, string value, string command, int step, string uid)
        {
            int size = top - bottom < 0.035f ? 8 : 10;
            ui.Add(new CuiButton
            {
                Button = { Color = ColBtn, Command = $"{command} -{step}" },
                Text = { Text = "−", FontSize = size, Align = TextAnchor.MiddleCenter, Color = ColText },
                RectTransform = { AnchorMin = $"{x} {bottom + 0.004f}", AnchorMax = $"{x + 0.045f} {top - 0.004f}" }
            }, panel);
            ui.Add(new CuiLabel
            {
                Text = { Text = value, FontSize = size, Align = TextAnchor.MiddleCenter, Color = ColText },
                RectTransform = { AnchorMin = $"{x + 0.045f} {bottom}", AnchorMax = $"{x + 0.145f} {top}" }
            }, panel);
            ui.Add(new CuiButton
            {
                Button = { Color = ColBtn, Command = $"{command} {step}" },
                Text = { Text = "+", FontSize = size, Align = TextAnchor.MiddleCenter, Color = ColText },
                RectTransform = { AnchorMin = $"{x + 0.145f} {bottom + 0.004f}", AnchorMax = $"{x + 0.19f} {top - 0.004f}" }
            }, panel);
        }

        [ConsoleCommand("farmhand.ui")]
        private void CmdUi(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || dataBroken) return;
            string action = arg.GetString(0, "");
            string key = arg.GetString(1, "");
            int delta = arg.GetInt(2, 0);
            if (action == "close")
            {
                CuiHelper.DestroyUi(player, UiPanelName);
                panelTab.Remove(player.userID);
                panelHand.Remove(player.userID);
                return;
            }
            var liveHand = PanelHand(player);
            if (liveHand == null) { CuiHelper.DestroyUi(player, UiPanelName); return; }
            var hand = liveHand.Farm;
            var worker = liveHand.Worker;

            switch (action)
            {
                case "tab":
                    panelTab[player.userID] = key == "setup" || key == "crops" || key == "animals" || key == "herd" || key == "yard" || key == "market" || key == "vendor" ? key : "hand";
                    break;
                case "hand":
                {
                    ulong id;
                    if (ulong.TryParse(key, out id))
                        foreach (var other in hand.Hands)
                            if (other.Id == id) panelHand[player.userID] = id;
                    break;
                }
                case "focus":
                {
                    string focus = FocusOf(key);
                    if (focus != null) hand.Focus = focus;
                    break;
                }
                case "plantfocus":
                    PlantForFocus(hand);
                    break;
                case "pies":
                    hand.PiesToKeep = Mathf.Clamp(hand.PiesToKeep + arg.GetInt(1, 0), 0, 100);
                    break;
                case "teas":
                    hand.TeasToKeep = Mathf.Clamp(hand.TeasToKeep + arg.GetInt(1, 0), 0, 100);
                    break;
                case "tea":
                    if (key.Length > 0 && ItemManager.FindItemDefinition(key) != null)
                    {
                        if (hand.Teas.Contains(key)) hand.Teas.Remove(key); else hand.Teas.Add(key);
                    }
                    break;
                case "role":
                {
                    string role = RoleOf(key);
                    if (role != null && role != worker.Role)
                    {
                        Puts($"{player.displayName} made {worker.Name} the {worker.Role} a {role}.");
                        worker.Role = role;
                        if (liveHand.Npc != null && !liveHand.Npc.IsDestroyed)
                        {
                            liveHand.Npc.displayName = Msg("Ui.Named", null, worker.Name, Msg("Role." + role, null));
                            Dress(liveHand.Npc, role);
                        }
                    }
                    break;
                }
                case "post":
                {
                    if (worker.AwayUntil > Now) { Reply(player, "Move.Away", worker.Name); return; }
                    BuildingPrivlidge cupboard;
                    if (!TryGetPost(player, out cupboard)) return;
                    if (PostHere(player, worker, cupboard)) Reply(player, "Move.Done");
                    break;
                }
                case "dismiss":
                {
                    ulong owner = player.userID;
                    string roleName = NameOf(worker, player.UserIDString);
                    Dismiss(owner, worker);
                    if (Wage > 0 && PaidUntil(owner) > Now) Reply(player, "Dismiss.Done", roleName, FormatSpan(PaidUntil(owner) - Now));
                    else Reply(player, "Dismiss.DoneUnpaid", roleName);
                    CuiHelper.DestroyUi(player, UiPanelName);
                    panelHand.Remove(player.userID);
                    return;
                }
                case "box":
                {
                    ulong id;
                    if (!ulong.TryParse(arg.GetString(2, ""), out id)) return;
                    var box = FindBox(id);
                    var cupboard = FindCupboard(hand.Cupboard);
                    string kind = BoxKind(box);
                    bool fits = kind == "water" ? key == "water" : kind == "compost" ? key == "compost" : kind == "box" && (key == "output" || key == "supply" || key == "fuel" || key == "takings");
                    if (box == null || cupboard == null || !fits || box.GetBuildingPrivilege() != cupboard || !WithinReach(hand, box.transform.position)) return;
                    AssignBox(hand, key, box, null);
                    break;
                }
                case "autosetup":
                    AutoSetup(hand, liveHand.Home);
                    break;
                case "duty":
                    if (key == "harvest") hand.Harvest = !hand.Harvest;
                    else if (key == "replant") hand.Replant = !hand.Replant;
                    else if (key == "water") hand.Watering = !hand.Watering;
                    else if (key == "fertilise") hand.Fertilise = !hand.Fertilise;
                    else if (key == "compost") hand.Composting = !hand.Composting;
                    else if (key == "chickens") hand.Chickens = !hand.Chickens;
                    else if (key == "honey") hand.Honey = !hand.Honey;
                    else if (key == "fetch") hand.FetchWater = !hand.FetchWater;
                    else if (key == "livestock") hand.Livestock = !hand.Livestock;
                    else if (key == "market") hand.MarketRuns = !hand.MarketRuns;
                    else if (key == "cull") { hand.CullLivestock = !hand.CullLivestock; if (hand.CullLivestock) hand.LastCull = Now; }
                    else if (key == "buyseeds") hand.BuySeeds = !hand.BuySeeds;
                    else if (key == "vendor") hand.VendorRuns = !hand.VendorRuns;
                    else if (key == "biofuel") hand.Biofuel = !hand.Biofuel;
                    else if (key == "breeding") hand.Breeding = !hand.Breeding;
                    else if (key == "buystock") hand.BuyLivestock = !hand.BuyLivestock;
                    else if (key == "advteas") hand.AdvancedTeas = !hand.AdvancedTeas;
                    break;
                case "keep":
                    hand.ChickensWanted = Mathf.Clamp(hand.ChickensWanted + arg.GetInt(1, 0), 0, 12);
                    break;
                case "dung":
                    hand.DungToGenerator = Mathf.Clamp(hand.DungToGenerator + arg.GetInt(1, 0), 0, 100);
                    break;
                case "bake":
                    hand.Bake = Mathf.Clamp(hand.Bake + arg.GetInt(1, 0), 0, 2);
                    break;
                case "loaves":
                    hand.LoavesToKeep = Mathf.Clamp(hand.LoavesToKeep + arg.GetInt(1, 0), 0, 100);
                    break;
                case "tick":
                {
                    int current = worker.JobSeconds > 0 ? worker.JobSeconds : Mathf.RoundToInt(Mathf.Max(3f, config.JobSeconds));
                    worker.JobSeconds = Mathf.Clamp(current + arg.GetInt(1, 0), 5, 60);
                    break;
                }
                case "prio":
                {
                    var order = OrderOf(worker);
                    int at = order.IndexOf(key);
                    string dir = arg.GetString(2, "");
                    if (at < 0) return;
                    if (dir == "up" && at > 0) { order.RemoveAt(at); order.Insert(at - 1, key); }
                    else if (dir == "down" && at < order.Count - 1) { order.RemoveAt(at); order.Insert(at + 1, key); }
                    break;
                }
                case "vendorhours":
                    hand.VendorHours = Mathf.Clamp(hand.VendorHours + arg.GetInt(1, 0), 1, 48);
                    break;
                case "buyperrun":
                    hand.BuyPerRun = Mathf.Clamp(hand.BuyPerRun + arg.GetInt(1, 0), 0, 4);
                    break;
                case "animalsperrun":
                    hand.AnimalsPerRun = Mathf.Clamp(hand.AnimalsPerRun + arg.GetInt(1, 0), 0, 10);
                    break;
                case "keepcows":
                    hand.KeepCows = Mathf.Clamp(hand.KeepCows + arg.GetInt(1, 0), 0, 50);
                    break;
                case "keepsheep":
                    hand.KeepSheep = Mathf.Clamp(hand.KeepSheep + arg.GetInt(1, 0), 0, 50);
                    break;
                case "keepproduce":
                    hand.KeepProduce = Mathf.Clamp(hand.KeepProduce + arg.GetInt(1, 0), 0, 1000);
                    break;
                case "keepof":
                    if (key.Length == 0 || ItemManager.FindItemDefinition(key) == null) return;
                    if (hand.KeepEach == null) hand.KeepEach = new Dictionary<string, int>();
                    hand.KeepEach[key] = Mathf.Clamp(KeepOf(hand, key) + delta, 0, 1000);
                    break;
                case "keepreset":
                    if (hand.KeepEach != null) hand.KeepEach.Remove(key);
                    if (hand.CapEach != null) hand.CapEach.Remove(key);
                    break;
                case "capof":
                    if (key.Length == 0 || ItemManager.FindItemDefinition(key) == null) return;
                    if (hand.CapEach == null) hand.CapEach = new Dictionary<string, int>();
                    hand.CapEach[key] = Mathf.Clamp(CapOf(hand, key) + delta, 0, 5000);
                    break;
                case "stockcap":
                    hand.StockCap = Mathf.Clamp(hand.StockCap + arg.GetInt(1, 0), 0, 5000);
                    break;
                case "markethours":
                    hand.MarketHours = Mathf.Clamp(hand.MarketHours + arg.GetInt(1, 0), 1, 48);
                    break;
                case "butcher":
                {
                    int was = hand.ButcherHours;
                    hand.ButcherHours = Mathf.Clamp(hand.ButcherHours + arg.GetInt(1, 0), 0, 168);
                    if (was == 0 && hand.ButcherHours > 0) hand.LastButcher = Now;   // the clock starts now, not in 1970
                    break;
                }
                case "plant":
                case "feed":
                case "seeds":
                case "min":
                case "max":
                {
                    Crop crop;
                    if (!cropByKey.TryGetValue(key, out crop)) return;
                    var setting = EditSetting(hand, crop);
                    if (action == "plant") setting.Plant = !PlantsCrop(hand, crop);
                    else if (action == "feed") setting.Feed = !FeedsCrop(hand, crop);
                    else if (action == "seeds") setting.KeepSeeds = Mathf.Clamp(KeepSeeds(hand, crop) + delta, 0, 500);
                    else if (action == "min")
                    {
                        setting.MinWater = Mathf.Clamp(MinWater(hand, crop) + delta, 0, MaxWater(hand, crop));
                        setting.MaxWater = MaxWater(hand, crop);
                    }
                    else
                    {
                        int max = Mathf.Clamp(MaxWater(hand, crop) + delta, MinWater(hand, crop), 100);
                        setting.MinWater = MinWater(hand, crop);
                        setting.MaxWater = max;
                    }
                    break;
                }
                default:
                    return;
            }
            ShowPanel(player);
        }

        #endregion

        #region Paying through PublicWorks

        private void RegisterBill()
        {
            if (OfficeOpen)
                PublicWorks.Call("RegisterBillable", this, BillKey);
        }

        // PublicWorks asks this each time it draws the accounts page for a player.
        // Only players with hands on the books get a row: hiring happens at the base.
        private Dictionary<string, object> PublicWorksBillQuery(BasePlayer player, string key)
        {
            if (key != BillKey || player == null || dataBroken || Wage <= 0) return null;

            ulong owner = player.userID;
            var state = StateOf(owner);
            int hands = HandsOf(owner);
            if (hands == 0 && state != WageState.Paid) return null;
            string userId = player.UserIDString;

            return new Dictionary<string, object>
            {
                ["Label"] = Msg("Bill.Label", userId),
                ["Description"] = Msg("Bill.Description", userId, Mathf.Max(1, hands), Wage),
                ["Status"] = state == WageState.Paid ? Msg("Bill.Left", userId, FormatSpan(PaidUntil(owner) - Now)) : Msg("Bill.Unpaid", userId),
                ["Price"] = DaysPurchasable(owner) > 0 ? DailyWage(owner) : 0,
                ["Button"] = Msg("Bill.Button", userId),
                ["Active"] = state == WageState.Paid
            };
        }

        // PublicWorks has already taken the scrap. Returning false makes it refund.
        private object PublicWorksBillPaid(BasePlayer player, string key, int scrap)
        {
            if (key != BillKey || player == null || dataBroken) return false;
            ulong owner = player.userID;
            var state = StateOf(owner);
            if (HandsOf(owner) == 0 && state != WageState.Paid) return false;
            if (scrap < DailyWage(owner) || DaysPurchasable(owner) < 1) return false;

            Reply(player, "Pay.Extended", FormatSpan(ExtendWage(owner, 1)));
            return true;
        }

        #endregion

        #region Commands

        [ChatCommand("farmhand")]
        private void CmdFarmHand(BasePlayer player, string command, string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLower() : "";
            string arg1 = args.Length > 1 ? args[1] : "";
            switch (sub)
            {
                case "":
                case "status":
                    if (!MayUse(player)) { Reply(player, "NoPermission"); return; }
                    if (dataBroken) return;
                    TellStatus(player);
                    return;
                case "hire":
                    CmdHire(player, arg1);
                    return;
                case "post":
                    CmdPost(player, arg1);
                    return;
                case "move":
                case "here":
                    CmdMove(player);
                    return;
                case "dismiss":
                case "fire":
                    CmdDismiss(player, arg1);
                    return;
                case "pay":
                    CmdPay(player, args);
                    return;
                case "menu":
                case "panel":
                case "ui":
                {
                    if (!MayUse(player)) { Reply(player, "NoPermission"); return; }
                    if (dataBroken) return;
                    var liveHand = NearestHand(player, 100f);
                    Worker pick = liveHand != null ? liveHand.Worker : null;
                    if (pick == null)
                    {
                        var farm = FarmHere(player);
                        var farms = farm != null ? new List<FarmData> { farm } : FarmsOf(player.userID);
                        foreach (var candidate in farms)
                            if (candidate.Hands.Count > 0) { pick = candidate.Hands[0]; break; }
                    }
                    if (pick == null) { Reply(player, "Panel.None"); return; }
                    panelHand[player.userID] = pick.Id;
                    ShowPanel(player);
                    return;
                }
                case "go":
                {
                    if (!MayUse(player)) { Reply(player, "NoPermission"); return; }
                    if (dataBroken) return;
                    var farms = FarmsOf(player.userID);
                    if (farms.Count == 0) { Reply(player, "Box.None"); return; }
                    foreach (var farm in farms)
                    {
                        farm.LastMarketRun = 0;
                        farm.LastVendorRun = 0;
                        foreach (var worker in farm.Hands)
                        {
                            LiveHand liveHand;
                            if (live.TryGetValue(worker.Id, out liveHand)) { liveHand.MarketCheckedAt = -1000f; liveHand.VendorCheckedAt = -1000f; }
                        }
                    }
                    Reply(player, "Go");
                    return;
                }
                case "output":
                case "supply":
                case "water":
                case "compost":
                case "fuel":
                case "takings":
                    CmdBox(player, sub);
                    return;
                case "boxes":
                {
                    if (dataBroken) return;
                    var hand = FarmHere(player);
                    if (hand == null) { Reply(player, "Box.None"); return; }
                    string set = Msg("Box.Set", player.UserIDString), unset = Msg("Box.Unset", player.UserIDString);
                    Reply(player, "Box.Status", OutputsOf(hand).Count > 0 ? OutputsOf(hand).Count.ToString() : unset, FindBox(hand.Supply) != null ? set : unset, FindWater(hand.Water) != null ? set : unset, FindBox(hand.Compost) is Composter ? set : unset, FindBox(hand.Fuel) != null ? set : unset);
                    ReplyRaw(player, "Box.Usage");
                    return;
                }
            }

            if (!permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                Reply(player, "NoPermission");
                return;
            }
            if (dataBroken) return;

            switch (sub)
            {
                case "list":
                {
                    int posted = PostedCount();
                    if (posted == 0) { ReplyRaw(player, "Admin.ListNone"); return; }
                    ReplyRaw(player, "Admin.ListHeader", posted, MaxHands);
                    foreach (var farm in data.Farms.Values)
                    {
                        if (farm.Hands.Count == 0) continue;
                        var owner = covalence.Players.FindPlayerById(farm.Owner.ToString());
                        string name = owner != null ? owner.Name : farm.Owner.ToString();
                        string roles = RolesOf(farm.Hands, player.UserIDString);
                        if (StateOf(farm.Owner) == WageState.Paid)
                            ReplyRaw(player, "Admin.ListPosted", name, GridOf(farm), roles, FormatSpan(Wage <= 0 ? DaySeconds : PaidUntil(farm.Owner) - Now));
                        else
                            ReplyRaw(player, "Admin.ListUnpaid", name, GridOf(farm), roles);
                    }
                    return;
                }
                case "grant":
                {
                    float days;
                    if (args.Length < 3 || !float.TryParse(args[2], out days) || days <= 0f) { ReplyRaw(player, "Admin.Usage"); return; }
                    var target = BasePlayer.Find(args[1]);
                    if (target == null) { ReplyRaw(player, "Admin.NoPlayer", args[1]); return; }
                    ReplyRaw(player, "Admin.Granted", target.displayName, FormatSpan(ExtendWage(target.userID, days)));
                    return;
                }
                case "remove":
                {
                    if (args.Length < 2) { ReplyRaw(player, "Admin.Usage"); return; }
                    var target = BasePlayer.Find(args[1]);
                    if (target == null) { ReplyRaw(player, "Admin.NoPlayer", args[1]); return; }
                    int removed = 0;
                    foreach (var farm in FarmsOf(target.userID))
                        foreach (var worker in new List<Worker>(farm.Hands)) { Unpost(farm, worker, null); removed++; }
                    if (removed == 0) { ReplyRaw(player, "Admin.NotPosted", target.displayName); return; }
                    Notify(target.userID, "Left.Admin");
                    ReplyRaw(player, "Admin.Removed", target.displayName);
                    return;
                }
                default:
                    ReplyRaw(player, "Admin.Usage");
                    return;
            }
        }

        // Hire a hand of a role. Standing in a cupboard of yours, it is posted there on
        // the spot; anywhere else it goes on the books until /farmhand post at a base.
        private void CmdHire(BasePlayer player, string roleText)
        {
            if (!MayUse(player)) { Reply(player, "NoPermission"); return; }
            if (dataBroken) return;
            ulong owner = player.userID;
            var account = AccountOf(owner, true);
            TellNote(player, account);

            string role = RoleOf(roleText);
            if (role == null) { Reply(player, "Hire.Roles", string.Join(", ", Roles)); return; }
            if (PostedCount() + account.Unposted.Count >= MaxHands) { Reply(player, "Hire.ServerFull", MaxHands); return; }

            // Standing somewhere a hand can be posted?
            BuildingPrivlidge cupboard = player.isMounted || player.IsSwimming() || !player.IsOnGround() ? null : player.GetBuildingPrivilege();
            if (cupboard != null && (cupboard.net == null || !cupboard.IsAuthed(player))) cupboard = null;
            var farm = cupboard != null ? FarmOf(cupboard.net.ID.Value) : null;
            if (farm != null && farm.Owner != owner) cupboard = null;
            if (farm != null && cupboard != null && farm.Hands.Count >= MaxPerFarm) { Reply(player, "Hire.FarmFull", MaxPerFarm); return; }

            // Wages still on the account carry over; otherwise the first day for the
            // whole crew, this hand included, is paid here on the spot.
            bool owes = Wage > 0 && Now >= account.PaidUntil;
            int due = Wage * (HandsOf(owner) + 1);
            var def = ItemManager.FindItemDefinition(ScrapShortname);
            if (owes && (def == null || player.inventory.GetAmount(def.itemid) < due))
            {
                Reply(player, "Hire.NeedScrap", due);
                return;
            }

            var worker = new Worker { Id = NewWorkerId(), Role = role, Name = DrawName(owner) };
            bool posted = false;
            if (cupboard != null)
            {
                posted = PostHere(player, worker, cupboard);
                if (!posted) return;
            }
            else
                account.Unposted.Add(worker);

            if (owes)
            {
                player.inventory.Take(null, def.itemid, due);
                account.PaidUntil = Now + DaySeconds;
            }
            string roleName = NameOf(worker, player.UserIDString);
            Puts($"{player.displayName} hired {worker.Name} the {worker.Role}{(posted ? " and posted them at " + MapHelper.PositionToString(player.transform.position) : " (on the books)")}.");
            if (posted) Reply(player, owes ? "Hire.Done" : "Hire.DonePaid", roleName, owes ? due : 0, FormatSpan(Wage <= 0 ? DaySeconds : account.PaidUntil - Now));
            else Reply(player, "Hire.Unposted", roleName);
        }

        // Post a hired hand where you stand.
        private void CmdPost(BasePlayer player, string roleText)
        {
            if (!MayUse(player)) { Reply(player, "NoPermission"); return; }
            if (dataBroken) return;
            var account = AccountOf(player.userID);
            string role = roleText.Length > 0 ? RoleOf(roleText) : null;
            if (roleText.Length > 0 && role == null) { Reply(player, "Hire.Roles", string.Join(", ", Roles)); return; }
            Worker worker = null;
            if (account != null)
                foreach (var candidate in account.Unposted)
                    if (role == null || candidate.Role == role) { worker = candidate; break; }
            if (worker == null) { Reply(player, "Post.None"); return; }
            if (PostedCount() >= MaxHands) { Reply(player, "Hire.ServerFull", MaxHands); return; }

            BuildingPrivlidge cupboard;
            if (!TryGetPost(player, out cupboard)) return;
            if (PostHere(player, worker, cupboard))
            {
                Puts($"{player.displayName} posted {worker.Name} the {worker.Role} at {MapHelper.PositionToString(player.transform.position)}.");
                Reply(player, "Post.Done", NameOf(worker, player.UserIDString));
            }
        }

        // Move the nearest of your hands to where you stand (any farm of yours).
        private void CmdMove(BasePlayer player)
        {
            if (!MayUse(player)) { Reply(player, "NoPermission"); return; }
            if (dataBroken) return;
            var liveHand = NearestHand(player, 50f);
            if (liveHand == null) { Reply(player, "Move.None"); return; }

            BuildingPrivlidge cupboard;
            if (!TryGetPost(player, out cupboard)) return;
            if (PostHere(player, liveHand.Worker, cupboard)) Reply(player, "Move.Done");
        }

        // Dismiss the nearest of your hands, or with no hand near, one on the books.
        private void CmdDismiss(BasePlayer player, string roleText)
        {
            if (dataBroken) return;
            ulong owner = player.userID;
            string role = roleText.Length > 0 ? RoleOf(roleText) : null;
            var liveHand = NearestHand(player, 50f);
            Worker worker = liveHand != null && (role == null || liveHand.Worker.Role == role) ? liveHand.Worker : null;
            if (worker == null)
            {
                var account = AccountOf(owner);
                if (account != null)
                    foreach (var candidate in account.Unposted)
                        if (role == null || candidate.Role == role) { worker = candidate; break; }
            }
            if (worker == null) { Reply(player, "Dismiss.None"); return; }

            string roleName = NameOf(worker, player.UserIDString);
            Dismiss(owner, worker);
            if (Wage > 0 && PaidUntil(owner) > Now) Reply(player, "Dismiss.Done", roleName, FormatSpan(PaidUntil(owner) - Now));
            else Reply(player, "Dismiss.DoneUnpaid", roleName);
        }

        // The standalone desk: scrap straight from the player's inventory.
        private void CmdPay(BasePlayer player, string[] args)
        {
            if (!MayUse(player)) { Reply(player, "NoPermission"); return; }
            if (dataBroken) return;
            if (OfficeOpen) { Reply(player, "Pay.AtOfficeOnly"); TellWhereToPay(player); return; }
            if (!config.PayByCommand) { Reply(player, "NoPermission"); return; }

            int days = 1;
            if (args.Length > 1 && (!int.TryParse(args[1], out days) || days < 1))
            {
                Reply(player, "Pay.Usage");
                return;
            }

            ulong owner = player.userID;
            if (HandsOf(owner) == 0) { Reply(player, "Pay.NoHand"); return; }
            int allowed = DaysPurchasable(owner);
            if (allowed < 1)
            {
                Reply(player, "Status.MaxPrepaid", Mathf.Max(1, config.MaxPrepaidDays));
                return;
            }
            if (days > allowed) days = allowed;

            int total = DailyWage(owner) * days;
            var def = ItemManager.FindItemDefinition(ScrapShortname);
            if (def == null) return;
            if (total > 0)
            {
                if (player.inventory.GetAmount(def.itemid) < total)
                {
                    Reply(player, "Pay.NeedScrap", total, days);
                    return;
                }
                player.inventory.Take(null, def.itemid, total);
            }
            Reply(player, "Pay.Done", total, FormatSpan(ExtendWage(owner, days)));
        }

        #endregion

        #region HelpMenu

        // object GetHelpInfo() — the HelpMenu plugin's entry (its hook schema).
        [HookMethod("GetHelpInfo")]
        private object GetHelpInfo()
        {
            var commands = new List<Dictionary<string, object>>
            {
                HelpCommand("/farmhand", "", Msg("Help.Cmd.Status", null), ""),
                HelpCommand("/farmhand", "hire [all | planter | stockman | yard | carter]", Msg("Help.Cmd.Hire", null), ""),
                HelpCommand("/farmhand", "post [role]", Msg("Help.Cmd.Post", null), ""),
                HelpCommand("/farmhand", "move", Msg("Help.Cmd.Move", null), ""),
                HelpCommand("/farmhand", "dismiss [role]", Msg("Help.Cmd.Dismiss", null), ""),
                HelpCommand("/farmhand", "output | supply | water | compost | fuel", Msg("Help.Cmd.Boxes", null), ""),
                HelpCommand("/farmhand", "pay [days]", Msg("Help.Cmd.Pay", null), ""),
                HelpCommand("/farmhand", "go", Msg("Help.Cmd.Go", null), ""),
                HelpCommand("/farmhand", "menu", Msg("Help.Cmd.Menu", null), ""),
                HelpCommand("/farmhand", "list | grant <player> <days> | remove <player>", Msg("Help.Cmd.Admin", null), PermAdmin),
            };
            return new Dictionary<string, object>
            {
                ["Plugin"] = Name,
                ["Title"] = Msg("Help.Title", null),
                ["Description"] = Msg("Help.Description", null),
                ["Commands"] = commands,
                ["HowTo"] = Msg("Help.HowTo", null).Split('\n'),
                ["Notes"] = Msg("Help.Notes", null).Split('\n'),
                ["Order"] = 34,   // sidebar: after Public Works and Public Lockers
            };
        }

        private static Dictionary<string, object> HelpCommand(string command, string args, string description, string perm) =>
            new Dictionary<string, object>
            {
                ["Command"] = command,
                ["Args"] = args,
                ["Description"] = description,
                ["Permission"] = perm,
                ["AuthLevel"] = 0,
            };

        #endregion
    }
}
