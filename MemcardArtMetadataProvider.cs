using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace MemcardArtPlugin
{
    public class MemcardArtMetadataProvider : OnDemandMetadataProvider
    {
        private readonly MetadataRequestOptions options;
        private readonly MemcardArtPlugin plugin;
        private static readonly ILogger logger = LogManager.GetLogger();

        private const string baseUrl = "https://memcard.art";

        // We cache the HTML so bulk downloads are blazing fast
        private static string cachedHtml = string.Empty;
        private static DateTime cacheTime = DateTime.MinValue;
        private static readonly object cacheLock = new object();

        public MemcardArtMetadataProvider(MetadataRequestOptions options, MemcardArtPlugin plugin)
        {
            this.options = options;
            this.plugin = plugin;
        }

        private string NormalizeGameName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]", "");
        }

        public override List<MetadataField> AvailableFields => new List<MetadataField> { MetadataField.Icon };

        public override MetadataFile GetIcon(GetMetadataFieldArgs args)
        {
            var gameName = options.GameData.Name;
            var targetNameNormal = NormalizeGameName(gameName);

            try
            {
                logger.Info($"Fetching Memcard.art for: {gameName}");

                string htmlSource = string.Empty;

                // Lock ensures multiple games downloading at once don't spawn 50 webviews
                lock (cacheLock)
                {
                    if (string.IsNullOrEmpty(cachedHtml) || (DateTime.Now - cacheTime).TotalHours > 1)
                    {
                        logger.Info("Loading Memcard.art HTML via invisible browser...");
                        using (var webView = plugin.PlayniteApi.WebViews.CreateOffscreenView())
                        {
                            webView.NavigateAndWait(baseUrl);
                            // Give JavaScript 4 seconds to render the gallery
                            Thread.Sleep(4000); 
                            cachedHtml = webView.GetPageSource();
                            cacheTime = DateTime.Now;
                        }
                    }
                    htmlSource = cachedHtml;
                }

                if (string.IsNullOrEmpty(htmlSource))
                {
                    logger.Error("Failed to get HTML from memcard.art WebView.");
                    return base.GetIcon(args);
                }

                // Safely search for image tags without external DLLs
                var imgRegex = new Regex(@"<img[^>]+>", RegexOptions.IgnoreCase);
                var srcRegex = new Regex(@"src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                var altRegex = new Regex(@"alt=[""']([^""']+)[""']", RegexOptions.IgnoreCase);

                var matches = imgRegex.Matches(htmlSource);
                foreach (Match match in matches)
                {
                    var imgTag = match.Value;
                    var srcMatch = srcRegex.Match(imgTag);
                    var altMatch = altRegex.Match(imgTag);

                    if (srcMatch.Success && altMatch.Success)
                    {
                        var srcText = srcMatch.Groups[1].Value;
                        var altText = altMatch.Groups[1].Value;

                        var siteGameNormal = NormalizeGameName(altText);

                        if (!string.IsNullOrEmpty(siteGameNormal) && 
                           (siteGameNormal == targetNameNormal || siteGameNormal.Contains(targetNameNormal)))
                        {
                            string fullImageUrl = srcText.StartsWith("/") ? baseUrl + srcText : srcText;
                            if (!fullImageUrl.StartsWith("http")) fullImageUrl = baseUrl + "/" + srcText;

                            logger.Info($"Found matching icon for {gameName}: {fullImageUrl}");
                            return new MetadataFile(fullImageUrl);
                        }
                    }
                }

                logger.Info($"No memcard.art icon found for {gameName}.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error fetching metadata from memcard.art for {gameName}");
            }

            return base.GetIcon(args);
        }
    }
}
