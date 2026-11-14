using System;
using System.IO;
using System.Reflection;

namespace Warp.Util;

/// <summary>
/// Reads text files that are built into the plugin.
/// A file of its own because it is the one place that reads the plugin's own built-in files, which the palette and the
/// colors file both need.
/// </summary>
internal static class EmbeddedText
{
    /// <summary> The text of an embedded file, as UTF-8. Throws if the plugin has no such file. </summary>
    public static string Read(string name)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                              ?? throw new InvalidOperationException("The plugin has no embedded file named " + name);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
