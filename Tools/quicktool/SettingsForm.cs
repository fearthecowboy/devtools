﻿//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011  Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------
using CoApp.Toolkit.Configuration;

namespace QuickTool {
    using System.Windows.Forms;
    using Properties;

    public partial class SettingsForm : Form {
        public SettingsForm() {
            InitializeComponent();
            LoadSettings();

            buttonOK.Click += (x, y) => {
                SaveSettings();
                Hide();
            };

            buttonCancel.Click += (x, y) => Hide();

            btnSetQuickUploaderHotkey.Click += (x, y) => {
                quickUploaderHotkeyLabel.Text = Resources.txt_PRESS_KEY_COMBINATION;
                btnSetQuickUploaderHotkey.KeyDown += SettingsFormKeyDown;
                btnSetQuickUploaderHotkey.KeyUp += SettingsFormKeyUp;
            };

            btnSetSourceUploaderHotkey.Click += (x, y) => {
                quickSourceHotkeyLabel.Text = Resources.txt_PRESS_KEY_COMBINATION;
                btnSetSourceUploaderHotkey.KeyDown += SettingsFormKeyDown;
                btnSetSourceUploaderHotkey.KeyUp += SettingsFormKeyUp;
            };

            btnSetManualBitlyHotkey.Click += (x, y) => {
                manualBitlylabel.Text = Resources.txt_PRESS_KEY_COMBINATION;
                btnSetManualBitlyHotkey.KeyDown += SettingsFormKeyDown;
                btnSetManualBitlyHotkey.KeyUp += SettingsFormKeyUp;
            };

            FormClosing += (x, y) => {
                y.Cancel = true;
                Hide();
            };
        }

        public static string KeyToText(Keys key) {
            #region BIGSWITCH

            switch (key) {
                case Keys.None:
                    return "None";
                case Keys.LButton:
                    return "LButton ";
                case Keys.RButton:
                    return "RButton";
                case Keys.Cancel:
                    return "Cancel";
                case Keys.MButton:
                    return "MButton";
                case Keys.XButton1:
                    return "XButton1";
                case Keys.XButton2:
                    return "XButton2";
                case Keys.Back:
                    return "Back";
                case Keys.Tab:
                    return "Tab";
                case Keys.LineFeed:
                    return "LineFeed";
                case Keys.Clear:
                    return "Clear";
                case Keys.Enter:
                    return "Enter";
                case Keys.ShiftKey:
                    return "ShiftKey";
                case Keys.ControlKey:
                    return "ControlKey";
                case Keys.Menu:
                    return "Menu";
                case Keys.Pause:
                    return "Pause";
                case Keys.CapsLock:
                    return "CapsLock";
                case Keys.KanaMode:
                    return "KanaMode";
                case Keys.JunjaMode:
                    return "JunjaMode";
                case Keys.FinalMode:
                    return "FinalMode";
                case Keys.KanjiMode:
                    return "KanjiMode";
                case Keys.Escape:
                    return "Escape";
                case Keys.IMEConvert:
                    return "IMEConvert";
                case Keys.IMENonconvert:
                    return "IMENonconvert";
                case Keys.IMEAceept:
                    return "IMEAceept";
                case Keys.IMEModeChange:
                    return "IMEModeChange";
                case Keys.Space:
                    return "Space";
                case Keys.PageUp:
                    return "PageUp";
                case Keys.PageDown:
                    return "PageDown";
                case Keys.End:
                    return "End";
                case Keys.Home:
                    return "Home";
                case Keys.Left:
                    return "Left";
                case Keys.Up:
                    return "Up";
                case Keys.Right:
                    return "Right";
                case Keys.Down:
                    return "Down";
                case Keys.Select:
                    return "Select";
                case Keys.Print:
                    return "Print";
                case Keys.Execute:
                    return "Execute";
                case Keys.PrintScreen:
                    return "PrintScreen";
                case Keys.Insert:
                    return "Insert";
                case Keys.Delete:
                    return "Delete";
                case Keys.Help:
                    return "Help";
                case Keys.D0:
                    return "D0";
                case Keys.D1:
                    return "D1";
                case Keys.D2:
                    return "D2";
                case Keys.D3:
                    return "D3";
                case Keys.D4:
                    return "D4";
                case Keys.D5:
                    return "D5";
                case Keys.D6:
                    return "D6";
                case Keys.D7:
                    return "D7";
                case Keys.D8:
                    return "D8";
                case Keys.D9:
                    return "D9";
                case Keys.A:
                    return "A";
                case Keys.B:
                    return "B";
                case Keys.C:
                    return "C";
                case Keys.D:
                    return "D";
                case Keys.E:
                    return "E";
                case Keys.F:
                    return "F";
                case Keys.G:
                    return "G";
                case Keys.H:
                    return "H";
                case Keys.I:
                    return "I";
                case Keys.J:
                    return "J";
                case Keys.K:
                    return "K";
                case Keys.L:
                    return "L";
                case Keys.M:
                    return "M";
                case Keys.N:
                    return "N";
                case Keys.O:
                    return "O";
                case Keys.P:
                    return "P";
                case Keys.Q:
                    return "Q";
                case Keys.R:
                    return "R";
                case Keys.S:
                    return "S";
                case Keys.T:
                    return "T";
                case Keys.U:
                    return "U";
                case Keys.V:
                    return "V";
                case Keys.W:
                    return "W";
                case Keys.X:
                    return "X";
                case Keys.Y:
                    return "Y";
                case Keys.Z:
                    return "Z";
                case Keys.LWin:
                    return "LWin";
                case Keys.RWin:
                    return "RWin";
                case Keys.Apps:
                    return "Apps";
                case Keys.Sleep:
                    return "Sleep";
                case Keys.NumPad0:
                    return "NumPad0";
                case Keys.NumPad1:
                    return "NumPad1";
                case Keys.NumPad2:
                    return "NumPad2";
                case Keys.NumPad3:
                    return "NumPad3";
                case Keys.NumPad4:
                    return "NumPad4";
                case Keys.NumPad5:
                    return "NumPad5";
                case Keys.NumPad6:
                    return "NumPad6";
                case Keys.NumPad7:
                    return "NumPad7";
                case Keys.NumPad8:
                    return "NumPad8";
                case Keys.NumPad9:
                    return "NumPad9";
                case Keys.Multiply:
                    return "Multiply";
                case Keys.Add:
                    return "Add";
                case Keys.Separator:
                    return "Separator";
                case Keys.Subtract:
                    return "Subtract";
                case Keys.Decimal:
                    return "Decimal";
                case Keys.Divide:
                    return "Divide";
                case Keys.F1:
                    return "F1";
                case Keys.F2:
                    return "F2";
                case Keys.F3:
                    return "F3";
                case Keys.F4:
                    return "F4";
                case Keys.F5:
                    return "F5";
                case Keys.F6:
                    return "F6";
                case Keys.F7:
                    return "F7";
                case Keys.F8:
                    return "F8";
                case Keys.F9:
                    return "F9";
                case Keys.F10:
                    return "F10";
                case Keys.F11:
                    return "F11";
                case Keys.F12:
                    return "F12";
                case Keys.F13:
                    return "F13";
                case Keys.F14:
                    return "F14";
                case Keys.F15:
                    return "F15";
                case Keys.F16:
                    return "F16";
                case Keys.F17:
                    return "F17";
                case Keys.F18:
                    return "F18";
                case Keys.F19:
                    return "F19";
                case Keys.F20:
                    return "F20";
                case Keys.F21:
                    return "F21";
                case Keys.F22:
                    return "F22";
                case Keys.F23:
                    return "F23";
                case Keys.F24:
                    return "F24";
                case Keys.NumLock:
                    return "NumLock";
                case Keys.Scroll:
                    return "Scroll";
                case Keys.LShiftKey:
                    return "LShiftKey";
                case Keys.RShiftKey:
                    return "RShiftKey";
                case Keys.LControlKey:
                    return "LControlKey";
                case Keys.RControlKey:
                    return "RControlKey";
                case Keys.LMenu:
                    return "LMenu";
                case Keys.RMenu:
                    return "RMenu";
                case Keys.BrowserBack:
                    return "BrowserBack";
                case Keys.BrowserForward:
                    return "BrowserForward";
                case Keys.BrowserRefresh:
                    return "BrowserRefresh";
                case Keys.BrowserStop:
                    return "BrowserStop";
                case Keys.BrowserSearch:
                    return "BrowserSearch";
                case Keys.BrowserFavorites:
                    return "BrowserFavorites";
                case Keys.BrowserHome:
                    return "BrowserHome";
                case Keys.VolumeMute:
                    return "VolumeMute";
                case Keys.VolumeDown:
                    return "VolumeDown";
                case Keys.VolumeUp:
                    return "VolumeUp";
                case Keys.MediaNextTrack:
                    return "MediaNextTrack";
                case Keys.MediaPreviousTrack:
                    return "MediaPreviousTrack";
                case Keys.MediaStop:
                    return "MediaStop";
                case Keys.MediaPlayPause:
                    return "MediaPlayPause";
                case Keys.LaunchMail:
                    return "LaunchMail";
                case Keys.SelectMedia:
                    return "SelectMedia";
                case Keys.LaunchApplication1:
                    return "LaunchApplication1";
                case Keys.LaunchApplication2:
                    return "LaunchApplication2";
                case Keys.OemSemicolon:
                    return "OemSemicolon";
                case Keys.Oemplus:
                    return "Oemplus";
                case Keys.Oemcomma:
                    return "Oemcomma";
                case Keys.OemMinus:
                    return "OemMinus";
                case Keys.OemPeriod:
                    return "OemPeriod";
                case Keys.OemQuestion:
                    return "OemQuestion";
                case Keys.Oemtilde:
                    return "Oemtilde";
                case Keys.OemOpenBrackets:
                    return "OemOpenBrackets";
                case Keys.OemPipe:
                    return "OemPipe";
                case Keys.OemCloseBrackets:
                    return "OemCloseBrackets";
                case Keys.OemQuotes:
                    return "OemQuotes";
                case Keys.Oem8:
                    return "Oem8";
                case Keys.OemBackslash:
                    return "OemBackslash";
                case Keys.ProcessKey:
                    return "ProcessKey";
                case Keys.Packet:
                    return "Packet";
                case Keys.Attn:
                    return "Attn";
                case Keys.Crsel:
                    return "Crsel";
                case Keys.Exsel:
                    return "Exsel";
                case Keys.EraseEof:
                    return "EraseEof";
                case Keys.Play:
                    return "Play";
                case Keys.Zoom:
                    return "Zoom";
                case Keys.NoName:
                    return "NoName";
                case Keys.Pa1:
                    return "Pa1";
                case Keys.OemClear:
                    return "OemClear";
                case Keys.KeyCode:
                    return "KeyCode";
                case Keys.Shift:
                    return "Shift";
                case Keys.Control:
                    return "Control";
                case Keys.Alt:
                    return "Alt";
                default:
                    return "";
            }

            #endregion
        }

        public void SaveSettings() {
            RegistryView.ApplicationUser["#bit.ly-username"].StringValue = bitlyUserId.Text;
            RegistryView.ApplicationUser["#bit.ly-password"].EncryptedStringValue = bitlyApiKey.Text;

            RegistryView.ApplicationUser["#ftp-server"].StringValue = ftpServer.Text;
            RegistryView.ApplicationUser["#ftp-username"].StringValue = ftpUsername.Text;
            RegistryView.ApplicationUser["#ftp-password"].EncryptedStringValue = ftpPassword.Text;
            RegistryView.ApplicationUser["#ftp-folder"].StringValue = ftpFolder.Text;
            RegistryView.ApplicationUser["#image-filename-template"].StringValue = imageFilename.Text;
            RegistryView.ApplicationUser["#image-finishedurl-template"].StringValue = httpUrlTemplate.Text;
            RegistryView.ApplicationUser["#enable-audio-cues"].BoolValue = cbAudioCues.Checked;
            RegistryView.ApplicationUser["#enable-auto-bitly"].BoolValue = cbAutoBitly.Checked;

            if (quickUploaderHotkeyLabel.Text != Resources.txt_PRESS_KEY_COMBINATION) {
                RegistryView.ApplicationUser["#quick-uploader-hotkey"].StringValue = quickUploaderHotkeyLabel.Text;
            }

            if (quickSourceHotkeyLabel.Text != Resources.txt_PRESS_KEY_COMBINATION) {
                RegistryView.ApplicationUser["#quick-source-hotkey"].StringValue = quickSourceHotkeyLabel.Text;
            }

            if (manualBitlylabel.Text != Resources.txt_PRESS_KEY_COMBINATION) {
                RegistryView.ApplicationUser["#manual-bitly-hotkey"].StringValue = manualBitlylabel.Text;
            }
        }

        public void LoadSettings() {
            bitlyUserId.Text = RegistryView.ApplicationUser["#bit.ly-username"].StringValue ?? "";
            bitlyApiKey.Text = RegistryView.ApplicationUser["#bit.ly-password"].EncryptedStringValue ?? "";

            ftpServer.Text = RegistryView.ApplicationUser["#ftp-server"].StringValue ?? "";
            ftpUsername.Text = RegistryView.ApplicationUser["#ftp-username"].StringValue ?? "";
            ftpPassword.Text = RegistryView.ApplicationUser["#ftp-password"].EncryptedStringValue ?? "";
            ftpFolder.Text = RegistryView.ApplicationUser["#ftp-folder"].StringValue ?? "";
            imageFilename.Text = RegistryView.ApplicationUser["#image-filename-template"].StringValue ?? "file-{date}-{time}-{counter}.png";
            httpUrlTemplate.Text = RegistryView.ApplicationUser["#image-finishedurl-template"].StringValue ?? "http://servername.com/path/to/{0}";
            cbAudioCues.Checked = RegistryView.ApplicationUser["#enable-audio-cues"].BoolValue;
            cbAutoBitly.Checked = RegistryView.ApplicationUser["#enable-auto-bitly"].BoolValue;
            quickUploaderHotkeyLabel.Text = RegistryView.ApplicationUser["#quick-uploader-hotkey"].StringValue ?? "Alt+Control+NumPad9";
            quickSourceHotkeyLabel.Text = RegistryView.ApplicationUser["#quick-source-hotkey"].StringValue ?? "Alt+Control+NumPad6";
            manualBitlylabel.Text = RegistryView.ApplicationUser["#manual-bitly-hotkey"].StringValue ?? "Alt+Control+NumPad3";
        }

        private void SettingsFormKeyDown(object sender, KeyEventArgs e) {
            Keys win32Key = e.KeyCode & Keys.KeyCode;

            if ((e.Control & e.KeyCode == Keys.ControlKey) || (e.Alt & e.KeyCode == Keys.Menu) || (e.Shift & e.KeyCode == Keys.ShiftKey)) {
                return;
            }

            string newKey = KeyToText(win32Key);

            if (e.Control) {
                newKey = "Control+" + newKey;
            }
            if (e.Alt) {
                newKey = "Alt+" + newKey;
            }
            if (e.Shift) {
                newKey = "Shift+" + newKey;
            }

            if (sender == btnSetSourceUploaderHotkey) {
                quickSourceHotkeyLabel.Text = newKey;
            }

            if (sender == btnSetQuickUploaderHotkey) {
                quickUploaderHotkeyLabel.Text = newKey;
            }

            if (sender == btnSetManualBitlyHotkey) {
                manualBitlylabel.Text = newKey;
            }
        }

        private void SettingsFormKeyUp(object sender, KeyEventArgs e) {
            btnSetQuickUploaderHotkey.KeyDown -= SettingsFormKeyDown;
            btnSetQuickUploaderHotkey.KeyUp -= SettingsFormKeyUp;

            btnSetSourceUploaderHotkey.KeyDown -= SettingsFormKeyDown;
            btnSetSourceUploaderHotkey.KeyUp -= SettingsFormKeyUp;
        }
    }
}