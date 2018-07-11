using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace MoreFactionInteraction
{
    public class IncidentWorker_ReverseTradeRequest : IncidentWorker
    {
        private const int TimeoutTicks = GenDate.TicksPerDay;
        private static List<Map> tmpAvailableMaps = new List<Map>();

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return TryGetRandomAvailableTargetMap(map: out Map map) && IncidentWorker_QuestTradeRequest.RandomNearbyTradeableSettlement(originTile: map.Tile) != null && base.CanFireNowSub(parms: parms);

        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!TryGetRandomAvailableTargetMap(map: out Map map)) return false;
            SettlementBase settlement = IncidentWorker_QuestTradeRequest.RandomNearbyTradeableSettlement(originTile: map.Tile);
            if (settlement != null)
            {
                //TODO: look into making the below dynamic based on requester's biome, faction, pirate outpost vicinity and other stuff.
                ThingCategoryDef thingCategoryDef = DetermineThingCategoryDef();

                string letterToSend = DetermineLetterToSend(thingCategoryDef: thingCategoryDef);
                int feeRequest = Math.Max(val1: Rand.Range(min: 150, max: 300), val2: (int)parms.points);
                string categorylabel = (thingCategoryDef == ThingCategoryDefOf.PlantFoodRaw) ? thingCategoryDef.label + " items" : thingCategoryDef.label;
                ChoiceLetter_ReverseTradeRequest choiceLetter_ReverseTradingRequest = (ChoiceLetter_ReverseTradeRequest)LetterMaker.MakeLetter(label: this.def.letterLabel, text: letterToSend.Translate(args: new object[]
                {
                    settlement.Faction.leader.LabelShort,
                    settlement.Faction.def.leaderTitle,
                    settlement.Faction.Name,
                    settlement.Label,
                    categorylabel,
                    feeRequest,
                }).AdjustedFor(p: settlement.Faction.leader), def: this.def.letterDef);
                choiceLetter_ReverseTradingRequest.title = "MFI_ReverseTradeRequestTitle".Translate(args: new object[]
                {
                    map.info.parent.Label
                }).CapitalizeFirst();

                choiceLetter_ReverseTradingRequest.thingCategoryDef = thingCategoryDef;
                choiceLetter_ReverseTradingRequest.map = map;
                parms.target = map;
                choiceLetter_ReverseTradingRequest.incidentParms = parms;
                choiceLetter_ReverseTradingRequest.faction = settlement.Faction;
                choiceLetter_ReverseTradingRequest.fee = feeRequest;
                choiceLetter_ReverseTradingRequest.StartTimeout(duration: TimeoutTicks);
                choiceLetter_ReverseTradingRequest.tile = settlement.Tile;
                Find.LetterStack.ReceiveLetter(@let: choiceLetter_ReverseTradingRequest);
                return true;
            }
            return false;
        }

        private static ThingCategoryDef DetermineThingCategoryDef()
        {
            ThingCategoryDef thingCategoryDef;

            int rand = Rand.RangeInclusive(min: 0, max: 100);
            if (rand < 33) thingCategoryDef = ThingCategoryDefOf.Apparel;
            else if (rand > 33 && rand < 66) thingCategoryDef = ThingCategoryDefOf.PlantFoodRaw;
            else if (rand > 66 && rand < 90) thingCategoryDef = ThingCategoryDefOf.Weapons;
            else thingCategoryDef = ThingCategoryDefOf.Medicine;
            return thingCategoryDef;
        }

        private static string DetermineLetterToSend(ThingCategoryDef thingCategoryDef)
        {

            if (thingCategoryDef == ThingCategoryDefOf.PlantFoodRaw) return "MFI_ReverseTradeRequest_Blight";

            switch (Rand.RangeInclusive(min: 0, max: 4))
            {
                case 0:
                    return "MFI_ReverseTradeRequest_Pyro";                
                case 1:
                    return "MFI_ReverseTradeRequest_Mechs";
                case 2:
                    return "MFI_ReverseTradeRequest_Caravan";
                case 3:
                    return "MFI_ReverseTradeRequest_Pirates";
                case 4:
                    return "MFI_ReverseTradeRequest_Hardship";

                default:
                    return "MFI_ReverseTradeRequest_Pyro";
            }
        }

        private static bool TryGetRandomAvailableTargetMap(out Map map)
        {
            tmpAvailableMaps.Clear();
            List<Map> maps = Find.Maps;
            foreach (Map potentialTargetMap in maps)
            {
                if (potentialTargetMap.IsPlayerHome && IncidentWorker_QuestTradeRequest.RandomNearbyTradeableSettlement(originTile: potentialTargetMap.Tile) != null)
                {
                    tmpAvailableMaps.Add(item: potentialTargetMap);
                }
            }
            bool result = tmpAvailableMaps.TryRandomElement(result: out map);
            tmpAvailableMaps.Clear();
            return result;
        }
    }
}