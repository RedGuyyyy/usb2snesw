using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// usb2snes describes the WebSocket interface to the usb2snes.
/// </summary>
namespace usb2snes
{
    public static class Constants
    {
        public const int MaxMessageSize = 1024;
    }

    public enum OpcodeType
    {
        // Error handling is done via the underlying WebSocket protocol which disconnects the socket.

        // Connection
        DeviceList, // Request: Operands: null,                                   Response: Operands[stringList]
        Attach,     // Request: Operands: [COM#],                                 NoResponse
        AppVersion, // Request: Operands: null,                                   Response: Operands[stringList]
        Name,       // Request: Operands: [name],                                 NoResponse

        // Special
        Info,       // Request: Operands: null,                                   Response: Result: [versionString, version#]
        Boot,       // Request: Operands: [filename],                             
        Menu,       // Request: Operands: null,                                   
        Reset,      // Request: Operands: null,                                   
        Stream,     // Request: Operands: null,                                   
        Fence,      // Request: Operands: null,                                   

        // Address space access
        GetAddress, // Request: Operands: [address,size],                         Data: [data]
        PutAddress, // Request: Operands: [address,size],    Data: [data]         
        PutIPS,     // Request: Operands: [""/"hook",size],  Data: [data]         

        // File system access
        GetFile,    // Request: Operands: [filename],                             Data: [data]
        PutFile,    // Request: Operands: [filename],        Data: [data]         
        List,       // Request: Operands: null,                                   Response: Operands[stringList]
        Remove,     // Request: Operands: [filename],                             
        Rename,     // Request: Operands: [filename0,filename1]                   
        MakeDir,    // Request: Operands: [filename],                             
    }

    /// <summary>
    /// Request interface
    /// </summary>
    public class RequestType
    {
        public string Opcode { get; set; }
        public string Space { get; set; }
        public List<string> Flags { get; set; }
        public List<string> Operands { get; set; }

        public bool RequiresData() { return Opcode == "GetAddress" || Opcode == "GetFile" || Opcode == "List" || Opcode == "Info" || Opcode == "Stream"; }
        public bool HasData() { return Opcode == "PutAddress" || Opcode == "PutFile"; }
    }

    /// <summary>
    /// Response interface
    /// </summary>
    public class ResponseType
    {
        public List<string> Results { get; set; }
    }

    public class IPS
    {
        public IPS() { Items = new List<Patch>(); }

        public class Patch
        {
            public Patch() { data = new List<Byte>(); }

            public int address; // 24b file address
            public List<Byte> data;
        }

        public List<Patch> Items;

        public void Parse(Byte[] data)
        {
            int index = 0;

            // make sure the first few characters match string

            System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
            for (int i = 0; i < 5; i++)
            {
                if (data[i] != enc.GetBytes("PATCH")[i])
                    throw new Exception("IPS: error parsing PATCH");
            }
            index += 5;

            bool foundEOF = false;
            while (!foundEOF)
            {
                // read address
                // check EOF
                if (index == data.Length - 3 || index == data.Length - 6)
                {
                    foundEOF = true;
                    // check for EOF
                    for (int i = 0; i < 3; i++)
                    {
                        if (data[index + i] != enc.GetBytes("EOF")[i])
                        {
                            foundEOF = false;
                            break;
                        }
                    }
                }

                if (!foundEOF)
                {
                    Patch patch = new Patch();
                    Items.Add(patch);

                    // get address
                    if (index + 3 >= data.Length) throw new Exception("IPS: error parsing address");
                    patch.address = data[index + 0]; patch.address <<= 8;
                    patch.address |= data[index + 1]; patch.address <<= 8;
                    patch.address |= data[index + 2]; patch.address <<= 0;
                    index += 3;

                    // get length
                    if (index + 2 >= data.Length) throw new Exception("IPS: error parsing length");
                    int length = data[index + 0]; length <<= 8;
                    length |= data[index + 1]; length <<= 0;
                    index += 2;

                    // check if RLE
                    if (length == 0)
                    {
                        // RLE
                        if (index + 3 >= data.Length) throw new Exception("IPS: error parsing RLE count/byte");
                        int count = data[index + 0]; count <<= 8;
                        count |= data[index + 1]; count <<= 0;
                        Byte val = data[index + 2];
                        index += 3;

                        patch.data.AddRange(Enumerable.Repeat(val, count));
                    }
                    else
                    {
                        int count = 0;
                        while (count < length)
                        {
                            if (index + length >= data.Length) throw new Exception("IPS: error parsing data");
                            patch.data.AddRange(new ArraySegment<Byte>(data, index, length).ToArray());
                            count += length;
                            index += length;
                        }
                    }
                }
            }

            // ignore truncation
            if (index != data.Length && index != data.Length - 3)
                throw new Exception("IPS: unexpected end of file");
        }

        public void Parse(string fileName)
        {
            int index = 0;

            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            // make sure the first few characters match string
            byte[] buffer = new byte[512];

            System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
            fs.Read(buffer, 0, 5);
            for (int i = 0; i < 5; i++)
            {
                if (buffer[i] != enc.GetBytes("PATCH")[i])
                    throw new Exception("IPS: error parsing PATCH");
            }
            index += 5;

            bool foundEOF = false;
            while (!foundEOF)
            {
                int bytesRead = 0;

                // read address
                bytesRead = fs.Read(buffer, 0, 3);
                // check EOF
                if (index == fs.Length - 3 || index == fs.Length - 6)
                {
                    foundEOF = true;
                    // check for EOF
                    for (int i = 0; i < 3; i++)
                    {
                        if (buffer[i] != enc.GetBytes("EOF")[i])
                        {
                            foundEOF = false;
                            break;
                        }
                    }
                }

                if (!foundEOF)
                {
                    Patch patch = new Patch();
                    Items.Add(patch);

                    // get address
                    if (bytesRead != 3) throw new Exception("IPS: error parsing address");
                    patch.address = buffer[0]; patch.address <<= 8;
                    patch.address |= buffer[1]; patch.address <<= 8;
                    patch.address |= buffer[2]; patch.address <<= 0;
                    index += bytesRead;

                    // get length
                    bytesRead = fs.Read(buffer, 0, 2);
                    if (bytesRead != 2) throw new Exception("IPS: error parsing length");
                    int length = buffer[0]; length <<= 8;
                    length |= buffer[1]; length <<= 0;
                    index += bytesRead;

                    // check if RLE
                    if (length == 0)
                    {
                        // RLE
                        bytesRead = fs.Read(buffer, 0, 3);
                        if (bytesRead != 3) throw new Exception("IPS: error parsing RLE count/byte");
                        int count = buffer[0]; count <<= 8;
                        count |= buffer[1]; count <<= 0;
                        Byte val = buffer[2];
                        index += bytesRead;

                        patch.data.AddRange(Enumerable.Repeat(val, count));
                    }
                    else
                    {
                        int count = 0;
                        while (count < length)
                        {
                            bytesRead = fs.Read(buffer, 0, Math.Min(buffer.Length, length - count));
                            if (bytesRead == 0) throw new Exception("IPS: error parsing data");
                            count += bytesRead;
                            index += bytesRead;
                            patch.data.AddRange(buffer.Take(bytesRead));
                        }
                    }
                }
            }

            // ignore truncation
            if (index != fs.Length && index != fs.Length - 3)
                throw new Exception("IPS: unexpected end of file");

            fs.Close();
        }
    }

}
