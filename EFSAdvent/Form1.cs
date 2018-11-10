using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace EFSAdvent
{
    public partial class Form1 : Form
    {
        Bitmap tilesetBitmap, layersBitmap, layersBuffer, layersBackbuffer, csvBitmap, 
            currentTileBitmap, actorSpritesBitmap, actorsBuffer, actorsBackbuffer, currentActorBitmap;
        Graphics csvMapGraphics, layerGraphics, actorsBufferGraphics, layersBufferGraphics;
        int currentTile;
        int[,] map;
        int layerWidth, layerHeight;
        int tileSize = 16;
        ushort[,] szsData = new ushort[16,1024];

        ushort[,] undoSteps = new ushort[50,3]; //Tile value, layer, tile number
        int undoPosition = 0;
        int actorClickedOn = 1000;

        //Prep szs filepath
        string szsFilePath;
        string[] szsFilesPaths = new string[16];
        bool layerDataHasChanged = false;

        string room;

        struct actor
        {
            internal string name;
            internal byte layer;
            internal byte xCoord;
            internal byte yCoord;
            internal byte variable1;
            internal byte variable2;
            internal byte variable3;
            internal byte variable4;
        }
        actor[] actorData = new actor[0];

        //For csv files
        string csvMapName;
        string csvMapNumber;
        string csvPath;
        byte currentRoomNumber;
        byte currentRoomSelected;
        byte[] csvData = new byte[100];
        int[] csvHeader = new int[9];

        string[] actorNames = new string[352];
        bool actorsHaveBeenChanged = false;
        string actorsFilePath;

        public Form1()
        {
            InitializeComponent();
            tilesetBitmap = new Bitmap("data\\Tile Sheet 00.PNG"); //Load default tilesheet
            tilePictureBox.Image = tilesetBitmap;

            currentTileBitmap = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            currentTilePictureBox.Image = currentTileBitmap;

            layerWidth = 32; //Set width height of layers, as number of tiles, not pixels
            layerHeight = 32;
            map = new int[layerWidth, layerHeight];

            layersBuffer = new Bitmap(layerWidth * tileSize, layerHeight * tileSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            layersBackbuffer = new Bitmap(layerWidth * tileSize, layerHeight * tileSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            layersBufferGraphics = Graphics.FromImage(layersBuffer);
            
            //Setup main window graphics
            layersBitmap = new Bitmap(layerWidth * tileSize, layerHeight * tileSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            layerGraphics = Graphics.FromImage(layersBitmap);
            layersPictureBox.Image = layersBitmap;

            //Prep CSV
            csvBitmap = new Bitmap(200, 200, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            csvPictureBox.Image = csvBitmap;
            csvMapGraphics = Graphics.FromImage(csvBitmap);

            comboBox1.SelectedIndex = 0;

            //Prep Actor SpriteSheet
            actorSpritesBitmap = new Bitmap("data\\FSA Actors Sprite Sheet.png");
            actorsBuffer = new Bitmap(layerWidth * tileSize, layerHeight * tileSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            actorsBackbuffer = new Bitmap(layerWidth * tileSize, layerHeight * tileSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            actorsBufferGraphics = Graphics.FromImage(actorsBuffer);

            currentActorBitmap = new Bitmap(64, 64);
            ActorInfoPictureBox.Image = currentActorBitmap;

            brushSizeComboBox.SelectedIndex = 0;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadActorNames();
        }

        private void LoadActorNames()
        {
            //Open the file of names
            FileStream actorNamesLoader = new FileStream("data\\FSA Actor Namelist.txt", FileMode.Open);

            byte[] names = new byte[actorNamesLoader.Length];
            actorNamesLoader.Read(names, 0, names.Length);
            actorNamesLoader.Close();

            char[] name = new char[4];

            //Read them in without the linebreaks
            for (int i = 0; i < 352; i++)
            {
                name[0] = (char)names[i * 6];
                name[1] = (char)names[i * 6 + 1];
                name[2] = (char)names[i * 6 + 2];
                name[3] = (char)names[i * 6 + 3];
                actorNames[i] = new string(name);
            }
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //map = new int[map.GetLength(0), map.GetLength(1)];
            szsData = new ushort[16, 1024];
            DrawFullLayer(0, szsData);
            layersPictureBox.Refresh();
            csvData = new byte[100];
            for (int i = 0; i < csvData.Length; i++)
                csvData[i] = 0xFF;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "CSV map files|*.csv";

            if (openFile.ShowDialog() == DialogResult.OK)
            {
                if (openFile.FileName.EndsWith("csv"))
                {
                    csvPath = openFile.FileName;

                    //Read in csv file and get map name
                    byte[] readBuffer = File.ReadAllBytes(openFile.FileName);
                    csvMapName = "map" + Convert.ToString((readBuffer[3] - 0x30)) + Convert.ToString((readBuffer[4] - 0x30)) + Convert.ToString((readBuffer[5] - 0x30));
                    csvMapNumber = Convert.ToString((readBuffer[3] - 0x30)) + Convert.ToString((readBuffer[4] - 0x30)) + Convert.ToString((readBuffer[5] - 0x30));
                    csvVariablesGroupBox.Text = csvMapName;

                    //Get a string which is just the root bossxxx filepath for loading other files
                    int charsIn = 14;
                    string tempStrng = openFile.FileName;
                    if (tempStrng[tempStrng.Length - 6] == '_')
                        charsIn = 16;
                    filepathTextBox.Text = tempStrng.Remove((tempStrng.Length - charsIn), charsIn);

                    //Load csv header variables into csvHeader
                    int readFrom = 7;
                    for (int i = 0; i < csvHeader.Length; i++)
                    {
                        csvHeader[i] = (readBuffer[readFrom] - 0x30);
                        readFrom++;
                        if (readBuffer[readFrom] < 0x30)
                            readFrom++;
                        else
                        {
                            csvHeader[i] = ((csvHeader[i] * 10) + (readBuffer[readFrom] - 0x30));
                            readFrom += 2;
                        }
                    }

                    //Put variables into their text boxes
                    FillOutCsvVariableBoxes(csvHeader);

                    //Select the right tilesheet
                    comboBox1.SelectedIndex = csvHeader[4];
                    ChangeTileSet();

                    //Set readBuffer postion to start of layout data
                    readFrom = 0;

                    //Fill out csvData
                    for (int y = 0; y < 10; y++)
                    {
                        while (readBuffer[readFrom] != 0x0A)
                            readFrom++;
                        readFrom++;
                        for (int x = 0; x < 10; x++)
                        {
                            //If it's a NULL entry save 0xFF and skip ahead 5 bytes
                            if (readBuffer[readFrom] == 0x4E)
                            {
                                readFrom += 5;
                                //NULL entries are saved as 0xFF, as you should never really need more than 100 rooms
                                csvData[x + (y * 10)] = 0xFF;
                            }
                            //Otherwise work out what number it is
                            else
                            {
                                int roomValue = 0;
                                roomValue = readBuffer[readFrom] - 0x30;
                                readFrom++;
                                //Check to see if the next byte is a comma or "end of line" 0x0D
                                if (readBuffer[readFrom] != 0x2C && readBuffer[readFrom] != 0x0D)
                                    {
                                        roomValue = (roomValue * 10) + (readBuffer[readFrom] - 0x30);
                                        readFrom++;
                                        if (readBuffer[readFrom] != 0x2C)
                                            if (readBuffer[readFrom] != 0x0D)
                                                roomValue = (roomValue * 10) + (readBuffer[readFrom] - 0x30);
                                    }
                                readFrom++;
                                csvData[x + (y * 10)] = (byte)roomValue;
                            }
                        }
                    }
                    DrawCsvMap();
                    layerGraphics.Clear(Color.FromArgb(0, 0, 0, 0));
                    layersPictureBox.Refresh();

                    //Enable csv related tools
                    csvRoomUpdate.Enabled = true;
                    csvSaveButton.Enabled = true;
                    exportToolStripMenuItem.Enabled = true;
                }
                else
                    MessageBox.Show("File must be a *.csv");
            }
        }

        private void FillOutCsvVariableBoxes(int[] variables)
        {
            int i = 0;
            csvVariableBox1.Text = Convert.ToString(variables[i]);
            i++;
            csvVariableBox2.Text = Convert.ToString(variables[i]);
            i++;
            csvVariableBox3.Text = Convert.ToString(variables[i]);
            i++;
            csvVariableBox4.Text = Convert.ToString(variables[i]);
            i++;
            csvVariableBox5.Text = Convert.ToString(variables[i]);
            i++;
            csvVariableBox6.Text = Convert.ToString(variables[i]);
            i++;
            csvVariableBox7.Text = Convert.ToString(variables[i]);
            i++;
            csvVariableBox8.Text = Convert.ToString(variables[i]);
            i++;
            csvVariableBox9.Text = Convert.ToString(variables[i]);
        }

        private void DrawCsvMap()
        {
            Font serif = new Font("Microsoft Sans Serif", 7);
            Brush brush = Brushes.White;
            int dataPostion = 0;

            //Iterate through csvData drawing each entry as a room on the map
            for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                {
                    //If it's a NULL(0xFF) entry draw white
                    if (csvData[dataPostion] == 0xFF)
                    {
                        for (int px = 0; px < 20; px++)
                            for (int py = 0; py < 20; py++)
                                csvBitmap.SetPixel((x * 20) + px, (y * 20) + py, Color.FromArgb(255, 255, 255));
                    }
                    //Otherwise draw the room as black
                    else
                    {
                        for (int px = 0; px < 20; px++)
                            for (int py = 0; py < 20; py++)
                                csvBitmap.SetPixel((x * 20) + px, (y * 20) + py, Color.FromArgb(0,0,0,0));

                        //Draw the room number over the top of the block for clarity
                        csvMapGraphics.DrawString(Convert.ToString(csvData[dataPostion]), serif, brush, (x * 20), (y * 20));
                    }

                    dataPostion++;
                }

            csvPanel.Refresh();
        }

        private void csvPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            //When the user clicks in the csvMapBox load the value of the clicked room into the edit box
            csvRoomEditTextbox.Text = Convert.ToString(csvData[((e.Y / 20) * 10) + (e.X / 20)]);
            currentRoomNumber = (byte)(csvData[((e.Y / 20) * 10) + (e.X / 20)]);
            currentRoomSelected = (byte)(((e.Y / 20) * 10) + (e.X / 20));

            //Check whether or not the value is a NULL(255) value and if so disable the load button
            if (currentRoomNumber == 0xFF)
                csvRoomLoad.Enabled = false;
            else
                csvRoomLoad.Enabled = true;

            //Redraw the csvMap to remove any old room selections then draw the current one
            DrawCsvMap();
            SolidBrush brush = new SolidBrush(Color.FromArgb(100, 0, 255, 0));
            csvMapGraphics.FillRectangle(brush, ((e.X / 20) * 20), ((e.Y / 20) * 20), 20, 20);
            csvPanel.Refresh();
        }

        private void csvRoomUpdate_Click(object sender, EventArgs e)
        {
            csvData[currentRoomSelected] = Convert.ToByte(csvRoomEditTextbox.Text);
            DrawCsvMap();
        }

        private void csvSaveButton_Click(object sender, EventArgs e)
        {
            //Write the mapname
            FileStream writeCSV = new FileStream(csvPath, FileMode.Create);
            writeCSV.WriteByte(Convert.ToByte(csvMapName[0]));
            writeCSV.WriteByte(Convert.ToByte(csvMapName[1]));
            writeCSV.WriteByte(Convert.ToByte(csvMapName[2]));
            writeCSV.WriteByte(Convert.ToByte(csvMapName[3]));
            writeCSV.WriteByte(Convert.ToByte(csvMapName[4]));
            writeCSV.WriteByte(Convert.ToByte(csvMapName[5]));

            //Get all the variables in a list to itterate through
            ArrayList csvVariableBoxes = new ArrayList();
            csvVariableBoxes.Add(csvVariableBox1.Text);
            csvVariableBoxes.Add(csvVariableBox2.Text);
            csvVariableBoxes.Add(csvVariableBox3.Text);
            csvVariableBoxes.Add(csvVariableBox4.Text);
            csvVariableBoxes.Add(csvVariableBox5.Text);
            csvVariableBoxes.Add(csvVariableBox6.Text);
            csvVariableBoxes.Add(csvVariableBox7.Text);
            csvVariableBoxes.Add(csvVariableBox8.Text);
            csvVariableBoxes.Add(csvVariableBox9.Text);

            //Write each variable with a comma in front.
            for (int i = 0; i < csvVariableBoxes.Count; i++)
            {
                writeCSV.WriteByte(0x2C);
                string currentVar = (string)csvVariableBoxes[i];
                if (currentVar.Length == 1)
                    writeCSV.WriteByte(Convert.ToByte(currentVar[0]));
                else
                    if (currentVar.Length == 2)
                    {
                        writeCSV.WriteByte(Convert.ToByte(currentVar[0]));
                        writeCSV.WriteByte(Convert.ToByte(currentVar[1]));
                    }
                    else
                        if (currentVar.Length == 3)
                        {
                            writeCSV.WriteByte(Convert.ToByte(currentVar[0]));
                            writeCSV.WriteByte(Convert.ToByte(currentVar[1]));
                            writeCSV.WriteByte(Convert.ToByte(currentVar[2]));
                        }
            }
            writeCSV.WriteByte(0x0D);
            writeCSV.WriteByte(0x0A);
            //now on a new line

            //Write "NULL" if it's a 255
            //Otherwise figure out if it's a 1,2 or 3 digit number and write it out
            for (int y = 0; y < 100; y += 10)
            {
                //Do blocks of ten per line
                for (int x = 0; x < 10; x++)
                {
                    if (x != 0)
                        writeCSV.WriteByte(0x2C);
                    if (csvData[x + y] == 0xFF)
                    {
                        writeCSV.WriteByte(0x4E);
                        writeCSV.WriteByte(0x55);
                        writeCSV.WriteByte(0x4C);
                        writeCSV.WriteByte(0x4C);
                    }
                    else
                    {
                        if (csvData[x + y] < 10)
                        {
                            byte temp = (byte)(csvData[x + y] + 0x30);
                            writeCSV.WriteByte(temp);
                        }
                        else
                        {
                            if (csvData[x + y] < 100)
                            {
                                string tempS = Convert.ToString(csvData[x + y]);
                                writeCSV.WriteByte(Convert.ToByte(tempS[0]));
                                writeCSV.WriteByte(Convert.ToByte(tempS[1]));
                            }
                            else
                            {
                                string tempS = Convert.ToString(csvData[x + y]);
                                writeCSV.WriteByte(Convert.ToByte(tempS[0]));
                                writeCSV.WriteByte(Convert.ToByte(tempS[1]));
                                writeCSV.WriteByte(Convert.ToByte(tempS[2]));
                            }
                        }
                    }

                }
                writeCSV.WriteByte(0x0D);
                writeCSV.WriteByte(0x0A);
            }

            //Write the end of file marker
            writeCSV.WriteByte(0x65);
            writeCSV.WriteByte(0x6E);
            writeCSV.WriteByte(0x64);
            writeCSV.WriteByte(0x2C);
            writeCSV.WriteByte(0x2C);
            writeCSV.WriteByte(0x2C);
            writeCSV.WriteByte(0x2C);
            writeCSV.WriteByte(0x0D);
            writeCSV.WriteByte(0x0A);

            //Finally it's over ^-^
            writeCSV.Flush();
            writeCSV.Close();

        }


        private void csvRoomLoad_Click(object sender, EventArgs e)
        {
            if (actorsHaveBeenChanged == true)
                switch (MessageBox.Show("Actor Data has been changed, save changes first?",
                            "Save changes?",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question))
                {
                    case DialogResult.Yes:
                        SaveActors();
                        break;

                    case DialogResult.No:
                        actorsHaveBeenChanged = false;
                        break;
                }
            if (layerDataHasChanged == true)
                switch (MessageBox.Show("Layer Data has been changed, save changes first?",
                            "Save changes?",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question))
                {
                    case DialogResult.Yes:
                        SaveSZSData();
                        break;

                    case DialogResult.No:
                        layerDataHasChanged = false;
                        break;
                }

            undoPosition = 0;

            string szsDirectory = filepathTextBox.Text + @"szs\m" + csvMapNumber + @"\";

            //Make sure a number has been put in the box, not letters or symbols
            try
            {
                int canIt = Convert.ToInt32(csvRoomEditTextbox.Text);
            }
            catch (FormatException)
            {
                MessageBox.Show("You must enter a valid NUMBER in the room number box.");
                csvRoomEditTextbox.Text = "0";
                return;
            }

            if (Convert.ToInt32(csvRoomEditTextbox.Text) < 10)
                room = "0" + csvRoomEditTextbox.Text;
            else
                room = csvRoomEditTextbox.Text;

            //Wipe graphics clean before we draw on top
            layerGraphics.Clear(Color.FromArgb(0, 0, 0, 0));

            //Load szsData into array
            //Begin by creating the szs files name
            szsData = new ushort[16, 1024];
            for (int num1 = 1; num1 < 3; num1++)
                for (int num2 = 0; num2 < 8; num2++)
                {
                    int tempNum = num2;
                    if (num1 == 2)
                        tempNum = num2 + 8;
                    szsFilePath = szsDirectory + "d_" + csvMapName + "_" + room + "_mmm_" + (num1) + "_" + num2 + ".szs";
                    szsFilesPaths[tempNum] = szsFilePath;

                    byte[] decomData = uncompressedData(szsFilePath);

                    for (int i = 0; i < 1024; i++)
                        szsData[tempNum, i] = (ushort)(decomData[i * 2] + (decomData[(i * 2) + 1] << 8));
                }
            //Check only Base layers
            for (int i = 1; i < 16; i++)
                layersCheckList.SetItemChecked(i, false);
            layersCheckList.SetItemChecked(0, true);
            layersCheckList.SetItemChecked(8, true);

            LoadActors();

            UpdateMainWindow(false);
        }

        private byte[] uncompressedData(string szsFilePath)
        {

            int srcPlace = 16, dstPlace = 0; //current read/write positions
            byte[] src;
            byte[] dst = new byte[2048];

            try
            {
                src = File.ReadAllBytes(szsFilePath);
            }
            catch (FileNotFoundException)
            {
                debugText.AppendText("\r\n" + "File not found:" + "\r\n" + szsFilePath);
                return dst;
            }

            string yaz0 = (Convert.ToString(src[0]) + Convert.ToString(src[1]) + Convert.ToString(src[2]) + Convert.ToString(src[3]));
            if (yaz0 != "899712248")//If the file is not Yaz0 compressed
                return src;

            uint validBitCount = 0; //number of valid bits left in "code" byte
            byte currCodeByte = 0;
            while (dstPlace < 2048)
            {
                //read new "code" byte if the current one is used up
                if (validBitCount == 0)
                {
                    currCodeByte = src[srcPlace];
                    ++srcPlace;
                    validBitCount = 8;
                }

                if ((currCodeByte & 0x80) != 0)
                {
                    //straight copy
                    dst[dstPlace] = src[srcPlace];
                    dstPlace++;
                    srcPlace++;
                }
                else
                {
                    //RLE part
                    byte byte1 = src[srcPlace];
                    byte byte2 = src[srcPlace + 1];
                    srcPlace += 2;

                    int dist = ((byte1 & 0xF) << 8) | byte2;
                    int copySource = dstPlace - (dist + 1);

                    int numBytes = byte1 >> 4;
                    if (numBytes == 0)
                    {
                        numBytes = src[srcPlace] + 0x12;
                        srcPlace++;
                    }
                    else
                        numBytes += 2;

                    //copy run
                    for (int i = 0; i < numBytes; ++i)
                    {
                        dst[dstPlace] = dst[copySource];
                        copySource++;
                        dstPlace++;
                    }
                }

                //use next bit from "code" byte
                currCodeByte <<= 1;
                validBitCount -= 1;
            }

            return dst;
        }

        private void DrawFullLayer(int layer, ushort[,] currentLayerData)
        {
            ushort tile;
            int tileValue = 0;
            //Draw top left quarter of map
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    tile = currentLayerData[layer, tileValue];
                    DrawTile(x, y, tile);
                    tileValue++;
                }
            }
            //Now draw top right corner
            for (int y = 0; y < 16; y++)
            {
                for (int x = 16; x < 32; x++)
                {
                    tile = currentLayerData[layer, tileValue];
                    DrawTile(x, y, tile);
                    tileValue++;
                }
            }
            //Now bottom left corner
            for (int y = 16; y < 32; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    tile = currentLayerData[layer, tileValue];
                    DrawTile(x, y, tile);
                    tileValue++;
                }
            }
            //And finally bottom right corner! Yosh!
            for (int y = 16; y < 32; y++)
            {
                for (int x = 16; x < 32; x++)
                {
                    tile = currentLayerData[layer, tileValue];
                    DrawTile(x, y, tile);
                    tileValue++;
                }
            }
            layersBufferGraphics.DrawImage(layersBackbuffer, 0, 0);
            layerGraphics.DrawImage(layersBuffer, 0, 0);
        }

        private unsafe void DrawTile(int x, int y, int tileNo)
        {
            int srcX, srcY, dstX, dstY;
            srcX = (tileNo % tileSize) * tileSize;
            srcY = (tileNo / tileSize) * tileSize;
            dstY = y * tileSize;
            dstX = x * tileSize;

            System.Drawing.Imaging.BitmapData tileSource = tilesetBitmap.LockBits(new Rectangle(srcX, srcY, 16, 16), System.Drawing.Imaging.ImageLockMode.ReadOnly, tilesetBitmap.PixelFormat);
            System.Drawing.Imaging.BitmapData lockedLayersBitmap = layersBackbuffer.LockBits(new Rectangle(dstX, dstY, 16, 16), System.Drawing.Imaging.ImageLockMode.WriteOnly, layersBackbuffer.PixelFormat);
            for (int py = 0; py < tileSource.Height; py++)
            {
                byte* srcRow = (byte*)tileSource.Scan0 + (py * tileSource.Stride);
                byte* dstRow = (byte*)lockedLayersBitmap.Scan0 + (py * lockedLayersBitmap.Stride);
                for (int px = 0; px < (lockedLayersBitmap.Width * 4); px++)
                {
                    dstRow[px] = srcRow[px];
                }
            }
            tilesetBitmap.UnlockBits(tileSource);
            layersBackbuffer.UnlockBits(lockedLayersBitmap);
        }

        private void layersPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            layersPictureBox_MouseMove(sender, e);

            //Only execute the actor moving code if the actor tab is selected
            if (tabControl1.SelectedTab.TabIndex == 2)
            {
                if (e.Button == MouseButtons.Left)
                {
                    int mouseCoordsX = e.X / 8;
                    int mouseCoordsY = e.Y / 8;

                    

                    for (int i = 0; i < actorsCheckListBox.Items.Count; i++)
                    {
                        if (actorsCheckListBox.GetItemChecked(i) == true)
                            if (actorData[i].xCoord == mouseCoordsX && actorData[i].yCoord == mouseCoordsY)
                            {
                                actorClickedOn = i;
                                actorsCheckListBox.SelectedIndex = i;
                                return;
                            }
                            else
                                actorClickedOn = 1000;
                    }
                }
            }
        }

        private void layersPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            //Only execute the actor moving code if the actor tab is selected
            //      AND a valid actor is selected
            //      AND the left button is down
            if (tabControl1.SelectedTab.TabIndex == 2 && actorClickedOn != 1000 && e.Button == MouseButtons.Left)
            {
                actorData[actorClickedOn].xCoord = (byte)(e.X / 8);
                actorData[actorClickedOn].yCoord = (byte)(e.Y / 8);
                actorTextboxXPos.Text = Convert.ToString(e.X / 8);
                actorTextboxYPos.Text = Convert.ToString(e.Y / 8);
                actorsHaveBeenChanged = true;

                layerGraphics.Clear(Color.FromArgb(0, 0, 0, 0));
                layerGraphics.DrawImage(layersBuffer, 0, 0);
                DrawActors(false);
            }


            //Only execute the tile code if the tilesheet tab is being used
            if (tabControl1.SelectedTab.TabIndex == 1)
            {
                int x = e.X / tileSize;
                int y = e.Y / tileSize;

                if (x < 0 || x >= layerWidth || y < 0 || y >= layerHeight)
                    return;
                debugText.Text = ("Tile coordinates: " + Convert.ToString(x) + " " + Convert.ToString(y));

                //If right button is pressed get the tile number
                if (e.Button == MouseButtons.Right)
                {
                    int layer = 0;
                    int tile;
                    //Work out which layer we are working on(highest ticked layer)
                    for (int i = 16; i > 0; i--)
                    {
                        if (layersCheckList.GetItemChecked(i - 1) == true)
                        {
                            layer = (i - 1);
                            break;
                        }
                    }
                    //work out which tile is clicked on and change the current tile
                    if (x < 16 && y < 16)
                        tile = (x + (y * 16));
                    else
                        if (x < 32 && y < 16)
                            tile = ((x - 16) + (y * 16) + 256);
                        else
                            if (x < 16 && y < 32)
                                tile = (x + ((y - 16) * 16) + 512);
                            else
                                tile = ((x - 16) + ((y - 16) * 16) + 768);
                    currentTile = szsData[layer, tile];

                    //Write the value into the currentTile Label and update the current tile image
                    currentTileLabel.Text = Convert.ToString(currentTile);
                    for (int px = 0; px < 16; px++)
                        for (int py = 0; py < 16; py++)
                        {
                            currentTileBitmap.SetPixel(px, py, tilesetBitmap.GetPixel(((currentTile % 16) * 16) + px, ((currentTile / 16) * 16) + py));
                        }
                    currentTilePictureBox.Refresh();
                }

                //If left button is pressed change the tile
                if (e.Button == MouseButtons.Left)
                {
                    layerDataHasChanged = true;
                    int layer = 0;
                    int tile;
                    //Work out which layer we are working on(highest ticked layer)
                    for (int i = 16; i > 0; i--)
                    {
                        if (layersCheckList.GetItemChecked(i - 1) == true)
                        {
                            layer = (i - 1);
                            break;
                        }
                    }
                    //work out which tile is changing
                    if (x < 16 && y < 16)
                        tile = (x + (y * 16));
                    else
                        if (x < 32 && y < 16)
                            tile = ((x - 16) + (y * 16) + 256);
                        else
                            if (x < 16 && y < 32)
                                tile = (x + ((y - 16) * 16) + 512);
                            else
                                tile = ((x - 16) + ((y - 16) * 16) + 768);

                    //Only run the undo sequence if the tile is different from the new tile
                    if ((ushort)currentTile != szsData[layer, tile])
                    {
                        //Save the current tile in the undo array
                        //If we are at the end of the array shunt each step back one
                        if (undoPosition > 49)
                        {
                            for (int entry = 0; entry < 49; entry++)
                            {
                                undoSteps[entry, 0] = undoSteps[entry + 1, 0];
                                undoSteps[entry, 1] = undoSteps[entry + 1, 1];
                                undoSteps[entry, 2] = undoSteps[entry + 1, 2];
                            }
                            undoPosition = 49;
                        }
                        //Load the current position with the current tile before it's changed
                        undoSteps[undoPosition, 0] = szsData[layer, tile];
                        undoSteps[undoPosition, 1] = (ushort)layer;
                        undoSteps[undoPosition, 2] = (ushort)tile;
                        undoPosition++;
                    }

                    //Change the tile
                    szsData[layer, tile] = (ushort)currentTile;

                    //If 2x2 brush is selected
                    if (brushSizeComboBox.SelectedIndex == 1)
                    {
                        if (x == 15 && y != 15 && y != 31)
                        {
                            szsData[layer, tile + 16] = (ushort)currentTile;
                            szsData[layer, tile + 241] = (ushort)currentTile;
                            if (y != 31)
                                szsData[layer, tile + 257] = (ushort)currentTile;
                        }
                        else
                        {
                            if (y == 15 && x != 15 && x != 31)
                            {
                                szsData[layer, tile + 1] = (ushort)currentTile;
                                szsData[layer, tile + 272] = (ushort)currentTile;
                                szsData[layer, tile + 273] = (ushort)currentTile;
                            }
                            else
                            {
                                if (y == 15 && x == 15)
                                {
                                    szsData[layer, tile + 241] = (ushort)currentTile;
                                    szsData[layer, tile + 272] = (ushort)currentTile;
                                    szsData[layer, tile + 513] = (ushort)currentTile;
                                }
                                else
                                {
                                    if (x != 31)
                                    {
                                        szsData[layer, tile + 1] = (ushort)currentTile;
                                        if (y != 31 && x != 31)
                                        {
                                            szsData[layer, tile + 16] = (ushort)currentTile;
                                            szsData[layer, tile + 17] = (ushort)currentTile;
                                        }
                                    }
                                }
                            }
                        }



                    }
                    UpdateMainWindow(false);
                    buttonSaveLayers.Enabled = true;
                }
            }
        }

        private void tilePictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            //When a TileSheet tile is clicked on load its value into currentTile
            currentTile = ((e.Y / 16) * 16) + (e.X / 16);

            //Write the value into the currentTile Label and update the current tile image
            currentTileLabel.Text = Convert.ToString(currentTile);
            for (int px = 0; px < 16; px++)
                for (int py = 0; py < 16; py++)
                {
                    currentTileBitmap.SetPixel(px, py, tilesetBitmap.GetPixel(((e.X / 16) * 16) + px, ((e.Y / 16) * 16) + py));
                }
            currentTilePictureBox.Refresh();
        }

        private void updateLayersButton_Click(object sender, EventArgs e)
        {
            UpdateMainWindow(false);
        }

        private void UpdateMainWindow(bool layers)
        {
            //Going to redraw all layers whos checkbox is ticked
            //Wipe graphics clean before we draw on top
            layerGraphics.Clear(Color.FromArgb(0, 0, 0, 0));
            layersBufferGraphics.Clear(Color.FromArgb(0, 0, 0, 0));

            for (int i = 0; i < 16; i++)//For each layer check box...
            {
                if (layersCheckList.GetItemChecked(i) == true)//Draw if it's checked
                {
                    DrawFullLayer(i, szsData);
                }
            }
            //Don't forget to redraw actors as they were lost in the graphics wipe too!
            DrawActors(layers);
        }

        private void SaveSZSData()
        {
            byte[] reversedSZSData = new byte[2048];
            
            for (int layerNumber = 0; layerNumber < 16; layerNumber++)//Do each layer
            {
                for (int i = 0; i < 1024; i++)
                {
                    //Put the ushorts back into reverse order bytes
                    reversedSZSData[i * 2] = (byte)(szsData[layerNumber, i] & 0xFF);
                    reversedSZSData[i * 2 + 1] = (byte)((szsData[layerNumber, i] >> 8) & 0xFF);
                }
                FileStream writeSZSFile = File.Create(szsFilesPaths[layerNumber], reversedSZSData.Length);
                writeSZSFile.Write(reversedSZSData, 0, reversedSZSData.Length);
                writeSZSFile.Flush();
                writeSZSFile.Close();

                layerDataHasChanged = false;
            }
        }


        private void LoadActors()
        {
            //Set up the filepath and read the file into the buffer
            actorsFilePath = filepathTextBox.Text + @"bin\b" + csvMapNumber + @"\d_enemy_" + csvMapName + "_" + room + ".bin";

            try
            {
                byte[] readBuffer = File.ReadAllBytes(actorsFilePath);
                char[] name = new char[4];

                //Skip the last null entry
                actorData = new actor[(readBuffer.Length / 11) - 1];

                //Transfer from Buffer into array
                for (int i = 0; i < actorData.Length; i++)
                {
                    name[0] = (char)readBuffer[i * 11];
                    name[1] = (char)readBuffer[i * 11 + 1];
                    name[2] = (char)readBuffer[i * 11 + 2];
                    name[3] = (char)readBuffer[i * 11 + 3];
                    actorData[i].name = new string(name);
                    actorData[i].layer = readBuffer[i * 11 + 4];
                    actorData[i].xCoord = readBuffer[i * 11 + 5];
                    actorData[i].yCoord = readBuffer[i * 11 + 6];
                    actorData[i].variable1 = readBuffer[i * 11 + 7];
                    actorData[i].variable2 = readBuffer[i * 11 + 8];
                    actorData[i].variable3 = readBuffer[i * 11 + 9];
                    actorData[i].variable4 = readBuffer[i * 11 + 10];
                    char a1 = 'a';
                    char b2 = 'b';
                    string a11 = a1.ToString() + b2;
                }
                DisplayActorData();
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show("Can't find actors *.bin. \nA new actors file has been created.");
                actorData = new actor[0];
            }

            //Enable all the actor buttons now that data to work with exists
            actorDeleteButton.Enabled = true;
            actorsAddNewButton.Enabled = true;
            actorsSaveButton.Enabled = true;
            actorsUpdateButton.Enabled = true;
            actorsReloadButton.Enabled = true;
            actorLayerComboBox.Enabled = true;

        }

        private void DisplayActorData()
        {
            //Reset the checkbox list
            actorsCheckListBox.Items.Clear();

            //Go through all the actor data and populate the checklistbox
            for (int i = 0; i < actorData.Length; i++)
            {
                actorsCheckListBox.Items.Add(actorData[i].name, false);
                actorsCheckListBox.SelectedIndex = 0;
            }
        }

        private void SaveActors()
        {
            FileStream writeActors = File.Create(actorsFilePath, actorData.Length);

            for (int i = 0; i < actorData.Length; i++)
            {
                writeActors.WriteByte((byte)actorData[i].name[0]);
                writeActors.WriteByte((byte)actorData[i].name[1]);
                writeActors.WriteByte((byte)actorData[i].name[2]);
                writeActors.WriteByte((byte)actorData[i].name[3]);
                writeActors.WriteByte(actorData[i].layer);
                writeActors.WriteByte(actorData[i].xCoord);
                writeActors.WriteByte(actorData[i].yCoord);
                writeActors.WriteByte(actorData[i].variable1);
                writeActors.WriteByte(actorData[i].variable2);
                writeActors.WriteByte(actorData[i].variable3);
                writeActors.WriteByte(actorData[i].variable4);
            }
            //Final null entry
            writeActors.WriteByte(0x20);
            writeActors.WriteByte(0x20);
            writeActors.WriteByte(0x20);
            writeActors.WriteByte(0x20);
            writeActors.WriteByte(0x00);
            writeActors.WriteByte(0x00);
            writeActors.WriteByte(0x00);
            writeActors.WriteByte(0x00);
            writeActors.WriteByte(0x00);
            writeActors.WriteByte(0x00);
            writeActors.WriteByte(0x00);

            writeActors.Flush();
            writeActors.Close();

            actorsHaveBeenChanged = false;
        }

        private void actorsReloadButton_Click(object sender, EventArgs e)
        {
            LoadActors();
        }

        private void actorsCheckListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            NewActorSelected();
        }

        private void NewActorSelected()
        {
            //Set each of the Actor Atributes boxes with the appropriate value
            ActorNameComboBox.SelectedIndex = actorNumber(actorData[actorsCheckListBox.SelectedIndex].name);
            actorTextboxLayer.Text = Convert.ToString(actorData[actorsCheckListBox.SelectedIndex].layer);
            actorTextboxXPos.Text = Convert.ToString(actorData[actorsCheckListBox.SelectedIndex].xCoord);
            actorTextboxYPos.Text = Convert.ToString(actorData[actorsCheckListBox.SelectedIndex].yCoord);
            actorTextboxU1.Text = Convert.ToString(actorData[actorsCheckListBox.SelectedIndex].variable1);
            actorTextboxVariable.Text = Convert.ToString(actorData[actorsCheckListBox.SelectedIndex].variable2);
            actorTextboxUN2.Text = Convert.ToString(actorData[actorsCheckListBox.SelectedIndex].variable3);
            actorTextboxUN3.Text = Convert.ToString(actorData[actorsCheckListBox.SelectedIndex].variable4);

            //Load the actor info txt into the box
            string exepath = Application.ExecutablePath;
            string directory = Path.GetDirectoryName(exepath);
            string name = actorsCheckListBox.Text;
            string path = directory + @"\actorinfo\" + name + ".txt";
            ActorInfoTextBox.Clear();
            try
            {
                string info = File.ReadAllText(path);
                ActorInfoTextBox.Text = info;
            }
            catch (FileNotFoundException)
            {
                debugText.AppendText("\r\nFile " + name + ".txt not found.");
            }

            //ActorDrawTile(4, 4, ActorNameComboBox.SelectedIndex, currentActorBitmap);
            string actorImgPath = directory + "\\data\\actors\\" + name + ".png";
            try
            {
                currentActorBitmap = new Bitmap(actorImgPath);
            }
            catch (ArgumentException)
            {
                return;
            }
            ActorInfoPictureBox.Image = currentActorBitmap;
        }

        private void DrawActors(bool updatingLayer)
        {

            int x;
            int y;
            //Let's wipe the actor bitmaps clean before we draw on it again
            Graphics actorGraphics = Graphics.FromImage(actorsBackbuffer);
            actorGraphics.Clear(Color.FromArgb(0, 0, 0, 0));
            actorsBufferGraphics.Clear(Color.FromArgb(0, 0, 0, 0));

            if (updatingLayer == true)
                //reset all the check boxes
                for (int e = 0; e < actorsCheckListBox.Items.Count; e++)
                    actorsCheckListBox.SetItemChecked(e, false);

            //Go through all actor data
            for (int i = 0; i < actorData.Length; i++)
            {
                //If we are here because the layer box has been changed then draw all actors for that layer
                if (updatingLayer == true)
                {
                    //Only draw actors for the selected layer
                    if ((int)actorData[i].layer == actorLayerComboBox.SelectedIndex)
                    {
                        //Check boxes of all actors that are being rendered
                        actorsCheckListBox.SetItemChecked(i, true);

                        string name = actorData[i].name.ToString();

                        x = (Convert.ToInt32(actorData[i].xCoord));
                        y = (Convert.ToInt32(actorData[i].yCoord));
                        //Draw the sprite
                        ActorDrawTile(x, y, actorsBackbuffer, name);
                    }
                }
                //Otherwise only draw actors whose box is checked
                else
                {
                    if (actorsCheckListBox.GetItemChecked(i) == true)
                    {
                        x = (int)actorData[i].xCoord;
                        y = (int)actorData[i].yCoord;
                        //Draw the sprite
                        ActorDrawTile(x, y, actorsBackbuffer, actorData[i].name);
                    }
                }
            }
            layerGraphics.DrawImage(actorsBuffer, 0, 0);
            layersPictureBox.Refresh();
        }

        private int actorNumber(string currentActorName)
        {
            //Go through the list of actor names to find out which one we are dealing with
            int i = 0;

            while (actorNames[i] != currentActorName)
                i++;

            return i;
        }

        private void actorLayerComboBox_SelectionChangeCommitted_1(object sender, EventArgs e)
        {
            UpdateMainWindow(true);
        }

        private void ActorDrawTile(int x, int y, Bitmap destinationImage, string actorName)
        {
            string actorImgPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
            actorImgPath = actorImgPath.Remove(0, 6);
            actorImgPath = actorImgPath + "\\data\\actors\\" + actorName + ".png";
            try
            {
                actorSpritesBitmap = new Bitmap(actorImgPath);
            }
            catch (ArgumentException)
            {
                debugText.AppendText("\n File " + actorImgPath + " not found!");
                return;
            }

            int imageSizeX = actorSpritesBitmap.Width;
            int imageSizeY = actorSpritesBitmap.Height;

            int  dstX, dstY;
            //where to put pixels..
            dstY = y * 8 + ((imageSizeY / 2) - 1);
            dstX = x * 8 - ((imageSizeX / 2) + 1);
            for (int py = 0; py < imageSizeY; py++)
            {
                int SrcPy = ((imageSizeY - 1) - py);

                //If it's about to try and draw outside of the buffer(Y) then skip this pixel
                if ((dstY - py) < 0 || (dstY - py) > 511)
                    continue;

                for (int px = imageSizeX; px > 0; px--)
                {
                    //If it's about to try and draw outside of the buffer(X) then skip this pixel
                    if ((dstX + px) > 511 || (dstX + px) < 0)
                        continue;
                    destinationImage.SetPixel(dstX + px, dstY - py, actorSpritesBitmap.GetPixel(px - 1, SrcPy));
                }
            }
            actorsBufferGraphics.DrawImage(actorsBackbuffer, 0, 0);
        }

        private void actorsUpdateButton_Click(object sender, EventArgs e)
        {
            actorsHaveBeenChanged = true;

            actorsUpdateButton.BackColor = System.Drawing.Color.Transparent;

            //First save the name
            actorData[actorsCheckListBox.SelectedIndex].name = actorNames[ActorNameComboBox.SelectedIndex];

            //Put the numbers from the boxes into the actor data array
            //Check that the value is not too high
            int value = Convert.ToByte(actorTextboxLayer.Text);
            if (value < 8)
                actorData[actorsCheckListBox.SelectedIndex].layer = (byte)value;
            else
                MessageBox.Show("A value of " + Convert.ToString(value) + " is invalid, must be 0 - 7.", "Too High");

            value = Convert.ToByte(actorTextboxXPos.Text);
            if (value < 64)
                actorData[actorsCheckListBox.SelectedIndex].xCoord = (byte)value;
            else
                MessageBox.Show("A value of " + Convert.ToString(value) + " is invalid, must be 0 - 63.", "Too High");

            value = Convert.ToByte(actorTextboxYPos.Text);
            if (value < 64)
                actorData[actorsCheckListBox.SelectedIndex].yCoord = (byte)value;
            else
                MessageBox.Show("A value of " + Convert.ToString(value) + " is invalid, must be 0 - 63.", "Too High");

            value = Convert.ToInt32(actorTextboxU1.Text);
            if (value < 256)
                actorData[actorsCheckListBox.SelectedIndex].variable1 = (byte)value;
            else
                MessageBox.Show("A value of " + Convert.ToString(value) + " is invalid, must be 0 - 255.", "Too High");

            value = Convert.ToInt32(actorTextboxVariable.Text);
            if (value < 256)
                actorData[actorsCheckListBox.SelectedIndex].variable2 = (byte)value;
            else
                MessageBox.Show("A value of " + Convert.ToString(value) + " is invalid, must be 0 - 255.", "Too High");

            value = Convert.ToInt32(actorTextboxUN2.Text);
            if (value < 256)
                actorData[actorsCheckListBox.SelectedIndex].variable3 = (byte)value;
            else
                MessageBox.Show("A value of " + Convert.ToString(value) + " is invalid, must be 0 - 255.", "Too High");

            value = Convert.ToInt32(actorTextboxUN3.Text);
            if (value < 256)
                actorData[actorsCheckListBox.SelectedIndex].variable4 = (byte)value;
            else
                MessageBox.Show("A value of " + Convert.ToString(value) + " is invalid, must be 0 - 255.", "Too High");

            DisplayActorData();
            UpdateMainWindow(true);
        }

        private void actorsSaveButton_Click(object sender, EventArgs e)
        {
            SaveActors();
            MessageBox.Show("Changes Saved");
        }

        private void actorsAddNewButton_Click(object sender, EventArgs e)
        {
            actor[] tempData = new actor[actorData.Length + 1];

            for (int i = 0; i < actorData.Length; i++)
            {
                tempData[i].name = actorData[i].name;
                tempData[i].layer = actorData[i].layer;
                tempData[i].xCoord = actorData[i].xCoord;
                tempData[i].yCoord = actorData[i].yCoord;
                tempData[i].variable1 = actorData[i].variable1;
                tempData[i].variable2 = actorData[i].variable2;
                tempData[i].variable3 = actorData[i].variable3;
                tempData[i].variable4 = actorData[i].variable4;
            }

            tempData[actorData.Length].name = actorNames[0];
            tempData[actorData.Length].layer = 0x00;
            tempData[actorData.Length].xCoord = 0x00;
            tempData[actorData.Length].yCoord = 0x00;
            tempData[actorData.Length].variable1 = 0x00;
            tempData[actorData.Length].variable2 = 0x00;
            tempData[actorData.Length].variable3 = 0x00;
            tempData[actorData.Length].variable4 = 0x00;

            actorData = tempData;
            DisplayActorData();
            DrawActors(true);
        }

        private void actorDeleteButton_Click(object sender, EventArgs e)
        {
            if (actorData.Length > 0)
            {
                actorsHaveBeenChanged = true;

                //create the temporary buffer and number
                actor[] tempData = new actor[actorData.Length - 1];
                int bonus = 0;

                for (int i = 0; i < actorData.Length; i++)
                {
                    //if NOT the selected actor then copy it across
                    if (i != (actorsCheckListBox.SelectedIndex))
                    {
                        tempData[i - bonus].name = actorData[i].name;
                        tempData[i - bonus].layer = actorData[i].layer;
                        tempData[i - bonus].xCoord = actorData[i].xCoord;
                        tempData[i - bonus].yCoord = actorData[i].yCoord;
                        tempData[i - bonus].variable1 = actorData[i].variable1;
                        tempData[i - bonus].variable2 = actorData[i].variable2;
                        tempData[i - bonus].variable3 = actorData[i].variable3;
                        tempData[i - bonus].variable4 = actorData[i].variable4;
                    }
                    else
                        bonus = 1;
                }

                actorData = tempData;
            }

            //Rebuild the actor checklist box
            DisplayActorData();

            UpdateMainWindow(false);
        }



        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void comboBox1_SelectionChangeCommitted(object sender, EventArgs e)
        {
            ChangeTileSet();
        }

        private void ChangeTileSet()
        {
            string tileSheet = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
            tileSheet = tileSheet.Remove(0, 6);

            //Change the tilesheet to the one selected
            if (comboBox1.SelectedIndex < 10)
                tileSheet += @"\data\Tile Sheet 0" + comboBox1.SelectedIndex + ".PNG";
            else
                tileSheet += @"\data\Tile Sheet " + comboBox1.SelectedIndex + ".PNG";
            Bitmap one = new Bitmap(tileSheet);
            Graphics gr = Graphics.FromImage(tilesetBitmap);
            gr.Clear(Color.FromArgb(00000000));
            gr.DrawImage(one, 0, 0);
            tilePictureBox.Image = tilesetBitmap;
        }

        private void buttonActorsSelectNone_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < actorsCheckListBox.Items.Count; i++)
                actorsCheckListBox.SetItemChecked(i, false);
        }

        private void buttonSaveLayers_Click(object sender, EventArgs e)
        {
            SaveSZSData();
            MessageBox.Show("Changes Saved");
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
                switch (MessageBox.Show("Save all data before exporting?",
                            "Save changes?",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question))
                {
                    case DialogResult.Yes:
                        SaveActors();
                        SaveSZSData();
                        csvSaveButton_Click();
                        break;

                    case DialogResult.No:
                        break;
                }

            SaveFileDialog saveRarc = new SaveFileDialog();
            saveRarc.DefaultExt = ".arc";
            saveRarc.AddExtension = true;
            saveRarc.Filter = "RARC files|*.arc";
            string name = "boss" + csvMapName[3] + csvMapName[4] + csvMapName[5];
            saveRarc.FileName = name;
            string saveDestination = ".";
            if (saveRarc.ShowDialog() == DialogResult.OK)
            {
                saveDestination = saveRarc.FileName;

                string[] folderPath = new string[1];
                folderPath[0] = filepathTextBox.Text.Remove((filepathTextBox.Text.Length - 1));
                RarcPacker packer = new RarcPacker();
                packer.Rarc(folderPath,saveDestination);
            }
            
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Editor for Four Swords Adventures by JaytheHam. v1.0\nwww.jaytheham.com\nSend comments, bug reports etc to: jaytheham@gmail.com", "EFSAdvent version 1.0");
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Undo();
        }

        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Redo();
        }

        private void Undo()
        {
            if (undoPosition == 0)
                return;
            undoPosition--;
            ushort oldTile = szsData[undoSteps[undoPosition, 1], undoSteps[undoPosition, 2]];
            szsData[undoSteps[undoPosition, 1], undoSteps[undoPosition, 2]] = undoSteps[undoPosition, 0];
            undoSteps[undoPosition, 0] = oldTile;

            UpdateMainWindow(false);
        }

        private void Redo()
        {
            if (undoPosition == 49)
                return;
            ushort oldTile = szsData[undoSteps[undoPosition, 1], undoSteps[undoPosition, 2]];
            szsData[undoSteps[undoPosition, 1], undoSteps[undoPosition, 2]] = undoSteps[undoPosition, 0];
            undoSteps[undoPosition, 0] = oldTile;
            undoPosition++;
            UpdateMainWindow(false);
        }

        private void layersPictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            //When the mouse button is released select no actor so that a new one can be selected
            if (tabControl1.SelectedTab.TabIndex == 2)
                actorClickedOn = 1000;
        }

        private void csvSaveButton_Click()
        {

        }

        private void actorChanged(object sender, KeyPressEventArgs e)
        {
            actorsUpdateButton.BackColor = System.Drawing.Color.FromArgb(80, 255, 0, 0);
        }

        private void actorChanged2(object sender, EventArgs e)
        {
            actorsUpdateButton.BackColor = System.Drawing.Color.FromArgb(80, 255, 0, 0);
        }


    }
}
