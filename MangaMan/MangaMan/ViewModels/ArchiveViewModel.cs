using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace MangaMan.ViewModels;

public partial class ArchiveViewModel : ViewModelBase
{
    public required Guid ArchiveId { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task OpenArchive()
    {
        await MainWindowVM.OpenArchive(ArchiveId, Path);
    }
}