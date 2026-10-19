using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ControllersOff.Domain;
using Microsoft.Win32;

namespace ControllersOff.Devices;

public sealed class SetupApiDeviceProvider : IDeviceProvider
{
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const uint DigcfAllClasses = 0x00000004;
    private const uint DevpropTypeDword = 0x00000012;
    private const uint HidpStatusSuccess = 0x00110000;
    private const uint DnHasProblem = 0x00000400;
    private const uint CmProbDisabled = 22;
    private const uint DifPropertyChange = 0x00000012;
    private const uint DicsEnable = 0x00000001;
    private const uint DicsDisable = 0x00000002;
    private const uint DicsFlagGlobal = 0x00000001;
    private const int ErrorDiDoDefault = unchecked((int)0xE000020B);

    private const int DevicePathOffset = sizeof(int);
    private static readonly int DeviceInterfaceDetailSize = IntPtr.Size == 8 ? 8 : 6;
    private static readonly Guid HidInterfaceGuid = new("4D1E55B2-F16F-11CF-88CB-001111000030");
    private static readonly Regex InfReferencePattern = new(@"^@[^;]*;(?<name>.+)$", RegexOptions.CultureInvariant);
    private static readonly DEVPROPKEY FriendlyNameKey = Key("A45C254E-DF1C-4EFD-8020-67D146A850E0", 2);

    private static DEVPROPKEY Key(string formatId, uint propertyId)
        => new() { FormatId = new Guid(formatId), PropertyId = propertyId };

    public string? LastError { get; private set; }

    public IReadOnlyList<HidDevice> Enumerate()
    {
        var devices = new List<HidDevice>();
        var interfaceGuid = HidInterfaceGuid;
        var deviceInfoSet = Native.SetupDiGetClassDevs(ref interfaceGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == Native.InvalidHandleValue)
        {
            return devices;
        }

        try
        {
            for (var index = 0u; QueryNextDevice(deviceInfoSet, interfaceGuid, index, out var interfaceData, out var deviceInfo); index++)
            {
                if (TryRead(deviceInfoSet, interfaceData, deviceInfo, out var device, out var failure))
                {
                    devices.Add(device);
                }
                else
                {
                    LastError = failure;
                }
            }
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return devices;
    }

    public bool SetEnabled(string instanceId, bool enabled)
    {
        if (!FindDevInst(instanceId, out var nodeDevInst))
        {
            return !enabled;
        }

        var physicalDevInst = ResolvePhysicalDevInst(nodeDevInst);
        var physicalId = GetDeviceId(physicalDevInst);
        if (string.IsNullOrEmpty(physicalId))
        {
            physicalId = instanceId;
        }

        var wholeDevice = physicalDevInst != nodeDevInst && !HasProtectedSibling(instanceId, physicalDevInst);
        Trace.Write($"'{instanceId}' node={nodeDevInst} physical={physicalDevInst} '{physicalId}' whole={wholeDevice} enable={enabled}");

        if (!enabled)
        {
            return SwitchByInstanceId(wholeDevice ? physicalId : instanceId, false);
        }

        var nodeOk = SwitchByInstanceId(instanceId, true);
        if (!wholeDevice)
        {
            return nodeOk;
        }

        return SwitchByInstanceId(physicalId, true) || nodeOk;
    }

    public bool EnableAllDisabled()
    {
        var fixedCount = 0;
        var success = true;
        foreach (var devInst in EnumerateSwitchableNodes())
        {
            if (!IsDisabled(devInst))
            {
                continue;
            }

            var id = GetDeviceId(devInst);
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            success &= SwitchByInstanceId(id, true);
            fixedCount++;
        }

        Trace.Write($"enable-all disabled nodes found={fixedCount} success={success}");
        return success;
    }

    private bool SwitchByInstanceId(string instanceId, bool enabled)
    {
        var deviceInfoSet = Native.SetupDiGetClassDevsAll(DigcfAllClasses);
        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == Native.InvalidHandleValue)
        {
            LastError = "SetupDiGetClassDevs failed: " + Marshal.GetLastWin32Error();
            return false;
        }

        try
        {
            if (!TryFindDeviceInfo(deviceInfoSet, instanceId, out var deviceInfo))
            {
                LastError = "device '" + instanceId + "' not found";
                return false;
            }

            var propChange = new SP_PROPCHANGE_PARAMS
            {
                ClassInstallHeader = new SP_CLASSINSTALL_HEADER
                {
                    cbSize = (uint)Marshal.SizeOf<SP_CLASSINSTALL_HEADER>(),
                    InstallFunction = DifPropertyChange
                },
                StateChange = enabled ? DicsEnable : DicsDisable,
                Scope = DicsFlagGlobal,
                HwProfile = 0
            };

            if (!Native.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfo, ref propChange, (uint)Marshal.SizeOf<SP_PROPCHANGE_PARAMS>()))
            {
                LastError = "SetupDiSetClassInstallParams failed: " + Marshal.GetLastWin32Error();
                return false;
            }

            if (!Native.SetupDiCallClassInstaller(DifPropertyChange, deviceInfoSet, ref deviceInfo))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != ErrorDiDoDefault)
                {
                    LastError = "SetupDiCallClassInstaller failed: " + error;
                    return false;
                }
            }

            return true;
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private bool FindDevInst(string instanceId, out uint devInst)
    {
        devInst = 0;
        var deviceInfoSet = Native.SetupDiGetClassDevsAll(DigcfAllClasses);
        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == Native.InvalidHandleValue)
        {
            LastError = "SetupDiGetClassDevs failed: " + Marshal.GetLastWin32Error();
            return false;
        }

        try
        {
            if (!TryFindDeviceInfo(deviceInfoSet, instanceId, out var deviceInfo))
            {
                LastError = $"device '{instanceId}' not found";
                return false;
            }

            devInst = deviceInfo.DevInst;
            return true;
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static bool TryFindDeviceInfo(IntPtr deviceInfoSet, string instanceId, out SP_DEVINFO_DATA deviceInfo)
    {
        for (var index = 0u; ; index++)
        {
            deviceInfo = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            if (!Native.SetupDiEnumDeviceInfo(deviceInfoSet, index, ref deviceInfo))
            {
                return false;
            }

            var candidate = deviceInfo;
            if (TryGetInstanceId(deviceInfoSet, ref candidate, out var candidateId)
                && string.Equals(candidateId, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
    }

    private static IEnumerable<uint> EnumerateSwitchableNodes()
    {
        var deviceInfoSet = Native.SetupDiGetClassDevsAll(DigcfAllClasses);
        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == Native.InvalidHandleValue)
        {
            yield break;
        }

        try
        {
            for (var index = 0u; ; index++)
            {
                var deviceInfo = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
                if (!Native.SetupDiEnumDeviceInfo(deviceInfoSet, index, ref deviceInfo))
                {
                    yield break;
                }

                var candidate = deviceInfo;
                if (TryGetInstanceId(deviceInfoSet, ref candidate, out var instanceId) && IsRecoverableNode(instanceId))
                {
                    yield return deviceInfo.DevInst;
                }
            }
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static uint ResolvePhysicalDevInst(uint devInst)
    {
        // Composite devices expose one HID node per interface, so the whole USB device is the real target.
        // Walking past it would reach the root hub and take every USB device down with it.
        var current = devInst;
        for (var hop = 0; hop < 4; hop++)
        {
            if (!TryReadDevInst(slot => Native.CM_Get_Parent(slot, current, 0), out var parent) || parent == 0 || parent == current)
            {
                break;
            }

            var parentId = GetDeviceId(parent);
            if (!IsUsbDeviceNode(parentId))
            {
                break;
            }

            Trace.Write($"hop {hop + 1}: {GetDeviceId(current)} -> {parentId}");
            current = parent;
        }

        return current;
    }

    private bool HasProtectedSibling(string instanceId, uint physicalDevInst)
    {
        var interfaceGuid = HidInterfaceGuid;
        var deviceInfoSet = Native.SetupDiGetClassDevs(ref interfaceGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == Native.InvalidHandleValue)
        {
            // Keep the node-level target when siblings cannot be inspected: never risk taking a keyboard down.
            return true;
        }

        try
        {
            for (var index = 0u; QueryNextDevice(deviceInfoSet, interfaceGuid, index, out var interfaceData, out var deviceInfo); index++)
            {
                var candidate = deviceInfo;
                if (!TryGetInstanceId(deviceInfoSet, ref candidate, out var siblingId)
                    || string.Equals(siblingId, instanceId, StringComparison.OrdinalIgnoreCase)
                    || ResolvePhysicalDevInst(deviceInfo.DevInst) != physicalDevInst)
                {
                    continue;
                }

                var capsRef = deviceInfo;
                if (!TryReadCapabilities(deviceInfoSet, interfaceData, ref capsRef, out var caps))
                {
                    continue;
                }

                if (HidUsage.Classify(caps.UsagePage, caps.Usage) is DeviceCategory.Keyboard or DeviceCategory.Mouse)
                {
                    LastError = null;
                    return true;
                }
            }
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        LastError = null;
        return false;
    }

    private static bool IsUsbDeviceNode(string deviceId)
        => deviceId.StartsWith("USB", StringComparison.OrdinalIgnoreCase)
           && !deviceId.StartsWith("USB\\ROOT_HUB", StringComparison.OrdinalIgnoreCase);

    private static bool IsRecoverableNode(string deviceId)
        => IsUsbDeviceNode(deviceId) || deviceId.StartsWith("HID", StringComparison.OrdinalIgnoreCase);

    private static string GetDeviceId(uint devInst)
    {
        var buffer = Marshal.AllocHGlobal(512);
        try
        {
            return Native.CM_Get_Device_ID(devInst, buffer, 256, 0) == 0
                ? Marshal.PtrToStringUni(buffer) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool TryReadDevInst(Func<IntPtr, int> locate, out uint devInst)
    {
        devInst = 0;
        var slot = Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            if (locate(slot) != 0)
            {
                return false;
            }

            devInst = (uint)Marshal.ReadInt32(slot);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(slot);
        }
    }

    private static bool QueryNextDevice(IntPtr deviceInfoSet, Guid interfaceGuid, uint index, out SP_DEVICE_INTERFACE_DATA interfaceData, out SP_DEVINFO_DATA deviceInfo)
    {
        interfaceData = new SP_DEVICE_INTERFACE_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
        deviceInfo = default;
        if (!Native.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref interfaceGuid, index, ref interfaceData))
        {
            return false;
        }

        deviceInfo = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
        // The detail buffer is only queried for its size; the call is expected to fail while still filling DeviceInfo.
        Native.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out _, ref deviceInfo);
        return true;
    }

    private bool TryRead(IntPtr deviceInfoSet, SP_DEVICE_INTERFACE_DATA interfaceData, SP_DEVINFO_DATA deviceInfo, out HidDevice device, out string? failure)
    {
        device = null!;
        failure = null;
        var deviceInfoRef = deviceInfo;
        if (!TryGetInstanceId(deviceInfoSet, ref deviceInfoRef, out var instanceId))
        {
            failure = $"instance id failed: {Marshal.GetLastWin32Error()}";
            return false;
        }

        if (!TryReadCapabilities(deviceInfoSet, interfaceData, ref deviceInfoRef, out var caps))
        {
            failure = LastError ?? $"{instanceId}: caps failed: {Marshal.GetLastWin32Error()}";
            return false;
        }

        device = new HidDevice(
            instanceId,
            ReadFriendlyName(deviceInfoSet, deviceInfo, instanceId) ?? instanceId,
            caps.VendorId,
            caps.ProductId,
            caps.UsagePage,
            caps.Usage,
            !IsDisabled(deviceInfo.DevInst));
        return true;
    }

    private static bool TryGetInstanceId(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfo, out string instanceId)
    {
        instanceId = string.Empty;
        var buffer = Marshal.AllocHGlobal(512);
        try
        {
            if (!Native.SetupDiGetDeviceInstanceId(deviceInfoSet, ref deviceInfo, buffer, 512, out _))
            {
                return false;
            }

            instanceId = Marshal.PtrToStringUni(buffer) ?? string.Empty;
            return instanceId.Length > 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private string? ReadFriendlyName(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfo, string instanceId)
    {
        var buffer = new byte[4096];
        var type = DevpropTypeDword;
        var key = FriendlyNameKey;
        if (Native.SetupDiGetDeviceProperty(deviceInfoSet, ref deviceInfo, ref key, ref type, buffer, (uint)buffer.Length, out _))
        {
            // Multi-string device properties come back with the remaining entries glued to the first one.
            var text = System.Text.Encoding.Unicode.GetString(buffer);
            var end = text.IndexOf('\0');
            if ((end < 0 ? text : text[..end]).Trim() is { Length: > 0 } name)
            {
                return name;
            }
        }

        return ReadDeviceDescription(instanceId);
    }

    private static string? ReadDeviceDescription(string instanceId)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\{instanceId}");
        if (key?.GetValue("DeviceDesc") is not string description)
        {
            return null;
        }

        // Devices without a real name store an INF reference like "@input.inf,%token%;Name".
        var match = InfReferencePattern.Match(description);
        return (match.Success ? match.Groups["name"].Value : description).Trim() is { Length: > 0 } name
            ? name
            : null;
    }

    private static bool IsDisabled(uint devInst)
        => Native.CM_Get_DevNode_Status(out var status, out var problem, devInst, 0) == 0
           && (status & DnHasProblem) != 0
           && problem == CmProbDisabled;

    private bool TryReadCapabilities(IntPtr deviceInfoSet, SP_DEVICE_INTERFACE_DATA interfaceData, ref SP_DEVINFO_DATA deviceInfo, out HidCapabilities caps)
    {
        caps = default;
        var devicePath = ReadDevicePath(deviceInfoSet, ref interfaceData, ref deviceInfo);
        if (devicePath is null)
        {
            return false;
        }

        var handle = Native.CreateFile(devicePath, 0, FileShare.ReadWrite | FileShare.Delete, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
        if (handle == Native.InvalidHandleValue)
        {
            LastError = $"CreateFile failed for '{devicePath}': {Marshal.GetLastWin32Error()}";
            return false;
        }

        try
        {
            var attributes = new HIDD_ATTRIBUTES { Size = Marshal.SizeOf<HIDD_ATTRIBUTES>() };
            if (!Native.HidD_GetAttributes(handle, ref attributes))
            {
                LastError = $"HidD_GetAttributes failed: {Marshal.GetLastWin32Error()}";
                return false;
            }

            if (!Native.HidD_GetPreparsedData(handle, out var preparsed))
            {
                LastError = $"HidD_GetPreparsedData failed: {Marshal.GetLastWin32Error()}";
                return false;
            }

            try
            {
                var status = Native.HidP_GetCaps(preparsed, out var hidCaps);
                if (status != HidpStatusSuccess)
                {
                    LastError = $"HidP_GetCaps returned 0x{status:X8}";
                    return false;
                }

                caps = new HidCapabilities(attributes.VendorID, attributes.ProductID, hidCaps.UsagePage, hidCaps.Usage);
                return true;
            }
            finally
            {
                Native.HidD_FreePreparsedData(preparsed);
            }
        }
        finally
        {
            Native.CloseHandle(handle);
        }
    }

    private string? ReadDevicePath(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA interfaceData, ref SP_DEVINFO_DATA deviceInfo)
    {
        Native.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out var required, ref deviceInfo);
        if (required == 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)Math.Max(required, DeviceInterfaceDetailSize + 2));
        try
        {
            Marshal.WriteInt32(buffer, DeviceInterfaceDetailSize);
            return Native.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, buffer, required, out _, ref deviceInfo)
                ? Marshal.PtrToStringUni(buffer + DevicePathOffset)
                : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private readonly record struct HidCapabilities(ushort VendorId, ushort ProductId, ushort UsagePage, ushort Usage);

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public uint cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_CLASSINSTALL_HEADER
    {
        public uint cbSize;
        public uint InstallFunction;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_PROPCHANGE_PARAMS
    {
        public SP_CLASSINSTALL_HEADER ClassInstallHeader;
        public uint StateChange;
        public uint Scope;
        public uint HwProfile;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDD_ATTRIBUTES
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    private static class Native
    {
        public static readonly IntPtr InvalidHandleValue = new(-1);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr parent, uint flags);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevsAllClasses(IntPtr classGuid, IntPtr enumerator, IntPtr parent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint index, ref SP_DEVINFO_DATA deviceInfo);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfo, ref Guid interfaceClassGuid, uint index, ref SP_DEVICE_INTERFACE_DATA interfaceData);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInterfaceDetailW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA interfaceData, IntPtr detail, uint detailSize, out uint requiredSize, ref SP_DEVINFO_DATA deviceInfo);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInstanceIdW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfo, IntPtr instanceId, int instanceIdSize, out int requiredSize);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDevicePropertyW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDevicePropertyRaw(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfo, ref DEVPROPKEY propertyKey, ref uint propertyType, byte[] buffer, uint bufferSize, out uint requiredSize, uint flags);

        [DllImport("setupapi.dll", EntryPoint = "SetupDiSetClassInstallParamsW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiSetClassInstallParams(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfo, ref SP_PROPCHANGE_PARAMS installParams, uint installParamsSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiCallClassInstaller(uint installFunction, IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfo);

        [DllImport("cfgmgr32.dll")]
        public static extern int CM_Get_Parent(IntPtr parentDevInst, uint devInst, uint flags);

        [DllImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_IDW", CharSet = CharSet.Unicode)]
        public static extern int CM_Get_Device_ID(uint devInst, IntPtr buffer, int bufferLength, uint flags);

        [DllImport("cfgmgr32.dll")]
        public static extern int CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, uint flags);

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateFile(string fileName, uint desiredAccess, FileShare shareMode, IntPtr securityAttributes, FileMode creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetAttributes(IntPtr device, ref HIDD_ATTRIBUTES attributes);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetPreparsedData(IntPtr device, out IntPtr preparsedData);

        [DllImport("hid.dll")]
        public static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        public static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

        public static IntPtr SetupDiGetClassDevsAll(uint flags)
            => SetupDiGetClassDevsAllClasses(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, flags);

        public static bool SetupDiGetDeviceProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfo, ref DEVPROPKEY propertyKey, ref uint propertyType, byte[] buffer, uint bufferSize, out uint requiredSize)
            => SetupDiGetDevicePropertyRaw(deviceInfoSet, ref deviceInfo, ref propertyKey, ref propertyType, buffer, bufferSize, out requiredSize, 0);
    }
}
