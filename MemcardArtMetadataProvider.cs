using HtmlAgilityPack;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MemcardArtPlugin
{
    public class MemcardArtMetadataProvider : OnDemandMetadataProvider
    {
        private readonly MetadataRequestOptions options;
        private readonly MemcardArtPlugin plugin;
        private static readonly ILogger logger = LogManager.GetLogger();

        private const string baseUrl = "https://memcard.art";

        public MemcardArtMetadataProvider(MetadataRequestOptions options, MemcardArtPlugin plugin)
        {
            this.options = options;
            this.plugin = plugin;
        }

        private string NormalizeGameName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            // Remove special characters, spaces, and make lowercase to ensure matching works properly
            var normalized = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]", "");
            return normalized;
        }

        public override List<MetadataField> AvailableFields => new List<MetadataField> { MetadataField.Icon };

        public override MetadataFile GetIcon(GetMetadataFieldArgs args)
        {
            var gameName = options.GameData.Name;
            var targetNameNormal = NormalizeGameName(gameName);

            try
            {
                logger.Info($"Fetching Memcard.art for: {gameName}");

                // Load the HTML from memcard.art
                var web = new HtmlWeb();
                var doc = web.Load(baseUrl);

                // Find all image tags on the site that have an alt attribute
                var imageNodes = doc.DocumentNode.SelectNodes("//img[@alt]");

                if (imageNodes != null)
                {
                    foreach (var node in imageNodes)
                    {
                        var altText = node.GetAttributeValue("alt", "");
                        var srcText = node.GetAttributeValue("src", "");

                        // Skip if no source or alt text
                        if (string.IsNullOrEmpty(altText) || string.IsNullOrEmpty(srcText)) continue;

                        var siteGameNormal = NormalizeGameName(altText);

                        // If the normalized names match (e.g., "Final Fantasy VII" matches "finalfantasyvii")
                        if (siteGameNormal == targetNameNormal || siteGameNormal.Contains(targetNameNormal))
                        {
                            // Fix relative URLs (e.g., "/images/ff7.gif" -> "https://memcard.art/images/ff7.gif")
                            string fullImageUrl = srcText.StartsWith("/") ? baseUrl + srcText : srcText;

                            logger.Info($"Found matching icon for {gameName}: {fullImageUrl}");
                            
                            // Return the URL. Playnite will automatically download it and save it as the icon!
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

            // Return empty if nothing is found
            return base.GetIcon(args);
        }
    }
}
