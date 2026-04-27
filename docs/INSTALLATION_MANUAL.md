# IRIS — System Installation Manual

This section documents how to install IRIS across the three components that
make up the system. Hardware and software requirement tables are produced
separately by running `scripts/Get-IrisRequirementsData.ps1` on each role of
PC and consolidating the output (see §1.3).

The following are its subsections:

- 1.1 System Overview
- 1.2 Installation
  - 1.2.1 PostgreSQL Database Host (one-time)
  - 1.2.2 IRIS UI (Operator Dashboard)
  - 1.2.3 IRIS Agent (Lab PC)
- 1.3 Producing the Requirements Tables

---

## 1.1 System Overview

IRIS is a LAN-only network management system designed for the University of
San Carlos ACC computer laboratories. The deployment consists of three
components communicating over the lab LAN:

| Component | Where it runs | Role |
|---|---|---|
| **PostgreSQL database** | One server on the lab LAN | Persists PCs, metrics, alerts, usage history, audit logs |
| **IRIS UI** | Operator workstations (Admin / IT / Faculty) | Dashboard for monitoring, control, reporting |
| **IRIS Agent** | Every lab PC | Collects metrics, serves screen snapshots, executes remote commands |

Both the UI and Agent are shipped as MSI installers that bundle the .NET
runtime (self-contained publish), so target PCs require no separate .NET
install.

---

## 1.2 Installation

### 1.2.1 PostgreSQL Database Host (one-time)

1. Provision a server on the lab LAN with a static IP address.
2. Install **PostgreSQL 14 or later** (16 LTS recommended).
3. Open inbound TCP `5432` from the lab subnet. Block from anywhere else.
4. Create the IRIS role and database. From a `psql` session as a superuser:

   ```sql
   CREATE ROLE accadmin WITH LOGIN PASSWORD '<your password>';
   CREATE DATABASE iris_db OWNER accadmin;
   ```

5. Apply the schema. From a workstation with the IRIS source tree:

   ```bash
   ./scripts/setup-database-linux.sh --host <db-host-ip>
   ```

   Or, on Windows, run `docs/migrations.sql` against `iris_db` via `psql`.

6. Verify connectivity from a lab workstation:

   ```powershell
   Test-NetConnection -ComputerName <db-host-ip> -Port 5432
   ```

   `TcpTestSucceeded : True` confirms the host is reachable.

### 1.2.2 IRIS UI — Operator Dashboard

1. Copy `IRIS.UI.<version>.msi` to the operator workstation.
2. Open an elevated PowerShell prompt and run:

   ```powershell
   msiexec /i "IRIS.UI.<version>.msi" /qn /l*v C:\temp\iris-ui-install.log
   ```

   For an interactive install with the default UI, double-click the MSI and
   click through the prompts.

3. Verify install:

   ```powershell
   (Get-Item "C:\Program Files\IRIS\UI\IRIS.UI.exe").LastWriteTime
   Get-Package -Name "IRIS UI*"
   ```

4. Edit `C:\Program Files\IRIS\UI\appsettings.json` and confirm
   `ConnectionStrings:IRISDatabase` points at the correct DB host.
5. Launch IRIS from the Start Menu and log in. Default seeded credentials:

   - `admin / admin` (System Administrator)
   - `itperson / admin` (IT Personnel)
   - `faculty / admin` (Faculty)

### 1.2.3 IRIS Agent — Lab PC

The Agent must be deployed to every lab PC that will be monitored. For 80
PCs, deploy via Group Policy software installation, a scripted PSExec push,
or in-person admin sweep. The MSI supports silent install.

1. Copy `IRIS.Agent.<version>.msi` to each target lab PC (or push via GPO).
2. From an elevated prompt on the target PC:

   ```cmd
   msiexec /i IRIS.Agent.<version>.msi /qn /l*v C:\temp\iris-agent-install.log
   ```

3. Verify install:

   ```powershell
   (Get-Item "C:\Program Files\IRIS\Agent\IRIS.Agent.exe").LastWriteTime
   Get-Process IRIS.Agent | Select Id, StartTime, SessionId, Path
   ```

   Expect two processes: one in Session 0 (`--system-helper`, runs as
   SYSTEM) and one in the console session (`--background`, runs as the
   logged-in user).

4. The agent's first heartbeat lands in PostgreSQL within ~5 seconds, and
   the PC will appear in the operator's Monitor dashboard.

---

## 1.3 Producing the Requirements Tables

Run `scripts/Get-IrisRequirementsData.ps1` on each of:

- One representative **operator workstation** running IRIS UI
- One representative **lab PC** running IRIS Agent
- The **database server** (or, if the DB is on Linux, run the `psql`
  commands the script lists)

The script collects OS, CPU, RAM, disk, network, .NET, IRIS process memory,
log sizes, and listening ports — everything you need to fill the Operator
Workstation, Lab PC, and Database Server tables for the project report.

```powershell
# Run with the role parameter matching what the PC is
.\scripts\Get-IrisRequirementsData.ps1 -Role UI
.\scripts\Get-IrisRequirementsData.ps1 -Role Agent
.\scripts\Get-IrisRequirementsData.ps1 -Role DB -DbHost localhost -DbUser postgres -DbPassword postgres
```

Output is written to the console as a table and saved as a timestamped JSON
file (`iris-requirements-<role>-<hostname>-<timestamp>.json`) you can attach
to the report appendix.
