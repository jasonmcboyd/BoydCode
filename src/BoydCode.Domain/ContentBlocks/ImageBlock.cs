using BoydCode.Domain.Enums;

namespace BoydCode.Domain.ContentBlocks;

public sealed record ImageBlock(string MediaType, string Base64Data) : ContentBlock(ContentBlockType.Image);
