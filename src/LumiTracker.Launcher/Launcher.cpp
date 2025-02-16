#include <iostream>
#include <string>
#include <map>
#include <vector>
#include <fstream>
#include <filesystem>
#include <cstdlib>
#include <sstream>
#include <cassert>

#include "resource.h"
#include "json.hpp"
#include "cxxopts.hpp"

#include <Windows.h>
#include <ShlObj.h>
#include <KnownFolders.h>

using json = nlohmann::json;
namespace fs = std::filesystem;

namespace lumi
{

struct HandleGuard
{
    HANDLE handle;
    HandleGuard(HANDLE h) noexcept : handle(h) {}
    ~HandleGuard()
    {
        if (handle && handle != INVALID_HANDLE_VALUE)
        {
            CloseHandle(handle);
        }
    }
    HandleGuard(const HandleGuard& other) noexcept = delete;
    HandleGuard(HandleGuard&& other) noexcept = delete;
    HandleGuard& operator=(const HandleGuard& other) noexcept = delete;
    HandleGuard& operator=(HandleGuard&& other) noexcept = delete;
};

class OfStreamWrapper
{
private:
    std::ofstream stream;

public:
    explicit operator bool() const noexcept 
    { 
        return bool(stream); 
    }

    bool is_open() const noexcept
    {
        return stream.is_open();
    }

    void open(const fs::path& path, std::ios_base::openmode mode)
    {
        stream.open(path, mode);
    }

    template <typename T>
    OfStreamWrapper& operator<<(const T& value)
    {
        if (stream.is_open())
        {
            stream << value;
        }
        return *this;
    }

    using Manipulator = std::ostream& (*)(std::ostream&); // Function pointer type
    OfStreamWrapper& operator<<(const Manipulator& value)
    {
        if (stream.is_open())
        {
            stream << value;
        }
        return *this;
    }
};

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

// Global variables
static   OfStreamWrapper   gLaunchLog;
static   fs::path          gRootDir;
static   fs::path          gDocumentsDir;
// Commandline arguments
static   bool              gJustUpdated = false;
static   DWORD             gDelayTime   = 0;

static void _PostAlert(bool open_link, bool should_exit)
{
    if (open_link)
    {
        LPCWSTR url = L"https://uex8no0g44.feishu.cn/docx/SBXZdiKNvoXeSrxgfpccuIvVnAe#share-E1aLdoGEmotBuvxPMiFcY9isnBe";
        ShellExecuteW(NULL, L"open", url, NULL, NULL, SW_SHOWNORMAL);
    }
    if (should_exit)
    {
        ExitProcess(1);
    }
}

static void Alert(const std::string& errorMessage, bool open_link = true, bool should_exit = true)
{
    gLaunchLog << errorMessage << std::endl;
    MessageBoxA(NULL, errorMessage.c_str(), "Error", MB_OK | MB_ICONERROR);
    _PostAlert(open_link, should_exit);
}

static void Alert(const std::wstring& errorMessage, bool open_link = true, bool should_exit = true)
{
    gLaunchLog << ToUtf8Buffer(errorMessage) << std::endl;
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
        Alert(msg.str());
    }

    // Read the file into a std::string
    std::string buffer((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
    std::wstring wideString;
    if (!Utf8BufferToWString(buffer, wideString))
    {
        std::wostringstream msg;
        msg << L"Error: Conversion failed for file " << filePath << std::endl;
        Alert(msg.str());
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
    auto hReadGuard = HandleGuard(hRead);
    auto hWriteGuard = HandleGuard(hWrite);

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
        return L"Failed to execute 'dotnet.exe --list-runtimes'. Check if the .NET SDK is installed properly.";
    }
    auto hProcessGuard = HandleGuard(pi.hProcess);
    auto hThreadGuard = HandleGuard(pi.hThread);

    // Close the write end of the pipe since we don't need it
    CloseHandle(hWrite);
    hWriteGuard.handle = NULL;

    // Read output from the pipe.
    std::vector<char> buffer(4096);
    DWORD bytesRead;
    std::string output;
    while (ReadFile(hRead, buffer.data(), static_cast<DWORD>(buffer.size()), &bytesRead, NULL) && bytesRead > 0) 
    {
        output.append(buffer.data(), bytesRead);
    }

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
        Alert(prompt + errorMessage);
    }
}

static json LoadUserConfig()
{
    json userConfig = json::object();

    // Try to open user config
    fs::path userConfigPath = gDocumentsDir / "config" / "config.json";
    if (!fs::exists(userConfigPath))
    {
        return userConfig;
    }

    std::ifstream userConfigfile(userConfigPath);
    if (!userConfigfile.is_open())
    {
        gLaunchLog << "Failed to open file: " << userConfigPath << std::endl;
        return userConfig;
    }

    try 
    {
        userConfig = json::parse(userConfigfile);
    }
    catch (const std::exception& e) 
    {
        gLaunchLog << "Error when parsing json from " << userConfigPath << ": " << e.what() << std::endl;
        userConfig = json::object();
    }
    return userConfig;
}

static void Launch()
{
    gLaunchLog << "Root dir: " << ToUtf8Buffer(gRootDir) << std::endl;
    if (gDelayTime > 0)
    {
        Sleep(gDelayTime);
    }

    // Call the environment check
    CheckDotNetEnvironment();

    // Define the path to the .ini file
    fs::path iniFilePath = gRootDir / L"LumiTracker.ini";

    // Read and parse the .ini file
    IniData iniData = ReadIniFile(iniFilePath);

    // Read the executable path from the parsed data
    auto appSectionIt = iniData.find(L"Application");
    if (appSectionIt == iniData.end()) 
    {
        Alert("Error: No 'Application' section found in the .ini file.");
    }

    auto& applicationSection = appSectionIt->second;
    auto versionIt = applicationSection.find(L"Version");
    if (versionIt == applicationSection.end())
    {
        Alert("Error: No 'Version' specified in the 'Application' section.");
    }

    std::wstring appVersion = versionIt->second;

    // Check if the executable exists
    fs::path workingDir = gRootDir / (L"LumiTrackerApp-" + appVersion);
    fs::path app = workingDir / L"LumiTrackerApp.exe";
    if (!fs::exists(app))
    {
        Alert(L"Error: Specified executable not found: " + app.wstring());
    }

    // Get launch settings
    json userConfig = LoadUserConfig();
    std::wstring parameters = L"";
    if (gJustUpdated)
    {
        parameters += L"just_updated";
    }
    auto consoleIt = applicationSection.find(L"Console");
    bool showConsole = (consoleIt != applicationSection.end() && consoleIt->second == L"1");
    bool runAsAdmin = userConfig.value("run_as_admin", false);

    // Define the startup info structure
    STARTUPINFOW si;
    PROCESS_INFORMATION pi;
    ZeroMemory(&si, sizeof(si));
    ZeroMemory(&pi, sizeof(pi));
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = showConsole ? SW_SHOW : SW_HIDE;

    // Create the process
    BOOL success = false;
    if (runAsAdmin)
    {
        SHELLEXECUTEINFOW sei = { 0 };
        sei.cbSize = sizeof(sei);
        sei.fMask = SEE_MASK_NOCLOSEPROCESS;
        sei.lpVerb = L"runas";
        sei.lpFile = app.c_str();
        sei.lpParameters = parameters.empty() ? nullptr : parameters.c_str();
        sei.lpDirectory = workingDir.c_str();
        sei.nShow = si.wShowWindow;

        success = ShellExecuteExW(&sei);
        if (success)
        {
            pi.hProcess = sei.hProcess;
        }
    }
    else
    {
        std::wstring command = app.wstring() + L" " + parameters;
        DWORD flags = showConsole ? CREATE_NEW_CONSOLE : 0;
        success = CreateProcessW(
            NULL,                   // Application name (NULL to rely on lpCommandLine)
            command.data(),         // Full command line (app path + arguments)
            NULL,                   // Process security attributes
            NULL,                   // Thread security attributes
            FALSE,                  // Inherit handles
            flags,                  // Creation flags
            NULL,                   // Environment
            workingDir.c_str(),     // Working directory
            &si,                    // STARTUPINFO pointer
            &pi                     // PROCESS_INFORMATION pointer
        );
    }
    auto hProcessGuard = HandleGuard(pi.hProcess);
    auto hThreadGuard = HandleGuard(pi.hThread);

    if (!success) 
    {
        DWORD errNo = GetLastError();
        LPWSTR errorTextPtr = nullptr;
        FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM,
            NULL, errNo, 0, (LPWSTR)&errorTextPtr, 0, NULL);
        std::wstring errorText = errorTextPtr;
        LocalFree(errorTextPtr);

        std::wostringstream msg;
        msg << L"CreateProcess failed (" << errNo << L"): " << errorText << std::endl;
        Alert(msg.str());
    }

    gLaunchLog << "LumiTrackerApp-" << ToUtf8Buffer(appVersion) << " launched." << std::endl;
}

static fs::path GetRootDirectory()
{
    fs::path res = fs::current_path();

    wchar_t path[MAX_PATH];
    if (GetModuleFileNameW(NULL, path, MAX_PATH) > 0) 
    {
        std::wstring fullPath(path);
        size_t pos = fullPath.find_last_of(L"\\/");
        if (pos != std::wstring::npos) 
        {
            res = fullPath.substr(0, pos) + L"\\";
        }
    }
    
    return fs::absolute(res);
}

static fs::path GetSystemDocumentsDirectory()
{
    fs::path res = fs::temp_directory_path();

    PWSTR path = NULL;
    HRESULT hr = SHGetKnownFolderPath(FOLDERID_Documents, 0, NULL, &path);
    if (SUCCEEDED(hr)) 
    {
        res = path;
    }
    CoTaskMemFree(path);

    return fs::absolute(res);
}

} // namespace lumi


// Main function
int APIENTRY wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ LPWSTR lpCmdLine, _In_ int nCmdShow)
{
    using namespace lumi;
    try
    {
        HICON hIcon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_APP_ICON));

        // Convert all arguments to UTF-8
        int argc = 0;
        std::vector<const char*> argv;
        std::vector<std::string> argvStr;
        LPWSTR* argvW = CommandLineToArgvW(GetCommandLineW(), &argc);
        if (argvW)
        {
            for (int i = 0; i < argc; ++i) 
            {
                argvStr.emplace_back(ToUtf8Buffer(argvW[i]));
                argv.emplace_back(argvStr.back().c_str());
            }
            LocalFree(argvW);
        }

        cxxopts::Options options("LumiTracker", "LumiTracker app launcher.");
        options.allow_unrecognised_options();  // Allow unknown args
        options.add_options()
            ("just_updated", "Whether the c# app has just updated", cxxopts::value<bool>()->default_value("false"))
            ("delay", "Delay time in seconds before launching the app", cxxopts::value<double>()->default_value("0"))
            ("h,help", "Show help")
            ;


        // Init global variables
        gRootDir = GetRootDirectory();
        gDocumentsDir = GetSystemDocumentsDirectory() / L"LumiTracker";
        fs::create_directories(gDocumentsDir);
        fs::path logFilePath = gDocumentsDir / L"launch.log";
        gLaunchLog.open(logFilePath, std::ios::out | std::ios::trunc);
        if (!gLaunchLog.is_open()) 
        {
#ifdef _DEBUG
            std::wostringstream msg;
            msg << L"Failed to open error log: " << logFilePath << std::endl;
            Alert(msg.str());
#endif // _DEBUG
        }

        for (int i = 0; i < argc; i++)
        {
            gLaunchLog << argvStr[i];
            if (i < argc - 1)
            {
                gLaunchLog << " ";
            }
            else
            {
                gLaunchLog << std::endl;
            }
        }

        // Parse command line arguments
        auto result = options.parse(argc, argv.data());
        if (result.count("help"))
        {
            gLaunchLog << options.help() << std::endl;
            return 0;
        }
        gJustUpdated = result["just_updated"].as<bool>();
        gDelayTime = DWORD(result["delay"].as<double>() * 1000);
        for (const auto& arg : result.unmatched()) 
        {
            if (arg == "just_updated") 
            {
                gJustUpdated = true;
            }
        }
        if (gDelayTime == 0 && gJustUpdated)
        {
            gDelayTime = 1500;
        }

        Launch();
    }
    catch (const std::exception& ex)
    {
        Alert(std::string("Unexpected Error: ") + ex.what());
    }

    return 0;
}
