using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace P2TZX
{
    public class TZXConverter: IConverter
    {
        static readonly byte[] TZX_HEADER = {
            0x5a, 0x58, 0x54, 0x61, 0x70, 0x65, 0x21, 0x1a, // ZXTape!
            1, 20                                          // TZX v1.20
        };

        static readonly byte[] GDB_BLOCK = {
            0x19,                       // Generalised Data Block (spit)
            0, 0, 0, 0,                 // GDB Block length - overwrite
            0xe8, 0x03,                 // Pause after block - 1 sec
            0, 0, 0, 0,                 // No pilot/sync block
            0, 0,                       // No pulses per symbol, or pilot/sync alphabet
            0, 0, 0, 0,                 // Records in data stream - overwrite
            0x12,                       // Max pulses per data symbol
            0x02,                       // Number of pulses in data alphabet

            0x03, 0x12, 0x02, 0x08, 0x02, 0x12, 0x02, 0x08, 0x02, 0x12, 0x02, 
            0x08, 0x02, 0x12, 0x02, 0x51, 0x12, 0x00, 0x00,  0x00, 0x00, 0x00, 0x00, 
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00,      // SYMDEF[0]

            0x03, 0x12, 0x02, 0x08, 0x02,  0x12, 0x02, 0x08, 0x02, 0x12, 0x02,
            0x08, 0x02, 0x12, 0x02, 0x08, 0x02, 0x12, 0x02, 0x08, 0x02, 0x12, 0x02, 
            0x08, 0x02, 0x12, 0x02, 0x08, 0x02, 0x12, 0x02, 0x08, 0x02, 0x12, 0x02,
            0x51, 0x12     // SYMDEF[1]
        };

        static string TEXT_DESCRIPTION = "Created by P2TZX, (C) Brendan Alford 2016";
        static string TEXT_DESC_BUFFER = "\x30\x29" + TEXT_DESCRIPTION;
        static int TEXT_DESC_BUFFER_LEN = 43;
        
        readonly int GDB_BLOCK_LEN = GDB_BLOCK.Length;
        static int GDB_DATASTREAM_LEN = 0x0C;
        readonly int GDB_DATASTREAM_OFFSET = GDB_BLOCK.Length;
        readonly int TZX_HEADER_LEN = 0x0a;

        public void Convert(string fileName)
        {
            FileInfo file = new FileInfo(fileName);
            string extension = file.Extension.ToUpper();
            if (extension.CompareTo(".P") == 0)
            {
                ConvertPToTZX(file);
            }
            else if (extension.CompareTo(".TZX") == 0)
            {
                ConvertTZXToP(file);
            }
            else
            {
                throw new ArgumentException("Unsupported file type: " + extension);
            }
        }

        private void ConvertPToTZX(FileInfo file)
        {
            // Allocate input buffer to allow enough room for
            // ZX81 filename + raw data
            int fNameLen = file.Name.IndexOf('.');

            FileStream fs = new FileStream(file.FullName, FileMode.Open);
            int plen = (int)file.Length;
            byte[] data = new byte[plen + fNameLen];
            fs.Read(data, fNameLen, plen);
            fs.Close();

            plen += fNameLen;
            string fName = file.Name.ToUpper();
            
            for (int i = 0; i < fNameLen; i++)
            {
                data[i] = ZX81Chars.get81Char(fName[i]);
                if (i + 1 == fNameLen)
                {
                    data[i] += 0x80;
                }
            }

            // Allocate a buffer big enough for:
            // - the TZX header
            // - Text Description block
            // - the P file name and content

            byte[] tzxBuffer = new byte[TZX_HEADER_LEN +
                TEXT_DESC_BUFFER_LEN + GDB_BLOCK_LEN + plen];
            int ptr = 0;
            Array.Copy(TZX_HEADER, 0, tzxBuffer, ptr, TZX_HEADER_LEN);
            ptr += TZX_HEADER_LEN;
            Array.Copy(System.Text.Encoding.UTF8.GetBytes(TEXT_DESC_BUFFER),
                0, tzxBuffer, ptr, TEXT_DESC_BUFFER_LEN);
            ptr += TEXT_DESC_BUFFER_LEN;
            Array.Copy(GDB_BLOCK, 0, tzxBuffer, ptr, GDB_BLOCK_LEN);
            Array.Copy(data, 0, tzxBuffer, ptr + GDB_DATASTREAM_OFFSET, plen);

            // Fill in the header size info

            WriteDword(tzxBuffer, ptr + 1, (GDB_BLOCK_LEN - 1 + plen) - 4);
            WriteDword(tzxBuffer, ptr + 1 + GDB_DATASTREAM_LEN, plen * 8);

            // Open a file and write it out

            string tzxFileName = file.Name;
            tzxFileName = tzxFileName.Substring(0, tzxFileName.LastIndexOf('.'));
            tzxFileName = tzxFileName + ".tzx";

            FileStream tzxFs = new FileStream(tzxFileName, FileMode.Create);
            tzxFs.Write(tzxBuffer, 0, tzxBuffer.Length);
            Console.WriteLine("{0} -> {1}: {2} byte(s)", file.Name, tzxFileName, tzxBuffer.Length);
            tzxFs.Close();
        }

        private void ConvertTZXToP(FileInfo file)
        {
            FileStream fs = new FileStream(file.FullName, FileMode.Open);
            int plen = (int)file.Length;
            byte[] data = new byte[plen];
            fs.Read(data, 0, plen);
            fs.Close();

            int ptr = 0;
            string signature = System.Text.Encoding.Default.GetString(data, ptr, 7);
            if (signature.CompareTo("ZXTape!") != 0)
            {
                Console.WriteLine("WARN: {0} is not a valid .TZX file", file.Name);
                return;
            }
            ptr = 0x0a;

            // Skip any Text Description blocks
            while (data[ptr] == 0x30)
            {
                ptr++;
                int textLen = data[ptr++];
                ptr += textLen;
            }

            if (data[ptr] != 0x19)
            {
                Console.WriteLine("WARN: GDB not first data block in {0}", file.Name);
                return;
            }
            ptr++;
            if (ReadDword(data, ptr + 0x06) != 0 ||
                data[ptr + 0x0a] != 0 ||
                data[ptr + 0x0b] != 0)
            {
                Console.WriteLine("WARN: GDB block in {0} does not appear to describe a valid ZX81 program", file.Name);
                return;
            }
            int totd = ReadDword(data, ptr + 0x0c);
            int npd = data[ptr + 0x10];
            int asd = data[ptr + 0x11];

            // Calculate position of data stream
            
            ptr += 0x12 + (2 * npd + 1) * asd;

            // Remove any filename data at start of TZX data block
            int length = (data.Length - ptr);
            while (data[ptr] < 0x80)
            {
                ptr++;
                length--;
            }
            ptr++;
            length--;
            

            // Open a file and write it out

            string pFileName = file.Name;
            pFileName = pFileName.Substring(0, pFileName.LastIndexOf('.'));
            pFileName = pFileName + ".p";

            FileStream pFs = new FileStream(pFileName, FileMode.Create);
            pFs.Write(data, ptr, length);
            Console.WriteLine("{0} -> {1}: {2} byte(s)", file.Name, pFileName, length);
            pFs.Close();
        }

        private void WriteDword(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte) (value & 0xff);
            buffer[offset + 1] = (byte)((value & 0xff00) >> 8);
            buffer[offset + 2] = (byte)((value & 0xff0000) >> 16);
            buffer[offset + 3] = (byte)((value & 0xff000000) >> 24);
        }

        private int ReadDword(byte[] buffer, int offset)
        {
            return (buffer[offset] +
                    buffer[offset + 1] << 8 +
                    buffer[offset + 2] << 16 +
                    buffer[offset + 3] << 24);
        }
    }
}
