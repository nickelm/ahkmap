using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace AutoHotKeyMap
{
    public partial class MainForm : Form
    {
        public const int ViewportMargin = 8;
        public const int KeyPadding = 2;
        public const int KeyRadius = 5;
        public const int LabelWidth = 200;

        public const string BuildString = "Version 1.0 - August 10, 2021.";

        private OpenFileDialog openFileDialog = new OpenFileDialog();
        private Dictionary<string, List<HotKey>> hotkeys = new Dictionary<string, List<HotKey>>();

        private Pen stroke = new Pen(Color.White);
        private Pen greenStroke = new Pen(Color.LimeGreen, 2);
        private SolidBrush whiteBrush = new SolidBrush(Color.White);
        private SolidBrush grayBrush = new SolidBrush(Color.LightGray);
        private SolidBrush blackBrush = new SolidBrush(Color.Black);
        private SolidBrush yellowBrush = new SolidBrush(Color.Yellow);
        private SolidBrush darkBrush = new SolidBrush(Color.FromArgb(255, 32, 32, 32));
        private Font headerFont = new Font("Consolas", 14, FontStyle.Regular);
        private Font smallerFont = new Font("Consolas", 9, FontStyle.Regular);

        private LabelBank leftBank, rightBank;

        private class Modifier
        {
            public bool Alt { get; set; }
            public bool Shift { get; set; }
            public bool Ctrl { get; set; }
            public Modifier()
            {
                Alt = Shift = Ctrl = false;
            }
        }

        private class HotKey
        {
            public string Key { get; }
            public string Function { get; }
            public Modifier Mod { get;  }
            public HotKey(string key, Modifier mod, string function)
            {
                Key = key;
                Mod = mod;
                Function = function;
            }
        }

        private class LabelBank
        {
            private const int SlotSize = 12;
            private float start, size;
            private bool[] slots;
            public LabelBank(float start, float size)
            {
                this.start = start;
                this.size = size;
                int numSlots = (int) size / SlotSize;
                slots = new bool[numSlots];
            }

            public float GetFreePos(float pos)
            {
                // Find slot
                int slot = (int) ((pos - start) / SlotSize);
                if (slot < 0 || slot >= slots.Length) return pos;

                // Find the next available slot
                // FIXME: Some error occurring here
                while (slots[slot] == true)
                {
                    slot++;
                    if (slot >= slots.Length)
                    {
                        slot = slots.Length - 1;
                        break;
                    }
                }

                // Mark this slot as taken
                slots[slot] = true;

                // Return the slot position
                return start + slot * SlotSize;
            }
        }

        public MainForm(string[] args)
        {
            InitializeComponent();

            // Set file dialog properties
            openFileDialog.DefaultExt = "ahk";
            openFileDialog.Filter = "AHK files (*.ahk)|*.ahk|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.Multiselect = false;

            // Enable double buffering for the rendering window
            typeof(Panel).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, MainPanel, new object[] { true });
            Resize += new EventHandler(MainForm_Refresh);
            LocationChanged += new EventHandler(MainForm_Refresh);

            KeyDown += new KeyEventHandler(MainForm_KeyDown);

            // Set the line cap
            AdjustableArrowCap bigArrow = new AdjustableArrowCap(5, 5);
            greenStroke.CustomEndCap = bigArrow;
            greenStroke.StartCap = LineCap.SquareAnchor;

            // Is there a command line argument?
            if (args.Length > 0)
            {
                // Yes, so load the script
                hotkeys = ReadScript(args[0]);

                // Turn off the window decorations
                ToggleWindowDecorations();
            }
        }

        private void MainForm_Refresh(object sender, EventArgs e)
        {
            Refresh();
        }

        private static Dictionary<string, List<HotKey>> ReadScript(string filename)
        {
            Dictionary<string, List<HotKey>> hotkeys = new Dictionary<string, List<HotKey>>();
            string line;
            string lastComment = "";
            bool multiline = false;

            // Read the file 
            System.IO.StreamReader file = new System.IO.StreamReader(filename);
            while ((line = file.ReadLine()) != null)
            {
                // Trim whitespace
                line = line.Trim();

                // Are we in a multiline comment?
                if (multiline)
                {
                    // Check if it is being ended here
                    if (line.Contains("*/")) multiline = false;
                    
                    // Regardless, we should skip this line
                    continue;
                }

                // Are we starting in a multiline comment?
                if (line.StartsWith("/*"))
                {
                    multiline = true;
                    continue;
                }

                // Is this a descriptive comment?
                if (line.StartsWith(";"))
                {
                    char[] charsToTrim = { ';', ' ', '\t', '>', '-', '<' };
                    lastComment = line.Trim(charsToTrim);
                    continue;
                }

                // Is there a hotkey specification here?
                int pos = line.IndexOf("::");
                if (pos == -1) continue;

                // Parse the hokey
                StringBuilder key = new StringBuilder();
                Modifier mod = new Modifier();
                for (int i = 0; i < pos; i++)
                {
                    switch (line[i].ToString())
                    {
                        case "!": mod.Alt = true; break;
                        case "+": mod.Shift = true; break;
                        case "^": mod.Ctrl = true; break;
                        case "$": break;
                        default:
                            key.Append(line[i]);
                            break;
                    }
                }

                // If there is no binding already, create an empty list
                var currKey = key.ToString().ToLower();
                if (!hotkeys.ContainsKey(currKey))
                {
                    hotkeys.Add(currKey, new List<HotKey>());
                }

                // Now create it and add it
                hotkeys[currKey].Add(new HotKey(key.ToString().ToLower(), mod, lastComment));
            }

            // Close the file
            file.Close();

            return hotkeys;
        }

        private void OpenAHKScriptToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Select a file and leave if none was selected
            if (openFileDialog.ShowDialog() != DialogResult.OK) return;

            // Read the AHK script file and extract the hotkeys and comments
            hotkeys = ReadScript(openFileDialog.FileName);

            // Set the window title
            this.Text = Path.GetFileName(openFileDialog.FileName);

            // Force redraw
            Refresh();
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        public static void DrawRoundedRectangle(Graphics graphics, Pen pen, Rectangle bounds, int cornerRadius)
        {
            using (GraphicsPath path = RoundedRect(bounds, cornerRadius))
            {
                graphics.DrawPath(pen, path);
            }
        }
        public static void FillRoundedRectangle(Graphics graphics, Brush brush, Rectangle bounds, int cornerRadius)
        {
            using (GraphicsPath path = RoundedRect(bounds, cornerRadius))
            {
                graphics.FillPath(brush, path);
            }
        }

        private void DrawKey(Graphics g, int x, int y, int width, int height, string key)
        {
            // Is this key used?
            if (hotkeys.ContainsKey(key.ToLower()))
            {
                // Draw it solid
                FillRoundedRectangle(g, yellowBrush, new Rectangle(x + KeyPadding, y + KeyPadding, width - 2 * KeyPadding, height - 2 * KeyPadding), KeyRadius);

                // Draw the key string
                g.DrawString(key, headerFont, blackBrush, x + KeyPadding, y + KeyPadding);
            }

            // No it is not
            else
            {
                // Draw it solid
                FillRoundedRectangle(g, darkBrush, new Rectangle(x + KeyPadding, y + KeyPadding, width - 2 * KeyPadding, height - 2 * KeyPadding), KeyRadius);

                // Draw the key string
                g.DrawString(key, headerFont, whiteBrush, x + KeyPadding, y + KeyPadding);
            }

            // Now draw the outline
            DrawRoundedRectangle(g, stroke, new Rectangle(x + KeyPadding, y + KeyPadding, width - 2 * KeyPadding, height - 2 * KeyPadding), KeyRadius);
        }

        private void DrawLabel(Graphics g, Rectangle viewport, Rectangle keyboardView, float keyX, float keyY, float keyDim, string key)
        {
            foreach (HotKey currKey in hotkeys[key.ToLower()])
            {
                LabelBank bank = leftBank;

                // Construct the label
                string modString = (currKey.Mod.Ctrl ? "C" : "") + (currKey.Mod.Shift ? "S" : "") + (currKey.Mod.Alt ? "A" : "");
                string label = modString == "" ? currKey.Function : "[" + modString + "] " + currKey.Function;

                // Which side of the keyboard?
                float offset = g.MeasureString(label, smallerFont).Width;
                float posX = keyX < (viewport.X + viewport.Width / 2) ? viewport.X + ViewportMargin : (viewport.X + viewport.Width - offset - ViewportMargin);
                if (keyX > (viewport.X + viewport.Width / 2))
                {
                    offset = 0;
                    bank = rightBank;
                }

                // Now find the next available vertical space
                float posY = bank.GetFreePos(keyY + 2 * KeyPadding);

                // Draw the function string
                g.DrawString(label, smallerFont, grayBrush, posX, posY);

                // Do we need a broken line or not?
                if (posY > keyY + keyDim)
                {
                    int midX = (int) (LabelWidth + (posY - keyY));
                    Point[] points =
                    {
                        new Point((int) (posX + offset), (int) (posY + 8)),
                        new Point(midX, (int) (posY + 8)),
                        new Point(midX, (int) (keyY + 2 * KeyPadding)),
                        new Point((int) keyX, (int) (keyY + + 2 * KeyPadding)),
                    };

                    // Draw a poly line
                    g.DrawLines(greenStroke, points);
                }
                else
                {
                    // Draw a single line
                    g.DrawLine(greenStroke, posX + offset, posY + 8, keyX, posY + 8);
                }
            }
        }

        private void DrawKeyMap(Graphics g, Rectangle viewport)
        {
            string fullKeyboard =
                "1{Esc}1{}1{F1}1{F2}1{F3}1{F4}1{}1{F5}1{F6}1{F7}1{F8}1{}1{F9}1{F10}1{F11}1{F12}\\" +
                "1.5{~}1{1}1{2}1{3}1{4}1{5}1{6}1{7}1{8}1{9}1{0}1{-}1{=}2.5{Bkspc}\\" +
                "2{Tab}1{Q}1{W}1{E}1{R}1{T}1{Y}1{U}1{I}1{O}1{P}1{[}1{]}2{Bkslsh}\\" +
                "2.5{CapsLock}1{A}1{S}1{D}1{F}1{G}1{H}1{J}1{K}1{L}1{;}1{'}2.5{Enter}\\" +
                "3{Shift}1{Z}1{X}1{C}1{V}1{B}1{N}1{M}1{,}1{.}1{/}3{Shift}\\" +
                "1.5{Ctrl}1.5{Win}1.5{Alt}6{Space}1.5{Alt}1.5{Fn}1{Ctx}1.5{Ctrl}";
            string smallKeyboard =
                "1{Esc}1{}1{F1}1{F2}1{F3}1{F4}\\" +
                "1.5{~}1{1}1{2}1{3}1{4}1{5}\\" +
                "2{Tab}1{Q}1{W}1{E}1{R}1{T}\\" +
                "2.5{CapsLock}1{A}1{S}1{D}1{F}1{G}\\" +
                "3{Shift}1{Z}1{X}1{C}1{V}1{B}\\" +
                "1.5{Ctrl}1.5{Win}1.5{Alt}3.5{Space}";

            // Choose the keyboard to use
            string keyboardLayout = FullKeyboardMenuItem.Checked ? fullKeyboard : smallKeyboard;

            // Keyboard viewport
            Rectangle keyboardView = new Rectangle(viewport.X + LabelWidth, viewport.Y, viewport.Width - 2 * LabelWidth, viewport.Height);

            // Create label banks
            leftBank = new LabelBank(keyboardView.Y, keyboardView.Height);
            rightBank = new LabelBank(keyboardView.Y, keyboardView.Height);

            // Count the number of rows and columns
            string[] keyboardRows = keyboardLayout.Split('\\');
            float keyboardColumns = 0;
            for (int row = 0; row < keyboardRows.Length; row++)
            {
                string currRow = keyboardRows[row];
                float currWidth = 0;

                // Extract the commands
                string[] commands = keyboardRows[row].Split('}');

                // Parse a command at a time
                for (int ndx = 0; ndx < commands.Length; ndx++)
                {
                    int delim = commands[ndx].IndexOf("{");
                    if (delim == -1) continue;
                    currWidth += Single.Parse(commands[ndx].Substring(0, delim));
                }

                // Update as need be
                if (currWidth > keyboardColumns) keyboardColumns = currWidth;
            }

            // Now calculate the button sizes
            int keyWidth = (int) (keyboardView.Width / keyboardColumns);
            int keyHeight = keyboardView.Height / keyboardRows.Length;
            int keyDim = Math.Min(keyWidth, keyHeight);

            // Center the viewport
            float diff = keyboardView.Width - keyDim * keyboardColumns;
            keyboardView.X += (int) (diff / 2);
            keyboardView.Width = (int) (keyDim * keyboardColumns);

            // Clear the window
            g.Clear(Color.Black);

            // Set graphics rendering
            g.SmoothingMode = SmoothingMode.HighQuality;

            // Key storage
            Dictionary<string, Point> keyTargets = new Dictionary<string, Point>();

            // Let's draw them!
            for (int row = 0; row < keyboardRows.Length; row++)
            {
                float posX = 0;

                // Extract the commands
                string[] commands = keyboardRows[row].Split('}');

                // Draw a command at a time
                for (int ndx = 0; ndx < commands.Length; ndx++)
                {
                    // Extract the parameters
                    int delim = commands[ndx].IndexOf("{");
                    if (delim == -1) continue;
                    float len = Single.Parse(commands[ndx].Substring(0, delim));
                    string key = commands[ndx].Substring(delim + 1);

                    // Draw a key if there is one
                    if (key != "")
                    {
                        // Draw the key
                        DrawKey(g, (int) (posX * keyDim + keyboardView.X), row * keyDim + keyboardView.Y, (int) (keyDim * len), keyDim, key);

                        // Add the label for drawing if needed
                        if (hotkeys.ContainsKey(key.ToLower()))
                        {
                            keyTargets.Add(key.ToLower(), new Point((int) ((posX + 0.5f) * keyDim + keyboardView.X), row * keyDim + keyboardView.Y));
                        }
                    }

                    // Advance appropriately
                    posX += len;
                }
            }

            // Finally, draw the labels (they should be in front of everything else
            foreach (KeyValuePair<string, Point> entry in keyTargets)
            {
                DrawLabel(g, viewport, keyboardView, entry.Value.X, entry.Value.Y, keyDim, entry.Key);
            }

            // Draw usage instructions
            string helpMsg = "Esc - exit | F11 - toggle window";
            float msgWidth = g.MeasureString(helpMsg, smallerFont).Width;
            g.FillRectangle(yellowBrush, viewport.Width / 2 - msgWidth / 2, viewport.Y + viewport.Height - 12, msgWidth, 14);
            g.DrawString(helpMsg, smallerFont, blackBrush, viewport.Width / 2 - msgWidth / 2, viewport.Y + viewport.Height - 12);
        }

        private void FullKeyboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Refresh();
        }

        private void MadgrimsDAoCWebsiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://sites.google.com/view/daoc-proposals/");
        }

        private void AboutMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("AutoHotKeyMap by Madgrim Laeknir.\n" + BuildString, "AutoHotKeyMap", MessageBoxButtons.OK);
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Application.Exit();
            }
            else if (e.KeyCode == Keys.F11)
            {
                ToggleWindowDecorations();
            }
        }

        private void ToggleWindowDecorations()
        {
            // Turn off everything
            if (FormBorderStyle == FormBorderStyle.Sizable)
            {
                menuStrip.Visible = false;
                FormBorderStyle = FormBorderStyle.None;
            }
            else
            {
                menuStrip.Visible = true;
                FormBorderStyle = FormBorderStyle.Sizable;
            }

            // Redraw the screen
            Refresh();
        }

        private void MainPanel_Paint(object sender, PaintEventArgs e)
        {
            // Define the viewport
            Rectangle viewport = new Rectangle(e.ClipRectangle.X + ViewportMargin, e.ClipRectangle.Y + ViewportMargin,
                e.ClipRectangle.Width - 2 * ViewportMargin, e.ClipRectangle.Height - 2 * ViewportMargin);

            // Draw the keymap
            DrawKeyMap(e.Graphics, viewport);
        }
    }
}
