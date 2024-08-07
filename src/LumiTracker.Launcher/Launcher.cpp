#include <iostream>
#include <string>
#include <map>
#include <vector>
#include <fstream>
#include <filesystem>
#include <cstdlib>
#include <Windows.h>

#include "resource.h"

namespace fs = std::filesystem;

// Define the type aliases for easier manipulation
using SectionMap = std::map<std::string, std::string>;
using IniData    = std::map<std::string, SectionMap>;

std::string Trim(const std::string& str) 
{
    auto start = str.find_first_not_of(" \t\n\r");
    auto end   = str.find_last_not_of(" \t\n\r");
    return (start == std::string::npos || end == std::string::npos) ? "" : str.substr(start, end - start + 1);
}

// Function to handle INI file parsing
IniData ReadIniFile(const std::string& filePath) 
{
    std::ifstream file(filePath);
    if (!file.is_open()) 
    {
        std::cerr << "Error: Could not open file " << filePath << std::endl;
        return {};
    }

    IniData iniData;
    std::string currentSection;
    std::string line;
    while (std::getline(file, line)) 
    {
        std::string trimmedLine = Trim(line);

        // Skip empty lines and comments
        if (trimmedLine.empty() || trimmedLine[0] == ';')
            continue;

        // Handle section headers
        if (trimmedLine[0] == '[' && trimmedLine.back() == ']') 
        {
            currentSection = trimmedLine.substr(1, trimmedLine.size() - 2);
            currentSection = Trim(currentSection);
            iniData[currentSection] = SectionMap();
            continue;
        }

        // Handle key=value pairs
        if (!currentSection.empty()) 
        {
            std::size_t delimiterPos = trimmedLine.find('=');
            if (delimiterPos != std::string::npos) 
            {
                std::string key   = Trim(trimmedLine.substr(0, delimiterPos));
                std::string value = Trim(trimmedLine.substr(delimiterPos + 1));
                iniData[currentSection][key] = value;
            }
        }
    }

    return iniData;
}

// Main function
int APIENTRY WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    HICON hIcon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_APP_ICON));

    std::string rootDir = lpCmdLine;
    if (!rootDir.empty())
    {
        Sleep(1500);
    }

    // Define the path to the .ini file
    std::string iniFilePath = rootDir + "LumiTracker.ini";

    // Read and parse the .ini file
    IniData iniData = ReadIniFile(iniFilePath);

    // Read the executable path from the parsed data
    auto appSectionIt = iniData.find("Application");
    if (appSectionIt == iniData.end()) 
    {
        std::cerr << "Error: No 'Application' section found in the .ini file." << std::endl;
        return 1;
    }

    auto& applicationSection = appSectionIt->second;
    auto versionIt = applicationSection.find("Version");
    if (versionIt == applicationSection.end()) 
    {
        std::cerr << "Error: No 'Version' specified in the 'Application' section." << std::endl;
        return 1;
    }

    std::string appVersion = versionIt->second;

    // Check if the executable exists
    std::string app = rootDir + "LumiTrackerApp-" + appVersion + "\\LumiTrackerApp.exe";
    if (!fs::exists(app)) 
    {
        std::cerr << "Error: Specified executable not found." << std::endl;
        return 1;
    }

    // Launch the executable
    // Define the startup info structure
    STARTUPINFOA si;
    PROCESS_INFORMATION pi;

    // Initialize the STARTUPINFO structure
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE; // Hide the console window

    // Initialize the PROCESS_INFORMATION structure
    ZeroMemory(&pi, sizeof(pi));

    // Create the process
    BOOL success = CreateProcessA(
        NULL,                   // Application name
        app.data(),             // Command line (process to run)
        NULL,                   // Process security attributes
        NULL,                   // Thread security attributes
        FALSE,                  // Inherit handles
        0,                      // Creation flags
        NULL,                   // Environment
        NULL,                   // Current directory
        &si,                    // STARTUPINFO pointer
        &pi                     // PROCESS_INFORMATION pointer
    );

    if (success) 
    {
        std::cout << "Process created successfully!" << std::endl;
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
    }
    else 
    {
        std::cerr << "CreateProcess failed (" << GetLastError() << ")." << std::endl;
    }

    return 0;
}
