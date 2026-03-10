using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using IRIS.Core.DTOs;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Helpers;
using IRIS.UI.Services;

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
        private bool _isViewActive = true;

        public ObservableCollection<RoomDto> Rooms { get; } = new();
        public ObservableCollection<SelectablePC> UnassignedPCs { get; } = new();

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

        public RelayCommand CreateCommand { get; }
        public RelayCommand UpdateCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand AssignCommand { get; }

        public LabsViewModel(IRoomService roomService, IPCAdminService pcAdminService)
        {
            _roomService = roomService;
            _pcAdminService = pcAdminService;

            CreateCommand = new RelayCommand(async () => await CreateAsync(), () => !string.IsNullOrWhiteSpace(AddRoomNumber));
            UpdateCommand = new RelayCommand(async () => await UpdateAsync(), () => SelectedRoom != null);
            DeleteCommand = new RelayCommand(async () => await DeleteAsync(), () => SelectedRoom != null);
            AssignCommand = new RelayCommand(async () => await AssignAsync(), () => true);

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

            if (!await _loadRoomsSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                if (!_isViewActive)
                {
                    return;
                }

            var rooms = await _roomService.GetRoomsAsync();
            Rooms.Clear();
            foreach (var room in rooms.Where(r => r.RoomNumber != "DEFAULT"))
                Rooms.Add(room);

            var targetId = selectedRoomId ?? SelectedRoom?.Id;
            if (targetId.HasValue)
            {
                SelectedRoom = Rooms.FirstOrDefault(r => r.Id == targetId.Value);
            }
            }
            finally
            {
                _loadRoomsSemaphore.Release();
            }
        }

        private async Task LoadUnassignedAsync()
        {
            if (!_isViewActive)
            {
                return;
            }

            if (!await _loadUnassignedSemaphore.WaitAsync(0))
            {
                return;
            }

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

        private void PopulateFormFromSelection()
        {
            if (SelectedRoom == null)
            {
                RoomNumber = string.Empty;
                Description = string.Empty;
                CapacityText = "0";
                IsActive = true;
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

        private async Task CreateAsync()
        {
            if (!TryBuildRequest(AddRoomNumber, AddDescription, AddCapacityText, AddIsActive, out var request, out var validationError))
            {
                SetStatus(validationError, true);
                return;
            }

            try
            {
                var created = await _roomService.CreateRoomAsync(request);
                AddRoomNumber = string.Empty;
                AddDescription = string.Empty;
                AddCapacityText = "0";
                AddIsActive = true;
                await LoadRoomsAsync(created.Id);
                SetStatus($"Room {created.RoomNumber} created.", false);
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

            try
            {
                var updated = await _roomService.UpdateRoomAsync(SelectedRoom.Id, request);
                if (updated != null)
                {
                    await LoadRoomsAsync(updated.Id);
                    SetStatus($"Room {updated.RoomNumber} updated.", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private async Task DeleteAsync()
        {
            if (SelectedRoom == null) return;
            if (SelectedRoom.RoomNumber == "DEFAULT") return;

            try
            {
                var deleted = await _roomService.DeleteRoomAsync(SelectedRoom.Id);
                if (deleted)
                {
                    SelectedRoom = null;
                    await LoadRoomsAsync();
                    await LoadUnassignedAsync();
                    SetStatus("Room deleted.", false);
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

        private async Task AssignAsync()
        {
            if (SelectedRoom == null) return;
            var selectedIds = UnassignedPCs.Where(pc => pc.IsSelected).Select(pc => pc.Id).ToList();
            if (!selectedIds.Any()) return;

            try
            {
                var success = await _pcAdminService.AssignPCsToRoomAsync(selectedIds, SelectedRoom.Id);
                if (success)
                {
                    await LoadUnassignedAsync();
                    SetStatus($"Assigned {selectedIds.Count} PC(s) to room {SelectedRoom.RoomNumber}.", false);
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

        public void OnNavigatedFrom()
        {
            _isViewActive = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
