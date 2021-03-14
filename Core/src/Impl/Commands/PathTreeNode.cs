using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class PathTreeNode
  {
    private Dictionary<string, PathTreeNode> myChildren;
    private List<string> myFiles;
    private readonly string myName;
    private readonly PathTreeNode myParent;
    private long myReferences;
    private readonly object myLock = new();

    public PathTreeNode()
    {
    }

    private PathTreeNode([NotNull] PathTreeNode parent, [NotNull] string name)
    {
      myParent = parent ?? throw new ArgumentNullException(nameof(parent));
      myName = name ?? throw new ArgumentNullException(nameof(name));
    }

    public bool HasChildren => myChildren != null && myChildren.Count > 0;
    public bool HasFiles => myFiles != null && myFiles.Count > 0;
    public bool HasReferences => Interlocked.Read(ref myReferences) != 0;

    [NotNull]
    public IEnumerable<PathTreeNode> Children
    {
      get
      {
        lock (myLock)
          return myChildren?.Values ?? Enumerable.Empty<PathTreeNode>();
      }
    }

    [NotNull]
    public IEnumerable<string> Files
    {
      get
      {
        lock (myLock)
          return myFiles ?? Enumerable.Empty<string>();
      }
    }

    [NotNull]
    public override string ToString()
    {
      if (myParent == null)
        return "";
      if (myParent.myParent == null)
        return myName;
      return myParent.ToString() + Path.DirectorySeparatorChar + myName;
    }

    [NotNull]
    public PathTreeNode GetOrInsert(string part)
    {
      lock (myLock)
      {
        if (myChildren == null)
          myChildren = new Dictionary<string, PathTreeNode>();
        else if (myChildren.TryGetValue(part, out var value))
          return value;
        var child = new PathTreeNode(this, part);
        myChildren.Add(part, child);
        return child;
      }
    }

    [CanBeNull]
    public PathTreeNode Lookup(string part)
    {
      lock (myLock)
        return myChildren != null && myChildren.TryGetValue(part, out var value) ? value : null;
    }

    public void AddFile([NotNull] string file)
    {
      if (file == null) throw new ArgumentNullException(nameof(file));
      lock (myLock)
      {
        myFiles ??= new List<string>();
        myFiles.Add(file);
      }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementReferences() => Interlocked.Increment(ref myReferences);
  }
}