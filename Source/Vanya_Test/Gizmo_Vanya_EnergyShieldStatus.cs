using RimWorld;
using UnityEngine;
using Verse;

namespace Vanya_Test;

[StaticConstructorOnStartup]
internal class Gizmo_Vanya_EnergyShieldStatus : Gizmo
{
    private static readonly Texture2D FullShieldBarTex =
        SolidColorMaterials.NewSolidColorTexture(new Color(0.3f, 0.2f, 0.3f));

    private static readonly Texture2D EmptyShieldBarTex = SolidColorMaterials.NewSolidColorTexture(Color.clear);

    public Vanya_ShieldBelt shield;

    // Note: this type is marked as 'beforefieldinit'.
    static Gizmo_Vanya_EnergyShieldStatus()
    {
    }


    public virtual float Width => 140f;

    public virtual GizmoResult GizmoOnGUI(Vector2 topLeft)
    {
        return GizmoOnGUI(topLeft, Width, new GizmoRenderParms());
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        var overRect = new Rect(topLeft.x, topLeft.y, Width, 75f);
        Find.WindowStack.ImmediateWindow(984688, overRect, WindowLayer.GameUI, delegate
        {
            var rect = overRect.AtZero().ContractedBy(6f);
            var rect2 = rect;
            rect2.height = overRect.height / 2f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(rect2, shield.LabelCap);
            var rect3 = rect;
            rect3.yMin = overRect.height / 2f;
            var num = shield.Energy / Mathf.Max(1f, shield.GetStatValue(StatDefOf.EnergyShieldEnergyMax));
            Widgets.FillableBar(rect3, num, FullShieldBarTex, EmptyShieldBarTex, false);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect3,
                $"{shield.Energy * 100f:F0} / {shield.GetStatValue(StatDefOf.EnergyShieldEnergyMax) * 100f:F0}");
            Text.Anchor = 0;
        });
        return new GizmoResult(GizmoState.Clear);
    }

    public override float GetWidth(float maxWidth)
    {
        return Width;
    }
}