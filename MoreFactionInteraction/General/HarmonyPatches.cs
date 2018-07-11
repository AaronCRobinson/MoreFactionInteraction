﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Harmony;
using UnityEngine;
using RimWorld.Planet;

namespace MoreFactionInteraction
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            HarmonyInstance harmony = HarmonyInstance.Create(id: "Mehni.RimWorld.MFI.main");

            #region MoreTraders
            harmony.Patch(original: AccessTools.Method(type: typeof(TraderKindDef), name: nameof(TraderKindDef.PriceTypeFor)), prefix: null,
                postfix: new HarmonyMethod(type: typeof(HarmonyPatches), name: nameof(PriceTypeSetter_PostFix)), transpiler: null);

            harmony.Patch(original: AccessTools.Method(type: typeof(StoryState), name: nameof(StoryState.Notify_IncidentFired)), prefix: null,
                postfix: new HarmonyMethod(type: typeof(HarmonyPatches), name: nameof(IncidentFired_TradeCounter_Postfix)), transpiler: null);

            harmony.Patch(original: AccessTools.Method(type: typeof(CompQuality), name: nameof(CompQuality.PostPostGeneratedForTrader)),
                prefix: new HarmonyMethod(type: typeof(HarmonyPatches), name: nameof(CompQuality_TradeQualityIncreasePreFix)), postfix: null);

            harmony.Patch(original: AccessTools.Method(type: typeof(ThingSetMaker), name: nameof(ThingSetMaker.Generate), parameters: new Type[] { typeof(ThingSetMakerParams) }), prefix: null,
                postfix: new HarmonyMethod(type: typeof(HarmonyPatches), name: nameof(TraderStocker_OverStockerPostFix)), transpiler: null);
            #endregion

            #region WorldIncidents
            harmony.Patch(original: AccessTools.Method(type: typeof(SettlementBase), name: nameof(SettlementBase.GetCaravanGizmos)), prefix: null,
                postfix: new HarmonyMethod(type: typeof(HarmonyPatches), name: nameof(SettlementBase_CaravanGizmos_Postfix)), transpiler: null);

            harmony.Patch(original: AccessTools.Method(type: typeof(WorldReachabilityUtility), name: nameof(WorldReachabilityUtility.CanReach)), prefix: null,
                postfix: new HarmonyMethod(type: typeof(HarmonyPatches), name: nameof(WorldReachUtility_PostFix)), transpiler: null);
            #endregion
        }

        #region MoreTraders
        private static void TraderStocker_OverStockerPostFix(ref List<Thing> __result, ref ThingSetMakerParams parms)
        {
            if (parms.traderDef != null)
            {
                Map map = null;

                //much elegant. Such wow ;-;
                if (parms.tile.HasValue && parms.tile != -1 && Current.Game.FindMap(tile: parms.tile.Value) != null && Current.Game.FindMap(tile: parms.tile.Value).IsPlayerHome)
                    map = Current.Game.FindMap(tile: parms.tile.Value);

                else if (Find.AnyPlayerHomeMap != null)
                    map = Find.AnyPlayerHomeMap; 

                else if (Find.CurrentMap != null)
                    map = Find.CurrentMap;


                if (parms.traderDef.orbital || parms.traderDef.defName.Contains(value: "Base_") && map != null)
                {
                    float silverCount = __result.First(predicate: x => x.def == ThingDefOf.Silver).stackCount;
                    silverCount *= WealthSilverIncreaseDeterminationCurve.Evaluate(x: map.PlayerWealthForStoryteller);
                    __result.First(predicate: x => x.def == ThingDefOf.Silver).stackCount = (int)silverCount;
                    return;
                }
                if (map != null && parms.traderFaction != null)
                {
                    __result.First(predicate: x => x.def == ThingDefOf.Silver).stackCount += (int)(parms.traderFaction.GoodwillWith(other: Faction.OfPlayer) * (map.GetComponent<MapComponent_GoodWillTrader>().TimesTraded[key: parms.traderFaction] * MoreFactionInteraction_Settings.traderWealthOffsetFromTimesTraded));
                    return;
                }
            }
        }

        private static readonly SimpleCurve WealthSilverIncreaseDeterminationCurve = new SimpleCurve
        {
            {
                new CurvePoint(x: 0, y: 0.8f),
                true
            },
            {
                new CurvePoint(x: 10000, y: 1),
                true
            },
            {
                new CurvePoint(x: 75000, y: 2),
                true
            },
            {
                new CurvePoint(x: 300000, y: 4),
                true
            },
            {
                new CurvePoint(x: 1000000, y: 6f),
                true
            },
            {
                new CurvePoint(x: 2000000, y: 7f),
                true
            },
        };

        #region TradeQualityImprovements
        private static bool CompQuality_TradeQualityIncreasePreFix(CompQuality __instance, ref TraderKindDef trader, ref int forTile, ref Faction forFaction)
        {
            //forTile is assigned in RimWorld.ThingSetMaker_TraderStock.Generate. It's either a best-effort map, or -1.
            Map map = null;
            if (forTile != -1) map = Current.Game.FindMap(tile: forTile);
            __instance.SetQuality(q: FactionAndGoodWillDependantQuality(faction: forFaction, map: map, trader: trader), source: ArtGenerationContext.Outsider);
            return false;
        }

        /// <summary>
        /// Change quality carried by traders depending on Faction/Goodwill/Wealth.
        /// </summary>
        /// <returns>QualityCategory depending on wealth or goodwill. Fallsback to vanilla when fails.</returns>
        private static QualityCategory FactionAndGoodWillDependantQuality(Faction faction, Map map, TraderKindDef trader)
        {
            if (map != null && faction != null)
            {
                float qualityIncreaseFromTimesTradedWithFaction = Mathf.Clamp01(value: map.GetComponent<MapComponent_GoodWillTrader>().TimesTraded[key: faction] / 100);
                float qualityIncreaseFactorFromPlayerGoodWill = Mathf.Clamp01(value: faction.GoodwillWith(other: Faction.OfPlayer) / 100);

                if (Rand.Value < 0.25f)
                {
                    return QualityCategory.Normal;
                }
                float num = Rand.Gaussian(centerX: 2.5f + qualityIncreaseFactorFromPlayerGoodWill, widthFactor: 0.84f + qualityIncreaseFromTimesTradedWithFaction);
                num = Mathf.Clamp(value: num, min: 0f, max: QualityUtility.AllQualityCategories.Count - 0.5f);
                return (QualityCategory)((int)num);
            }
            if ((trader.orbital || trader.defName.Contains(value: "_Base")) && map != null)
            {
                if (Rand.Value < 0.25f)
                {
                    return QualityCategory.Normal;
                }
                float num = Rand.Gaussian(centerX: WealthQualityDeterminationCurve.Evaluate(x: map.wealthWatcher.WealthTotal), widthFactor: WealthQualitySpreadDeterminationCurve.Evaluate(x: map.wealthWatcher.WealthTotal));
                num = Mathf.Clamp(value: num, min: 0f, max: QualityUtility.AllQualityCategories.Count - 0.5f);
                return (QualityCategory)((int)num);
            }
            return QualityUtility.GenerateQualityTraderItem();
        }

        #region SimpleCurves
        private static readonly SimpleCurve WealthQualityDeterminationCurve = new SimpleCurve
        {
            {
                new CurvePoint(x: 0, y: 1),
                true
            },
            {
                new CurvePoint(x: 10000, y: 1.5f),
                true
            },
            {
                new CurvePoint(x: 75000, y: 2.5f),
                true
            },
            {
                new CurvePoint(x: 300000, y: 3),
                true
            },
            {
                new CurvePoint(x: 1000000, y: 3.8f),
                true
            },
            {
                new CurvePoint(x: 2000000, y: 4.3f),
                true
            },
        };

        private static readonly SimpleCurve WealthQualitySpreadDeterminationCurve = new SimpleCurve
        {
            {
                new CurvePoint(x: 0, y: 4.2f),
                true
            },
            {
                new CurvePoint(x: 10000, y: 4), //5.5
                true
            },
            {
                new CurvePoint(x: 75000, y: 2.5f), //5
                true
            },
            {
                new CurvePoint(x: 300000, y: 2.1f), //5.1
                true
            },
            {
                new CurvePoint(x: 1000000, y: 1.5f), //5.3
                true
            },
            {
                new CurvePoint(x: 2000000, y: 1.2f), //5.5
                true
            },
        };
        #endregion SimpleCurves
        #endregion TradeQualityImprovements

        /// <summary>
        /// Increment TimesTraded count of dictionary by one for this faction.
        /// </summary>
        private static void IncidentFired_TradeCounter_Postfix(ref FiringIncident qi)
        {
            if (qi.parms.target is Map map && qi.def == IncidentDefOf.TraderCaravanArrival)
            {
                map.GetComponent<MapComponent_GoodWillTrader>().TimesTraded[key: qi.parms.faction] += 1;
            }
        }

        private static void PriceTypeSetter_PostFix(ref TraderKindDef __instance, ref PriceType __result, TradeAction action)
        {
            //PriceTypeSetter is more finicky than I'd like, part of the reason traders arrive without any sellable inventory.
            // had issues with pricetype undefined, pricetype normal and *all* traders having pricetype expensive for *all* goods. This works.
            PriceType priceType = __result;
            if (priceType == PriceType.Undefined)
            {
                return;
            }
            //if (__instance.stockGenerators[i] is StockGenerator_BuyCategory && action == TradeAction.PlayerSells)
            if (__instance.stockGenerators.Any(predicate: x => x is StockGenerator_BuyCategory) && action == TradeAction.PlayerSells)
            {
                __result = PriceType.Expensive;
            }
            else __result = priceType;
        }
        #endregion

        #region WorldIncidents
        private static void SettlementBase_CaravanGizmos_Postfix(Settlement __instance, ref Caravan caravan, ref IEnumerable<Gizmo> __result)
        {
            if (__instance.GetComponent<World_Incidents.WorldObjectComp_SettlementBumperCropComp>()?.ActiveRequest ?? false)
            {
                Texture2D setPlantToGrowTex = ContentFinder<Texture2D>.Get(itemPath: "UI/Commands/SetPlantToGrow", reportFailure: true);
                Caravan localCaravan = caravan;

                Command_Action command_Action = new Command_Action
                {
                    defaultLabel = "MFI_CommandHelpOutHarvesting".Translate(),
                    defaultDesc = "MFI_CommandHelpOutHarvesting".Translate(),
                    icon = setPlantToGrowTex,
                    action = delegate
                    {
                        World_Incidents.WorldObjectComp_SettlementBumperCropComp bumperCrop2 = __instance.GetComponent<World_Incidents.WorldObjectComp_SettlementBumperCropComp>();
                        if (bumperCrop2 != null)
                        {
                            if (!bumperCrop2.ActiveRequest)
                            {
                                Log.Error(text: "Attempted to fulfill an unavailable request");
                                return;
                            }
                            if (BestCaravanPawnUtility.FindPawnWithBestStat(caravan: localCaravan, stat: StatDefOf.PlantHarvestYield) == null)
                            {
                                Messages.Message(text: "MFI_MessageBumperCropNoGrower".Translate(), lookTargets: localCaravan, def: MessageTypeDefOf.NegativeEvent);
                                return;
                            }
                            Find.WindowStack.Add(window: Dialog_MessageBox.CreateConfirmation(text: "MFI_CommandFulfillBumperCropHarvestConfirm".Translate(args: new object[] {localCaravan.LabelCap}),
                            confirmedAct: delegate
                            {
                                bumperCrop2.NotifyCaravanArrived(caravan: localCaravan);
                            }, destructive: false, title: null));
                        }
                    }
                };

                World_Incidents.WorldObjectComp_SettlementBumperCropComp bumperCrop = __instance.GetComponent<World_Incidents.WorldObjectComp_SettlementBumperCropComp>();
                if (BestCaravanPawnUtility.FindPawnWithBestStat(caravan: localCaravan, stat: StatDefOf.PlantHarvestYield) == null)
                {
                    command_Action.Disable(reason: "MFI_MessageBumperCropNoGrower".Translate());
                }
                __result = __result.Add(item: command_Action);
            }
        }

        private static void WorldReachUtility_PostFix(ref bool __result, ref Caravan c)
        {
            SettlementBase settlement = CaravanVisitUtility.SettlementVisitedNow(caravan: c);
            World_Incidents.WorldObjectComp_SettlementBumperCropComp bumperCropComponent = settlement?.GetComponent<World_Incidents.WorldObjectComp_SettlementBumperCropComp>();

            if (bumperCropComponent != null)
            {
                __result = !bumperCropComponent.CaravanIsWorking;
            }
        }
        #endregion


    }
}
