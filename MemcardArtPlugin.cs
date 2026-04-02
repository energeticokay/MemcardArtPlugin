using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;

namespace MemcardArtPlugin
{
    public class MemcardArtPlugin : MetadataPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        // ONLY provide metadata for Sony PlayStation
        public override Guid Id { get; } = Guid.Parse("11111111-2222-3333-4444-555555555555"); // Replace with a unique GUID if you want

        public override List<MetadataField> SupportedFields { get; } = new List<MetadataField>
        {
            MetadataField.Icon
        };

        public override string Name => "Memcard.art";

        public MemcardArtPlugin(IPlayniteAPI api) : base(api)
        {
        }

        public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
        {
            return new MemcardArtMetadataProvider(options, this);
        }
    }
}
