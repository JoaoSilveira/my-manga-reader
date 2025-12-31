using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MangaMan.Models;
using Microsoft.EntityFrameworkCore;

namespace MangaMan.ViewModels;

public partial class ArchiveViewModel : ViewModelBase
{
    public required Guid ArchiveId { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }

    [ObservableProperty] private bool _wasRead;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task OpenArchive()
    {
        await MainWindowVM.OpenArchive(ArchiveId, Path);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ToggleRead()
    {
        await using var ctx = new MangaManDbContext();
        await ctx.MangaArchives
            .Where(m => m.Id == ArchiveId)
            .ExecuteUpdateAsync(setter => setter.SetProperty(m => m.WasRead, !WasRead));
        await ctx.SaveChangesAsync();
        WasRead = !WasRead;
    }
    
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task EditArchive()
    {
        await MainWindowVM.EditArchive(ArchiveId);
    }
}