#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  /// <summary>
  /// Path tree: represents file system hierarchy
  /// </summary>
  /// <param name="rootNode">Root node for this tree</param>
  internal readonly struct PathTree(PathTreeNode rootNode)
  {
    /// <summary>
    /// Creates builder for the tree
    /// </summary>
    public static PathTreeBuilder BuildNew() => PathTreeBuilder.BuildNew();

    public PathTreeNode Root => rootNode;

    public PathTreeNode? LookupPathRecursive(string path)
    {
      return rootNode.LookupPathRecursive(path.AsSpan());
    }
  }

  /// <summary>
  /// Path tree builder. Allows to build file system hierarchy
  /// </summary>
  internal readonly struct PathTreeBuilder
  {
    public static PathTreeBuilder BuildNew() => new PathTreeBuilder(PathTreeNode.CreateRoot());
    
    private readonly PathTreeNode myRootNode;
    
    public PathTreeBuilder()
    {
      myRootNode = PathTreeNode.CreateRoot();
    }
    private PathTreeBuilder(PathTreeNode rootNode)
    {
      myRootNode = rootNode;
    }

    public PathTreeNode.Builder Root => PathTreeNode.Builder.CreateUnsafe(myRootNode);

    public PathTreeNode.Builder AddPathRecursive(string path)
    {
      return Root.AddPathRecursive(path.AsSpan());
    }

    public PathTree Build()
    {
      return new PathTree(myRootNode);
    }
  }
  
  /// <summary>
  /// Node of the <see cref="PathTree"/>
  /// </summary>
  internal sealed class PathTreeNode
  {
    private static readonly List<string> EmptyList = new List<string>(0);
    private static readonly Dictionary<string, PathTreeNode> EmptyDict = new Dictionary<string, PathTreeNode>(0);
    
    public static PathTreeNode CreateRoot() => new PathTreeNode("", null);
    
    // =====================
    
    private readonly string myName;
    private readonly PathTreeNode? myParent;
    
    private readonly Lock myLock;
    private Dictionary<string, PathTreeNode>? myChildren;
    private List<string>? myFiles;
    
    private long myReferences;

    private PathTreeNode(string name, PathTreeNode? parent)
    {
      myName = name;
      myParent = parent;
      
      myLock = new Lock();
      myChildren = null;
      myFiles = null;

      myReferences = 0;
    }
    
    public bool HasChildren => myChildren != null && myChildren.Count > 0;
    public bool HasFiles => myFiles != null && myFiles.Count > 0;
    public bool HasReferences => Volatile.Read(ref myReferences) != 0;

    public IReadOnlyCollection<PathTreeNode> GetChildren()
    {
      return myChildren?.Values ?? EmptyDict.Values;
    }
    public IReadOnlyCollection<string> GetFiles()
    {
      return myFiles ?? EmptyList;
    }

    public PathTreeNode? Lookup(string part)
    {
      return myChildren != null && myChildren.TryGetValue(part, out var value) ? value : null;
    }
    public PathTreeNode? Lookup(ReadOnlySpan<char> part)
    {
      return myChildren != null && myChildren.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(part, out var value) ? value : null;
    }
    
    public PathTreeNode? LookupPathRecursive(ReadOnlySpan<char> path)
    {
      PathTreeNode? node = this;
      foreach (var partRange in path.GetPathComponents())
        node = node?.Lookup(path[partRange]);

      return node;
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementReferences() => Interlocked.Increment(ref myReferences);
    
    public override string ToString()
    {
      if (myParent == null)
        return "";
      if (myParent.myParent == null)
        return myName;
      
      return myParent.ToString() + Path.DirectorySeparatorChar + myName;
    }
    
    /// <summary>
    /// Builder implements thread-safe access to the tree node.
    /// Builder operations should not be used simultaneously with <see cref="PathTreeNode"/> methods,
    /// because protection between them is not implemented (for simplicity)
    /// </summary>
    public readonly struct Builder
    {
      /// <summary>
      /// Creates builder for existed node. Should never be used directly
      /// </summary>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
      internal static Builder CreateUnsafe(PathTreeNode node)
      {
        return new Builder(node);
      }
      
      // ============
      
      private readonly PathTreeNode myNode;

      private Builder(PathTreeNode node)
      {
        myNode = node;
      }
      
      public Builder GetOrInsert(string part)
      {
        lock (myNode.myLock)
        {
          if (myNode.myChildren == null)
            myNode.myChildren = new Dictionary<string, PathTreeNode>();
          else if (myNode.myChildren.TryGetValue(part, out var value))
            return new Builder(value);
          var child = new PathTreeNode(part, myNode);
          myNode.myChildren.Add(part, child);
          return new Builder(child);
        }
      }
      public Builder GetOrInsert(ReadOnlySpan<char> part)
      {
        lock (myNode.myLock)
        {
          if (myNode.myChildren == null)
            myNode.myChildren = new Dictionary<string, PathTreeNode>();
          else if (myNode.myChildren.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(part, out var value))
            return new Builder(value);

          var name = new string(part);
          var child = new PathTreeNode(name, myNode);
          myNode.myChildren.Add(name, child);
          return new Builder(child);
        }
      }
      
      public void AddFile(string file)
      {
        lock (myNode.myLock)
        {
          myNode.myFiles ??= new List<string>();
          myNode.myFiles.Add(file);
        }
      }
      
      public Builder AddPathRecursive(ReadOnlySpan<char> path)
      {
        var curNode = this;
        foreach (var partRange in path.GetPathComponents())
          curNode = curNode.GetOrInsert(path[partRange]);
        
        return curNode;
      }
    }
  }
}