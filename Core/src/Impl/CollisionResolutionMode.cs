namespace JetBrains.SymbolStorage.Impl
{
  /// <summary>
  /// Collision resolution mode when 2 files fall into the same path, but are different
  /// </summary>
  public enum CollisionResolutionMode
  {
    /// <summary>
    /// Terminate processing and generate error
    /// </summary>
    Terminate,
    /// <summary>
    /// Keep already existed file
    /// </summary>
    KeepExisted,
    /// <summary>
    /// Overwrite file and save existed to the local storage as backup
    /// </summary>
    Overwrite,
    /// <summary>
    /// Overwrite file without backup creation
    /// </summary>
    OverwriteWithoutBackup
  }
}