#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <hidsdi.h>
#include <setupapi.h>
#include <cstdint>
#include <cstring>
#include <string>
#include <vector>

#pragma comment(lib, "hid.lib")
#pragma comment(lib, "setupapi.lib")

static std::wstring FindEnhancedPath() {
    GUID guid{}; HidD_GetHidGuid(&guid);
    HDEVINFO info = SetupDiGetClassDevsW(&guid, nullptr, nullptr, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
    if (info == INVALID_HANDLE_VALUE) return {};
    std::wstring found;
    for (DWORD i = 0;; ++i) {
        SP_DEVICE_INTERFACE_DATA iface{sizeof(iface)};
        if (!SetupDiEnumDeviceInterfaces(info, nullptr, &guid, i, &iface)) break;
        DWORD required = 0;
        SetupDiGetDeviceInterfaceDetailW(info, &iface, nullptr, 0, &required, nullptr);
        std::vector<uint8_t> bytes(required);
        auto* detail = reinterpret_cast<SP_DEVICE_INTERFACE_DETAIL_DATA_W*>(bytes.data());
        detail->cbSize = sizeof(*detail);
        if (!SetupDiGetDeviceInterfaceDetailW(info, &iface, detail, required, nullptr, nullptr)) continue;
        HANDLE h = CreateFileW(detail->DevicePath, GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr);
        if (h == INVALID_HANDLE_VALUE) continue;
        HIDD_ATTRIBUTES attr{sizeof(attr)}; PHIDP_PREPARSED_DATA pp = nullptr; HIDP_CAPS caps{};
        bool haveAttributes = HidD_GetAttributes(h, &attr);
        bool supportedProduct = attr.ProductID == 0x10C5 || attr.ProductID == 0x10C6;
        bool match = haveAttributes && attr.VendorID == 0x3537 && supportedProduct &&
            HidD_GetPreparsedData(h, &pp) && HidP_GetCaps(pp, &caps) == HIDP_STATUS_SUCCESS &&
            caps.UsagePage == 0xFFF0 && caps.Usage == 0x0040;
        if (pp) HidD_FreePreparsedData(pp);
        CloseHandle(h);
        if (match) { found = detail->DevicePath; break; }
    }
    SetupDiDestroyDeviceInfoList(info);
    return found;
}

int wmain(int argc, wchar_t** argv) {
    const bool rawMode = argc > 1 && _wcsicmp(argv[1], L"--raw") == 0;
    SetHandleInformation(GetStdHandle(STD_OUTPUT_HANDLE), HANDLE_FLAG_INHERIT, 0);
    auto path = FindEnhancedPath();
    if (path.empty()) return 2;
    HANDLE device = CreateFileW(path.c_str(), GENERIC_READ | GENERIC_WRITE,
        FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr);
    if (device == INVALID_HANDLE_VALUE) return 3;
    uint8_t heartbeat[64]{}; heartbeat[0] = 0x0F; heartbeat[1] = 0xF2;
    DWORD written = 0; WriteFile(device, heartbeat, sizeof(heartbeat), &written, nullptr);
    ULONGLONG nextHeartbeat = GetTickCount64() + 500;
    HANDLE out = GetStdHandle(STD_OUTPUT_HANDLE);
    for (;;) {
        if (GetTickCount64() >= nextHeartbeat) {
            WriteFile(device, heartbeat, sizeof(heartbeat), &written, nullptr);
            nextHeartbeat = GetTickCount64() + 500;
        }
        uint8_t report[64]{}; DWORD received = 0;
        if (!ReadFile(device, report, sizeof(report), &received, nullptr)) break;
        if (received != 64 || report[0] != 0x12) continue;
        uint8_t extras = 0;
        if (report[60] & 0x08) extras |= 0x01; // rear-left -> L4
        if (report[60] & 0x10) extras |= 0x02; // rear-right -> R4
        if (report[63] & 0x01) extras |= 0x04; // top-left -> L5
        if (report[63] & 0x02) extras |= 0x08; // top-right -> R5

        if (rawMode) {
            // Diagnostics mode: decoded extras followed by the complete
            // 64-byte 0x12 report. The GUI updates at 20 Hz, so retaining
            // the raw packet here adds no rendering work to the hot path.
            uint8_t frame[65]{};
            frame[0] = extras;
            std::memcpy(frame + 1, report, sizeof(report));
            if (!WriteFile(out, frame, sizeof(frame), &written, nullptr) || written != sizeof(frame)) break;
            continue;
        }

        uint8_t frame[13]{};
        frame[0] = extras;
        // The XInput-mode enhanced report stores six little-endian IMU
        // channels starting at byte 13: gyro XYZ followed by accel XYZ.
        // Starting at byte 14 makes a stationary gyro look like +/-255-count
        // noise because it combines the wrong adjacent bytes.
        for (int i = 0; i < 12; ++i) frame[1 + i] = report[13 + i];
        if (!WriteFile(out, frame, sizeof(frame), &written, nullptr) || written != sizeof(frame)) break;
    }
    CloseHandle(device);
    return 0;
}
