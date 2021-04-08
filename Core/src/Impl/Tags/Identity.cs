using System;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl.Tags
{
  internal sealed class Identity
  {
    [NotNull]
    public readonly string Product;

    [NotNull]
    public readonly string Version;

    public Identity(
      [NotNull] string product,
      [NotNull] string version)
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