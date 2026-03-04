# IRIS Project Guide for AI Assistants

## Project Identity

**IRIS** (Integrated Remote Infrastructure System) is a Windows-based, LAN-only Network Management System for the Architecture Computer Center (ACC) at the University of San Carlos, SAFAD department.

**Core Purpose:** Centralized monitoring, control, and management of 80 Windows lab PCs across 4 computer laboratories.

---

## Critical Context

### What IRIS Is
- A **client-server system** with lightweight agents on lab PCs reporting to a central dashboard
- **LAN-only** - no cloud, no internet dependency
- **Role-based** - 3 user types: System Administrator, IT Personnel, Faculty
- **Academic tool** - designed for classroom supervision, hardware monitoring, and lab management

### What IRIS Is NOT
- Not enterprise-grade NMS (no SNMP polling, no router provisioning)
- Not cross-platform (Windows 10/11 only)
- Not scalable to other departments without redesign
- Not cloud-based or internet-dependent

### Design Philosophy
- **Bottom-up development** - build and test modules independently before integration
- **Minimal code** - avoid verbose implementations
- **LAN-optimized** - low latency, high reliability within local network
- **Department-specific** - tailored to ACC's workflows, not generic

---

## Technology Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Language** | C# with .NET 9.0 | Backend services, agents, business logic |
| **UI** | WPF (XAML + C#) | Desktop dashboard for admins/faculty |
| **Database** | PostgreSQL | Logs, metrics, alerts, user data |
| **ORM** | Entity Framework Core | C# to PostgreSQL mapping |
| **Monitoring** | WMI, PerformanceCounter, LibreHardwareMonitor | CPU, GPU, RAM, temp, disk metrics |
| **Communication** | TCP Sockets | Agent-to-server data transmission |
| **Deployment** | PDQ Deploy | Remote software installation |

---

## System Architecture

### Client-Server Model
```
Lab PCs (80 units)
  ↓ (TCP Sockets)
IRIS Agent (per PC)
  ↓ (JSON packets)
IRIS Server (ACC)
  ↓
PostgreSQL Database
  ↓
WPF Dashboard (Admin/IT/Faculty)
```

### Network Topology
- 4 labs → 4 lab switches → Main switch → ACC router → ACC server
- No external internet required for IRIS operation
- All communication stays within ACC LAN

---

## Core Modules (Bottom-Up Priority)

### 1. Role-Based Management (Sprint 1, Weeks 1-2)
**Priority:** Build FIRST - foundation for all other modules

**Features:**
- User authentication (BCrypt hashing, work factor 11)
- 3 roles: System Administrator, IT Personnel, Faculty
- Session management
- User account CRUD (admin only)

**Key Files:**
- `IRIS.Core/Services/AuthenticationService.cs`
- `IRIS.Core/Data/IRISDbContext.cs` (seed data)

**Default Credentials:**
- admin/admin (System Administrator)
- itperson/admin (IT Personnel)
- faculty/admin (Faculty)

### 2. Hardware & Network Monitoring (Sprint 2, Weeks 3-5)
**Priority:** Build SECOND - provides data for other modules

**Features:**
- Real-time metrics: CPU, GPU, RAM, disk, temperature, bandwidth
- Alert thresholds: CPU >85%, RAM >90%, GPU >90% (sustained 3-5 min)
- Device status dashboard (online/offline)
- Historical logs for trend analysis

**Data Sources:**
- WMI: CPU load, disk stats, network throughput
- PerformanceCounter: High-frequency CPU, disk I/O, packet counts
- LibreHardwareMonitor: GPU temp, fan speeds, voltages

### 3. Laboratory Management & Usage Metrics (Sprint 3, Weeks 6-8)
**Priority:** Build THIRD - depends on auth and monitoring

**Features:**
- Live screen monitoring (CCTV-style tiled view, 20 tiles max)
- Remote actions: lock, restart, shutdown (NO automated shutdown)
- Screen polling: 5s default (adjustable 3-10s)
- UI refresh: 2s
- Activity logging: idle time, app switches, input patterns
- Usage metrics: most-used apps, most-visited websites

### 4. Software & Policy Management (Sprint 4, Weeks 9-11)
**Priority:** Build LAST - depends on all prior modules

**Features:**
- Remote software installation (via PDQ Deploy)
- Software inventory auditing
- App uninstallation
- Faculty software requests
- Policy scripts: wallpaper reset, access control, scheduled shutdown
- UI restrictions: prevent student customization

---

## Database Schema (Key Entities)

### Core Tables
- **User** - UserID, Username, PasswordHash, Role, CreatedAt
- **PC** - PCID, MACAddress, IPAddress, Status, RoomID
- **Room** - RoomID, RoomName, PolicyID
- **Policy** - PolicyID, WallpaperReset, AccessControl, AutoShutdown
- **Hardware_Metrics** - MetricID, PCID, CPU, GPU, RAM, Disk, Temp, Timestamp
- **Network_Metrics** - MetricID, PCID, Bandwidth, Latency, PacketLoss, Timestamp
- **Software_Installed** - InstallID, PCID, SoftwareID, InstalledAt, Status
- **Software_Usage_History** - UsageID, PCID, SoftwareID, StartTime, EndTime
- **Website_Usage_History** - VisitID, PCID, URL, VisitTime
- **Alerts** - AlertID, PCID, UserID, Severity, Message, AcknowledgedAt
- **User_Logs** - LogID, UserID, Action, Timestamp

---

## Development Guidelines

### Code Style
- **Minimal implementations** - only write code that directly solves the requirement
- **No verbose patterns** - avoid over-engineering
- **Batch file operations** - read/modify multiple files in one tool call when possible
- **Error handling** - always include try-catch for database/network operations
- **Input validation** - prevent SQL injection, validate user input

### Testing Approach
1. **Unit Testing** - test modules in isolation
2. **Black Box Testing** - 5-10 users, standard use cases
3. **Stress Testing** - 60-80 concurrent devices, 100 Mbps → 20 Mbps throttle
4. **UAT** - ISO/IEC 25010 aligned (Functionality, Usability, Performance, Reliability)

### Sprint Workflow (Scrum)
- Sprint planning → Development → In-sprint unit testing → Final testing → Sprint review → Retrospective
- 2-week sprints
- Daily stand-ups (15 min)
- Product backlog maintained by Product Owner (Jansen Choi)
- Scrum Master (Josh Lui) removes blockers

---

## Key Constraints & Assumptions

### Constraints
- **Windows-only** - no macOS, Linux, mobile support
- **LAN-only** - no cloud, no external APIs
- **80 endpoints max** - 4 labs × 20 PCs
- **Uniform hardware** - same specs per lab
- **No Deep Freeze** - IRIS handles software consistency directly

### Assumptions
- All PCs connected to ACC LAN
- IRIS agent persists across reboots
- IT notified when machines re-imaged
- Logs stored locally, used for academic supervision only
- Faculty may have elevated privileges (no Deep Freeze)

---

## Alert Thresholds

### Device-Level
| Metric | Normal | Alert Threshold | Duration |
|--------|--------|----------------|----------|
| CPU | 0-50% | >85% | 5+ min |
| GPU | <30% | >90% | 5+ min |
| RAM | <70% | >90% | 3+ min |
| Disk | <80% | >95% | Immediate |
| Disk I/O | <20ms | >50-100ms | Sustained |

### Network-Level
| Metric | Alert Threshold | Duration |
|--------|----------------|----------|
| Bandwidth | >80% capacity | 3+ min |
| Packet Loss | >2% | 2+ min |
| Latency | >250ms | 2+ min |

---

## User Roles & Permissions

### System Administrator (Full Access)
- All monitoring dashboards
- User management (create, edit, delete accounts)
- Policy configuration
- Software deployment
- Remote control (lock, restart, shutdown, remote desktop)
- Access logs, usage metrics
- Alert management

### IT Personnel (Operational Access)
- Hardware/network monitoring
- Software deployment
- Policy enforcement (view/edit)
- Remote control (lock, restart, shutdown, remote desktop)
- Usage metrics
- Limited to assigned labs

### Faculty (Supervision Access)
- Live screen monitoring (tiled view)
- Remote actions (lock, restart, shutdown - NO remote desktop)
- Usage metrics (view only)
- Software requests (submit only)
- Activity logs (view only)

---

## Common Tasks & Solutions

### Adding a New Module
1. Design database schema (ERD)
2. Create Entity Framework models
3. Implement service layer (business logic)
4. Build WPF UI (XAML + ViewModel)
5. Add role-based access checks
6. Write unit tests
7. Integrate with existing modules

### Debugging Agent Communication
- Check TCP socket connection (agent → server)
- Verify JSON serialization format
- Confirm firewall rules allow LAN traffic
- Review agent logs for errors
- Test with single PC before scaling

### Handling Database Migrations
```bash
# Create migration
dotnet ef migrations add MigrationName --project IRIS.Core

# Apply migration
dotnet ef database update --project IRIS.Core
```

### Fixing PostgreSQL Collation Issues
```sql
\c template1
ALTER DATABASE template1 REFRESH COLLATION VERSION;
\c postgres
CREATE DATABASE iris_db TEMPLATE template0;
```

---

## File Structure (Key Paths)

```
IRIS/
├── IRIS.Core/                    # Backend logic, services, data
│   ├── Data/
│   │   └── IRISDbContext.cs      # EF Core context, seed data
│   ├── Services/
│   │   └── AuthenticationService.cs  # Login, password hashing
│   └── Models/                   # Entity classes
├── IRIS.UI/                      # WPF frontend
│   ├── App.xaml.cs               # DI setup, startup
│   ├── appsettings.json          # Connection strings
│   └── Views/                    # XAML pages
├── IRIS.Agent/                   # Lightweight client agent
│   └── (Monitoring, data collection)
├── README.md                     # User-facing documentation
├── PASSWORD_MIGRATION_GUIDE.md   # BCrypt migration guide
└── AI_PROJECT_GUIDE.md           # This file
```

---

## Expected Impact (Success Metrics)

- **70% reduction** in hardware issue detection time
- **50% reduction** in downtime-related disruptions
- **5-8 staff hours saved** per week in manual interventions
- Real-time visibility into 80 lab PCs
- Centralized software management
- Enhanced academic integrity during exams

---

## Important Reminders

1. **Always use BCrypt** for password hashing (never SHA256, MD5, plaintext)
2. **LAN-only design** - do not add cloud dependencies
3. **Role-based access** - check user permissions before allowing actions
4. **Minimal code** - avoid over-engineering, keep it simple
5. **Bottom-up approach** - build Role-Based Management first, then monitoring, then lab management, then software/policy
6. **No automated shutdown in Lab Management** - only manual remote shutdown
7. **Windows-only** - do not attempt cross-platform support
8. **Test with 60-80 devices** - stress test at scale before deployment

---

## Contact & Support

**Development Team:**
- Jansen Choi (Product Owner) - jansenchoikx@gmail.com
- Jeskha Derama (Frontend) - jeskhasamanthaderama@gmail.com
- Rafael Delgado (Frontend) - jee.jld6264@gmail.com
- Josh Lui (Scrum Master, Backend) - joshedlui4@gmail.com

**Faculty Adviser:** Godwin S. Monserate, PhD

**Deployment Site:** ACC, SAFAD, University of San Carlos

---

## Quick Reference Commands

```bash
# Clone repo
git clone https://github.com/your-repo/IRIS.git

# Create database
CREATE DATABASE iris_db TEMPLATE template0;

# Apply migrations
dotnet ef database update --project IRIS.Core

# Build and run
dotnet build
dotnet run --project IRIS.UI

# Default login
Username: admin
Password: admin
```

---

**Last Updated:** July 2025  
**Version:** 1.0.0  
**Status:** In Development (Sprint Phase)
