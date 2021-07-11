﻿using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Vanya_Test
{
    // Token: 0x02000003 RID: 3
    [StaticConstructorOnStartup]
    public class Vanya_ShieldBelt : Apparel
    {
        // Token: 0x04000004 RID: 4
        private const float MinDrawSize = 1.2f;

        // Token: 0x04000005 RID: 5
        private const float MaxDrawSize = 1.4f;

        // Token: 0x04000006 RID: 6
        private const float MaxDamagedJitterDist = 0.05f;

        // Token: 0x04000007 RID: 7
        private const int JitterDurationTicks = 8;

        // Token: 0x04000012 RID: 18
        private static readonly Material BubbleMat =
            MaterialPool.MatFrom("Vanya_Shield/Vanya_Shield", ShaderDatabase.Transparent);

        // Token: 0x04000011 RID: 17
        private readonly float ApparelScorePerEnergyMax = 0.25f;

        // Token: 0x0400000F RID: 15
        private readonly float EnergyLossPerDamage = 0.025f;

        // Token: 0x0400000E RID: 14
        private readonly float EnergyOnReset = 0.2f;

        // Token: 0x04000010 RID: 16
        private readonly int KeepDisplayingTicks = 1000;

        // Token: 0x0400000D RID: 13
        private readonly int StartingTicksToReset = 3000;

        // Token: 0x04000008 RID: 8
        private float energy;

        // Token: 0x0400000B RID: 11
        private Vector3 impactAngleVect;

        // Token: 0x0400000C RID: 12
        private int lastAbsorbDamageTick = -9999;

        // Token: 0x0400000A RID: 10
        private int lastKeepDisplayTick = -9999;

        // Token: 0x04000009 RID: 9
        private int ticksToReset = -1;

        // Token: 0x06000015 RID: 21 RVA: 0x000027DC File Offset: 0x000009DC
        // Note: this type is marked as 'beforefieldinit'.
        static Vanya_ShieldBelt()
        {
        }

        // Token: 0x06000014 RID: 20 RVA: 0x00002774 File Offset: 0x00000974

        // Token: 0x17000002 RID: 2
        // (get) Token: 0x06000005 RID: 5 RVA: 0x00002114 File Offset: 0x00000314
        private float EnergyMax => this.GetStatValue(StatDefOf.EnergyShieldEnergyMax);

        // Token: 0x17000003 RID: 3
        // (get) Token: 0x06000006 RID: 6 RVA: 0x00002134 File Offset: 0x00000334
        private float EnergyGainPerTick => this.GetStatValue(StatDefOf.EnergyShieldRechargeRate) / 60f;

        // Token: 0x17000004 RID: 4
        // (get) Token: 0x06000007 RID: 7 RVA: 0x00002158 File Offset: 0x00000358
        public float Energy => energy;

        // Token: 0x17000005 RID: 5
        // (get) Token: 0x06000008 RID: 8 RVA: 0x00002170 File Offset: 0x00000370
        public ShieldState ShieldState
        {
            get
            {
                var result = ticksToReset > 0 ? ShieldState.Resetting : ShieldState.Active;

                return result;
            }
        }

        // Token: 0x17000006 RID: 6
        // (get) Token: 0x06000009 RID: 9 RVA: 0x00002198 File Offset: 0x00000398
        private bool ShouldDisplay
        {
            get
            {
                var wearer = Wearer;
                return !wearer.Dead && !wearer.Downed &&
                       (!wearer.IsPrisonerOfColony || wearer.MentalStateDef != null && wearer.MentalStateDef.IsAggro) &&
                       (wearer.Drafted || wearer.Faction.HostileTo(Faction.OfPlayer) ||
                        Find.TickManager.TicksGame < lastKeepDisplayTick + KeepDisplayingTicks);
            }
        }

        // Token: 0x0600000A RID: 10 RVA: 0x00002217 File Offset: 0x00000417
        public override IEnumerable<Gizmo> GetWornGizmos()
        {
            foreach (var gizmo in base.GetWornGizmos())
            {
                yield return gizmo;
            }

            yield return new Gizmo_Vanya_EnergyShieldStatus
            {
                shield = this
            };
        }

        // Token: 0x0600000B RID: 11 RVA: 0x00002228 File Offset: 0x00000428
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref energy, "energy");
            Scribe_Values.Look(ref ticksToReset, "ticksToReset", -1);
            Scribe_Values.Look(ref lastKeepDisplayTick, "lastKeepDisplayTick");
        }

        // Token: 0x0600000C RID: 12 RVA: 0x0000227C File Offset: 0x0000047C
        public override float GetSpecialApparelScoreOffset()
        {
            return EnergyMax * ApparelScorePerEnergyMax;
        }

        // Token: 0x0600000D RID: 13 RVA: 0x0000229C File Offset: 0x0000049C
        public override void Tick()
        {
            base.Tick();
            if (Wearer == null)
            {
                energy = 0f;
            }
            else
            {
                if (ShieldState == ShieldState.Resetting)
                {
                    ticksToReset--;
                    if (ticksToReset <= 0)
                    {
                        Reset();
                    }
                }
                else
                {
                    if (ShieldState != ShieldState.Active)
                    {
                        return;
                    }

                    energy += EnergyGainPerTick;
                    if (energy > EnergyMax)
                    {
                        energy = EnergyMax;
                    }
                }
            }
        }

        // Token: 0x0600000E RID: 14 RVA: 0x00002348 File Offset: 0x00000548
        public override bool CheckPreAbsorbDamage(DamageInfo dinfo)
        {
            bool result;
            if (ShieldState == ShieldState.Active && (dinfo.Instigator != null || dinfo.Def.isExplosive))
            {
                if (dinfo.Instigator != null)
                {
                    AttachableThing attachableThing;
                    if ((attachableThing = dinfo.Instigator as AttachableThing) != null &&
                        attachableThing.parent == Wearer)
                    {
                        return false;
                    }
                }

                energy -= dinfo.Amount * EnergyLossPerDamage;
                if (energy < 0f)
                {
                    Break();
                }
                else
                {
                    AbsorbedDamage(dinfo);
                }

                result = true;
            }
            else
            {
                result = false;
            }

            return result;
        }

        // Token: 0x0600000F RID: 15 RVA: 0x0000240C File Offset: 0x0000060C
        public void KeepDisplaying()
        {
            lastKeepDisplayTick = Find.TickManager.TicksGame;
        }

        // Token: 0x06000010 RID: 16 RVA: 0x00002420 File Offset: 0x00000620
        private void AbsorbedDamage(DamageInfo dinfo)
        {
            SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(Wearer.Position, Wearer.Map));
            impactAngleVect = Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle);
            var vector = Wearer.TrueCenter() + (impactAngleVect.RotatedBy(180f) * 0.5f);
            var num = Mathf.Min(10f, 2f + (dinfo.Amount / 10f));
            FleckMaker.Static(vector, Wearer.Map, FleckDefOf.ExplosionFlash, num);
            var num2 = (int) num;
            for (var i = 0; i < num2; i++)
            {
                FleckMaker.ThrowDustPuff(vector, Wearer.Map, Rand.Range(0.8f, 1.2f));
            }

            lastAbsorbDamageTick = Find.TickManager.TicksGame;
            KeepDisplaying();
        }

        // Token: 0x06000011 RID: 17 RVA: 0x00002520 File Offset: 0x00000720
        private void Break()
        {
            SoundDefOf.EnergyShield_Broken.PlayOneShot(new TargetInfo(Wearer.Position, Wearer.Map));
            FleckMaker.Static(Wearer.TrueCenter(), Wearer.Map, FleckDefOf.ExplosionFlash, 12f);
            for (var i = 0; i < 6; i++)
            {
                var vector = Wearer.TrueCenter() + (Vector3Utility.HorizontalVectorFromAngle(Rand.Range(0, 360)) *
                                                    Rand.Range(0.3f, 0.6f));
                FleckMaker.ThrowDustPuff(vector, Wearer.Map, Rand.Range(0.8f, 1.2f));
            }

            energy = 0f;
            ticksToReset = StartingTicksToReset;
        }

        // Token: 0x06000012 RID: 18 RVA: 0x00002600 File Offset: 0x00000800
        private void Reset()
        {
            var spawned = Wearer.Spawned;
            if (spawned)
            {
                SoundDefOf.EnergyShield_Reset.PlayOneShot(new TargetInfo(Wearer.Position, Wearer.Map));
                FleckMaker.ThrowLightningGlow(Wearer.TrueCenter(), Wearer.Map, 3f);
            }

            ticksToReset = -1;
            energy = EnergyOnReset;
        }

        // Token: 0x06000013 RID: 19 RVA: 0x00002680 File Offset: 0x00000880
        public override void DrawWornExtras()
        {
            if (ShieldState != ShieldState.Active || !ShouldDisplay)
            {
                return;
            }

            var num = Mathf.Lerp(1.2f, 1.55f, energy);
            var vector = Wearer.Drawer.DrawPos;
            vector.y = AltitudeLayer.Blueprint.AltitudeFor();
            var num2 = Find.TickManager.TicksGame - lastAbsorbDamageTick;
            if (num2 < 8)
            {
                var num3 = (8 - num2) / 8f * 0.05f;
                vector += impactAngleVect * num3;
                num -= num3;
            }

            float num4 = Rand.Range(0, 360);
            var vector2 = new Vector3(num, 1f, num);
            var matrix4x = default(Matrix4x4);
            matrix4x.SetTRS(vector, Quaternion.AngleAxis(num4, Vector3.up), vector2);
            Graphics.DrawMesh(MeshPool.plane10, matrix4x, BubbleMat, 0);
        }
    }
}