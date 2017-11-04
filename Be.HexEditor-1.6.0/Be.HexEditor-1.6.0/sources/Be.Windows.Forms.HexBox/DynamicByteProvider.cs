using System;
using System.Collections.Generic;

namespace Be.Windows.Forms
{
    /// <summary>
    /// Event args for single byte change
    /// </summary>
    public class HexBoxEventArgs : EventArgs
    {
        /// <summary>
        /// Constructor for single byte change
        /// </summary>
        /// <param name="i"></param>
        /// <param name="v"></param>
        public HexBoxEventArgs(long i, byte v)
        {
            index = i;
            value = v;
        }

        /// <summary>
        /// index of change
        /// </summary>
        public long index { get; set; }

        /// <summary>
        /// value changed to
        /// </summary>
        public byte value { get; set; }
    }

    /// <summary>
    /// Byte provider for a small amount of data.
    /// </summary>
    public class DynamicByteProvider : IByteProvider
	{

		/// <summary>
		/// Contains information about changes.
		/// </summary>
		bool _hasChanges;
		/// <summary>
		/// Contains a byte collection.
		/// </summary>
		List<byte> _bytes;
        List<byte> _timestamps;

		/// <summary>
		/// Initializes a new instance of the DynamicByteProvider class.
		/// </summary>
		/// <param name="data"></param>
		public DynamicByteProvider(byte[] data) : this(new List<Byte>(data)) 
		{
            _timestamps = new List<byte>(new List<Byte>(data));
            _timestamps.ForEach(t => t = 0);
        }

		/// <summary>
		/// Initializes a new instance of the DynamicByteProvider class.
		/// </summary>
		/// <param name="bytes"></param>
		public DynamicByteProvider(List<Byte> bytes)
		{
			_bytes = bytes;
            _timestamps = new List<byte>(new List<Byte>(bytes));
            _timestamps.ForEach(t => t = 0);
        }

        /// <summary>
        /// Raises the Changed event.
        /// </summary>
        void OnChanged(EventArgs e)
		{
			_hasChanges = true;

			if(Changed != null)
				Changed(this, e);
		}

		/// <summary>
		/// Raises the LengthChanged event.
		/// </summary>
		void OnLengthChanged(EventArgs e)
		{
			if(LengthChanged != null)
				LengthChanged(this, e);
		}

		/// <summary>
		/// Gets the byte collection.
		/// </summary>
		public List<Byte> Bytes
		{
			get { return _bytes; }
		}

        /// <summary>
        /// Write a byte into the byte collection without calling the event.
        /// </summary>
        /// <param name="index">the index of the byte to write.</param>
        /// <param name="value">the byte</param>
        public void WriteByteNoEvent(long index, byte value)
        {
            if (_bytes[(int)index] != value)
                _timestamps[(int)index] = 252; // make even so we don't wrap on decrement
            _bytes[(int)index] = value;
        }

        #region IByteProvider Members
        /// <summary>
        /// True, when changes are done.
        /// </summary>
        public bool HasChanges()
		{
			return _hasChanges;
		}

		/// <summary>
		/// Applies changes.
		/// </summary>
		public void ApplyChanges()
		{
			_hasChanges = false;
		}

		/// <summary>
		/// Occurs, when the write buffer contains new changes.
		/// </summary>
		public event EventHandler Changed;

		/// <summary>
		/// Occurs, when InsertBytes or DeleteBytes method is called.
		/// </summary>
		public event EventHandler LengthChanged;


		/// <summary>
		/// Reads a byte from the byte collection.
		/// </summary>
		/// <param name="index">the index of the byte to read</param>
		/// <returns>the byte</returns>
		public byte ReadByte(long index)
		{ if (_timestamps[(int)index] != 0) _timestamps[(int)index] -= 4; return _bytes[(int)index]; }

        /// <summary>
        /// Reads a timestamp from the byte collection.
        /// </summary>
        /// <param name="index">the index of the byte to read</param>
        /// <returns>the byte</returns>
        public byte ReadTimestamp(long index)
        { return _timestamps[(int)index]; }

        /// <summary>
        /// Write a byte into the byte collection.
        /// </summary>
        /// <param name="index">the index of the byte to write.</param>
        /// <param name="value">the byte</param>
        public void WriteByte(long index, byte value)
		{
			_bytes[(int)index] = value;
     	    OnChanged(new HexBoxEventArgs(index, value));
		}

		/// <summary>
		/// Deletes bytes from the byte collection.
		/// </summary>
		/// <param name="index">the start index of the bytes to delete.</param>
		/// <param name="length">the length of bytes to delete.</param>
		public void DeleteBytes(long index, long length)
		{ 
			int internal_index = (int)Math.Max(0, index);
			int internal_length = (int)Math.Min((int)Length, length);
			_bytes.RemoveRange(internal_index, internal_length);
            _timestamps.RemoveRange(internal_index, internal_length);

            OnLengthChanged(EventArgs.Empty);
			OnChanged(EventArgs.Empty);
		}

		/// <summary>
		/// Inserts byte into the byte collection.
		/// </summary>
		/// <param name="index">the start index of the bytes in the byte collection</param>
		/// <param name="bs">the byte array to insert</param>
		public void InsertBytes(long index, byte[] bs)
		{ 
			_bytes.InsertRange((int)index, bs);
            _timestamps.InsertRange((int)index, bs);
            _timestamps.ForEach(t => t = 0);

            OnLengthChanged(EventArgs.Empty);
			OnChanged(EventArgs.Empty);
		}

		/// <summary>
		/// Gets the length of the bytes in the byte collection.
		/// </summary>
		public long Length
		{
			get
			{
				return _bytes.Count;
			}
		}

		/// <summary>
		/// Returns true
		/// </summary>
		public bool SupportsWriteByte()
		{
			return true;
		}

		/// <summary>
		/// Returns true
		/// </summary>
		public bool SupportsInsertBytes()
		{
			return true;
		}

		/// <summary>
		/// Returns true
		/// </summary>
		public bool SupportsDeleteBytes()
		{
			return true;
		}
		#endregion

    }
}
