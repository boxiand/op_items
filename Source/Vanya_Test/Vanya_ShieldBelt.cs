using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Vanya_Test;

[StaticConstructorOnStartup]
public class Vanya_ShieldBelt : Apparel
{
    private const float MinDrawSize = 1.2f;

    private const float MaxDrawSize = 1.4f;

    private const float MaxDamagedJitterDist = 0.05f;

    private const int JitterDurationTicks = 8;

    private static readonly Material BubbleMat =
        MaterialPool.MatFrom("Vanya_Shield/Vanya_Shield", ShaderDatabase.Transparent);

    private readonly float ApparelScorePerEnergyMax = 0.25f;

    private readonly float EnergyLossPerDamage = 0.025f;

    private readonly float EnergyOnReset = 0.2f;

    private readonly int KeepDisplayingTicks = 1000;

    private readonly int StartingTicksToReset = 3000;

    private float energy;

    private Vector3 impactAngleVect;

    private int lastAbsorbDamageTick = -9999;

    private int lastKeepDisplayTick = -9999;

    private int ticksToReset = -1;

    // Note: this type is marked as 'beforefieldinit'.
    static Vanya_ShieldBelt()
    {
    }


    private float EnergyMax => this.GetStatValue(StatDefOf.EnergyShieldEnergyMax);

    private float EnergyGainPerTick => this.GetStatValue(StatDefOf.EnergyShieldRechargeRate) / 60f;

    public float Energy => energy;

    public ShieldState ShieldState
    {
        get
        {
            var result = ticksToReset > 0 ? ShieldState.Resetting : ShieldState.Active;

            return result;
        }
    }

    private bool ShouldDisplay
    {
        get
        {
            var wearer = Wearer;
            return !wearer.Dead && !wearer.Downed &&
                   (!wearer.IsPrisonerOfColony || wearer.MentalStateDef is { IsAggro: true }) &&
                   (wearer.Drafted || wearer.Faction.HostileTo(Faction.OfPlayer) ||
                    Find.TickManager.TicksGame < lastKeepDisplayTick + KeepDisplayingTicks);
        }
    }

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

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref energy, "energy");
        Scribe_Values.Look(ref ticksToReset, "ticksToReset", -1);
        Scribe_Values.Look(ref lastKeepDisplayTick, "lastKeepDisplayTick");
    }

    public override float GetSpecialApparelScoreOffset()
    {
        return EnergyMax * ApparelScorePerEnergyMax;
    }

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

    public void KeepDisplaying()
    {
        lastKeepDisplayTick = Find.TickManager.TicksGame;
    }

    private void AbsorbedDamage(DamageInfo dinfo)
    {
        SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(Wearer.Position, Wearer.Map));
        impactAngleVect = Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle);
        var vector = Wearer.TrueCenter() + (impactAngleVect.RotatedBy(180f) * 0.5f);
        var num = Mathf.Min(10f, 2f + (dinfo.Amount / 10f));
        FleckMaker.Static(vector, Wearer.Map, FleckDefOf.ExplosionFlash, num);
        var num2 = (int)num;
        for (var i = 0; i < num2; i++)
        {
            FleckMaker.ThrowDustPuff(vector, Wearer.Map, Rand.Range(0.8f, 1.2f));
        }

        lastAbsorbDamageTick = Find.TickManager.TicksGame;
        KeepDisplaying();
    }

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