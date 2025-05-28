using System.Collections;

namespace MonoDetour.Reflection.Unspeakable;

/// <summary>
/// A Method which takes in an <see cref="IEnumerator"/> instance and
/// returns a field reference of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Field type.</typeparam>
/// <param name="instance"><see cref="IEnumerator"/> instance whose field to get.</param>
/// <returns>The field reference.</returns>
public delegate ref T EnumeratorFieldReferenceGetter<T>(IEnumerator instance);
