# MonoDetour

## Usage

```cs
[DetourInit]
static void Init()
{
    var myHook = D.PlatformerController.SpinBounce.Prefix((args) =>
    {
        args.power = 100;
    })

    G.PlayerControllerB.Awake.Prefix(a =>
    {
        
    });
}
```

```cs
static void ILHook_PlayerControllerB_Awake(ILContent il)
{
    ILCursor c = new(il);
    
}
```
