﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Fluffy
{
    [StaticConstructorOnStartup]
    public static class Utility_Medical
    {
        public static Texture2D[] CareTextures =
        {
            ContentFinder<Texture2D>.Get( "UI/Icons/Medical/NoCare" ),
            ContentFinder<Texture2D>.Get( "UI/Icons/Medical/NoMeds" ),
            ThingDefOf.HerbalMedicine.uiIcon,
            ThingDefOf.Medicine.uiIcon,
            ThingDefOf.GlitterworldMedicine.uiIcon
        };

        public static Texture2D BloodTexture = ContentFinder<Texture2D>.Get( "UI/Icons/Medical/Bleeding" ),
                                BloodTextureWhite = ContentFinder<Texture2D>.Get( "UI/Buttons/blood" ),
                                OpTexture = ContentFinder<Texture2D>.Get( "UI/Buttons/medical" );

        public static void MedicalCareSetter( Rect rect, ref MedicalCareCategory medCare )
        {
            var iconSize = rect.width / 5f;
            var iconHeightOffset = ( rect.height - iconSize ) / 2;
            var rect2 = new Rect( rect.x, rect.y + iconHeightOffset, iconSize, iconSize );
            for ( var i = 0; i < 5; i++ )
            {
                var mc = (MedicalCareCategory) i;
                Widgets.DrawHighlightIfMouseover( rect2 );
                GUI.DrawTexture( rect2, CareTextures[i] );
                if ( Widgets.ButtonInvisible( rect2 ) )
                {
                    medCare = mc;
                    SoundDefOf.TickHigh.PlayOneShotOnCamera( );
                }
                if ( medCare == mc )
                {
                    GUI.DrawTexture( rect2, Widgets.CheckboxOnTex );
                }
                TooltipHandler.TipRegion( rect2, ( ) => mc.GetLabel( ), 632165 + i * 17 );
                rect2.x += rect2.width;
            }
        }

        public static void DoHediffTooltip( Rect rect, Pawn p, string effLabel, PawnCapacityDef capDef )
        {
            var tooltip = new StringBuilder( );
            tooltip.AppendLine( effLabel );
            try
            {
                // get parts that matter for this capDef
                var activityGroups = p.RaceProps.body.GetActivityGroups( capDef );
                var relevantParts = new List<BodyPartRecord>( );
                foreach ( var t in activityGroups ) {
                    relevantParts.AddRange( p.RaceProps.body.GetParts( capDef, t ) );
                }
                relevantParts = relevantParts.Distinct().ToList();

                // the following is an incredible hacky way to show all diffs, but not child nodes of missing body parts
                // if you care about good code, look away.
                // remove missing parts
                relevantParts.RemoveAll(
                    bp => p.health.hediffSet.GetHediffs<Hediff_MissingPart>( ).Select( h => h.Part ).Contains( bp ) );

                // add common ancestors back in
                relevantParts.AddRange( p.health.hediffSet.GetMissingPartsCommonAncestors( ).Select( h => h.Part ) );

                // hediffs with a direct effect listed (CapMods), or affecting a relevant part.
                var hediffs = p.health.hediffSet.GetHediffs<Hediff>( ).Where( h => h.Visible &&
                                                                                   ( ( h.CapMods != null &&
                                                                                       h.CapMods.Count > 0 &&
                                                                                       h.CapMods.Any(
                                                                                           cm => cm.capacity == capDef ) ) ||
                                                                                     relevantParts.Contains( h.Part ) ) );
                foreach ( var diff in hediffs )
                {
                    tooltip.AppendLine( ( diff.Part == null ? "Whole body" : diff.Part.def.LabelCap ) + ": " +
                                        diff.LabelCap );
                }
            }
            catch ( Exception )
            {
                Log.Message( "Error getting tooltip for medical info." );
            }

            TooltipHandler.TipRegion( rect, tooltip.ToString( ) );
        }

        public static void DoHediffTooltip(Rect rect, Pawn p, float bleedRate, float healthPercent)
        {
            var tooltip = new StringBuilder();
            tooltip.AppendLine("BleedingRate".Translate() + ": " + bleedRate.ToStringPercent() + "/" + "LetterDay".Translate());
            if (!Mathf.Approximately(bleedRate, 0f))
            {
                var ticksBloodLoss = HealthUtility.TicksUntilDeathDueToBloodLoss(p);
                tooltip.AppendLine(ticksBloodLoss < 60000 ? " (" + "TimeToDeath".Translate(ticksBloodLoss.ToStringTicksToPeriod(true)) + ")" : " (" + "WontBleedOutSoon".Translate() + ")");
            }
            tooltip.AppendLine();
            tooltip.AppendLine("FluffyMedical.HealthPoints".Translate() + ": " + healthPercent.ToStringPercent());
            if (Mathf.Approximately(healthPercent, 1f))
            {
                TooltipHandler.TipRegion(rect, tooltip.ToString());
                return;
            }

            try
            {
                var bodyPartRecords = p.RaceProps.body.AllParts;
                foreach (var bodyPartRecord in bodyPartRecords)
                {
                    var healthPoint = p.health.hediffSet.GetPartHealth(bodyPartRecord);
                    var hitPoint = bodyPartRecord.def.GetMaxHealth(p);

                    if (Mathf.Approximately(healthPoint, hitPoint))
                            continue;

                    tooltip.AppendLine(bodyPartRecord.def.LabelCap + ": " + healthPoint.ToString() + " / " + bodyPartRecord.def.GetMaxHealth(p).ToString());
                }
            }
            catch (Exception)
            {
                Log.Message("Error getting tooltip for medical info.");
            }
            TooltipHandler.TipRegion(rect, tooltip.ToString());
        }

        public static void MedicalCareSetterAll( List<Pawn> pawns )
        {
            var list = new List<FloatMenuOption>( );
            for ( var i = 0; i < 5; i++ )
            {
                var mc = (MedicalCareCategory) i;
                var option = new FloatMenuOption( mc.GetLabel( ), delegate
                {
                    foreach ( var t in pawns ) {
                        t.playerSettings.medCare = mc;
                    }
                    SoundDefOf.TickHigh.PlayOneShotOnCamera( );
                    MainTabWindow_Medical.IsDirty = true;
                } );
                list.Add( option );
            }
            Find.WindowStack.Add( new FloatMenu( list ) );
        }

        public static void RecipeOptionsMaker( Pawn pawn )
        {
            if (pawn.RaceProps.Animal)
            {
                // TODO: See if we can auto-detect ADS, and/or auto-detect available bills on animals.
                Log.Warning( "Medical bills are currently not supported on animals. Stay tuned!");
                return;
            }

            Thing thingForMedBills = pawn;
            var list = new List<FloatMenuOption>( );
            foreach ( var current in thingForMedBills.def.AllRecipes )
            {
                if (!current.AvailableNow) continue;
                IEnumerable<ThingDef> enumerable = current.PotentiallyMissingIngredients( null, thingForMedBills.MapHeld );
                IEnumerable<ThingDef> thingDefs = enumerable as ThingDef[] ?? enumerable.ToArray();
                if (thingDefs.Any(x => x.isBodyPartOrImplant)) continue;
                {
                    IEnumerable<BodyPartRecord> partsToApplyOn = current.Worker.GetPartsToApplyOn( pawn, current );
                    IEnumerable<BodyPartRecord> bodyPartRecords = partsToApplyOn as BodyPartRecord[] ?? partsToApplyOn.ToArray();
                    if (!bodyPartRecords.Any()) continue;
                    foreach ( var current2 in bodyPartRecords )
                    {
                        var localRecipe = current;
                        var localPart = current2;
                        // Could not found RemoveBodyPartSpecialLabel() for A16.
                        //var text = localRecipe == RecipeDefOf.RemoveBodyPart ? HealthCardUtility.RemoveBodyPartSpecialLabel( pawn, current2 ) : localRecipe.LabelCap;
                        var text = localRecipe.LabelCap;
                        if ( !current.hideBodyPartNames )
                        {
                            text = text + " (" + current2.def.label + ")";
                        }
                        Action action = null;
                        if ( thingDefs.Any( ) )
                        {
                            text += " (";
                            var flag = true;
                            foreach ( var current3 in thingDefs )
                            {
                                if ( !flag )
                                {
                                    text += ", ";
                                }
                                flag = false;
                                text += "MissingMedicalBillIngredient".Translate( current3.label );
                            }
                            text += ")";
                        }
                        else
                        {
                            action = delegate
                            {
                                if (
                                    !Find.VisibleMap.mapPawns.FreeColonists.Any( col => localRecipe.PawnSatisfiesSkillRequirements( col ) ) )
                                {
                                    Bill.CreateNoPawnsWithSkillDialog( localRecipe );
                                }
                                var pawn2 = thingForMedBills as Pawn;
                                if ( pawn2 != null && !pawn.InBed( ) && pawn.RaceProps.Humanlike )
                                {
                                    if (
                                        !Find.VisibleMap.listerBuildings.allBuildingsColonist.Any( x => x is Building_Bed && ( (Building_Bed) x ).Medical ) )
                                    {
                                        Messages.Message( "MessageNoMedicalBeds".Translate( ),
                                            MessageSound.Negative );
                                    }
                                }
                                var billMedical = new Bill_Medical( localRecipe );
                                if (pawn2 == null) return;
                                pawn2.BillStack.AddBill( billMedical );
                                billMedical.Part = localPart;
                                if ( pawn2.Faction != null && !pawn2.Faction.def.hidden &&
                                     !pawn2.Faction.HostileTo( Faction.OfPlayer ) &&
                                     localRecipe.Worker.IsViolationOnPawn( pawn2, localPart, Faction.OfPlayer ) )
                                {
                                    Messages.Message(
                                        "MessageMedicalOperationWillAngerFaction".Translate( pawn2.Faction ),
                                        MessageSound.Negative );
                                }
                            };
                        }
                        list.Add( new FloatMenuOption( text, action ) );
                    }
                }
            }
            Find.WindowStack.Add( new FloatMenu( list ) );
        }
    }
}
