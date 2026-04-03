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

                string searchUrl = $"https://memcard.art/wp-json/wp/v2/posts?search={Uri.EscapeDataString(gameName)}&per_page=3";
                
                using (var webClient = new WebClient())
                {
                    webClient.Headers.Add("User-Agent", "Playnite Metadata Plugin");
                    string jsonResponse = webClient.DownloadString(searchUrl);

                    var posts = Serialization.FromJson<List<WpPost>>(jsonResponse);

                    if (posts == null || posts.Count == 0)
                    {
                        logger.Info($"No memcard.art results found for {gameName}.");
                        return base.GetIcon(args);
                    }

                    foreach (var post in posts)
                    {
                        string postContent = post.content?.rendered ?? "";
                        
                        var urlRegex = new Regex(@"(https://memcard\.art/wp-content/uploads/[^""\s]+\.(?:gif|png))", RegexOptions.IgnoreCase);
                        var matches = urlRegex.Matches(postContent);

                        string bestUrl = null;
                        int bestScore = -1;

                        foreach (Match match in matches)
                        {
                            string url = match.Groups[1].Value;
                            
                            // .NET 4.6.2 compatible check for the "-MI." string (Multi-Icon grid preview)
                            if (url.IndexOf("-MI.", StringComparison.OrdinalIgnoreCase) >= 0) 
                            {
                                continue;
                            }

                            int score = 0;
                            if (url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)) score += 10;
                            
                            // .NET 4.6.2 compatible check for "-256"
                            if (url.IndexOf("-256", StringComparison.OrdinalIgnoreCase) >= 0) score += 5;

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
