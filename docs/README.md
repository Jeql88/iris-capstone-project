# IRIS: Integrated Remote Infrastructure System

A Windows-Based Client-Server Network Management System for SAFAD's ACC Laboratories

---

## 📋 Project Overview

**IRIS** (Integrated Remote Infrastructure System) is a LAN-based Network Management System (NMS) designed specifically for the Architecture Computer Center (ACC) of the School of Architecture, Fine Arts, and Design (SAFAD) at the University of San Carlos.

### Key Features

- **Hardware & Network Monitoring** - Real-time tracking of CPU, GPU, RAM, temperature, and bandwidth
- **Laboratory Management** - Live screen monitoring, remote control, and session management
- **Software Management** - Centralized deployment, removal, and inventory tracking
- **Policy Enforcement** - Automated rules for system behavior and compliance
- **Role-Based Access Control** - Secure access for Admins, IT Personnel, and Faculty

---

## 🎯 Project Objectives

### General Objective
Design and implement IRIS as a LAN-based Network Management System for ACC's computer laboratories.

### Specific Objectives
1. Analyze and document ACC's existing infrastructure and administrative workflows
2. Design and implement a Windows-based client-server NMS with LAN-based functionalities
3. Perform systematic testing to evaluate functionality, performance, and user experience
4. Deploy IRIS in ACC's laboratory rooms with complete technical documentation

---

## 👥 Development Team

**Capstone Project Team (AY 2024-2025)**

- **Jansen Kai Xuan Choi** - Product Owner & Developer
- **Jeskha Samantha B. Derama** - Developer (Frontend)
- **Jose Rafael Achilles L. Delgado** - Developer (Frontend)
- **Josh Edward Q. Lui** - Scrum Master & Developer (Backend)

**Faculty Adviser:** Godwin S. Monserate, PhD

---

## 🏗️ System Architecture

### Technology Stack

| Component | Technology |
|-----------|-----------|
| **Programming Language** | C# with .NET Framework |
| **UI Framework** | WPF (Windows Presentation Foundation) |
| **Database** | PostgreSQL |
| **ORM** | Entity Framework Core |
| **System Monitoring** | WMI, PerformanceCounter, LibreHardwareMonitor |
| **Communication** | TCP Sockets |
| **Version Control** | GitHub |

### Architecture Pattern
- **Client-Server Architecture** with lightweight agents on lab PCs
- **Bottom-Up Development Approach** for modular component building
- **LAN-Only Operation** - No cloud dependencies

---

## 📦 System Modules

### 1. Role-Based Management
- User authentication and authorization
- Three user roles: System Administrator, IT Personnel, Faculty
- Secure session management

### 2. Hardware and Network Monitoring
- Real-time system resource tracking (CPU, GPU, RAM, Disk, Temperature)
- Bandwidth usage monitoring per device
- Network behavior alerts for anomalies
- Device status dashboard

### 3. Laboratory Management
- Live screen monitoring (CCTV-style tiled view)
- Remote control actions (lock, restart, shutdown)
- User activity and input logging
- Room-based interface organization

### 4. Software Management
- Remote software installation across multiple PCs
- Application uninstallation
- Software inventory viewer and auditing
- Faculty software request system

### 5. Policy Enforcement
- Automated policy scripts (wallpaper reset, access control)
- UI/usage restrictions
- Scheduled actions (auto-shutdown after inactivity)

### 6. Usage Metrics
- Most frequently opened applications tracking
- Most visited websites monitoring
- Behavioral analytics for lab supervision

---

## 🚀 Getting Started

### Prerequisites

- Windows 10 or Windows 11
- PostgreSQL 14 or higher
- .NET 9.0 SDK
- Visual Studio 2022 (recommended)

### Installation

1. **Clone the Repository**
   ```bash
   git clone https://github.com/your-repo/IRIS.git
   cd IRIS
   ```

2. **Database Setup**
   
   Create the PostgreSQL database:
   ```bash
   # Using pgAdmin or psql
   CREATE DATABASE iris_db TEMPLATE template0;
   ```

   If you encounter collation issues, run:
   ```sql
   \c template1
   ALTER DATABASE template1 REFRESH COLLATION VERSION;
   \c postgres
   CREATE DATABASE iris_db TEMPLATE template0;
   ```

3. **Configure Connection String**
   
   Update `IRIS.UI/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "IRISDatabase": "Host=localhost;Port=5432;Database=iris_db;Username=postgres;Password=your_password"
     }
   }
   ```

4. **Apply Database Migrations**
   ```bash
   cd IRIS.UI
   dotnet ef database update --project ..\IRIS.Core
   ```

5. **Build and Run**
   ```bash
   dotnet build
   dotnet run --project IRIS.UI
   ```

### Default Login Credentials

After migration, use these credentials to log in:

| Username | Password | Role |
|----------|----------|------|
| admin | admin | System Administrator |
| itperson | admin | IT Personnel |
| faculty | admin | Faculty |

⚠️ **Important:** Change default passwords immediately after first login!

---

## 🔐 Security Features

### Password Security
- **BCrypt Hashing** with automatic salting (work factor: 11)
- Secure password storage resistant to rainbow table attacks
- Input validation to prevent SQL injection
- Role-based access control (RBAC)

### Network Security
- LAN-only operation (no external internet dependency)
- Encrypted communication between agents and server
- Session-based authentication
- Comprehensive audit logging

---

## 📊 System Requirements

### Server Requirements
- **OS:** Windows Server 2019+ or Windows 10/11
- **CPU:** 4+ cores recommended
- **RAM:** 8GB minimum, 16GB recommended
- **Storage:** 50GB+ for logs and database
- **Network:** Gigabit Ethernet

### Client Requirements (Lab PCs)
- **OS:** Windows 10 or Windows 11
- **RAM:** 4GB minimum
- **Network:** Connected to ACC LAN
- **Agent:** IRIS monitoring agent installed

### Network Infrastructure
- **Topology:** LAN-based with managed switches
- **Bandwidth:** 100 Mbps minimum
- **Devices:** Up to 80 endpoints (4 labs × 20 PCs)

---

## 🧪 Testing

### Testing Methodology
- **Unit Testing** - Individual module validation
- **Black Box Testing** - User-driven testing (5-10 users)
- **Stress Testing** - 60-80 concurrent devices
- **User Acceptance Testing (UAT)** - ISO/IEC 25010 aligned

### Performance Metrics
- Screen polling interval: 5 seconds (adjustable 3-10s)
- UI refresh frequency: 2 seconds
- Maximum tile count: 20 active PC feeds per dashboard
- Alert thresholds: CPU >85%, RAM >90%, GPU >90%

---

## 📖 Documentation

### Available Documentation
- [Software Requirements Specification (SRS)](docs/SRS.md)
- [Password Migration Guide](PASSWORD_MIGRATION_GUIDE.md)
- [Database Setup Guide](CREATE_DATABASE.md)
- [User Acceptance Testing Survey](docs/UAT_SURVEY.md)
- [Work Breakdown Structure](docs/WBS.md)

### User Guides
- System Administrator Manual
- IT Personnel Manual
- Faculty User Guide

---

## 🛠️ Development

### Development Model
- **Methodology:** Agile (Scrum Framework)
- **Sprint Duration:** 2 weeks
- **Development Approach:** Bottom-Up

### Sprint Schedule (AY 2025-2026, Second Semester)

| Sprint | Module | Duration |
|--------|--------|----------|
| Sprint 1 | Role-Based Management | Weeks 1-2 |
| Sprint 2 | Hardware & Network Monitoring | Weeks 3-5 |
| Sprint 3 | Laboratory Management & Usage Metrics | Weeks 6-8 |
| Sprint 4 | Software & Policy Management | Weeks 9-11 |
| Post-Sprint | UAT, Deployment, Training | Weeks 11-14 |

---

## 🎓 Academic Context

### Institution
**University of San Carlos**  
Department of Computer, Information Sciences, and Mathematics  
Bachelor of Science in Information Technology

### Capstone Project
Presented in partial fulfillment of the requirements for the degree of Bachelor of Science in Information Technology, August 2025.

### Target Deployment
**School of Architecture, Fine Arts, and Design (SAFAD)**  
Architecture Computer Center (ACC)  
4 Computer Laboratories × 20 PCs = 80 Total Endpoints

---

## 📈 Expected Impact

### Operational Improvements
- **70% reduction** in hardware issue detection time
- **50% reduction** in downtime-related disruptions
- **5-8 staff hours saved** per week in manual interventions

### Benefits by Stakeholder

**For ACC:**
- Enhanced laboratory supervision and resource optimization
- Improved instructional delivery through real-time monitoring

**For System Administrators:**
- Centralized dashboard for all network functions
- Faster response times and reduced downtime

**For Faculty:**
- Real-time screen monitoring during classes and exams
- Tools to maintain academic integrity

**For Students:**
- More secure and structured laboratory environment
- Fair and technically stable learning space

---

## 🔄 Future Enhancements

### Potential Improvements
- Multi-platform support (macOS, Linux)
- Cloud integration for remote access
- AI-driven anomaly detection
- Mobile app for faculty monitoring
- Integration with university-wide systems

---

## 📝 License

This project is developed as an academic capstone project for the University of San Carlos. All rights reserved by the development team and the university.

---

## 📞 Contact

For questions, issues, or contributions, please contact:

- **Jansen Choi** - jansenchoikx@gmail.com
- **Jeskha Derama** - jeskhasamanthaderama@gmail.com
- **Rafael Delgado** - jee.jld6264@gmail.com
- **Josh Lui** - joshedlui4@gmail.com

**Faculty Adviser:** Godwin S. Monserate, PhD

---

## 🙏 Acknowledgments

Special thanks to:
- **Sir Joseph Viesca** - ACC System Administrator, for insights and requirements gathering
- **SAFAD Faculty and Staff** - For their support and feedback
- **University of San Carlos** - For providing the academic framework and resources

---

## 📚 References

For a complete list of references and related literature, please refer to the [Bibliography](docs/BIBLIOGRAPHY.md) section of the full documentation.

---

**Last Updated:** July 2025  
**Version:** 1.0.0  
**Status:** In Development (Sprint Phase)
