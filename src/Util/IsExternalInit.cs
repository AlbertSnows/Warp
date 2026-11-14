namespace System.Runtime.CompilerServices;

/// <summary>
/// A stand-in for a type that .NET Framework 4.8 does not have, so that records and <c>init</c> properties compile for
/// Warp, which has to target net48 because Rain World's Unity runs mods on that family of libraries. The compiler marks
/// an <c>init</c> setter with this type and refuses to build without one; an empty internal class is enough, and it has
/// no effect at run time.
/// <para>
/// Keep the namespace and name exactly as they are, keep it <c>internal</c> (a public copy would clash with another
/// mod's), and do not compile it into the test project, which targets net8.0 and has the real type.
/// </para>
/// It has to be a file of its own: the compiler looks the type up by this exact namespace and name.
/// </summary>
internal static class IsExternalInit
{
}
