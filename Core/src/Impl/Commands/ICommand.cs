#nullable enable

using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal interface ICommand
  {
    Task<int> ExecuteAsync();
  }
}