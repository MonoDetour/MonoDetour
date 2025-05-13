# MonoDetour.Bindings.Reorg

This internal library contains wrapper methods for MonoMod reorg (>= 25) while allowing the consuming assembly to reference a legacy version of MonoMod, removing the need for separate MonoDetour assemblies for different MonoMod versions.

The purpose is the make supporting MonoDetour for multiple MonoMod versions easier since legacy MonoMod is still widely used.

This library is used at [/src/MonoDetour/Interop/](/src/MonoDetour/Interop/).
