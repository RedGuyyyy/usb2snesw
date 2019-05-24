using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace usb2snesmemoryviewer.Properties
{
    public partial class sa1debug : Form
    {

        public Byte[] memory { get; set; }
        public usb2snes.usb2snesmemoryviewer parent { get; set; }
        Timer t = new Timer();
        uint PCprev;
        uint Step = 0;
        bool multiByte = false;

        private int debugCnt = 0;

        public sa1debug()
        {
            InitializeComponent();
        }

        void Update(object o, EventArgs e)
        {
            // update all the state
            stateValueCCNT.Text = ((0) | (memory[0x0] << 0)).ToString("X2");
            stateValueSIE.Text  = ((0) | (memory[0x1] << 0)).ToString("X2");
            stateValueSIC.Text  = ((0) | (memory[0x2] << 0)).ToString("X2");
            stateValueCRV.Text  = ((memory[0x4] << 8) | (memory[0x3] << 0)).ToString("X4");
            stateValueCNV.Text  = ((memory[0x6] << 8) | (memory[0x5] << 0)).ToString("X4");
            stateValueCIV.Text  = ((memory[0x8] << 8) | (memory[0x7] << 0)).ToString("X4");
            stateValueSCNT.Text = ((0) | (memory[0x9] << 0)).ToString("X2");
            stateValueCIE.Text  = ((0) | (memory[0xA] << 0)).ToString("X2");
            stateValueCIC.Text  = ((0) | (memory[0xB] << 0)).ToString("X2");
            stateValueSNV.Text  = ((memory[0xD] << 8) | (memory[0xC] << 0)).ToString("X4");
            stateValueSIV.Text  = ((memory[0xF] << 8) | (memory[0xE] << 0)).ToString("X4");
            stateValueTMC.Text  = ((0) | (memory[0x10] << 0)).ToString("X2");
            stateValueCTR.Text  = ((0) | (memory[0x11] << 0)).ToString("X2");
            stateValueHCNT.Text = ((memory[0x13] << 8) | (memory[0x12] << 0)).ToString("X4");
            stateValueVCNT.Text = ((memory[0x15] << 8) | (memory[0x14] << 0)).ToString("X4");
            stateValueCXB.Text  = ((0) | (memory[0x20] << 0)).ToString("X2");
            stateValueDXB.Text  = ((0) | (memory[0x21] << 0)).ToString("X2");
            stateValueEXB.Text  = ((0) | (memory[0x22] << 0)).ToString("X2");
            stateValueFXB.Text  = ((0) | (memory[0x23] << 0)).ToString("X2");
            stateValueBMAPS.Text = ((0) | (memory[0x24] << 0)).ToString("X2");
            stateValueBMAP.Text = ((0) | (memory[0x25] << 0)).ToString("X2");
            stateValueSWBE.Text = ((0) | (memory[0x26] << 0)).ToString("X2");
            stateValueCWBE.Text = ((0) | (memory[0x27] << 0)).ToString("X2");
            stateValueBWPA.Text = ((0) | (memory[0x28] << 0)).ToString("X2");
            stateValueSIWP.Text = ((0) | (memory[0x29] << 0)).ToString("X2");
            stateValueCIWP.Text = ((0) | (memory[0x2A] << 0)).ToString("X2");
            stateValueDCNT.Text = ((0) | (memory[0x30] << 0)).ToString("X2");
            stateValueCDMA.Text = ((0) | (memory[0x31] << 0)).ToString("X2");
            stateValueSDA.Text = ((memory[0x34] << 16) | (memory[0x33] << 8) | (memory[0x32] << 0)).ToString("X6");
            stateValueDDA.Text = ((memory[0x37] << 16) | (memory[0x36] << 8) | (memory[0x35] << 0)).ToString("X6");
            stateValueDTC.Text = ((memory[0x39] << 8) | (memory[0x38] << 0)).ToString("X4");
            stateValueBBF.Text = ((0) | (memory[0x3F] << 0)).ToString("X2");
            stateValueBRF0.Text = ((memory[0x41] << 8) | (memory[0x40] << 0)).ToString("X4");
            stateValueBRF1.Text = ((memory[0x43] << 8) | (memory[0x42] << 0)).ToString("X4");
            stateValueBRF2.Text = ((memory[0x45] << 8) | (memory[0x44] << 0)).ToString("X4");
            stateValueBRF3.Text = ((memory[0x47] << 8) | (memory[0x46] << 0)).ToString("X4");
            stateValueBRF4.Text = ((memory[0x49] << 8) | (memory[0x48] << 0)).ToString("X4");
            stateValueBRF5.Text = ((memory[0x4B] << 8) | (memory[0x4A] << 0)).ToString("X4");
            stateValueBRF6.Text = ((memory[0x4D] << 8) | (memory[0x4C] << 0)).ToString("X4");
            stateValueBRF7.Text = ((memory[0x4F] << 8) | (memory[0x4E] << 0)).ToString("X4");
            stateValueMCNT.Text = ((0) | (memory[0x50] << 0)).ToString("X2");
            stateValueMA.Text = ((memory[0x52] << 8) | (memory[0x51] << 0)).ToString("X4");
            stateValueMB.Text = ((memory[0x54] << 8) | (memory[0x53] << 0)).ToString("X4");
            stateValueVBD.Text = ((0) | (memory[0x58] << 0)).ToString("X2");
            stateValueVDA.Text = ((0) | (memory[0x59] << 0)).ToString("X2");
            stateValueSFR.Text = ((0) | (memory[0x60] << 0)).ToString("X2");
            stateValueCFR.Text = ((0) | (memory[0x61] << 0)).ToString("X2");
            stateValueHCR.Text = ((memory[0x63] << 8) | (memory[0x62] << 0)).ToString("X4");
            stateValueVCR.Text = ((memory[0x65] << 8) | (memory[0x64] << 0)).ToString("X4");
            stateValueMR.Text = ((memory[0x6A] << 32) | (memory[0x69] << 24) | (memory[0x68] << 16) | (memory[0x67] << 8) | (memory[0x66] << 0)).ToString("X10");
            stateValueOF.Text = ((0) | (memory[0x6B] << 0)).ToString("X2");
            stateValueVDP.Text = ((memory[0x6D] << 8) | (memory[0x6C] << 0)).ToString("X4");
            stateValueVC.Text = ((0) | (memory[0x6E] << 0)).ToString("X2");

            stateValuePC.Text = ((memory[0x8C] << 16) | (memory[0x8B] << 8) | (memory[0x8A] << 0)).ToString("X6");
            stateValueA.Text = ((memory[0x81] << 8) | (memory[0x80] << 0)).ToString("X4");
            stateValueX.Text = ((memory[0x83] << 8) | (memory[0x82] << 0)).ToString("X4");
            stateValueY.Text = ((memory[0x85] << 8) | (memory[0x84] << 0)).ToString("X4");
            stateValueS.Text = ((memory[0x87] << 8) | (memory[0x86] << 0)).ToString("X4");
            stateValueD.Text = ((memory[0x89] << 8) | (memory[0x88] << 0)).ToString("X4");
            stateValueDB.Text = ((0) | (memory[0x8D] << 0)).ToString("X2");
            stateValueP.Text = ((0) | (memory[0x8E] << 0)).ToString("X2");
            stateValueST.Text = ((0) | (memory[0x8F] << 0)).ToString("X2");
            stateValueMDR.Text = ((0) | (memory[0x90] << 0)).ToString("X2");

            checkBoxC.Checked = ((memory[0x8E] << 0) & 0x01) != 0;
            checkBoxZ.Checked = ((memory[0x8E] << 0) & 0x02) != 0;
            checkBoxI.Checked = ((memory[0x8E] << 0) & 0x04) != 0;
            checkBoxD.Checked = ((memory[0x8E] << 0) & 0x08) != 0;
            checkBoxX.Checked = ((memory[0x8E] << 0) & 0x10) != 0;
            checkBoxM.Checked = ((memory[0x8E] << 0) & 0x20) != 0;
            checkBoxV.Checked = ((memory[0x8E] << 0) & 0x40) != 0;
            checkBoxN.Checked = ((memory[0x8E] << 0) & 0x80) != 0;
            checkBoxE.Checked = ((memory[0x8F] << 0)) != 0;
            checkBoxWAI.Checked = ((memory[0x91] << 0)) != 0;

            uint PC = (uint)((memory[0x8C] << 16) | (memory[0x8B] << 8) | (memory[0x8A] << 0));
            bool opComplete = true;

            // decode opcode
            string opString = GetInstruction(PC);

            if (PC != PCprev)
            {
                // add new operation
                if (multiByte) listBoxCode.Items.RemoveAt(listBoxCode.Items.Count - 1);
                listBoxCode.Items.Add(opString + "\n");
                if (listBoxCode.Items.Count > 50) listBoxCode.Items.RemoveAt(0);
                listBoxCode.SelectedIndex = listBoxCode.Items.Count - 1;
                multiByte = !opComplete;

                PCprev = PC;
            }
        }

        private void sa1debug_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible)
            {
                t.Tick += new EventHandler(Update);
                t.Interval = 200;
                t.Start();
                PCprev = 0xFFFFFFFF;
                multiByte = false;
            }
            else
            {
                t.Stop();
            }
        }

        private void buttonStep_Click(object sender, EventArgs e)
        {
            // mask/value,reg
            parent.GSUReg(new int[] { 0xFE0000 });
            parent.GSUReg(new int[] { 0x000001 | (++debugCnt << 8) });
        }

        private void buttonRun_Click(object sender, EventArgs e)
        {
            // mask/value,reg
            parent.GSUReg(new int[] { 0xFE0100 });
        }

        private void buttonStopOnStop_Click(object sender, EventArgs e)
        {
            // mask/value,reg
            parent.GSUReg(new int[] { 0x004002 });
        }

        private void buttonBreakSet_Click(object sender, EventArgs e)
        {
            var reg = new int[4];
            // set break address first to avoid false positive on old address
            reg[0] = 0x000005;
            reg[1] = 0x000006;
            reg[2] = 0x000007;
            reg[3] = 0x000002;
            //reg[4] = 0x000004;

            // mask/value,reg
            reg[3] |= checkBoxBreakS.Checked ? 0x4000 : 0x0000;
            reg[3] |= checkBoxBreakE.Checked ? 0x8000 : 0x0000;

            // check if address is hex
            if (System.Text.RegularExpressions.Regex.IsMatch(textBoxBreakAddr.Text, @"\A\b[0-9a-fA-F]+\b\Z"))
            {
                reg[3] |= checkBoxBreakR.Checked ? 0x1000 : 0x0000;
                reg[3] |= checkBoxBreakW.Checked ? 0x2000 : 0x0000;
                reg[3] |= checkBoxBreakX.Checked ? 0x0800 : 0x0000;
                //reg[3] |= checkBoxBreakD.Checked ? 0x0100 : 0x0000;

                int addr = int.Parse(textBoxBreakAddr.Text, System.Globalization.NumberStyles.HexNumber);
                reg[0] |= ((addr >>  0) & 0xFF) << 8;
                reg[1] |= ((addr >>  8) & 0xFF) << 8;
                reg[2] |= ((addr >> 16) & 0xFF) << 8;

                //int data = int.Parse(textBoxBreakData.Text, System.Globalization.NumberStyles.HexNumber);
                //reg[4] |= ((data >>  0) & 0xFF) << 8;
            }

            parent.GSUReg(reg);
        }

        private void buttonTrace_Click(object sender, EventArgs e)
        {
            int v;
            if (Int32.TryParse(traceInstCount.Text, out v)) {
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"trace.txt"))
                {
                    var r = new Random();
                    Byte tempCnt = Convert.ToByte(r.Next() & 0xFF);

                    for (int i = 0; i < v; i++)
                    {
                        // print current instruction
                        uint PC = (uint)((memory[0x8C] << 16) | (memory[0x8B] << 8) | (memory[0x8A] << 0));
                        var inst = GetInstruction(PC, true);
                        file.WriteLine(inst);

                        parent.GSUReg(new int[] { 0x000001 | (++tempCnt << 8) });
                        while (memory[0xF1] != tempCnt) {
                            //System.Threading.Thread.Sleep(100);
                            parent.GSUUpdate();
                        };
                    }
                }
            }
        }

        bool byteToggle = false;
        private void buttonByte_Click(object sender, EventArgs e)
        {
            // mask/value,reg
            byteToggle = !byteToggle;
            parent.GSUReg(new int[] { 0xFB0000 | (byteToggle ? 0x0400 : 0x0000)});
        }

        private string GetInstruction(uint PC, bool traceMode = false)
        {
            string opString;

            //sprintf(s, "%.6x ", (uint32)pc.d);
            Byte op = memory[0x0C2];
            opString = string.Format("{0,0:x6} ", PC);
            if (!traceMode) opString += string.Format("{0,0:X2} ", op);

            //uint8 op = dreadb(pc.d); pc.w++;
            //uint8 op0 = dreadb(pc.d); pc.w++;
            //uint8 op1 = dreadb(pc.d); pc.w++;
            //uint8 op2 = dreadb(pc.d);

            //#define op8  ((op0))
            //#define op16 ((op0) | (op1 << 8))
            //#define op24 ((op0) | (op1 << 8) | (op2 << 16))
            //#define a8   (regs.e || regs.p.m)
            //#define x8   (regs.e || regs.p.x)
            Byte op8 = memory[0x0C3];
            Byte op1 = memory[0x0C4];
            int op16 = (memory[0x0C4] << 8) | (memory[0x0C3] << 0);
            int op24 = (memory[0x0C5] << 16) | (memory[0x0C4] << 8) | (memory[0x0C3] << 0);
            Byte p = memory[0x8E];
            bool a8 = (p & 0x20) == 0x20;
            bool x8 = (p & 0x10) == 0x10;
            int exe_addr = (memory[0x0C8] << 16) | (memory[0x0C7] << 8) | (memory[0x0C6] << 0);
            int exe_data = (memory[0x0CA] << 8) | (memory[0x0C9] << 0);
            int tpc16 = (memory[0x0DE] << 8) | (memory[0x0DD] << 0);
            int tpc24 = (memory[0x0DF] << 16) | (memory[0x0DE] << 8) | (memory[0x0DD] << 0);

            switch (op)
            {
                case 0x00: opString += string.Format("brk #${0,2:x2}              ", op8); break;
                case 0x01: opString += string.Format("ora (${0,2:x2},x)   [{1,6:x6}]", op8, exe_addr); break;
                case 0x02: opString += string.Format("cop #${0,2:x2}              ", op8); break;
                case 0x03: opString += string.Format("ora ${0,2:x2},s     [{1,6:x6}]", op8, exe_addr); break;
                case 0x04: opString += string.Format("tsb ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x05: opString += string.Format("ora ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x06: opString += string.Format("asl ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x07: opString += string.Format("ora [${0,2:x2}]     [{1,6:x6}]", op8, exe_addr); break;
                case 0x08: opString += string.Format("php                   "); break;
                case 0x09:
                    if (a8) opString += string.Format("ora #${0,2:x2}              ", op8);
                    else opString += string.Format("ora #${0,4:x4}            ", op16); break;
                case 0x0a: opString += string.Format("asl a                 "); break;
                case 0x0b: opString += string.Format("phd                   "); break;
                case 0x0c: opString += string.Format("tsb ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x0d: opString += string.Format("ora ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x0e: opString += string.Format("asl ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x0f: opString += string.Format("ora ${0,6:x6}   [{1,6:x6}]", op24, exe_addr); break;
                case 0x10: opString += string.Format("bpl ${0,4:x4}     [{1,6:x6}]", tpc16, tpc24); break;
                case 0x11: opString += string.Format("ora (${0,2:x2}),y   [{1,6:x6}]", op8, exe_addr); break;
                case 0x12: opString += string.Format("ora (${0,2:x2})     [{1,6:x6}]", op8, exe_addr); break;
                case 0x13: opString += string.Format("ora (${0,2:x2},s),y [{1,6:x6}]", op8, exe_addr); break;
                case 0x14: opString += string.Format("trb ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x15: opString += string.Format("ora ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0x16: opString += string.Format("asl ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0x17: opString += string.Format("ora [${0,2:x2}],y   [{1,6:x6}]", op8, exe_addr); break;
                case 0x18: opString += string.Format("clc                   "); break;
                case 0x19: opString += string.Format("ora ${0,4:x4},y   [{1,6:x6}]", op16, exe_addr); break;
                case 0x1a: opString += string.Format("inc                   "); break;
                case 0x1b: opString += string.Format("tcs                   "); break;
                case 0x1c: opString += string.Format("trb ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x1d: opString += string.Format("ora ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0x1e: opString += string.Format("asl ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0x1f: opString += string.Format("ora ${0,6:x6},x [{1,6:x6}]", op24, exe_addr); break;
                case 0x20: opString += string.Format("jsr ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x21: opString += string.Format("and (${0,2:x2},x)   [{1,6:x6}]", op8, exe_addr); break;
                case 0x22: opString += string.Format("jsl ${0,6:x6}   [{1,6:x6}]", op24, exe_addr); break;
                case 0x23: opString += string.Format("and ${0,2:x2},s     [{1,6:x6}]", op8, exe_addr); break;
                case 0x24: opString += string.Format("bit ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x25: opString += string.Format("and ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x26: opString += string.Format("rol ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x27: opString += string.Format("and [${0,2:x2}]     [{1,6:x6}]", op8, exe_addr); break;
                case 0x28: opString += string.Format("plp                   "); break;
                case 0x29:
                    if (a8) opString += string.Format("and #${0,2:x2}              ", op8);
                    else opString += string.Format("and #${0,4:x4}            ", op16); break;
                case 0x2a: opString += string.Format("rol a                 "); break;
                case 0x2b: opString += string.Format("pld                   "); break;
                case 0x2c: opString += string.Format("bit ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x2d: opString += string.Format("and ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x2e: opString += string.Format("rol ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x2f: opString += string.Format("and ${0,6:x6}   [{1,6:x6}]", op24, exe_addr); break;
                case 0x30: opString += string.Format("bmi ${0,4:x4}     [{1,6:x6}]", tpc16, tpc24); break;
                case 0x31: opString += string.Format("and (${0,2:x2}),y   [{1,6:x6}]", op8, exe_addr); break;
                case 0x32: opString += string.Format("and (${0,2:x2})     [{1,6:x6}]", op8, exe_addr); break;
                case 0x33: opString += string.Format("and (${0,2:x2},s),y [{1,6:x6}]", op8, exe_addr); break;
                case 0x34: opString += string.Format("bit ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0x35: opString += string.Format("and ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0x36: opString += string.Format("rol ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0x37: opString += string.Format("and [${0,2:x2}],y   [{1,6:x6}]", op8, exe_addr); break;
                case 0x38: opString += string.Format("sec                   "); break;
                case 0x39: opString += string.Format("and ${0,4:x4},y   [{1,6:x6}]", op16, exe_addr); break;
                case 0x3a: opString += string.Format("dec                   "); break;
                case 0x3b: opString += string.Format("tsc                   "); break;
                case 0x3c: opString += string.Format("bit ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0x3d: opString += string.Format("and ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0x3e: opString += string.Format("rol ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0x3f: opString += string.Format("and ${0,6:x6},x [{1,6:x6}]", op24, exe_addr); break;
                case 0x40: opString += string.Format("rti                   "); break;
                case 0x41: opString += string.Format("eor (${0,2:x2},x)   [{1,6:x6}]", op8, exe_addr); break;
                case 0x42: opString += string.Format("wdm #${0,2:x2}              ", op8); break;
                case 0x43: opString += string.Format("eor ${0,2:x2},s     [{1,6:x6}]", op8, exe_addr); break;
                case 0x44: opString += string.Format("mvp ${0,2:x2},${0,2:x2}           ", op1, op8); break;
                case 0x45: opString += string.Format("eor ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x46: opString += string.Format("lsr ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x47: opString += string.Format("eor [${0,2:x2}]     [{1,6:x6}]", op8, exe_addr); break;
                case 0x48: opString += string.Format("pha                   "); break;
                case 0x49:
                    if (a8) opString += string.Format("eor #${0,2:x2}              ", op8);
                    else opString += string.Format("eor #${0,4:x4}            ", op16); break;
                case 0x4a: opString += string.Format("lsr a                 "); break;
                case 0x4b: opString += string.Format("phk                   "); break;
                case 0x4c: opString += string.Format("jmp ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x4d: opString += string.Format("eor ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x4e: opString += string.Format("lsr ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x4f: opString += string.Format("eor ${0,6:x6}   [{1,6:x6}]", op24, exe_addr); break;
                case 0x50: opString += string.Format("bvc ${0,4:x4}     [{1,6:x6}]", tpc16, tpc24); break;
                case 0x51: opString += string.Format("eor (${0,2:x2}),y   [{1,6:x6}]", op8, exe_addr); break;
                case 0x52: opString += string.Format("eor (${0,2:x2})     [{1,6:x6}]", op8, exe_addr); break;
                case 0x53: opString += string.Format("eor (${0,2:x2},s),y [{1,6:x6}]", op8, exe_addr); break;
                case 0x54: opString += string.Format("mvn ${0,2:x2},${1,2:x2}           ", op1, op8); break;
                case 0x55: opString += string.Format("eor ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0x56: opString += string.Format("lsr ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0x57: opString += string.Format("eor [${0,2:x2}],y   [{1,6:x6}]", op8, exe_addr); break;
                case 0x58: opString += string.Format("cli                   "); break;
                case 0x59: opString += string.Format("eor ${0,4:x4},y   [{1,6:x6}]", op16, exe_addr); break;
                case 0x5a: opString += string.Format("phy                   "); break;
                case 0x5b: opString += string.Format("tcd                   "); break;
                case 0x5c: opString += string.Format("jml ${0,6:x6}   [{1,6:x6}]", op24, exe_addr); break;
                case 0x5d: opString += string.Format("eor ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0x5e: opString += string.Format("lsr ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0x5f: opString += string.Format("eor ${0,6:x6},x [{1,6:x6}]", op24, exe_addr); break;
                case 0x60: opString += string.Format("rts                   "); break;
                case 0x61: opString += string.Format("adc (${0,2:x2},x)   [{1,6:x6}]", op8, exe_addr); break;
                case 0x62: opString += string.Format("per ${0,4:x4}             ", exe_data); break;
                case 0x63: opString += string.Format("adc ${0,2:x2},s     [{1,6:x6}]", op8, exe_addr); break;
                case 0x64: opString += string.Format("stz ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x65: opString += string.Format("adc ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x66: opString += string.Format("ror ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x67: opString += string.Format("adc [${0,2:x2}]     [{1,6:x6}]", op8, exe_addr); break;
                case 0x68: opString += string.Format("pla                   "); break;
                case 0x69:
                    if (a8) opString += string.Format("adc #${0,2:x2}              ", op8);
                    else opString += string.Format("adc #${0,4:x4}            ", op16); break;
                case 0x6a: opString += string.Format("ror a                 "); break;
                case 0x6b: opString += string.Format("rtl                   "); break;
                case 0x6c: opString += string.Format("jmp (${0,4:x4})   [{1,6:x6}]", op16, exe_addr); break;
                case 0x6d: opString += string.Format("adc ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x6e: opString += string.Format("ror ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x6f: opString += string.Format("adc ${0,6:x6}   [{1,6:x6}]", op24, exe_addr); break;
                case 0x70: opString += string.Format("bvs ${0,4:x4}     [{1,6:x6}]", tpc16, tpc24); break;
                case 0x71: opString += string.Format("adc (${0,2:x2}),y   [{1,6:x6}]", op8, exe_addr); break;
                case 0x72: opString += string.Format("adc (${0,2:x2})     [{1,6:x6}]", op8, exe_addr); break;
                case 0x73: opString += string.Format("adc (${0,2:x2},s),y [{1,6:x6}]", op8, exe_addr); break;
                case 0x74: opString += string.Format("stz ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0x75: opString += string.Format("adc ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0x76: opString += string.Format("ror ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0x77: opString += string.Format("adc [${0,2:x2}],y   [{1,6:x6}]", op8, exe_addr); break;
                case 0x78: opString += string.Format("sei                   "); break;
                case 0x79: opString += string.Format("adc ${0,4:x4},y   [{1,6:x6}]", op16, exe_addr); break;
                case 0x7a: opString += string.Format("ply                   "); break;
                case 0x7b: opString += string.Format("tdc                   "); break;
                case 0x7c: opString += string.Format("jmp (${0,4:x4},x) [{1,6:x6}]", op16, exe_addr); break;
                case 0x7d: opString += string.Format("adc ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0x7e: opString += string.Format("ror ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0x7f: opString += string.Format("adc ${0,6:x6},x [{1,6:x6}]", op24, exe_addr); break;
                case 0x80: opString += string.Format("bra ${0,4:x4}     [{1,6:x6}]", tpc16, tpc24); break;
                case 0x81: opString += string.Format("sta (${0,2:x2},x)   [{1,6:x6}]", op8, exe_addr); break;
                case 0x82: opString += string.Format("brl ${0,4:x4}     [{1,6:x6}]", tpc16, tpc24); break;
                case 0x83: opString += string.Format("sta ${0,2:x2},s     [{1,6:x6}]", op8, exe_addr); break;
                case 0x84: opString += string.Format("sty ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x85: opString += string.Format("sta ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x86: opString += string.Format("stx ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0x87: opString += string.Format("sta [${0,2:x2}]     [{1,6:x6}]", op8, exe_addr); break;
                case 0x88: opString += string.Format("dey                   "); break;
                case 0x89:
                    if (a8) opString += string.Format("bit #${0,2:x2}              ", op8);
                    else opString += string.Format("bit #${0,4:x4}            ", op16); break;
                case 0x8a: opString += string.Format("txa                   "); break;
                case 0x8b: opString += string.Format("phb                   "); break;
                case 0x8c: opString += string.Format("sty ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x8d: opString += string.Format("sta ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x8e: opString += string.Format("stx ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x8f: opString += string.Format("sta ${0,6:x6}   [{1,6:x6}]", op24, exe_addr); break;
                case 0x90: opString += string.Format("bcc ${0,4:x4}     [{1,6:x6}]", tpc16, tpc24); break;
                case 0x91: opString += string.Format("sta (${0,2:x2}),y   [{1,6:x6}]", op8, exe_addr); break;
                case 0x92: opString += string.Format("sta (${0,2:x2})     [{1,6:x6}]", op8, exe_addr); break;
                case 0x93: opString += string.Format("sta (${0,2:x2},s),y [{1,6:x6}]", op8, exe_addr); break;
                case 0x94: opString += string.Format("sty ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0x95: opString += string.Format("sta ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0x96: opString += string.Format("stx ${0,2:x2},y     [{1,6:x6}]", op8, exe_addr); break;
                case 0x97: opString += string.Format("sta [${0,2:x2}],y   [{1,6:x6}]", op8, exe_addr); break;
                case 0x98: opString += string.Format("tya                   "); break;
                case 0x99: opString += string.Format("sta ${0,4:x4},y   [{1,6:x6}]", op16, exe_addr); break;
                case 0x9a: opString += string.Format("txs                   "); break;
                case 0x9b: opString += string.Format("txy                   "); break;
                case 0x9c: opString += string.Format("stz ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0x9d: opString += string.Format("sta ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0x9e: opString += string.Format("stz ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0x9f: opString += string.Format("sta ${0,6:x6},x [{1,6:x6}]", op24, exe_addr); break;
                case 0xa0:
                    if (x8) opString += string.Format("ldy #${0,2:x2}              ", op8);
                    else opString += string.Format("ldy #${0,4:x4}            ", op16); break;
                case 0xa1: opString += string.Format("lda (${0,2:x2},x)   [{1,6:x6}]", op8, exe_addr); break;
                case 0xa2:
                    if (x8) opString += string.Format("ldx #${0,2:x2}              ", op8);
                    else opString += string.Format("ldx #${0,4:x4}            ", op16); break;
                case 0xa3: opString += string.Format("lda ${0,2:x2},s     [{1,6:x6}]", op8, exe_addr); break;
                case 0xa4: opString += string.Format("ldy ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0xa5: opString += string.Format("lda ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0xa6: opString += string.Format("ldx ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0xa7: opString += string.Format("lda [${0,2:x2}]     [{1,6:x6}]", op8, exe_addr); break;
                case 0xa8: opString += string.Format("tay                   "); break;
                case 0xa9:
                    if (a8) opString += string.Format("lda #${0,2:x2}              ", op8);
                    else opString += string.Format("lda #${0,4:x4}            ", op16); break;
                case 0xaa: opString += string.Format("tax                   "); break;
                case 0xab: opString += string.Format("plb                   "); break;
                case 0xac: opString += string.Format("ldy ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0xad: opString += string.Format("lda ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0xae: opString += string.Format("ldx ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0xaf: opString += string.Format("lda ${0,6:x6}   [{1,6:x6}]", op24, exe_addr); break;
                case 0xb0: opString += string.Format("bcs ${0,4:x4}     [{1,6:x6}]", tpc16, tpc24); break;
                case 0xb1: opString += string.Format("lda (${0,2:x2}),y   [{1,6:x6}]", op8, exe_addr); break;
                case 0xb2: opString += string.Format("lda (${0,2:x2})     [{1,6:x6}]", op8, exe_addr); break;
                case 0xb3: opString += string.Format("lda (${0,2:x2},s),y [{1,6:x6}]", op8, exe_addr); break;
                case 0xb4: opString += string.Format("ldy ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0xb5: opString += string.Format("lda ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0xb6: opString += string.Format("ldx ${0,2:x2},y     [{1,6:x6}]", op8, exe_addr); break;
                case 0xb7: opString += string.Format("lda [${0,2:x2}],y   [{1,6:x6}]", op8, exe_addr); break;
                case 0xb8: opString += string.Format("clv                   "); break;
                case 0xb9: opString += string.Format("lda ${0,4:x4},y   [{1,6:x6}]", op16, exe_addr); break;
                case 0xba: opString += string.Format("tsx                   "); break;
                case 0xbb: opString += string.Format("tyx                   "); break;
                case 0xbc: opString += string.Format("ldy ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0xbd: opString += string.Format("lda ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0xbe: opString += string.Format("ldx ${0,4:x4},y   [{1,6:x6}]", op16, exe_addr); break;
                case 0xbf: opString += string.Format("lda ${0,6:x6},x [{1,6:x6}]", op24, exe_addr); break;
                case 0xc0:
                    if (x8) opString += string.Format("cpy #${0,2:x2}              ", op8);
                    else opString += string.Format("cpy #${0,4:x4}            ", op16); break;
                case 0xc1: opString += string.Format("cmp (${0,2:x2},x)   [{1,6:x6}]", op8, exe_addr); break;
                case 0xc2: opString += string.Format("rep #${0,2:x2}              ", op8); break;
                case 0xc3: opString += string.Format("cmp ${0,2:x2},s     [{1,6:x6}]", op8, exe_addr); break;
                case 0xc4: opString += string.Format("cpy ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0xc5: opString += string.Format("cmp ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0xc6: opString += string.Format("dec ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0xc7: opString += string.Format("cmp [${0,2:x2}]     [{1,6:x6}]", op8, exe_addr); break;
                case 0xc8: opString += string.Format("iny                   "); break;
                case 0xc9:
                    if (a8) opString += string.Format("cmp #${0,2:x2}              ", op8);
                    else opString += string.Format("cmp #${0,4:x4}            ", op16); break;
                case 0xca: opString += string.Format("dex                   "); break;
                case 0xcb: opString += string.Format("wai                   "); break;
                case 0xcc: opString += string.Format("cpy ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0xcd: opString += string.Format("cmp ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0xce: opString += string.Format("dec ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0xcf: opString += string.Format("cmp ${0,6:x6}   [{1,6:x6}]", op24, exe_addr); break;
                case 0xd0: opString += string.Format("bne ${0,4:x4}     [{1,6:x6}]", tpc16, tpc24); break;
                case 0xd1: opString += string.Format("cmp (${0,2:x2}),y   [{1,6:x6}]", op8, exe_addr); break;
                case 0xd2: opString += string.Format("cmp (${0,2:x2})     [{1,6:x6}]", op8, exe_addr); break;
                case 0xd3: opString += string.Format("cmp (${0,2:x2},s),y [{1,6:x6}]", op8, exe_addr); break;
                case 0xd4: opString += string.Format("pei (${0,2:x2})     [{1,6:x6}]", op8, exe_addr); break;
                case 0xd5: opString += string.Format("cmp ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0xd6: opString += string.Format("dec ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0xd7: opString += string.Format("cmp [${0,2:x2}],y   [{1,6:x6}]", op8, exe_addr); break;
                case 0xd8: opString += string.Format("cld                   "); break;
                case 0xd9: opString += string.Format("cmp ${0,4:x4},y   [{1,6:x6}]", op16, exe_addr); break;
                case 0xda: opString += string.Format("phx                   "); break;
                case 0xdb: opString += string.Format("stp                   "); break;
                case 0xdc: opString += string.Format("jmp [${0,4:x4}]   [{1,6:x6}]", op16, exe_addr); break;
                case 0xdd: opString += string.Format("cmp ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0xde: opString += string.Format("dec ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0xdf: opString += string.Format("cmp ${0,6:x6},x [{1,6:x6}]", op24, exe_addr); break;
                case 0xe0:
                    if (x8) opString += string.Format("cpx #${0,2:x2}              ", op8);
                    else opString += string.Format("cpx #${0,4:x4}            ", op16); break;
                case 0xe1: opString += string.Format("sbc (${0,2:x2},x)   [{1,6:x6}]", op8, exe_addr); break;
                case 0xe2: opString += string.Format("sep #${0,2:x2}              ", op8); break;
                case 0xe3: opString += string.Format("sbc ${0,2:x2},s     [{1,6:x6}]", op8, exe_addr); break;
                case 0xe4: opString += string.Format("cpx ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0xe5: opString += string.Format("sbc ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0xe6: opString += string.Format("inc ${0,2:x2}       [{1,6:x6}]", op8, exe_addr); break;
                case 0xe7: opString += string.Format("sbc [${0,2:x2}]     [{1,6:x6}]", op8, exe_addr); break;
                case 0xe8: opString += string.Format("inx                   "); break;
                case 0xe9:
                    if (a8) opString += string.Format("sbc #${0,2:x2}              ", op8);
                    else opString += string.Format("sbc #${0,4:x4}            ", op16); break;
                case 0xea: opString += string.Format("nop                   "); break;
                case 0xeb: opString += string.Format("xba                   "); break;
                case 0xec: opString += string.Format("cpx ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0xed: opString += string.Format("sbc ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0xee: opString += string.Format("inc ${0,4:x4}     [{1,6:x6}]", op16, exe_addr); break;
                case 0xef: opString += string.Format("sbc ${0,6:x6}   [{1,6:x6}]", op24, exe_addr); break;
                case 0xf0: opString += string.Format("beq ${0,4:x4}     [{1,6:x6}]", tpc16, tpc24); break;
                case 0xf1: opString += string.Format("sbc (${0,2:x2}),y   [{1,6:x6}]", op8, exe_addr); break;
                case 0xf2: opString += string.Format("sbc (${0,2:x2})     [{1,6:x6}]", op8, exe_addr); break;
                case 0xf3: opString += string.Format("sbc (${0,2:x2},s),y [{1,6:x6}]", op8, exe_addr); break;
                case 0xf4: opString += string.Format("pea ${0,4:x4}             ", op16); break;
                case 0xf5: opString += string.Format("sbc ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0xf6: opString += string.Format("inc ${0,2:x2},x     [{1,6:x6}]", op8, exe_addr); break;
                case 0xf7: opString += string.Format("sbc [${0,2:x2}],y   [{1,6:x6}]", op8, exe_addr); break;
                case 0xf8: opString += string.Format("sed                   "); break;
                case 0xf9: opString += string.Format("sbc ${0,4:x4},y   [{1,6:x6}]", op16, exe_addr); break;
                case 0xfa: opString += string.Format("plx                   "); break;
                case 0xfb: opString += string.Format("xce                   "); break;
                case 0xfc: opString += string.Format("jsr (${0,4:x4},x) [{1,6:x6}]", op16, exe_addr); break;
                case 0xfd: opString += string.Format("sbc ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0xfe: opString += string.Format("inc ${0,4:x4},x   [{1,6:x6}]", op16, exe_addr); break;
                case 0xff: opString += string.Format("sbc ${0,6:x6},x [{1,6:x6}]", op24, exe_addr); break;
            }

            //strcat(s, t);
            //strcat(s, " ");
            opString += " ";

            //sprintf(t, "A:%.4x X:%.4x Y:%.4x S:%.4x D:%.4x DB:%.2x ",
            //  regs.a.w, regs.x.w, regs.y.w, regs.s.w, regs.d.w, regs.db);
            //strcat(s, t);
            int A = ((memory[0x81] << 8) | (memory[0x80] << 0));
            int X = ((memory[0x83] << 8) | (memory[0x82] << 0));
            int Y = ((memory[0x85] << 8) | (memory[0x84] << 0));
            int S = ((memory[0x87] << 8) | (memory[0x86] << 0));
            int D = ((memory[0x89] << 8) | (memory[0x88] << 0));
            int DB = ((0) | (memory[0x8D] << 0));
            opString += string.Format("A:{0,4:x4} X:{1,4:x4} Y:{2,4:x4} S:{3,4:x4} D:{4,4:x4} DB:{5,2:x2} ", A, X, Y, S, D, DB);

            bool C = ((memory[0x8E] << 0) & 0x01) != 0;
            bool Z = ((memory[0x8E] << 0) & 0x02) != 0;
            bool I = ((memory[0x8E] << 0) & 0x04) != 0;
            bool BCD = ((memory[0x8E] << 0) & 0x08) != 0;
            //checkBoxX.Checked = ((memory[0x8E] << 0) & 0x10) != 0;
            //checkBoxM.Checked = ((memory[0x8E] << 0) & 0x20) != 0;
            bool V = ((memory[0x8E] << 0) & 0x40) != 0;
            bool N = ((memory[0x8E] << 0) & 0x80) != 0;
            bool E = ((memory[0x8F] << 0)) != 0;
            if (E)
            {
                //sprintf(t, "%c%c%c%c%c%c%c%c",
                //  regs.p.n ? 'N' : 'n', regs.p.v ? 'V' : 'v',
                //  regs.p.m ? '1' : '0', regs.p.x ? 'B' : 'b',
                //  regs.p.d ? 'D' : 'd', regs.p.i ? 'I' : 'i',
                //  regs.p.z ? 'Z' : 'z', regs.p.c ? 'C' : 'c');
                opString += N ? 'N' : 'n';
                opString += V ? 'V' : 'v';
                opString += a8 ? '1' : '0';
                opString += x8 ? 'B' : 'b';
                opString += BCD ? 'D' : 'd';
                opString += I ? 'I' : 'i';
                opString += Z ? 'Z' : 'z';
                opString += C ? 'C' : 'c';
            }
            else
            {
                //sprintf(t, "%c%c%c%c%c%c%c%c",
                //  regs.p.n ? 'N' : 'n', regs.p.v ? 'V' : 'v',
                //  regs.p.m ? 'M' : 'm', regs.p.x ? 'X' : 'x',
                //  regs.p.d ? 'D' : 'd', regs.p.i ? 'I' : 'i',
                //  regs.p.z ? 'Z' : 'z', regs.p.c ? 'C' : 'c');
                opString += N ? 'N' : 'n';
                opString += V ? 'V' : 'v';
                opString += a8 ? 'M' : 'm';
                opString += x8 ? 'X' : 'x';
                opString += BCD ? 'D' : 'd';
                opString += I ? 'I' : 'i';
                opString += Z ? 'Z' : 'z';
                opString += C ? 'C' : 'c';
            }

            //strcat(s, t);
            //strcat(s, " ");
            opString += " ";

            //if (hclocks)
            //    sprintf(t, "V:%3d H:%4d F:%2d", cpu.vcounter(), cpu.hcounter(), cpu.framecounter());
            //else
            //    sprintf(t, "V:%3d H:%3d F:%2d", cpu.vcounter(), cpu.hdot(), cpu.framecounter());
            //strcat(s, t);

            return opString;
        }

        private void sa1debug_Load(object sender, EventArgs e)
        {

        }
    }
}
