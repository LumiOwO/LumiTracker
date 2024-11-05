#include <iostream>
#include <string>
#include <map>
#include <vector>
#include <fstream>
#include <filesystem>
#include <cstdlib>
#include <sstream>
#include <cassert>
#include <Windows.h>

#include "resource.h"

namespace fs = std::filesystem;

static bool Utf8BufferToWString(const std::string& src, std::wstring& dst)
{
    // Convert the buffer (UTF-8) to a wide string (UTF-16)
    int sizeNeeded = MultiByteToWideChar(CP_UTF8, 0, src.data(), static_cast<int>(src.size()), nullptr, 0);
    if (sizeNeeded <= 0)
        return false;

    dst = std::wstring(sizeNeeded, L'\0'); // Create a wide string with the required size
    MultiByteToWideChar(CP_UTF8, 0, src.data(), static_cast<int>(src.size()), &dst[0], sizeNeeded);
    return true;
}

static bool WStringToUtf8Buffer(const std::wstring& src, std::string& dst)
{
    // Convert the wide string (UTF-16) to a UTF-8 buffer
    int sizeNeeded = WideCharToMultiByte(CP_UTF8, 0, src.data(), static_cast<int>(src.size()), nullptr, 0, nullptr, nullptr);
    if (sizeNeeded <= 0)
        return false;

    dst = std::string(sizeNeeded, '\0'); // Create a string with the required size
    WideCharToMultiByte(CP_UTF8, 0, src.data(), static_cast<int>(src.size()), &dst[0], sizeNeeded, nullptr, nullptr);
    return true;
}

static std::string ToUtf8Buffer(const std::wstring& wstr)
{
    std::string res;
    assert(WStringToUtf8Buffer(wstr, res));
    return res;
}

static std::ofstream gLaunchLog;

enum EOpenLink : uint8_t
{
    NO_OPEN_LINK = 0,
    OPEN_LINK
};

enum EShouldExit : uint8_t
{
    NOT_EXIT = 0,
    SHOULD_EXIT,
};

static void _PostAlert(bool open_link, bool should_exit)
{
    if (open_link)
    {
        LPCWSTR url = L"https://uex8no0g44.feishu.cn/docx/SBXZdiKNvoXeSrxgfpccuIvVnAe#share-TPW9dOyEFoEmPOxa1ZPczYS5nNh";
        ShellExecuteW(NULL, L"open", url, NULL, NULL, SW_SHOWNORMAL);
    }
    if (should_exit)
    {
        if (gLaunchLog.is_open())
        {
            gLaunchLog.close();
        }
        ExitProcess(1);
    }
}

static void AlertA(const std::string& errorMessage, bool open_link, bool should_exit)
{
    if (gLaunchLog.is_open())
    {
        gLaunchLog << errorMessage << std::endl;
    }
    MessageBoxA(NULL, errorMessage.c_str(), "Error", MB_OK | MB_ICONERROR);
    _PostAlert(open_link, should_exit);
}

static void AlertW(const std::wstring& errorMessage, bool open_link, bool should_exit)
{
    if (gLaunchLog.is_open())
    {
        gLaunchLog << ToUtf8Buffer(errorMessage) << std::endl;
    }
    MessageBoxW(NULL, errorMessage.c_str(), L"Error", MB_OK | MB_ICONERROR);
    _PostAlert(open_link, should_exit);
}


// Define the type aliases for easier manipulation
using SectionMap = std::map<std::wstring, std::wstring>;
using IniData    = std::map<std::wstring, SectionMap>;

static std::wstring Trim(const std::wstring& str) 
{
    auto start = str.find_first_not_of(L" \t\n\r");
    auto end   = str.find_last_not_of(L" \t\n\r");
    return (start == std::wstring::npos || end == std::wstring::npos) ? L"" : str.substr(start, end - start + 1);
}

// Function to handle INI file parsing
static IniData ReadIniFile(const std::wstring& filePath)
{
    std::ifstream file(filePath, std::ios::binary);
    if (!file.is_open()) 
    {
        std::wostringstream msg;
        msg << L"Error: Could not open file " << filePath << std::endl;
        AlertW(msg.str(), NO_OPEN_LINK, SHOULD_EXIT);
    }

    // Read the file into a std::string
    std::string buffer((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
    std::wstring wideString;
    if (!Utf8BufferToWString(buffer, wideString))
    {
        std::wostringstream msg;
        msg << L"Error: Conversion failed for file " << filePath << std::endl;
        AlertW(msg.str(), NO_OPEN_LINK, SHOULD_EXIT);
    }

    auto content = std::wistringstream(wideString);
    IniData iniData;
    std::wstring currentSection;
    std::wstring line;
    while (std::getline(content, line))
    {
        std::wstring trimmedLine = Trim(line);

        // Skip empty lines and comments
        if (trimmedLine.empty() || trimmedLine[0] == L';')
            continue;

        // Handle section headers
        if (trimmedLine[0] == L'[' && trimmedLine.back() == L']') 
        {
            currentSection = trimmedLine.substr(1, trimmedLine.size() - 2);
            currentSection = Trim(currentSection);
            iniData[currentSection] = SectionMap();
            continue;
        }

        // Handle key=value pairs
        if (!currentSection.empty()) 
        {
            std::size_t delimiterPos = trimmedLine.find(L'=');
            if (delimiterPos != std::wstring::npos) 
            {
                std::wstring key   = Trim(trimmedLine.substr(0, delimiterPos));
                std::wstring value = Trim(trimmedLine.substr(delimiterPos + 1));
                iniData[currentSection][key] = value;
            }
        }
    }

    return iniData;
}

static std::wstring _CheckDotNetRuntimes()
{
    // Prepare the command to execute
    std::string command = "dotnet.exe --list-runtimes";

    // Set up security attributes
    SECURITY_ATTRIBUTES sa;
    sa.nLength = sizeof(SECURITY_ATTRIBUTES);
    sa.lpSecurityDescriptor = NULL;
    sa.bInheritHandle = TRUE;

    // Create a pipe for the output
    HANDLE hRead, hWrite;
    if (!CreatePipe(&hRead, &hWrite, &sa, 0)) 
    {
        return L"Error creating pipe.";
    }

    // Set up the process information and startup info
    PROCESS_INFORMATION pi;
    STARTUPINFOA si; // Use STARTUPINFOA for ANSI
    ZeroMemory(&pi, sizeof(pi));
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESTDHANDLES;
    si.hStdOutput = hWrite;
    si.hStdError = hWrite;

    // Create the process
    BOOL success = CreateProcessA(
        NULL,                   // Application name
        command.data(),         // Command line (process to run)
        NULL,                   // Process security attributes
        NULL,                   // Thread security attributes
        TRUE,                   // Inherit handles
        CREATE_NO_WINDOW,       // Creation flags
        NULL,                   // Environment
        NULL,                   // Current directory
        &si,                    // STARTUPINFO pointer
        &pi                     // PROCESS_INFORMATION pointer
    );
    if (!success)
    {
        CloseHandle(hWrite);
        CloseHandle(hRead);
        return L"Failed to execute 'dotnet.exe --list-runtimes'. Check if the .NET SDK is installed properly.";
    }

    // Close the write end of the pipe since we don't need it
    CloseHandle(hWrite);

    // Read output from the pipe.
    std::vector<char> buffer(4096);
    DWORD bytesRead;
    std::string output;
    while (ReadFile(hRead, buffer.data(), static_cast<DWORD>(buffer.size()), &bytesRead, NULL) && bytesRead > 0) 
    {
        output.append(buffer.data(), bytesRead);
    }

    // Close handles
    CloseHandle(hRead);
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);

    bool foundNETCore = false;
    bool foundWindowsDesktop = false;

    std::istringstream lines(output);
    std::string line;
    while (std::getline(lines, line)) 
    {
        if (!foundNETCore && line.find("Microsoft.NETCore.App 8.") != std::string::npos) 
        {
            foundNETCore = true;
        }
        else if (!foundWindowsDesktop && line.find("Microsoft.WindowsDesktop.App 8.") != std::string::npos) 
        {
            foundWindowsDesktop = true;
        }
    }

    if (!foundNETCore && !foundWindowsDesktop) 
    {
        return L"Microsoft.NETCore.App & Microsoft.WindowsDesktop.App version 8.x not found in the runtime list.";
    }
    else if (!foundNETCore) 
    {
        return L"Microsoft.NETCore.App version 8.x not found in the runtime list.";
    }
    else if (!foundWindowsDesktop) 
    {
        return L"Microsoft.WindowsDesktop.App version 8.x not found in the runtime list.";
    }
    else
    {
        // Success
        return L"";
    }
}

static void CheckDotNetEnvironment() 
{
    std::wstring errorMessage = _CheckDotNetRuntimes();
    if (!errorMessage.empty())
    {
        // Get the current system locale
        LCID locale = GetUserDefaultLCID();

        // Define error messages for different locales
        std::wstring prompt;
        if (locale == MAKELANGID(LANG_CHINESE, SUBLANG_CHINESE_SIMPLIFIED))
        {
            prompt = 
                L"未检测到 .NET 8.0 环境，记牌器无法启动！\n"
                L"请参考「常见问题」中的第一条，以解决此问题。\n"
                L"（该窗口关闭后，将自动跳转到说明文档链接）\n"
                L"\n"
                L"错误信息：\n"
                ;
        }
        else
        {
            prompt =
                L".NET 8.0 environment not detected. LumiTracker failed to start!\n"
                L"Please refer to the FAQ to resolve this issue.\n"
                L"(Closing this will redirect you to the docs)\n"
                L"\n"
                L"Error message:\n"
                ;
        }
        AlertW(prompt + errorMessage, OPEN_LINK, NOT_EXIT);
    }
}

void Launch(const std::wstring& rootDir, bool just_updated)
{
    gLaunchLog << "Root dir: " << ToUtf8Buffer(rootDir) << std::endl;
    if (just_updated)
    {
        Sleep(1500);
    }

    // Call the environment check
    CheckDotNetEnvironment();

    // Define the path to the .ini file
    std::wstring iniFilePath = rootDir + L"LumiTracker.ini";

    // Read and parse the .ini file
    IniData iniData = ReadIniFile(iniFilePath);

    // Read the executable path from the parsed data
    auto appSectionIt = iniData.find(L"Application");
    if (appSectionIt == iniData.end()) 
    {
        AlertA("Error: No 'Application' section found in the .ini file.", NO_OPEN_LINK, SHOULD_EXIT);
    }

    auto& applicationSection = appSectionIt->second;
    auto versionIt = applicationSection.find(L"Version");
    if (versionIt == applicationSection.end())
    {
        AlertA("Error: No 'Version' specified in the 'Application' section.", NO_OPEN_LINK, SHOULD_EXIT);
    }

    std::wstring appVersion = versionIt->second;

    // Check if the executable exists
    std::wstring workingDir = rootDir + L"LumiTrackerApp-" + appVersion;
    std::wstring app = workingDir + L"\\LumiTrackerApp.exe";
    if (!fs::exists(app))
    {
        AlertW(L"Error: Specified executable not found: " + app, NO_OPEN_LINK, SHOULD_EXIT);
    }

    std::wstring parameters = L"";
    if (just_updated)
    {
        parameters = L"just_updated";
    }

    auto consoleIt = applicationSection.find(L"Console");
    bool showConsole = (consoleIt != applicationSection.end() && consoleIt->second == L"1");

    // Launch the executable
    // Define the startup info structure
    STARTUPINFOW si;
    PROCESS_INFORMATION pi;
    ZeroMemory(&si, sizeof(si));
    ZeroMemory(&pi, sizeof(pi));

    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = showConsole ? SW_SHOW : SW_HIDE;

    DWORD flags = showConsole ? CREATE_NEW_CONSOLE: 0;

    // Create the process
    BOOL success = CreateProcessW(
        app.data(),             // Application name
        parameters.data(),      // Command line arguments
        NULL,                   // Process security attributes
        NULL,                   // Thread security attributes
        FALSE,                  // Inherit handles
        flags,                  // Creation flags
        NULL,                   // Environment
        workingDir.data(),      // Working directory
        &si,                    // STARTUPINFO pointer
        &pi                     // PROCESS_INFORMATION pointer
    );

    if (!success) 
    {
        std::ostringstream msg;
        msg << "CreateProcess failed (" << GetLastError() << ")." << std::endl;
        AlertA(msg.str(), NO_OPEN_LINK, SHOULD_EXIT);
    }
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);

    gLaunchLog << "LumiTrackerApp-" << ToUtf8Buffer(appVersion) << " launched." << std::endl;
}

std::wstring GetRootDirectory()
{
    wchar_t path[MAX_PATH];
    if (GetModuleFileNameW(NULL, path, MAX_PATH) > 0) 
    {
        std::wstring fullPath(path);
        size_t pos = fullPath.find_last_of(L"\\/");
        if (pos != std::wstring::npos) 
        {
            return fullPath.substr(0, pos) + L"\\";
        }
    }
    // Return an empty wstring if failed, which means relative path
    return L"";
}

// Main function
int APIENTRY wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ LPWSTR lpCmdLine, _In_ int nCmdShow)
{
    try
    {
        HICON hIcon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_APP_ICON));

        std::wstring rootDir = GetRootDirectory();
        std::wstring filename = rootDir + std::wstring(L"launch.log");
        gLaunchLog.open(filename, std::ios::out | std::ios::trunc);
        if (!gLaunchLog.is_open()) 
        {
            std::wostringstream msg;
            msg << L"Failed to open error log: " << filename << std::endl;
            AlertW(msg.str(), NO_OPEN_LINK, SHOULD_EXIT);
        }

        Launch(rootDir, lpCmdLine == std::wstring(L"just_updated"));
    }
    catch (std::exception ex)
    {
        AlertA(std::string("Unexpected Error: ") + ex.what(), OPEN_LINK, SHOULD_EXIT);
    }

    if (gLaunchLog.is_open()) 
    {
        gLaunchLog.close();
    }

    return 0;
}
