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
}
```
