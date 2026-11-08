// "Add to StartQueue" in the Windows 11 context menu for programs and shortcuts.
// Explorer loads this through the StartQueue package (IExplorerCommand in a COM surrogate).
// Invoking it starts StartQueue.exe --add "<file>" from the same folder.

#include <windows.h>
#include <shobjidl_core.h>
#include <shlwapi.h>
#include <shlobj.h>
#include <new>
#include <string>

// {2B22CA93-19BE-483D-BB1A-E3E2B4853EA8}, must match package\AppxManifest.xml
static const CLSID CLSID_AddToStartQueue = { 0x2b22ca93, 0x19be, 0x483d, { 0xbb, 0x1a, 0xe3, 0xe2, 0xb4, 0x85, 0x3e, 0xa8 } };

static HMODULE g_module = nullptr;
static LONG g_objects = 0;
static LONG g_locks = 0;

static std::wstring AppPath()
{
    wchar_t path[MAX_PATH];
    DWORD length = GetModuleFileNameW(g_module, path, MAX_PATH);
    if (length == 0 || length >= MAX_PATH) return L"";
    PathRemoveFileSpecW(path);
    return std::wstring(path) + L"\\StartQueue.exe";
}

// The language chosen in StartQueue: "German", "English" or "System" (read from its config.json).
static std::string ChosenLanguage()
{
    wchar_t appData[MAX_PATH];
    if (FAILED(SHGetFolderPathW(nullptr, CSIDL_APPDATA, nullptr, SHGFP_TYPE_CURRENT, appData))) return "";
    std::wstring file = std::wstring(appData) + L"\\StartQueue\\config.json";
    HANDLE handle = CreateFileW(file.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                                nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (handle == INVALID_HANDLE_VALUE) return "";
    std::string text;
    LARGE_INTEGER size{};
    if (GetFileSizeEx(handle, &size) && size.QuadPart > 0 && size.QuadPart < 16 * 1024 * 1024)
    {
        text.resize(static_cast<size_t>(size.QuadPart));
        DWORD read = 0;
        if (!ReadFile(handle, &text[0], static_cast<DWORD>(text.size()), &read, nullptr)) read = 0;
        text.resize(read);
    }
    CloseHandle(handle);

    // "language": "English" -> English
    size_t key = text.find("\"language\"");
    if (key == std::string::npos) return "";
    size_t open = text.find('"', text.find(':', key));
    size_t close = open == std::string::npos ? open : text.find('"', open + 1);
    return close == std::string::npos ? "" : text.substr(open + 1, close - open - 1);
}

// Menu title in the language chosen in StartQueue, like the app itself; "Same as Windows" follows the display language.
static LPCWSTR Title()
{
    std::string chosen = ChosenLanguage();
    bool german = chosen == "German" ||
                  (chosen != "English" && PRIMARYLANGID(GetUserDefaultUILanguage()) == LANG_GERMAN);
    return german ? L"Zu StartQueue hinzufügen" : L"Add to StartQueue";
}

static bool IsSupported(const std::wstring& path)
{
    LPCWSTR extension = PathFindExtensionW(path.c_str());
    return _wcsicmp(extension, L".exe") == 0 || _wcsicmp(extension, L".lnk") == 0;
}

static HRESULT ItemPath(IShellItemArray* items, DWORD index, std::wstring& path)
{
    IShellItem* item = nullptr;
    HRESULT hr = items->GetItemAt(index, &item);
    if (FAILED(hr)) return hr;
    PWSTR name = nullptr;
    hr = item->GetDisplayName(SIGDN_FILESYSPATH, &name);
    if (SUCCEEDED(hr))
    {
        path = name;
        CoTaskMemFree(name);
    }
    item->Release();
    return hr;
}

class AddCommand final : public IExplorerCommand
{
public:
    AddCommand() { InterlockedIncrement(&g_objects); }

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        if (riid == IID_IUnknown || riid == __uuidof(IExplorerCommand))
        {
            *ppv = static_cast<IExplorerCommand*>(this);
            AddRef();
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&m_refs); }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        ULONG refs = InterlockedDecrement(&m_refs);
        if (refs == 0) delete this;
        return refs;
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, LPWSTR* name) override { return SHStrDupW(Title(), name); }

    IFACEMETHODIMP GetIcon(IShellItemArray*, LPWSTR* icon) override
    {
        std::wstring location = AppPath() + L",0";
        return SHStrDupW(location.c_str(), icon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* tip) override
    {
        *tip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* guid) override
    {
        *guid = CLSID_AddToStartQueue;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) override
    {
        *state = ECS_HIDDEN;
        DWORD count = 0;
        if (!items || FAILED(items->GetCount(&count)) || count == 0) return S_OK;
        for (DWORD i = 0; i < count; i++)
        {
            std::wstring path;
            if (FAILED(ItemPath(items, i, path)) || !IsSupported(path)) return S_OK;
        }
        *state = ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
    {
        DWORD count = 0;
        if (!items || FAILED(items->GetCount(&count))) return S_OK;
        std::wstring app = AppPath();
        for (DWORD i = 0; i < count; i++)
        {
            std::wstring path;
            if (FAILED(ItemPath(items, i, path))) continue;
            std::wstring command = L"\"" + app + L"\" --add \"" + path + L"\"";
            STARTUPINFOW startup{ sizeof(startup) };
            PROCESS_INFORMATION process{};
            if (CreateProcessW(app.c_str(), &command[0], nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startup, &process))
            {
                CloseHandle(process.hThread);
                CloseHandle(process.hProcess);
            }
        }
        return S_OK;
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
    {
        *commands = nullptr;
        return E_NOTIMPL;
    }

private:
    ~AddCommand() { InterlockedDecrement(&g_objects); }
    LONG m_refs = 1;
};

class Factory final : public IClassFactory
{
public:
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        if (riid == IID_IUnknown || riid == IID_IClassFactory)
        {
            *ppv = static_cast<IClassFactory*>(this);
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override { return 2; }
    IFACEMETHODIMP_(ULONG) Release() override { return 1; }

    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        *ppv = nullptr;
        if (outer) return CLASS_E_NOAGGREGATION;
        AddCommand* command = new (std::nothrow) AddCommand();
        if (!command) return E_OUTOFMEMORY;
        HRESULT hr = command->QueryInterface(riid, ppv);
        command->Release();
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        if (lock) InterlockedIncrement(&g_locks);
        else InterlockedDecrement(&g_locks);
        return S_OK;
    }
};

static Factory g_factory;

STDAPI DllGetClassObject(REFCLSID clsid, REFIID riid, LPVOID* ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;
    if (clsid != CLSID_AddToStartQueue) return CLASS_E_CLASSNOTAVAILABLE;
    return g_factory.QueryInterface(riid, ppv);
}

STDAPI DllCanUnloadNow()
{
    return (g_objects == 0 && g_locks == 0) ? S_OK : S_FALSE;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = module;
        DisableThreadLibraryCalls(module);
    }
    return TRUE;
}
