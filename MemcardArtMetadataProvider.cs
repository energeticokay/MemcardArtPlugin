using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace MemcardArtPlugin
{
    // These classes map the hidden WordPress API response
    public class WpPost
    {
        public WpContent content { get; set; }
    }

    public class WpContent
    {
        public string rendered { get; set; }
    }

    public class MemcardArtMetadataProvider : OnDemandMetadataProvider
    {
        private readonly MetadataRequestOptions options;
        private readonly MemcardArtPlugin plugin;
        private static readonly ILogger logger = LogManager.GetLogger();

        public MemcardArtMetadataProvider(MetadataRequestOptions options, MemcardArtPlugin plugin)
        {
            this.options = options;
            this.plugin = plugin;
        }

        public override List<MetadataField> AvailableFields => new List<MetadataField> { MetadataField.Icon };

        public override MetadataFile GetIcon(GetMetadataFieldArgs args)
        {
            var gameName = options.GameData.Name;
            if (string.IsNullOrEmpty(gameName)) return base.GetIcon(args);

            try
            {
                logger.Info($"Querying Memcard.art API for: {gameName}");

                // Force modern secure connections
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                // 1. Ask the hidden WordPress API to search for the game
                string searchUrl = $"https://memcard.art/wp-json/wp/v2/posts?search={Uri.EscapeDataString(gameName)}&per_page=3";
                
                using (var webClient = new WebClient())
                {
                    webClient.Headers.Add("User-Agent", "Playnite Metadata Plugin");
                    string jsonResponse = webClient.DownloadString(searchUrl);

                    // 2. Parse the results using Playnite's native JSON reader
                    var posts = Serialization.FromJson<List<WpPost>>(jsonResponse);

                    if (posts == null || posts.Count == 0)
                    {
                        logger.Info($"No memcard.art results found for {gameName}.");
                        return base.GetIcon(args);
                    }

                    // Look through the top results
                    foreach (var post in posts)
                    {
                        string postContent = post.content?.rendered ?? "";
                        
                        // 3. Find all images inside the post's code
                        var urlRegex = new Regex(@"(https://memcard\.art/wp-content/uploads/[^""\s]+\.(?:gif|png))", RegexOptions.IgnoreCase);
                        var matches = urlRegex.Matches(postContent);

                        string bestUrl = null;
                        int bestScore = -1;

                        foreach (Match match in matches)
                        {
                            string url = match.Groups[1].Value;
                            
                            // Skip the multi-icon preview grids (we want the single memory card)
                            if (url.Contains("-MI.", StringComparison.OrdinalIgnoreCase)) continue;

                            // Score the image to ensure we get the best quality version
                            int score = 0;
                            if (url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)) score += 10; // Prefer animated GIFs!
                            if (url.Contains("-256")) score += 5; // Prefer high res 256px over 32px

                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestUrl = url;
                            }
                        }

                        if (!string.IsNullOrEmpty(bestUrl))
                        {
                            logger.Info($"Found memcard.art icon for {gameName}: {bestUrl}");
                            return new MetadataFile(bestUrl);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error fetching metadata from memcard.art API for {gameName}");
            }

            return base.GetIcon(args);
        }
    }
}
