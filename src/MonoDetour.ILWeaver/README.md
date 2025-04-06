# MonoDetour.ILWeaver

A redesigned ILCursor.

## Design Philosophy

### Be Explicit

Don't hide details.

- **ILCursor** abstracts away dealing with branching labels when removing instructions.
- **ILWeaver** forces the user to deal with retargeting labels on instruction removal.

The goal is to familiarize users with these concepts, which helps to avoid bugs caused by uninformed actions. While this may appear to make the API harder on the surface level, it will be beneficial when the user actually needs to deal with labels.

### Avoid Pitfalls

**ILCursor**'s `GotoNext` method is great, except when it isn't. It's too easy to match to a wrong place, usually because your match wasn't strict enough. Therefore **ILWeaver** prefers alternatives which avoid this issue, such as `ILWeaver.GotoMatch` which analyzes the whole target method and only allows one match instance. This also ties closely with the next section.

### Helpful Errors

Instead of just telling what the error is, ILWeaver should do its best to help the user to solve the errors it throws.
