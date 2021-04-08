using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl.Tags
{
  internal sealed class IdentityFilter
  {
    private readonly List<Regex> myExcludeProductRegexs;
    private readonly List<Regex> myExcludeVersionRegexs;
    private readonly List<Regex> myIncludeProductRegexs;
    private readonly List<Regex> myIncludeVersionRegexs;

    public IdentityFilter(
      [NotNull] IEnumerable<string> incProductWildcards,
      [NotNull] IEnumerable<string> excProductWildcards,
      [NotNull] IEnumerable<string> incVersionWildcards,
      [NotNull] IEnumerable<string> excVersionWildcards)
    {
      if (incProductWildcards == null) throw new ArgumentNullException(nameof(incProductWildcards));
      if (excProductWildcards == null) throw new ArgumentNullException(nameof(excProductWildcards));
      if (incVersionWildcards == null) throw new ArgumentNullException(nameof(incVersionWildcards));
      if (excVersionWildcards == null) throw new ArgumentNullException(nameof(excVersionWildcards));

      [NotNull]
      static string ConvertWildcardToRegex([NotNull] string str) => "^" + Regex.Escape(str).Replace("\\?", ".").Replace("\\*", ".*") + "$";

      myIncludeProductRegexs = incProductWildcards.Select(x =>
        {
          if (x == null)
            throw new ArgumentNullException(nameof(incProductWildcards));
          if (!TagUtil.ValidateProductWildcard(x))
            throw new ArgumentException($"Invalid product name include wildcard {x}", nameof(incProductWildcards));
          return new Regex(ConvertWildcardToRegex(x));
        }).ToList();
      myExcludeProductRegexs = excProductWildcards.Select(x =>
        {
          if (x == null)
            throw new ArgumentNullException(nameof(incProductWildcards));
          if (!TagUtil.ValidateProductWildcard(x))
            throw new ArgumentException($"Invalid product name exclude wildcard {x}", nameof(excProductWildcards));
          return new Regex(ConvertWildcardToRegex(x));
        }).ToList();
      myIncludeVersionRegexs = incVersionWildcards.Select(x =>
        {
          if (x == null)
            throw new ArgumentNullException(nameof(incProductWildcards));
          if (!TagUtil.ValidateVersionWildcard(x))
            throw new ArgumentException($"Invalid version include wildcard {x}", nameof(incVersionWildcards));
          return new Regex(ConvertWildcardToRegex(x));
        }).ToList();
      myExcludeVersionRegexs = excVersionWildcards.Select(x =>
        {
          if (x == null)
            throw new ArgumentNullException(nameof(incProductWildcards));
          if (!TagUtil.ValidateVersionWildcard(x))
            throw new ArgumentException($"Invalid version exclude wildcard {x}", nameof(excVersionWildcards));
          return new Regex(ConvertWildcardToRegex(x));
        }).ToList();
    }

    public bool IsMatch([NotNull] string product, [NotNull] string version) =>
      (myIncludeProductRegexs.Count == 0 || myIncludeProductRegexs.Any(x => x.IsMatch(product))) && myExcludeProductRegexs.All(x => !x.IsMatch(product)) &&
      (myIncludeVersionRegexs.Count == 0 || myIncludeVersionRegexs.Any(x => x.IsMatch(version))) && myExcludeVersionRegexs.All(x => !x.IsMatch(version));
  }
}