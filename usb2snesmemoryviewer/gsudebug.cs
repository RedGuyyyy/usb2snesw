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
    public partial class gsudebug : Form
    {
        enum eState
        {
            ADDR_R0 = 0x00,
            ADDR_R1 = 0x02,
            ADDR_R2 = 0x04,
            ADDR_R3 = 0x06,
            ADDR_R4 = 0x08,
            ADDR_R5 = 0x0A,
            ADDR_R6 = 0x0C,
            ADDR_R7 = 0x0E,
            ADDR_R8 = 0x10,
            ADDR_R9 = 0x12,
            ADDR_R10 = 0x14,
            ADDR_R11 = 0x16,
            ADDR_R12 = 0x18,
            ADDR_R13 = 0x1A,
            ADDR_R14 = 0x1C,
            ADDR_R15 = 0x1E,


            ADDR_SFR = 0x30,
            ADDR_BRAMR = 0x33,
            ADDR_PBR = 0x34,
            ADDR_ROMBR = 0x36,
            ADDR_CFGR = 0x37,
            ADDR_SCBR = 0x38,
            ADDR_CLSR = 0x39,
            ADDR_SCMR = 0x3A,
            ADDR_VCR = 0x3B,
            ADDR_RAMBR = 0x3C,
            ADDR_CBR = 0x3E,

            ADDR_COLR = 0x40,
            ADDR_POR = 0x41,
            ADDR_SREG = 0x42,
            ADDR_DREG = 0x43,
            ADDR_ROMRDBUF = 0x44,
            ADDR_RAMWRBUF = 0x45,
            ADDR_RAMADDR  = 0x46,
        }

        public Byte[] memory { get; set; }
        public usb2snes.usb2snesmemoryviewer parent { get; set; }
        Timer t = new Timer();
        uint R15prev;
        bool multiByte;

        private int debugCnt = 0;

        public gsudebug()
        {
            InitializeComponent();
        }

        void Update(object o, EventArgs e)
        {
            // update all the state
            stateValueR0.Text = ((memory[(int)eState.ADDR_R0  + 1] << 8) | (memory[(int)eState.ADDR_R0 ] << 0)).ToString("X4");
            stateValueR1.Text  = ((memory[(int)eState.ADDR_R1  + 1] << 8) | (memory[(int)eState.ADDR_R1 ] << 0)).ToString("X4");
            stateValueR2.Text  = ((memory[(int)eState.ADDR_R2  + 1] << 8) | (memory[(int)eState.ADDR_R2 ] << 0)).ToString("X4");
            stateValueR3.Text  = ((memory[(int)eState.ADDR_R3  + 1] << 8) | (memory[(int)eState.ADDR_R3 ] << 0)).ToString("X4");
            stateValueR4.Text  = ((memory[(int)eState.ADDR_R4  + 1] << 8) | (memory[(int)eState.ADDR_R4 ] << 0)).ToString("X4");
            stateValueR5.Text  = ((memory[(int)eState.ADDR_R5  + 1] << 8) | (memory[(int)eState.ADDR_R5 ] << 0)).ToString("X4");
            stateValueR6.Text  = ((memory[(int)eState.ADDR_R6  + 1] << 8) | (memory[(int)eState.ADDR_R6 ] << 0)).ToString("X4");
            stateValueR7.Text  = ((memory[(int)eState.ADDR_R7  + 1] << 8) | (memory[(int)eState.ADDR_R7 ] << 0)).ToString("X4");
            stateValueR8.Text  = ((memory[(int)eState.ADDR_R8  + 1] << 8) | (memory[(int)eState.ADDR_R8 ] << 0)).ToString("X4");
            stateValueR9.Text  = ((memory[(int)eState.ADDR_R9  + 1] << 8) | (memory[(int)eState.ADDR_R9 ] << 0)).ToString("X4");
            stateValueR10.Text = ((memory[(int)eState.ADDR_R10 + 1] << 8) | (memory[(int)eState.ADDR_R10] << 0)).ToString("X4");
            stateValueR11.Text = ((memory[(int)eState.ADDR_R11 + 1] << 8) | (memory[(int)eState.ADDR_R11] << 0)).ToString("X4");
            stateValueR12.Text = ((memory[(int)eState.ADDR_R12 + 1] << 8) | (memory[(int)eState.ADDR_R12] << 0)).ToString("X4");
            stateValueR13.Text = ((memory[(int)eState.ADDR_R13 + 1] << 8) | (memory[(int)eState.ADDR_R13] << 0)).ToString("X4");
            stateValueR14.Text = ((memory[(int)eState.ADDR_R14 + 1] << 8) | (memory[(int)eState.ADDR_R14] << 0)).ToString("X4");
            stateValueR15.Text = ((memory[(int)eState.ADDR_R15 + 1] << 8) | (memory[(int)eState.ADDR_R15] << 0)).ToString("X4");

            stateValueSFR.Text = ((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)).ToString("X4");
            stateValueBRAM.Text = (memory[(int)eState.ADDR_BRAMR] << 0).ToString("X2");
            stateValuePBR.Text = (memory[(int)eState.ADDR_PBR] << 0).ToString("X2");
            stateValueROMBR.Text = (memory[(int)eState.ADDR_ROMBR] << 0).ToString("X2");
            stateValueCFGR.Text = (memory[(int)eState.ADDR_CFGR] << 0).ToString("X2");
            stateValueSCBR.Text = (memory[(int)eState.ADDR_SCBR] << 0).ToString("X2");
            stateValueCLSR.Text = (memory[(int)eState.ADDR_CLSR] << 0).ToString("X2");
            stateValueSCMR.Text = (memory[(int)eState.ADDR_SCMR] << 0).ToString("X2");
            stateValueVCR.Text = (memory[(int)eState.ADDR_VCR] << 0).ToString("X2");
            stateValueRAMBR.Text = (memory[(int)eState.ADDR_RAMBR] << 0).ToString("X2");
            stateValueCBR.Text = ((memory[(int)eState.ADDR_CBR + 1] << 8) | (memory[(int)eState.ADDR_CBR] << 0)).ToString("X4");
            stateValueCOLR.Text = (memory[(int)eState.ADDR_COLR] << 0).ToString("X2");
            stateValuePOR.Text = (memory[(int)eState.ADDR_POR] << 0).ToString("X2");
            stateValueSREG.Text = (memory[(int)eState.ADDR_SREG] << 0).ToString("X2");
            stateValueDREG.Text = (memory[(int)eState.ADDR_DREG] << 0).ToString("X2");
            stateValueROMRD.Text = (memory[(int)eState.ADDR_ROMRDBUF] << 0).ToString("X2");
            stateValueRAMWR.Text = (memory[(int)eState.ADDR_RAMWRBUF] << 0).ToString("X2");
            stateValueRAMAD.Text = ((memory[(int)eState.ADDR_RAMADDR + 1] << 8) | (memory[(int)eState.ADDR_RAMADDR] << 0)).ToString("X4");

            checkBoxZ.Checked  = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0002) != 0;
            checkBoxC.Checked  = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0004) != 0;
            checkBoxN.Checked  = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0008) != 0;
            checkBoxV.Checked  = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0010) != 0;
            checkBoxG.Checked  = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0020) != 0;
            checkBoxR.Checked  = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0040) != 0;
            checkBoxA1.Checked = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0100) != 0;
            checkBoxA2.Checked = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0200) != 0;
            checkBoxIL.Checked = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0400) != 0;
            checkBoxIH.Checked = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0800) != 0;
            checkBoxB.Checked  = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x1000) != 0;
            checkBoxI.Checked  = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x8000) != 0;

            uint R15 = (uint)((memory[(int)eState.ADDR_PBR] << 16) | (memory[(int)eState.ADDR_R15 + 1] << 8) | (memory[(int)eState.ADDR_R15] << 0));
            bool opComplete = memory[0x0B5] == 1;

            // decode opcode
            string opString = GetInstruction(R15prev);

            if (R15 != R15prev)
            {
                // add new operation
                if (multiByte) listBoxCode.Items.RemoveAt(listBoxCode.Items.Count - 1);
                listBoxCode.Items.Add(opString + "\n");
                if (listBoxCode.Items.Count > 50) listBoxCode.Items.RemoveAt(0);
                listBoxCode.SelectedIndex = listBoxCode.Items.Count - 1;
                multiByte = !opComplete;

                R15prev = R15;
            }
        }

        private void gsudebug_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible)
            {
                t.Tick += new EventHandler(Update);
                t.Interval = 200;
                t.Start();
                R15prev = 0xFFFFFFFF;
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

            // mask/value,reg
            reg[3] |= checkBoxBreakS.Checked ? 0x4000 : 0x0000;
            reg[3] |= checkBoxBreakE.Checked ? 0x8000 : 0x0000;

            // check if address is hex
            if (System.Text.RegularExpressions.Regex.IsMatch(textBoxBreakAddr.Text, @"\A\b[0-9a-fA-F]+\b\Z"))
            {
                reg[3] |= checkBoxBreakR.Checked ? 0x1000 : 0x0000;
                reg[3] |= checkBoxBreakW.Checked ? 0x2000 : 0x0000;
                reg[3] |= checkBoxBreakX.Checked ? 0x0800 : 0x0000;

                int addr = int.Parse(textBoxBreakAddr.Text, System.Globalization.NumberStyles.HexNumber);
                reg[0] |= ((addr >>  0) & 0xFF) << 8;
                reg[1] |= ((addr >>  8) & 0xFF) << 8;
                reg[2] |= ((addr >> 16) & 0xFF) << 8;
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

                    uint R15old = R15prev;
                    for (int i = 0; i < v; i++)
                    {
                        // print current instruction
                        var inst = GetInstruction(R15old, true);
                        file.WriteLine(inst);
                        uint R15 = (uint)((memory[(int)eState.ADDR_PBR] << 16) | (memory[(int)eState.ADDR_R15 + 1] << 8) | (memory[(int)eState.ADDR_R15] << 0));

                        parent.GSUReg(new int[] { 0x000001 | (++tempCnt << 8) });
                        while (memory[0xE0] != tempCnt) {
                            //System.Threading.Thread.Sleep(100);
                            parent.GSUUpdate();
                        };
                        R15old = R15;
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

            Byte opcode = memory[0x0B2];
            Byte operand8 = memory[0x0B3];
            int operand16 = (memory[0x0B4] << 8) | (memory[0x0B3] << 0);

            bool alt1 = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0100) != 0;
            bool alt2 = (((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0200) != 0;
            bool alt3 = alt1 && alt2;

            opString = string.Format("{0,0:x6} ", PC);
            if (!traceMode) opString += string.Format("{0,0:X2} ", opcode);

            switch (opcode)
            {
                case 0x00: opString += string.Format("{0,-5}", "stop"); break;
                case 0x01: opString += string.Format("{0,-5}", "nop"); break;
                case 0x02: opString += string.Format("{0,-5}", "cache"); break;
                case 0x03: opString += string.Format("{0,-5}", "lsr"); break;
                case 0x04: opString += string.Format("{0,-5}", "rol"); break;
                case 0x05: opString += string.Format("{0,-5} {1,2:X2}", "bra", operand8); break;
                case 0x06: opString += string.Format("{0,-5} {1,2:X2}", "bge", operand8); break;
                case 0x07: opString += string.Format("{0,-5} {1,2:X2}", "blt", operand8); break;
                case 0x08: opString += string.Format("{0,-5} {1,2:X2}", "bne", operand8); break;
                case 0x09: opString += string.Format("{0,-5} {1,2:X2}", "beq", operand8); break;
                case 0x0A: opString += string.Format("{0,-5} {1,2:X2}", "bpl", operand8); break;
                case 0x0B: opString += string.Format("{0,-5} {1,2:X2}", "bmi", operand8); break;
                case 0x0C: opString += string.Format("{0,-5} {1,2:X2}", "bcc", operand8); break;
                case 0x0D: opString += string.Format("{0,-5} {1,2:X2}", "bcs", operand8); break;
                case 0x0E: opString += string.Format("{0,-5} {1,2:X2}", "bvc", operand8); break;
                case 0x0F: opString += string.Format("{0,-5} {1,2:X2}", "bvs", operand8); break;
                case 0x10: case 0x11: case 0x12: case 0x13: case 0x14: case 0x15: case 0x16: case 0x17:
                case 0x18: case 0x19: case 0x1A: case 0x1B: case 0x1C: case 0x1D: case 0x1E: case 0x1F:
                    opString += string.Format("{0,-5} r{1,-2:G}", "to", opcode & 0xF); break;
                case 0x20: case 0x21: case 0x22: case 0x23: case 0x24: case 0x25: case 0x26: case 0x27:
                case 0x28: case 0x29: case 0x2A: case 0x2B: case 0x2C: case 0x2D: case 0x2E: case 0x2F:
                    opString += string.Format("{0,-5} r{1,-2:G}", "with", opcode & 0xF); break;
                case 0x30: case 0x31: case 0x32: case 0x33: case 0x34: case 0x35: case 0x36: case 0x37:
                case 0x38: case 0x39: case 0x3A: case 0x3B:
                    opString += string.Format("{0,-5} (r{1,-2:G})", alt1 ? "stb" : "stw", opcode & 0xF); break;
                case 0x3C: opString += string.Format("{0,-5}", "loop"); break;
                case 0x3D: opString += string.Format("{0,-5}", "alt1"); break;
                case 0x3E: opString += string.Format("{0,-5}", "alt2"); break;
                case 0x3F: opString += string.Format("{0,-5}", "alt3"); break;
                case 0x40: case 0x41: case 0x42: case 0x43: case 0x44: case 0x45: case 0x46: case 0x47:
                case 0x48: case 0x49: case 0x4A: case 0x4B:
                    opString += string.Format("{0,-5} (r{1,-2:G})", alt1 ? "ldb" : "ldw", opcode & 0xF); break;
                case 0x4C: opString += string.Format("{0,-5}", alt1 ? "rpix" : "plot"); break;
                case 0x4D: opString += string.Format("{0,-5}", "swap"); break;
                case 0x4E: opString += string.Format("{0,-5}", alt1 ? "cmode" : "color"); break;
                case 0x4F: opString += string.Format("{0,-5}", "not"); break;
                case 0x50: case 0x51: case 0x52: case 0x53: case 0x54: case 0x55: case 0x56: case 0x57:
                case 0x58: case 0x59: case 0x5A: case 0x5B: case 0x5C: case 0x5D: case 0x5E: case 0x5F:
                    opString += string.Format("{0,-5} {2,1}{1,-2:G}", alt1 ? "adc" : "add", opcode & 0xF, alt2 ? "#" : "r"); break;
                case 0x60: case 0x61: case 0x62: case 0x63: case 0x64: case 0x65: case 0x66: case 0x67:
                case 0x68: case 0x69: case 0x6A: case 0x6B: case 0x6C: case 0x6D: case 0x6E: case 0x6F:
                    opString += string.Format("{0,-5} {2,1}{1,-2:G}", alt3 ? "cmp" : alt1 ? "sbc" : "sub", opcode & 0xF, (alt2 && !alt3) ? "#" : "r"); break;
                case 0x70: opString += string.Format("{0,-5}", "merge"); break;
                           case 0x71: case 0x72: case 0x73: case 0x74: case 0x75: case 0x76: case 0x77:
                case 0x78: case 0x79: case 0x7A: case 0x7B: case 0x7C: case 0x7D: case 0x7E: case 0x7F:
                    opString += string.Format("{0,-5} {2,1}{1,-2:G}", alt1 ? "bic" : "and", opcode & 0xF, alt2 ? "#" : "r"); break;
                case 0x80: case 0x81: case 0x82: case 0x83: case 0x84: case 0x85: case 0x86: case 0x87:
                case 0x88: case 0x89: case 0x8A: case 0x8B: case 0x8C: case 0x8D: case 0x8E: case 0x8F:
                    opString += string.Format("{0,-5} {2,1}{1,-2:G}", alt1 ? "umult" : "mult", opcode & 0xF, alt2 ? "#" : "r"); break;
                case 0x90: opString += string.Format("{0,-5}", "sbk"); break;
                case 0x91: case 0x92: case 0x93: case 0x94:
                    opString += string.Format("{0,-5} #{1,1:G}", "link", opcode & 0xF); break;
                case 0x95: opString += string.Format("{0,-5}", "sex"); break;
                case 0x96: opString += string.Format("{0,-5}", alt1 ? "div2" : "asr"); break;
                case 0x97: opString += string.Format("{0,-5}", "ror"); break;
                case 0x98: case 0x99: case 0x9A: case 0x9B: case 0x9C: case 0x9D:
                    opString += string.Format("{0,-5} r{1,-2:G}", alt1 ? "ljmp" : "jmp", opcode & 0xF); break;
                case 0x9E: opString += string.Format("{0,-5}", "lob"); break;
                case 0x9F: opString += string.Format("{0,-5}", alt1 ? "lmult" : "fmult"); break;
                case 0xA0: case 0xA1: case 0xA2: case 0xA3: case 0xA4: case 0xA5: case 0xA6: case 0xA7:
                case 0xA8: case 0xA9: case 0xAA: case 0xAB: case 0xAC: case 0xAD: case 0xAE: case 0xAF:
                    opString += string.Format("{0,-5} r{1,-2:G}, {2,2:X}", alt1 ? "lms" : alt2 ? "sms" : "ibt", opcode & 0xF, operand8); break;
                case 0xB0: case 0xB1: case 0xB2: case 0xB3: case 0xB4: case 0xB5: case 0xB6: case 0xB7:
                case 0xB8: case 0xB9: case 0xBA: case 0xBB: case 0xBC: case 0xBD: case 0xBE: case 0xBF:
                    opString += string.Format("{0,-5} r{1,-2:G}", "from", opcode & 0xF); break;
                case 0xC0: opString += string.Format("{0,-5}", "hib"); break;
                           case 0xC1: case 0xC2: case 0xC3: case 0xC4: case 0xC5: case 0xC6: case 0xC7:
                case 0xC8: case 0xC9: case 0xCA: case 0xCB: case 0xCC: case 0xCD: case 0xCE: case 0xCF:
                    opString += string.Format("{0,-5} {2,1}{1,-2:G}", alt1 ? "xor" : "or", opcode & 0xF, alt2 ? "#" : "r"); break;
                case 0xD0: case 0xD1: case 0xD2: case 0xD3: case 0xD4: case 0xD5: case 0xD6: case 0xD7:
                case 0xD8: case 0xD9: case 0xDA: case 0xDB: case 0xDC: case 0xDD: case 0xDE:
                    opString += string.Format("{0,-5} r{1,-2:G}", "inc", opcode & 0xF); break;
                case 0xDF: opString += string.Format("{0,-5}", alt3 ? "romb" : alt2 ? "ramb" : "getc"); break;
                case 0xE0: case 0xE1: case 0xE2: case 0xE3: case 0xE4: case 0xE5: case 0xE6: case 0xE7:
                case 0xE8: case 0xE9: case 0xEA: case 0xEB: case 0xEC: case 0xED: case 0xEE:
                    opString += string.Format("{0,-5} r{1,-2:G}", "dec", opcode & 0xF); break;
                case 0xEF: opString += string.Format("{0,-5}", alt3 ? "getbs" : alt2 ? "getbl" : alt1 ? "getbh" : "getb"); break;
                case 0xF0: case 0xF1: case 0xF2: case 0xF3: case 0xF4: case 0xF5: case 0xF6: case 0xF7:
                case 0xF8: case 0xF9: case 0xFA: case 0xFB: case 0xFC: case 0xFD: case 0xFE: case 0xFF:
                    opString += string.Format("{0,-5} r{1,-2:G}, {2,4:X}", alt1 ? "lm" : alt2 ? "sm" : "iwt", opcode & 0xF, operand16); break;

                default: opString += string.Format("error"); break;
            }

            opString = opString.PadRight(24);

            opString += " S:" + ((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)).ToString("x4");
            opString += " " +
                        (((((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0002) != 0) ? "Z" : "z") +
                        (((((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0004) != 0) ? "C" : "c") +
                        (((((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0008) != 0) ? "N" : "n") +
                        (((((memory[(int)eState.ADDR_SFR + 1] << 8) | (memory[(int)eState.ADDR_SFR] << 0)) & 0x0010) != 0) ? "V" : "v");

            if (traceMode)
            {
                opString += " ";
                for (int i = 0; i < 16; i++)
                {
                    int reg = ((memory[2 * i + 1] << 8) | (memory[2 * i] << 0));
                    opString += string.Format("R{0,-2:G}:{1,4:x4} ", i, reg);
                }
            }

            return opString;
        }

    }
}
