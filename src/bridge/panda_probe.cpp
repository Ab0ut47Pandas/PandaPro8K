#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <hidsdi.h>
#include <setupapi.h>
#include <xinput.h>

#include <atomic>
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <string>
#include <thread>
#include <vector>

#pragma comment(lib, "hid.lib")
#pragma comment(lib, "setupapi.lib")
#pragma comment(lib, "xinput.lib")

namespace {
constexpr USHORT kGameSirVid = 0x3537;
constexpr USHORT kAimlabsDonglePid = 0x10c6;
constexpr USAGE kVendorUsagePage = 0xfff0;
constexpr USAGE kVendorUsage = 0x0040;

struct Counters {
    std::atomic<uint64_t> xinputPolls{0};
    std::atomic<uint64_t> xinputChanges{0};
    std::atomic<uint64_t> enhancedReports{0};
    std::atomic<uint64_t> imuChanges{0};
    std::atomic<uint8_t> extras{0};
    std::atomic<int16_t> pitch{0}, yaw{0}, roll{0};
};

int16_t be16(const uint8_t* p) {
    return static_cast<int16_t>((static_cast<uint16_t>(p[0]) << 8) | p[1]);
}

std::wstring findVendorPath() {
    GUID hidGuid{};
    HidD_GetHidGuid(&hidGuid);
    HDEVINFO info = SetupDiGetClassDevsW(&hidGuid, nullptr, nullptr,
        DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
    if (info == INVALID_HANDLE_VALUE) return {};

    std::wstring result;
    for (DWORD index = 0;; ++index) {
        SP_DEVICE_INTERFACE_DATA iface{sizeof(iface)};
        if (!SetupDiEnumDeviceInterfaces(info, nullptr, &hidGuid, index, &iface)) break;
        DWORD required = 0;
        SetupDiGetDeviceInterfaceDetailW(info, &iface, nullptr, 0, &required, nullptr);
        std::vector<uint8_t> storage(required);
        auto* detail = reinterpret_cast<SP_DEVICE_INTERFACE_DETAIL_DATA_W*>(storage.data());
        detail->cbSize = sizeof(*detail);
        if (!SetupDiGetDeviceInterfaceDetailW(info, &iface, detail, required, nullptr, nullptr)) continue;
        HANDLE handle = CreateFileW(detail->DevicePath, GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr);
        if (handle == INVALID_HANDLE_VALUE) continue;
        HIDD_ATTRIBUTES attrs{sizeof(attrs)};
        PHIDP_PREPARSED_DATA preparsed = nullptr;
        HIDP_CAPS caps{};
        const bool match = HidD_GetAttributes(handle, &attrs) &&
            attrs.VendorID == kGameSirVid && attrs.ProductID == kAimlabsDonglePid &&
            HidD_GetPreparsedData(handle, &preparsed) &&
            HidP_GetCaps(preparsed, &caps) == HIDP_STATUS_SUCCESS &&
            caps.UsagePage == kVendorUsagePage && caps.Usage == kVendorUsage;
        if (preparsed) HidD_FreePreparsedData(preparsed);
        CloseHandle(handle);
        if (match) {
            result = detail->DevicePath;
            break;
        }
    }
    SetupDiDestroyDeviceInfoList(info);
    return result;
}

void xinputLoop(DWORD userIndex, std::atomic<bool>& run, Counters& counters) {
    XINPUT_STATE previous{};
    bool havePrevious = false;
    while (run.load(std::memory_order_relaxed)) {
        XINPUT_STATE current{};
        if (XInputGetState(userIndex, &current) == ERROR_SUCCESS) {
            counters.xinputPolls.fetch_add(1, std::memory_order_relaxed);
            if (!havePrevious || std::memcmp(&current.Gamepad, &previous.Gamepad,
                                              sizeof(current.Gamepad)) != 0) {
                counters.xinputChanges.fetch_add(1, std::memory_order_relaxed);
                previous = current;
                havePrevious = true;
            }
        }
        YieldProcessor();
    }
}

void enhancedLoop(HANDLE device, std::atomic<bool>& run, Counters& counters) {
    uint8_t heartbeat[64]{};
    heartbeat[0] = 0x0f;
    heartbeat[1] = 0xf2;
    auto nextHeartbeat = std::chrono::steady_clock::now();
    uint8_t previousImu[12]{};
    bool haveImu = false;
    while (run.load(std::memory_order_relaxed)) {
        const auto now = std::chrono::steady_clock::now();
        if (now >= nextHeartbeat) {
            DWORD written = 0;
            WriteFile(device, heartbeat, sizeof(heartbeat), &written, nullptr);
            nextHeartbeat = now + std::chrono::milliseconds(500);
        }
        uint8_t report[64]{};
        DWORD received = 0;
        if (!ReadFile(device, report, sizeof(report), &received, nullptr)) break;
        if (received != 64 || report[0] != 0x12) continue;
        counters.enhancedReports.fetch_add(1, std::memory_order_relaxed);
        if (!haveImu || std::memcmp(previousImu, report + 14, sizeof(previousImu)) != 0) {
            std::memcpy(previousImu, report + 14, sizeof(previousImu));
            haveImu = true;
            counters.imuChanges.fetch_add(1, std::memory_order_relaxed);
        }
        uint8_t extras = 0;
        if (report[60] & 0x08) extras |= 0x01; // rear-left paddle
        if (report[60] & 0x10) extras |= 0x02; // rear-right paddle
        if (report[63] & 0x01) extras |= 0x04; // top-left extra
        if (report[63] & 0x02) extras |= 0x08; // top-right extra
        counters.extras.store(extras, std::memory_order_relaxed);
        counters.pitch.store(be16(report + 14), std::memory_order_relaxed);
        counters.yaw.store(be16(report + 16), std::memory_order_relaxed);
        counters.roll.store(be16(report + 18), std::memory_order_relaxed);
    }
}
} // namespace

int wmain(int argc, wchar_t** argv) {
    int seconds = 10;
    if (argc > 1) seconds = _wtoi(argv[1]);
    DWORD xinputUser = 0;
    if (argc > 2) xinputUser = static_cast<DWORD>(_wtoi(argv[2]));
    if (xinputUser >= XUSER_MAX_COUNT) {
        std::fprintf(stderr, "XInput user must be 0 through 3.\n");
        return 1;
    }
    const std::wstring path = findVendorPath();
    if (path.empty()) {
        std::fprintf(stderr, "Panda Pro: GameSir 3537:10C6 enhanced interface not found.\n");
        return 2;
    }
    HANDLE device = CreateFileW(path.c_str(), GENERIC_READ | GENERIC_WRITE,
        FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr);
    if (device == INVALID_HANDLE_VALUE) {
        std::fprintf(stderr, "Panda Pro: cannot open enhanced interface (error %lu).\n", GetLastError());
        return 3;
    }
    std::printf("Panda Pro 8K native bridge probe\n");
    std::printf("Physical input: GameSir 3537:10C6 (XInput + enhanced 0x12)\n");
    std::printf("XInput slots at startup:\n");
    for (DWORD i = 0; i < XUSER_MAX_COUNT; ++i) {
        XINPUT_STATE state{};
        const DWORD result = XInputGetState(i, &state);
        if (result == ERROR_SUCCESS) {
            const auto& g = state.Gamepad;
            std::printf("  user %lu: connected packet=%lu buttons=%04X sticks=(%d,%d,%d,%d) triggers=(%u,%u)%s\n",
                i, state.dwPacketNumber, g.wButtons, g.sThumbLX, g.sThumbLY,
                g.sThumbRX, g.sThumbRY, g.bLeftTrigger, g.bRightTrigger,
                i == xinputUser ? " [selected]" : "");
        } else {
            std::printf("  user %lu: not connected%s\n", i, i == xinputUser ? " [selected]" : "");
        }
    }
    std::printf("Running for %d seconds...\n", seconds);

    Counters counters;
    std::atomic<bool> run{true};
    std::thread xinput(xinputLoop, xinputUser, std::ref(run), std::ref(counters));
    std::thread enhanced(enhancedLoop, device, std::ref(run), std::ref(counters));
    uint64_t lastPolls = 0, lastChanges = 0, lastReports = 0, lastImu = 0;
    for (int i = 0; i < seconds; ++i) {
        Sleep(1000);
        const uint64_t polls = counters.xinputPolls.load();
        const uint64_t changes = counters.xinputChanges.load();
        const uint64_t reports = counters.enhancedReports.load();
        const uint64_t imu = counters.imuChanges.load();
        std::printf("%2ds xinput-api=%6llu/s fresh-states=%5llu/s enhanced=%3llu/s imu=%3llu/s "
                    "extras=%X gyro=(%d,%d,%d)\n", i + 1,
            static_cast<unsigned long long>(polls - lastPolls),
            static_cast<unsigned long long>(changes - lastChanges),
            static_cast<unsigned long long>(reports - lastReports),
            static_cast<unsigned long long>(imu - lastImu),
            counters.extras.load(), counters.pitch.load(), counters.yaw.load(), counters.roll.load());
        lastPolls = polls;
        lastChanges = changes;
        lastReports = reports;
        lastImu = imu;
    }
    run.store(false);
    CancelIoEx(device, nullptr);
    xinput.join();
    enhanced.join();
    CloseHandle(device);
    std::printf("Complete: XInput changes=%llu, enhanced reports=%llu, fresh IMU=%llu\n",
        static_cast<unsigned long long>(counters.xinputChanges.load()),
        static_cast<unsigned long long>(counters.enhancedReports.load()),
        static_cast<unsigned long long>(counters.imuChanges.load()));
    return 0;
}
