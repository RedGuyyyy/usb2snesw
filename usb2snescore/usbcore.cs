using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Management;

namespace usb2snes {

    public enum usbint_server_opcode_e
    {
        // address space operations
        GET = 0,
        PUT,
        VGET,
        VPUT,

        // file system operations
        LS,
        MKDIR,
        RM,
        MV,

        // special operations
        RESET,
        BOOT,
        POWER_CYCLE,
        INFO,
        MENU_RESET,
        STREAM,
        TIME,

        // response
        RESPONSE,
    };

    public enum usbint_server_space_e
    {
        FILE = 0,
        SNES,
        MSU,
        CMD,
        CONFIG,
    };

    public enum usbint_server_flags_e
    {
        NONE = 0,
        SKIPRESET = 1,
        ONLYRESET = 2,
        CLRX = 4,
        SETX = 8,
        NORESP = 64,
        DATA64B = 128,
    };

    public class core
    {

        public core()
        {
            serialPort = new System.IO.Ports.SerialPort();
        }

        public bool Connected()
        {
            return serialPort.IsOpen;
        }

        public string PortName()
        {
            return serialPort.PortName;
        }

        public class Port : IComparable<Port>
        {
            public string Name;
            public string Desc;

            public Port(string name, string desc)
            {
                Name = name;
                Desc = desc;
            }

            public override string ToString()
            {
                return Desc;
            }

            public int CompareTo(Port p)
            {
                if (p == null)
                    return 1;

                return Name.CompareTo(p.Name);
            }
        }

        public static List<Port> GetDeviceList()
        {
            List<Port> portList = new List<Port>();

            var deviceList = utils.Win32DeviceMgmt.GetAllCOMPorts();
            foreach (var device in deviceList)
            {
                if (device.bus_description.Contains("sd2snes"))
                {
                    portList.Add(new Port(device.name.Trim(), device.name.Trim() + " | " + device.description.Trim()));
                }
            }
            portList.Sort();

            return portList;
        }

        public void Connect(string portName)
        {
            if (serialPort.IsOpen) Disconnect();
            serialPort = new SerialPort();
            serialPort.PortName = portName;
            serialPort.BaudRate = 9600;
            serialPort.Parity = Parity.None;
            serialPort.DataBits = 8;
            serialPort.StopBits = StopBits.One;
            serialPort.Handshake = Handshake.None;

            // Support long timeout (infinite may lock up app)
            serialPort.ReadTimeout = 5000;
            serialPort.WriteTimeout = 5000;

            serialPort.DtrEnable = true;
            serialPort.Open();
        }

        public void Reset()
        {
            bool dtr = serialPort.DtrEnable;
            serialPort.DtrEnable = false;
            System.Threading.Thread.Sleep(500);
            serialPort.DtrEnable = dtr;
        }

        public void Disconnect()
        {
            try { serialPort.DtrEnable = false; serialPort.Close(); } catch (Exception x) { serialPort = new SerialPort(); }
        }

        public object SendCommand(usbint_server_opcode_e opcode, usbint_server_space_e space, usbint_server_flags_e flags, params object[] args)
        {
            byte[] tBuffer = new byte[512];
            object ret = null;

            // send directory command
            Array.Clear(tBuffer, 0, tBuffer.Length);
            tBuffer[0] = Convert.ToByte('U');
            tBuffer[1] = Convert.ToByte('S');
            tBuffer[2] = Convert.ToByte('B');
            tBuffer[3] = Convert.ToByte('A'); // directory listing
            tBuffer[4] = Convert.ToByte(opcode); // opcode
            tBuffer[5] = Convert.ToByte(space); // space
            tBuffer[6] = Convert.ToByte(flags); // flags

            // assign arguments
            if (space == usbint_server_space_e.FILE)
            {
                switch (opcode)
                {
                    case usbint_server_opcode_e.GET:
                    case usbint_server_opcode_e.PUT:
                    case usbint_server_opcode_e.LS:
                    case usbint_server_opcode_e.MKDIR:
                    case usbint_server_opcode_e.RM:
                    case usbint_server_opcode_e.MV:
                    case usbint_server_opcode_e.BOOT:
                        {
                            // name
                            if (args[0] == null || !(args[0] is string)) throw new Exception("Command: " + opcode.ToString() + " missing arg[0] string");
                            var s = (string)args[0];
                            Buffer.BlockCopy(ASCIIEncoding.ASCII.GetBytes(s.ToArray()), 0, tBuffer, 256, Math.Min(255, s.Length));

                            if (opcode == usbint_server_opcode_e.MV)
                            {
                                if (args[1] == null || !(args[1] is string)) throw new Exception("Command: " + opcode.ToString() + " missing arg[1] string");
                                // new name
                                var n = (string)args[1];
                                Buffer.BlockCopy(ASCIIEncoding.ASCII.GetBytes(n.ToArray()), 0, tBuffer, 8, n.Length);
                            }
                            else if (opcode == usbint_server_opcode_e.PUT)
                            {
                                // size
                                if (args[1] == null || !(args[1] is uint)) throw new Exception("Command: " + opcode.ToString() + " missing arg[1] uint");
                                var size = (uint)args[1];
                                tBuffer[252] = Convert.ToByte((size >> 24) & 0xFF);
                                tBuffer[253] = Convert.ToByte((size >> 16) & 0xFF);
                                tBuffer[254] = Convert.ToByte((size >> 8) & 0xFF);
                                tBuffer[255] = Convert.ToByte((size >> 0) & 0xFF);
                            }

                            break;
                        }

                    // passthrough command
                    case usbint_server_opcode_e.RESET:
                    case usbint_server_opcode_e.MENU_RESET:
                    case usbint_server_opcode_e.INFO:
                    case usbint_server_opcode_e.STREAM:
                    case usbint_server_opcode_e.POWER_CYCLE:
                        break;

                    //case usbint_server_opcode_e.EXECUTE:
                    //case usbint_server_opcode_e.ATOMIC:
                    //case usbint_server_opcode_e.MENU_LOCK:
                    //case usbint_server_opcode_e.MENU_UNLOCK:
                    //case usbint_server_opcode_e.MENU_RESET:
                    //case usbint_server_opcode_e.EXE:
                    //case usbint_server_opcode_e.TIME:

                    default:
                        throw new Exception("Unhandled Command: " + opcode.ToString() + " space: " + space.ToString() + " flags: " + flags.ToString());
                }
            }
            else
            {
                switch (opcode)
                {
                    case usbint_server_opcode_e.GET:
                    case usbint_server_opcode_e.PUT:
                        {
                            // offset
                            if (args[0] == null || !(args[0] is uint)) throw new Exception("Command: " + opcode.ToString() + " missing arg[0] uint");
                            var offset = (uint)args[0];
                            tBuffer[256] = Convert.ToByte((offset >> 24) & 0xFF);
                            tBuffer[257] = Convert.ToByte((offset >> 16) & 0xFF);
                            tBuffer[258] = Convert.ToByte((offset >> 8) & 0xFF);
                            tBuffer[259] = Convert.ToByte((offset >> 0) & 0xFF);

                            // size
                            if (args[1] == null || !(args[1] is uint)) throw new Exception("Command: " + opcode.ToString() + " missing arg[1] uint");
                            var size = (uint)args[1];
                            tBuffer[252] = Convert.ToByte((size >> 24) & 0xFF);
                            tBuffer[253] = Convert.ToByte((size >> 16) & 0xFF);
                            tBuffer[254] = Convert.ToByte((size >> 8) & 0xFF);
                            tBuffer[255] = Convert.ToByte((size >> 0) & 0xFF);

                            break;
                        }
                    case usbint_server_opcode_e.VGET:
                    case usbint_server_opcode_e.VPUT:
                        {
                            if (args.Length == 0 || args.Length > 8) throw new Exception("Command: " + opcode.ToString() + " need 2 <= args <= 16 and a multiple of 2.  Format: (size0, offset0), ...");
                            uint i = 0;
                            foreach (var a in args) {
                                var t = (Tuple<int, int>)a;
                                Byte size = Convert.ToByte(t.Item2);
                                uint offset = Convert.ToUInt32(t.Item1);
                                tBuffer[32 + i * 4] = size;
                                tBuffer[33 + i * 4] = Convert.ToByte((offset >> 16) & 0xFF);
                                tBuffer[34 + i * 4] = Convert.ToByte((offset >> 8) & 0xFF);
                                tBuffer[35 + i * 4] = Convert.ToByte((offset >> 0) & 0xFF);
                                i++;
                            }

                            break;
                        }
                    // passthrough command
                    case usbint_server_opcode_e.STREAM:
                    case usbint_server_opcode_e.RESET:
                    case usbint_server_opcode_e.MENU_RESET:
                    case usbint_server_opcode_e.INFO:
                    case usbint_server_opcode_e.POWER_CYCLE:
                        break;

                    default:
                        throw new Exception("Unhandled Request: " + opcode.ToString() + " space: " + space.ToString() + " flags: " + flags.ToString());
                }
            }

            // handle 64B commands here
            int cmdSize = (opcode == usbint_server_opcode_e.VGET || opcode == usbint_server_opcode_e.VPUT) ? 64 : tBuffer.Length;
            serialPort.Write(tBuffer, 0, cmdSize);

            // read response command
            int curSize = 0;
            if ((flags & usbint_server_flags_e.NORESP) == 0) {
                Array.Clear(tBuffer, 0, tBuffer.Length);
                while (curSize < 512) curSize += serialPort.Read(tBuffer, (curSize % 512), 512 - (curSize % 512));
                if (tBuffer[0] != 'U' || tBuffer[1] != 'S' || tBuffer[2] != 'B' || tBuffer[3] != 'A' || tBuffer[4] != Convert.ToByte(usbint_server_opcode_e.RESPONSE) || tBuffer[5] == 1)
                    throw new Exception("Response Error Request: " + opcode.ToString() + " space: " + space.ToString() + " flags: " + flags.ToString() + " Response: " + tBuffer[4].ToString());
            }

            // handle response
            switch (opcode)
            {
                case usbint_server_opcode_e.INFO:
                    {
                        List<string> sL = new List<string>();
                        var s = System.Text.Encoding.UTF8.GetString(tBuffer, 256 + 4, Array.IndexOf<byte>(tBuffer, 0, 256 + 4) - (256 + 4));
                        sL.Add(s);
                        int v = (tBuffer[256] << 24) | (tBuffer[257] << 16) | (tBuffer[258] << 8) | (tBuffer[259] << 0);
                        s = v.ToString("X");
                        sL.Add(s);
                        ret = sL;
                        break;
                    }
                case usbint_server_opcode_e.LS:
                    {
                        int type = 0;
                        var list = new List<Tuple<int, string>>();
                        ret = list;

                        // read directory listing packets
                        do
                        {
                            int bytesRead = serialPort.Read(tBuffer, (curSize % 512), 512 - (curSize % 512));

                            if (bytesRead != 0)
                            {
                                curSize += bytesRead;

                                if (curSize % 512 == 0)
                                {
                                    // parse strings
                                    for (int i = 0; i < 512;)
                                    {
                                        type = tBuffer[i++];

                                        if (type == 0 || type == 1)
                                        {
                                            string name = "";

                                            while (tBuffer[i] != 0x0)
                                            {
                                                name += (char)tBuffer[i++];
                                            }
                                            i++;

                                            list.Add(Tuple.Create(type, name));
                                        }
                                        else if (type == 2 || type == 0xFF)
                                        {
                                            // continued on the next packet
                                            break;
                                        }
                                        else
                                        {
                                            throw new IndexOutOfRangeException();
                                        }
                                    }
                                }
                            }
                        } while (type != 0xFF);

                        break;
                    }
                case usbint_server_opcode_e.VGET:
                case usbint_server_opcode_e.GET:
                    {
                        int fileSize = 0;
                        fileSize |= tBuffer[252]; fileSize <<= 8;
                        fileSize |= tBuffer[253]; fileSize <<= 8;
                        fileSize |= tBuffer[254]; fileSize <<= 8;
                        fileSize |= tBuffer[255]; fileSize <<= 0;
                        ret = fileSize;
                        break;
                    }
                default: break;
            }

            return ret;
        }

        public void SendData(byte[] data, int size)
        {
            if (size > data.Length) throw new Exception("Bytes to send larger than array size");
            // always send the full array
            serialPort.Write(data, 0, size);
        }

        public int GetData(byte[] data, int offset, int length)
        {
            return serialPort.Read(data, offset, length);
        }

        public SerialPort serialPort;
    }

}

namespace usb2snes.utils
{
    using System;
    using System.Text;
    using System.Runtime.InteropServices;
    using System.Collections.Generic;

    public class Win32DeviceMgmt
    {
        [Flags]
        public enum DiGetClassFlags : uint
        {
            DIGCF_DEFAULT = 0x00000001,  // only valid with DIGCF_DEVICEINTERFACE
            DIGCF_PRESENT = 0x00000002,
            DIGCF_ALLCLASSES = 0x00000004,
            DIGCF_PROFILE = 0x00000008,
            DIGCF_DEVICEINTERFACE = 0x00000010,
        }
        /// <summary>
        /// Device registry property codes
        /// </summary>
        public enum SPDRP : uint
        {
            /// <summary>
            /// DeviceDesc (R/W)
            /// </summary>
            SPDRP_DEVICEDESC = 0x00000000,

            /// <summary>
            /// HardwareID (R/W)
            /// </summary>
            SPDRP_HARDWAREID = 0x00000001,

            /// <summary>
            /// CompatibleIDs (R/W)
            /// </summary>
            SPDRP_COMPATIBLEIDS = 0x00000002,

            /// <summary>
            /// unused
            /// </summary>
            SPDRP_UNUSED0 = 0x00000003,

            /// <summary>
            /// Service (R/W)
            /// </summary>
            SPDRP_SERVICE = 0x00000004,

            /// <summary>
            /// unused
            /// </summary>
            SPDRP_UNUSED1 = 0x00000005,

            /// <summary>
            /// unused
            /// </summary>
            SPDRP_UNUSED2 = 0x00000006,

            /// <summary>
            /// Class (R--tied to ClassGUID)
            /// </summary>
            SPDRP_CLASS = 0x00000007,

            /// <summary>
            /// ClassGUID (R/W)
            /// </summary>
            SPDRP_CLASSGUID = 0x00000008,

            /// <summary>
            /// Driver (R/W)
            /// </summary>
            SPDRP_DRIVER = 0x00000009,

            /// <summary>
            /// ConfigFlags (R/W)
            /// </summary>
            SPDRP_CONFIGFLAGS = 0x0000000A,

            /// <summary>
            /// Mfg (R/W)
            /// </summary>
            SPDRP_MFG = 0x0000000B,

            /// <summary>
            /// FriendlyName (R/W)
            /// </summary>
            SPDRP_FRIENDLYNAME = 0x0000000C,

            /// <summary>
            /// LocationInformation (R/W)
            /// </summary>
            SPDRP_LOCATION_INFORMATION = 0x0000000D,

            /// <summary>
            /// PhysicalDeviceObjectName (R)
            /// </summary>
            SPDRP_PHYSICAL_DEVICE_OBJECT_NAME = 0x0000000E,

            /// <summary>
            /// Capabilities (R)
            /// </summary>
            SPDRP_CAPABILITIES = 0x0000000F,

            /// <summary>
            /// UiNumber (R)
            /// </summary>
            SPDRP_UI_NUMBER = 0x00000010,

            /// <summary>
            /// UpperFilters (R/W)
            /// </summary>
            SPDRP_UPPERFILTERS = 0x00000011,

            /// <summary>
            /// LowerFilters (R/W)
            /// </summary>
            SPDRP_LOWERFILTERS = 0x00000012,

            /// <summary>
            /// BusTypeGUID (R)
            /// </summary>
            SPDRP_BUSTYPEGUID = 0x00000013,

            /// <summary>
            /// LegacyBusType (R)
            /// </summary>
            SPDRP_LEGACYBUSTYPE = 0x00000014,

            /// <summary>
            /// BusNumber (R)
            /// </summary>
            SPDRP_BUSNUMBER = 0x00000015,

            /// <summary>
            /// Enumerator Name (R)
            /// </summary>
            SPDRP_ENUMERATOR_NAME = 0x00000016,

            /// <summary>
            /// Security (R/W, binary form)
            /// </summary>
            SPDRP_SECURITY = 0x00000017,

            /// <summary>
            /// Security (W, SDS form)
            /// </summary>
            SPDRP_SECURITY_SDS = 0x00000018,

            /// <summary>
            /// Device Type (R/W)
            /// </summary>
            SPDRP_DEVTYPE = 0x00000019,

            /// <summary>
            /// Device is exclusive-access (R/W)
            /// </summary>
            SPDRP_EXCLUSIVE = 0x0000001A,

            /// <summary>
            /// Device Characteristics (R/W)
            /// </summary>
            SPDRP_CHARACTERISTICS = 0x0000001B,

            /// <summary>
            /// Device Address (R)
            /// </summary>
            SPDRP_ADDRESS = 0x0000001C,

            /// <summary>
            /// UiNumberDescFormat (R/W)
            /// </summary>
            SPDRP_UI_NUMBER_DESC_FORMAT = 0X0000001D,

            /// <summary>
            /// Device Power Data (R)
            /// </summary>
            SPDRP_DEVICE_POWER_DATA = 0x0000001E,

            /// <summary>
            /// Removal Policy (R)
            /// </summary>
            SPDRP_REMOVAL_POLICY = 0x0000001F,

            /// <summary>
            /// Hardware Removal Policy (R)
            /// </summary>
            SPDRP_REMOVAL_POLICY_HW_DEFAULT = 0x00000020,

            /// <summary>
            /// Removal Policy Override (RW)
            /// </summary>
            SPDRP_REMOVAL_POLICY_OVERRIDE = 0x00000021,

            /// <summary>
            /// Device Install State (R)
            /// </summary>
            SPDRP_INSTALL_STATE = 0x00000022,

            /// <summary>
            /// Device Location Paths (R)
            /// </summary>
            SPDRP_LOCATION_PATHS = 0x00000023,
        }
        private const UInt32 DICS_FLAG_GLOBAL = 0x00000001;
        private const UInt32 DIREG_DEV = 0x00000001;
        private const UInt32 KEY_QUERY_VALUE = 0x0001;

        /// <summary>
        /// The SP_DEVINFO_DATA structure defines a device instance that is a member of a device information set.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public UInt32 cbSize;
            public Guid ClassGuid;
            public UInt32 DevInst;
            public UIntPtr Reserved;
        };


        [StructLayout(LayoutKind.Sequential)]
        struct DEVPROPKEY
        {
            public Guid fmtid;
            public UInt32 pid;
        }


        [DllImport("setupapi.dll")]
        private static extern Int32 SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, UInt32 MemberIndex, ref SP_DEVINFO_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid gClass, UInt32 iEnumerator, UInt32 hParent, DiGetClassFlags nFlags);

        [DllImport("Setupapi", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetupDiOpenDevRegKey(IntPtr hDeviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint scope,
            uint hwProfile, uint parameterRegistryValueKind, uint samDesired);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryValueExW", SetLastError = true)]
        private static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, int lpReserved, out uint lpType,
            byte[] lpData, ref uint lpcbData);

        [DllImport("advapi32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int RegCloseKey(IntPtr hKey);

        [DllImport("kernel32.dll")]
        private static extern Int32 GetLastError();

        const int BUFFER_SIZE = 1024;

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiClassGuidsFromName(string ClassName,
            ref Guid ClassGuidArray1stItem, UInt32 ClassGuidArraySize,
            out UInt32 RequiredSize);

        [DllImport("setupapi.dll")]
        private static extern Int32 SetupDiClassNameFromGuid(ref Guid ClassGuid,
            StringBuilder className, Int32 ClassNameSize, ref Int32 RequiredSize);

        /// <summary>
        /// The SetupDiGetDeviceRegistryProperty function retrieves the specified device property.
        /// This handle is typically returned by the SetupDiGetClassDevs or SetupDiGetClassDevsEx function.
        /// </summary>
        /// <param Name="DeviceInfoSet">Handle to the device information set that contains the interface and its underlying device.</param>
        /// <param Name="DeviceInfoData">Pointer to an SP_DEVINFO_DATA structure that defines the device instance.</param>
        /// <param Name="Property">Device property to be retrieved. SEE MSDN</param>
        /// <param Name="PropertyRegDataType">Pointer to a variable that receives the registry data Type. This parameter can be NULL.</param>
        /// <param Name="PropertyBuffer">Pointer to a buffer that receives the requested device property.</param>
        /// <param Name="PropertyBufferSize">Size of the buffer, in bytes.</param>
        /// <param Name="RequiredSize">Pointer to a variable that receives the required buffer size, in bytes. This parameter can be NULL.</param>
        /// <returns>If the function succeeds, the return value is nonzero.</returns>
        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            SPDRP Property,
            out UInt32 PropertyRegDataType,
            byte[] PropertyBuffer,
            uint PropertyBufferSize,
            out UInt32 RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiGetDevicePropertyW(
            IntPtr deviceInfoSet,
            [In] ref SP_DEVINFO_DATA DeviceInfoData,
            [In] ref DEVPROPKEY propertyKey,
            [Out] out UInt32 propertyType,
            byte[] propertyBuffer,
            UInt32 propertyBufferSize,
            out UInt32 requiredSize,
            UInt32 flags);

        const int utf16terminatorSize_bytes = 2;

        public struct DeviceInfo
        {
            public string name;
            public string description;
            public string bus_description;
        }

        static DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc;

        static Win32DeviceMgmt()
        {
            DEVPKEY_Device_BusReportedDeviceDesc = new DEVPROPKEY();
            DEVPKEY_Device_BusReportedDeviceDesc.fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2);
            DEVPKEY_Device_BusReportedDeviceDesc.pid = 4;
        }

        public static List<DeviceInfo> GetAllCOMPorts()
        {
            Guid[] guids = GetClassGUIDs("Ports");
            List<DeviceInfo> devices = new List<DeviceInfo>();
            for (int index = 0; index < guids.Length; index++)
            {
                IntPtr hDeviceInfoSet = SetupDiGetClassDevs(ref guids[index], 0, 0, DiGetClassFlags.DIGCF_PRESENT);
                if (hDeviceInfoSet == IntPtr.Zero)
                {
                    throw new Exception("Failed to get device information set for the COM ports");
                }

                try
                {
                    UInt32 iMemberIndex = 0;
                    while (true)
                    {
                        SP_DEVINFO_DATA deviceInfoData = new SP_DEVINFO_DATA();
                        deviceInfoData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                        bool success = SetupDiEnumDeviceInfo(hDeviceInfoSet, iMemberIndex, ref deviceInfoData);
                        if (!success)
                        {
                            // No more devices in the device information set
                            break;
                        }

                        DeviceInfo deviceInfo = new DeviceInfo();
                        deviceInfo.name = GetDeviceName(hDeviceInfoSet, deviceInfoData);
                        deviceInfo.description = GetDeviceDescription(hDeviceInfoSet, deviceInfoData);
                        try
                        {
                            deviceInfo.bus_description = GetDeviceBusDescription(hDeviceInfoSet, deviceInfoData);
                            devices.Add(deviceInfo);
                        }
                        catch (Exception e)
                        {
                            // ignore device that excepts
                        }

                        iMemberIndex++;
                    }
                }
                finally
                {
                    SetupDiDestroyDeviceInfoList(hDeviceInfoSet);
                }
            }
            return devices;
        }

        private static string GetDeviceName(IntPtr pDevInfoSet, SP_DEVINFO_DATA deviceInfoData)
        {
            IntPtr hDeviceRegistryKey = SetupDiOpenDevRegKey(pDevInfoSet, ref deviceInfoData,
                DICS_FLAG_GLOBAL, 0, DIREG_DEV, KEY_QUERY_VALUE);
            if (hDeviceRegistryKey == IntPtr.Zero)
            {
                throw new Exception("Failed to open a registry key for device-specific configuration information");
            }

            byte[] ptrBuf = new byte[BUFFER_SIZE];
            uint length = (uint)ptrBuf.Length;
            try
            {
                uint lpRegKeyType;
                int result = RegQueryValueEx(hDeviceRegistryKey, "PortName", 0, out lpRegKeyType, ptrBuf, ref length);
                if (result != 0)
                {
                    throw new Exception("Can not read registry value PortName for device " + deviceInfoData.ClassGuid);
                }
            }
            finally
            {
                RegCloseKey(hDeviceRegistryKey);
            }

            return Encoding.Unicode.GetString(ptrBuf, 0, (int)length - utf16terminatorSize_bytes);
        }

        private static string GetDeviceDescription(IntPtr hDeviceInfoSet, SP_DEVINFO_DATA deviceInfoData)
        {
            byte[] ptrBuf = new byte[BUFFER_SIZE];
            uint propRegDataType;
            uint RequiredSize;
            bool success = SetupDiGetDeviceRegistryProperty(hDeviceInfoSet, ref deviceInfoData, SPDRP.SPDRP_DEVICEDESC,
                out propRegDataType, ptrBuf, BUFFER_SIZE, out RequiredSize);
            if (!success)
            {
                throw new Exception("Can not read registry value PortName for device " + deviceInfoData.ClassGuid);
            }
            return Encoding.Unicode.GetString(ptrBuf, 0, (int)RequiredSize - utf16terminatorSize_bytes);
        }

        private static string GetDeviceBusDescription(IntPtr hDeviceInfoSet, SP_DEVINFO_DATA deviceInfoData)
        {
            byte[] ptrBuf = new byte[BUFFER_SIZE];
            uint propRegDataType;
            uint RequiredSize;
            bool success = SetupDiGetDevicePropertyW(hDeviceInfoSet, ref deviceInfoData, ref DEVPKEY_Device_BusReportedDeviceDesc,
                out propRegDataType, ptrBuf, BUFFER_SIZE, out RequiredSize, 0);
            if (!success)
            {
                throw new Exception("Can not read Bus provided device description device " + deviceInfoData.ClassGuid);
            }
            return System.Text.UnicodeEncoding.Unicode.GetString(ptrBuf, 0, (int)RequiredSize - utf16terminatorSize_bytes);
        }

        private static Guid[] GetClassGUIDs(string className)
        {
            UInt32 requiredSize = 0;
            Guid[] guidArray = new Guid[1];

            bool status = SetupDiClassGuidsFromName(className, ref guidArray[0], 1, out requiredSize);
            if (true == status)
            {
                if (1 < requiredSize)
                {
                    guidArray = new Guid[requiredSize];
                    SetupDiClassGuidsFromName(className, ref guidArray[0], requiredSize, out requiredSize);
                }
            }
            else
                throw new System.ComponentModel.Win32Exception();

            return guidArray;
        }
    }
}