// Taken from MonoMod, licensed under the MIT license.
// https://github.com/MonoMod/MonoMod/blob/bc177577/src/MonoMod.RuntimeDetour/DetourConfig.cs

using System.Collections.Generic;
using System.Linq;
using MonoDetour.Bindings.Reorg.RuntimeDetour;

namespace MonoDetour;

/// <summary>
/// Priority configuration for a MonoDetour Hook.
/// </summary>
public class MonoDetourConfig : IMonoDetourConfig
{
    /// <summary>
    /// Gets the override ID if one is defined. If not defined,
    /// MonoDetour applies <see cref="MonoDetourManager.Id"/> as the hooks' ID.
    /// </summary>
    public string? OverrideId { get; }

    /// <summary>
    /// Gets the priority of the detours represented by this config.
    /// </summary>
    /// <remarks>
    /// The priority only affects the relative ordering of detours which are not otherwise ordered by e.g.
    /// <see cref="Before"/> or <see cref="After"/>.<br/>
    /// MonoDetour hooks always have a priority which is 0 by default.<br/>
    /// MonoMod detours with no priority are ordered <i>after</i> all detours which have a priority.
    /// </remarks>
    public int Priority { get; }

    /// <summary>
    /// Gets the detour IDs to run before this detour.
    /// </summary>
    /// <remarks>
    /// This takes takes priority over <see cref="Priority"/>.
    /// </remarks>
    public IEnumerable<string> Before { get; }

    /// <summary>
    /// Gets the detour IDs to run after this detour.
    /// </summary>
    /// <remarks>
    /// This takes takes priority over <see cref="Priority"/>.
    /// </remarks>
    public IEnumerable<string> After { get; }

    // /// <summary>
    // /// Gets the sub-priority of the detours represented by this config, which controls the order of hooks with the same priority.
    // /// </summary>
    // /// <remarks>
    // /// This is only intended to be used for advanced applications - you should use <see cref="Priority"/> for almost all regular use cases.
    // /// </remarks>
    // [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    // private int SubPriority { get; } // For now not public since this is not supported in legacy.

    /// <summary>
    /// Constructs a <see cref="MonoDetourConfig"/> with a specific ID, and any of the ordering options.
    /// </summary>
    /// <param name="priority">The priority of the detour config. Refer to <see cref="Priority"/> for details.</param>
    /// <param name="before">An enumerable containing the list of IDs of detours to run before detours with this config.</param>
    /// <param name="after">An enumerable containing the list of IDs of detours to run after detours with this config.</param>
    /// <param name="overrideId">The ID for hooks. If not defined,
    /// MonoDetour applies <see cref="MonoDetourManager.Id"/> as the hooks' ID.</param>
    public MonoDetourConfig(
        int priority = 0,
        IEnumerable<string>? before = null,
        IEnumerable<string>? after = null,
        string? overrideId = null
    )
    {
        Priority = priority;
        Before = AsFixedSize(before ?? []);
        After = AsFixedSize(after ?? []);
        OverrideId = overrideId;
    }

    // : this(id, priority, before, after, 0) { }

    // /// <summary>
    // /// Constructs a <see cref="MonoDetourPriority"/> with a specific ID, and any of the ordering options (including advanced options).
    // /// </summary>
    // /// <param name="id">The ID of the detour config.</param>
    // /// <param name="priority">The priority of the detour config. Refer to <see cref="Priority"/> for details.</param>
    // /// <param name="before">An enumerable containing the list of IDs of detours to run before detours with this config.</param>
    // /// <param name="after">An enumerable containing the list of IDs of detours to run after detours with this config.</param>
    // /// <param name="subPriority">The sub-priority of the detour config. Refer to <see cref="SubPriority"/> for details.</param>
    // [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    // public MonoDetourPriority(
    //     string id,
    //     int? priority,
    //     IEnumerable<string>? before,
    //     IEnumerable<string>? after,
    //     int subPriority
    // )
    // {
    //     OverrideId = id;
    //     Priority = priority;
    //     Before = AsFixedSize(before ?? []);
    //     After = AsFixedSize(after ?? []);
    //     SubPriority = subPriority;
    // }

    private static IEnumerable<string> AsFixedSize(IEnumerable<string> enumerable)
    {
        if (enumerable == Enumerable.Empty<string>())
            return enumerable;
        if (enumerable is ICollection<string>)
            return enumerable;
        return enumerable.ToArray();
    }

    /// <summary>
    /// Creates a new <see cref="MonoDetourConfig"/> which is identical to this one, but with <see cref="Priority"/> equal to <paramref name="priority"/>.
    /// </summary>
    /// <param name="priority">The priority of the new <see cref="MonoDetourConfig"/>.</param>
    /// <returns>A <see cref="MonoDetourConfig"/> identical to this one, but with <see cref="Priority"/> equal to <paramref name="priority"/>.</returns>
    public MonoDetourConfig WithPriority(int priority) =>
        new(
            priority,
            Before,
            After, /* , SubPriority */
            OverrideId
        );

    /// <summary>
    /// Creates a new <see cref="MonoDetourConfig"/> which is identical to this one, but with <see cref="Before"/> equal to <paramref name="before"/>.
    /// </summary>
    /// <param name="before">The <see cref="Before"/> list for the new <see cref="MonoDetourConfig"/>.</param>
    /// <returns>A <see cref="MonoDetourConfig"/> identical to this one, but with <see cref="Before"/> equal to <paramref name="before"/>.</returns>
    public MonoDetourConfig WithBefore(IEnumerable<string> before) =>
        new(
            Priority,
            before,
            After, /* , SubPriority */
            OverrideId
        );

    /// <summary>
    /// Creates a new <see cref="MonoDetourConfig"/> which is identical to this one, but with <see cref="Before"/> equal to <paramref name="before"/>.
    /// </summary>
    /// <param name="before">The <see cref="Before"/> list for the new <see cref="MonoDetourConfig"/>.</param>
    /// <returns>A <see cref="MonoDetourConfig"/> identical to this one, but with <see cref="Before"/> equal to <paramref name="before"/>.</returns>
    public MonoDetourConfig WithBefore(params string[] before) => WithBefore(before.AsEnumerable());

    /// <summary>
    /// Creates a new <see cref="MonoDetourConfig"/> which is identical to this one, but with <see cref="After"/> equal to <paramref name="after"/>.
    /// </summary>
    /// <param name="after">The <see cref="After"/> list for the new <see cref="MonoDetourConfig"/>.</param>
    /// <returns>A <see cref="MonoDetourConfig"/> identical to this one, but with <see cref="After"/> equal to <paramref name="after"/>.</returns>
    public MonoDetourConfig WithAfter(IEnumerable<string> after) =>
        new(
            Priority,
            Before,
            after, /* , SubPriority */
            OverrideId
        );

    /// <summary>
    /// Creates a new <see cref="MonoDetourConfig"/> which is identical to this one, but with <see cref="After"/> equal to <paramref name="after"/>.
    /// </summary>
    /// <param name="after">The <see cref="After"/> list for the new <see cref="MonoDetourConfig"/>.</param>
    /// <returns>A <see cref="MonoDetourConfig"/> identical to this one, but with <see cref="After"/> equal to <paramref name="after"/>.</returns>
    public MonoDetourConfig WithAfter(params string[] after) => WithAfter(after.AsEnumerable());

    /// <summary>
    /// Creates a new <see cref="MonoDetourConfig"/> which is identical to this one, but with <paramref name="before"/> added to <see cref="Before"/>.
    /// </summary>
    /// <param name="before">The list of IDs to add to <see cref="Before"/>.</param>
    /// <returns>A <see cref="MonoDetourConfig"/> with <paramref name="before"/> added to <see cref="Before"/>.</returns>
    public MonoDetourConfig AddBefore(IEnumerable<string> before) =>
        WithBefore(Before.Concat(before));

    /// <summary>
    /// Creates a new <see cref="MonoDetourConfig"/> which is identical to this one, but with <paramref name="before"/> added to <see cref="Before"/>.
    /// </summary>
    /// <param name="before">The list of IDs to add to <see cref="Before"/>.</param>
    /// <returns>A <see cref="MonoDetourConfig"/> with <paramref name="before"/> added to <see cref="Before"/>.</returns>
    public MonoDetourConfig AddBefore(params string[] before) => AddBefore(before.AsEnumerable());

    /// <summary>
    /// Creates a new <see cref="MonoDetourConfig"/> which is identical to this one, but with <paramref name="after"/> added to <see cref="After"/>.
    /// </summary>
    /// <param name="after">The list of IDs to add to <see cref="After"/>.</param>
    /// <returns>A <see cref="MonoDetourConfig"/> with <paramref name="after"/> added to <see cref="After"/>.</returns>
    public MonoDetourConfig AddAfter(IEnumerable<string> after) => WithAfter(After.Concat(after));

    /// <summary>
    /// Creates a new <see cref="MonoDetourConfig"/> which is identical to this one, but with <paramref name="after"/> added to <see cref="After"/>.
    /// </summary>
    /// <param name="after">The list of IDs to add to <see cref="After"/>.</param>
    /// <returns>A <see cref="MonoDetourConfig"/> with <paramref name="after"/> added to <see cref="After"/>.</returns>
    public MonoDetourConfig AddAfter(params string[] after) => AddAfter(after.AsEnumerable());
}
