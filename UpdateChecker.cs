using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

public sealed class UpdateResult
{
    public string Version { get; set; }
    public string ReleaseUrl { get; set; }
    public DateTime CheckedUtc { get; set; }
}

public static class UpdateChecker
{
    public static Version ParseVersion(string tag)
    {
        // Published beta builds use numeric vX.Y.Z tags; unknown naming schemes are ignored.
        if (tag == null || !Regex.IsMatch(tag, @"^v?\d+\.\d+\.\d+(\.\d+)?$")) return null;
        Version version;
        if (!System.Version.TryParse(tag.TrimStart('v'), out version)) return null;
        return new Version(version.Major, version.Minor, version.Build, Math.Max(0, version.Revision));
    }

    public static bool IsReleaseUrl(string value)
    {
        Uri uri;
        return Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == "https" &&
            uri.Host == "github.com" && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
            uri.AbsolutePath.StartsWith("/AlbinoPriest/swooshdrop/releases/tag/", StringComparison.Ordinal) &&
            uri.Query.Length == 0 && uri.Fragment.Length == 0;
    }

    public static UpdateResult SelectRelease(string payload)
    {
        var serializer = new JavaScriptSerializer { MaxJsonLength = 2097152 };
        var releases = serializer.Deserialize<List<Dictionary<string, object>>>(payload);
        if (releases == null) throw new InvalidDataException("Invalid release response.");
        Version best = null;
        UpdateResult result = null;
        foreach (var release in releases)
        {
            object draft, tag, url, assets;
            if (!release.TryGetValue("draft", out draft) || !(draft is bool) || (bool)draft ||
                !release.TryGetValue("tag_name", out tag) || !release.TryGetValue("html_url", out url) ||
                !IsReleaseUrl(url as string) || !release.TryGetValue("assets", out assets)) continue;
            Version version = ParseVersion(tag as string);
            if (version == null || (best != null && version <= best)) continue;
            bool installer = false;
            var list = assets as System.Collections.IEnumerable;
            if (list == null) continue;
            foreach (object entry in list)
            {
                var asset = entry as Dictionary<string, object>;
                object name, state;
                if (asset != null && asset.TryGetValue("name", out name) && asset.TryGetValue("state", out state) &&
                    (name as string) == "SwooshDropSetup.exe" && (state as string) == "uploaded") installer = true;
            }
            if (!installer) continue;
            best = version;
            result = new UpdateResult { Version = tag as string, ReleaseUrl = url as string };
        }
        if (result == null) throw new InvalidDataException("No downloadable release was found.");
        result.CheckedUtc = DateTime.UtcNow;
        return result;
    }

    public static UpdateResult Check()
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        // The releases list includes betas, unlike GitHub's /latest endpoint.
        var request = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/AlbinoPriest/swooshdrop/releases?per_page=100");
        request.UserAgent = "SwooshDrop-update-check";
        request.Accept = "application/vnd.github+json";
        request.Headers["X-GitHub-Api-Version"] = "2026-03-10";
        request.Timeout = 15000; request.ReadWriteTimeout = 15000;
        request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        using (var response = request.GetResponse())
        using (var reader = new StreamReader(response.GetResponseStream()))
        {
            var buffer = new char[4096];
            var payload = new System.Text.StringBuilder();
            int count;
            while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (payload.Length + count > 2097152) throw new InvalidDataException("Release response is too large.");
                payload.Append(buffer, 0, count);
            }
            return SelectRelease(payload.ToString());
        }
    }
}
