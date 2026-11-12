namespace System.Runtime.CompilerServices;

/// <summary>
/// A stand-in for a type that .NET Framework 4.8 does not have, so that records and <c>init</c> properties
/// compile for Warp, which has to target net48 because Rain World's Unity 2020.3 runs on that family of libraries.
/// <para>
/// Why it is needed: for a property with an <c>init</c> accessor (which a record, a <c>readonly record struct</c>
/// and a <c>readonly</c> struct's properties all have) the compiler marks the setter with a required modifier named
/// <c>System.Runtime.CompilerServices.IsExternalInit</c>. The type ships with .NET 5 and newer, not with net48, and
/// without it the build stops with error CS0518 ("Predefined type ... IsExternalInit is not defined or imported").
/// A plain <c>record struct</c>, which has ordinary setters, does not need it.
/// </para>
/// <para>
/// How the stand-in works: the compiler only needs a type with that exact namespace and name to exist, so an empty
/// <c>internal</c> class in this assembly is enough. It has no members and no effect at run time.
/// </para>
/// <para>
/// Rules: keep the namespace and name exactly as they are, keep it <c>internal</c> (a public copy would clash with
/// another mod's copy), and do not compile it into the test project, which targets net8.0 and already has the
/// real type.
/// Other missing types are done the same way, one small file each, only when the code needs them.
/// </para>
/// <para>
/// Sources:
/// the IsExternalInit polyfill package and its README, which describe re-declaring the class as <c>internal</c> in your
/// own project (https://github.com/manuelroemer/IsExternalInit);
/// a walk-through of records on older frameworks
/// (https://michalbrylka.github.io/posts/using-records-in-older-frameworks).
/// That a class record fails without it and a plain record struct does not was checked on this branch with a net48
/// test project.
/// </para>
/// </summary>
internal static class IsExternalInit
{
}
