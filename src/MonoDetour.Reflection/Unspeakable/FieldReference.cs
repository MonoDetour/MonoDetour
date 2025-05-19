namespace MonoDetour.Reflection.Unspeakable;

/// <summary>
/// A Method which takes in an object instance and returns a field reference
/// of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Field type.</typeparam>
/// <param name="instance">Enumerator instance.</param>
/// <returns>The field reference.</returns>
public delegate ref T FieldReference<T>(object instance);
