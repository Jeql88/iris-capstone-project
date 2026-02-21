using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Linq;
using IRIS.Core.DTOs;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Helpers;

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

    public class LabsViewModel : INotifyPropertyChanged
    {
        private readonly IRoomService _roomService;
        private readonly IPCAdminService _pcAdminService;

        public ObservableCollection<RoomDto> Rooms { get; } = new();
        public ObservableCollection<SelectablePC> UnassignedPCs { get; } = new();

        private RoomDto? _selectedRoom;
        public RoomDto? SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                _selectedRoom = value;
                OnPropertyChanged();
                PopulateFormFromSelection();
            }
        }

        // Form fields
        private string _roomNumber = string.Empty;
        public string RoomNumber
        {
            get => _roomNumber;
            set
            {
                _roomNumber = value;
                OnPropertyChanged();
                CreateCommand.RaiseCanExecuteChanged();
            }
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

            CreateCommand = new RelayCommand(async () => await CreateAsync(), () => !string.IsNullOrWhiteSpace(RoomNumber));
            UpdateCommand = new RelayCommand(async () => await UpdateAsync(), () => SelectedRoom != null);
            DeleteCommand = new RelayCommand(async () => await DeleteAsync(), () => SelectedRoom != null);
            AssignCommand = new RelayCommand(async () => await AssignAsync(), () => true);

            _ = LoadRoomsAsync();
            _ = LoadUnassignedAsync();
        }

        private async Task LoadRoomsAsync(int? selectedRoomId = null)
        {
            var rooms = await _roomService.GetRoomsAsync();
            Rooms.Clear();
            foreach (var room in rooms)
                Rooms.Add(room);

            var targetId = selectedRoomId ?? SelectedRoom?.Id;
            if (targetId.HasValue)
            {
                SelectedRoom = Rooms.FirstOrDefault(r => r.Id == targetId.Value);
            }
        }

        private async Task LoadUnassignedAsync()
        {
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
            if (!TryBuildRequest(out var request, out var validationError))
            {
                SetStatus(validationError, true);
                return;
            }

            try
            {
                var created = await _roomService.CreateRoomAsync(request);
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

            if (!TryBuildRequest(out var request, out var validationError))
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
            if (SelectedRoom.RoomNumber == "DEFAULT") return; // safeguard

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

        private bool TryBuildRequest(out RoomCreateUpdateDto request, out string error)
        {
            request = new RoomCreateUpdateDto(string.Empty, null, 0, true);
            error = string.Empty;

            var normalizedRoomNumber = RoomNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedRoomNumber))
            {
                error = "Room number is required.";
                return false;
            }

            if (!int.TryParse(CapacityText, out var parsedCapacity) || parsedCapacity < 0)
            {
                error = "Capacity must be a valid non-negative number.";
                return false;
            }

            request = new RoomCreateUpdateDto(normalizedRoomNumber, Description?.Trim(), parsedCapacity, IsActive);
            return true;
        }

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message;
            IsStatusError = isError;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
