﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace chess_pos_db_gui
{
    public partial class FenInputForm : Form
    {
        public bool WasCancelled { get; private set; }
        public string Fen { get; private set; }

        public FenInputForm()
        {
            InitializeComponent();

            WasCancelled = false;
            Fen = "";
            if (Clipboard.ContainsText())
            {
                fenTextBox.Text = Clipboard.GetText();
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            WasCancelled = true;
            Close();
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            WasCancelled = false;
            Fen = fenTextBox.Text;
            Close();
        }
    }
}