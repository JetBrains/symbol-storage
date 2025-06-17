using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace JetBrains.SymbolStorage.Impl.Tags
{
  internal sealed class IdentityFilter
  {
    private readonly Regex? myExcludeProductRegex;
    private readonly Regex? myExcludeVersionRegex;
    private readonly Regex? myIncludeProductRegex;
    private readonly Regex? myIncludeVersionRegex;

    public IdentityFilter(
      IEnumerable<string> incProductWildcards,
      IEnumerable<string> excProductWildcards,
      IEnumerable<string> incVersionWildcards,
      IEnumerable<string> excVersionWildcards)
    {
      if (incProductWildcards == null) throw new ArgumentNullException(nameof(incProductWildcards));
      if (excProductWildcards == null) throw new ArgumentNullException(nameof(excProductWildcards));
      if (incVersionWildcards == null) throw new ArgumentNullException(nameof(incVersionWildcards));
      if (excVersionWildcards == null) throw new ArgumentNullException(nameof(excVersionWildcards));

      myExcludeProductRegex = BuildRegex("product exclude", excProductWildcards, TagUtil.ValidateProductWildcard);
      myExcludeVersionRegex = BuildRegex("version exclude", excVersionWildcards, TagUtil.ValidateVersionWildcard);
      myIncludeProductRegex = BuildRegex("product include", incProductWildcards, TagUtil.ValidateProductWildcard);
      myIncludeVersionRegex = BuildRegex("version include", incVersionWildcards, TagUtil.ValidateVersionWildcard);
    }
    
    private static string ConvertWildcardToRegex(string str) => Regex.Escape(str).Replace("\\?", ".").Replace("\\*", ".*");
    private static Regex? BuildRegex(string name, IEnumerable<string> wildcards, Func<string, bool> validator)
    {
      int count = 0;
      StringBuilder builder = new StringBuilder();
      foreach (var wildcard in wildcards)
      {
        if (!validator(wildcard))
          throw new ArgumentException($"Invalid {name} wildcard: '{wildcard}'", nameof(wildcards));

        if (count == 0)
          builder.Append("^(?:(?:");
        else if (count >= 1)
          builder.Append(")|(?:");

        builder.Append(ConvertWildcardToRegex(wildcard));
        count++;
      }

      if (count == 0)
        return null;

      builder.Append("))$");
      return new Regex(builder.ToString(), RegexOptions.Compiled);
    }

    public bool IsMatch(string product, string version) =>
      (myIncludeProductRegex == null || myIncludeProductRegex.IsMatch(product)) &&
      (myExcludeProductRegex == null || !myExcludeProductRegex.IsMatch(product)) &&
      (myIncludeVersionRegex == null || myIncludeVersionRegex.IsMatch((version))) &&
      (myExcludeVersionRegex == null || !myExcludeVersionRegex.IsMatch((version)));
  }
}