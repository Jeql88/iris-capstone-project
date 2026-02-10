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
            set { _roomNumber = value; OnPropertyChanged(); }
        }

        private string? _description;
        public string? Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        private int _capacity;
        public int Capacity
        {
            get => _capacity;
            set { _capacity = value; OnPropertyChanged(); }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
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

        private async Task LoadRoomsAsync()
        {
            var rooms = await _roomService.GetRoomsAsync();
            Rooms.Clear();
            foreach (var room in rooms)
                Rooms.Add(room);
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
                Capacity = 0;
                IsActive = true;
            }
            else
            {
                RoomNumber = SelectedRoom.RoomNumber;
                Description = SelectedRoom.Description;
                Capacity = SelectedRoom.Capacity;
                IsActive = SelectedRoom.IsActive;
            }
            UpdateCommand.RaiseCanExecuteChanged();
            DeleteCommand.RaiseCanExecuteChanged();
            AssignCommand.RaiseCanExecuteChanged();
        }

        private async Task CreateAsync()
        {
            var dto = new RoomCreateUpdateDto(RoomNumber, Description, Capacity, IsActive);
            var created = await _roomService.CreateRoomAsync(dto);
            Rooms.Add(created);
            SelectedRoom = created;
        }

        private async Task UpdateAsync()
        {
            if (SelectedRoom == null) return;
            var dto = new RoomCreateUpdateDto(RoomNumber, Description, Capacity, IsActive);
            var updated = await _roomService.UpdateRoomAsync(SelectedRoom.Id, dto);
            if (updated != null)
            {
                var index = Rooms.IndexOf(SelectedRoom);
                Rooms[index] = updated;
                SelectedRoom = updated;
            }
        }

        private async Task DeleteAsync()
        {
            if (SelectedRoom == null) return;
            if (SelectedRoom.RoomNumber == "DEFAULT") return; // safeguard
            var deleted = await _roomService.DeleteRoomAsync(SelectedRoom.Id);
            if (deleted)
            {
                Rooms.Remove(SelectedRoom);
                SelectedRoom = null;
            }
        }

        private async Task AssignAsync()
        {
            if (SelectedRoom == null) return;
            var selectedIds = UnassignedPCs.Where(pc => pc.IsSelected).Select(pc => pc.Id).ToList();
            if (!selectedIds.Any()) return;

            await _pcAdminService.AssignPCsToRoomAsync(selectedIds, SelectedRoom.Id);
            await LoadUnassignedAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
