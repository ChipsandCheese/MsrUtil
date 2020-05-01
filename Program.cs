﻿using PmcReader.Intel;
using PmcReader.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PmcReader
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Ring0.Open();
            OpCode.Open();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HaswellForm());
            OpCode.Close();
            Ring0.Close();
        }
    }
}
