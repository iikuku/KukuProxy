using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace kukuProxy
{
    public partial class Form1 : Form
    {
        private Server proxy = new Server();
        
        public Form1()
        {
            InitializeComponent();

        }

        public void Addtxt(string str)
        {
            //lambda 表达式看傻了吧
            textBox1.Invoke(new Action(() => {
                textBox1.AppendText("[" + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "]" + str + "\r\n"); 
            }));
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            button2.Enabled = true;
            proxy.Port = 1234;
            proxy.OutputFunc = this.Addtxt;
            proxy.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            button1.Enabled = true;
            button2.Enabled = false;
            proxy.Stop();
        }
    }
}
