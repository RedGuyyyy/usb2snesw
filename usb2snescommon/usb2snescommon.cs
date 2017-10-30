using System;
using System.Collections.Generic;

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

}
