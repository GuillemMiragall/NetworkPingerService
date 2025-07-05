# Network Pinger Service

![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![Linux](https://img.shields.io/badge/Linux-FCC624?style=for-the-badge&logo=linux&logoColor=black)
![Windows](https://img.shields.io/badge/Windows-0078D4?style=for-the-badge&logo=windows&logoColor=white)

## 📝 Overview

The Network Pinger Service is a lightweight, cross-platform .NET application designed to continuously monitor the network connectivity of a configurable list of IP addresses or hostnames. It's built as a background service, making it ideal for deployment on dedicated monitoring machines or servers.

Its primary purpose is to help pinpoint transient network failures by: 
- Concurrently pinging multiple targets.
- Accurately logging failure events with precise timestamps.
- Sending debounced email notifications for network outages, including a list of affected devices and the relevant log file as an attachment.

This service is particularly useful for diagnosing intermittent network issues that are hard to catch with manual checks.

## ✨ Features

-   **Cross-Platform:** Runs on both Windows and Linux (and potentially macOS).
-   **Concurrent Monitoring:** Pings multiple targets simultaneously using dedicated threads for true independence.
-   **Accurate Failure Detection:** Logs the exact moment a device becomes unreachable, minimizing OS-level reporting delays.
-   **Configurable Targets:** Easily define monitored devices (IPs/hostnames) and their friendly names in `appsettings.json`.
-   **Debounced Email Notifications:**
    -   Sends a single email notification for a network outage after a configurable delay.
    -   Avoids email storms by ignoring subsequent failure events while an email is already scheduled.
    -   Retries email sending a configurable number of times if the SMTP server is unreachable.
    -   Includes a list of all devices that failed during the debouncing period in the email body.
    -   Attaches the current day's log file to the email for remote diagnostics.
-   **Low Footprint:** Designed as a .NET Worker Service for efficient, long-running background operation.
-   **File Logging:** All events are logged to a local file for historical analysis.

## 🚀 Getting Started

### Prerequisites

Before you begin, ensure you have the following installed:

-   [.NET 9.0 SDK](https://dotnet.microsoft.com/download) or newer.
-   [Git](https://git-scm.com/downloads) (for cloning the repository).

### Installation

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/GuillemMiragall/NetworkPingerService.git
    cd NetworkPingerService
    ```

2.  **Restore dependencies:**
    ```bash
    dotnet restore
    ```

3.  **Build the project:**
    ```bash
    dotnet build --configuration Release
    ```

### Configuration (`appsettings.json`)

Open the `appsettings.json` file in the project root. This file contains all the configurable settings for the service.

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "./logs/network_pinger_log.txt",
          "rollingInterval": "Day",
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  },
  "EmailSettings": {
    // SMTP server hostname (e.g., smtp.gmail.com, smtp.office365.com)
    "SmtpHost": "your.smtp.host.com",
    // SMTP server port (e.g., 587 for TLS/STARTTLS, 465 for SSL)
    "SmtpPort": 587,
    // Optional: SMTP Username. If not provided, the system will look for the NETWORKPINGER_SMTP_USERNAME environment variable.
    "SmtpUsername": "", 
    // Optional: SMTP Password. If not provided, the system will look for the NETWORKPINGER_SMTP_PASSWORD environment variable.
    "SmtpPassword": "",
    // The email address from which notifications will be sent
    "FromAddress": "sender@example.com",
    // The email address to which notifications will be sent
    "ToAddress": "recipient@example.com",
    // Delay in minutes before sending the first notification for an outage, and between subsequent notifications for ongoing outages.
    "NotificationDelayMinutes": 10,
    // Maximum number of times to retry sending an email if it fails. After this, retries stop until a new failure event.
    "MaxEmailRetries": 5,
    // Set to true to enable verbose logging for email services (e.g., scheduling, cancellation). Set to false for production.
    "EmailLoggingEnabled": false,
    // Set to true to enable email notifications. If false, email sending will be skipped.
    "EmailNotificationsEnabled": false
  },
  "PingSettings": {
    "PingIntervalMilliseconds": 2000,
    "PingTimeoutMilliseconds": 1000,
    "Targets": [
      {
        "Name": "Gateway",
        "Address": "192.168.11.1"
      },
      {
        "Name": "Main DNS",
        "Address": "8.8.8.8"
      },
      {
        "Name": "Backup DNS",
        "Address": "8.8.4.4"
      }
    ]
  }
}
```

**Key Configuration Points:**

-   **`EmailSettings`**: Fill in your SMTP server details, email addresses, and desired notification behavior. Note that `SmtpUsername` and `SmtpPassword` are optional in `appsettings.json` and will fall back to environment variables if not provided. `EmailNotificationsEnabled` must be set to `true` to activate email sending.
-   **`PingSettings`**: Customize the `PingIntervalMilliseconds`, `PingTimeoutMilliseconds`, and the `Targets` array with the devices you wish to monitor. Each target needs a `Name` (for easy identification in logs and emails) and an `Address` (IP or hostname).

## 🏃 How to Run

### For Development/Testing

Navigate to the project directory in your terminal and run:

```bash
dotnet run
```

The service will start and log output to your console and the configured log file. Press `Ctrl+C` to stop the service.

### For Production (Linux - Systemd Service)

For continuous, background operation on Linux, it's recommended to deploy the application as a systemd service.

1.  **Publish the application:**
    ```bash
    dotnet publish --configuration Release --runtime linux-x64 --self-contained true
    ```
    This command creates a self-contained deployment in the `publish` folder, meaning all necessary .NET runtime components are included, and it can run without a global .NET installation.

2.  **Copy published files to a deployment location:**
    Choose a suitable directory on your Linux machine, e.g., `/opt/networkpingerservice`.
    ```bash
    sudo mkdir -p /opt/networkpingerservice
    sudo cp -r publish/* /opt/networkpingerservice/
    ```

3.  **Create a systemd service file:**
    Create a file named `networkpingerservice.service` in `/etc/systemd/system/`:
    ```bash
    sudo nano /etc/systemd/system/networkpingerservice.service
    ```
    Add the following content:
    ```ini
    [Unit]
    Description=Network Pinger Service
    After=network.target

    [Service]
    User=your_linux_user_here  # Replace with a non-root user, e.g., 'networkpinger'
    WorkingDirectory=/opt/networkpingerservice
    ExecStart=/opt/networkpingerservice/NetworkPingerService # The executable name
    Restart=always
    RestartSec=10
    StandardOutput=journal
    StandardError=journal
    SyslogIdentifier=networkpingerservice

    [Install]
    WantedBy=multi-user.target
    ```
    **Important:** Replace `your_linux_user_here` with an actual non-root user on your system. It's best practice to run services under a dedicated, unprivileged user.

4.  **Reload systemd, enable, and start the service:**
    ```bash
    sudo systemctl daemon-reload
    sudo systemctl enable networkpingerservice.service
    sudo systemctl start networkpingerservice.service
    ```

5.  **Check service status and logs:**
    ```bash
    sudo systemctl status networkpingerservice.service
    sudo journalctl -u networkpingerservice.service -f
    ```

### For Production (Windows - NSSM Service)

For continuous, background operation on Windows, it's recommended to deploy the application as a Windows service using a service wrapper like [NSSM (Non-Sucking Service Manager)](https://nssm.cc/). NSSM allows you to run any executable as a Windows service, providing robust process management, logging, and automatic restarts.

1.  **Publish the application for Windows:**
    On a Windows machine (or using `dotnet publish` with the correct runtime identifier on Linux/macOS):
    ```bash
    dotnet publish --configuration Release --runtime win-x64 --self-contained true
    ```
    This command creates a self-contained deployment in the `publish-win` folder, meaning all necessary .NET runtime components are included, and it can run without a global .NET installation on the target Windows machine.

2.  **Copy published files to a deployment location:**
    Copy the entire contents of the `publish-win` folder to a suitable directory on your Windows machine, e.g., `C:\Program Files\NetworkPingerService`.

3.  **Download NSSM:**
    Download the latest stable release of NSSM from [https://nssm.cc/download](https://nssm.cc/download). Extract the contents of the zip file. You'll typically find `nssm.exe` in the `win64` or `win32` subfolder.

4.  **Install the service using NSSM:**
    Open an **Administrator Command Prompt** or **PowerShell** and navigate to the directory where you extracted `nssm.exe`. Then, run the following command:
    ```cmd
    nssm install NetworkPingerService
    ```
    This will open the NSSM service installer GUI.

    -   **Path:** Browse to the executable of your published application. This will be `C:\Program Files\NetworkPingerService\NetworkPingerService.exe` (adjust path if different).
    -   **Startup directory:** Set this to `C:\Program Files\NetworkPingerService` (the folder where you copied your published files).
    -   **Details Tab:** You can set the "Display name" (e.g., "Network Pinger Service") and "Description" for your service.
    -   **Logon Tab:** Choose the user account under which the service will run. For most cases, "Local System account" is fine, but for network access or specific permissions, you might need to specify a user account.
    -   **I/O Tab:** You can optionally redirect stdout/stderr to log files for debugging.
    -   **Exit Actions Tab:** Configure restart behavior (e.g., `Restart application` on failure).
    -   **Environment Tab:** **Crucially, this is where you can set environment variables for your SMTP credentials (e.g., `SmtpUsername`, `SmtpPassword`) to avoid hardcoding them in `appsettings.json`.**
    -   Click **Install service**.

5.  **Start and manage the service:**
    Once installed, you can manage the service via the Windows Services Manager (`services.msc`) or using `sc` commands in an Administrator Command Prompt:
    ```cmd
    sc start NetworkPingerService
    sc stop NetworkPingerService
    sc query NetworkPingerService
    ```
    To uninstall the service:
    ```cmd
    nssm remove NetworkPingerService
    ```

## 🤝 Contributing

Contributions are welcome! If you have suggestions for improvements, bug reports, or want to add new features, please feel free to:

1.  Fork the repository.
2.  Create a new branch (`git checkout -b feature/your-feature-name`).
3.  Make your changes.
4.  Commit your changes (`git commit -m 'Add new feature'`).
5.  Push to the branch (`git push origin feature/your-feature-name`).
6.  Open a Pull Request.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 📞 Support / Contact

If you have any questions or need further assistance, please open an issue on the GitHub repository.
