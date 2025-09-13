using System;
using System.Windows.Forms;

namespace MoggadoSim
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new SimulatedMeltForm());
        }
    }
}
