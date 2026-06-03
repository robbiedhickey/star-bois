namespace Content.Server._FTL.FTLPoints.Effects;

[DataDefinition]
public sealed partial class ApplyWorldGenConfigEffect : FtlPointEffect
{
    [DataField("config")]
    public string ConfigPrototype = "Default";

    public override void Effect(FtlPointEffectArgs args)
    {
        // This fork does not carry the old Ekrixi worldgen configuration stack.
        // Keep the effect loadable so imported point definitions can stay intact.
    }
}
