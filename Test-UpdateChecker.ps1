$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$testDirectory = Join-Path $PSScriptRoot 'obj\update-tests'
New-Item -ItemType Directory -Force $testDirectory | Out-Null
$source = @'
using System;
using System.Web.Script.Serialization;
public class UpdateTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static string Release(string version, bool draft, string asset, string state, string url)
    {
        return new JavaScriptSerializer().Serialize(new { tag_name = version, draft = draft, prerelease = true,
            html_url = url ?? "https://github.com/AlbinoPriest/swooshdrop/releases/tag/" + version,
            assets = new[] { new { name = asset, state = state } } });
    }
    public static void Main(string[] args)
    {
        Assert(UpdateChecker.ParseVersion("v0.3.6") == new Version(0,3,6,0), "Normalize three-part versions");
        Assert(UpdateChecker.ParseVersion("v0.3.10") > UpdateChecker.ParseVersion("v0.3.9"), "Numeric ordering");
        Assert(UpdateChecker.ParseVersion("v0.3.6-beta") == null, "Unknown tags must not suggest updates");
        string payload = "[" + Release("v0.3.6", false, "SwooshDropSetup.exe", "uploaded", null) + "," +
            Release("v9.0.0", true, "SwooshDropSetup.exe", "uploaded", null) + "," +
            Release("v8.0.0", false, "SwooshDropSetup.exe", "new", null) + "," +
            Release("v7.0.0", false, "Other.exe", "uploaded", null) + "," +
            Release("v6.0.0", false, "SwooshDropSetup.exe", "uploaded", "https://example.com/download") + "," +
            Release("v0.3.10", false, "SwooshDropSetup.exe", "uploaded", null) + "]";
        Assert(UpdateChecker.SelectRelease(payload).Version == "v0.3.10", "Select downloadable beta; reject drafts, incomplete assets, other repos");
        Assert(!UpdateChecker.IsReleaseUrl("https://github.com.evil.test/AlbinoPriest/swooshdrop/releases/tag/v1.0.0"), "Reject lookalike host");
        Assert(!UpdateChecker.IsReleaseUrl("https://github.com/Other/repo/releases/tag/v1.0.0"), "Reject other repositories");
        Assert(!UpdateChecker.IsReleaseUrl("http://github.com/AlbinoPriest/swooshdrop/releases/tag/v1.0.0"), "Require HTTPS");
        bool rejected = false;
        try { UpdateChecker.SelectRelease("[]"); } catch (System.IO.InvalidDataException) { rejected = true; }
        Assert(rejected, "Empty response must not claim up to date");
        rejected = false;
        try { UpdateChecker.SelectRelease("not json"); } catch { rejected = true; }
        Assert(rejected, "Reject malformed responses");
        Console.WriteLine("Update selection and URL checks passed.");
        if (args.Length > 0 && args[0] == "--live")
        {
            var result = UpdateChecker.Check();
            Console.WriteLine("Live GitHub check: " + result.Version + " " + result.ReleaseUrl);
        }
    }
}
'@
$testSource = Join-Path $testDirectory 'UpdateTests.cs'
[IO.File]::WriteAllText($testSource, $source)
$testExe = Join-Path $testDirectory 'UpdateTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe "/out:$testExe" "/reference:$(Join-Path $framework 'System.Web.Extensions.dll')" (Join-Path $PSScriptRoot 'UpdateChecker.cs') $testSource
if ($LASTEXITCODE -ne 0) { throw 'Update test compilation failed.' }
& $testExe @args
if ($LASTEXITCODE -ne 0) { throw 'Update checker tests failed.' }
