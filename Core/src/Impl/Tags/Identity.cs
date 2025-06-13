#nullable enable

using System;

namespace JetBrains.SymbolStorage.Impl.Tags
{
  internal sealed record Identity
  {
    public string Product { get; }
    public string Version { get; }

    public Identity(
      string product,
      string version)
    {
      Product = product ?? throw new ArgumentNullException(nameof(product));
      Version = version ?? throw new ArgumentNullException(nameof(version));

      if (!TagUtil.ValidateProduct(product))
        throw new ArgumentException($"Invalid product name {product}", nameof(product));

      if (!TagUtil.ValidateVersion(version))
        throw new ArgumentException($"Invalid version {version}", nameof(version));
    }
  }
}