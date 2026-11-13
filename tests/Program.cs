namespace Warp.Tests;

internal static class Program
{
    private static int Main()
    {
        AtomTests.Run();
        TranslationTests.Run();
        SwitchPolicyTests.Run();
        WarpWaitPolicyTests.Run();
        RegionFilesTests.Run();
        LayoutTests.Run();
        DataTests.Run();
        PreviewTests.Run();
        return Harness.Report();
    }
}
