using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System;
using IRIS.Core.DTOs;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using IRIS.UI.Views.Dialogs;
using System.Windows;

namespace IRIS.UI.ViewModels
{
    public class SelectablePC : INotifyPropertyChanged
    {
        private bool _isSelected;
        public int Id { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public string? Hostname { get; set; }
        public string? Room { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class LabsViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly IRoomService _roomService;
        private readonly IPCAdminService _pcAdminService;
        private readonly SemaphoreSlim _loadRoomsSemaphore = new(1, 1);
        private readonly SemaphoreSlim _loadUnassignedSemaphore = new(1, 1);
        private readonly RelayCommand _previousPageRelayCommand;
        private readonly RelayCommand _nextPageRelayCommand;
        private List<RoomDto> _allRooms = new();
        private List<RoomDto> _filteredRooms = new();
        private bool _isViewActive = true;
        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalPages = 1;
        private int _totalCount = 0;

        public ObservableCollection<RoomDto> Rooms { get; } = new();
        public ICollectionView RoomsView { get; }
        public ObservableCollection<SelectablePC> UnassignedPCs { get; } = new();
        public ObservableCollection<SelectablePC> AssignedPCs { get; } = new();
        public List<int> PageSizeOptions { get; } = new() { 10, 25, 50 };

        private string _searchText = string.Empty;
        private string _appliedSearchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
            }
        }

        private RoomDto? _selectedRoom;
        public RoomDto? SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                if (_selectedRoom?.Id == value?.Id)
                {
                    _selectedRoom = null;
                }
                else
                {
                    _selectedRoom = value;
                }
                OnPropertyChanged();
                PopulateFormFromSelection();
            }
        }

        // Add New Laboratory fields
        private string _addRoomNumber = string.Empty;
        public string AddRoomNumber
        {
            get => _addRoomNumber;
            set
            {
                _addRoomNumber = value;
                OnPropertyChanged();
                CreateCommand.RaiseCanExecuteChanged();
            }
        }

        private string? _addDescription;
        public string? AddDescription
        {
            get => _addDescription;
            set { _addDescription = value; OnPropertyChanged(); }
        }

        private string _addCapacityText = "0";
        public string AddCapacityText
        {
            get => _addCapacityText;
            set { _addCapacityText = value; OnPropertyChanged(); }
        }

        private bool _addIsActive = true;
        public bool AddIsActive
        {
            get => _addIsActive;
            set { _addIsActive = value; OnPropertyChanged(); }
        }

        // Manage Laboratory fields
        private string _roomNumber = string.Empty;
        public string RoomNumber
        {
            get => _roomNumber;
            set { _roomNumber = value; OnPropertyChanged(); }
        }

        private string? _description;
        public string? Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        private string _capacityText = "0";
        public string CapacityText
        {
            get => _capacityText;
            set { _capacityText = value; OnPropertyChanged(); }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private bool _isStatusError;
        public bool IsStatusError
        {
            get => _isStatusError;
            set { _isStatusError = value; OnPropertyChanged(); }
        }

        private bool _isAddModalOpen;
        public bool IsAddModalOpen
        {
            get => _isAddModalOpen;
            set { _isAddModalOpen = value; OnPropertyChanged(); }
        }

        private bool _isEditModalOpen;
        public bool IsEditModalOpen
        {
            get => _isEditModalOpen;
            set { _isEditModalOpen = value; OnPropertyChanged(); }
        }

        private bool _isAssignedPCsModalOpen;
        public bool IsAssignedPCsModalOpen
        {
            get => _isAssignedPCsModalOpen;
            set { _isAssignedPCsModalOpen = value; OnPropertyChanged(); }
        }

        private int _selectedPCsTabIndex;
        public int SelectedPCsTabIndex
        {
            get => _selectedPCsTabIndex;
            set { _selectedPCsTabIndex = value; OnPropertyChanged(); }
        }

        private int _modalRoomId;
        public int ModalRoomId
        {
            get => _modalRoomId;
            set { _modalRoomId = value; OnPropertyChanged(); }
        }

        private string _modalRoomNumber = string.Empty;
        public string ModalRoomNumber
        {
            get => _modalRoomNumber;
            set { _modalRoomNumber = value; OnPropertyChanged(); }
        }

        public int PageSize
        {
            get => _pageSize;
            set { _pageSize = value; OnPropertyChanged(); CurrentPage = 1; _ = LoadRoomsAsync(); }
        }

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageInfo));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
                _previousPageRelayCommand.RaiseCanExecuteChanged();
                _nextPageRelayCommand.RaiseCanExecuteChanged();
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            set
            {
                _totalPages = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageInfo));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
                _previousPageRelayCommand.RaiseCanExecuteChanged();
                _nextPageRelayCommand.RaiseCanExecuteChanged();
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            set { _totalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageInfo)); }
        }

        public string PageInfo => $"Page {CurrentPage} of {TotalPages} ({TotalCount} total entries)";
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public RelayCommand OpenAddModalCommand { get; }
        public RelayCommand CloseAddModalCommand { get; }
        public RelayCommand OpenEditModalCommand { get; }
        public RelayCommand CloseEditModalCommand { get; }
        public RelayCommand CreateCommand { get; }
        public RelayCommand UpdateCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand AssignCommand { get; }
        public RelayCommand AssignPCsToModalRoomCommand { get; }
        public RelayCommand UnassignCommand { get; }
        public RelayCommand ViewAssignedPCsCommand { get; }
        public RelayCommand PreviousPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand ApplyFiltersCommand { get; }
        public RelayCommand ResetFiltersCommand { get; }

        public LabsViewModel(IRoomService roomService, IPCAdminService pcAdminService)
        {
            _roomService = roomService;
            _pcAdminService = pcAdminService;

            RoomsView = CollectionViewSource.GetDefaultView(Rooms);

            _previousPageRelayCommand = new RelayCommand(async () => await PreviousPageAsync(), () => HasPreviousPage);
            _nextPageRelayCommand = new RelayCommand(async () => await NextPageAsync(), () => HasNextPage);
            PreviousPageCommand = _previousPageRelayCommand;
            NextPageCommand = _nextPageRelayCommand;

            ApplyFiltersCommand = new RelayCommand(() => { ApplyRoomFilter(); return Task.CompletedTask; }, () => true);
            ResetFiltersCommand = new RelayCommand(async () =>
            {
                SearchText = string.Empty;
                _appliedSearchText = string.Empty;
                CurrentPage = 1;
                await LoadRoomsAsync();
            }, () => true);

            OpenAddModalCommand = new RelayCommand(() => { IsAddModalOpen = true; return Task.CompletedTask; }, () => true);
            CloseAddModalCommand = new RelayCommand(() => { IsAddModalOpen = false; return Task.CompletedTask; }, () => true);
            OpenEditModalCommand = new RelayCommand((param) => { OpenEditModal(param); return Task.CompletedTask; }, () => true);
            CloseEditModalCommand = new RelayCommand(() => { IsEditModalOpen = false; return Task.CompletedTask; }, () => true);
            CreateCommand = new RelayCommand(async () => await CreateAsync(), () => !string.IsNullOrWhiteSpace(AddRoomNumber));
            UpdateCommand = new RelayCommand(async () => await UpdateAsync(), () => SelectedRoom != null);
            DeleteCommand = new RelayCommand(async (param) => await DeleteAsync(param), () => true);
            AssignCommand = new RelayCommand(async () => await AssignAsync(), () => true);
            AssignPCsToModalRoomCommand = new RelayCommand(async () => await AssignPCsToModalRoomAsync(), () => true);
            UnassignCommand = new RelayCommand(async () => await UnassignAsync(), () => true);
            ViewAssignedPCsCommand = new RelayCommand(async (param) => await ViewAssignedPCsAsync(param), () => true);

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadRoomsAsync();
            await LoadUnassignedAsync();
        }

        private async Task LoadRoomsAsync(int? selectedRoomId = null)
        {
            if (!_isViewActive)
            {
                return;
            }

            await _loadRoomsSemaphore.WaitAsync();

            try
            {
                if (!_isViewActive)
                {
                    return;
                }

                var rooms = await _roomService.GetRoomsAsync();
                _allRooms = rooms.Where(r => r.RoomNumber != "DEFAULT").OrderBy(r => r.RoomNumber).ToList();

                RebuildPagedRooms(selectedRoomId ?? SelectedRoom?.Id);
            }
            finally
            {
                _loadRoomsSemaphore.Release();
            }
        }

        private void ApplyRoomFilter()
        {
            _appliedSearchText = SearchText?.Trim() ?? string.Empty;
            CurrentPage = 1;
            RebuildPagedRooms(SelectedRoom?.Id);
        }

        private void RebuildPagedRooms(int? selectedRoomId = null)
        {
            var query = _allRooms.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_appliedSearchText))
            {
                query = query.Where(r => r.RoomNumber.Contains(_appliedSearchText, StringComparison.OrdinalIgnoreCase));
            }

            _filteredRooms = query.OrderBy(r => r.RoomNumber).ToList();

            TotalCount = _filteredRooms.Count;
            TotalPages = TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);

            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }
            if (CurrentPage < 1)
            {
                CurrentPage = 1;
            }

            Rooms.Clear();
            var skip = (CurrentPage - 1) * PageSize;
            foreach (var room in _filteredRooms.Skip(skip).Take(PageSize))
            {
                Rooms.Add(room);
            }

            if (selectedRoomId.HasValue)
            {
                var match = Rooms.FirstOrDefault(r => r.Id == selectedRoomId.Value) ?? _filteredRooms.FirstOrDefault(r => r.Id == selectedRoomId.Value);
                if (match != null)
                {
                    SelectedRoom = match;
                }
            }

            RoomsView.Refresh();
        }

        private async Task PreviousPageAsync()
        {
            if (HasPreviousPage)
            {
                CurrentPage--;
                RebuildPagedRooms(SelectedRoom?.Id);
            }
        }

        private async Task NextPageAsync()
        {
            if (HasNextPage)
            {
                CurrentPage++;
                RebuildPagedRooms(SelectedRoom?.Id);
            }
        }

        private async Task LoadUnassignedAsync()
        {
            if (!_isViewActive)
            {
                return;
            }

            await _loadUnassignedSemaphore.WaitAsync();

            try
            {
                if (!_isViewActive)
                {
                    return;
                }

                var pcs = await _pcAdminService.GetUnassignedPCsAsync();
                UnassignedPCs.Clear();
                foreach (var pc in pcs)
                {
                    UnassignedPCs.Add(new SelectablePC
                    {
                        Id = pc.Id,
                        MacAddress = pc.MacAddress,
                        Hostname = pc.Hostname,
                        Room = pc.RoomId.ToString(),
                        IsSelected = false
                    });
                }
            }
            finally
            {
                _loadUnassignedSemaphore.Release();
            }
        }

        private void OpenEditModal(object? param)
        {
            if (param is RoomDto room)
            {
                SelectedRoom = room;
                IsEditModalOpen = true;
            }
        }

        private void PopulateFormFromSelection()
        {
            if (SelectedRoom == null)
            {
                RoomNumber = string.Empty;
                Description = string.Empty;
                CapacityText = "0";
                IsActive = true;
                AssignedPCs.Clear();
            }
            else
            {
                RoomNumber = SelectedRoom.RoomNumber;
                Description = SelectedRoom.Description;
                CapacityText = SelectedRoom.Capacity.ToString();
                IsActive = SelectedRoom.IsActive;
            }
            UpdateCommand.RaiseCanExecuteChanged();
            DeleteCommand.RaiseCanExecuteChanged();
            AssignCommand.RaiseCanExecuteChanged();
        }

        public async Task LoadAssignedPCsForRoomAsync(int roomId, ItemsControl itemsControl)
        {
            var pcs = await _pcAdminService.GetPCsByRoomAsync(roomId);
            var selectablePCs = pcs.Select(pc => new SelectablePC
            {
                Id = pc.Id,
                MacAddress = pc.MacAddress,
                Hostname = pc.Hostname,
                Room = pc.RoomId.ToString(),
                IsSelected = false
            }).ToList();
            itemsControl.ItemsSource = selectablePCs;
        }

        private async Task LoadAssignedPCsAsync()
        {
            if (SelectedRoom == null) return;

            var pcs = await _pcAdminService.GetPCsByRoomAsync(SelectedRoom.Id);
            AssignedPCs.Clear();
            foreach (var pc in pcs)
            {
                AssignedPCs.Add(new SelectablePC
                {
                    Id = pc.Id,
                    MacAddress = pc.MacAddress,
                    Hostname = pc.Hostname,
                    Room = pc.RoomId.ToString(),
                    IsSelected = false
                });
            }
        }

        private async Task ViewAssignedPCsAsync(object? param)
        {
            if (param is not int roomId) return;

            ModalRoomId = roomId;
            var room = Rooms.FirstOrDefault(r => r.Id == roomId);
            if (room != null)
                ModalRoomNumber = room.RoomNumber;

            var pcs = await _pcAdminService.GetPCsByRoomAsync(roomId);
            AssignedPCs.Clear();
            foreach (var pc in pcs)
            {
                AssignedPCs.Add(new SelectablePC
                {
                    Id = pc.Id,
                    MacAddress = pc.MacAddress,
                    Hostname = pc.Hostname,
                    Room = pc.RoomId.ToString(),
                    IsSelected = false
                });
            }

            await LoadUnassignedAsync();
            SelectedPCsTabIndex = 0;
            IsAssignedPCsModalOpen = true;
        }

        private async Task CreateAsync()
        {
            if (!TryBuildRequest(AddRoomNumber, AddDescription, AddCapacityText, AddIsActive, out var request, out var validationError))
            {
                SetStatus(validationError, true);
                return;
            }

            var dialog = new ConfirmationDialog(
                "Create Laboratory",
                $"Are you sure you want to create laboratory '{AddRoomNumber}'?",
                "Add24");
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var created = await _roomService.CreateRoomAsync(request);
                AddRoomNumber = string.Empty;
                AddDescription = string.Empty;
                AddCapacityText = "0";
                AddIsActive = true;
                IsAddModalOpen = false;
                CurrentPage = 1;
                await LoadRoomsAsync(created.Id);
                SetStatus($"Room {created.RoomNumber} created.", false);
                ShowSuccessDialog("Laboratory Created", $"Laboratory '{created.RoomNumber}' was created successfully.");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private async Task UpdateAsync()
        {
            if (SelectedRoom == null) return;

            if (!TryBuildRequest(RoomNumber, Description, CapacityText, IsActive, out var request, out var validationError))
            {
                SetStatus(validationError, true);
                return;
            }

            var dialog = new ConfirmationDialog(
                "Update Laboratory",
                $"Are you sure you want to update laboratory '{SelectedRoom.RoomNumber}'?",
                "Edit24");
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var updated = await _roomService.UpdateRoomAsync(SelectedRoom.Id, request);
                if (updated != null)
                {
                    IsEditModalOpen = false;
                    CurrentPage = 1;
                    await LoadRoomsAsync(updated.Id);
                    SetStatus($"Room {updated.RoomNumber} updated.", false);
                    ShowSuccessDialog("Laboratory Updated", $"Laboratory '{updated.RoomNumber}' was updated successfully.");
                }
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private async Task DeleteAsync(object? param)
        {
            if (param is not RoomDto room) return;
            if (room.RoomNumber == "DEFAULT") return;

            var dialog = new ConfirmationDialog(
                "Delete Laboratory",
                $"Are you sure you want to delete laboratory '{room.RoomNumber}'?\n\nAll PCs will be unassigned and policies will be removed. This action cannot be undone.",
                "Delete24");
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var deleted = await _roomService.DeleteRoomAsync(room.Id);
                if (deleted)
                {
                    if (SelectedRoom?.Id == room.Id)
                    {
                        SelectedRoom = null;
                        IsEditModalOpen = false;
                    }
                    CurrentPage = 1;
                    await LoadRoomsAsync();
                    await LoadUnassignedAsync();
                    SetStatus("Room deleted.", false);
                    ShowSuccessDialog("Laboratory Deleted", $"Laboratory '{room.RoomNumber}' was deleted successfully.");
                }
                else
                {
                    SetStatus("Could not delete room.", true);
                }
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private async Task AssignPCsToModalRoomAsync()
        {
            if (ModalRoomId == 0) return;
            var selectedIds = UnassignedPCs.Where(pc => pc.IsSelected).Select(pc => pc.Id).ToList();
            if (!selectedIds.Any()) return;

            var dialog = new ConfirmationDialog(
                "Assign PCs",
                $"Are you sure you want to assign {selectedIds.Count} PC(s) to laboratory '{ModalRoomNumber}'?",
                "Laptop24");
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var success = await _pcAdminService.AssignPCsToRoomAsync(selectedIds, ModalRoomId);
                if (success)
                {
                    await LoadUnassignedAsync();
                    await ViewAssignedPCsAsync(ModalRoomId);
                    SetStatus($"Assigned {selectedIds.Count} PC(s) to room {ModalRoomNumber}.", false);
                    ShowSuccessDialog("PCs Assigned", $"Assigned {selectedIds.Count} PC(s) to laboratory '{ModalRoomNumber}'.");
                }
                else
                {
                    SetStatus("No PCs were assigned.", true);
                }
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private async Task AssignAsync()
        {
            if (SelectedRoom == null) return;
            var selectedIds = UnassignedPCs.Where(pc => pc.IsSelected).Select(pc => pc.Id).ToList();
            if (!selectedIds.Any()) return;

            var dialog = new ConfirmationDialog(
                "Assign PCs",
                $"Are you sure you want to assign {selectedIds.Count} PC(s) to laboratory '{SelectedRoom.RoomNumber}'?",
                "Laptop24");
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var success = await _pcAdminService.AssignPCsToRoomAsync(selectedIds, SelectedRoom.Id);
                if (success)
                {
                    await LoadUnassignedAsync();
                    SetStatus($"Assigned {selectedIds.Count} PC(s) to room {SelectedRoom.RoomNumber}.", false);
                    ShowSuccessDialog("PCs Assigned", $"Assigned {selectedIds.Count} PC(s) to laboratory '{SelectedRoom.RoomNumber}'.");
                }
                else
                {
                    SetStatus("No PCs were assigned.", true);
                }
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private async Task UnassignAsync()
        {
            var selectedIds = AssignedPCs.Where(pc => pc.IsSelected).Select(pc => pc.Id).ToList();
            if (!selectedIds.Any()) return;

            var dialog = new ConfirmationDialog(
                "Unassign PCs",
                $"Are you sure you want to unassign {selectedIds.Count} PC(s) from this laboratory?\n\nThey will be moved to the DEFAULT room.",
                "Laptop24");
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var success = await _pcAdminService.UnassignPCsAsync(selectedIds);
                if (success)
                {
                    await LoadUnassignedAsync();
                    if (ModalRoomId != 0)
                    {
                        await ViewAssignedPCsAsync(ModalRoomId);
                    }
                    else
                    {
                        AssignedPCs.Clear();
                    }
                    SetStatus($"Unassigned {selectedIds.Count} PC(s) from room.", false);
                    ShowSuccessDialog("PCs Unassigned", $"Unassigned {selectedIds.Count} PC(s) from the laboratory.");
                }
                else
                {
                    SetStatus("No PCs were unassigned.", true);
                }
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private bool TryBuildRequest(string roomNumber, string? description, string capacityText, bool isActive, out RoomCreateUpdateDto request, out string error)
        {
            request = new RoomCreateUpdateDto(string.Empty, null, 0, true);
            error = string.Empty;

            var normalizedRoomNumber = roomNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedRoomNumber))
            {
                error = "Room number is required.";
                return false;
            }

            if (!int.TryParse(capacityText, out var parsedCapacity) || parsedCapacity < 0)
            {
                error = "Capacity must be a valid non-negative number.";
                return false;
            }

            request = new RoomCreateUpdateDto(normalizedRoomNumber, description?.Trim(), parsedCapacity, isActive);
            return true;
        }

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message;
            IsStatusError = isError;
        }

        private static void ShowSuccessDialog(string title, string message)
        {
            var successDialog = new ConfirmationDialog(
                title,
                message,
                "Checkmark24",
                "OK",
                "Cancel",
                false);
            successDialog.Owner = Application.Current.MainWindow;
            successDialog.ShowDialog();
        }

        public void OnNavigatedTo()
        {
            _isViewActive = true;
            _ = ReloadPageOnNavigateAsync();
        }

        private async Task ReloadPageOnNavigateAsync()
        {
            CurrentPage = 1;
            await LoadRoomsAsync();
            await LoadUnassignedAsync();
        }

        public void OnNavigatedFrom()
        {
            _isViewActive = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
