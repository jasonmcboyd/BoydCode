using BoydCode.Domain.Enums;

namespace BoydCode.Domain.ContentBlocks;

public sealed record TextBlock(string Text) : ContentBlock(ContentBlockType.Text);
